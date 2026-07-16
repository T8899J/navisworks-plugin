using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks
{
    internal static class DiagnosticLogExtensions
    {
        private static readonly FieldInfo BufferField =
            typeof(DiagnosticLogSession).GetField(
                "_sb",
                BindingFlags.Instance | BindingFlags.NonPublic);

        public static void StartSection(this DiagnosticLogSession session, string title)
        {
            AppendLine(session, $"--- {title} ---");
        }

        public static void LogScopeInfo(
            this DiagnosticLogSession session,
            IEnumerable<ModelItem> scopeRoots,
            int scopeItemsCount)
        {
            session.StartSection("搜索范围");

            var roots = (scopeRoots ?? Enumerable.Empty<ModelItem>()).ToList();
            AppendLine(session, $"scopeRoots count: {roots.Count}");

            int index = 0;
            foreach (ModelItem item in roots)
            {
                AppendLine(session, $"scopeRoot[{index}]: {Safe(item.DisplayName)}");
                index++;
            }

            AppendLine(session, $"scopeItems count: {scopeItemsCount}");
        }

        public static void LogModelPrefixInfo(
            this DiagnosticLogSession session,
            IEnumerable<string> modelPrefixes,
            string modelPrefix,
            string protectedName)
        {
            session.StartSection("模型前缀识别");
            string prefixSummary = string.Join(
                ", ",
                (modelPrefixes ?? Enumerable.Empty<string>()).DefaultIfEmpty("(none)"));
            AppendLine(
                session,
                $"model prefixes: {prefixSummary}");
            AppendLine(session, $"final modelPrefix: {Safe(modelPrefix)}");
            AppendLine(session, $"dynamic protectedName: {Safe(protectedName)}");
        }

        public static void LogXmlScopeResultStats(
            this DiagnosticLogSession session,
            int rawResultCount,
            int matchedItemsInScopeCount,
            int outOfScopeCount)
        {
            session.StartSection("XML 搜索结果");
            int repeatedReferenceCount = Math.Max(
                0,
                rawResultCount - matchedItemsInScopeCount);
            AppendLine(session, $"条件匹配次数（对象去重前）: {rawResultCount}");
            AppendLine(session, $"唯一匹配对象数（跨条件去重后）: {matchedItemsInScopeCount}");
            AppendLine(session, $"重复对象引用数: {repeatedReferenceCount}");
            AppendLine(session, $"out-of-scope match count: {outOfScopeCount}");
        }

        public static void LogSearchResults(
            this DiagnosticLogSession session,
            IEnumerable<SearchResult> results,
            bool hideGatePassed)
        {
            if (session == null)
                return;

            List<SearchResult> list = (results ?? Enumerable.Empty<SearchResult>()).ToList();
            session.StartSection("查询条件结果");
            foreach (SearchResult result in list)
            {
                if (result == null)
                {
                    AppendLine(session, "(empty search result)");
                    continue;
                }

                SearchConditionSnapshot condition = result.Condition;
                AppendLine(
                    session,
                    $"#{condition?.DisplayIndex.ToString() ?? "(empty)"} " +
                    $"状态={SearchResultPolicy.GetDisplayName(result.Status)}, " +
                    $"分类显示={Safe(condition?.CategoryDisplay)}, " +
                    $"分类内部={Safe(condition?.CategoryInternal)}, " +
                    $"属性显示={Safe(condition?.PropertyDisplay)}, " +
                    $"属性内部={Safe(condition?.PropertyInternal)}, " +
                    $"方式={Safe(condition?.Test)}, " +
                    $"查询值={Safe(condition?.Value)}, " +
                    $"匹配数={result.MatchCount}, " +
                    $"说明={Safe(result.StatusMessage)}");
            }

            AppendLine(session, $"已找到: {list.Count(r => r != null && r.Status == SearchResultStatus.Found)}");
            AppendLine(session, $"未找到: {list.Count(r => r != null && r.Status == SearchResultStatus.NotFound)}");
            AppendLine(session, $"重复: {list.Count(r => r != null && r.Status == SearchResultStatus.Duplicate)}");
            AppendLine(session, $"条件异常: {list.Count(r => r != null && r.Status == SearchResultStatus.ConditionInvalid)}");
            AppendLine(session, $"隐藏门禁通过: {(hideGatePassed ? "是" : "否")}");
        }

        public static void LogProtectedNodeStats(
            this DiagnosticLogSession session,
            string targetNodeName,
            bool found,
            string matchMode,
            int matchedNodeCount,
            int descendantCount,
            int protectedItemsCount)
        {
            session.StartSection("强制保留节点");
            AppendLine(session, $"protectedName: {Safe(targetNodeName)}");
            AppendLine(session, $"found: {found}");
            AppendLine(session, $"match mode: {Safe(matchMode)}");
            AppendLine(session, $"matched node count: {matchedNodeCount}");
            AppendLine(session, $"descendant count: {descendantCount}");
            AppendLine(session, $"protectedItems count: {protectedItemsCount}");
        }

        public static void LogFinalKeepStats(
            this DiagnosticLogSession session,
            int matchedItemsInScopeCount,
            int protectedItemsCount,
            int finalKeepCount,
            int actualSelectionCount,
            bool actualSelectionMatchesFinalKeep,
            bool hideWillExecute)
        {
            session.StartSection("最终保留集合");
            AppendLine(session, $"matchedItemsInScope count: {matchedItemsInScopeCount}");
            AppendLine(session, $"protectedItems count: {protectedItemsCount}");
            AppendLine(session, $"finalKeepItems count: {finalKeepCount}");
            AppendLine(session, $"CurrentSelection count after write: {actualSelectionCount}");
            AppendLine(session, $"CurrentSelection exactly matches finalKeepItems: {actualSelectionMatchesFinalKeep}");
            AppendLine(session, $"Hide Unselected will execute: {hideWillExecute}");
        }

        public static void LogDecision(this DiagnosticLogSession session, string message)
        {
            AppendLine(session, message);
        }

        public static void LogHideCandidateCounts(
            this DiagnosticLogSession session,
            int invertedSelectionCount,
            int toHideCount)
        {
            AppendLine(session, $"Inverted selection count: {invertedSelectionCount}");
            AppendLine(session, $"toHide count: {toHideCount}");
        }

        public static void LogHideCandidates(
            this DiagnosticLogSession session,
            IEnumerable<ModelItem> items)
        {
            AppendLine(session, "First 20 toHide items:");

            int index = 0;
            foreach (ModelItem item in items ?? Enumerable.Empty<ModelItem>())
            {
                if (index >= 20)
                    break;

                AppendLine(
                    session,
                    $"{index + 1}. DisplayName={Safe(item.DisplayName)}, " +
                    $"ClassName={Safe(item.ClassName)}, " +
                    $"InstanceGuid={item.InstanceGuid}");
                index++;
            }

            if (index == 0)
                AppendLine(session, "0. (none)");
        }

        private static void AppendLine(DiagnosticLogSession session, string line)
        {
            if (session == null)
                return;

            var buffer = BufferField?.GetValue(session) as StringBuilder;
            buffer?.AppendLine(line);
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
        }
    }
}
