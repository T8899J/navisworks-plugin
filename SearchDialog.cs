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
using Color = System.Drawing.Color;

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
        private CheckBox _chkHideAfterSearch;
        private CheckBox _chkTestMode;
        private CheckBox _chkDiagnosticLog;

        // ── 模块 4：结果（搜索后显示） ──
        private Label _lblResultSummary;
        private FlowLayoutPanel _resultFilterPanel;
        private DataGridView _resultsGrid;
        private SearchResultFilter _activeResultFilter = SearchResultFilter.All;
        private Button _btnSearch;
        private Button _btnExportResults;
        private Button _btnCreateSelectionSet;
        private Button _btnClose;

        // ── 公共操作按钮 ──
        private TabControl _tabControl;
        private TabPage _tabConditions;
        private TabPage _tabOptions;
        private TabPage _tabResults;

        // ── 数据 ──
        private readonly Document _doc;
        private readonly string _initialXmlPath;
        private List<SearchCondition> _conditions;
        private string _currentXmlPath;

        // ── 搜索结果缓存 ──
        private List<SearchResult> _lastResults;
        private int _lastTotalMatched;
        private bool _lastHideExecuted;
        private string _currentModelPrefix;

        // ── 列索引常量 ──
        private const int COL_CATEGORY = 0;
        private const int COL_PROPERTY = 1;
        private const int COL_TEST = 2;
        private const int COL_VALUE = 3;
        private const int BASE_DPI = 96;
        private static readonly Regex ModelPrefixRegex =
            new Regex(@"^(TS-M[0-9A-Z]+)-", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public SearchDialog(Document doc, string initialXmlPath = null)
        {
            _doc = doc;
            _initialXmlPath = initialXmlPath;
            _conditions = new List<SearchCondition>();
            InitializeComponent();
            TryLoadInitialXml();
            RefreshConditionsGrid();
        }

        #region 初始化 UI

        private void InitializeComponent()
        {
            this.Text = "傑出品";
            this.Size = new Size(ScaleLogical(960), ScaleLogical(680));
            this.MinimumSize = new Size(ScaleLogical(760), ScaleLogical(540));
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Microsoft YaHei UI", 9F);
            this.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            this.Icon = null;

            // ── TabControl ──
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(ScaleLogical(12), ScaleLogical(6)),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                SizeMode = TabSizeMode.Normal,
            };

            // ========== 选项卡 1：搜索条件 ==========
            _tabConditions = new TabPage("搜索条件");
            _tabConditions.BackColor = this.BackColor;
            _tabConditions.Padding = new Padding(ScaleLogical(8));
            BuildConditionsTab();

            // ========== 选项卡 2：选项 ==========
            _tabOptions = new TabPage("选项");
            _tabOptions.BackColor = this.BackColor;
            _tabOptions.Padding = new Padding(ScaleLogical(8));
            BuildOptionsTab();

            // ========== 选项卡 3：结果 ==========
            _tabResults = new TabPage("结果");
            _tabResults.BackColor = this.BackColor;
            _tabResults.Padding = new Padding(ScaleLogical(8));
            BuildResultsTab();

            _tabControl.TabPages.Add(_tabConditions);
            _tabControl.TabPages.Add(_tabOptions);
            _tabControl.TabPages.Add(_tabResults);

            // ── 底部按钮面板 ──
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = CalculatePanelHeight(new Font("Microsoft YaHei UI", 9F, FontStyle.Bold), 44),
                Padding = new Padding(ScaleLogical(12)),
                BackColor = System.Drawing.Color.FromArgb(245, 247, 250),
            };

            var btnFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            var btnHeight = CalculateButtonHeight(btnFont);

            _btnSearch = new Button
            {
                Text = "执行搜索",
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(37, 99, 235),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                    BorderSize = 0,
                    MouseOverBackColor = System.Drawing.Color.FromArgb(29, 78, 216),
                },
                Font = btnFont,
                UseVisualStyleBackColor = false,
            };
            _btnSearch.Click += BtnSearch_Click;

            _btnExportResults = new Button
            {
                Text = "导出结果",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                    BorderColor = System.Drawing.Color.FromArgb(203, 213, 225),
                    BorderSize = 1,
                    MouseOverBackColor = System.Drawing.Color.FromArgb(239, 246, 255),
                },
                BackColor = System.Drawing.Color.White,
                ForeColor = System.Drawing.Color.FromArgb(51, 65, 85),
                Font = btnFont,
                UseVisualStyleBackColor = false,
                Enabled = false,
            };
            _btnExportResults.Click += BtnExportResults_Click;

            _btnCreateSelectionSet = new Button
            {
                Text = "创建选择集",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                    BorderColor = System.Drawing.Color.FromArgb(203, 213, 225),
                    BorderSize = 1,
                    MouseOverBackColor = System.Drawing.Color.FromArgb(239, 246, 255),
                },
                BackColor = System.Drawing.Color.White,
                ForeColor = System.Drawing.Color.FromArgb(51, 65, 85),
                Font = btnFont,
                UseVisualStyleBackColor = false,
                Enabled = false,
            };
            _btnCreateSelectionSet.Click += BtnCreateSelectionSet_Click;

            _btnClose = new Button
            {
                Text = "关闭",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                    BorderColor = System.Drawing.Color.FromArgb(203, 213, 225),
                    BorderSize = 1,
                    MouseOverBackColor = System.Drawing.Color.FromArgb(248, 250, 252),
                },
                BackColor = System.Drawing.Color.White,
                ForeColor = System.Drawing.Color.FromArgb(51, 65, 85),
                Font = btnFont,
                UseVisualStyleBackColor = false,
                DialogResult = DialogResult.Cancel,
            };

            var bottomLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Padding = new Padding(0),
            };
            var bottomGap = ScaleLogical(12);
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(120)));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, bottomGap));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(108)));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, bottomGap));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(120)));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(84)));
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            bottomLayout.Controls.Add(_btnSearch, 0, 0);
            bottomLayout.Controls.Add(_btnExportResults, 2, 0);
            bottomLayout.Controls.Add(_btnCreateSelectionSet, 4, 0);
            bottomLayout.Controls.Add(_btnClose, 6, 0);
            bottomPanel.Controls.Add(bottomLayout);

            // ── 主布局 ──
            this.Controls.Add(_tabControl);
            this.Controls.Add(bottomPanel);
            this.CancelButton = _btnClose;
        }

        /// <summary>
        /// 创建工具栏按钮：单元格决定尺寸，支持 DPI 缩放。
        /// </summary>
        private static Button MakeToolButton(string text, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSize = false,
                MinimumSize = new Size(ScaleLogical(80), 0),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                    BorderColor = System.Drawing.Color.FromArgb(203, 213, 225),
                    BorderSize = 1,
                    MouseOverBackColor = System.Drawing.Color.FromArgb(239, 246, 255),
                    MouseDownBackColor = System.Drawing.Color.FromArgb(219, 234, 254),
                },
                BackColor = System.Drawing.Color.White,
                ForeColor = System.Drawing.Color.FromArgb(51, 65, 85),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = false,
                Margin = new Padding(ScaleLogical(4)),
            };
            btn.Height = CalculateButtonHeight(btn.Font);
            btn.MinimumSize = new Size(btn.MinimumSize.Width, btn.Height);
            btn.Click += onClick;
            return btn;
        }

        private static int ScaleLogical(int logicalPixels)
        {
            using (var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
            {
                return (int)Math.Ceiling(logicalPixels * graphics.DpiX / BASE_DPI);
            }
        }

        private static int MeasureTextHeight(Font font)
        {
            return TextRenderer.MeasureText("中文Ag", font).Height;
        }

        private static int CalculateButtonHeight(Font font)
        {
            return MeasureTextHeight(font) + ScaleLogical(14);
        }

        private static int CalculateHeaderHeight(Font font)
        {
            return MeasureTextHeight(font) + ScaleLogical(10);
        }

        private static int CalculatePanelHeight(Font font, int logicalVerticalPadding)
        {
            return MeasureTextHeight(font) + ScaleLogical(logicalVerticalPadding);
        }

        private static int CalculateToolbarHeight(Control sampleButton, Padding padding)
        {
            return sampleButton.Height + sampleButton.Margin.Vertical + padding.Vertical;
        }

        private static int CalculateContentHeight(Font font, int lineCount, int logicalVerticalPadding)
        {
            return (MeasureTextHeight(font) * lineCount) + ScaleLogical(logicalVerticalPadding);
        }

        private static void ApplyGridHeaderLayout(DataGridView grid)
        {
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = CalculateHeaderHeight(
                grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font);
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

            // 工具栏：DPI 自适应等宽按钮
            var toolStrip = new TableLayoutPanel
            {
                Height = CalculatePanelHeight(new Font("Microsoft YaHei UI", 9F, FontStyle.Regular), 20),
                Dock = DockStyle.Top,
                RowCount = 1,
                Padding = new Padding(ScaleLogical(6)),
                BackColor = System.Drawing.Color.FromArgb(245, 247, 250),
            };
            for (int i = 0; i < 6; i++)
                toolStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 6F));

            _btnImportXml = MakeToolButton("导入", BtnImportXml_Click);
            _btnExportXml = MakeToolButton("导出", BtnExportXml_Click);
            _btnAddCondition = MakeToolButton("添加", BtnAddCondition_Click);
            _btnDeleteCondition = MakeToolButton("删除", BtnDeleteCondition_Click);
            _btnClearConditions = MakeToolButton("清空", (s, e) =>
            {
                _conditions.Clear();
                RefreshConditionsGrid();
                InvalidateSearchResults("条件已清空，请添加条件后重新执行搜索。");
            });
            _btnUsageGuide = MakeToolButton("使用说明", BtnUsageGuide_Click);

            toolStrip.Controls.Add(_btnImportXml, 0, 0);
            toolStrip.Controls.Add(_btnExportXml, 1, 0);
            toolStrip.Controls.Add(_btnAddCondition, 2, 0);
            toolStrip.Controls.Add(_btnDeleteCondition, 3, 0);
            toolStrip.Controls.Add(_btnClearConditions, 4, 0);
            toolStrip.Controls.Add(_btnUsageGuide, 5, 0);
            toolStrip.Height = CalculateToolbarHeight(_btnImportXml, toolStrip.Padding);

            // ── 搜索条件表格：逐步构建确保列标题可见 ──
            _conditionsGrid = CreateSearchGrid();
            _conditionsGrid.CellDoubleClick += ConditionsGrid_CellDoubleClick;
            _conditionsGrid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete) BtnDeleteCondition_Click(null, null);
            };

            var gridHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.White,
                Padding = new Padding(ScaleLogical(6), 0, ScaleLogical(6), ScaleLogical(6)),
            };
            gridHost.Controls.Add(_conditionsGrid);
            _tabConditions.Controls.Add(gridHost);
            _tabConditions.Controls.Add(toolStrip);
            _tabConditions.ResumeLayout(true);
        }

        /// <summary>
        /// 创建统一风格的搜索条件表格。
        /// </summary>
        private DataGridView CreateSearchGrid()
        {
            var grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.ColumnHeadersVisible = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor = System.Drawing.Color.White;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.GridColor = System.Drawing.Color.FromArgb(226, 232, 240);
            grid.EnableHeadersVisualStyles = false;
            grid.RowTemplate.Height = CalculateContentHeight(grid.Font, 1, 12);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            // 先添加列，再设置 Fill 模式
            grid.Columns.Add("Category", "分类");
            grid.Columns.Add("Property", "属性");
            grid.Columns.Add("Test", "匹配方式");
            grid.Columns.Add("Value", "查询值");
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            // 列标题样式
            grid.ColumnHeadersHeight = 24;
            grid.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(241, 245, 249);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            ApplyGridHeaderLayout(grid);
            grid.DefaultCellStyle.BackColor = System.Drawing.Color.White;
            grid.DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            grid.DefaultCellStyle.Padding = new Padding(ScaleLogical(4), 0, ScaleLogical(4), 0);
            grid.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            grid.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(30, 41, 59);
            grid.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            return grid;
        }

        private void BuildOptionsTab()
        {
            _tabOptions.SuspendLayout();

            var optionsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(ScaleLogical(12)),
                BackColor = this.BackColor,
                ColumnCount = 1,
                RowCount = 4,
            };
            // GroupBox 开销 = 标题(~20) + 上内边距(14) + 下内边距(8) + 边框(~4) ≈ 44
            // 行高 = N×行高 + 开销
            var lineHeight = CalculateContentHeight(this.Font, 1, 4);
            var groupRowHeight = lineHeight * 2 + ScaleLogical(44);
            var singleRowHeight = lineHeight + ScaleLogical(44);
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, groupRowHeight));
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, groupRowHeight));
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, singleRowHeight));
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _chkHideAfterSearch = new CheckBox
            {
                Text = "模式 B：查找并选中后，弹窗确认，再执行隐藏未选中",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Checked = false,
                ForeColor = System.Drawing.Color.FromArgb(51, 65, 85),
                Margin = new Padding(0),
            };

            _chkTestMode = new CheckBox
            {
                Text = "模式 A：仅查找并选中，不隐藏",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Checked = true,
                ForeColor = System.Drawing.Color.FromArgb(51, 65, 85),
                Margin = new Padding(0),
            };

            _chkHideAfterSearch.CheckedChanged += (s, e) =>
            {
                if (_chkHideAfterSearch.Checked)
                {
                    _chkTestMode.Checked = false;
                }
                else if (!_chkTestMode.Checked)
                {
                    _chkTestMode.Checked = true;
                }
            };
            _chkTestMode.CheckedChanged += (s, e) =>
            {
                if (_chkTestMode.Checked)
                {
                    _chkHideAfterSearch.Checked = false;
                }
                else if (!_chkHideAfterSearch.Checked)
                {
                    _chkHideAfterSearch.Checked = true;
                }
            };

            GroupBox MakeOptionGroup(string title, Control content)
            {
                var group = new GroupBox
                {
                    Text = title,
                    Dock = DockStyle.Fill,
                    Padding = new Padding(ScaleLogical(10), ScaleLogical(14), ScaleLogical(10), ScaleLogical(8)),
                    ForeColor = System.Drawing.Color.FromArgb(51, 65, 85),
                    BackColor = this.BackColor,
                    Margin = new Padding(0, 0, 0, ScaleLogical(8)),
                };
                content.Dock = DockStyle.Fill;
                group.Controls.Add(content);
                return group;
            }

            var modePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            modePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            modePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            modePanel.Controls.Add(_chkTestMode, 0, 0);
            modePanel.Controls.Add(_chkHideAfterSearch, 0, 1);

            var lblScope = new Label
            {
                Text = "搜索范围由 Navisworks 选择树当前选中的节点决定。\r\n" +
                       "如果没有预先选中范围，执行搜索时会按整个模型处理。",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = System.Drawing.Color.FromArgb(71, 85, 105),
            };

            _chkDiagnosticLog = new CheckBox
            {
                Text = "启用诊断日志（每次搜索结束后输出详细诊断文件，用于排查问题）",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Checked = false,
                ForeColor = System.Drawing.Color.FromArgb(71, 85, 105),
                Margin = new Padding(0),
            };

            optionsLayout.Controls.Add(MakeOptionGroup("搜索模式", modePanel), 0, 0);
            optionsLayout.Controls.Add(MakeOptionGroup("搜索范围", lblScope), 0, 1);
            optionsLayout.Controls.Add(MakeOptionGroup("诊断日志", _chkDiagnosticLog), 0, 2);
            _tabOptions.Controls.Add(optionsLayout);
            _tabOptions.ResumeLayout();
        }

        private void BuildResultsTab()
        {
            _tabResults.SuspendLayout();
            int summaryHeight = CalculateContentHeight(this.Font, 2, 24);
            int filterHeight = CalculateButtonHeight(this.Font) + ScaleLogical(12);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(ScaleLogical(4)),
                BackColor = _tabResults.BackColor,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, summaryHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, filterHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lblResultSummary = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(ScaleLogical(12), ScaleLogical(8), ScaleLogical(12), 0),
                BackColor = System.Drawing.Color.FromArgb(239, 246, 255),
                ForeColor = System.Drawing.Color.FromArgb(30, 64, 175),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "尚未执行搜索。",
            };

            _resultFilterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, ScaleLogical(6), 0, 0),
                BackColor = _tabResults.BackColor,
            };
            AddResultFilterButton(SearchResultFilter.All, "全部");
            AddResultFilterButton(SearchResultFilter.Problems, "问题项");
            AddResultFilterButton(SearchResultFilter.Found, "已找到");
            AddResultFilterButton(SearchResultFilter.NotFound, "未找到");
            AddResultFilterButton(SearchResultFilter.Duplicate, "重复");
            AddResultFilterButton(SearchResultFilter.ConditionInvalid, "条件异常");

            _resultsGrid = CreateResultsGrid();
            _resultsGrid.CellDoubleClick += ResultsGrid_CellDoubleClick;
            SetActiveResultFilter(SearchResultFilter.All);

            root.Controls.Add(_lblResultSummary, 0, 0);
            root.Controls.Add(_resultFilterPanel, 0, 1);
            root.Controls.Add(_resultsGrid, 0, 2);
            _tabResults.Controls.Add(root);
            _tabResults.ResumeLayout(true);
        }

        private void AddResultFilterButton(SearchResultFilter filter, string text)
        {
            var button = new Button
            {
                Text = text,
                Tag = filter,
                AutoSize = true,
                MinimumSize = new Size(
                    ScaleLogical(74),
                    CalculateButtonHeight(this.Font)),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(51, 65, 85),
                Font = this.Font,
                Margin = new Padding(0, 0, ScaleLogical(6), 0),
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
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
                AllowUserToResizeRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(226, 232, 240),
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(51, 65, 85);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
            grid.EnableHeadersVisualStyles = false;
            grid.RowTemplate.Height = CalculateContentHeight(grid.Font, 1, 12);
            ApplyGridHeaderLayout(grid);

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ConditionIndex",
                HeaderText = "序号",
                Width = ScaleLogical(58),
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "状态",
                Width = ScaleLogical(88),
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Category",
                HeaderText = "分类",
                Width = ScaleLogical(118),
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Property",
                HeaderText = "属性名",
                Width = ScaleLogical(132),
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
                FillWeight = 32F,
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MatchCount",
                HeaderText = "匹配数",
                Width = ScaleLogical(70),
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Message",
                HeaderText = "说明",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 48F,
            });
            return grid;
        }

        private void ResultsGrid_CellDoubleClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _resultsGrid.Rows.Count)
                return;

            var result = _resultsGrid.Rows[e.RowIndex].Tag as SearchResult;
            if (result == null)
                return;

            if (result.Status == SearchResultStatus.Duplicate)
            {
                try
                {
                    List<ModelItem> selected = SelectionService.SetSelection(
                        _doc,
                        result.MatchedItems);
                    MessageBox.Show(
                        this,
                        $"已在 Navisworks 中选中 {selected.Count} 个重复对象。\n" +
                        "再次搜索前，请重新确认选择树中的搜索范围。",
                        "傑出品·重复对象",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        "无法选中重复对象：\n" + ex.Message,
                        "傑出品·定位失败",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                return;
            }

            int conditionIndex = result.Condition.ConditionIndex;
            if (conditionIndex < 0 || conditionIndex >= _conditionsGrid.Rows.Count)
                return;

            _tabControl.SelectedTab = _tabConditions;
            _conditionsGrid.ClearSelection();
            DataGridViewRow conditionRow = _conditionsGrid.Rows[conditionIndex];
            conditionRow.Selected = true;
            _conditionsGrid.CurrentCell = conditionRow.Cells[COL_VALUE];
            _conditionsGrid.FirstDisplayedScrollingRowIndex = conditionIndex;
        }

        private void ResultFilterButton_Click(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is SearchResultFilter filter)
                SetActiveResultFilter(filter);
        }

        private void SetActiveResultFilter(SearchResultFilter filter)
        {
            _activeResultFilter = filter;
            foreach (Control control in _resultFilterPanel.Controls)
            {
                var button = control as Button;
                if (button == null)
                    continue;

                bool active = button.Tag is SearchResultFilter value && value == filter;
                button.BackColor = active ? Color.FromArgb(37, 99, 235) : Color.White;
                button.ForeColor = active ? Color.White : Color.FromArgb(51, 65, 85);
            }
            RefreshResultsGrid(_lastResults ?? Enumerable.Empty<SearchResult>());
        }

        private void RefreshResultsGrid(IEnumerable<SearchResult> results)
        {
            _resultsGrid.Rows.Clear();
            foreach (SearchResult result in results)
            {
                if (!SearchResultPolicy.MatchesFilter(result.Status, _activeResultFilter))
                    continue;

                int rowIndex = _resultsGrid.Rows.Add(
                    result.Condition.DisplayIndex,
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
                foreach (DataGridViewCell cell in row.Cells)
                    cell.ToolTipText = Convert.ToString(cell.Value);
            }
        }

        private static void ApplyResultRowStyle(
            DataGridViewRow row,
            SearchResultStatus status)
        {
            switch (status)
            {
                case SearchResultStatus.Found:
                    row.DefaultCellStyle.BackColor = Color.White;
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(22, 101, 52);
                    break;
                case SearchResultStatus.NotFound:
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 241, 242);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(153, 27, 27);
                    break;
                case SearchResultStatus.Duplicate:
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 247, 237);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(154, 52, 18);
                    break;
                case SearchResultStatus.ConditionInvalid:
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 251, 235);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(133, 77, 14);
                    break;
            }
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            row.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
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

        private void RefreshConditionsGrid()
        {
            _conditionsGrid.Rows.Clear();
            foreach (var c in _conditions)
            {
                _conditionsGrid.Rows.Add(
                    c.CategoryDisplay ?? c.CategoryInternal ?? "",
                    c.PropertyDisplay ?? c.PropertyInternal ?? "",
                    c.Test,
                    c.Value);
            }

        }

        /// <summary>
        /// 从 XML 文件加载搜索条件。
        /// </summary>
        private void LoadFromXml(string xmlPath)
        {
            _currentXmlPath = xmlPath;
            _conditions = XmlSearchParser.Parse(xmlPath);
            RefreshConditionsGrid();
            InvalidateSearchResults("已导入新的搜索条件，请执行搜索。");
        }

        private void InvalidateSearchResults(string message)
        {
            _lastResults = null;
            _lastTotalMatched = 0;
            _lastHideExecuted = false;
            _currentModelPrefix = null;
            _lblResultSummary.Text = message;
            UpdateResultFilterCaptions(Array.Empty<SearchResult>());
            SetActiveResultFilter(SearchResultFilter.All);
            _btnExportResults.Enabled = false;
            _btnCreateSelectionSet.Enabled = false;
        }

        #endregion

        #region 条件编辑对话框

        private static DialogResult ShowConditionEditor(
            ref string category,
            ref string property,
            ref string test,
            ref string value)
        {
            var form = new Form
            {
                Text = "编辑条件",
                ClientSize = new Size(ScaleLogical(760), ScaleLogical(420)),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                Font = new Font("Microsoft YaHei UI", 9F),
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(ScaleLogical(16)),
                ColumnCount = 2,
                RowCount = 6,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(130)));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleLogical(156)));
            for (int i = 0; i < 4; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleLogical(42)));
            }
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleLogical(54)));

            var helpText = new Label
            {
                Text = "填写方法：先在 Navisworks 里选中目标对象，打开“属性”窗口，对照属性面板填写。\r\n" +
                       "分类：填属性所在的分组/选项卡名称，例如 Item、Element、SmartPlant 3D；不确定可留空。\r\n" +
                       "属性名：填属性面板左侧名称，例如 名称、System Path、Tag，必须和界面显示一致。\r\n" +
                       "查询值：填属性面板右侧要找的值，例如 M14-101、P-001 或某段编号。\r\n" +
                       "匹配方式：equals = 完全相同；contains = 属性值里包含这段文字即可。\r\n" +
                       "示例：属性面板显示 Item / 名称 = M14-101，就填 分类：Item，属性名：名称，查询值：M14-101。",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(ScaleLogical(10)),
                BackColor = System.Drawing.Color.FromArgb(245, 247, 250),
                BorderStyle = BorderStyle.FixedSingle,
            };
            layout.Controls.Add(helpText, 0, 0);
            layout.SetColumnSpan(helpText, 2);

            Label MakeFieldLabel(string text)
            {
                return new Label
                {
                    Text = text,
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(0, 0, ScaleLogical(8), 0),
                };
            }

            TextBox MakeTextBox(string text)
            {
                return new TextBox
                {
                    Text = text,
                    Dock = DockStyle.Top,
                    Margin = new Padding(0, ScaleLogical(7), 0, 0),
                };
            }

            var lblCat = MakeFieldLabel("分类（可选）");
            var txtCat = MakeTextBox(category);

            var lblProp = MakeFieldLabel("属性名（必填）");
            var txtProp = MakeTextBox(property);

            var lblTest = MakeFieldLabel("匹配方式");
            var cmbTest = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, ScaleLogical(7), 0, 0),
            };
            cmbTest.Items.AddRange(new[] { "equals", "contains" });
            cmbTest.SelectedItem = string.Equals(test, "contains", StringComparison.OrdinalIgnoreCase)
                ? "contains"
                : "equals";

            var lblVal = MakeFieldLabel("查询值");
            var txtVal = MakeTextBox(value);

            layout.Controls.Add(lblCat, 0, 1);
            layout.Controls.Add(txtCat, 1, 1);
            layout.Controls.Add(lblProp, 0, 2);
            layout.Controls.Add(txtProp, 1, 2);
            layout.Controls.Add(lblTest, 0, 3);
            layout.Controls.Add(cmbTest, 1, 3);
            layout.Controls.Add(lblVal, 0, 4);
            layout.Controls.Add(txtVal, 1, 4);

            var buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, ScaleLogical(10), 0, 0),
                ColumnCount = 4,
                RowCount = 1,
            };
            var buttonGap = ScaleLogical(12);
            var buttonWidth = ScaleLogical(112);
            var buttonHeight = CalculateButtonHeight(form.Font) + ScaleLogical(8);
            var buttonSize = new Size(buttonWidth, buttonHeight);
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, buttonWidth));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, buttonGap));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, buttonWidth));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, buttonHeight));

            var btnOk = new Button
            {
                Text = "确定",
                Dock = DockStyle.Fill,
                Size = buttonSize,
                TextAlign = ContentAlignment.MiddleCenter,
                DialogResult = DialogResult.OK,
                Margin = new Padding(0),
            };
            var btnCancel = new Button
            {
                Text = "取消",
                Dock = DockStyle.Fill,
                Size = buttonSize,
                TextAlign = ContentAlignment.MiddleCenter,
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(0),
            };
            buttonPanel.Controls.Add(btnOk, 1, 0);
            buttonPanel.Controls.Add(btnCancel, 3, 0);
            layout.Controls.Add(buttonPanel, 0, 5);
            layout.SetColumnSpan(buttonPanel, 2);

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

            form.Controls.Add(layout);
            form.AcceptButton = btnOk;
            form.CancelButton = btnCancel;

            var result = form.ShowDialog();
            if (result == DialogResult.OK)
            {
                category = txtCat.Text;
                property = txtProp.Text;
                test = cmbTest.SelectedItem?.ToString() ?? "equals";
                value = txtVal.Text;
            }
            return result;
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
                RefreshConditionsGrid();
                InvalidateSearchResults("条件已修改，请重新执行搜索。");
            }
        }

        #endregion

        #region 按钮事件

        private void BtnImportXml_Click(object sender, EventArgs e)
        {
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
            using (var dialog = new SaveFileDialog
            {
                Title = "导出 XML 文件",
                Filter = "XML 文件 (*.xml)|*.xml",
                FileName = "搜索条件.xml",
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    ExportConditionsToXml(dialog.FileName);
                }
            }
        }

        private void BtnUsageGuide_Click(object sender, EventArgs e)
        {
            ShowUsageGuideDialog();
        }

        private void ShowUsageGuideDialog()
        {
            var bodyFont = new Font("Microsoft YaHei UI", 10F);
            var boldFont = new Font(bodyFont, FontStyle.Bold);
            var titleFont = new Font(bodyFont.FontFamily, 14F, FontStyle.Bold);
            var contentPad = ScaleLogical(22);

            var form = new Form
            {
                Text = "使用说明 — 傑出品 Navisworks 查找插件",
                ClientSize = new Size(ScaleLogical(780), ScaleLogical(600)),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                BackColor = System.Drawing.Color.FromArgb(245, 247, 250),
                Font = bodyFont,
            };
            form.Disposed += (s, e) =>
            {
                boldFont.Dispose();
                titleFont.Dispose();
                bodyFont.Dispose();
            };

            // RichTextBox：单控件承载全部文本，仅格式化标题/正文两级
            var rtb = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = System.Drawing.Color.White,
                Font = bodyFont,
                ForeColor = System.Drawing.Color.FromArgb(30, 41, 59),
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true,
                Multiline = true,
            };

            void H(string text)
            {
                int start = rtb.TextLength;
                rtb.AppendText(text + "\n");
                rtb.Select(start, text.Length);
                rtb.SelectionFont = boldFont;
            }
            void T(string text)
            {
                int start = rtb.TextLength;
                rtb.AppendText(text + "\n");
                rtb.Select(start, text.Length);
                rtb.SelectionFont = rtb.Font;
            }
            void G() => rtb.AppendText("\n");

            // ── 构建内容 ──
            {
                int start = rtb.TextLength;
                rtb.AppendText("傑出品 Navisworks 查找插件 — 使用说明\n");
                rtb.Select(start, rtb.TextLength - start - 1);
                rtb.SelectionFont = titleFont;
            }
            G();

            H("▎插件简介");
            T("「傑出品查找」用于在 Navisworks 三维模型中按属性值搜索对象。");
            T("支持导入 XML 查找文件或手动填写条件，可执行精确匹配（equals）和模糊匹配（contains）。");
            T("核心流程：搜索 → 选中 → 隐藏未选中 / 创建选择集 / 导出结果。");
            G();

            H("━━━ 操作步骤 ━━━");
            G();

            H("第一步：打开模型并选择搜索范围");
            T("1. 在 Navisworks 中打开模型（.nwd / .nwf / .nwc）。");
            T("2. 在左侧「选择树」中单击选中要搜索的模型节点");
            T("   （通常是以 TS-MXXXX-... 开头的根节点，代表一个模型）。");
            T("3. 如需只搜索某个子系统，选中对应的子节点即可。");
            T("4. 不选范围时点击搜索会提示你选择。");
            G();

            H("第二步：打开插件");
            T("点击 Navisworks 顶部 Add-Ins 选项卡 → 工具栏中的「傑出品查找」按钮。");
            T("弹出主对话框，包含三个选项卡：搜索条件、选项、结果。");
            G();

            H("第三步：准备搜索条件");
            T("");
            T("  【方式一：导入 XML（推荐）】");
            T("  点击工具栏「导入」→ 选择 Python 工具生成的 .xml 查找文件 → 条件自动填入表格。");
            T("  双击表格行可修改条件（分类、属性名、匹配方式、查询值）。");
            T("");
            T("  【方式二：手动添加】");
            T("  1. 在模型中选中目标对象，打开 Navisworks「属性」窗口。");
            T("  2. 点击插件「添加」按钮，对照属性面板填写：");
            T("     · 分类（可选）：属性所在的分组名，如 Item、Element、SmartPlant 3D。");
            T("       不确定可留空，插件会自动识别。");
            T("     · 属性名（必填）：属性面板左侧的名称，如 名称、System Path。");
            T("     · 查询值：属性面板右侧的值，如 M14-101、P-001。");
            T("     · 匹配方式：equals = 完全相同；contains = 包含即可。");
            T("");
            T("  【填表示例】");
            T("  · 属性面板显示 Item / 名称 = M14-101");
            T("    填：分类=Item，属性名=名称，匹配方式=equals，查询值=M14-101");
            T("  · 属性面板显示 SmartPlant 3D / System Path = Area-01/P-001/Line-A");
            T("    填：分类=SmartPlant 3D，属性名=System Path，匹配方式=contains，查询值=P-001");
            T("");
            T("  【工具栏按钮】");
            T("  导入：从 XML 加载条件 | 导出：保存当前条件为 XML | 添加/删除/清空：管理条件。");
            G();

            H("第四步：选择搜索模式（切换到「选项」页）");
            T("");
            T("  【模式 A：仅查找并选中】（默认）");
            T("  搜索后选中匹配对象，不做隐藏。适合快速定位查看。");
            T("");
            T("  【模式 B：查找 + 隐藏未选中】");
            T("  搜索并选中后弹窗确认，确认后隐藏不相关的对象，只保留匹配项和 STR 结构节点。");
            T("  匹配数为 0 时自动拒绝隐藏，防止误隐藏整个模型。");
            T("");
            T("  【诊断日志（默认关闭）】勾选后每次搜索输出详细诊断文件。日常无需开启，排查问题时使用。");
            G();

            H("第五步：执行搜索");
            T("确认条件表格不为空、选择树中已选中范围 → 点击底部蓝色「执行搜索」按钮。");
            T("大模型可能需数秒，插件不会卡死 Navisworks。完成后自动切换到「结果」页。");
            G();

            H("第六步：查看结果与后续操作");
            T("");
            T("  【结果摘要（蓝色区域）】条件总数、找到/未找到条数、总匹配对象数。");
            T("  【详细列表】[✓] 匹配成功  [✗] 未匹配到。");
            T("");
            T("  搜索完成后，底部按钮启用：");
            T("  · 导出结果 → 将匹配结果保存为 CSV/TXT 文件。");
            T("  · 创建选择集 → 将匹配对象持久化为 Navisworks「集合」面板中的选择集。");
            T("    右键该选择集 →「选择」后可批量修改颜色、透明度、隐藏等属性。");
            T("    选择集会随 .nwf 文件保存，关闭插件后仍可使用。");
            T("  · 隐藏未选中（仅模式 B 生效）→ 弹窗确认后隐藏不相关对象。");
            G();

            H("━━━ 常见问题 ━━━");
            G();

            H("Q: 搜索不到任何对象？");
            T("A: 检查属性名是否与 Navisworks 属性面板完全一致（含空格、大小写）。");
            T("   确认查询值前后无多余空格、搜索范围正确。可开启诊断日志排查。");
            G();

            H("Q: 分类（Category）要不要填？");
            T("A: 可不填，插件会自动扫描模型发现分类。模型很大且属性名在多个分类都存在时，手动填写可提高准确性。");
            G();

            H("Q: equals 和 contains 怎么选？");
            T("A: equals = 完全相等（适合编号/名称）；contains = 包含即可（适合路径/描述中的关键词）。");
            G();

            H("Q: 隐藏错了怎么恢复？");
            T("A: Navisworks「常用」选项卡 →「全部显示」。建议隐藏前先用模式 A 确认搜索结果。");

            // ── 用 Panel 包裹 RichTextBox → 提供可见边界 + 内边距 ──
            var contentCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(contentPad),
                Margin = new Padding(0),
            };
            contentCard.Controls.Add(rtb);
            rtb.Dock = DockStyle.Fill;

            // ── 关闭按钮 ──
            var btnFont = new Font(bodyFont, FontStyle.Bold);
            var btnClose = new Button
            {
                Text = "关闭",
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                    BorderColor = System.Drawing.Color.FromArgb(203, 213, 225),
                    BorderSize = 1,
                    MouseOverBackColor = System.Drawing.Color.FromArgb(248, 250, 252),
                },
                BackColor = System.Drawing.Color.White,
                ForeColor = System.Drawing.Color.FromArgb(51, 65, 85),
                Font = btnFont,
                UseVisualStyleBackColor = false,
                Size = new Size(ScaleLogical(110), CalculateButtonHeight(btnFont) + ScaleLogical(8)),
                DialogResult = DialogResult.OK,
            };

            // ── 布局：内容卡片在上，按钮在右下 ──
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(ScaleLogical(14)),
                ColumnCount = 1,
                RowCount = 2,
                BackColor = form.BackColor,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, btnClose.Height + ScaleLogical(20)));

            var buttonRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = form.BackColor,
            };
            buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, btnClose.Width));
            buttonRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            buttonRow.Controls.Add(btnClose, 1, 0);
            btnClose.Anchor = AnchorStyles.Right;

            layout.Controls.Add(contentCard, 0, 0);
            layout.Controls.Add(buttonRow, 0, 1);
            form.Controls.Add(layout);
            form.AcceptButton = btnClose;
            form.CancelButton = btnClose;
            form.Shown += (s, e) => btnClose.Focus();
            form.ShowDialog(this);
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
                RefreshConditionsGrid();
                InvalidateSearchResults("已添加搜索条件，请重新执行搜索。");
            }
        }

        private void BtnDeleteCondition_Click(object sender, EventArgs e)
        {
            if (_conditionsGrid.SelectedRows.Count > 0)
            {
                int idx = _conditionsGrid.SelectedRows[0].Index;
                if (idx >= 0 && idx < _conditions.Count)
                {
                    _conditions.RemoveAt(idx);
                    RefreshConditionsGrid();
                    InvalidateSearchResults("已删除搜索条件，请重新执行搜索。");
                }
            }
        }

        #endregion

        #region 搜索执行

        private void BtnSearch_Click(object sender, EventArgs e)
        {
            if (_conditions.Count == 0)
            {
                MessageBox.Show(this, "请先添加至少一个搜索条件。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _tabControl.SelectedTab = _tabConditions;
                return;
            }

            if (_doc == null)
            {
                MessageBox.Show(this, "文档无效，请重新打开 Navisworks。",
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

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

            diagnosticLog?.LogMode(modeName);
            diagnosticLog?.LogHideIntent(!isSelectOnlyMode);

            try
            {
                Cursor = Cursors.WaitCursor;
                _btnSearch.Enabled = false;

                List<ModelItem> scopeRoots = SnapshotCurrentSelection(_doc);
                if (scopeRoots.Count == 0)
                {
                    diagnosticLog?.LogScopeInfo(scopeRoots, 0);
                    diagnosticLog?.LogModelPrefixInfo(
                        Enumerable.Empty<string>(),
                        null,
                        null);
                    MessageBox.Show(this,
                        "请先在选择树中选中本次搜索范围。",
                        "傑出品·搜索范围",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                List<string> modelPrefixes = ExtractModelPrefixes(scopeRoots);
                string modelPrefix = null;
                string protectedName = null;
                if (modelPrefixes.Count == 0)
                {
                    diagnosticLog?.LogScopeInfo(scopeRoots, 0);
                    diagnosticLog?.LogModelPrefixInfo(
                        modelPrefixes,
                        null,
                        null);
                    MessageBox.Show(this,
                        "无法从当前选择范围识别模型前缀，请检查选择树节点名称。",
                        "傑出品·模型前缀",
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

                List<ModelItem> matchedItemsInScope =
                    MergeUniqueItems(results.SelectMany(r => r.MatchedItems));
                totalMatched = matchedItemsInScope.Count;
                bool resultGatePassed = SearchResultPolicy.CanHide(
                    results.Select(r => r.Status));

                _lastResults = results;
                _lastTotalMatched = totalMatched;
                _lastHideExecuted = false;
                ShowResults(results, totalMatched, modelPrefix);
                diagnosticLog?.LogXmlScopeResultStats(
                    results.Sum(r => r.MatchCount),
                    matchedItemsInScope.Count,
                    0);
                diagnosticLog?.LogSearchResults(results, resultGatePassed);

                if (!isSelectOnlyMode && !resultGatePassed)
                {
                    int notFoundCount = results.Count(
                        r => r.Status == SearchResultStatus.NotFound);
                    int duplicateCount = results.Count(
                        r => r.Status == SearchResultStatus.Duplicate);
                    int invalidCount = results.Count(
                        r => r.Status == SearchResultStatus.ConditionInvalid);
                    string reason =
                        $"结果未通过唯一性校验：未找到 {notFoundCount} 条，" +
                        $"重复 {duplicateCount} 条，条件异常 {invalidCount} 条。";

                    if (matchedItemsInScope.Count > 0)
                    {
                        List<ModelItem> problemSelection = SelectionService.SetSelection(
                            _doc,
                            matchedItemsInScope,
                            diagnosticLog);
                        diagnosticLog?.LogDecision(
                            $"问题结果定位选择数量: {problemSelection.Count}");
                    }

                    diagnosticLog?.LogDecision(reason);
                    diagnosticLog?.LogHideBlocked(reason);
                    MessageBox.Show(
                        this,
                        reason +
                        "\n\n已阻止隐藏未选中。结果页保留全部问题项；" +
                        "修改条件或模型数据后请重新搜索。" +
                        "\n再次搜索前，请在选择树中重新选择目标模型范围。",
                        "傑出品·唯一性校验未通过",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    _lastHideExecuted = false;
                    _btnExportResults.Enabled = true;
                    _btnCreateSelectionSet.Enabled = totalMatched > 0;
                    _tabControl.SelectedTab = _tabResults;
                    return;
                }

                ProtectedKeepResult protectedKeepResult;
                ModelItem protectedNode = scopeRoots.Find(
                    r => string.Equals(r.DisplayName, protectedName, StringComparison.Ordinal));
                if (protectedNode != null)
                {
                    protectedKeepResult = ProtectedKeepService.BuildFromNode(
                        protectedName, protectedNode);
                }
                else
                {
                    protectedKeepResult = ProtectedKeepService.FindProtectedItems(
                        _doc, protectedName);
                }
                diagnosticLog?.LogProtectedNodeStats(
                    protectedKeepResult.TargetNodeName,
                    protectedKeepResult.Found,
                    protectedKeepResult.MatchMode,
                    protectedKeepResult.MatchedNodeCount,
                    protectedKeepResult.DescendantCount,
                    protectedKeepResult.ProtectedItems.Count);

                if (totalMatched == 0)
                {
                    if (!isSelectOnlyMode)
                    {
                        diagnosticLog?.LogHidePrecheck(0, 0);
                        diagnosticLog?.LogHideBlocked();
                    }

                    MessageBox.Show(this,
                        "在选定范围内没有搜索到任何对象，已取消隐藏，防止隐藏整个模型。",
                        "傑出品·查找结果",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    if (protectedKeepResult.ProtectedItems.Count == 0 && !isSelectOnlyMode)
                    {
                        DialogResult protectedChoice = MessageBox.Show(this,
                            $"未找到当前模型对应的 STR 节点：{protectedName}。继续后该结构节点可能会被隐藏，是否继续？",
                            "傑出品·警告",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);
                        diagnosticLog?.LogDecision(
                            $"Protected node warning choice: {protectedChoice}");
                        if (protectedChoice != DialogResult.Yes)
                        {
                            return;
                        }
                    }

                    List<ModelItem> finalKeepItems = MergeUniqueItems(
                        matchedItemsInScope,
                        protectedKeepResult.ProtectedItems);

                    List<ModelItem> actualSelectionItems = SelectionService.SetSelection(
                        _doc,
                        finalKeepItems,
                        diagnosticLog);
                    int actualSelectionCount = actualSelectionItems.Count;
                    bool willHide = !isSelectOnlyMode
                        && resultGatePassed
                        && totalMatched > 0
                        && finalKeepItems.Count > 0
                        && actualSelectionCount > 0;

                    diagnosticLog?.LogFinalKeepStats(
                        matchedItemsInScope.Count,
                        protectedKeepResult.ProtectedItems.Count,
                        finalKeepItems.Count,
                        actualSelectionCount,
                        willHide);

                    if (finalKeepItems.Count == 0)
                    {
                        diagnosticLog?.LogDecision("Blocked: finalKeepItems count is 0.");
                        diagnosticLog?.LogHidePrecheck(0, actualSelectionCount);
                        diagnosticLog?.LogHideBlocked();
                        MessageBox.Show(this,
                            "最终保留集合为空，已取消隐藏。",
                            "傑出品·保护",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    if (actualSelectionCount == 0)
                    {
                        diagnosticLog?.LogDecision("Blocked: CurrentSelection count is 0.");
                        diagnosticLog?.LogHidePrecheck(finalKeepItems.Count, actualSelectionCount);
                        diagnosticLog?.LogHideBlocked();
                        MessageBox.Show(this,
                            "写入最终保留集合后当前选择为空，已取消隐藏。",
                            "傑出品·保护",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    if (isSelectOnlyMode)
                    {
                        string testModeMsg =
                            $"【模式 A：仅查找并选中】\n\n" +
                            $"范围内命中对象：{matchedItemsInScope.Count} 个\n" +
                            $"当前模型 STR 保留对象：{protectedKeepResult.ProtectedItems.Count} 个\n" +
                            $"最终保留对象：{finalKeepItems.Count} 个\n\n" +
                            "插件已选中最终保留对象，未执行隐藏。";
                        MessageBox.Show(this, testModeMsg,
                            "傑出品·模式 A", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        DialogResult choice = MessageBox.Show(this,
                            $"范围内命中对象数量：{matchedItemsInScope.Count}\n" +
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

                        if (choice == DialogResult.Yes)
                        {
                            hideExecuted = HideService.HideUnselected(
                                _doc,
                                finalKeepItems,
                                finalKeepItems.Count,
                                diagnosticLog);
                        }
                    }
                }

                _lastResults = results;
                _lastTotalMatched = totalMatched;
                _lastHideExecuted = hideExecuted;
                _btnExportResults.Enabled = true;
                _btnCreateSelectionSet.Enabled = totalMatched > 0;
                _tabControl.SelectedTab = _tabResults;
            }
            catch (Exception ex)
            {
                diagnosticLog?.LogException("执行搜索", ex);

                string detail = ex.Message;
                if (ex.InnerException != null)
                    detail += "\n\n详情：" + ex.InnerException.Message;

                MessageBox.Show(this, "操作失败：\n" + detail,
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                diagnosticLog?.WriteToFile();
                Cursor = Cursors.Default;
                _btnSearch.Enabled = true;
            }
        }

        /// <summary>
        /// 在"结果"选项卡中显示搜索结果。
        /// </summary>
        private void ShowResults(
            List<SearchResult> results,
            int totalMatched,
            string scopeLabel)
        {
            int found = results.Count(r => r.Status == SearchResultStatus.Found);
            int notFound = results.Count(r => r.Status == SearchResultStatus.NotFound);
            int duplicate = results.Count(r => r.Status == SearchResultStatus.Duplicate);
            int invalid = results.Count(r => r.Status == SearchResultStatus.ConditionInvalid);

            _lastResults = results;
            _lblResultSummary.Text =
                $"范围：{scopeLabel}　　条件总数：{results.Count}　　" +
                $"已找到：{found}　未找到：{notFound}　重复：{duplicate}　条件异常：{invalid}\n" +
                $"去重后的总匹配对象：{totalMatched} 个";
            UpdateResultFilterCaptions(results);
            SetActiveResultFilter(results.Any(r => r.IsProblem)
                ? SearchResultFilter.Problems
                : SearchResultFilter.All);
        }

        #endregion

        #region 导出

        private void BtnExportResults_Click(object sender, EventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0) return;

            using (var dialog = new SaveFileDialog
            {
                Title = "导出结果",
                Filter = "CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
                FileName = $"傑出品结果_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        using (var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
                        {
                            writer.WriteLine("查询值,匹配数,匹配详情");
                            foreach (var r in _lastResults)
                            {
                                string details = r.MatchCount > 0
                                    ? string.Join("; ", r.MatchedItems.Select(
                                        i => i.DisplayName ?? i.InstanceGuid.ToString()))
                                    : "";
                                writer.WriteLine($"\"{r.QueryValue}\",{r.MatchCount},\"{details}\"");
                            }
                        }
                        MessageBox.Show(this, "导出成功！\n" + dialog.FileName,
                            "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "导出失败：\n" + ex.Message,
                            "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnCreateSelectionSet_Click(object sender, EventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0) return;

            try
            {
                var flatItems = MergeUniqueItems(
                    _lastResults.SelectMany(r => r.MatchedItems));

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

        private void ExportConditionsToXml(string xmlPath)
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
                MessageBox.Show(this, "导出 XML 成功！\n" + xmlPath,
                    "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：\n" + ex.Message,
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private static void RestoreSelection(Document doc, IEnumerable<ModelItem> items)
        {
            using (var selection = new ModelItemCollection())
            {
                foreach (ModelItem item in items)
                    selection.Add(item);

                doc.CurrentSelection.CopyFrom(selection);
            }
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

        #endregion
    }
}
