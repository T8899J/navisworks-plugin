using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using CheckBoxState = System.Windows.Forms.VisualStyles.CheckBoxState;
using Color = System.Drawing.Color;
using NavApp = Autodesk.Navisworks.Api.Application;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// 傑出品主对话框。
    /// 包含搜索条件编辑、结果查看等模块。
    /// 设计为可扩展的选项卡布局，便于后续添加新功能。
    /// </summary>
    public partial class SearchDialog : Form
    {
        // ── 模块 1：搜索条件 ──
        private DataGridView _conditionsGrid;
        private Button _btnAddCondition;
        private Button _btnDeleteCondition;
        private Button _btnClearConditions;
        private Button _btnImportXml;
        private Button _btnExportXml;
        private Button _btnUsageGuide;

        // ── 模块 2：选项 ──
        private RadioButton _chkHideAfterSearch;
        private RadioButton _chkTestMode;
        private CheckBox _chkDiagnosticLog;

        // ── 模块 4：结果（搜索后显示） ──
        private Label _lblResultSummary;
        private string _resultsEmptyMessage;
        private FlowLayoutPanel _matchActionsPanel;
        private Label _lblExportSelection;
        private Label _lblDuplicateInclusion;
        private ThemedButton _btnToggleDuplicateInclusion;
        private ThemedButton _btnRestoreSelection;
        private FlowLayoutPanel _resultFilterPanel;
        private SearchBox _resultSearchBox;
        private TableLayoutPanel _resultFilterRow;
        private string _resultSearchText = string.Empty;
        private DataGridView _resultsGrid;
        private SearchResultFilter _activeResultFilter = SearchResultFilter.All;
        private ThemedButton _btnSearch;
        private ThemedButton _btnExportResults;
        private ThemedButton _btnCreateSelectionSet;
        private ThemedButton _btnHideUnselected;
        private ThemedButton _btnClose;

        // ── 公共操作按钮 ──
        private TabControl _tabControl;
        private TabPage _tabConditions;
        private TabPage _tabOptions;
        private TabPage _tabResults;
        private NavTab _navConditions;
        private NavTab _navResults;
        private NavTab _navOptions;
        private Control _lblHeaderTitle;
        private Label _lblHeaderSubtitle;
        private ThemedButton _btnMode;
        // 同一时间只有一个浮动面板；记录刚关闭的锚点，避免“点按钮收起”被当成再次打开。
        private OptionPicker _activePicker;
        private Control _activePickerAnchor;
        private Control _lastPickerAnchor;
        private DateTime _pickerClosedAt = DateTime.MinValue;
        private ToastWindow _toast;
        private Panel _header;
        private NavIndicator _navIndicator;
        private PageTransition _pageTransition;
        private bool _fadeInOnShow;
        private ToolTip _toolTip;

        // ── 数据 ──
        private readonly Document _doc;
        private readonly string _initialXmlPath;
        private readonly IntPtr _ownerHandle;
        private List<SearchCondition> _conditions;
        // 手动添加、修改、删除后置位；导入或导出后清除。
        private bool _conditionsDirty;
        private string _currentXmlPath;

        // 无模式窗口：记住上次位置（跨重开），缓存搜索选中的全部对象以便还原。
        private static Point? _lastLocation;
        private List<ModelItem> _lastSearchSelection;

        // ── 搜索结果缓存 ──
        private List<SearchResult> _lastResults;
        private int _lastTotalMatched;
        private bool _lastHideExecuted;
        private string _currentModelPrefix;
        private List<ModelItem> _lastScopeRoots = new List<ModelItem>();
        private List<ModelItem> _lastMatchedItemsInScope = new List<ModelItem>();
        private List<ModelItem> _lastProtectedItems = new List<ModelItem>();
        private HashSet<int> _checkedExportResultIndices = new HashSet<int>();
        private HashSet<int> _includedDuplicateResultIndices = new HashSet<int>();
        private bool _updatingResultChecks;
        // 最近一次隐藏/选择操作的最终保留对象数量，用于完成提示。
        private int _lastFinalKeepCount;

        // ── 列索引常量 ──
        private const int COL_INDEX = 0;
        private const int COL_CATEGORY = 1;
        private const int COL_PROPERTY = 2;
        private const int COL_TEST = 3;
        private const int COL_VALUE = 4;
        private const int RESULT_COL_STATUS = 3;
        private const int RESULT_COL_EXPORT = 0;
        private const int RESULT_COL_INCLUDE_MATCH = 1;
        private static readonly Regex ModelPrefixRegex =
            new Regex(@"^(TS-M[0-9A-Z]+)-", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public SearchDialog(
            Document doc,
            string initialXmlPath = null,
            IntPtr ownerHandle = default(IntPtr))
        {
            _doc = doc;
            _initialXmlPath = initialXmlPath;
            _ownerHandle = ownerHandle;
            _conditions = new List<SearchCondition>();
            InitializeComponent();
            InitializeMeasurementLink();
            TryLoadInitialXml();
            RefreshConditionsGrid();
        }

        /// <summary>PluginEntry 用于判断再次点击时是否为同一文档。</summary>
        internal Document Document
        {
            get { return _doc; }
        }

        #region 初始化 UI

        private void InitializeComponent()
        {
            this.Text = "Curi · 傑出品";
            this.Size = new Size(ScaleLogical(980), ScaleLogical(700));
            this.MinimumSize = new Size(ScaleLogical(780), ScaleLogical(560));
            // 无模式窗口：手动定位到 Navisworks 主窗口右上角（见 PositionForModeless）。
            this.StartPosition = FormStartPosition.Manual;
            this.Font = UiTheme.BodyFont;
            this.BackColor = UiTheme.Canvas;
            this.ForeColor = UiTheme.TextBody;
            this.DoubleBuffered = true;
            this.Icon = null;
            _toolTip = new ToolTip { InitialDelay = 400, ReshowDelay = 100, AutoPopDelay = 8000 };

            // 系统页签条隐藏，由页头 NavTab 驱动切换。
            _tabControl = new HeaderlessTabControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            _tabConditions = CreateTabPage("搜索条件");
            _tabResults = CreateTabPage("结果");
            _tabOptions = CreateTabPage("选项");
            BuildConditionsTab();
            BuildResultsTab();
            BuildOptionsTab();
            _tabControl.TabPages.Add(_tabConditions);
            _tabControl.TabPages.Add(_tabResults);
            _tabControl.TabPages.Add(_tabOptions);

            Control header = BuildHeader();
            Control footer = BuildFooter();

            // Dock 按集合逆序计算：Fill 必须最先加入。
            this.Controls.Add(_tabControl);
            this.Controls.Add(header);
            this.Controls.Add(footer);
            this.CancelButton = _btnClose;

            // 浮动提示：独立的圆角小窗，真实淡入淡出；位于页脚上方居中。
            _toast = new ToastWindow(this, UiTheme.ControlHeight + ScaleLogical(24) + 1 + ScaleLogical(16));

            ApplyTooltips();

            _tabControl.SelectedIndexChanged += (s, e) => UpdateNavState();
            // 切到结果页即聚焦搜索框，直接输入编号即可查找。
            _tabControl.SelectedIndexChanged += (s, e) =>
            {
                if (_tabControl.SelectedTab == _tabResults && _lastResults != null)
                    BeginInvoke(new Action(() => _resultSearchBox.FocusAndSelectAll()));
            };
            _tabControl.HandleCreated += (s, e) => UpdateNavState();
            this.Shown += (s, e) =>
            {
                UpdateNavState();
                UpdateNavIndicator(animate: false);
            };
            _tabControl.EnabledChanged += (s, e) => UpdateNavState();
            UpdateNavState();
            UpdateModeHint();
        }

        private TabPage CreateTabPage(string text)
        {
            return new TabPage(text)
            {
                BackColor = UiTheme.Canvas,
                Padding = new Padding(ScaleLogical(20), ScaleLogical(16), ScaleLogical(20), ScaleLogical(16)),
                Margin = new Padding(0),
            };
        }

        /// <summary>在页脚上方居中显示一条短暂提示，数秒后自动淡出。</summary>
        private void ShowToast(string message)
        {
            if (_toast == null || _toast.IsDisposed || IsDisposed)
                return;
            _toast.ShowMessage(message);
        }

        private void ApplyTooltips()
        {
            void Tip(Control control, string text) => _toolTip.SetToolTip(control, text);
            Tip(_btnImportXml, "载入 XML 查找文件，替换当前条件（也可直接把文件拖进窗口）");
            Tip(_btnExportXml, "把当前条件保存为 XML，下次可直接导入");
            Tip(_btnAddCondition, "手动添加一条条件");
            Tip(_btnDeleteCondition, "删除选中的条件，可多选（Delete）");
            Tip(_btnClearConditions, "删除全部条件");
            Tip(_btnUsageGuide, "四步上手指南");
            Tip(_btnSearch, "在选择树选中的范围内查找（Ctrl+Enter）");
            Tip(_btnMode, "切换搜索模式：仅选中，或选中后确认隐藏");
            Tip(_btnExportResults, "把结果清单导出为 CSV / TXT");
            Tip(_btnCreateSelectionSet, "把匹配对象保存为 Navisworks 选择集，随 .nwf 保存");
            Tip(_btnHideUnselected, "隐藏范围内未匹配的对象；可在 Navisworks 常用 → 全部显示 恢复");
            Tip(_btnRestoreSelection, "查看单个结果后，恢复为本次搜索选中的全部对象");
            Tip(_resultSearchBox, "输入编号片段即时过滤；Enter 在模型中定位，Esc 清空");
        }

        /// <summary>
        /// 页头单行：品牌字标 | 导航页签 …… 条件来源（右对齐）。
        /// 页签下划线贴合页头底边分隔线。
        /// </summary>
        private Control BuildHeader()
        {
            _navConditions = CreateNavTab(_tabConditions);
            _navResults = CreateNavTab(_tabResults);
            _navOptions = CreateNavTab(_tabOptions);
            int rowHeight = _navConditions.Height;

            // 品牌字标：“Curi”（创造 Create + 好奇 Curious）+ 强调色圆点，自绘以精确控制字距。
            var brand = new BrandMark("Curi")
            {
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 0, ScaleLogical(3)),
            };
            _lblHeaderTitle = brand;

            var divider = new Panel
            {
                Width = 1,
                Height = UiTheme.TextHeight(UiTheme.BodyFont),
                Anchor = AnchorStyles.Left,
                BackColor = UiTheme.Border,
                Margin = new Padding(ScaleLogical(20), 0, ScaleLogical(24), ScaleLogical(3)),
            };

            var nav = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = UiTheme.Surface,
            };
            nav.Controls.AddRange(new Control[] { _navConditions, _navResults, _navOptions });

            _lblHeaderSubtitle = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted,
                Margin = new Padding(ScaleLogical(16), 0, 0, ScaleLogical(3)),
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(ScaleLogical(20), 0, ScaleLogical(20), 0),
                Margin = new Padding(0),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
            layout.Controls.Add(brand, 0, 0);
            layout.Controls.Add(divider, 1, 0);
            layout.Controls.Add(nav, 2, 0);
            layout.Controls.Add(_lblHeaderSubtitle, 3, 0);
            _btnTrayMeasurement = MakeToolButton("桥架测量 ▾", ToggleMeasurementPicker);
            _btnTrayMeasurement.Anchor = AnchorStyles.Right;
            _btnTrayMeasurement.Margin = new Padding(ScaleLogical(16), 0, 0, ScaleLogical(3));
            layout.Controls.Add(_btnTrayMeasurement, 4, 0);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = UiTheme.Surface,
                Height = rowHeight + ScaleLogical(8) + 1,
                Padding = new Padding(0, ScaleLogical(8), 0, 0),
            };
            header.Controls.Add(layout);
            header.Controls.Add(CreateHairline(DockStyle.Bottom));

            // 导航下划线：独立控件，在页签间滑动。
            _header = header;
            _navIndicator = new NavIndicator();
            header.Controls.Add(_navIndicator);
            _navIndicator.BringToFront();
            nav.Layout += (s, e) => UpdateNavIndicator(animate: false);
            header.Resize += (s, e) => UpdateNavIndicator(animate: false);
            return header;
        }

        private void UpdateNavIndicator(bool animate)
        {
            if (_navIndicator == null || !_header.IsHandleCreated)
                return;
            TabPage selected = _tabControl.SelectedTab ?? _tabConditions;
            NavTab tab = selected == _tabResults ? _navResults
                : selected == _tabOptions ? _navOptions
                : _navConditions;
            if (!tab.IsHandleCreated)
                return;
            Point origin = _header.PointToClient(tab.PointToScreen(Point.Empty));
            int barHeight = _navIndicator.Height;
            _navIndicator.Enabled = _tabControl.Enabled;
            _navIndicator.MoveTo(
                new Rectangle(origin.X, _header.Height - 1 - barHeight, tab.Width, barHeight),
                animate);
        }

        /// <summary>
        /// 切换页面：截取前后画面做交叉淡入（新页面轻微上移），窗口不可见或半透明时直接切换。
        /// </summary>
        private void SwitchTab(TabPage page)
        {
            if (page == null || _tabControl.SelectedTab == page)
                return;
            _pageTransition?.Finish();
            _pageTransition = null;

            bool animate = IsHandleCreated && Visible && Opacity >= 0.99
                && WindowState != FormWindowState.Minimized;
            Bitmap from = animate ? PageTransition.Snapshot(_tabControl) : null;
            _tabControl.SelectedTab = page;
            if (from == null)
                return;
            page.PerformLayout();
            Bitmap to = PageTransition.Snapshot(_tabControl);
            _pageTransition = PageTransition.Play(_tabControl, from, to);
        }

        private NavTab CreateNavTab(TabPage page)
        {
            var tab = new NavTab { Text = page.Text };
            tab.Click += (s, e) =>
            {
                if (_tabControl.Enabled)
                    SwitchTab(page);
            };
            return tab;
        }

        private static Control CreateHairline(DockStyle dock)
        {
            return new Panel { Dock = dock, Height = 1, BackColor = UiTheme.Border };
        }

        /// <summary>页脚：主操作在左，结果操作靠右，关闭独立。</summary>
        private Control BuildFooter()
        {
            _btnSearch = new ThemedButton(ButtonKind.Primary)
            {
                Text = "执行搜索",
                MinimumSize = new Size(ScaleLogical(116), UiTheme.ControlHeight),
                Margin = new Padding(0),
            };
            _btnSearch.Click += BtnSearch_Click;

            // 搜索模式：与导出结果同样的下拉按钮样式，一眼可知可点击；紧邻执行搜索，表明它决定搜索行为。
            _btnMode = new ThemedButton(ButtonKind.Secondary)
            {
                Margin = new Padding(ScaleLogical(8), 0, 0, 0),
            };
            _btnMode.Click += (s, e) => ToggleModePicker();

            _btnExportResults = new ThemedButton(ButtonKind.Secondary)
            {
                Text = "导出结果 ▾",
                Enabled = false,
            };
            _btnExportResults.Click += BtnExportResults_Click;

            _btnCreateSelectionSet = new ThemedButton(ButtonKind.Secondary)
            {
                Text = "创建选择集",
                Enabled = false,
            };
            _btnCreateSelectionSet.Click += BtnCreateSelectionSet_Click;

            _btnHideUnselected = new ThemedButton(ButtonKind.Danger)
            {
                Text = "隐藏未选中",
                Enabled = false,
            };
            _btnHideUnselected.Click += BtnHideUnselected_Click;

            _btnClose = new ThemedButton(ButtonKind.Secondary)
            {
                Text = "关闭",
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(0),
            };
            // 无模式窗口下 DialogResult/CancelButton 不会自动关窗，需显式关闭
            // （Esc 经 CancelButton 也会触发此 Click）。
            _btnClose.Click += (s, e) => this.Close();

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = UiTheme.Surface,
            };
            actions.Controls.AddRange(new Control[]
            {
                _btnExportResults,
                _btnCreateSelectionSet,
                _btnHideUnselected,
                UiTheme.CreateDivider(),
                _btnClose,
            });

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(ScaleLogical(20), ScaleLogical(12), ScaleLogical(20), ScaleLogical(12)),
                Margin = new Padding(0),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(_btnSearch, 0, 0);
            layout.Controls.Add(_btnMode, 1, 0);
            layout.Controls.Add(actions, 3, 0);

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                BackColor = UiTheme.Surface,
                Height = UiTheme.ControlHeight + layout.Padding.Vertical + 1,
            };
            footer.Controls.Add(layout);
            footer.Controls.Add(CreateHairline(DockStyle.Top));
            return footer;
        }

        private void UpdateNavState()
        {
            if (_navConditions == null)
                return;
            bool enabled = _tabControl.Enabled;
            foreach (NavTab tab in new[] { _navConditions, _navResults, _navOptions })
                tab.Enabled = enabled;
            // 句柄创建前 SelectedIndex 可能为 -1，此时按首页处理。
            TabPage selected = _tabControl.SelectedTab ?? _tabConditions;
            _navConditions.Active = selected == _tabConditions;
            _navResults.Active = selected == _tabResults;
            _navOptions.Active = selected == _tabOptions;
            UpdateNavIndicator(animate: true);
        }

        private void UpdateNavCaptions()
        {
            if (_navConditions == null)
                return;
            _navConditions.Text = _conditions.Count > 0
                ? $"搜索条件  {_conditions.Count}"
                : "搜索条件";
            int resultCount = _lastResults?.Count ?? 0;
            _navResults.Text = resultCount > 0 ? $"结果  {resultCount}" : "结果";

            // 仅在导入 XML 后显示来源文件名；手动条件不标注。
            _lblHeaderSubtitle.Text = string.IsNullOrEmpty(_currentXmlPath)
                ? string.Empty
                : Path.GetFileName(_currentXmlPath);
        }

        /// <summary>搜索模式面板：上方浮出，标题 + 说明两行，当前模式高亮。</summary>
        private OptionPicker BuildModePicker()
        {
            var picker = new OptionPicker(
                "搜索模式",
                new[]
                {
                    new PickerOption("仅选中", "只选中匹配对象，不改变模型可见性", "推荐"),
                    new PickerOption("选中并隐藏", "选中后弹窗确认，再隐藏范围内其余对象"),
                },
                _chkHideAfterSearch.Checked ? 1 : 0);
            picker.Chosen += (s, index) =>
            {
                if (index == 0)
                    _chkTestMode.Checked = true;
                else
                    _chkHideAfterSearch.Checked = true;
            };
            return picker;
        }

        private void ToggleModePicker()
        {
            TogglePicker(_btnMode, BuildModePicker, alignRight: false, afterClose: null);
        }

        /// <summary>
        /// 打开或收起锚定在按钮上的浮动面板。afterClose 在面板完全关闭后执行，
        /// 用于会弹出文件对话框等的动作，避免与关闭动画抢焦点。
        /// </summary>
        private void TogglePicker(
            ThemedButton anchor,
            Func<OptionPicker> build,
            bool alignRight,
            Action<int> afterClose,
            bool openBelow = false)
        {
            if (!_tabControl.Enabled || !anchor.Enabled)
                return;
            // 面板失焦关闭发生在按钮点击之前：刚因这次点击关闭的面板不再重开。
            if (_activePicker != null
                || (ReferenceEquals(_lastPickerAnchor, anchor)
                    && (DateTime.UtcNow - _pickerClosedAt).TotalMilliseconds < 250))
                return;

            OptionPicker picker = build();
            _activePicker = picker;
            _activePickerAnchor = anchor;
            anchor.Active = true;
            UpdatePickerArrows();
            picker.FormClosed += (s, e) =>
            {
                _activePicker = null;
                _activePickerAnchor = null;
                _lastPickerAnchor = anchor;
                _pickerClosedAt = DateTime.UtcNow;
                if (anchor.IsDisposed)
                    return;
                anchor.Active = false;
                UpdatePickerArrows();
                int chosen = picker.ChosenIndex;
                if (afterClose != null && chosen >= 0)
                    BeginInvoke(new Action(() => afterClose(chosen)));
            };
            picker.ShowNear(anchor, this, alignRight, openBelow);
        }

        private void UpdatePickerArrows()
        {
            UpdateModeHint();
            if (_btnTrayMeasurement != null)
                _btnTrayMeasurement.Text = ReferenceEquals(_activePickerAnchor, _btnTrayMeasurement)
                    ? "桥架测量 ▴" : "桥架测量 ▾";
            if (_btnExportResults != null)
            {
                _btnExportResults.Text = ReferenceEquals(_activePickerAnchor, _btnExportResults)
                    ? "导出结果 ▴"
                    : "导出结果 ▾";
            }
        }

        /// <summary>导出面板：三种范围及各自条数；没有可导出条目的范围禁用并说明原因。</summary>
        private OptionPicker BuildExportPicker()
        {
            List<int> allIds = (_lastResults ?? new List<SearchResult>())
                .Where(result => result?.Condition != null)
                .Select(result => result.Condition.DisplayIndex)
                .ToList();
            int visibleCount = GetCurrentFilteredResults().Count;
            int checkedCount = _checkedExportResultIndices.Count;
            bool filtered = visibleCount != allIds.Count;

            return new OptionPicker(
                "导出结果为 CSV / TXT",
                new[]
                {
                    new PickerOption(
                        "导出已勾选",
                        checkedCount > 0 ? "导出列中勾选的条件" : "先在结果表格的导出列中勾选条件",
                        checkedCount.ToString(),
                        checkedCount > 0),
                    new PickerOption(
                        "导出当前筛选",
                        filtered ? "当前筛选与搜索下可见的结果" : "当前未筛选，与全部结果相同",
                        visibleCount.ToString(),
                        visibleCount > 0),
                    new PickerOption(
                        "导出全部结果",
                        "本次搜索的全部条件",
                        allIds.Count.ToString(),
                        allIds.Count > 0),
                },
                -1);
        }

        private void UpdateModeHint()
        {
            if (_btnMode == null || _chkHideAfterSearch == null)
                return;
            // 按钮上只写简短模式名，完整说明在面板里；展开时箭头朝上。
            string arrow = ReferenceEquals(_activePickerAnchor, _btnMode) ? "▴" : "▾";
            _btnMode.Text = (_chkHideAfterSearch.Checked ? "模式：选中并隐藏 " : "模式：仅选中 ") + arrow;
        }

        /// <summary>
        /// 创建工具栏按钮，统一高度并支持 DPI 缩放。
        /// </summary>
        private static ThemedButton MakeToolButton(
            string text,
            EventHandler onClick,
            ButtonKind kind = ButtonKind.Secondary)
        {
            var btn = new ThemedButton(kind) { Text = text };
            btn.Click += onClick;
            return btn;
        }

        private static int ScaleLogical(int logicalPixels)
        {
            return UiTheme.Scale(logicalPixels);
        }

        private static int CalculateButtonHeight(Font font)
        {
            return UiTheme.TextHeight(font) + ScaleLogical(14);
        }

        private void TryLoadInitialXml()
        {
            if (string.IsNullOrWhiteSpace(_initialXmlPath))
                return;

            try
            {
                LoadFromXml(_initialXmlPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "自动加载 XML 失败：\n" + ex.Message,
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        #endregion

        #region 选项卡构建

        private void BuildConditionsTab()
        {
            _tabConditions.SuspendLayout();

            _btnAddCondition = MakeToolButton("＋ 添加", BtnAddCondition_Click);
            _btnDeleteCondition = MakeToolButton("删除", BtnDeleteCondition_Click);
            _btnClearConditions = MakeToolButton("清空", BtnClearConditions_Click);
            _btnImportXml = MakeToolButton("导入 XML", BtnImportXml_Click);
            _btnExportXml = MakeToolButton("导出 XML", BtnExportXml_Click);
            _btnUsageGuide = MakeToolButton("？ 使用说明", BtnUsageGuide_Click, ButtonKind.Ghost);
            _btnUsageGuide.Margin = new Padding(0);

            // 分组：导入/导出（数据进出）| 添加/删除/清空（逐条编辑）。
            var editGroup = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };
            editGroup.Controls.AddRange(new Control[]
            {
                _btnImportXml,
                _btnExportXml,
                UiTheme.CreateDivider(),
                _btnAddCondition,
                _btnDeleteCondition,
                _btnClearConditions,
            });

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, ScaleLogical(12)),
                Padding = new Padding(0),
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.Controls.Add(editGroup, 0, 0);
            toolbar.Controls.Add(_btnUsageGuide, 1, 0);

            _conditionsGrid = CreateSearchGrid();
            _conditionsGrid.Margin = new Padding(1);
            _conditionsGrid.MultiSelect = true;
            _conditionsGrid.CellDoubleClick += ConditionsGrid_CellDoubleClick;
            _conditionsGrid.SelectionChanged += (s, e) => UpdateConditionActionState();
            _conditionsGrid.KeyDown += ConditionsGrid_KeyDown;
            _conditionsGrid.CellMouseDown += (s, e) =>
            {
                // 右键点在未选中的行上时，先选中该行再弹菜单。
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0
                    && !_conditionsGrid.Rows[e.RowIndex].Selected)
                {
                    _conditionsGrid.ClearSelection();
                    _conditionsGrid.CurrentCell = _conditionsGrid.Rows[e.RowIndex].Cells[COL_VALUE];
                }
            };
            _conditionsGrid.ContextMenuStrip = BuildConditionsMenu();
            UiTheme.AttachEmptyState(_conditionsGrid, () =>
                "还没有搜索条件\n\n点击导入 XML 载入查找文件，或点击添加手动填写一条\n也可以直接把 XML 文件拖进窗口", UiTheme.EmptyIcon.Document);

            // 支持把 XML 文件直接拖进窗口导入。
            this.AllowDrop = true;
            this.DragEnter += (s, e) =>
            {
                e.Effect = GetDroppedXmlPath(e.Data) != null && _tabControl.Enabled
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            };
            this.DragDrop += (s, e) =>
            {
                string path = GetDroppedXmlPath(e.Data);
                if (path == null || !ConfirmDiscardUnsavedConditions("导入会替换当前全部条件。"))
                    return;
                try
                {
                    LoadFromXml(path);
                    SwitchTab(_tabConditions);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导入 XML 失败：\n" + ex.Message,
                        "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = UiTheme.Canvas,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(toolbar, 0, 0);
            root.Controls.Add(_conditionsGrid, 0, 1);
            UiTheme.AttachHairlineBorder(root, _conditionsGrid);

            _tabConditions.Controls.Add(root);
            _tabConditions.ResumeLayout(true);
        }

        private void BtnClearConditions_Click(object sender, EventArgs e)
        {
            if (_conditions.Count == 0)
                return;
            if (MessageBox.Show(this,
                    $"确定清空全部 {_conditions.Count} 条搜索条件吗？此操作不可撤销。",
                    "傑出品·清空条件",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.OK)
                return;

            _conditions.Clear();
            _conditionsDirty = false;
            RefreshConditionsGrid();
            InvalidateSearchResults("条件已清空，请添加条件后重新执行搜索。");
        }

        private void ConditionsGrid_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyData)
            {
                case Keys.Delete:
                    BtnDeleteCondition_Click(null, null);
                    break;
                case Keys.Enter:
                case Keys.F2:
                    EditSelectedCondition();
                    break;
                case Keys.Control | Keys.D:
                    DuplicateSelectedConditions();
                    break;
                case Keys.Alt | Keys.Up:
                    MoveSelectedCondition(-1);
                    break;
                case Keys.Alt | Keys.Down:
                    MoveSelectedCondition(1);
                    break;
                default:
                    return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private ContextMenuStrip BuildConditionsMenu()
        {
            var menu = new ContextMenuStrip { Font = UiTheme.BodyFont, ShowImageMargin = false };
            var edit = new ToolStripMenuItem("编辑", null, (s, e) => EditSelectedCondition()) { ShortcutKeyDisplayString = "Enter" };
            var copy = new ToolStripMenuItem("复制一条", null, (s, e) => DuplicateSelectedConditions()) { ShortcutKeyDisplayString = "Ctrl+D" };
            var up = new ToolStripMenuItem("上移", null, (s, e) => MoveSelectedCondition(-1)) { ShortcutKeyDisplayString = "Alt+↑" };
            var down = new ToolStripMenuItem("下移", null, (s, e) => MoveSelectedCondition(1)) { ShortcutKeyDisplayString = "Alt+↓" };
            var delete = new ToolStripMenuItem("删除", null, (s, e) => BtnDeleteCondition_Click(null, null)) { ShortcutKeyDisplayString = "Delete" };
            var add = new ToolStripMenuItem("添加条件", null, (s, e) => BtnAddCondition_Click(null, null));
            menu.Items.AddRange(new ToolStripItem[]
            {
                edit, copy, new ToolStripSeparator(), up, down, new ToolStripSeparator(), delete, new ToolStripSeparator(), add,
            });
            menu.Opening += (s, e) =>
            {
                List<int> indices = GetSelectedConditionIndices();
                bool single = indices.Count == 1;
                edit.Enabled = single;
                copy.Enabled = indices.Count > 0;
                up.Enabled = single && indices[0] > 0;
                down.Enabled = single && indices[0] < _conditions.Count - 1;
                delete.Enabled = indices.Count > 0;
                delete.Text = indices.Count > 1 ? $"删除 {indices.Count} 条" : "删除";
            };
            ThemedMenuRenderer.Apply(menu);
            return menu;
        }

        private static string GetDroppedXmlPath(IDataObject data)
        {
            if (!(data?.GetData(DataFormats.FileDrop) is string[] files) || files.Length != 1)
                return null;
            return string.Equals(Path.GetExtension(files[0]), ".xml", StringComparison.OrdinalIgnoreCase)
                ? files[0]
                : null;
        }

        /// <summary>
        /// 创建统一风格的搜索条件表格。
        /// </summary>
        private DataGridView CreateSearchGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ColumnHeadersVisible = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            };
            UiTheme.StyleGrid(grid);
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Index",
                HeaderText = "#",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = ScaleLogical(52),
                DefaultCellStyle = { ForeColor = UiTheme.TextMuted },
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "分类", FillWeight = 18F });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Property", HeaderText = "属性", FillWeight = 22F });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Test", HeaderText = "匹配方式", FillWeight = 12F });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "查询值", FillWeight = 48F });
            return grid;
        }

        private void BuildOptionsTab()
        {
            _tabOptions.SuspendLayout();
            _tabOptions.AutoScroll = true;

            var card = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(ScaleLogical(24), ScaleLogical(8), ScaleLogical(24), ScaleLogical(16)),
                Margin = new Padding(0),
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // 两种模式互斥：用单选按钮表达，而非两个互相联动的复选框。
            _chkTestMode = MakeOptionRadio("仅选中（推荐）", true);
            _chkHideAfterSearch = MakeOptionRadio("选中并隐藏", false);
            _chkHideAfterSearch.CheckedChanged += (s, e) => UpdateModeHint();

            _chkDiagnosticLog = new CheckBox
            {
                Text = "启用诊断日志",
                AutoSize = true,
                Checked = false,
                ForeColor = UiTheme.Text,
                Margin = new Padding(0, ScaleLogical(10), 0, 0),
            };

            AddOptionSection(card, "搜索模式", "决定执行搜索后是否继续隐藏未选中的对象。", 1,
                _chkTestMode,
                MakeOptionNote("搜索后只选中匹配对象，不改变可见性，适合先核对结果。"),
                _chkHideAfterSearch,
                MakeOptionNote("选中后弹窗确认再隐藏；存在未找到、重复或条件异常时会暂停并提示。"));
            AddOptionSection(card, "搜索范围",
                "搜索范围由 Navisworks 选择树当前选中的节点决定。\n" +
                "如果没有预先选中范围，执行搜索会拒绝本次操作；不会搜索整个模型。", 2);
            AddOptionSection(card, "诊断日志", "每次搜索结束后输出详细诊断文件，仅在排查问题时开启。", 1,
                _chkDiagnosticLog);

            _tabOptions.Controls.Add(card);
            UiTheme.AttachHairlineBorder(_tabOptions, card);
            _tabOptions.ResumeLayout(true);
        }

        private RadioButton MakeOptionRadio(string text, bool isChecked)
        {
            return new RadioButton
            {
                Text = text,
                AutoSize = true,
                Checked = isChecked,
                ForeColor = UiTheme.Text,
                Margin = new Padding(0, ScaleLogical(10), 0, 0),
            };
        }

        private Label MakeOptionNote(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = UiTheme.TextHeight(UiTheme.BodyFont),
                ForeColor = UiTheme.TextMuted,
                // 与单选按钮文字左对齐。
                Margin = new Padding(ScaleLogical(18), ScaleLogical(2), 0, 0),
            };
        }

        private void AddOptionSection(
            TableLayoutPanel card,
            string title,
            string description,
            int descriptionLines,
            params Control[] content)
        {
            if (card.Controls.Count > 0)
            {
                var separator = new Panel
                {
                    Height = 1,
                    BackColor = UiTheme.Border,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right,
                    Margin = new Padding(0, ScaleLogical(16), 0, 0),
                };
                AddOptionRow(card, separator);
            }

            AddOptionRow(card, new Label
            {
                Text = title,
                Font = UiTheme.BodyStrongFont,
                ForeColor = UiTheme.Text,
                AutoSize = true,
                Margin = new Padding(0, ScaleLogical(16), 0, 0),
            });
            AddOptionRow(card, new Label
            {
                Text = description,
                AutoSize = false,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = UiTheme.TextHeight(UiTheme.BodyFont) * descriptionLines,
                ForeColor = UiTheme.TextMuted,
                Margin = new Padding(0, ScaleLogical(4), 0, 0),
            });
            foreach (Control control in content)
                AddOptionRow(card, control);
        }

        private static void AddOptionRow(TableLayoutPanel card, Control control)
        {
            card.RowCount += 1;
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Controls.Add(control, 0, card.RowCount - 1);
        }

        private void BuildResultsTab()
        {
            _tabResults.SuspendLayout();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = UiTheme.Canvas,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // 摘要：单行文字，位于筛选按钮之上；无结果时整组工具行隐藏。
            _lblResultSummary = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                Height = UiTheme.TextHeight(UiTheme.BodyStrongFont),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.BodyStrongFont,
                ForeColor = UiTheme.Text,
                BackColor = UiTheme.Canvas,
                Margin = new Padding(0, 0, 0, ScaleLogical(10)),
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _resultFilterPanel = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = UiTheme.Canvas,
            };

            // 结果内搜索：输入编号或查询值即时过滤，Enter 定位到模型，Esc 清空。
            _resultSearchBox = new SearchBox("搜索编号或查询值，#12 按序号")
            {
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Width = ScaleLogical(260),
                Margin = new Padding(ScaleLogical(12), 0, 0, 0),
            };
            _resultSearchBox.SearchTextChanged += (s, e) =>
            {
                _resultSearchText = _resultSearchBox.SearchText;
                RefreshResultsGrid(_lastResults ?? Enumerable.Empty<SearchResult>());
                SelectFirstResultRow();
            };
            _resultSearchBox.EnterPressed += (s, e) => LocateFirstSearchMatch();
            _resultSearchBox.DownPressed += (s, e) => _resultsGrid.Focus();

            _resultFilterRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Height = UiTheme.ControlHeight,
                Margin = new Padding(0, 0, 0, ScaleLogical(8)),
                Padding = new Padding(0),
                BackColor = UiTheme.Canvas,
            };
            _resultFilterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _resultFilterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _resultFilterRow.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight));
            _resultFilterRow.Controls.Add(_resultFilterPanel, 0, 0);
            _resultFilterRow.Controls.Add(_resultSearchBox, 1, 0);
            AddResultFilterButton(SearchResultFilter.All, "全部");
            AddResultFilterButton(SearchResultFilter.Problems, "问题项");
            AddResultFilterButton(SearchResultFilter.Found, "已找到");
            AddResultFilterButton(SearchResultFilter.NotFound, "未找到");
            AddResultFilterButton(SearchResultFilter.Duplicate, "重复");
            AddResultFilterButton(SearchResultFilter.ConditionInvalid, "条件异常");

            _matchActionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, ScaleLogical(10)),
                Padding = new Padding(0),
                BackColor = UiTheme.Canvas,
            };
            _btnToggleDuplicateInclusion = new ThemedButton(ButtonKind.Secondary)
            {
                Text = "全选重复项",
                MinimumSize = new Size(ScaleLogical(120), UiTheme.ControlHeight),
                Enabled = false,
            };
            _btnToggleDuplicateInclusion.Click += (s, e) =>
                ToggleDuplicateInclusion(GetDuplicateResultIds(_lastResults));

            _lblDuplicateInclusion = MakeInlineStatusLabel(new Padding(0, 0, ScaleLogical(8), 0));
            _lblExportSelection = MakeInlineStatusLabel(new Padding(0));
            _lblExportSelection.Text = "导出已勾选 0 条";

            // 查看重复对象后，一键把当前选择还原为搜索选中的全部对象。
            _btnRestoreSelection = new ThemedButton(ButtonKind.Secondary)
            {
                Text = "还原全部选择",
                Enabled = false,
            };
            _btnRestoreSelection.Click += BtnRestoreSelection_Click;

            _matchActionsPanel.Controls.AddRange(new Control[]
            {
                _btnToggleDuplicateInclusion,
                _lblDuplicateInclusion,
                UiTheme.CreateDivider(),
                _btnRestoreSelection,
                _lblExportSelection,
            });

            _resultsGrid = CreateResultsGrid();
            _resultsGrid.Margin = new Padding(1);
            _resultsGrid.CellDoubleClick += ResultsGrid_CellDoubleClick;
            _resultsGrid.CellMouseDown += (s, e) =>
            {
                // 右键先把该行设为当前行，菜单才作用于正确的结果。
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
                    _resultsGrid.CurrentCell = _resultsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            };
            _resultsGrid.ContextMenuStrip = BuildResultsMenu();
            _resultsGrid.KeyDown += (s, e) =>
            {
                if (e.KeyData == Keys.Enter)
                {
                    SearchResult current = GetCurrentResult();
                    if (current != null)
                        ResultsGrid_CellDoubleClick(_resultsGrid,
                            new DataGridViewCellEventArgs(RESULT_COL_STATUS, _resultsGrid.CurrentRow.Index));
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyData == Keys.Up && _resultsGrid.CurrentRow?.Index == 0)
                {
                    FocusResultSearch();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            // 在列表中直接打字即转入搜索框，无需快捷键。
            _resultsGrid.KeyPress += (s, e) =>
            {
                if (char.IsControl(e.KeyChar) || _lastResults == null)
                    return;
                _resultSearchBox.AppendAndFocus(e.KeyChar);
                e.Handled = true;
            };
            _resultsGrid.CellValueChanged += ResultsGrid_CellValueChanged;
            _resultsGrid.CellPainting += ResultsGrid_CellPainting;
            _resultsGrid.CurrentCellDirtyStateChanged +=
                ResultsGrid_CurrentCellDirtyStateChanged;
            _resultsGrid.ColumnHeaderMouseClick +=
                ResultsGrid_ColumnHeaderMouseClick;
            // 无结果时，状态提示（尚未搜索、条件已修改、搜索中）显示在表格中央。
            UiTheme.AttachEmptyState(_resultsGrid, () => _lastResults == null
                ? (_resultsEmptyMessage ?? "还没有搜索结果\n\n执行搜索后，这里会逐条列出每个条件的匹配情况。")
                : string.IsNullOrEmpty(_resultSearchText)
                    ? "当前筛选下没有结果。"
                    : $"没有找到“{_resultSearchText}”\n\n可切换到全部筛选，或检查输入的编号。", UiTheme.EmptyIcon.Search);
            SetActiveResultFilter(SearchResultFilter.All);

            root.Controls.Add(_lblResultSummary, 0, 0);
            root.Controls.Add(_resultFilterRow, 0, 1);
            root.Controls.Add(_matchActionsPanel, 0, 2);
            root.Controls.Add(_resultsGrid, 0, 3);
            UiTheme.AttachHairlineBorder(root, _resultsGrid);
            UpdateResultChromeVisibility();
            _tabResults.Controls.Add(root);
            _tabResults.ResumeLayout(true);
        }

        /// <summary>有结果时才显示摘要、筛选与重复项工具行。</summary>
        private void UpdateResultChromeVisibility()
        {
            if (_matchActionsPanel == null)
                return;
            bool hasResults = _lastResults != null;
            _lblResultSummary.Visible = hasResults;
            _resultFilterRow.Visible = hasResults;
            _matchActionsPanel.Visible = hasResults;
            _resultsGrid.Invalidate();
        }

        private Label MakeInlineStatusLabel(Padding margin)
        {
            return new Label
            {
                AutoSize = true,
                MinimumSize = new Size(0, UiTheme.ControlHeight),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextMuted,
                Margin = margin,
            };
        }

        private void AddResultFilterButton(SearchResultFilter filter, string text)
        {
            var button = new ThemedButton(ButtonKind.Secondary)
            {
                Text = text,
                Tag = filter,
                MinimumSize = new Size(ScaleLogical(64), UiTheme.ControlHeight),
                Margin = new Padding(0, 0, ScaleLogical(6), 0),
            };
            button.Click += ResultFilterButton_Click;
            _resultFilterPanel.Controls.Add(button);
        }

        private DataGridView CreateResultsGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EditMode = DataGridViewEditMode.EditOnEnter,
                AutoGenerateColumns = false,
            };
            UiTheme.StyleGrid(grid);

            var exportColumn = new DataGridViewCheckBoxColumn
            {
                Name = "ExportSelected",
                HeaderText = "导出",
                Width = ScaleLogical(72),
                MinimumWidth = ScaleLogical(72),
                ThreeState = false,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = false,
                Resizable = DataGridViewTriState.False,
                FlatStyle = FlatStyle.Standard,
            };
            exportColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            exportColumn.DefaultCellStyle.Padding = new Padding(0);
            exportColumn.HeaderCell.ToolTipText = "单击表头可勾选或取消当前筛选下的全部结果，仅用于导出";
            grid.Columns.Add(exportColumn);
            var includeMatchColumn = new DataGridViewCheckBoxColumn
            {
                Name = "IncludeInMatch",
                HeaderText = "计入匹配",
                Width = ScaleLogical(108),
                MinimumWidth = ScaleLogical(108),
                ThreeState = false,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = false,
                Resizable = DataGridViewTriState.False,
                FlatStyle = FlatStyle.Standard,
            };
            includeMatchColumn.DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;
            includeMatchColumn.DefaultCellStyle.Padding = new Padding(0);
            includeMatchColumn.HeaderCell.ToolTipText =
                "单击表头全选或取消当前筛选中的重复项；唯一找到项固定计入";
            grid.Columns.Add(includeMatchColumn);
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ConditionIndex",
                HeaderText = "#",
                Width = ScaleLogical(52),
                DefaultCellStyle = { ForeColor = UiTheme.TextMuted },
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "状态",
                Width = ScaleLogical(96),
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Category",
                HeaderText = "分类",
                Width = ScaleLogical(110),
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Property",
                HeaderText = "属性名",
                Width = ScaleLogical(128),
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Test",
                HeaderText = "匹配方式",
                Width = ScaleLogical(86),
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Value",
                HeaderText = "查询值",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 55F,
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MatchCount",
                HeaderText = "匹配数",
                Width = ScaleLogical(70),
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight },
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Message",
                HeaderText = "说明",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 45F,
            });
            for (int columnIndex = 2; columnIndex < grid.Columns.Count; columnIndex++)
                grid.Columns[columnIndex].ReadOnly = true;
            return grid;
        }

        private void ResultsGrid_CellDoubleClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _resultsGrid.Rows.Count)
                return;
            if (e.ColumnIndex == RESULT_COL_EXPORT
                || e.ColumnIndex == RESULT_COL_INCLUDE_MATCH)
                return;

            var result = _resultsGrid.Rows[e.RowIndex].Tag as SearchResult;
            if (result == null)
                return;

            // 双击：有匹配对象则在模型中定位；没有（未找到、条件异常）则直接打开该条件修改。
            if (result.MatchedItems != null && result.MatchedItems.Count > 0)
                LocateResultInModel(result);
            else
                EditConditionForResult(result);
        }

        private void LocateResultInModel(SearchResult result)
        {
            if (result?.MatchedItems == null || result.MatchedItems.Count == 0)
                return;
            if (!EnsureDocumentUsable())
                return;

            try
            {
                List<ModelItem> selected = SelectionService.SetSelection(
                    _doc,
                    result.MatchedItems);
                // 相机对准选中对象；COM 失败为非致命，选择仍保留。
                ViewFocusService.ZoomToCurrentSelection(_doc);
                int displayIndex = result.Condition != null
                    ? result.Condition.DisplayIndex
                    : 0;
                ShowToast($"已在模型中定位条件 #{displayIndex} 的 {selected.Count} 个对象；" +
                    "点还原全部选择可恢复。");
            }
            catch (Exception ex)
            {
                // 仅“设为选择”本身失败才提示；相机缩放失败已被静默处理。
                MessageBox.Show(
                    this,
                    "定位失败：\n" + ex.Message,
                    "Curi · 定位结果",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void GoToConditionForResult(SearchResult result)
        {
            int conditionIndex = result?.Condition?.ConditionIndex ?? -1;
            if (conditionIndex < 0 || conditionIndex >= _conditionsGrid.Rows.Count)
                return;

            SwitchTab(_tabConditions);
            _conditionsGrid.ClearSelection();
            DataGridViewRow conditionRow = _conditionsGrid.Rows[conditionIndex];
            conditionRow.Selected = true;
            _conditionsGrid.CurrentCell = conditionRow.Cells[COL_VALUE];
            _conditionsGrid.FirstDisplayedScrollingRowIndex = conditionIndex;
        }

        private void EditConditionForResult(SearchResult result)
        {
            int conditionIndex = result?.Condition?.ConditionIndex ?? -1;
            if (conditionIndex < 0 || conditionIndex >= _conditions.Count)
                return;
            GoToConditionForResult(result);
            ConditionsGrid_CellDoubleClick(_conditionsGrid,
                new DataGridViewCellEventArgs(COL_VALUE, conditionIndex));
        }

        private ContextMenuStrip BuildResultsMenu()
        {
            var menu = new ContextMenuStrip { Font = UiTheme.BodyFont, ShowImageMargin = false };
            var locate = new ToolStripMenuItem("在模型中定位", null, (s, e) => LocateResultInModel(GetCurrentResult()));
            var edit = new ToolStripMenuItem("修改该条件…", null, (s, e) => EditConditionForResult(GetCurrentResult()));
            var go = new ToolStripMenuItem("转到条件列表", null, (s, e) => GoToConditionForResult(GetCurrentResult()));
            var copy = new ToolStripMenuItem("复制查询值", null, (s, e) =>
            {
                string value = GetCurrentResult()?.QueryValue;
                if (!string.IsNullOrEmpty(value))
                {
                    Clipboard.SetText(value);
                    ShowToast("已复制查询值：" + value);
                }
            });
            menu.Items.AddRange(new ToolStripItem[] { locate, edit, go, new ToolStripSeparator(), copy });
            menu.Opening += (s, e) =>
            {
                SearchResult result = GetCurrentResult();
                if (result == null)
                {
                    e.Cancel = true;
                    return;
                }
                locate.Enabled = result.MatchedItems != null && result.MatchedItems.Count > 0;
                copy.Enabled = !string.IsNullOrEmpty(result.QueryValue);
            };
            ThemedMenuRenderer.Apply(menu);
            return menu;
        }

        private SearchResult GetCurrentResult()
        {
            DataGridViewRow row = _resultsGrid.CurrentRow;
            return row?.Tag as SearchResult;
        }

        private void ResultFilterButton_Click(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is SearchResultFilter filter)
                SetActiveResultFilter(filter);
        }

        /// <summary>
        /// 把当前选择还原为本次搜索选中的全部对象（命中 ∪ STR 保护），并对齐相机。
        /// 供用户查看完个别重复对象后一键恢复。
        /// </summary>
        private void BtnRestoreSelection_Click(object sender, EventArgs e)
        {
            if (!EnsureDocumentUsable())
                return;

            if (_lastSearchSelection == null || _lastSearchSelection.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "没有可还原的搜索选择，请先执行搜索。",
                    "傑出品·还原选择",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                List<ModelItem> selected = SelectionService.SetSelection(
                    _doc,
                    _lastSearchSelection);
                ViewFocusService.ZoomToCurrentSelection(_doc);
                _lblResultSummary.Text =
                    $"已还原搜索选中的全部对象：{selected.Count} 个。";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "还原选择失败：\n" + ex.Message,
                    "傑出品·还原选择",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SetActiveResultFilter(SearchResultFilter filter)
        {
            _activeResultFilter = filter;
            foreach (Control control in _resultFilterPanel.Controls)
            {
                var button = control as Button;
                if (button == null)
                    continue;
                // 仅重绘筛选按钮，跳过“还原全部选择”等非筛选按钮，保留其自定义样式。
                if (!(button.Tag is SearchResultFilter value))
                    continue;

                if (button is ThemedButton themed)
                    themed.Active = value == filter;
            }
            RefreshResultsGrid(_lastResults ?? Enumerable.Empty<SearchResult>());
        }

        private void RefreshResultsGrid(IEnumerable<SearchResult> results)
        {
            _updatingResultChecks = true;
            try
            {
                _resultsGrid.Rows.Clear();
                foreach (SearchResult result in results)
                {
                    if (!IsResultVisible(result))
                        continue;

                    int displayIndex = result.Condition.DisplayIndex;
                    bool includeInMatch = IsResultIncludedInMatch(result);
                    int rowIndex = _resultsGrid.Rows.Add(
                        _checkedExportResultIndices.Contains(displayIndex),
                        includeInMatch,
                        displayIndex,
                        SearchResultPolicy.GetDisplayName(result.Status),
                        result.Condition.GetCategoryName(),
                        result.Condition.GetPropertyName(),
                        result.Condition.Test,
                        result.Condition.Value,
                        result.MatchCount,
                        result.StatusMessage);
                    DataGridViewRow row = _resultsGrid.Rows[rowIndex];
                    row.Tag = result;
                    ApplyResultRowStyle(row, result.Status);
                    row.Cells[RESULT_COL_EXPORT].ToolTipText =
                        "勾选后可通过底部“导出结果”导出该条件";
                    ConfigureIncludeMatchCell(
                        row.Cells[RESULT_COL_INCLUDE_MATCH],
                        result);
                    for (int cellIndex = 2; cellIndex < row.Cells.Count; cellIndex++)
                    {
                        DataGridViewCell cell = row.Cells[cellIndex];
                        cell.ToolTipText = Convert.ToString(cell.Value);
                    }
                }
            }
            finally
            {
                _updatingResultChecks = false;
            }

            UpdateExportSelectionState();
            UpdateDuplicateInclusionState();
        }

        private void ResultsGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_resultsGrid.IsCurrentCellDirty
                && (_resultsGrid.CurrentCell?.ColumnIndex == RESULT_COL_EXPORT
                    || _resultsGrid.CurrentCell?.ColumnIndex == RESULT_COL_INCLUDE_MATCH))
            {
                _resultsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private bool IsResultIncludedInMatch(SearchResult result)
        {
            if (result?.Condition == null)
                return false;
            if (result.Status == SearchResultStatus.Found)
                return true;
            return result.Status == SearchResultStatus.Duplicate
                && _includedDuplicateResultIndices.Contains(
                    result.Condition.DisplayIndex);
        }

        private void ConfigureIncludeMatchCell(
            DataGridViewCell cell,
            SearchResult result)
        {
            bool isDuplicate = result.Status == SearchResultStatus.Duplicate;
            cell.ReadOnly = !isDuplicate || _lastHideExecuted;

            if (result.Status == SearchResultStatus.Found)
            {
                cell.ToolTipText = "唯一找到项始终计入匹配对象总数";
                return;
            }

            if (isDuplicate)
            {
                cell.ToolTipText = _lastHideExecuted
                    ? "本次隐藏已经执行，重新搜索后才能调整"
                    : "勾选后，该重复条件命中的对象将计入总数、选择集和隐藏保留集合";
                return;
            }

            cell.ToolTipText = "该条件没有可计入的匹配对象";
        }

        private void ResultsGrid_CellValueChanged(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (_updatingResultChecks || e.RowIndex < 0)
            {
                return;
            }

            DataGridViewRow row = _resultsGrid.Rows[e.RowIndex];
            var result = row.Tag as SearchResult;
            if (result?.Condition == null)
                return;

            int displayIndex = result.Condition.DisplayIndex;
            if (e.ColumnIndex == RESULT_COL_EXPORT)
            {
                bool selected = Convert.ToBoolean(
                    row.Cells[RESULT_COL_EXPORT].Value);
                if (selected)
                    _checkedExportResultIndices.Add(displayIndex);
                else
                    _checkedExportResultIndices.Remove(displayIndex);

                UpdateExportSelectionState();
                return;
            }

            if (e.ColumnIndex == RESULT_COL_INCLUDE_MATCH
                && result.Status == SearchResultStatus.Duplicate
                && !_lastHideExecuted)
            {
                bool included = Convert.ToBoolean(
                    row.Cells[RESULT_COL_INCLUDE_MATCH].Value);
                SetDuplicateInclusion(new[] { displayIndex }, included);
            }
        }

        private static List<int> GetDuplicateResultIds(IEnumerable<SearchResult> results)
        {
            return (results ?? Enumerable.Empty<SearchResult>())
                .Where(result => result?.Condition != null
                    && result.Status == SearchResultStatus.Duplicate)
                .Select(result => result.Condition.DisplayIndex)
                .Distinct()
                .ToList();
        }

        private void ToggleDuplicateInclusion(List<int> duplicateIds)
        {
            if (_lastHideExecuted || duplicateIds.Count == 0)
                return;

            _resultsGrid.EndEdit();
            bool allIncluded = duplicateIds.All(_includedDuplicateResultIndices.Contains);
            SetDuplicateInclusion(duplicateIds, !allIncluded);
        }

        // 单条与批量共用入口。批量更新只计算一次有效集合，保留滚动位置和导出勾选。
        private void SetDuplicateInclusion(IEnumerable<int> duplicateIds, bool included)
        {
            if (_lastHideExecuted)
                return;

            _includedDuplicateResultIndices = DuplicateMatchInclusionPolicy.SetInclusion(
                _includedDuplicateResultIndices, duplicateIds, included);
            _updatingResultChecks = true;
            try
            {
                foreach (DataGridViewRow row in _resultsGrid.Rows)
                {
                    var result = row.Tag as SearchResult;
                    if (result?.Status != SearchResultStatus.Duplicate)
                        continue;
                    bool value = IsResultIncludedInMatch(result);
                    DataGridViewCell cell = row.Cells[RESULT_COL_INCLUDE_MATCH];
                    if (!Equals(cell.Value, value))
                        cell.Value = value;
                }
            }
            finally
            {
                _updatingResultChecks = false;
            }

            RecalculateEffectiveMatchContext(updateRestoreSelection: true);
            UpdateDuplicateInclusionState();
        }

        private void UpdateDuplicateInclusionState()
        {
            List<int> duplicateIds = GetDuplicateResultIds(_lastResults);
            int includedCount = duplicateIds.Count(_includedDuplicateResultIndices.Contains);
            bool allIncluded = duplicateIds.Count > 0 && includedCount == duplicateIds.Count;
            _btnToggleDuplicateInclusion.Text = allIncluded ? "取消全选重复项" : "全选重复项";
            _btnToggleDuplicateInclusion.Enabled = duplicateIds.Count > 0 && !_lastHideExecuted;
            _lblDuplicateInclusion.Text = $"重复项计入：{includedCount} / {duplicateIds.Count}";
            if (_lastHideExecuted)
                _lblDuplicateInclusion.Text += "（隐藏已执行）";

            DataGridViewColumn column = _resultsGrid.Columns[RESULT_COL_INCLUDE_MATCH];
            column.HeaderCell.ToolTipText = _lastHideExecuted
                ? "本次隐藏已经执行，重新搜索后才能调整"
                : "单击表头全选或取消当前筛选中的重复项；唯一找到项固定计入";
            _resultsGrid.InvalidateCell(column.HeaderCell);
        }

        private void RecalculateEffectiveMatchContext(bool updateRestoreSelection)
        {
            NotifyMeasurementSourceChanged();
            List<ModelItem> effectiveItems = ResolveEffectiveMatchedItems(
                _lastResults ?? Enumerable.Empty<SearchResult>());
            _lastMatchedItemsInScope = effectiveItems;
            _lastTotalMatched = effectiveItems.Count;

            if (updateRestoreSelection)
            {
                RememberSearchSelection(MergeUniqueItems(
                    effectiveItems,
                    _lastProtectedItems));
            }

            UpdateResultSummary(
                _lastResults ?? new List<SearchResult>(),
                _lastTotalMatched,
                _currentModelPrefix);
            _btnCreateSelectionSet.Enabled = _lastTotalMatched > 0;
            UpdateHideButtonState();
        }

        private List<ModelItem> ResolveEffectiveMatchedItems(
            IEnumerable<SearchResult> results)
        {
            List<SearchResult> resultList = (results ?? Enumerable.Empty<SearchResult>())
                .Where(result => result != null)
                .ToList();
            IEnumerable<ModelItem> foundItems = resultList
                .Where(result => result.Status == SearchResultStatus.Found)
                .SelectMany(result => result.MatchedItems ?? new List<ModelItem>());
            var duplicateItemsByResult = new Dictionary<int, IReadOnlyList<ModelItem>>();
            foreach (SearchResult result in resultList)
            {
                if (result.Status != SearchResultStatus.Duplicate
                    || result.Condition == null)
                {
                    continue;
                }

                duplicateItemsByResult[result.Condition.DisplayIndex] =
                    result.MatchedItems ?? new List<ModelItem>();
            }

            return DuplicateMatchInclusionPolicy.ResolveEffectiveItems(
                foundItems,
                duplicateItemsByResult,
                _includedDuplicateResultIndices);
        }

        private void ResultsGrid_ColumnHeaderMouseClick(
            object sender,
            DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            if (e.ColumnIndex == RESULT_COL_INCLUDE_MATCH)
            {
                ToggleDuplicateInclusion(GetDuplicateResultIds(GetCurrentFilteredResults()));
                return;
            }
            if (e.ColumnIndex != RESULT_COL_EXPORT)
                return;

            List<int> visibleIds = GetCurrentFilteredResults()
                .Select(result => result.Condition.DisplayIndex)
                .ToList();
            bool allVisibleSelected = visibleIds.Count > 0
                && visibleIds.All(id => _checkedExportResultIndices.Contains(id));
            _checkedExportResultIndices = ResultExportPolicy.SetVisibleSelection(
                _checkedExportResultIndices,
                visibleIds,
                !allVisibleSelected);
            RefreshResultsGrid(_lastResults ?? Enumerable.Empty<SearchResult>());
        }

        private void ResultsGrid_CellPainting(
            object sender,
            DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == RESULT_COL_STATUS)
            {
                PaintStatusBadge(e);
                return;
            }
            if (e.RowIndex != -1
                || (e.ColumnIndex != RESULT_COL_EXPORT && e.ColumnIndex != RESULT_COL_INCLUDE_MATCH))
                return;

            e.Paint(
                e.CellBounds,
                DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

            bool isExport = e.ColumnIndex == RESULT_COL_EXPORT;
            CheckBoxState state = isExport
                ? GetExportHeaderCheckBoxState()
                : GetHeaderCheckBoxState(
                    GetDuplicateResultIds(GetCurrentFilteredResults()),
                    _includedDuplicateResultIndices,
                    !_lastHideExecuted);
            Size glyphSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
            var glyphBounds = new Rectangle(
                e.CellBounds.Left + ScaleLogical(6),
                e.CellBounds.Top + ((e.CellBounds.Height - glyphSize.Height) / 2),
                glyphSize.Width,
                glyphSize.Height);

            CheckBoxRenderer.DrawCheckBox(e.Graphics, glyphBounds.Location, state);
            var textBounds = new Rectangle(
                glyphBounds.Right + ScaleLogical(4), e.CellBounds.Top,
                Math.Max(0, e.CellBounds.Right - glyphBounds.Right - ScaleLogical(6)),
                e.CellBounds.Height);
            TextRenderer.DrawText(e.Graphics, _resultsGrid.Columns[e.ColumnIndex].HeaderText,
                e.CellStyle.Font, textBounds, e.CellStyle.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            e.Handled = true;
        }

        /// <summary>“状态”列绘制为圆角标签。</summary>
        private void PaintStatusBadge(DataGridViewCellPaintingEventArgs e)
        {
            var result = _resultsGrid.Rows[e.RowIndex].Tag as SearchResult;
            if (result == null)
                return;

            e.Paint(e.CellBounds,
                DataGridViewPaintParts.Background
                | DataGridViewPaintParts.SelectionBackground
                | DataGridViewPaintParts.Border);
            UiTheme.GetStatusColors(result.Status, out Color back, out Color fore);
            string text = Convert.ToString(e.FormattedValue);
            Size textSize = TextRenderer.MeasureText(text, UiTheme.BodyStrongFont);
            int badgeHeight = textSize.Height + ScaleLogical(4);
            var badge = new Rectangle(
                e.CellBounds.Left + ScaleLogical(8),
                e.CellBounds.Top + (e.CellBounds.Height - badgeHeight) / 2,
                Math.Min(textSize.Width + ScaleLogical(12), e.CellBounds.Width - ScaleLogical(12)),
                badgeHeight);
            UiTheme.FillRoundedRectangle(e.Graphics, back, badge, badgeHeight / 2);
            TextRenderer.DrawText(e.Graphics, text, UiTheme.BodyStrongFont, badge, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.Handled = true;
        }

        private CheckBoxState GetExportHeaderCheckBoxState()
        {
            List<int> visibleIds = GetCurrentFilteredResults()
                .Select(result => result.Condition.DisplayIndex)
                .ToList();
            return GetHeaderCheckBoxState(visibleIds, _checkedExportResultIndices, true);
        }

        private static CheckBoxState GetHeaderCheckBoxState(
            List<int> eligibleIds, ISet<int> selectedIds, bool enabled)
        {
            if (eligibleIds.Count == 0)
                return CheckBoxState.UncheckedDisabled;

            int selectedCount = eligibleIds.Count(selectedIds.Contains);
            if (selectedCount == 0)
                return enabled ? CheckBoxState.UncheckedNormal : CheckBoxState.UncheckedDisabled;
            if (selectedCount == eligibleIds.Count)
                return enabled ? CheckBoxState.CheckedNormal : CheckBoxState.CheckedDisabled;
            return enabled ? CheckBoxState.MixedNormal : CheckBoxState.MixedDisabled;
        }

        private List<SearchResult> GetCurrentFilteredResults()
        {
            return (_lastResults ?? new List<SearchResult>())
                .Where(result => result?.Condition != null)
                .Where(IsResultVisible)
                .ToList();
        }

        /// <summary>状态筛选与搜索框共同决定一行是否显示；“当前筛选”的导出与表头勾选也以此为准。</summary>
        private bool IsResultVisible(SearchResult result)
        {
            if (result?.Condition == null
                || !SearchResultPolicy.MatchesFilter(result.Status, _activeResultFilter))
                return false;
            if (string.IsNullOrEmpty(_resultSearchText))
                return true;

            string query = _resultSearchText;
            // 以 # 开头按序号精确查找（如 #12）；否则在查询值、属性、分类、说明中做包含匹配，
            // 这样输入 15105 这类编号片段能直接找到查询值 ESPT-M12-15105。
            if (query.StartsWith("#", StringComparison.Ordinal))
                return int.TryParse(query.Substring(1), out int index)
                    && result.Condition.DisplayIndex == index;
            return Contains(result.Condition.Value, query)
                || Contains(result.Condition.GetPropertyName(), query)
                || Contains(result.Condition.GetCategoryName(), query)
                || Contains(result.StatusMessage, query);
        }

        private static bool Contains(string text, string query)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SelectFirstResultRow()
        {
            if (_resultsGrid.Rows.Count == 0)
                return;
            _resultsGrid.ClearSelection();
            DataGridViewRow first = _resultsGrid.Rows[0];
            first.Selected = true;
            _resultsGrid.CurrentCell = first.Cells[RESULT_COL_STATUS];
            _resultsGrid.FirstDisplayedScrollingRowIndex = 0;
        }

        /// <summary>搜索框 Enter：定位当前选中（默认第一条）结果到模型；无匹配对象时提示。</summary>
        private void LocateFirstSearchMatch()
        {
            SearchResult result = GetCurrentResult();
            if (result == null)
            {
                ShowToast(string.IsNullOrEmpty(_resultSearchText)
                    ? "先输入要查找的编号或查询值。"
                    : "没有匹配的结果。");
                return;
            }
            if (result.MatchedItems == null || result.MatchedItems.Count == 0)
            {
                ShowToast($"条件 #{result.Condition.DisplayIndex} 在模型中没有匹配对象，" +
                    "双击该行可直接修改条件。");
                return;
            }
            LocateResultInModel(result);
        }

        private void FocusResultSearch()
        {
            if (_lastResults == null)
                return;
            SwitchTab(_tabResults);
            _resultSearchBox.FocusAndSelectAll();
        }

        private void UpdateExportSelectionState()
        {
            List<int> allIds = (_lastResults ?? new List<SearchResult>())
                .Where(result => result?.Condition != null)
                .Select(result => result.Condition.DisplayIndex)
                .ToList();
            List<int> visibleIds = GetCurrentFilteredResults()
                .Select(result => result.Condition.DisplayIndex)
                .ToList();
            _checkedExportResultIndices = ResultExportPolicy.ResolveExportIds(
                ResultExportScope.Checked,
                allIds,
                visibleIds,
                _checkedExportResultIndices);

            if (_lblExportSelection != null)
                _lblExportSelection.Text = $"导出已勾选 {_checkedExportResultIndices.Count} 条";
            if (_btnExportResults != null)
                _btnExportResults.Enabled = allIds.Count > 0;

            if (_resultsGrid != null && _resultsGrid.Columns.Count > RESULT_COL_EXPORT)
            {
                _resultsGrid.InvalidateCell(
                    _resultsGrid.Columns[RESULT_COL_EXPORT].HeaderCell);
            }
        }

        private static void ApplyResultRowStyle(
            DataGridViewRow row,
            SearchResultStatus status)
        {
            // 行保持中性底色，仅“状态”列以彩色标签呈现，避免整表花色干扰阅读。
            UiTheme.GetStatusColors(status, out Color back, out Color fore);
            DataGridViewCellStyle statusStyle = row.Cells[RESULT_COL_STATUS].Style;
            statusStyle.ForeColor = fore;
            statusStyle.SelectionForeColor = fore;
            statusStyle.Font = UiTheme.BodyStrongFont;
        }

        private void UpdateResultFilterCaptions(IReadOnlyCollection<SearchResult> results)
        {
            foreach (Control control in _resultFilterPanel.Controls)
            {
                var button = control as Button;
                if (!(button?.Tag is SearchResultFilter filter))
                    continue;

                int count = results.Count(r =>
                    SearchResultPolicy.MatchesFilter(r.Status, filter));
                button.Text = $"{GetFilterTitle(filter)} {count}";
            }
        }

        private static string GetFilterTitle(SearchResultFilter filter)
        {
            switch (filter)
            {
                case SearchResultFilter.All: return "全部";
                case SearchResultFilter.Problems: return "问题项";
                case SearchResultFilter.Found: return "已找到";
                case SearchResultFilter.NotFound: return "未找到";
                case SearchResultFilter.Duplicate: return "重复";
                case SearchResultFilter.ConditionInvalid: return "条件异常";
                default: throw new ArgumentOutOfRangeException(nameof(filter));
            }
        }

        #endregion

        #region 数据绑定

        /// <summary>重建条件表；可指定刷新后选中的行，保持用户的编辑位置。</summary>
        private void RefreshConditionsGrid(int selectIndex = -1, int selectCount = 1)
        {
            _conditionsGrid.Rows.Clear();
            foreach (var c in _conditions)
            {
                _conditionsGrid.Rows.Add(
                    _conditionsGrid.Rows.Count + 1,
                    c.CategoryDisplay ?? c.CategoryInternal ?? "",
                    c.PropertyDisplay ?? c.PropertyInternal ?? "",
                    c.Test,
                    c.Value);
            }
            _conditionsGrid.ClearSelection();
            if (selectIndex >= 0 && selectIndex < _conditionsGrid.Rows.Count)
            {
                _conditionsGrid.CurrentCell = _conditionsGrid.Rows[selectIndex].Cells[COL_VALUE];
                int last = Math.Min(_conditionsGrid.Rows.Count, selectIndex + Math.Max(1, selectCount));
                for (int i = selectIndex; i < last; i++)
                    _conditionsGrid.Rows[i].Selected = true;
            }
            UpdateConditionActionState();
            UpdateNavCaptions();
        }

        private void UpdateConditionActionState()
        {
            if (_btnDeleteCondition == null)
                return;
            _btnDeleteCondition.Enabled = _conditionsGrid.SelectedRows.Count > 0;
            _btnClearConditions.Enabled = _conditions.Count > 0;
            _btnExportXml.Enabled = _conditions.Count > 0;
        }

        /// <summary>
        /// 从 XML 文件加载搜索条件。
        /// </summary>
        private void LoadFromXml(string xmlPath)
        {
            List<SearchCondition> importedConditions = XmlSearchParser.Parse(xmlPath);

            _currentXmlPath = xmlPath;
            _conditions = importedConditions;
            _conditionsDirty = false;
            RefreshConditionsGrid();
            InvalidateSearchResults("已导入新的搜索条件，请执行搜索。");
            ShowToast($"已导入 {importedConditions.Count} 条条件：{Path.GetFileName(xmlPath)}");
        }

        private void InvalidateSearchResults(string message)
        {
            NotifyMeasurementSourceChanged();
            _lastResults = null;
            _lastTotalMatched = 0;
            _lastHideExecuted = false;
            _currentModelPrefix = null;
            _lastScopeRoots.Clear();
            _lastMatchedItemsInScope.Clear();
            _lastProtectedItems.Clear();
            _checkedExportResultIndices.Clear();
            _includedDuplicateResultIndices.Clear();
            _resultSearchText = string.Empty;
            _resultSearchBox?.Clear();
            _lblResultSummary.Text = message;
            _resultsEmptyMessage = message;
            UpdateResultChromeVisibility();
            UpdateResultFilterCaptions(Array.Empty<SearchResult>());
            SetActiveResultFilter(SearchResultFilter.All);
            _btnExportResults.Enabled = false;
            _btnCreateSelectionSet.Enabled = false;
            ClearSearchSelection();
            UpdateHideButtonState();
            UpdateNavCaptions();
        }

        /// <summary>缓存本次搜索选中的全部对象，并启用“还原全部选择”按钮。</summary>
        private void RememberSearchSelection(List<ModelItem> keepItems)
        {
            _lastSearchSelection = keepItems == null
                ? new List<ModelItem>()
                : new List<ModelItem>(keepItems);
            if (_btnRestoreSelection != null)
                _btnRestoreSelection.Enabled = _lastSearchSelection.Count > 0;
        }

        /// <summary>清空缓存的搜索选择并禁用“还原全部选择”按钮。</summary>
        private void ClearSearchSelection()
        {
            _lastSearchSelection = null;
            if (_btnRestoreSelection != null)
                _btnRestoreSelection.Enabled = false;
        }

        #endregion

        #region 条件编辑对话框

        private static DialogResult ShowConditionEditor(
            ref string category,
            ref string property,
            ref string test,
            ref string value)
        {
            using (var form = new ThemedDialog("编辑条件", new Size(ScaleLogical(640), ScaleLogical(470))))
            {
                form.SetHeading(
                    "搜索条件",
                    "先在 Navisworks 中选中目标对象，对照「属性」窗口填写：分类为分组名，属性名为左侧名称，查询值为右侧值。");

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(0),
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

                void AddRow(Control control)
                {
                    layout.RowCount += 1;
                    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    layout.Controls.Add(control, 0, layout.RowCount - 1);
                }

                // 字段：标签在上、输入框在下、灰色辅助说明在输入框之下。
                TextBox AddTextField(string label, string hint, string placeholder, string text)
                {
                    AddRow(new Label
                    {
                        Text = label,
                        Font = UiTheme.BodyStrongFont,
                        ForeColor = UiTheme.Text,
                        AutoSize = true,
                        Margin = new Padding(0, layout.RowCount == 0 ? 0 : ScaleLogical(12), 0, ScaleLogical(4)),
                    });
                    var box = new TextBox
                    {
                        Text = text,
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        Margin = new Padding(0),
                    };
                    UiTheme.SetPlaceholder(box, placeholder);
                    AddRow(box);
                    AddRow(MakeFieldHint(hint));
                    return box;
                }

                TextBox txtCat = AddTextField("分类（可选）",
                    "属性所在的分组/选项卡名称；不确定可留空，插件会自动识别。",
                    "例如 Item、Element、SmartPlant 3D", category);
                TextBox txtProp = AddTextField("属性名（必填）",
                    "必须与属性面板左侧名称完全一致，含空格与大小写。",
                    "例如 名称、System Path、Tag", property);

                AddRow(new Label
                {
                    Text = "匹配方式",
                    Font = UiTheme.BodyStrongFont,
                    ForeColor = UiTheme.Text,
                    AutoSize = true,
                    Margin = new Padding(0, ScaleLogical(12), 0, ScaleLogical(4)),
                });
                var rdoEquals = new RadioButton
                {
                    Text = "equals · 完全相同（编号、名称）",
                    AutoSize = true,
                    Margin = new Padding(0, 0, ScaleLogical(24), 0),
                };
                var rdoContains = new RadioButton
                {
                    Text = "contains · 包含这段文字（路径、描述）",
                    AutoSize = true,
                    Margin = new Padding(0),
                };
                bool isContains = string.Equals(test, "contains", StringComparison.OrdinalIgnoreCase);
                rdoEquals.Checked = !isContains;
                rdoContains.Checked = isContains;
                var testRow = new FlowLayoutPanel
                {
                    AutoSize = true,
                    WrapContents = false,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                };
                testRow.Controls.Add(rdoEquals);
                testRow.Controls.Add(rdoContains);
                AddRow(testRow);

                TextBox txtVal = AddTextField("查询值",
                    "属性面板右侧要找的值；前后不要留多余空格。",
                    "例如 M14-101、P-001", value);

                form.Content.Controls.Add(layout);

                ThemedButton btnOk = form.AddButton("确定", ButtonKind.Primary, DialogResult.OK);
                ThemedButton btnCancel = form.AddButton("取消", ButtonKind.Secondary, DialogResult.Cancel);
                btnOk.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(txtProp.Text))
                    {
                        MessageBox.Show(form,
                            "请填写属性名，例如：名称、System Path。",
                            "属性名不能为空",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        form.DialogResult = DialogResult.None;
                        txtProp.Focus();
                    }
                };
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;
                form.Shown += (s, e) =>
                {
                    TextBox first = string.IsNullOrEmpty(txtProp.Text) ? txtProp : txtVal;
                    first.Focus();
                    first.SelectAll();
                };

                var result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    category = txtCat.Text;
                    property = txtProp.Text;
                    test = rdoContains.Checked ? "contains" : "equals";
                    value = txtVal.Text;
                }
                return result;
            }
        }

        private static Label MakeFieldHint(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                Margin = new Padding(0, ScaleLogical(4), 0, 0),
            };
        }

        private void ConditionsGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _conditions.Count) return;
            var c = _conditions[e.RowIndex];
            string cat = c.CategoryDisplay ?? c.CategoryInternal ?? "";
            string prop = c.PropertyDisplay ?? c.PropertyInternal ?? "";
            string test = c.Test;
            string val = c.Value;

            if (ShowConditionEditor(ref cat, ref prop, ref test, ref val) == DialogResult.OK)
            {
                _conditions[e.RowIndex] = new SearchCondition
                {
                    CategoryDisplay = cat,
                    PropertyInternal = prop,
                    PropertyDisplay = prop,
                    Test = test,
                    Value = val,
                };
                _conditionsDirty = true;
                RefreshConditionsGrid(e.RowIndex);
                InvalidateSearchResults("条件已修改，请重新执行搜索。");
            }
        }

        #endregion

        #region 按钮事件

        private void BtnImportXml_Click(object sender, EventArgs e)
        {
            if (!ConfirmDiscardUnsavedConditions("导入会替换当前全部条件。"))
                return;
            using (var dialog = new OpenFileDialog
            {
                Title = "选择傑出品 XML 查找文件",
                Filter = "XML 文件 (*.xml)|*.xml|所有文件 (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        LoadFromXml(dialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "导入 XML 失败：\n" + ex.Message,
                            "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnExportXml_Click(object sender, EventArgs e)
        {
            PromptExportConditions();
        }

        /// <summary>弹出保存框导出条件；默认沿用已导入文件的目录与文件名。</summary>
        private bool PromptExportConditions()
        {
            if (_conditions.Count == 0)
            {
                ShowToast("没有可导出的条件。");
                return false;
            }
            using (var dialog = new SaveFileDialog
            {
                Title = "导出 XML 文件",
                Filter = "XML 文件 (*.xml)|*.xml",
                FileName = string.IsNullOrEmpty(_currentXmlPath)
                    ? "搜索条件.xml"
                    : Path.GetFileName(_currentXmlPath),
                InitialDirectory = string.IsNullOrEmpty(_currentXmlPath)
                    ? string.Empty
                    : Path.GetDirectoryName(_currentXmlPath),
            })
            {
                return dialog.ShowDialog(this) == DialogResult.OK
                    && ExportConditionsToXml(dialog.FileName);
            }
        }

        private void BtnUsageGuide_Click(object sender, EventArgs e)
        {
            ShowUsageGuideDialog();
        }

        /// <summary>
        /// 快捷键表：两列，每项为“按键小方块 + 说明”，一眼可扫到要找的键。
        /// </summary>
        private static Control BuildShortcutPanel()
        {
            var grid = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0, ScaleLogical(20), 0, 0),
                Padding = new Padding(0),
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(170)));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = "快捷键",
                Font = UiTheme.BodyStrongFont,
                ForeColor = UiTheme.TextMuted,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleLogical(6)),
            };
            grid.Controls.Add(title, 0, 0);
            grid.SetColumnSpan(title, 4);

            var shortcuts = new[]
            {
                new[] { "Ctrl", "Enter", "执行搜索" },
                new[] { "Enter", null, "编辑条件 / 定位结果" },
                new[] { "Ctrl", "D", "复制选中条件" },
                new[] { "Delete", null, "删除选中条件" },
                new[] { "Alt", "↑ ↓", "调整条件顺序" },
                new[] { "Esc", null, "清空结果搜索" },
            };
            for (int i = 0; i < shortcuts.Length; i++)
            {
                string[] item = shortcuts[i];
                int row = 1 + i / 2;
                int column = (i % 2) * 2;

                var keys = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false,
                    BackColor = UiTheme.Surface,
                    Margin = new Padding(0, ScaleLogical(4), ScaleLogical(8), ScaleLogical(4)),
                    Padding = new Padding(0),
                };
                keys.Controls.Add(new KeyCap(item[0]) { Margin = new Padding(0) });
                if (item[1] != null)
                {
                    keys.Controls.Add(new Label
                    {
                        Text = "+",
                        AutoSize = true,
                        ForeColor = UiTheme.TextDisabled,
                        Margin = new Padding(ScaleLogical(1), ScaleLogical(2), ScaleLogical(1), 0),
                    });
                    keys.Controls.Add(new KeyCap(item[1]) { Margin = new Padding(0) });
                }
                grid.Controls.Add(keys, column, row);
                grid.Controls.Add(new Label
                {
                    Text = item[2],
                    AutoSize = true,
                    ForeColor = UiTheme.TextBody,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(0, 0, ScaleLogical(16), 0),
                }, column + 1, row);
            }

            var tip = new Label
            {
                Text = "切到结果页可直接输入编号搜索；在表格上单击右键可查看更多操作。",
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                Margin = new Padding(0, ScaleLogical(8), 0, 0),
            };
            int tipRow = 1 + (shortcuts.Length + 1) / 2;
            grid.Controls.Add(tip, 0, tipRow);
            grid.SetColumnSpan(tip, 4);
            return grid;
        }

        private void ShowUsageGuideDialog()
        {
            using (var form = new ThemedDialog(
                "使用说明 — Curi",
                new Size(ScaleLogical(600), ScaleLogical(560))))
            {
                form.SetHeading("四步完成查找", null);
                form.Content.Padding = new Padding(ScaleLogical(24), ScaleLogical(16), ScaleLogical(24), ScaleLogical(20));

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = 2,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(0),
                    Margin = new Padding(0),
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                int contentWidth = form.ClientSize.Width - ScaleLogical(48);
                int badgeSize = ScaleLogical(24);
                int badgeGap = ScaleLogical(12);
                // 标题行与序号圆点按中线对齐。
                int titleOffset = Math.Max(0, (badgeSize - UiTheme.TextHeight(UiTheme.BodyStrongFont)) / 2);

                // 文案约定：全角括号不出现在行首或冒号后，避免视觉缩进；长说明手动分行。
                void AddStep(int number, string title, string detail)
                {
                    int row = layout.RowCount++;
                    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    int top = row == 0 ? 0 : ScaleLogical(18);
                    layout.Controls.Add(new StepBadge(number)
                    {
                        Margin = new Padding(0, top, badgeGap, 0),
                    }, 0, row);

                    var text = new TableLayoutPanel
                    {
                        AutoSize = true,
                        AutoSizeMode = AutoSizeMode.GrowAndShrink,
                        ColumnCount = 1,
                        Margin = new Padding(0, top + titleOffset, 0, 0),
                        Padding = new Padding(0),
                    };
                    text.Controls.Add(new Label
                    {
                        Text = title,
                        Font = UiTheme.BodyStrongFont,
                        ForeColor = UiTheme.Text,
                        AutoSize = true,
                        Margin = new Padding(0),
                    }, 0, 0);
                    text.Controls.Add(new Label
                    {
                        Text = detail,
                        ForeColor = UiTheme.TextMuted,
                        AutoSize = true,
                        MaximumSize = new Size(contentWidth - badgeSize - badgeGap, 0),
                        Margin = new Padding(0, ScaleLogical(4), 0, 0),
                    }, 0, 1);
                    layout.Controls.Add(text, 1, row);
                }

                AddStep(1, "在选择树中选中要搜索的模型节点",
                    "通常是 TS-M 开头的根节点。插件只在选中范围内查找。");
                AddStep(2, "导入 XML，或手动添加条件",
                    "也可把 XML 文件直接拖进窗口。双击任一行可修改，属性名须与属性窗口中的写法一致。");
                AddStep(3, "点击底部的执行搜索",
                    "默认仅选中匹配对象；旁边的模式按钮可切换为选中并隐藏。完成后自动切到结果页。");
                AddStep(4, "在结果页检查，再决定下一步",
                    "右上角可搜索编号快速找到某条，双击即在模型中定位；没找到的条件双击可直接修改。\n" +
                    "确认无误后，可创建选择集、隐藏未选中，或导出结果。");

                // 遇到问题：问题加粗为重点，解法常规字；行间细分隔线。
                var faq = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = 2,
                    BackColor = UiTheme.SurfaceMuted,
                    Padding = new Padding(ScaleLogical(16), ScaleLogical(12), ScaleLogical(16), ScaleLogical(4)),
                    Margin = new Padding(0, ScaleLogical(28), 0, 0),
                };
                faq.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(140)));
                faq.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                int fixWidth = contentWidth - faq.Padding.Horizontal - ScaleLogical(140);

                var faqTitle = new Label
                {
                    Text = "遇到问题",
                    Font = UiTheme.BodyStrongFont,
                    ForeColor = UiTheme.TextMuted,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, ScaleLogical(6)),
                };
                faq.Controls.Add(faqTitle, 0, 0);
                faq.SetColumnSpan(faqTitle, 2);
                faq.RowCount = 1;

                void AddFaq(string problem, string fix)
                {
                    if (faq.RowCount > 1)
                    {
                        var separator = new Panel
                        {
                            Height = 1,
                            Dock = DockStyle.Fill,
                            BackColor = UiTheme.Border,
                            Margin = new Padding(0),
                        };
                        faq.Controls.Add(separator, 0, faq.RowCount);
                        faq.SetColumnSpan(separator, 2);
                        faq.RowCount++;
                    }

                    int row = faq.RowCount++;
                    var rowPadding = new Padding(0, ScaleLogical(9), 0, ScaleLogical(9));
                    faq.Controls.Add(new Label
                    {
                        Text = problem,
                        Font = UiTheme.BodyStrongFont,
                        ForeColor = UiTheme.Text,
                        AutoSize = true,
                        Margin = rowPadding,
                    }, 0, row);
                    faq.Controls.Add(new Label
                    {
                        Text = fix,
                        ForeColor = UiTheme.TextBody,
                        AutoSize = true,
                        MaximumSize = new Size(fixWidth, 0),
                        Margin = rowPadding,
                    }, 1, row);
                }

                AddFaq("什么都没找到", "核对属性名的空格与大小写，以及搜索范围是否选对。");
                AddFaq("结果显示重复", "一个条件命中了多个对象。收窄查询值，或勾选计入匹配。");
                AddFaq("equals 还是 contains", "编号、名称用 equals；路径、描述里的关键词用 contains。");
                AddFaq("隐藏错了", "在 Navisworks 常用选项卡点击全部显示，即可恢复。");

                int faqRow = layout.RowCount++;
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(faq, 0, faqRow);
                layout.SetColumnSpan(faq, 2);

                int keysRow = layout.RowCount++;
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                Control keys = BuildShortcutPanel();
                layout.Controls.Add(keys, 0, keysRow);
                layout.SetColumnSpan(keys, 2);

                form.Content.Controls.Add(layout);

                // 高度随内容收缩，避免底部大片留白。
                form.Load += (s, e) =>
                {
                    int needed = layout.GetPreferredSize(new Size(form.Content.DisplayRectangle.Width, 0)).Height
                        + form.Content.Padding.Vertical;
                    form.Height += needed - form.Content.Height;
                };

                ThemedButton btnClose = form.AddButton("开始使用", ButtonKind.Primary, DialogResult.OK);
                form.AcceptButton = btnClose;
                form.CancelButton = btnClose;
                form.Shown += (s, e) => btnClose.Focus();
                form.ShowDialog(this);
            }
        }

        private void BtnAddCondition_Click(object sender, EventArgs e)
        {
            string cat = "", prop = "", test = "equals", val = "";
            if (ShowConditionEditor(ref cat, ref prop, ref test, ref val) == DialogResult.OK && !string.IsNullOrEmpty(prop))
            {
                _conditions.Add(new SearchCondition
                {
                    CategoryDisplay = cat,
                    PropertyInternal = prop,
                    PropertyDisplay = prop,
                    Test = test,
                    Value = val,
                });
                _conditionsDirty = true;
                RefreshConditionsGrid(_conditions.Count - 1);
                InvalidateSearchResults("已添加搜索条件，请重新执行搜索。");
            }
        }

        private void BtnDeleteCondition_Click(object sender, EventArgs e)
        {
            List<int> indices = GetSelectedConditionIndices();
            if (indices.Count == 0)
            {
                ShowToast("先在表格中选中要删除的条件。");
                return;
            }
            if (indices.Count > 1 && MessageBox.Show(this,
                    $"确定删除选中的 {indices.Count} 条条件吗？",
                    "Curi · 删除条件",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.OK)
                return;

            foreach (int index in indices.OrderByDescending(i => i))
                _conditions.RemoveAt(index);
            _conditionsDirty = true;
            RefreshConditionsGrid(Math.Min(indices.Min(), _conditions.Count - 1));
            InvalidateSearchResults("已删除搜索条件，请重新执行搜索。");
        }

        private List<int> GetSelectedConditionIndices()
        {
            return _conditionsGrid.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(row => row.Index)
                .Where(index => index >= 0 && index < _conditions.Count)
                .Distinct()
                .OrderBy(index => index)
                .ToList();
        }

        /// <summary>复制选中条件并插入到其后，便于只改查询值批量录入。</summary>
        private void DuplicateSelectedConditions()
        {
            List<int> indices = GetSelectedConditionIndices();
            if (indices.Count == 0)
                return;
            int insertAt = indices.Max() + 1;
            List<SearchCondition> copies = indices.Select(i => CloneCondition(_conditions[i])).ToList();
            _conditions.InsertRange(insertAt, copies);
            _conditionsDirty = true;
            RefreshConditionsGrid(insertAt, copies.Count);
            InvalidateSearchResults("已复制搜索条件，请重新执行搜索。");
        }

        private void MoveSelectedCondition(int offset)
        {
            List<int> indices = GetSelectedConditionIndices();
            if (indices.Count != 1)
                return;
            int from = indices[0];
            int to = from + offset;
            if (to < 0 || to >= _conditions.Count)
                return;
            SearchCondition item = _conditions[from];
            _conditions.RemoveAt(from);
            _conditions.Insert(to, item);
            _conditionsDirty = true;
            RefreshConditionsGrid(to);
            InvalidateSearchResults("条件顺序已调整，请重新执行搜索。");
        }

        private static SearchCondition CloneCondition(SearchCondition source)
        {
            return new SearchCondition
            {
                CategoryInternal = source.CategoryInternal,
                CategoryDisplay = source.CategoryDisplay,
                PropertyInternal = source.PropertyInternal,
                PropertyDisplay = source.PropertyDisplay,
                Test = source.Test,
                Value = source.Value,
            };
        }

        private void EditSelectedCondition()
        {
            List<int> indices = GetSelectedConditionIndices();
            if (indices.Count == 1)
                ConditionsGrid_CellDoubleClick(_conditionsGrid,
                    new DataGridViewCellEventArgs(COL_VALUE, indices[0]));
        }

        /// <summary>有未导出的手动修改时，替换前确认。</summary>
        private bool ConfirmDiscardUnsavedConditions(string action)
        {
            if (!_conditionsDirty || _conditions.Count == 0)
                return true;
            return MessageBox.Show(this,
                    action + $"\n当前 {_conditions.Count} 条条件有未导出的修改，将会丢失。\n\n仍要继续吗？",
                    "Curi · 未导出的修改",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) == DialogResult.OK;
        }

        #endregion

        #region 搜索执行

        private void SetSearchBusy(bool busy)
        {
            _btnTrayMeasurement.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            UseWaitCursor = busy;
            _btnSearch.Enabled = !busy;
            _btnSearch.Text = busy ? "正在搜索..." : "执行搜索";
            _tabControl.Enabled = !busy;
            _btnClose.Enabled = !busy;
            _btnMode.Enabled = !busy;
        }

        private void ActivateResultsTab()
        {
            SwitchTab(_tabResults);
            _tabResults.PerformLayout();
            _tabResults.Invalidate(true);
            _tabControl.Update();
        }

        private void BtnSearch_Click(object sender, EventArgs e)
        {
            if (_conditions.Count == 0)
            {
                MessageBox.Show(this, "请先添加至少一个搜索条件。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SwitchTab(_tabConditions);
                return;
            }

            if (!EnsureDocumentUsable())
                return;

            InvalidateSearchResults("正在执行搜索，请稍候。");

            bool isSelectOnlyMode = !_chkHideAfterSearch.Checked;
            string modeName = isSelectOnlyMode
                ? "模式 A：仅查找并选中，不隐藏"
                : "模式 B：查找并选中后，弹窗确认，再执行隐藏未选中";
            var diagnosticLog = _chkDiagnosticLog.Checked
                ? LogService.CreateDiagnosticSession(_doc, _currentXmlPath, _conditions.Count)
                : null;
            List<SearchResult> results = null;
            int totalMatched = 0;
            bool hideExecuted = false;
            bool resultsFinalized = false;
            List<ModelItem> scopeRoots = null;
            List<ModelItem> matchedItemsInScope = null;
            string modelPrefix = null;

            diagnosticLog?.LogMode(modeName);
            diagnosticLog?.LogHideIntent(!isSelectOnlyMode);

            try
            {
                SetSearchBusy(true);

                scopeRoots = SnapshotCurrentSelection(_doc);
                if (scopeRoots.Count == 0)
                {
                    diagnosticLog?.LogScopeInfo(scopeRoots, 0);
                    diagnosticLog?.LogModelPrefixInfo(
                        Enumerable.Empty<string>(),
                        null,
                        null);
                    MessageBox.Show(this,
                        "还没有选择搜索范围。\n\n请先在 Navisworks 左侧选择树中单击要搜索的模型节点" +
                        "（通常是 TS-M 开头的根节点），再点执行搜索。",
                        "Curi · 搜索范围",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                List<string> modelPrefixes = ExtractModelPrefixes(scopeRoots);
                string protectedName = null;
                if (modelPrefixes.Count == 0)
                {
                    diagnosticLog?.LogScopeInfo(scopeRoots, 0);
                    diagnosticLog?.LogModelPrefixInfo(
                        modelPrefixes,
                        null,
                        null);
                    MessageBox.Show(this,
                        "选中的节点名称里没有识别到模型前缀（如 TS-M12-）。\n\n" +
                        "请在选择树中改选模型的根节点，而不是其中的某个构件。",
                        "Curi · 模型前缀",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (modelPrefixes.Count > 1)
                {
                    diagnosticLog?.LogScopeInfo(scopeRoots, 0);
                    diagnosticLog?.LogModelPrefixInfo(
                        modelPrefixes,
                        null,
                        null);
                    MessageBox.Show(this,
                        "检测到多个模型前缀，请一次只选择同一个模型范围。",
                        "傑出品·模型前缀",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                modelPrefix = modelPrefixes[0];
                _currentModelPrefix = modelPrefix;
                protectedName = modelPrefix + "-STR";

                diagnosticLog?.LogScopeInfo(scopeRoots, scopeRoots.Count);
                diagnosticLog?.LogModelPrefixInfo(
                    modelPrefixes,
                    modelPrefix,
                    protectedName);

                results = ModelItemMatcher.MatchAll(
                    _doc, scopeRoots, _conditions);

                List<ModelItem> rawMatchedItemsInScope =
                    MergeUniqueItems(results.SelectMany(r => r.MatchedItems));
                matchedItemsInScope = ResolveEffectiveMatchedItems(results);
                totalMatched = matchedItemsInScope.Count;
                bool resultGatePassed = SearchResultPolicy.CanHide(
                    results.Select(r => r.Status));

                diagnosticLog?.LogXmlScopeResultStats(
                    results.Sum(r => r.MatchCount),
                    rawMatchedItemsInScope.Count,
                    0);
                diagnosticLog?.LogDecision(
                    $"有效匹配对象数量: {matchedItemsInScope.Count}; " +
                    "重复项默认不计入。");
                diagnosticLog?.LogSearchResults(results, resultGatePassed);

                _lastScopeRoots = new List<ModelItem>(scopeRoots);
                _lastMatchedItemsInScope = new List<ModelItem>(matchedItemsInScope);
                FinalizeSearchResults(results, totalMatched, false, modelPrefix);
                resultsFinalized = true;

                hideExecuted = ExecuteCachedResultAction(
                    isSelectOnlyMode,
                    diagnosticLog);
                if (hideExecuted)
                {
                    _lastHideExecuted = true;
                    RefreshResultsGrid(_lastResults);
                    UpdateHideButtonState();
                }
            }
            catch (Exception ex)
            {
                diagnosticLog?.LogException("执行搜索", ex);

                if (results != null && !resultsFinalized)
                {
                    _lastScopeRoots = scopeRoots == null
                        ? new List<ModelItem>()
                        : new List<ModelItem>(scopeRoots);
                    _lastMatchedItemsInScope = matchedItemsInScope == null
                        ? new List<ModelItem>()
                        : new List<ModelItem>(matchedItemsInScope);
                    FinalizeSearchResults(
                        results,
                        totalMatched,
                        hideExecuted,
                        _currentModelPrefix);
                    resultsFinalized = true;
                }

                string detail = ex.Message;
                if (ex.InnerException != null)
                    detail += "\n\n详情：" + ex.InnerException.Message;

                MessageBox.Show(this, "操作失败：\n" + detail,
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                diagnosticLog?.WriteToFile();
                SetSearchBusy(false);
                if (resultsFinalized)
                    ActivateResultsTab();
                UpdateHideButtonState();
            }
        }

        private void BtnHideUnselected_Click(object sender, EventArgs e)
        {
            if (!EnsureDocumentUsable())
                return;

            UpdateHideButtonState();
            if (!_btnHideUnselected.Enabled)
            {
                MessageBox.Show(
                    this,
                    "当前没有可用的搜索结果，请重新选择模型范围并执行搜索。",
                    "傑出品·隐藏未选中",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var diagnosticLog = _chkDiagnosticLog.Checked
                ? LogService.CreateDiagnosticSession(_doc, _currentXmlPath, _conditions.Count)
                : null;

            bool hideExecuted = false;
            try
            {
                // 立即进入忙碌态并强制重绘，避免"点了没反应"的错觉——
                // 隐藏在 UI 主线程同步执行，若不先绘出忙碌态，界面会直接卡住数秒。
                SetHideBusy(true);
                Refresh();

                diagnosticLog?.LogMode("主界面：使用最近一次有效搜索结果执行隐藏");
                diagnosticLog?.LogHideIntent(true);
                diagnosticLog?.LogSearchResults(
                    _lastResults,
                    SearchResultPolicy.CanHide(_lastResults.Select(r => r.Status)));

                hideExecuted = ExecuteCachedResultAction(false, diagnosticLog);
                if (hideExecuted)
                {
                    _lastHideExecuted = true;
                    RefreshResultsGrid(_lastResults);
                }
            }
            catch (Exception ex)
            {
                diagnosticLog?.LogException("主界面执行隐藏", ex);
                MessageBox.Show(
                    this,
                    "隐藏失败：\n" + ex.Message,
                    "傑出品·隐藏未选中",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                diagnosticLog?.WriteToFile();
                SetHideBusy(false);
                UpdateHideButtonState();
            }

            // 真正完成隐藏后给非阻塞完成提示（此时内部各类确认/校验框都已走完）。
            if (hideExecuted)
            {
                _lblResultSummary.Text =
                    $"已隐藏未选中，保留 {_lastFinalKeepCount} 个对象。";
            }
        }

        /// <summary>隐藏操作的忙碌态：等待光标 + 按钮"正在隐藏..." + 禁用交互。</summary>
        private void SetHideBusy(bool busy)
        {
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            UseWaitCursor = busy;
            _btnSearch.Enabled = !busy;
            _btnHideUnselected.Enabled = !busy;
            _btnHideUnselected.Text = busy ? "正在隐藏..." : "隐藏未选中";
            _tabControl.Enabled = !busy;
            _btnClose.Enabled = !busy;
            _btnMode.Enabled = !busy;
        }

        private bool ExecuteCachedResultAction(
            bool selectOnly,
            DiagnosticLogSession diagnosticLog)
        {
            if (_lastResults == null
                || _lastResults.Count == 0
                || _lastScopeRoots == null
                || _lastScopeRoots.Count == 0
                || _lastMatchedItemsInScope == null)
            {
                diagnosticLog?.LogDecision("Blocked: cached search context is unavailable.");
                diagnosticLog?.LogHideBlocked("搜索结果上下文已失效。");
                MessageBox.Show(
                    this,
                    "搜索结果上下文已失效，请重新选择模型范围并执行搜索。",
                    "傑出品·结果已失效",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                InvalidateSearchResults("搜索结果已失效，请重新执行搜索。");
                return false;
            }

            List<SearchResultStatus> statuses = _lastResults
                .Select(result => result.Status)
                .ToList();
            bool resultGatePassed = SearchResultPolicy.CanHide(statuses);
            bool uniquenessOverrideConfirmed = false;

            if (!selectOnly && !resultGatePassed)
            {
                string reason = BuildUniquenessFailureReason(_lastResults);
                string inspectionSelectionNote = TrySelectProblemMatches(diagnosticLog);
                diagnosticLog?.LogDecision(reason);

                uniquenessOverrideConfirmed = ShowUniquenessOverrideDialog(
                    _lastResults,
                    _lastTotalMatched,
                    inspectionSelectionNote);
                diagnosticLog?.LogHidePrompt(
                    _lastTotalMatched,
                    SnapshotCurrentSelection(_doc).Count,
                    uniquenessOverrideConfirmed
                        ? "用户选择仍然继续隐藏"
                        : "用户选择返回检查");

                if (!uniquenessOverrideConfirmed)
                {
                    diagnosticLog?.LogHideBlocked(reason);
                    return false;
                }

                diagnosticLog?.LogDecision("用户已明确确认忽略唯一性问题并继续隐藏。");
            }

            if (!selectOnly
                && !SearchResultPolicy.CanProceedWithHide(
                    statuses,
                    uniquenessOverrideConfirmed))
            {
                diagnosticLog?.LogHideBlocked("唯一性校验未通过且未取得用户覆盖确认。");
                return false;
            }

            if (_lastTotalMatched <= 0 || _lastMatchedItemsInScope.Count == 0)
            {
                if (!selectOnly)
                {
                    diagnosticLog?.LogHidePrecheck(0, 0);
                    diagnosticLog?.LogHideBlocked();
                }

                MessageBox.Show(
                    this,
                    "在选定范围内没有搜索到任何对象，不能执行隐藏，防止隐藏整个模型。",
                    "傑出品·查找结果",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            string protectedName = _currentModelPrefix + "-STR";
            ProtectedKeepResult protectedKeepResult;
            ModelItem protectedNode = _lastScopeRoots.Find(
                root => string.Equals(
                    root.DisplayName,
                    protectedName,
                    StringComparison.Ordinal));
            if (protectedNode != null)
            {
                protectedKeepResult = ProtectedKeepService.BuildFromNode(
                    protectedName,
                    protectedNode);
            }
            else
            {
                protectedKeepResult = ProtectedKeepService.FindProtectedItems(
                    _doc,
                    protectedName);
            }
            _lastProtectedItems = new List<ModelItem>(
                protectedKeepResult.ProtectedItems);

            diagnosticLog?.LogProtectedNodeStats(
                protectedKeepResult.TargetNodeName,
                protectedKeepResult.Found,
                protectedKeepResult.MatchMode,
                protectedKeepResult.MatchedNodeCount,
                protectedKeepResult.DescendantCount,
                protectedKeepResult.ProtectedItems.Count);

            if (!selectOnly && protectedKeepResult.ProtectedItems.Count == 0)
            {
                DialogResult protectedChoice = MessageBox.Show(
                    this,
                    $"未找到当前模型对应的 STR 节点：{protectedName}。\n\n" +
                    "继续后该结构节点可能会被隐藏，是否继续？",
                    "傑出品·STR 保护警告",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                diagnosticLog?.LogDecision(
                    $"Protected node warning choice: {protectedChoice}");
                if (protectedChoice != DialogResult.Yes)
                    return false;
            }

            List<ModelItem> finalKeepItems = MergeUniqueItems(
                _lastMatchedItemsInScope,
                protectedKeepResult.ProtectedItems);
            _lastFinalKeepCount = finalKeepItems.Count;
            if (finalKeepItems.Count == 0)
            {
                diagnosticLog?.LogFinalKeepStats(
                    _lastMatchedItemsInScope.Count,
                    protectedKeepResult.ProtectedItems.Count,
                    0,
                    0,
                    false,
                    false);
                diagnosticLog?.LogDecision("Blocked: finalKeepItems count is 0.");
                diagnosticLog?.LogHidePrecheck(0, 0);
                diagnosticLog?.LogHideBlocked();
                MessageBox.Show(
                    this,
                    "最终保留集合为空，已取消隐藏。",
                    "傑出品·保护",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            List<ModelItem> actualSelectionItems = SelectionService.SetSelection(
                _doc,
                finalKeepItems,
                diagnosticLog);
            int actualSelectionCount = actualSelectionItems.Count;
            // 缓存搜索选中的全部对象，供“还原全部选择”按钮恢复。
            if (actualSelectionCount > 0)
                RememberSearchSelection(finalKeepItems);
            bool actualSelectionMatchesFinalKeep =
                SelectionEquivalencePolicy.AreEquivalent(
                    finalKeepItems,
                    actualSelectionItems);
            bool willHide = !selectOnly
                && SearchResultPolicy.CanProceedWithHide(
                    statuses,
                    uniquenessOverrideConfirmed)
                && finalKeepItems.Count > 0
                && actualSelectionCount > 0
                && actualSelectionMatchesFinalKeep;

            diagnosticLog?.LogFinalKeepStats(
                _lastMatchedItemsInScope.Count,
                protectedKeepResult.ProtectedItems.Count,
                finalKeepItems.Count,
                actualSelectionCount,
                actualSelectionMatchesFinalKeep,
                willHide);

            if (actualSelectionCount == 0)
            {
                diagnosticLog?.LogDecision("Blocked: CurrentSelection count is 0.");
                diagnosticLog?.LogHidePrecheck(finalKeepItems.Count, 0);
                diagnosticLog?.LogHideBlocked();
                MessageBox.Show(
                    this,
                    "写入最终保留集合后当前选择为空，已取消隐藏。",
                    "傑出品·保护",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            if (!actualSelectionMatchesFinalKeep)
            {
                diagnosticLog?.LogDecision(
                    "Blocked: CurrentSelection is not set-equivalent to " +
                    $"finalKeepItems. requested={finalKeepItems.Count}, " +
                    $"actual={actualSelectionCount}.");
                diagnosticLog?.LogHidePrecheck(finalKeepItems.Count, actualSelectionCount);
                diagnosticLog?.LogHideBlocked();
                MessageBox.Show(
                    this,
                    "Navisworks 当前实际选择与请求的最终保留集合不完全一致，已取消隐藏。\n\n" +
                    $"请求数量：{finalKeepItems.Count}\n" +
                    $"实际选择数量：{actualSelectionCount}\n\n" +
                    "实际选择可能存在额外或缺失对象，请重新执行搜索。",
                    "傑出品·选择校验",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            if (selectOnly)
            {
                // 仅选中模式的正常完成不再弹窗打断，用浮动提示反馈。
                ShowToast(
                    $"已选中 {finalKeepItems.Count} 个对象（匹配 {_lastMatchedItemsInScope.Count}，" +
                    $"STR 保留 {protectedKeepResult.ProtectedItems.Count}），未执行隐藏。");
                return false;
            }

            if (!uniquenessOverrideConfirmed)
            {
                DialogResult choice = MessageBox.Show(
                    this,
                    $"计入匹配对象数量：{_lastMatchedItemsInScope.Count}\n" +
                    $"当前模型 STR 保留对象数量：{protectedKeepResult.ProtectedItems.Count}\n" +
                    $"最终保留对象数量：{finalKeepItems.Count}\n" +
                    $"当前 Navisworks 实际选择数量：{actualSelectionCount}\n\n" +
                    "是否执行隐藏未选中，只保留当前最终选择对象？",
                    "傑出品·隐藏确认",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);
                diagnosticLog?.LogHidePrompt(
                    finalKeepItems.Count,
                    actualSelectionCount,
                    choice.ToString());
                if (choice != DialogResult.Yes)
                    return false;
            }

            return HideService.HideUnselected(
                _doc,
                finalKeepItems,
                finalKeepItems.Count,
                diagnosticLog);
        }

        private string TrySelectProblemMatches(DiagnosticLogSession diagnosticLog)
        {
            if (_lastMatchedItemsInScope.Count == 0)
                return string.Empty;

            try
            {
                List<ModelItem> actualSelection = SelectionService.SetSelection(
                    _doc,
                    _lastMatchedItemsInScope,
                    diagnosticLog);
                RememberSearchSelection(actualSelection);
                int actualSelectionCount = actualSelection.Count;
                diagnosticLog?.LogDecision(
                    $"问题结果定位实际选择数量: {actualSelectionCount}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                diagnosticLog?.LogException("问题结果自动定位选择", ex);
                return "\n自动定位问题对象失败，请在结果页手动检查匹配对象。";
            }
        }

        private static string BuildUniquenessFailureReason(
            IEnumerable<SearchResult> results)
        {
            List<SearchResult> resultList = (results ?? Enumerable.Empty<SearchResult>())
                .Where(result => result != null)
                .ToList();
            int notFoundCount = resultList.Count(
                result => result.Status == SearchResultStatus.NotFound);
            int duplicateCount = resultList.Count(
                result => result.Status == SearchResultStatus.Duplicate);
            int invalidCount = resultList.Count(
                result => result.Status == SearchResultStatus.ConditionInvalid);
            return $"结果未通过唯一性校验：未找到 {notFoundCount} 条，" +
                $"重复 {duplicateCount} 条，条件异常 {invalidCount} 条。";
        }

        private bool ShowUniquenessOverrideDialog(
            IEnumerable<SearchResult> results,
            int totalMatched,
            string inspectionSelectionNote)
        {
            List<SearchResult> resultList = (results ?? Enumerable.Empty<SearchResult>())
                .Where(result => result != null)
                .ToList();
            int notFoundCount = resultList.Count(
                result => result.Status == SearchResultStatus.NotFound);
            int duplicateCount = resultList.Count(
                result => result.Status == SearchResultStatus.Duplicate);
            int invalidCount = resultList.Count(
                result => result.Status == SearchResultStatus.ConditionInvalid);
            bool canContinue = totalMatched > 0;

            using (var form = new ThemedDialog(
                "傑出品·唯一性校验未通过",
                new Size(ScaleLogical(600), ScaleLogical(360))))
            {
                form.SetHeading(
                    "发现问题，已暂停隐藏",
                    "部分条件未能唯一匹配对象，请确认是否仍要继续隐藏。",
                    danger: true);

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(0),
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

                // 三项计数以状态色标签并排展示。
                var counts = new FlowLayoutPanel
                {
                    AutoSize = true,
                    WrapContents = false,
                    Margin = new Padding(0, 0, 0, ScaleLogical(12)),
                    Padding = new Padding(0),
                };
                counts.Controls.Add(MakeCountChip("未找到", notFoundCount, SearchResultStatus.NotFound));
                counts.Controls.Add(MakeCountChip("重复", duplicateCount, SearchResultStatus.Duplicate));
                counts.Controls.Add(MakeCountChip("条件异常", invalidCount, SearchResultStatus.ConditionInvalid));

                var matched = new Label
                {
                    Text = $"当前计入匹配的对象：{totalMatched} 个（按对象去重）",
                    Font = UiTheme.BodyStrongFont,
                    ForeColor = UiTheme.Text,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, ScaleLogical(8)),
                };

                string continueNote = canContinue
                    ? "继续后只保留当前计入匹配的对象；未勾选的重复项、未找到和" +
                      "条件异常不贡献对象。STR 保护与选择一致性检查仍然执行。"
                    : "当前没有任何匹配对象，不能继续隐藏，防止隐藏整个模型。";
                var details = new Label
                {
                    Text = continueNote + (inspectionSelectionNote ?? string.Empty),
                    AutoSize = true,
                    MaximumSize = new Size(form.ClientSize.Width - ScaleLogical(48), 0),
                    ForeColor = canContinue ? UiTheme.TextBody : UiTheme.DangerText,
                    Margin = new Padding(0),
                };

                layout.Controls.Add(counts, 0, 0);
                layout.Controls.Add(matched, 0, 1);
                layout.Controls.Add(details, 0, 2);
                form.Content.Controls.Add(layout);

                // 安全选项为默认主按钮；危险的“继续”放在左侧并使用危险样式。
                ThemedButton btnReturn = form.AddButton("返回检查", ButtonKind.Primary, DialogResult.Cancel);
                ThemedButton btnContinue = form.AddButton("仍然继续隐藏", ButtonKind.Danger, DialogResult.Yes);
                btnContinue.Enabled = canContinue;
                form.AcceptButton = btnReturn;
                form.CancelButton = btnReturn;
                form.Shown += (sender, args) => btnReturn.Select();

                return form.ShowDialog(this) == DialogResult.Yes;
            }
        }

        private static Control MakeCountChip(string label, int count, SearchResultStatus status)
        {
            UiTheme.GetStatusColors(status, out Color back, out Color fore);
            return new Label
            {
                Text = $"{label}  {count}",
                AutoSize = true,
                Font = UiTheme.BodyStrongFont,
                BackColor = count > 0 ? back : UiTheme.HeaderBack,
                ForeColor = count > 0 ? fore : UiTheme.TextMuted,
                Padding = new Padding(ScaleLogical(10), ScaleLogical(4), ScaleLogical(10), ScaleLogical(4)),
                Margin = new Padding(0, 0, ScaleLogical(8), 0),
            };
        }

        private void UpdateHideButtonState()
        {
            if (_btnHideUnselected == null)
                return;

            bool hasScopeSnapshot = _lastScopeRoots != null
                && _lastScopeRoots.Count > 0;
            _btnHideUnselected.Enabled = _btnSearch != null
                && _btnSearch.Enabled
                && SearchResultPolicy.CanOfferManualHide(
                    _lastResults?.Select(result => result.Status),
                    _lastTotalMatched,
                    hasScopeSnapshot,
                    _lastHideExecuted);
        }

        private void FinalizeSearchResults(
            List<SearchResult> results,
            int totalMatched,
            bool hideExecuted,
            string scopeLabel)
        {
            if (results == null)
                return;

            _lastResults = results;
            NotifyMeasurementSourceChanged();
            _lastTotalMatched = totalMatched;
            _lastHideExecuted = hideExecuted;
            ShowResults(results, totalMatched, scopeLabel);
            UpdateResultChromeVisibility();
            UpdateNavCaptions();
            _btnExportResults.Enabled = results.Count > 0;
            _btnCreateSelectionSet.Enabled = totalMatched > 0;
            UpdateHideButtonState();
        }

        /// <summary>
        /// 在"结果"选项卡中显示搜索结果。
        /// </summary>
        private void ShowResults(
            List<SearchResult> results,
            int totalMatched,
            string scopeLabel)
        {
            UpdateResultSummary(results, totalMatched, scopeLabel);
            UpdateResultFilterCaptions(results);
            SetActiveResultFilter(results.Any(r => r.IsProblem)
                ? SearchResultFilter.Problems
                : SearchResultFilter.All);
        }

        private void UpdateResultSummary(
            IReadOnlyCollection<SearchResult> results,
            int totalMatched,
            string scopeLabel)
        {
            results = results ?? Array.Empty<SearchResult>();
            int duplicate = results.Count(r => r.Status == SearchResultStatus.Duplicate);
            int includedDuplicate = results.Count(r =>
                r.Status == SearchResultStatus.Duplicate
                && r.Condition != null
                && _includedDuplicateResultIndices.Contains(
                    r.Condition.DisplayIndex));
            string duplicateContribution;
            if (duplicate == 0)
                duplicateContribution = "无重复项";
            else if (includedDuplicate == 0)
                duplicateContribution = "重复项未计入";
            else
                duplicateContribution = $"已计入 {includedDuplicate} 条重复项，按对象去重";

            // 各状态计数已显示在筛选按钮上，摘要只给范围与最终匹配对象数。
            _lblResultSummary.Text =
                $"{scopeLabel}　·　匹配对象 {totalMatched} 个　·　{duplicateContribution}";
        }

        #endregion

        #region 导出

        private void BtnExportResults_Click(object sender, EventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0)
                return;

            UpdateExportSelectionState();
            // 导出按钮在页脚右侧，面板右对齐，向上弹出。
            TogglePicker(_btnExportResults, BuildExportPicker, alignRight: true, afterClose: index =>
            {
                ResultExportScope[] scopes =
                {
                    ResultExportScope.Checked,
                    ResultExportScope.CurrentFilter,
                    ResultExportScope.All,
                };
                ExportResults(scopes[index]);
            });
        }

        private void ExportResults(ResultExportScope scope)
        {
            if (!EnsureDocumentUsable())
                return;

            List<SearchResult> exportResults = ResolveExportResults(scope);
            if (exportResults.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "当前导出范围内没有结果，请调整勾选或筛选条件。",
                    "傑出品·导出结果",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string scopeLabel = GetExportScopeLabel(scope);

            using (var dialog = new SaveFileDialog
            {
                Title = "导出结果",
                Filter = "CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
                FileName =
                    $"傑出品结果_{scopeLabel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt = "csv",
                AddExtension = true,
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        using (var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
                        {
                            writer.WriteLine(
                                "条件序号,状态,计入总匹配,CategoryDisplay,CategoryInternal,PropertyDisplay,PropertyInternal,匹配方式,查询值,匹配数,说明,匹配对象详情");
                            foreach (SearchResult result in exportResults)
                            {
                                SearchConditionSnapshot condition = result?.Condition;
                                string[] columns =
                                {
                                    condition == null ? string.Empty : condition.DisplayIndex.ToString(),
                                    result == null ? string.Empty : SearchResultPolicy.GetDisplayName(result.Status),
                                    IsResultIncludedInMatch(result) ? "是" : "否",
                                    condition?.CategoryDisplay,
                                    condition?.CategoryInternal,
                                    condition?.PropertyDisplay,
                                    condition?.PropertyInternal,
                                    condition?.Test,
                                    condition?.Value,
                                    result == null ? string.Empty : result.MatchCount.ToString(),
                                    result?.StatusMessage,
                                    FormatMatchedItems(result?.MatchedItems),
                                };
                                writer.WriteLine(string.Join(",", columns.Select(EscapeCsv)));
                            }
                        }
                        MessageBox.Show(
                            this,
                            $"导出成功，共 {exportResults.Count} 条。\n" + dialog.FileName,
                            "完成",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "导出失败：\n" + ex.Message,
                            "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private List<SearchResult> ResolveExportResults(ResultExportScope scope)
        {
            List<SearchResult> allResults = (_lastResults ?? new List<SearchResult>())
                .Where(result => result?.Condition != null)
                .ToList();
            List<int> allIds = allResults
                .Select(result => result.Condition.DisplayIndex)
                .ToList();
            List<int> visibleIds = GetCurrentFilteredResults()
                .Select(result => result.Condition.DisplayIndex)
                .ToList();
            HashSet<int> exportIds = ResultExportPolicy.ResolveExportIds(
                scope,
                allIds,
                visibleIds,
                _checkedExportResultIndices);
            return allResults
                .Where(result => exportIds.Contains(result.Condition.DisplayIndex))
                .ToList();
        }

        private static string GetExportScopeLabel(ResultExportScope scope)
        {
            switch (scope)
            {
                case ResultExportScope.Checked: return "已勾选";
                case ResultExportScope.CurrentFilter: return "当前筛选";
                case ResultExportScope.All: return "全部";
                default: throw new ArgumentOutOfRangeException(nameof(scope));
            }
        }

        private static string EscapeCsv(string value)
        {
            string safe = value ?? string.Empty;
            return "\"" + safe.Replace("\"", "\"\"") + "\"";
        }

        private static string FormatMatchedItems(IEnumerable<ModelItem> items)
        {
            return string.Join(
                "; ",
                (items ?? Enumerable.Empty<ModelItem>()).Select(item =>
                    item == null
                        ? "（空对象）"
                        : $"{item.DisplayName ?? "（无名称）"} [{item.InstanceGuid}]"));
        }

        private void BtnCreateSelectionSet_Click(object sender, EventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0) return;

            if (!EnsureDocumentUsable())
                return;

            try
            {
                var flatItems = new List<ModelItem>(_lastMatchedItemsInScope);

                if (flatItems.Count == 0)
                {
                    MessageBox.Show(this,
                        "没有匹配到的对象，无法创建选择集。",
                        "傑出品·选择集",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                string modelPrefix = _currentModelPrefix ?? "查找";
                string setName = $"{modelPrefix}_查找结果_{DateTime.Now:yyyyMMdd_HHmmss}";

                SelectionService.CreateSelectionSet(
                    _doc, setName, flatItems);

                MessageBox.Show(this,
                    $"选择集已创建：\n\n" +
                    $"名称：{setName}\n" +
                    $"包含对象：{flatItems.Count} 个\n\n" +
                    "请在 Navisworks「集合」面板中右键该选择集 →「选择」→ 批量修改。",
                    "傑出品·选择集完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "创建选择集失败：\n" + ex.Message,
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 导出 XML

        private bool ExportConditionsToXml(string xmlPath)
        {
            try
            {
                using (var writer = new StreamWriter(xmlPath, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("<?xml version='1.0' encoding='utf-8'?>");
                    writer.WriteLine("<exchange>");
                    writer.WriteLine("  <findspec>");

                    writer.WriteLine("    <conditions>");
                    foreach (var c in _conditions)
                        WriteConditionXml(writer, c, "      ");
                    writer.WriteLine("    </conditions>");

                    writer.WriteLine("  </findspec>");
                    writer.WriteLine("</exchange>");
                }
                _currentXmlPath = xmlPath;
                _conditionsDirty = false;
                UpdateNavCaptions();
                ShowToast($"已导出 {_conditions.Count} 条条件：{Path.GetFileName(xmlPath)}");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：\n" + ex.Message,
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private static void WriteConditionXml(StreamWriter writer, SearchCondition c, string indent)
        {
            writer.WriteLine(
                $"{indent}<condition test=\"{EscapeXml(c.Test)}\" flags=\"74\">");
            if (!string.IsNullOrEmpty(c.CategoryDisplay) || !string.IsNullOrEmpty(c.CategoryInternal))
            {
                writer.WriteLine($"{indent}  <category>");
                WriteNameElement(
                    writer,
                    $"{indent}    ",
                    c.CategoryDisplay ?? c.CategoryInternal,
                    c.CategoryInternal);
                writer.WriteLine($"{indent}  </category>");
            }
            writer.WriteLine($"{indent}  <property>");
            WriteNameElement(
                writer,
                $"{indent}    ",
                c.PropertyDisplay ?? c.PropertyInternal,
                c.PropertyInternal);
            writer.WriteLine($"{indent}  </property>");
            writer.WriteLine($"{indent}  <value>");
            writer.WriteLine($"{indent}    <data type=\"wstring\">{EscapeXml(c.Value)}</data>");
            writer.WriteLine($"{indent}  </value>");
            writer.WriteLine($"{indent}</condition>");
        }

        private static void WriteNameElement(
            StreamWriter writer,
            string indent,
            string displayName,
            string internalName)
        {
            string escapedDisplay = EscapeXml(displayName ?? string.Empty);
            string escapedInternal = EscapeXml(internalName);

            if (string.IsNullOrEmpty(escapedInternal))
            {
                writer.WriteLine($"{indent}<name>{escapedDisplay}</name>");
                return;
            }

            writer.WriteLine(
                $"{indent}<name internal=\"{escapedInternal}\">{escapedDisplay}</name>");
        }

        private static string EscapeXml(string value)
        {
            return SecurityElement.Escape(value) ?? string.Empty;
        }

        #endregion

        #region 工具方法

        private static int CountCurrentSelection(Document doc)
        {
            int count = 0;
            foreach (ModelItem _ in doc.CurrentSelection.SelectedItems)
                count++;
            return count;
        }

        private static List<ModelItem> SnapshotCurrentSelection(Document doc)
        {
            var result = new List<ModelItem>();
            foreach (ModelItem item in doc.CurrentSelection.SelectedItems)
                result.Add(item);
            return result;
        }

        private static List<string> ExtractModelPrefixes(IEnumerable<ModelItem> scopeRoots)
        {
            var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModelItem item in scopeRoots ?? Enumerable.Empty<ModelItem>())
            {
                string displayName = item?.DisplayName;
                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                Match match = ModelPrefixRegex.Match(displayName.Trim());
                if (match.Success)
                    prefixes.Add(match.Groups[1].Value.ToUpperInvariant());
            }

            return prefixes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<ModelItem> MergeUniqueItems(params IEnumerable<ModelItem>[] groups)
        {
            var uniqueItems = new HashSet<ModelItem>();
            foreach (IEnumerable<ModelItem> group in groups ?? Array.Empty<IEnumerable<ModelItem>>())
            {
                foreach (ModelItem item in group ?? Enumerable.Empty<ModelItem>())
                {
                    if (item != null)
                        uniqueItems.Add(item);
                }
            }

            return uniqueItems.ToList();
        }

        /// <summary>
        /// 校验捕获的文档是否仍可用且仍是活动文档。无模式窗口开着时用户可能
        /// 关闭或切换文档，需在每个模型操作入口拦截。
        /// </summary>
        private bool EnsureDocumentUsable()
        {
            if (DocumentIsUsable(_doc) && IsActiveDocument(_doc))
                return true;

            MessageBox.Show(
                this,
                "当前文档已关闭或切换，请重新打开插件。",
                "傑出品",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        private static bool IsActiveDocument(Document doc)
        {
            try
            {
                return doc != null && ReferenceEquals(NavApp.ActiveDocument, doc);
            }
            catch
            {
                return false;
            }
        }

        private static bool DocumentIsUsable(Document doc)
        {
            if (doc == null)
                return false;
            try
            {
                return !doc.IsClear;
            }
            catch
            {
                // 文档被释放/替换时属性访问可能抛异常。
                return false;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // 首次打开淡入；外部已设置透明度（如测试）时不干预。
            if (Opacity >= 1)
            {
                _fadeInOnShow = true;
                Opacity = 0;
            }
            // 在首次绘制前定位，避免从默认位置跳到右上角的闪烁。
            PositionForModeless();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            DisposeMeasurementLink();
            _toast?.Dispose();
            _toolTip?.Dispose();
            // 仅在正常状态记录位置，避免最小化时的 -32000 哨兵坐标。
            if (this.WindowState == FormWindowState.Normal)
                _lastLocation = this.Location;
            base.OnFormClosed(e);
        }

        /// <summary>
        /// 手动添加或修改过、尚未导出的条件，关闭前提醒保存。
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!_fadeInOnShow)
                return;
            _fadeInOnShow = false;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            UiAnimator.Run(() =>
            {
                if (IsDisposed)
                    return false;
                float t = UiAnimator.EaseOut((float)clock.Elapsed.TotalMilliseconds / 180f);
                Opacity = t;
                return t < 1f;
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && _conditionsDirty && _conditions.Count > 0)
            {
                DialogResult answer = MessageBox.Show(this,
                    $"有 {_conditions.Count} 条条件尚未导出为 XML，关闭后将丢失。\n\n是否先导出？",
                    "Curi · 关闭",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);
                if (answer == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (answer == DialogResult.Yes && !PromptExportConditions())
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnFormClosing(e);
        }

        /// <summary>
        /// 全局快捷键只保留 Ctrl+Enter 搜索。Ctrl+O/S/N/F、F1 等会先被 Navisworks 自身的
        /// 加速键截获（如 Ctrl+S 保存模型），插件不注册，避免误操作。
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_tabControl != null && _tabControl.Enabled
                && keyData == (Keys.Control | Keys.Enter)
                && _btnSearch.Enabled)
            {
                _btnSearch.PerformClick();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// 无模式窗口定位：优先复用上次位置，否则停靠到 Navisworks 主窗口右上角，
        /// 避免遮挡模型中央。用户随后可自由拖动。
        /// </summary>
        private void PositionForModeless()
        {
            if (_lastLocation.HasValue
                && IsRectVisible(new Rectangle(_lastLocation.Value, this.Size)))
            {
                this.Location = _lastLocation.Value;
                return;
            }

            RECT r;
            if (_ownerHandle != IntPtr.Zero && GetWindowRect(_ownerHandle, out r))
            {
                Rectangle owner = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                Rectangle work = Screen.FromHandle(_ownerHandle).WorkingArea;
                int margin = ScaleLogical(16);
                int x = owner.Right - this.Width - margin;
                int y = owner.Top + margin;
                x = Math.Max(work.Left, Math.Min(x, work.Right - this.Width));
                y = Math.Max(work.Top, Math.Min(y, work.Bottom - this.Height));
                this.Location = new Point(x, y);
                return;
            }

            Rectangle prim = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(
                prim.Left + (prim.Width - this.Width) / 2,
                prim.Top + (prim.Height - this.Height) / 2);
        }

        private static bool IsRectVisible(Rectangle rect)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(rect))
                    return true;
            }
            return false;
        }

        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(
            System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        #endregion
    }
}
