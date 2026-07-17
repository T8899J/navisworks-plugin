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
        // 唯一存活的无模式窗口。仅在 Navisworks UI 线程访问
        // （清单 executeInMainThread="true"），故无需加锁；同时钉住引用防止被 GC。
        private static SearchDialog _openDialog;

        public override int Execute(params string[] parameters)
        {
            try
            {
                Document doc = GetActiveDocument();
                if (doc == null)
                    return 1;

                // 再次点击功能区按钮：已有窗口时激活它，不再开第二个。
                if (_openDialog != null && !_openDialog.IsDisposed)
                {
                    if (ReferenceEquals(_openDialog.Document, doc))
                    {
                        RestoreAndActivate(_openDialog);
                        return 0;
                    }

                    // 活动文档已换成另一个 Document 对象：关闭旧窗口，绑定新文档重开。
                    _openDialog.Close();
                }

                IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
                string initialXmlPath = ResolveInitialXmlPath(parameters);

                var dialog = new SearchDialog(doc, initialXmlPath, hwnd);
                dialog.FormClosed += SearchDialog_FormClosed;
                _openDialog = dialog;

                // 无模式显示：窗口以 Navisworks 主窗口为宿主浮于其上，
                // Execute 立即返回，用户可同时操作三维视图。
                if (hwnd != IntPtr.Zero)
                    dialog.Show(new WindowWrapper(hwnd));
                else
                    dialog.Show();

                return 0;
            }
            catch (Exception ex)
            {
                _openDialog = null;
                MessageBox.Show(
                    "操作失败：\n" + ex.Message,
                    "傑出品 错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return -1;
            }
        }

        private static void SearchDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (sender is SearchDialog closed)
                closed.FormClosed -= SearchDialog_FormClosed;
            _openDialog = null;
        }

        private static void RestoreAndActivate(Form form)
        {
            if (form.WindowState == FormWindowState.Minimized)
                form.WindowState = FormWindowState.Normal;
            form.Activate();
            form.BringToFront();
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
