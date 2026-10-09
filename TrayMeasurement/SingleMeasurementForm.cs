using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using NavApp = Autodesk.Navisworks.Api.Application;

namespace JiePinPai.Navisworks.TrayMeasurement
{
    internal sealed class SingleMeasurementReading
    {
        public string ItemName;
        public double? Metres;
        public string Hint;
    }

    // Keeps the original single-item experiment's compact layout and explicit click-to-measure flow.
    internal sealed class SingleMeasurementForm : Form
    {
        private readonly Label _itemName = new Label { Text = "请选择一段直桥架", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        private readonly Label _length = new Label { Text = "—", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        private readonly Label _millimetres = new Label { TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        private readonly Label _hint = new Label { Text = "选中构件后，点击下方按钮", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        private readonly ThemedButton _measure = new ThemedButton(ButtonKind.Primary) { Text = "测量选中构件", Dock = DockStyle.Fill, AutoSize = false };
        private readonly Font _resultFont = new Font("Segoe UI", 38F, FontStyle.Bold);
        private readonly Func<SingleMeasurementReading> _readSelection;
        private bool _measuring;

        internal SingleMeasurementForm(Func<SingleMeasurementReading> readSelection = null)
        {
            _readSelection = readSelection ?? ReadCurrentSelection;
            Text = "桥架长度";
            Font = UiTheme.BodyFont;
            ClientSize = new Size(UiTheme.Scale(440), UiTheme.Scale(300));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiTheme.Canvas;
            DoubleBuffered = true;
            _itemName.ForeColor = _millimetres.ForeColor = _hint.ForeColor = UiTheme.TextMuted;
            _length.Font = _resultFont;
            _length.ForeColor = UiTheme.Text;
            _measure.Margin = new Padding(0, UiTheme.Scale(10), 0, 0);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
                Padding = new Padding(UiTheme.Scale(24), UiTheme.Scale(20), UiTheme.Scale(24), UiTheme.Scale(20))
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(30)));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(28)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(40)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(56)));
            layout.Controls.Add(_itemName, 0, 0);
            layout.Controls.Add(_length, 0, 1);
            layout.Controls.Add(_millimetres, 0, 2);
            layout.Controls.Add(_hint, 0, 3);
            layout.Controls.Add(_measure, 0, 4);
            Controls.Add(layout);
            _measure.Click += (s, e) => RunMeasurement();
        }

        internal static string SelectionHint(int count)
        {
            return count == 1 ? null : count == 0 ? "请先选中一段直桥架" : "一次只能测量一个构件";
        }

        private static SingleMeasurementReading ReadCurrentSelection()
        {
            var doc = NavApp.ActiveDocument;
            if (doc == null || doc.IsClear) return new SingleMeasurementReading { Hint = "请先打开模型" };
            var selected = doc.CurrentSelection.SelectedItems;
            string selectionHint = SelectionHint(selected.Count);
            if (selectionHint != null) return new SingleMeasurementReading { Hint = selectionHint };

            // Exactly one selected node, same geometry reader as the original experiment and batch mode.
            var item = selected[0];
            var snapshot = GeometryReader.Read(doc, item);
            var result = snapshot.Result;
            bool valid = result.IsStraightCandidate && result.SpanMetres > 0
                && !double.IsNaN(result.SpanMetres) && !double.IsInfinity(result.SpanMetres);
            return new SingleMeasurementReading
            {
                ItemName = string.IsNullOrWhiteSpace(snapshot.ItemName) ? "已选构件" : snapshot.ItemName,
                Metres = valid ? (double?)result.SpanMetres : null,
                Hint = valid ? string.Empty : "暂时无法确定长度，请选择一段直桥架"
            };
        }

        private void RunMeasurement()
        {
            if (_measuring) return;
            _measuring = true;
            _length.Text = "—";
            _millimetres.Text = string.Empty;
            _itemName.Text = "请选择一段直桥架";
            _hint.Text = "正在测量…";
            _measure.Enabled = false;
            UseWaitCursor = true;
            Refresh();
            try
            {
                SingleMeasurementReading reading = _readSelection();
                if (IsDisposed) return;
                if (!string.IsNullOrWhiteSpace(reading.ItemName)) _itemName.Text = reading.ItemName;
                _hint.Text = reading.Hint ?? string.Empty;
                if (!reading.Metres.HasValue) return;
                _length.Text = reading.Metres.Value.ToString("0.###", CultureInfo.CurrentCulture) + " m";
                _millimetres.Text = (reading.Metres.Value * 1000).ToString("0.#", CultureInfo.CurrentCulture) + " mm";
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                if (!IsDisposed) _hint.Text = "未能测量，请重新选择一段直桥架";
            }
            finally
            {
                _measuring = false;
                if (!IsDisposed) { UseWaitCursor = false; _measure.Enabled = true; }
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Opacity = 0;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var watch = Stopwatch.StartNew();
            UiAnimator.Run(() =>
            {
                if (IsDisposed) return false;
                float t = UiAnimator.EaseOut((float)watch.Elapsed.TotalMilliseconds / 180f);
                Opacity = t;
                return t < 1f;
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _resultFont.Dispose();
        }
    }

    internal static class SingleMeasurementWindow
    {
        private static SingleMeasurementForm _openForm;

        internal static void Open(IntPtr ownerHandle)
        {
            if (_openForm != null && !_openForm.IsDisposed)
            {
                _openForm.WindowState = FormWindowState.Normal;
                _openForm.Activate();
                _openForm.BringToFront();
                return;
            }
            var form = new SingleMeasurementForm();
            form.FormClosed += (s, e) => { if (ReferenceEquals(_openForm, form)) _openForm = null; };
            _openForm = form;
            try
            {
                // Owned by Navisworks, not SearchDialog: closing the large window leaves this tool usable.
                if (ownerHandle != IntPtr.Zero) form.Show(new WindowWrapper(ownerHandle));
                else form.Show();
            }
            catch
            {
                _openForm = null;
                form.Dispose();
                throw;
            }
        }
    }
}
