using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks
{
    internal sealed class ProtectedKeepResult
    {
        public string TargetNodeName { get; set; }
        public bool Found { get; set; }
        public string MatchMode { get; set; }
        public int MatchedNodeCount { get; set; }
        public int DescendantCount { get; set; }
        public List<ModelItem> ProtectedItems { get; set; }
    }

    internal static class ProtectedKeepService
    {
        public static ProtectedKeepResult FindProtectedItems(
            Document doc,
            string targetNodeName)
        {
            var result = new ProtectedKeepResult
            {
                TargetNodeName = targetNodeName,
                Found = false,
                MatchMode = "none",
                MatchedNodeCount = 0,
                DescendantCount = 0,
                ProtectedItems = new List<ModelItem>(),
            };

            if (doc == null || string.IsNullOrWhiteSpace(targetNodeName))
                return result;

            List<ModelItem> allItems = EnumerateAllModelItems(doc).ToList();
            List<ModelItem> matchedNodes = FindMatches(
                allItems,
                targetNodeName,
                out string matchMode);

            result.MatchMode = matchMode;
            result.MatchedNodeCount = matchedNodes.Count;
            result.Found = matchedNodes.Count > 0;

            if (!result.Found)
                return result;

            var protectedSet = new HashSet<ModelItem>();
            int descendantCount = 0;

            foreach (ModelItem node in matchedNodes)
            {
                foreach (ModelItem descendant in node.DescendantsAndSelf)
                    protectedSet.Add(descendant);

                descendantCount += node.Descendants.Cast<ModelItem>().Count();
            }

            result.DescendantCount = descendantCount;
            result.ProtectedItems = protectedSet.ToList();
            return result;
        }

        private static List<ModelItem> FindMatches(
            IEnumerable<ModelItem> allItems,
            string targetNodeName,
            out string matchMode)
        {
            List<ModelItem> exactMatches = allItems
                .Where(item => string.Equals(
                    item.DisplayName,
                    targetNodeName,
                    StringComparison.Ordinal))
                .ToList();
            if (exactMatches.Count > 0)
            {
                matchMode = "exact";
                return exactMatches;
            }

            List<ModelItem> trimMatches = allItems
                .Where(item => string.Equals(
                    (item.DisplayName ?? string.Empty).Trim(),
                    targetNodeName,
                    StringComparison.Ordinal))
                .ToList();
            if (trimMatches.Count > 0)
            {
                matchMode = "trim";
                return trimMatches;
            }

            List<ModelItem> containsMatches = allItems
                .Where(item => !string.IsNullOrWhiteSpace(item.DisplayName)
                    && item.DisplayName.IndexOf(
                        targetNodeName,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            if (containsMatches.Count > 0)
            {
                matchMode = "contains";
                return containsMatches;
            }

            matchMode = "none";
            return new List<ModelItem>();
        }

        private static IEnumerable<ModelItem> EnumerateAllModelItems(Document doc)
        {
            foreach (Model model in doc.Models)
            {
                if (model?.RootItem == null)
                    continue;

                foreach (ModelItem item in model.RootItem.DescendantsAndSelf)
                    yield return item;
            }
        }
    }
}
