using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Color = System.Drawing.Color;

namespace JiePinPai.Navisworks.TrayMeasurement
{
    internal sealed class BatchMeasurementForm : Form
    {
        private readonly Document _document;
        private readonly Func<MeasurementSourceKind, MeasurementSource> _capture;
        private readonly Func<MeasurementSourceStamp, bool> _isCurrent;
        private readonly Func<int> _selectionCount;
        private readonly Action _openSearch;
        private readonly Timer _work = new Timer { Interval = 25 };
        private readonly Timer _guard = new Timer { Interval = 400 };
        private readonly MeasurementSummary _summary = new MeasurementSummary();
        private readonly Label _detail = new Label();
        private readonly DataGridView _grid = new DataGridView();
        private readonly SearchBox _search = new SearchBox("筛选构件名称或查找编号");
        private readonly ThemedButton _read = new ThemedButton(ButtonKind.Ghost) { Text = "刷新查找结果" };
        private readonly ThemedButton _measure = new ThemedButton(ButtonKind.Primary) { Text = "开始批量测量" };
        private readonly ThemedButton _export = new ThemedButton(ButtonKind.Secondary) { Text = "导出 CSV" };
        private readonly ThemedButton _review = new ThemedButton(ButtonKind.Secondary) { Text = "只看待复核" };
        private readonly ThemedButton _locate = new ThemedButton(ButtonKind.Secondary) { Text = "定位构件" };
        private readonly ModeCard _batchMode = new ModeCard("批量导入查找", "沿用查找窗口的结果，一次测完全部桥架", ModeGlyph.List);
        private readonly ModeCard _manualMode = new ModeCard("手动点选构件", "在模型中选中桥架，按住 Ctrl 可多选", ModeGlyph.Pointer);
        private readonly ThemedButton _goSearch = new ThemedButton(ButtonKind.Ghost) { Text = "打开查找窗口" };
        private readonly Dictionary<MeasurementTarget, Stopwatch> _flash = new Dictionary<MeasurementTarget, Stopwatch>();
        private readonly Func<bool> _flashStep;
        private bool _flashing;
        private bool _autoScrolling;
        private DateTime _userScrolledAt = DateTime.MinValue;
        private readonly ToastWindow _toast;
        private TableLayoutPanel _body;
        private PageTransition _modeTransition;
        private MeasurementSourceKind? _mode;
        private int _selectedCount;
        private MeasurementSource _source;
        private List<MeasurementTarget> _visible = new List<MeasurementTarget>();
        private bool _running;
        private bool _inTick;
        private bool _attempted;
        private int _next;
        private int _runRevision;
        private string _emptyText = "先在上方选择一种测量方式\n\n批量导入查找：沿用查找窗口的结果，一次测完全部桥架。\n手动点选构件：在模型中选中桥架后直接测量，无需导入条件。";
        private string _measuredAt = string.Empty;

        public BatchMeasurementForm(Document document, Func<MeasurementSourceKind, MeasurementSource> capture,
            Func<MeasurementSourceStamp, bool> isCurrent, Func<int> selectionCount, Action openSearch)
        {
            _document = document;
            _capture = capture;
            _isCurrent = isCurrent;
            _selectionCount = selectionCount;
            _openSearch = openSearch;
            Text = "Curi · 桥架批量测量";
            ClientSize = new Size(S(940), S(740));
            MinimumSize = new Size(S(800), S(620));
            StartPosition = FormStartPosition.CenterScreen;
            Font = UiTheme.BodyFont;
            BackColor = UiTheme.Canvas;
            ForeColor = UiTheme.TextBody;
            DoubleBuffered = true;
            ShowIcon = false;
            _flashStep = FlashStep;
            BuildLayout();
            _toast = new ToastWindow(this, UiTheme.ControlHeight + S(48));
            _read.Click += (s, e) => ReadSource();
            _batchMode.Click += (s, e) => ChooseMode(MeasurementSourceKind.SearchResults);
            _manualMode.Click += (s, e) => ChooseMode(MeasurementSourceKind.ManualSelection);
            _goSearch.Click += (s, e) => _openSearch();
            _measure.Click += (s, e) => { if (_running) StopBatch(); else StartBatch(); };
            _grid.Scroll += (s, e) => { if (!_autoScrolling) _userScrolledAt = DateTime.UtcNow; };
            _export.Click += (s, e) => ExportCsv();
            _review.Click += (s, e) => { _review.Active = !_review.Active; RefreshRows(); };
            _search.SearchTextChanged += (s, e) => RefreshRows();
            _search.EnterPressed += (s, e) => LocateCurrent();
            _locate.Click += (s, e) => LocateCurrent();
            _work.Tick += MeasureNext;
            _guard.Tick += (s, e) => { EnsureCurrent(); RefreshManualSelection(); };
            UpdateModeControls();
            UpdateSummary();
        }

