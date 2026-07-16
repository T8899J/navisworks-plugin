using System;
using System.Collections.Generic;
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

                RestoreCurrentSelection(doc, matchedItemList);

                stateType.InvokeMember(
                    "InvertSelection",
                    BindingFlags.InvokeMethod,
                    null,
                    state,
                    null);

                // GetSelectedItems(Document) may not exist in 2021 — probe via reflection
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
                    foreach (ModelItem item in doc.CurrentSelection.SelectedItems)
                        invertedSelection.Add(item);
                }

                var toHide = new ModelItemCollection();
                foreach (ModelItem item in invertedSelection)
                {
                    if (!item.IsHidden)
                    {
                        toHide.Add(item);
                    }
                }

                diagnosticLog?.LogHideCandidateCounts(
                    invertedSelection.Count,
                    toHide.Count);
                diagnosticLog?.LogHideCandidates(toHide);

                if (toHide.Count > 0)
                {
                    stateType.InvokeMember(
                        "SetSelectionHidden",
                        BindingFlags.InvokeMethod,
                        null,
                        state,
                        new object[] { toHide, true });
                }

                RestoreCurrentSelection(doc, matchedItemList);

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
