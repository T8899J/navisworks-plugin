using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// Executes "hide unselected" through the Navisworks kernel selection APIs.
    /// </summary>
    public static class HideService
    {
        public static bool HideUnselected(
            Document doc,
            IEnumerable<ModelItem> matchedItems,
            int totalMatchedCount,
            DiagnosticLogSession diagnosticLog = null)
        {
            var matchedItemList = new List<ModelItem>();
            var matchedGuids = new HashSet<Guid>();
            if (matchedItems != null)
            {
                foreach (ModelItem item in matchedItems)
                {
                    if (item == null)
                        continue;

                    matchedItemList.Add(item);
                    matchedGuids.Add(item.InstanceGuid);
                }
            }

            int matchedSelectionCount = matchedItemList.Count;

            bool success = false;
            bool errorOccurred = false;
            const string hideMethodName =
                "Navisworks kernel: InvertSelection() + SetSelectionHidden(ModelItemCollection, true)";

            try
            {
                // Accessing doc.State via PropertyInfo to avoid compile-time binding
                // (doc.State property does not exist in Navisworks 2021 API)
                PropertyInfo stateProp = typeof(Document).GetProperty("State");
                if (stateProp == null)
                {
                    diagnosticLog?.LogHideOutcome(success: false, errorOccurred: false);
                    return false;
                }

                object state = stateProp.GetValue(doc);
                if (state == null)
                {
                    diagnosticLog?.LogHideOutcome(success: false, errorOccurred: false);
                    return false;
                }

                Type stateType = state.GetType();
                int currentSelectionCount = CountCurrentSelection(doc);

                diagnosticLog?.LogHideMethod(hideMethodName);
                diagnosticLog?.LogHidePrecheck(matchedSelectionCount, currentSelectionCount);

                if (matchedSelectionCount <= 0 || currentSelectionCount <= 0)
                {
                    diagnosticLog?.LogHideBlocked();
                    diagnosticLog?.LogHideOutcome(success: false, errorOccurred: false);
                    return false;
                }

                // 注：调用方（ExecuteCachedResultAction）在调用本方法前已
                // SetSelection(finalKeepItems) 并通过 AreEquivalent 校验，当前选择
                // 即等于 matchedItemList，故此处无需再次写入选择（省一次大集合写入）。

                var sw = Stopwatch.StartNew();
                stateType.InvokeMember(
                    "InvertSelection",
                    BindingFlags.InvokeMethod,
                    null,
                    state,
                    null);
                diagnosticLog?.LogDecision(
                    $"[计时] InvertSelection 耗时 {sw.ElapsedMilliseconds} ms");

                // GetSelectedItems(Document) may not exist in 2021 — probe via reflection
                sw.Restart();
                ModelItemCollection invertedSelection;
                MethodInfo getSelectedMethod = typeof(ModelItemCollection).GetMethod(
                    "GetSelectedItems", new[] { typeof(Document) });
                if (getSelectedMethod != null)
                {
                    invertedSelection = (ModelItemCollection)getSelectedMethod.Invoke(
                        doc.CurrentSelection.Value, new object[] { doc });
                }
                else
                {
                    // Fallback: iterate current selection items
                    invertedSelection = new ModelItemCollection();
                    invertedSelection.AddRange(doc.CurrentSelection.SelectedItems);
                }
                diagnosticLog?.LogDecision(
                    $"[计时] 获取反选集合({invertedSelection.Count} 项)耗时 " +
                    $"{sw.ElapsedMilliseconds} ms");

                // 直接以反选集合作为待隐藏集：SetSelectionHidden 把指定项的
                // IsHidden 设为 true，对已隐藏项重复设置是幂等无害的，因此无需
                // 逐项读 IsHidden 预筛（该预筛是主要耗时点，约 465ms）。
                ModelItemCollection toHide = invertedSelection;

                diagnosticLog?.LogHideCandidateCounts(
                    invertedSelection.Count,
                    toHide.Count);
                diagnosticLog?.LogHideCandidates(toHide);

                if (toHide.Count > 0)
                {
                    sw.Restart();
                    stateType.InvokeMember(
                        "SetSelectionHidden",
                        BindingFlags.InvokeMethod,
                        null,
                        state,
                        new object[] { toHide, true });
                    diagnosticLog?.LogDecision(
                        $"[计时] SetSelectionHidden 耗时 {sw.ElapsedMilliseconds} ms");
                }

                sw.Restart();
                RestoreCurrentSelection(doc, matchedItemList);
                diagnosticLog?.LogDecision(
                    $"[计时] 恢复选择耗时 {sw.ElapsedMilliseconds} ms");

                success = true;
                diagnosticLog?.LogHideOutcome(success: true, errorOccurred: false);
            }
            catch (MissingMethodException)
            {
                // Kernel API method does not exist in this Navisworks version
                diagnosticLog?.LogHideOutcome(success: false, errorOccurred: false);
                return false;
            }
            catch (MissingMemberException)
            {
                // Property or method does not exist in this Navisworks version
                diagnosticLog?.LogHideOutcome(success: false, errorOccurred: false);
                return false;
            }
            catch (Exception ex)
            {
                errorOccurred = true;
                diagnosticLog?.LogException("隐藏未选中", ex);
                throw new InvalidOperationException(
                    "Hide-unselected failed.",
                    ex);
            }
            finally
            {
                if (!success && errorOccurred)
                    diagnosticLog?.LogHideOutcome(success: false, errorOccurred: true);
            }

            return success;
        }

        private static void RestoreCurrentSelection(Document doc, IEnumerable<ModelItem> items)
        {
            using (var selection = new ModelItemCollection())
            {
                foreach (ModelItem item in items)
                    selection.Add(item);

                doc.CurrentSelection.CopyFrom(selection);
            }
        }

        private static int CountCurrentSelection(Document doc)
        {
            int count = 0;
            foreach (ModelItem _ in doc.CurrentSelection.SelectedItems)
                count++;
            return count;
        }
    }
}
