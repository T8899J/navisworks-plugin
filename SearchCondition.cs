using System;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// 一个 Navisworks 查找条件，对应 XML 中的一个 &lt;condition&gt; 元素。
    /// </summary>
    public class SearchCondition
    {
        /// <summary>
        /// category/name 的 internal 属性值（可选，无 category 时为 null）。
        /// </summary>
        public string CategoryInternal { get; set; }

        /// <summary>
        /// category/name 的显示文本（可选，无 category 时为 null）。
        /// </summary>
        public string CategoryDisplay { get; set; }

        /// <summary>
        /// property/name 的 internal 属性值。
        /// </summary>
        public string PropertyInternal { get; set; }

        /// <summary>
        /// property/name 的显示文本。
        /// </summary>
        public string PropertyDisplay { get; set; }

        /// <summary>
        /// 匹配方式：equals 或 contains。
        /// </summary>
        public string Test { get; set; }

        /// <summary>
        /// 查询值。
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 默认构造函数。
        /// </summary>
        public SearchCondition()
        {
            CategoryInternal = null;
            CategoryDisplay = null;
            PropertyInternal = string.Empty;
            PropertyDisplay = string.Empty;
            Test = "equals";
            Value = string.Empty;
        }
    }
}