        private static int S(int pixels) => UiTheme.Scale(pixels);

        private void BuildLayout()
        {
            var header = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = S(62), BackColor = UiTheme.Surface,
                Padding = new Padding(S(24), S(16), S(24), 0), WrapContents = false
            };
            header.Controls.Add(new BrandMark("Curi") { Margin = new Padding(0, 0, S(20), 0) });
            header.Controls.Add(new Label
            {
                Text = "桥架批量测量", AutoSize = true, Font = UiTheme.TitleFont,
                ForeColor = UiTheme.Text, Margin = new Padding(0, S(6), S(12), 0)
            });
            header.Controls.Add(new Label
            {
                Text = "按构件几何测量直桥架长度，弯头、三通等管件自动排除", AutoSize = true,
                ForeColor = UiTheme.TextMuted, Margin = new Padding(0, S(8), 0, 0)
            });
            UiTheme.AttachEdgeLine(header, false);

            var body = _body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(S(24), S(18), S(24), S(8)),
                ColumnCount = 1, RowCount = 5, Margin = Padding.Empty, BackColor = UiTheme.Canvas
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, S(76)));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, _summary.PreferredCardHeight + S(12)));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight + S(12)));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, S(32)));

            var chooser = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, S(12))
            };
            chooser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            chooser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _batchMode.Dock = _manualMode.Dock = DockStyle.Fill;
            _batchMode.Margin = new Padding(0, 0, S(6), 0);
            _manualMode.Margin = new Padding(S(6), 0, 0, 0);
            chooser.Controls.Add(_batchMode, 0, 0); chooser.Controls.Add(_manualMode, 1, 0);
            body.Controls.Add(chooser, 0, 0);

            _summary.Dock = DockStyle.Fill; _summary.Margin = new Padding(0, 0, 0, S(12));
            body.Controls.Add(_summary, 0, 1);

            var tools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _search.Dock = DockStyle.Fill; _search.Margin = new Padding(0, 0, S(8), S(12));
            _review.Margin = new Padding(0, 0, S(8), 0); _locate.Margin = Padding.Empty;
            tools.Controls.Add(_search, 0, 0); tools.Controls.Add(_review, 1, 0); tools.Controls.Add(_locate, 2, 0);
            body.Controls.Add(tools, 0, 2);

            _grid.Dock = DockStyle.Fill; _grid.ReadOnly = true; _grid.VirtualMode = true;
            _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false;
            _grid.MultiSelect = false; _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Margin = new Padding(1);
            UiTheme.StyleGrid(_grid);
            _grid.RowTemplate.Height = UiTheme.TextHeight(UiTheme.BodyFont) + S(18);
            AddColumn("序号", 8, 50);
            AddColumn("查找编号", 24, 120);
            AddColumn("构件", 36, 160);
            AddColumn("长度 (m)", 15, 100);
            AddColumn("状态", 17, 104);
            _grid.Columns[0].DefaultCellStyle.ForeColor = UiTheme.TextMuted;
            _grid.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns[3].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns[3].DefaultCellStyle.Font = UiTheme.BodyStrongFont;
            _grid.Columns[3].DefaultCellStyle.ForeColor = UiTheme.Text;
            _grid.CellValueNeeded += GridCellValueNeeded;
            _grid.CellToolTipTextNeeded += (s, e) => { if (e.RowIndex >= 0 && e.RowIndex < _visible.Count) e.ToolTipText = _visible[e.RowIndex].Value.Note; };
            _grid.CellFormatting += (s, e) =>
            {
                if (e.ColumnIndex != 3 || e.RowIndex < 0 || e.RowIndex >= _visible.Count) return;
                if (!_visible[e.RowIndex].Value.Metres.HasValue) e.CellStyle.ForeColor = UiTheme.TextDisabled;
            };
            _grid.CellPainting += GridCellPainting;
            _grid.SelectionChanged += (s, e) => UpdateDetail();
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) LocateCurrent(); };
            UiTheme.AttachEmptyState(_grid, () => _source != null && _source.Targets.Count > 0
                ? "没有符合当前筛选的构件\n\n清空筛选文字，或取消“待复核”筛选后查看全部构件。" : _emptyText, UiTheme.EmptyIcon.Document);
            body.Controls.Add(_grid, 0, 3);
            UiTheme.AttachHairlineBorder(body, _grid);
            _detail.Dock = DockStyle.Fill; _detail.TextAlign = ContentAlignment.MiddleLeft;
            _detail.ForeColor = UiTheme.TextMuted; _detail.AutoEllipsis = true;
            body.Controls.Add(_detail, 0, 4);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = UiTheme.ControlHeight + S(30), ColumnCount = 3, RowCount = 1,
                Padding = new Padding(S(24), S(15), S(24), S(15)), BackColor = UiTheme.Surface
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var left = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, BackColor = UiTheme.Surface };
            left.Controls.AddRange(new Control[] { _goSearch, _read });
            var right = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, BackColor = UiTheme.Surface };
            _measure.MinimumSize = new Size(S(132), UiTheme.ControlHeight);
            _measure.Margin = Padding.Empty;
            right.Controls.AddRange(new Control[] { _export, _measure });
            footer.Controls.Add(left, 0, 0); footer.Controls.Add(right, 2, 0);
            UiTheme.AttachEdgeLine(footer, true);
            Controls.Add(body); Controls.Add(header); Controls.Add(footer);
        }

        // 状态列绘制为圆角标签；正在测量的行使用浅强调色底，刚完成的行短暂高亮后淡出。
        private void GridCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _visible.Count) return;
            MeasurementTarget target = _visible[e.RowIndex];
            bool selected = _grid.Rows[e.RowIndex].Selected;
            bool measuring = IsMeasuring(target);
            Stopwatch flash;
            if (!selected && measuring) e.CellStyle.BackColor = UiTheme.AccentSoft;
            else if (!selected && _flash.TryGetValue(target, out flash))
            {
                float t = UiAnimator.EaseOut((float)flash.Elapsed.TotalMilliseconds / FlashMs);
                Color back, fore;
                UiTheme.GetStatusColors(StatusFor(target.Value.State), out back, out fore);
                e.CellStyle.BackColor = UiAnimator.Lerp(UiAnimator.Lerp(back, Color.White, 0.35f), e.CellStyle.BackColor, t);
            }
            if (e.ColumnIndex != 4) return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);
            Color badgeBack, badgeFore;
            string text = measuring ? "测量中…" : target.Value.StatusText;
            if (measuring) { badgeBack = UiTheme.Selection; badgeFore = UiTheme.AccentPressed; }
            else if (target.Value.State == MeasurementState.Pending || target.Value.State == MeasurementState.Cancelled)
            { badgeBack = UiTheme.HeaderBack; badgeFore = UiTheme.TextMuted; }
            else UiTheme.GetStatusColors(StatusFor(target.Value.State), out badgeBack, out badgeFore);
            Size textSize = TextRenderer.MeasureText(text, UiTheme.BodyStrongFont);
            int badgeHeight = textSize.Height + S(4);
            var badge = new Rectangle(e.CellBounds.Left + S(8), e.CellBounds.Top + (e.CellBounds.Height - badgeHeight) / 2,
                Math.Min(textSize.Width + S(12), e.CellBounds.Width - S(12)), badgeHeight);
            UiTheme.FillRoundedRectangle(e.Graphics, badgeBack, badge, badgeHeight / 2);
            TextRenderer.DrawText(e.Graphics, text, UiTheme.BodyStrongFont, badge, badgeFore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.Handled = true;
        }

        private static SearchResultStatus StatusFor(MeasurementState state) =>
            state == MeasurementState.Measured ? SearchResultStatus.Found
            : state == MeasurementState.Review ? SearchResultStatus.Duplicate : SearchResultStatus.ConditionInvalid;

        private bool IsMeasuring(MeasurementTarget target) =>
            _running && _source != null && _next < _source.Targets.Count && ReferenceEquals(_source.Targets[_next], target);

        private const float FlashMs = 900f;

        private void Flash(MeasurementTarget target)
        {
            _flash[target] = Stopwatch.StartNew();
            if (_flashing) return;
            _flashing = true;
            UiAnimator.Run(_flashStep);
        }

        private bool FlashStep()
        {
            if (IsDisposed) return _flashing = false;
            foreach (MeasurementTarget done in _flash.Where(p => p.Value.Elapsed.TotalMilliseconds >= FlashMs).Select(p => p.Key).ToList())
                _flash.Remove(done);
            _grid.Invalidate();
            if (_flash.Count == 0) _flashing = false;
            return _flashing;
        }

        // 测量时让当前行保持可见；用户刚手动滚动过则暂不跟随，避免抢夺视图。
        private void FollowCurrentRow()
        {
            if (!_running || _source == null || _next >= _source.Targets.Count) return;
            if ((DateTime.UtcNow - _userScrolledAt).TotalSeconds < 3) return;
            int row = _visible.IndexOf(_source.Targets[_next]);
            if (row < 0 || _grid.RowCount == 0) return;
            int first = _grid.FirstDisplayedScrollingRowIndex;
            int shown = Math.Max(1, _grid.DisplayedRowCount(false));
            if (first >= 0 && row >= first && row < first + shown) return;
            _autoScrolling = true;
            try { _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, Math.Min(row - shown / 2, _grid.RowCount - 1)); }
            catch (InvalidOperationException) { }
            finally { _autoScrolling = false; }
        }

        private void AddColumn(string title, float weight, int minimum)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = title, FillWeight = weight, MinimumWidth = S(minimum),
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private void GridCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _visible.Count) return;
            MeasurementTarget target = _visible[e.RowIndex];
            switch (e.ColumnIndex)
            {
                case 0: e.Value = target.Number; break;
                case 1: e.Value = target.Query; break;
                case 2: e.Value = target.Name; break;
                case 3: e.Value = target.Value.Metres.HasValue ? MeasurementBatchPolicy.FormatMetres(target.Value.Metres.Value) : "—"; break;
                case 4: e.Value = target.Value.StatusText; break;
            }
        }

        private void ChooseMode(MeasurementSourceKind kind)
        {
            if (_running || _mode == kind) return;
            _modeTransition?.Finish();
            _modeTransition = null;
            Bitmap before = Visible && Opacity >= .99 ? PageTransition.Snapshot(_body) : null;
            _mode = kind;
            _review.Active = false;
            _search.Clear();
            InvalidateSource(kind == MeasurementSourceKind.ManualSelection
                ? "在模型视图或选择树中选中桥架，再点击“测量当前选择”。"
                : "导入桥架条件并执行查找，然后在这里批量测量。");
            UpdateModeControls();
            if (kind == MeasurementSourceKind.SearchResults) ReadSource();
            else RefreshManualSelection();
            _body.PerformLayout();
            if (before != null) _modeTransition = PageTransition.Play(_body, before, PageTransition.Snapshot(_body));
        }

        private void UpdateModeControls()
        {
            bool manual = _mode == MeasurementSourceKind.ManualSelection;
            bool batch = _mode == MeasurementSourceKind.SearchResults;
            _batchMode.Active = batch;
            _manualMode.Active = manual;
            _batchMode.AccessibleName = batch ? "批量导入查找，已选择" : "批量导入查找";
            _manualMode.AccessibleName = manual ? "手动点选构件，已选择" : "手动点选构件";
            _read.Visible = _goSearch.Visible = batch;
            _grid.Columns[1].Visible = !manual;
            UpdateActions();
        }

        private void RefreshManualSelection()
        {
            if (_mode != MeasurementSourceKind.ManualSelection || IsDisposed) return;
            try { _selectedCount = _selectionCount(); }
            catch { _selectedCount = -1; }
            string current = _selectedCount < 0 ? "当前模型不可用，请重新打开插件"
                : _selectedCount == 0 ? "模型中尚未选择构件" : $"模型当前选中 {_selectedCount} 个";
            _summary.SourceText = _source == null ? current
                : $"本次测量 {_source.Targets.Count} 个 · {current}";
            if (_source == null)
            {
                _emptyText = _selectedCount > 0 ? $"模型中已选中 {_selectedCount} 个构件\n\n点击右下角“测量当前选择”即可得到长度。"
                    : "在模型中选中要测量的桥架\n\n可在模型视图或选择树中单选，按住 Ctrl 多选；无需导入查找条件。";
                _grid.Invalidate();
            }
            UpdateActions();
        }

        internal void InvalidateSearchSource()
        {
            // Search edits must not discard measurements made from a manual selection snapshot.
            if (_mode == MeasurementSourceKind.SearchResults)
                InvalidateSource("查找结果已变化，请重新读取查找结果。");
        }

        private bool ReadSource()
        {
            if (_running || !_mode.HasValue) return false;
            try
            {
                MeasurementSource source = _capture(_mode.Value);
                _source = source; _next = 0; _attempted = false; _measuredAt = string.Empty;
                _review.Active = false; _search.Clear();
                _emptyText = "没有可测量的对象\n\n请检查桥架查找条件，以及重复项是否已在查找窗口中“计入匹配”。";
                ResetValues();
                _summary.SourceText = $"{source.Scope ?? "查找结果"} · 有效对象 {source.Targets.Count} 个"
                    + (source.ExcludedDuplicates > 0 ? $" · 未计入重复 {source.ExcludedDuplicates} 条" : "")
                    + (source.UnmatchedConditions > 0 ? $" · 未找到/异常 {source.UnmatchedConditions} 条" : "");
                RefreshRows(); UpdateSummary();
                return true;
            }
            catch (Exception ex) { InvalidateSource(ex.Message); return false; }
        }

        private void ResetValues()
        {
            foreach (MeasurementTarget target in _source.Targets)
            {
                target.Value = new MeasurementValue();
                if (target.ReviewReason != null) target.Value.Complete(false, 0, target.ReviewReason);
            }
        }

        internal void InvalidateSource(string reason)
        {
            if (IsDisposed) return;
            _runRevision++;
            _work.Stop(); _running = false; _source = null; _next = 0; _attempted = false;
            _flash.Clear();
            _summary.SourceText = string.Empty; _emptyText = reason;
            RefreshRows(); UpdateSummary();
        }

        private bool EnsureCurrent()
        {
            if (_source == null || IsDisposed) return false;
            if (_isCurrent(_source.Stamp)) return true;
            InvalidateSource(_mode == MeasurementSourceKind.ManualSelection
                ? "模型已变化，请重新选择构件后测量。" : "模型或查找结果已变化，请重新搜索并读取结果。");
            return false;
        }

        private void StartBatch()
        {
            if (!_mode.HasValue) return;
            // Capture the live selection only at the explicit measure action. Looking at another
            // object or locating a result must not silently replace this batch or its total.
            if (_mode == MeasurementSourceKind.ManualSelection && !ReadSource()) return;
            if (!EnsureCurrent() || _source.Targets.Count == 0) return;
            _runRevision++;
            ResetValues(); _next = 0; _running = true; _attempted = true;
            RefreshManualSelection();
            _measuredAt = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture);
            _measure.Text = "停止测量";
            _userScrolledAt = DateTime.MinValue; _flash.Clear();
            RefreshRows(); UpdateSummary(); _work.Start();
        }

        // COM and Navisworks APIs stay on the host UI thread. Each tick measures one object,
        // then returns to the message loop for painting, cancellation and document-change events.
        private void MeasureNext(object sender, EventArgs e)
        {
            if (_inTick || !_running || !EnsureCurrent()) return;
            if (_next >= _source.Targets.Count) { FinishBatch(); return; }
            _inTick = true;
            int runRevision = _runRevision;
            MeasurementSource source = _source;
            MeasurementTarget target = source.Targets[_next];
            try
            {
                if (target.Value.State == MeasurementState.Pending)
                {
                    try
                    {
                        GeometrySnapshot snapshot = GeometryReader.Read(_document, target.Item);
                        if (!_running || runRevision != _runRevision || !ReferenceEquals(source, _source) || !EnsureCurrent()) return;
                        target.Value.Complete(snapshot.Result.IsStraightCandidate, snapshot.Result.SpanMetres,
                            FriendlyReason(snapshot.Result.Reason));
                        Flash(target);
                    }
                    catch (Exception ex)
                    {
                        if (!_running || runRevision != _runRevision || !ReferenceEquals(source, _source) || !EnsureCurrent()) return;
                        target.Value.Complete(false, 0, ex.Message);
                        Flash(target);
                    }
                }
                _next++;
                if (_review.Active) RefreshRows(); else _grid.Invalidate();
                FollowCurrentRow();
                UpdateSummary(); UpdateDetail();
                if (_next >= source.Targets.Count) FinishBatch();
            }
            finally { _inTick = false; }
        }

        private static string FriendlyReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return "无法可靠测量，请人工复核。";
            if (reason.StartsWith("FITTING", StringComparison.Ordinal)) return "疑似弯头、三通或变径，未计入直桥架总长。";
            return reason.Replace("UNKNOWN_GEOMETRY：", string.Empty);
        }

        private void StopBatch()
        {
            _runRevision++;
            _work.Stop(); _running = false;
            if (_source != null) foreach (MeasurementTarget target in _source.Targets) target.Value.Cancel();
            RefreshRows(); UpdateSummary();
            _toast.ShowMessage(_mode == MeasurementSourceKind.ManualSelection
                ? "已停止并保留已测结果；再次点击将测量模型中的当前选择。"
                : "已停止，保留已测长度；再次开始将重新测量全部构件。");
        }

        private void FinishBatch()
        {
            _work.Stop(); _running = false; UpdateSummary(); _grid.Invalidate();
            int review = _source?.Targets.Count(t => t.Value.State == MeasurementState.Review) ?? 0;
            _toast.ShowMessage(review > 0 ? $"测量完成，{review} 个待复核构件未计入总长。" : "测量完成，全部构件已计入总长。");
        }

        private void RefreshRows()
        {
            string query = (_search.SearchText ?? string.Empty).Trim();
            _visible = _source == null ? new List<MeasurementTarget>() : _source.Targets.Where(t =>
                (!_review.Active || t.Value.State == MeasurementState.Review)
                && (query.Length == 0 || Contains(t.Name, query) || Contains(t.Query, query))).ToList();
            _grid.RowCount = _visible.Count; _grid.Invalidate(); UpdateDetail();
        }

        private static bool Contains(string value, string query) => (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private MeasurementTarget CurrentTarget()
        {
            int index = _grid.CurrentCell?.RowIndex ?? -1;
            return index >= 0 && index < _visible.Count ? _visible[index] : null;
        }

        private void UpdateDetail()
        {
            MeasurementTarget target = CurrentTarget();
            _detail.Text = _running ? "正在逐件测量；点击“停止测量”会在当前构件处理完后停止。"
                : target == null ? "提示：双击一行或按 Enter 可在模型中定位该构件；悬停可查看待复核原因。"
                : $"#{target.Number}  {target.Name} — {target.Value.Note}";
            _locate.Enabled = target != null && !_running;
        }

        private void UpdateSummary()
        {
            var values = _source?.Targets.Select(t => t.Value).ToList() ?? new List<MeasurementValue>();
            int measured = values.Count(v => v.State == MeasurementState.Measured);
            int review = values.Count(v => v.State == MeasurementState.Review);
            int remaining = values.Count - measured - review;
            string status; SummaryTone tone;
            if (_source == null) { status = _mode.HasValue ? "等待测量对象" : "请先选择测量方式"; tone = SummaryTone.Muted; }
            else if (_running) { status = $"正在测量第 {Math.Min(_next + 1, values.Count)} / {values.Count} 个构件"; tone = SummaryTone.Accent; }
            else if (!_attempted) { status = $"就绪 · {values.Count} 个构件待测量"; tone = SummaryTone.Muted; }
            else if (remaining > 0) { status = $"已停止 · 当前为部分结果，还有 {remaining} 个未测"; tone = SummaryTone.Warning; }
            else if (review > 0) { status = $"测量完成 · {review} 个待复核构件未计入总长"; tone = SummaryTone.Warning; }
            else { status = "测量完成 · 全部构件已计入总长"; tone = SummaryTone.Success; }
            _summary.SetValues(measured > 0 ? (double?)MeasurementBatchPolicy.Total(values) : null,
                measured, review, remaining, _running ? _next : measured + review, _running, status, tone);
            _review.Text = review > 0 ? $"只看待复核 · {review}" : "只看待复核";
            UpdateActions();
            UpdateDetail();
        }

        private void UpdateActions()
        {
            bool manual = _mode == MeasurementSourceKind.ManualSelection;
            bool hasTargets = _source != null && _source.Targets.Count > 0;
            _batchMode.Enabled = _manualMode.Enabled = _goSearch.Enabled = _read.Enabled = !_running;
            _measure.Enabled = _running || _mode.HasValue && (manual ? _selectedCount > 0 : hasTargets);
            _measure.Kind = _running ? ButtonKind.Danger : ButtonKind.Primary;
            _review.Enabled = _source != null && _source.Targets.Count > 0;
            _measure.Text = !_mode.HasValue ? "请先选择测量方式"
                : _running ? "停止测量" : manual ? "测量当前选择"
                : _attempted ? "重新测量全部" : "开始批量测量";
            _export.Enabled = !_running && _attempted && hasTargets;
        }

        private void LocateCurrent()
        {
            if (_running || !EnsureCurrent()) return;
            MeasurementTarget target = CurrentTarget();
            if (target == null) return;
            try
            {
                var selected = new ModelItemCollection(); selected.Add(target.Item);
                _document.CurrentSelection.CopyFrom(selected);
                ViewFocusService.ZoomToCurrentSelection(_document);
                _toast.ShowMessage("已在模型中选中构件。");
            }
            catch (Exception ex) { InvalidateSource("构件已不可用，请重新选择测量对象。" + ex.Message); }
        }

        private void ExportCsv()
        {
            if (_running || !_attempted || !EnsureCurrent()) return;
            using (var dialog = new SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = "桥架测量_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || !EnsureCurrent()) return;
                try
                {
                    var text = new StringBuilder("序号,查找编号,构件,长度_m,状态,说明,测量时间,模型,范围,测量来源\r\n");
                    for (int i = 0; i < _source.Targets.Count; i++)
                    {
                        MeasurementTarget target = _source.Targets[i];
                        string[] fields = { (i + 1).ToString(), target.Query, target.Name,
                            target.Value.Metres.HasValue ? target.Value.Metres.Value.ToString("0.######", CultureInfo.InvariantCulture) : "",
                            target.Value.StatusText, target.Value.Note, _measuredAt, _source.DocumentPath, _source.Scope,
                            _source.Stamp.Kind == MeasurementSourceKind.ManualSelection ? "手动点选构件" : "批量导入查找" };
                        text.AppendLine(string.Join(",", fields.Select(MeasurementBatchPolicy.Csv)));
                    }
                    File.WriteAllText(dialog.FileName, text.ToString(), new UTF8Encoding(true));
                    _toast.ShowMessage($"已导出全部 {_source.Targets.Count} 个构件的测量清单。");
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Curi · 导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e); Opacity = 0;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e); _guard.Start();
            var watch = Stopwatch.StartNew();
            UiAnimator.Run(() =>
            {
                if (IsDisposed) return false;
                float t = UiAnimator.EaseOut((float)watch.Elapsed.TotalMilliseconds / 180f);
                Opacity = t; return t < 1f;
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _running = false; _work.Dispose(); _guard.Dispose(); _modeTransition?.Finish(); _toast?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
