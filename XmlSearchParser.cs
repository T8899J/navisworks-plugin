using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// 解析 Navisworks exchange XML 文件，提取查找条件列表。
    /// </summary>
    public static class XmlSearchParser
    {
        /// <summary>
        /// 解析指定的 XML 文件，返回查找条件列表。
        /// </summary>
        /// <param name="xmlPath">XML 文件完整路径。</param>
        /// <returns>查找条件列表。</returns>
        /// <exception cref="System.IO.FileNotFoundException">文件不存在时抛出。</exception>
        /// <exception cref="System.Xml.XmlException">XML 格式错误时抛出。</exception>
        /// <exception cref="InvalidOperationException">缺少必要元素时抛出。</exception>
        public static List<SearchCondition> Parse(string xmlPath)
        {
            XDocument doc = XDocument.Load(xmlPath);
            XElement root = doc.Root;

            if (root == null)
                throw new InvalidOperationException("XML 根元素为空");

            // 定位 <findspec>/<conditions>/<condition>
            XElement findspec = root.Element("findspec");
            if (findspec == null)
                throw new InvalidOperationException("XML 中缺少 <findspec> 元素");

            XElement conditionsElement = findspec.Element("conditions");
            if (conditionsElement == null)
                throw new InvalidOperationException("XML 中缺少 <conditions> 元素");

            IEnumerable<XElement> conditionElements = conditionsElement.Elements("condition");

            List<SearchCondition> conditions = new List<SearchCondition>();

            foreach (XElement cond in conditionElements)
            {
                SearchCondition searchCondition = ParseCondition(cond);
                conditions.Add(searchCondition);
            }

            if (conditions.Count == 0)
                throw new InvalidOperationException("XML 中未找到任何 <condition> 条件元素");

            return conditions;
        }

        /// <summary>
        /// 解析单个 &lt;condition&gt; 元素。
        /// </summary>
        private static SearchCondition ParseCondition(XElement conditionElement)
        {
            // 读取 test 属性（equals / contains）
            string test = conditionElement.Attribute("test")?.Value ?? "equals";

            // 读取可选的 <category>
            string categoryInternal = null;
            string categoryDisplay = null;

            XElement categoryElement = conditionElement.Element("category");
            if (categoryElement != null)
            {
                XElement catNameElement = categoryElement.Element("name");
                if (catNameElement != null)
                {
                    categoryInternal = catNameElement.Attribute("internal")?.Value;
                    categoryDisplay = catNameElement.Value;
                }
            }

            // 读取 <property>
            XElement propertyElement = conditionElement.Element("property");
            if (propertyElement == null)
                throw new InvalidOperationException("XML 中 <condition> 缺少 <property> 元素");

            XElement propNameElement = propertyElement.Element("name");
            if (propNameElement == null)
                throw new InvalidOperationException("XML 中 <condition> 缺少 <property>/<name> 元素");

            string propertyInternal = propNameElement.Attribute("internal")?.Value ?? string.Empty;
            string propertyDisplay = propNameElement.Value;

            if (string.IsNullOrEmpty(propertyInternal))
            {
                // 没有 internal 属性时，用 display name 回退
                propertyInternal = propertyDisplay;
            }

            if (string.IsNullOrEmpty(propertyInternal))
                throw new InvalidOperationException("XML 中 <property>/<name> 缺少 internal 属性");

            // 读取 <value>/<data>
            XElement valueElement = conditionElement.Element("value");
            if (valueElement == null)
                throw new InvalidOperationException("XML 中 <condition> 缺少 <value> 元素");

            XElement dataElement = valueElement.Element("data");
            if (dataElement == null)
                throw new InvalidOperationException("XML 中 <condition> 缺少 <value>/<data> 元素");

            string queryValue = dataElement.Value ?? string.Empty;

            if (string.IsNullOrEmpty(queryValue))
                throw new InvalidOperationException("XML 中 <value>/<data> 内容为空");

            return new SearchCondition
            {
                CategoryInternal = categoryInternal,
                CategoryDisplay = categoryDisplay,
                PropertyInternal = propertyInternal,
                PropertyDisplay = propertyDisplay,
                Test = test,
                Value = queryValue,
            };
        }
    }
}
