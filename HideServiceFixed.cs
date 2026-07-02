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
                object state = doc.State;
                if (state == null)
                    throw new InvalidOperationException(
                        "Unable to access the Navisworks document state.");

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

                ModelItemCollection invertedSelection =
                    doc.CurrentSelection.Value.GetSelectedItems(doc);

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
                errorOccurred = true;
                throw new InvalidOperationException(
                    "Navisworks hide API SetSelectionHidden is unavailable for this version.");
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
