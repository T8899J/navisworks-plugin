using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using NavisCondition = Autodesk.Navisworks.Api.SearchCondition;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// 使用 Navisworks 原生 Search API 进行属性匹配。
    ///
    /// == 设计原理 ==
    ///
    /// 旧实现：手动遍历所有元素的所有分类的所有属性（COM 跨边界 × 数千万次）
    /// 新实现：用 Navisworks Search API（C++ 原生执行，自带消息泵）
    ///
    /// 关键难点：Search API 要求提供属性所属的分类（category）名。
    /// - 杰出品格式的 XML 有时包含 &lt;category&gt; 元素（如 test_search.xml）
    /// - 但从 Navisworks 导出的 exchange XML 不包含（如 支架.xml）
    /// - 解决方案：从模型采样前几个元素，找出目标属性属于哪个分类
    /// </summary>
    public static class ModelItemMatcher
    {
        /// <summary>
        /// 在整个模型中搜索匹配的元素。
        /// </summary>
        public static List<SearchResult> MatchAll(
            Document doc,
            List<SearchCondition> conditions)
        {
            return ExecuteSearches(doc, conditions, scopeItems: null);
        }

        /// <summary>
        /// 在指定范围内搜索匹配的元素。
        /// </summary>
        public static List<SearchResult> MatchAll(
            Document doc,
            IEnumerable<ModelItem> scope,
            List<SearchCondition> conditions)
        {
            return ExecuteSearches(doc, conditions, scopeItems: scope);
        }

        /// <summary>
        /// 核心执行：对每个条件运行一次原生 Search.FindAll()。
        ///
        /// 第一阶段：为没有 category 的条件从模型采样找出正确分类
        /// 第二阶段：为每个条件运行一次 Search.FindAll
        /// </summary>
        private static List<SearchResult> ExecuteSearches(
            Document doc,
            List<SearchCondition> conditions,
            IEnumerable<ModelItem> scopeItems)
        {
            // ── 阶段 0：预发现——为没有分类的条件找出正确的分类名 ──
            var categoryCache = new Dictionary<string, string>();
            DiscoverCategories(doc, conditions, categoryCache);

            // ── 预构建 scope 集合（避免每轮 CopyFrom 都转换）──
            ModelItemCollection scopeCollection = null;
            if (scopeItems != null)
            {
                scopeCollection = new ModelItemCollection();
                foreach (ModelItem item in scopeItems)
                    scopeCollection.Add(item);
            }

            // ── 阶段 1：搜索 ──
            var results = new List<SearchResult>(conditions.Count);

            for (int conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
            {
                SearchCondition condition = conditions[conditionIndex];
                SearchConditionSnapshot snapshot = SearchConditionSnapshot.From(
                    conditionIndex,
                    condition);

                if (!TryBuildNavisCondition(
                        condition,
                        categoryCache,
                        out NavisCondition navisCondition,
                        out string validationError))
                {
                    results.Add(CreateResult(
                        snapshot,
                        conditionExecuted: false,
                        matchedItems: new List<ModelItem>(),
                        invalidReason: validationError));
                    continue;
                }

                using (var search = new Search())
                {
                    if (scopeCollection != null)
                        search.Selection.CopyFrom(scopeCollection);
                    else
                        search.Selection.SelectAll();

                    search.SearchConditions.Add(navisCondition);
                    ModelItemCollection found = search.FindAll(doc, false);
                    var seenItems = new HashSet<ModelItem>();
                    List<ModelItem> matchedItems = found
                        .Cast<ModelItem>()
                        .Where(item => item != null && seenItems.Add(item))
                        .ToList();
                    results.Add(CreateResult(
                        snapshot,
                        conditionExecuted: true,
                        matchedItems: matchedItems,
                        invalidReason: string.Empty));
                }
            }

            MarkCrossConditionDuplicates(results);
            return results;
        }

        private static void MarkCrossConditionDuplicates(List<SearchResult> results)
        {
            IReadOnlyList<ModelItem>[] matchesByCondition = results
                .Select(result => (IReadOnlyList<ModelItem>)result.MatchedItems)
                .ToArray();
            Dictionary<int, int> repeatedOwners =
                OneToOneMatchPolicy.FindRepeatedSingleMatches(matchesByCondition);

            foreach (KeyValuePair<int, int> repeatedOwner in repeatedOwners)
            {
                SearchResult repeatedResult = results[repeatedOwner.Key];
                if (repeatedResult.Status != SearchResultStatus.Found)
                    continue;

                SearchResult firstResult = results[repeatedOwner.Value];
                repeatedResult.Status = SearchResultStatus.Duplicate;
                repeatedResult.StatusMessage =
                    $"与条件 #{firstResult.Condition.DisplayIndex} 命中同一对象，" +
                    "每个对象只允许由一条条件唯一对应。";
            }
        }

        // =================================================================
        //  分类发现（Category Discovery）
        // =================================================================

        /// <summary>
        /// 扫描模型中的少量元素，找出目标属性属于哪个分类。
        ///
        /// 原理：Navisworks 中所有同类元素共享相同的分类结构。
        /// 属性"名称"（display name）通常属于"元素"（Element）分类，
        /// 扫描前几个元素就能确定，无需遍历整个模型。
        /// </summary>
        private static void DiscoverCategories(
            Document doc,
            List<SearchCondition> conditions,
            Dictionary<string, string> cache)
        {
            if (doc == null) return;

            // 收集需要发现分类的属性名（没有 category 的条件）
            var needsDiscovery = new HashSet<string>();
            foreach (var c in conditions)
            {
                if (c == null)
                    continue;

                bool hasCategory = !string.IsNullOrEmpty(c.CategoryDisplay)
                                || !string.IsNullOrEmpty(c.CategoryInternal);
                if (!hasCategory && !string.IsNullOrEmpty(c.PropertyDisplay))
                    needsDiscovery.Add(c.PropertyDisplay);
            }

            if (needsDiscovery.Count == 0) return;

            // 采样扫描：最多 2000 个元素（对抗大模型深层嵌套）
            int scanned = 0;
            const int maxScan = 2000;
            foreach (ModelItem item in doc.Models.RootItemDescendants)
            {
                foreach (PropertyCategory cat in item.PropertyCategories)
                {
                    foreach (DataProperty prop in cat.Properties)
                    {
                        if (needsDiscovery.Contains(prop.DisplayName)
                            && !cache.ContainsKey(prop.DisplayName))
                        {
                            cache[prop.DisplayName] = cat.DisplayName;
                            if (cache.Count >= needsDiscovery.Count)
                            {
                                return; // 全部找到
                            }
                        }
                    }
                }

                scanned++;
                if (scanned >= maxScan) break;
            }
        }

        // =================================================================
        //  原生 SearchCondition 构建
        // =================================================================

        /// <summary>
        /// 将杰出品格式的 SearchCondition 转为 Navisworks 原生 SearchCondition。
        ///
        /// 映射规则：
        ///   - XML 有 category → 直接使用 HasPropertyByDisplayName
        ///   - XML 无 category → 用已发现的分类名
        ///   - test="contains" → DisplayStringContains + IgnoreStringValueCase
        ///   - test="equals"   → EqualValue(VariantData) + IgnoreStringValueCase
        /// </summary>
        private static bool TryBuildNavisCondition(
            SearchCondition condition,
            Dictionary<string, string> categoryCache,
            out NavisCondition navisCondition,
            out string errorMessage)
        {
            navisCondition = null;
            if (!SearchConditionValidator.TryValidate(condition, out errorMessage))
                return false;

            string propertyName = !string.IsNullOrWhiteSpace(condition.PropertyDisplay)
                ? condition.PropertyDisplay
                : condition.PropertyInternal;
            string categoryName = !string.IsNullOrWhiteSpace(condition.CategoryDisplay)
                ? condition.CategoryDisplay
                : condition.CategoryInternal;

            if (string.IsNullOrWhiteSpace(categoryName))
            {
                string discoveryKey = condition.PropertyDisplay;
                if (string.IsNullOrWhiteSpace(discoveryKey)
                    || !categoryCache.TryGetValue(discoveryKey, out categoryName))
                {
                    errorMessage = $"无法识别属性“{propertyName}”所属分类。";
                    return false;
                }
            }

            try
            {
                NavisCondition propertyCondition =
                    NavisCondition.HasPropertyByDisplayName(categoryName, propertyName);
                bool contains = string.Equals(
                    condition.Test,
                    "contains",
                    StringComparison.OrdinalIgnoreCase);
                navisCondition = contains
                    ? propertyCondition.DisplayStringContains(condition.Value)
                        .IgnoreStringValueCase()
                    : propertyCondition.EqualValue(
                        VariantData.FromDisplayString(condition.Value))
                        .IgnoreStringValueCase();
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                navisCondition = null;
                errorMessage = "条件构造失败：" + ex.Message;
                return false;
            }
        }

        private static SearchResult CreateResult(
            SearchConditionSnapshot snapshot,
            bool conditionExecuted,
            List<ModelItem> matchedItems,
            string invalidReason)
        {
            int matchCount = matchedItems?.Count ?? 0;
            SearchResultStatus status = SearchResultPolicy.Classify(
                conditionExecuted,
                matchCount);

            string message;
            switch (status)
            {
                case SearchResultStatus.Found:
                    message = "当前选定范围内唯一匹配 1 个对象。";
                    break;
                case SearchResultStatus.NotFound:
                    message = "当前选定范围内未找到对象。";
                    break;
                case SearchResultStatus.Duplicate:
                    message = $"当前选定范围内匹配 {matchCount} 个对象，要求唯一。";
                    break;
                case SearchResultStatus.ConditionInvalid:
                    message = invalidReason;
                    break;
                default:
                    throw new InvalidOperationException("未知搜索结果状态。");
            }

            return new SearchResult
            {
                Condition = snapshot,
                Status = status,
                StatusMessage = message,
                MatchCount = matchCount,
                MatchedItems = matchedItems ?? new List<ModelItem>(),
            };
        }

    }
}
