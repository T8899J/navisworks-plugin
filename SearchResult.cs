using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// 单个查询值的匹配结果。
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// 原始查询值。
        /// </summary>
        public string QueryValue { get; set; }

        /// <summary>
        /// 匹配到的 ModelItem 数量。
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// 匹配到的 ModelItem 列表（MatchCount 为 0 时为空列表）。
        /// </summary>
        public List<ModelItem> MatchedItems { get; set; }

        /// <summary>
        /// 默认构造函数。
        /// </summary>
        public SearchResult()
        {
            QueryValue = string.Empty;
            MatchCount = 0;
            MatchedItems = new List<ModelItem>();
        }
    }
}
