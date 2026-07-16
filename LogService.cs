using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// 生成查找操作日志文件（旧版兼容）。
    ///
    /// 日志保存在 XML 文件同级目录，文件名格式：
    /// {xml文件名}_查找日志_YYYYMMDD_HHmmss.txt
    /// </summary>
    public static class LogService
    {
        public static DiagnosticLogSession CreateDiagnosticSession(
            Document doc,
            string xmlFilePath,
            int conditionCount)
        {
            return new DiagnosticLogSession(
                GetDocumentDisplayName(doc),
                xmlFilePath,
                conditionCount);
        }

        /// <summary>
        /// 写入查找日志文件（旧版兼容方法）。
        /// </summary>
        public static string WriteLog(
            string xmlFilePath,
            List<SearchResult> results,
            int totalMatchedCount,
            bool hideExecuted)
        {
            try
            {
                string xmlDir = Path.GetDirectoryName(xmlFilePath) ?? ".";
                string xmlName = Path.GetFileNameWithoutExtension(xmlFilePath);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string logFileName = $"{xmlName}_查找日志_{timestamp}.txt";
                string logFilePath = Path.Combine(xmlDir, logFileName);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("===== 傑出品 Navisworks 查找日志 =====");
                sb.AppendLine($"XML 文件: {xmlFilePath}");
                sb.AppendLine($"查找时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine(string.Empty);

                int foundCount = 0;
                int notFoundCount = 0;
                int duplicateCount = 0;
                int invalidCount = 0;

                sb.AppendLine("--- 查询结果 ---");
                foreach (SearchResult result in results)
                {
                    switch (result.Status)
                    {
                        case SearchResultStatus.Found: foundCount++; break;
                        case SearchResultStatus.NotFound: notFoundCount++; break;
                        case SearchResultStatus.Duplicate: duplicateCount++; break;
                        case SearchResultStatus.ConditionInvalid: invalidCount++; break;
                    }

                    SearchConditionSnapshot condition = result.Condition;
                    sb.AppendLine(
                        $"[{SearchResultPolicy.GetDisplayName(result.Status)}] " +
                        $"#{condition.DisplayIndex} " +
                        $"{condition.GetCategoryName()} / {condition.GetPropertyName()} / " +
                        $"{condition.Test} / {condition.Value} → " +
                        $"匹配 {result.MatchCount} 个对象；{result.StatusMessage}");
                }

                sb.AppendLine(string.Empty);
                sb.AppendLine("--- 汇总 ---");
                sb.AppendLine($"总条件数: {results.Count}");
                sb.AppendLine($"已找到: {foundCount}");
                sb.AppendLine($"未找到: {notFoundCount}");
                sb.AppendLine($"重复: {duplicateCount}");
                sb.AppendLine($"条件异常: {invalidCount}");
                sb.AppendLine($"总计匹配对象数（去重）: {totalMatchedCount}");
                sb.AppendLine($"唯一性校验: {(SearchResultPolicy.CanHide(results.Select(r => r.Status)) ? "通过" : "未通过")}");

                if (hideExecuted)
                {
                    sb.AppendLine($"隐藏未选中: 已执行");
                }

                File.WriteAllText(logFilePath, sb.ToString(), Encoding.UTF8);

                return logFilePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"写入日志失败: {ex.Message}");
                return null;
            }
        }

        private static string GetDocumentDisplayName(Document doc)
        {
            if (doc == null)
                return "（空）";

            string[] propertyNames = { "CurrentFileName", "FileName", "Title" };
            Type docType = doc.GetType();

            foreach (string propertyName in propertyNames)
            {
                try
                {
                    var property = docType.GetProperty(propertyName);
                    if (property == null)
                        continue;

                    var value = property.GetValue(doc, null) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                catch
                {
                    // Ignore reflection failures and keep probing fallbacks.
                }
            }

            return doc.ToString();
        }
    }
}
