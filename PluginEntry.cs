using System;
using System.Diagnostics;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NavApp = Autodesk.Navisworks.Api.Application;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// 傑出品 Navisworks 查找插件入口。
    ///
    /// 点击按钮后弹出 SearchDialog，用户在其中完成所有操作。
    /// </summary>
    [Plugin(
        "JiePinPai_SearchPlugin",
        "JiePinPai",
        DisplayName = "傑出品查找",
        ToolTip = "打开傑出品搜索工具")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class PluginEntry : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                Document doc = GetActiveDocument();
                if (doc == null)
                    return 1;

                IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
                string initialXmlPath = ResolveInitialXmlPath(parameters);
                using (var dialog = new SearchDialog(doc, initialXmlPath))
                {
                    if (hwnd != IntPtr.Zero)
                        dialog.ShowDialog(new WindowWrapper(hwnd));
                    else
                        dialog.ShowDialog();
                }

                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "操作失败：\n" + ex.Message,
                    "傑出品 错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return -1;
            }
        }

        private static string ResolveInitialXmlPath(string[] parameters)
        {
            if (parameters == null)
                return null;

            foreach (string rawParameter in parameters)
            {
                if (string.IsNullOrWhiteSpace(rawParameter))
                    continue;

                string candidate = rawParameter.Trim().Trim('"');
                if (System.IO.File.Exists(candidate))
                    return candidate;

                string relativeCandidate = System.IO.Path.GetFullPath(candidate);
                if (System.IO.File.Exists(relativeCandidate))
                    return relativeCandidate;
            }

            return null;
        }

        private static Document GetActiveDocument()
        {
            try
            {
                Document doc = NavApp.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show(
                        "请先打开一个 Navisworks 文档。",
                        "傑出品",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return null;
                }
                return doc;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "无法获取当前文档：\n" + ex.Message,
                    "傑出品 错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return null;
            }
        }
    }

    internal class WindowWrapper : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; }
        public WindowWrapper(IntPtr handle) { Handle = handle; }
    }
}
