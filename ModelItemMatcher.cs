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
            // key=属性 display name, value=分类 display name
            var categoryCache = new Dictionary<string, string>();
            DiscoverCategories(doc, conditions, categoryCache);

            // ── 阶段 1：搜索 ──
            var results = new List<SearchResult>(conditions.Count);
            int runningTotal = 0;

            foreach (var cond in conditions)
            {
                // 构建 Navisworks 原生 SearchCondition
                var navisCond = BuildNavisCondition(cond, categoryCache);
                if (navisCond == null)
                {
                    results.Add(MakeEmptyResult(cond.Value));
                    continue;
                }

                // 配置并执行 Search
                using (var search = new Search())
                {
                    if (scopeItems != null)
                        search.Selection.CopyFrom(scopeItems);
                    else
                        search.Selection.SelectAll();

                    search.SearchConditions.Add(navisCond);
                    ModelItemCollection found = search.FindAll(doc, reportProgress: true);

                    var matchedItems = found.Cast<ModelItem>().ToList();
                    results.Add(new SearchResult
                    {
                        QueryValue = cond.Value,
                        MatchCount = matchedItems.Count,
                        MatchedItems = matchedItems,
                    });

                    runningTotal += matchedItems.Count;
                }
            }

            return results;
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
        private static NavisCondition BuildNavisCondition(
            SearchCondition cond,
            Dictionary<string, string> categoryCache)
        {
            bool hasCategory = !string.IsNullOrEmpty(cond.CategoryDisplay)
                            || !string.IsNullOrEmpty(cond.CategoryInternal);

            string propName = cond.PropertyDisplay ?? cond.PropertyInternal;
            if (string.IsNullOrEmpty(propName))
                return null;

            // ── 确定分类名 ──
            string catName;
            if (hasCategory)
            {
                catName = cond.CategoryDisplay ?? cond.CategoryInternal;
            }
            else if (cond.PropertyDisplay != null
                     && categoryCache.TryGetValue(cond.PropertyDisplay, out catName))
            {
                // 从模型采样发现的分类 ✓
            }
            else
            {
                // 无法确定分类，搜索无法执行
                return null;
            }

            // ── 构建属性选择条件 ──
            var propCond = NavisCondition.HasPropertyByDisplayName(catName, propName);

            // ── 添加值匹配方式（忽略大小写）──
            bool isContains = string.Equals(cond.Test, "contains",
                StringComparison.OrdinalIgnoreCase);

            return isContains
                ? propCond.DisplayStringContains(cond.Value)
                    .IgnoreStringValueCase()
                : propCond.EqualValue(VariantData.FromDisplayString(cond.Value))
                    .IgnoreStringValueCase();
        }

        /// <summary>
        /// 生成一个空匹配结果。
        /// </summary>
        private static SearchResult MakeEmptyResult(string queryValue)
        {
            return new SearchResult
            {
                QueryValue = queryValue,
                MatchCount = 0,
                MatchedItems = new List<ModelItem>(),
            };
        }
    }
}
