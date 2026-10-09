using System;
using System.Diagnostics;
using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;

namespace JiePinPai.Navisworks.TrayMeasurement
{
    [Plugin("JiePinPai_QuickTrayMeasurement", "JiePinPai", DisplayName = "桥架便捷测量",
        ToolTip = "打开简易长度窗口，一次测量一个选中构件")]
    [AddInPlugin(AddInLocation.AddIn)]
    public sealed class SingleMeasurementPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                SingleMeasurementWindow.Open(Process.GetCurrentProcess().MainWindowHandle);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开便捷测量窗口：\n" + ex.Message, "桥架长度", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return -1;
            }
        }
    }
}
