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
        private ListBox _lstResultDetails;
        private Button _btnSearch;
        private Button _btnExportResults;
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
                ColumnCount = 5,
                RowCount = 1,
                Padding = new Padding(0),
            };
            var bottomGap = ScaleLogical(12);
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(120)));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, bottomGap));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(108)));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(84)));
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            bottomLayout.Controls.Add(_btnSearch, 0, 0);
            bottomLayout.Controls.Add(_btnExportResults, 2, 0);
            bottomLayout.Controls.Add(_btnClose, 4, 0);
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
            _btnClearConditions = MakeToolButton("清空", (s, e) => { _conditions.Clear(); RefreshConditionsGrid(); });
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

            var lineH = MeasureTextHeight(this.Font);
            // 摘要：3 行文字 + 左右内边距(20) + 边框(4)
            var summaryH = lineH * 3 + ScaleLogical(24);
            // 详情标题：1 行文字 + 上内边距
            var detailH = lineH + ScaleLogical(6);

            _lblResultSummary = new Label
            {
                Dock = DockStyle.Top,
                Height = summaryH,
                Padding = new Padding(ScaleLogical(10)),
                BackColor = System.Drawing.Color.FromArgb(239, 246, 255),
                ForeColor = System.Drawing.Color.FromArgb(30, 64, 175),
                BorderStyle = BorderStyle.FixedSingle,
            };

            var lblDetails = new Label
            {
                Text = "详细匹配结果：",
                Dock = DockStyle.Top,
                Height = detailH,
                Padding = new Padding(ScaleLogical(10), ScaleLogical(4), 0, 0),
                ForeColor = System.Drawing.Color.FromArgb(51, 65, 85),
            };

            _lstResultDetails = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F),
                IntegralHeight = false,
                BackColor = System.Drawing.Color.White,
                ForeColor = System.Drawing.Color.FromArgb(30, 41, 59),
                BorderStyle = BorderStyle.FixedSingle,
            };

            _tabResults.Controls.Add(_lstResultDetails);
            _tabResults.Controls.Add(lblDetails);
            _tabResults.Controls.Add(_lblResultSummary);
            _tabResults.ResumeLayout();
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
            var form = new Form
            {
                Text = "使用说明",
                ClientSize = new Size(ScaleLogical(760), ScaleLogical(620)),
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
                Padding = new Padding(ScaleLogical(14)),
                ColumnCount = 1,
                RowCount = 2,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleLogical(70)));

            var guidePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.White,
                Padding = new Padding(ScaleLogical(18)),
            };
            var guideContent = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Location = new Point(guidePanel.Padding.Left, guidePanel.Padding.Top),
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = System.Drawing.Color.White,
            };
            guidePanel.Controls.Add(guideContent);

            var guideLabels = new List<Label>();
            var guideHeadingFont = new Font(form.Font, FontStyle.Bold);
            form.Disposed += (s, e) => guideHeadingFont.Dispose();

            Label MakeGuideLabel(string text, Font font, System.Drawing.Color color, Padding margin)
            {
                var label = new Label
                {
                    AutoSize = true,
                    Text = text,
                    Font = font,
                    ForeColor = color,
                    BackColor = System.Drawing.Color.White,
                    Margin = margin,
                };
                guideLabels.Add(label);
                return label;
            }

            void AddGuideSection(string title, params string[] lines)
            {
                guideContent.Controls.Add(MakeGuideLabel(
                    title,
                    guideHeadingFont,
                    System.Drawing.Color.FromArgb(44, 62, 80),
                    new Padding(0, 0, 0, ScaleLogical(8))));

                foreach (var line in lines)
                {
                    guideContent.Controls.Add(MakeGuideLabel(
                        line,
                        form.Font,
                        System.Drawing.Color.FromArgb(33, 37, 41),
                        new Padding(0, 0, 0, ScaleLogical(6))));
                }

                guideContent.Controls.Add(new Panel
                {
                    Width = ScaleLogical(1),
                    Height = ScaleLogical(12),
                    Margin = new Padding(0),
                    BackColor = System.Drawing.Color.White,
                });
            }

            AddGuideSection("使用流程",
                "1. 在 Navisworks 里打开模型。",
                "2. 如果只想查某个范围，先在选择树里选中该范围；否则默认查整个模型。",
                "3. 在“搜索条件”页导入 XML，或点击“添加”手动填写一条条件。",
                "4. 点击“执行搜索”，插件会选中匹配对象并在“结果”页显示汇总。",
                "5. 如果选择隐藏模式，确认后才会隐藏未选中对象；匹配数为 0 时不会隐藏。");

            AddGuideSection("手动添加条件怎么填",
                "1. 先在模型中选中一个你想查找的目标对象。",
                "2. 打开 Navisworks 的“属性”窗口。",
                "3. 分类：填写属性窗口里的分组/选项卡名称，例如 Item、Element、SmartPlant 3D。不确定时可以留空。",
                "4. 属性名：填写属性面板左侧的名称，例如 名称、System Path、Tag。",
                "5. 查询值：填写属性面板右侧要找的值，例如 M14-101、P-001。",
                "6. 匹配方式：equals 表示完全相同；contains 表示包含这段文字即可。");

            AddGuideSection("示例一：按名称精确查找",
                "属性面板显示：Item / 名称 = M14-101",
                "填写：分类 = Item，属性名 = 名称，匹配方式 = equals，查询值 = M14-101");

            AddGuideSection("示例二：按路径包含查找",
                "属性面板显示：SmartPlant 3D / System Path = Area-01/P-001/Line-A",
                "填写：分类 = SmartPlant 3D，属性名 = System Path，匹配方式 = contains，查询值 = P-001");

            AddGuideSection("常见注意点",
                "- 属性名必须和 Navisworks 属性面板显示一致。",
                "- 查询值前后不要多打空格。",
                "- 分类不确定时先留空，插件会尝试自动识别。",
                "- 导入 XML 后也可以双击表格行修改条件。");

            void UpdateGuideLabelWidth()
            {
                var contentWidth = Math.Max(
                    ScaleLogical(320),
                    guidePanel.ClientSize.Width - guidePanel.Padding.Horizontal - ScaleLogical(24));
                guideContent.Width = contentWidth;
                foreach (var label in guideLabels)
                {
                    label.MaximumSize = new Size(contentWidth, 0);
                }
            }
            guidePanel.Resize += (s, e) => UpdateGuideLabelWidth();

            var buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, ScaleLogical(12), 0, ScaleLogical(4)),
                ColumnCount = 2,
                RowCount = 1,
            };
            var closeButtonWidth = ScaleLogical(104);
            var closeButtonHeight = CalculateButtonHeight(form.Font) + ScaleLogical(8);
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, closeButtonWidth));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, closeButtonHeight));
            var btnClose = new Button
            {
                Text = "关闭",
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Size = new Size(closeButtonWidth, closeButtonHeight),
                DialogResult = DialogResult.OK,
            };
            buttonPanel.Controls.Add(btnClose, 1, 0);

            layout.Controls.Add(guidePanel, 0, 0);
            layout.Controls.Add(buttonPanel, 0, 1);
            form.Controls.Add(layout);
            form.AcceptButton = btnClose;
            form.CancelButton = btnClose;
            form.Shown += (s, e) =>
            {
                UpdateGuideLabelWidth();
                btnClose.Focus();
            };
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
                protectedName = modelPrefix + "-STR";

                diagnosticLog?.LogScopeInfo(scopeRoots, scopeRoots.Count);
                diagnosticLog?.LogModelPrefixInfo(
                    modelPrefixes,
                    modelPrefix,
                    protectedName);

                results = ModelItemMatcher.MatchAll(
                    _doc, scopeRoots, _conditions);

                diagnosticLog?.LogXmlScopeResultStats(
                    results.Count, 0, 0);

                List<ModelItem> matchedItemsInScope =
                    MergeUniqueItems(results.SelectMany(r => r.MatchedItems));
                totalMatched = matchedItemsInScope.Count;
                int matchedConds = results.Count(r => r.MatchCount > 0);
                int unmatchedConds = results.Count - matchedConds;

                ShowResults(results, totalMatched, matchedConds, unmatchedConds);

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
            int matchedConds,
            int unmatchedConds)
        {
            _lblResultSummary.Text =
                $"条件总数：{results.Count}\n" +
                $"找到：{matchedConds}　　未找到：{unmatchedConds}\n" +
                $"总匹配对象：{totalMatched} 个";

            _lstResultDetails.Items.Clear();
            foreach (var r in results)
            {
                string icon = r.MatchCount > 0 ? "[✓]" : "[✗]";
                _lstResultDetails.Items.Add(
                    $"{icon} {r.QueryValue} → 匹配 {r.MatchCount} 个对象");
            }
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

        private static HashSet<ModelItem> ExpandItems(IEnumerable<ModelItem> rootItems)
        {
            var result = new HashSet<ModelItem>();
            var stack = new Stack<ModelItem>();

            foreach (ModelItem item in rootItems ?? Enumerable.Empty<ModelItem>())
            {
                if (item != null)
                    stack.Push(item);
            }

            while (stack.Count > 0)
            {
                ModelItem current = stack.Pop();
                if (!result.Add(current))
                    continue;

                foreach (ModelItem child in current.Children)
                    stack.Push(child);
            }

            return result;
        }

        private static List<SearchResult> FilterResultsToScope(
            List<SearchResult> rawResults,
            HashSet<ModelItem> scopeItems,
            out int rawResultCount,
            out int matchedItemsInScopeCount,
            out int outOfScopeCount)
        {
            rawResultCount = 0;
            matchedItemsInScopeCount = 0;
            outOfScopeCount = 0;

            var filteredResults = new List<SearchResult>();
            foreach (SearchResult result in rawResults ?? new List<SearchResult>())
            {
                var matchedItems = new List<ModelItem>();
                foreach (ModelItem item in result.MatchedItems)
                {
                    rawResultCount++;
                    if (scopeItems.Contains(item))
                    {
                        matchedItems.Add(item);
                        matchedItemsInScopeCount++;
                    }
                    else
                    {
                        outOfScopeCount++;
                    }
                }

                filteredResults.Add(new SearchResult
                {
                    QueryValue = result.QueryValue,
                    MatchCount = matchedItems.Count,
                    MatchedItems = matchedItems,
                });
            }

            return filteredResults;
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

        private static List<ModelItem> GetAllDescendants(ModelItemCollection items)
        {
            var result = new List<ModelItem>();
            var stack = new Stack<ModelItem>();
            foreach (ModelItem item in items) stack.Push(item);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                result.Add(current);
                foreach (ModelItem child in current.Children) stack.Push(child);
            }
            return result;
        }

        #endregion
    }
}
