using System;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// 创建用户显式启用的诊断日志会话。
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
