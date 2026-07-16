using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// 将匹配到的 ModelItem 设为 Navisworks 当前选择。
    /// </summary>
    public static class SelectionService
    {
        /// <summary>
        /// 将所有匹配的 ModelItem 合并去重后设为当前选择。
        /// </summary>
        /// <param name="doc">当前 Navisworks 文档。</param>
        /// <param name="results">所有条件的匹配结果列表。</param>
        /// <returns>写入后从 Navisworks 当前选择读取的实际 ModelItem 快照。</returns>
        public static List<ModelItem> SetSelection(
            Document doc,
            List<SearchResult> results,
            DiagnosticLogSession diagnosticLog = null)
        {
            int matchedItemCount = 0;
            foreach (SearchResult result in results)
                matchedItemCount += result.MatchedItems.Count;
            // 合并所有匹配项并去重
            HashSet<ModelItem> uniqueItems = new HashSet<ModelItem>();

            foreach (SearchResult result in results)
            {
                foreach (ModelItem item in result.MatchedItems)
                {
                    uniqueItems.Add(item);
                }
            }

            List<ModelItem> selectionList = uniqueItems.ToList();
            diagnosticLog?.LogMatchedItemCounts(matchedItemCount, selectionList.Count);

            // 构建 ModelItemCollection 并设为当前选择
            using (ModelItemCollection selection = new ModelItemCollection())
            {
                foreach (ModelItem item in selectionList)
                {
                    selection.Add(item);
                }
                doc.CurrentSelection.CopyFrom(selection);
            }

            var actualSelectedItems = new List<ModelItem>();
            foreach (ModelItem item in doc.CurrentSelection.SelectedItems)
                actualSelectedItems.Add(item);
            diagnosticLog?.LogSelectionWrite(
                selectionList.Count,
                actualSelectedItems.Count,
                actualSelectedItems);

            return actualSelectedItems;
        }

        public static List<ModelItem> SetSelection(
            Document doc,
            IEnumerable<ModelItem> items,
            DiagnosticLogSession diagnosticLog = null)
        {
            var uniqueItems = new HashSet<ModelItem>();
            foreach (ModelItem item in items ?? Enumerable.Empty<ModelItem>())
            {
                if (item != null)
                    uniqueItems.Add(item);
            }

            List<ModelItem> selectionList = uniqueItems.ToList();

            using (ModelItemCollection selection = new ModelItemCollection())
            {
                foreach (ModelItem item in selectionList)
                    selection.Add(item);

                doc.CurrentSelection.CopyFrom(selection);
            }

            var actualSelectedItems = new List<ModelItem>();
            foreach (ModelItem item in doc.CurrentSelection.SelectedItems)
                actualSelectedItems.Add(item);

            diagnosticLog?.LogSelectionWrite(
                selectionList.Count,
                actualSelectedItems.Count,
                actualSelectedItems);

            return actualSelectedItems;
        }

        /// <summary>
        /// 将扁平无结构的对象集合持久化为 Navisworks 选择集（SelectionSet）。
        /// 用户可在 Navisworks「集合」面板右键选择集 →「选择」→ 批量修改属性。
        /// </summary>
        /// <param name="doc">当前 Navisworks 文档。</param>
        /// <param name="name">选择集显示名称。</param>
        /// <param name="items">要放入选择集的 ModelItem 集合（自动去重）。</param>
        public static void CreateSelectionSet(
            Document doc,
            string name,
            IEnumerable<ModelItem> items)
        {
            var uniqueItems = new HashSet<ModelItem>();
            foreach (ModelItem item in items ?? Enumerable.Empty<ModelItem>())
            {
                if (item != null)
                    uniqueItems.Add(item);
            }

            if (uniqueItems.Count == 0)
                return;

            using (var collection = new ModelItemCollection())
            {
                foreach (ModelItem item in uniqueItems)
                    collection.Add(item);

                var selectionSet = new SelectionSet(collection)
                {
                    DisplayName = name
                };

                doc.SelectionSets.AddCopy(selectionSet);
                selectionSet.Dispose();
            }
        }
    }
}
