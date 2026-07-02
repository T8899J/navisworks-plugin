using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks
{
    public sealed class DiagnosticLogSession
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly string _xmlFilePath;

        public DiagnosticLogSession(string modelFileName, string xmlFilePath, int conditionCount)
        {
            _xmlFilePath = xmlFilePath;

            _sb.AppendLine("===== 傑出品 Navisworks 诊断日志 =====");
            _sb.AppendLine($"当前时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            _sb.AppendLine($"当前打开的 Navisworks 文件名: {Safe(modelFileName)}");
            _sb.AppendLine($"XML 文件路径: {Safe(xmlFilePath)}");
            _sb.AppendLine($"XML 解析出的条件数量: {conditionCount}");
            _sb.AppendLine();
        }

        public void LogMode(string modeName)
        {
            StartSection("运行模式");
            _sb.AppendLine($"当前模式: {modeName}");
        }

        public void LogMatchedItemCounts(int matchedItemCount, int dedupedItemCount)
        {
            StartSection("匹配结果");
            _sb.AppendLine($"匹配到的 ModelItem 数量: {matchedItemCount}");
            _sb.AppendLine($"去重后的 ModelItem 数量: {dedupedItemCount}");
        }

        public void LogSelectionWrite(
            int beforeWriteCount,
            int actualSelectionCount,
            IEnumerable<ModelItem> selectedItems)
        {
            StartSection("当前选择");
            _sb.AppendLine($"写入 CurrentSelection 前的对象数量: {beforeWriteCount}");
            _sb.AppendLine($"写入 CurrentSelection 后，Navisworks 当前实际选择数量: {actualSelectionCount}");
            _sb.AppendLine("前 20 个选中对象:");

            int index = 0;
            foreach (ModelItem item in selectedItems ?? Enumerable.Empty<ModelItem>())
            {
                if (index >= 20)
                    break;

                _sb.AppendLine(
                    $"{index + 1}. DisplayName={Safe(item.DisplayName)}, " +
                    $"ClassName={Safe(item.ClassName)}, " +
                    $"InstanceGuid={item.InstanceGuid}");
                index++;
            }

            if (index == 0)
                _sb.AppendLine("0. （无）");
        }

        public void LogHideIntent(bool shouldPrepareHide)
        {
            StartSection("隐藏计划");
            _sb.AppendLine($"是否准备执行隐藏未选中: {(shouldPrepareHide ? "是" : "否")}");
        }

        public void LogHidePrompt(int matchedCount, int currentSelectionCount, string choice)
        {
            _sb.AppendLine($"弹窗时匹配对象数量: {matchedCount}");
            _sb.AppendLine($"弹窗时当前 Navisworks 实际选择数量: {currentSelectionCount}");
            _sb.AppendLine($"弹窗结果: {choice}");
        }

        public void LogHideMethod(string methodName)
        {
            _sb.AppendLine($"执行隐藏未选中使用的方法或命令名称: {methodName}");
        }

        public void LogHidePrecheck(int matchedCount, int currentSelectionCount)
        {
            _sb.AppendLine($"执行隐藏前 matchedItems.Count: {matchedCount}");
            _sb.AppendLine($"执行隐藏前 CurrentSelection.Count: {currentSelectionCount}");
        }

        public void LogHideBlocked()
        {
            _sb.AppendLine("禁止隐藏：匹配数量或当前选择数量为 0。");
        }

        public void LogHideOutcome(bool success, bool errorOccurred)
        {
            _sb.AppendLine($"执行隐藏后是否报错: {(errorOccurred ? "是" : "否")}");
            _sb.AppendLine($"隐藏未选中是否成功返回: {(success ? "是" : "否")}");
        }

        public void LogException(string context, Exception ex)
        {
            StartSection($"异常 - {context}");
            _sb.AppendLine(ex?.ToString() ?? "Exception 为 null");
        }

        public string WriteToFile()
        {
            try
            {
                string baseDirectory = !string.IsNullOrWhiteSpace(_xmlFilePath)
                    ? (Path.GetDirectoryName(_xmlFilePath) ?? ".")
                    : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string baseName = !string.IsNullOrWhiteSpace(_xmlFilePath)
                    ? Path.GetFileNameWithoutExtension(_xmlFilePath)
                    : "未指定XML";
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string logPath = Path.Combine(baseDirectory, $"{baseName}_诊断日志_{timestamp}.txt");
                File.WriteAllText(logPath, _sb.ToString(), Encoding.UTF8);
                return logPath;
            }
            catch
            {
                return null;
            }
        }

        private void StartSection(string title)
        {
            _sb.AppendLine($"--- {title} ---");
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "（空）" : value;
        }
    }
}
