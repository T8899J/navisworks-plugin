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
        /// <summary>
        /// 查找指定名称的保护节点及其所有子孙元素。
        ///
        /// 优化策略（避免全模型遍历）：
        /// 1. 先在根节点的直接子节点中查找（STR 通常在这里，O(子节点数)）
        /// 2. 未找到则在根子节点后代中做浅层搜索（O(子节点数) × 浅层）
        /// 3. 最后才用 Search API（C++ 内部处理，远快于 .NET 端遍历）
        /// </summary>
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

            // BFS 从 RootItem 起按 DisplayName 查找，命中即停
            ModelItem foundNode = FindInRootChildren(doc, targetNodeName);
            if (foundNode != null)
            {
                return BuildResult(result, targetNodeName, foundNode, "exact");
            }

            return result;
        }

        /// <summary>
        /// 当调用方已知目标节点（如用户已选中 STR 节点）时，直接构建结果，
        /// 跳过 BFS 查找。
        /// </summary>
        public static ProtectedKeepResult BuildFromNode(
            string targetNodeName,
            ModelItem foundNode)
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

            if (foundNode == null)
                return result;

            return BuildResult(result, targetNodeName, foundNode, "exact-prechecked");
        }

        /// <summary>
        /// 在模型根节点的后代中按 DisplayName 查找目标节点。
        ///
        /// BFS 从 RootItem 起逐层搜索，命中即停。
        /// 命中路径极短（STR 通常在根下 1-3 层），最坏情况（未命中）遍历全部节点
        /// 也仅为一次遍历，远优于旧代码的三次全模型遍历。
        /// </summary>
        private static ModelItem FindInRootChildren(Document doc, string targetNodeName)
        {
            foreach (Model model in doc.Models)
            {
                if (model.RootItem == null)
                    continue;

                var queue = new Queue<ModelItem>();
                queue.Enqueue(model.RootItem);

                while (queue.Count > 0)
                {
                    ModelItem current = queue.Dequeue();

                    if (IsNameMatch(current.DisplayName, targetNodeName))
                        return current;

                    foreach (ModelItem child in current.Children)
                        queue.Enqueue(child);
                }
            }

            return null;
        }

        private static bool IsNameMatch(string displayName, string target)
        {
            if (string.IsNullOrEmpty(displayName))
                return false;

            // 精确匹配
            if (string.Equals(displayName, target, StringComparison.Ordinal))
                return true;

            // Trim 匹配（防御空格/制表符）
            if (string.Equals(displayName.Trim(), target, StringComparison.Ordinal))
                return true;

            return false;
        }

        /// <summary>
        /// 从找到的节点构建结果，收集其所有子孙元素。
        /// DescendantsAndSelf 是 Navisworks 原生调用，快速。
        /// </summary>
        private static ProtectedKeepResult BuildResult(
            ProtectedKeepResult result,
            string targetNodeName,
            ModelItem foundNode,
            string matchMode)
        {
            result.Found = true;
            result.MatchMode = matchMode;
            result.MatchedNodeCount = 1;
            result.TargetNodeName = targetNodeName;

            var protectedSet = new HashSet<ModelItem>();
            int descendantCount = 0;

            foreach (ModelItem item in foundNode.DescendantsAndSelf)
                protectedSet.Add(item);

            foreach (ModelItem _ in foundNode.Descendants)
                descendantCount++;

            result.DescendantCount = descendantCount;
            result.ProtectedItems = protectedSet.ToList();
            return result;
        }
    }
}
