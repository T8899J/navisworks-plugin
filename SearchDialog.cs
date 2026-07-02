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

        // ── 模块 2：选项 ──
        private CheckBox _chkHideAfterSearch;
        private CheckBox _chkTestMode;

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
            this.Size = new Size(900, 650);
            this.MinimumSize = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Microsoft YaHei UI", 9F);
            this.BackColor = System.Drawing.Color.FromArgb(248, 249, 250);
            this.Icon = null;

            // ── TabControl ──
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 6),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                SizeMode = TabSizeMode.Fixed,
            };
            _tabControl.ItemSize = CalculateTabItemSize(_tabControl.Font);

            // ========== 选项卡 1：搜索条件 ==========
            _tabConditions = new TabPage("搜索条件");
            BuildConditionsTab();

            // ========== 选项卡 2：选项 ==========
            _tabOptions = new TabPage("选项");
            BuildOptionsTab();

            // ========== 选项卡 3：结果 ==========
            _tabResults = new TabPage("结果");
            BuildResultsTab();

            _tabControl.TabPages.Add(_tabConditions);
            _tabControl.TabPages.Add(_tabOptions);
            _tabControl.TabPages.Add(_tabResults);

            // ── 底部按钮面板 ──
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = CalculatePanelHeight(new Font("Microsoft YaHei UI", 9F, FontStyle.Bold), 34),
                Padding = new Padding(ScaleLogical(10)),
                BackColor = System.Drawing.Color.FromArgb(240, 241, 242),
            };

            _btnSearch = new Button
            {
                Text = "执行搜索",
                Size = new Size(
                    ScaleLogical(108),
                    CalculateButtonHeight(new Font("Microsoft YaHei", 10F, FontStyle.Bold)) - ScaleLogical(4)),
                Location = new Point(ScaleLogical(8), ScaleLogical(6)),
                BackColor = System.Drawing.Color.FromArgb(52, 152, 219),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                    BorderSize = 0,
                    MouseOverBackColor = System.Drawing.Color.FromArgb(41, 128, 185),
                },
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold),
                UseVisualStyleBackColor = false,
            };
            _btnSearch.Click += BtnSearch_Click;

            _btnExportResults = new Button
            {
                Text = "导出结果",
                Size = new Size(
                    ScaleLogical(90),
                    CalculateButtonHeight(new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)) - ScaleLogical(4)),
                Location = new Point(ScaleLogical(136), ScaleLogical(6)),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                    BorderColor = System.Drawing.Color.FromArgb(189, 195, 199),
                    BorderSize = 1,
                    MouseOverBackColor = System.Drawing.Color.FromArgb(230, 240, 255),
                },
                BackColor = System.Drawing.Color.FromArgb(248, 249, 250),
                ForeColor = System.Drawing.Color.FromArgb(44, 62, 80),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                UseVisualStyleBackColor = false,
                Enabled = false,
            };
            _btnExportResults.Click += BtnExportResults_Click;

            _btnClose = new Button
            {
                Text = "关闭",
                Size = new Size(
                    ScaleLogical(72),
                    CalculateButtonHeight(new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)) - ScaleLogical(4)),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                    BorderColor = System.Drawing.Color.FromArgb(189, 195, 199),
                    BorderSize = 1,
                    MouseOverBackColor = System.Drawing.Color.FromArgb(230, 240, 255),
                },
                BackColor = System.Drawing.Color.FromArgb(248, 249, 250),
                ForeColor = System.Drawing.Color.FromArgb(44, 62, 80),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel,
            };
            // 关闭按钮右对齐
            _btnClose.Left = bottomPanel.ClientSize.Width - _btnClose.Width - 8;

            _btnExportResults.Left = _btnSearch.Right + 8;

            bottomPanel.Controls.Add(_btnSearch);
            bottomPanel.Controls.Add(_btnExportResults);
            bottomPanel.Controls.Add(_btnClose);

            // 窗口大小变化时重新定位关闭按钮
            bottomPanel.Resize += (s, e) =>
            {
                _btnClose.Left = bottomPanel.ClientSize.Width - _btnClose.Width - 8;
            };

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
                    BorderColor = System.Drawing.Color.FromArgb(189, 195, 199),
                    BorderSize = 1,
                    MouseOverBackColor = System.Drawing.Color.FromArgb(230, 240, 255),
                    MouseDownBackColor = System.Drawing.Color.FromArgb(189, 204, 230),
                },
                BackColor = System.Drawing.Color.FromArgb(248, 249, 250),
                ForeColor = System.Drawing.Color.FromArgb(44, 62, 80),
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

        private static Size CalculateTabItemSize(Font font)
        {
            return new Size(ScaleLogical(96), MeasureTextHeight(font) + ScaleLogical(12));
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
                ColumnCount = 5,
                RowCount = 1,
                Padding = new Padding(ScaleLogical(6)),
            };
            for (int i = 0; i < 5; i++)
                toolStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            _btnImportXml = MakeToolButton("导入", BtnImportXml_Click);
            _btnExportXml = MakeToolButton("导出", BtnExportXml_Click);
            _btnAddCondition = MakeToolButton("添加", BtnAddCondition_Click);
            _btnDeleteCondition = MakeToolButton("删除", BtnDeleteCondition_Click);
            _btnClearConditions = MakeToolButton("清空", (s, e) => { _conditions.Clear(); RefreshConditionsGrid(); });

            toolStrip.Controls.Add(_btnImportXml, 0, 0);
            toolStrip.Controls.Add(_btnExportXml, 1, 0);
            toolStrip.Controls.Add(_btnAddCondition, 2, 0);
            toolStrip.Controls.Add(_btnDeleteCondition, 3, 0);
            toolStrip.Controls.Add(_btnClearConditions, 4, 0);
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
                Padding = new Padding(0),
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
            grid.EnableHeadersVisualStyles = false;
            // 先添加列，再设置 Fill 模式
            grid.Columns.Add("Category", "分类");
            grid.Columns.Add("Property", "属性");
            grid.Columns.Add("Test", "匹配方式");
            grid.Columns.Add("Value", "查询值");
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            // 列标题样式
            grid.ColumnHeadersHeight = 24;
            grid.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.SystemColors.Control;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            ApplyGridHeaderLayout(grid);
            grid.DefaultCellStyle.BackColor = System.Drawing.Color.White;
            grid.DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(44, 62, 80);
            grid.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(230, 240, 255);
            grid.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(44, 62, 80);
            grid.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(250, 251, 252);
            return grid;
        }

        private void BuildOptionsTab()
        {
            _tabOptions.SuspendLayout();

            _chkHideAfterSearch = new CheckBox
            {
                Text = "搜索完成后询问是否隐藏未选中对象",
                Location = new Point(20, 20),
                Size = new Size(350, CalculateContentHeight(this.Font, 1, 6)),
                Checked = true,
            };

            _chkTestMode = new CheckBox
            {
                Text = "测试模式：仅查找并选中，不隐藏（用于验证插件是否正确选中对象）",
                Location = new Point(20, 80),
                Size = new Size(500, CalculateContentHeight(this.Font, 1, 6)),
                Checked = false,
                ForeColor = System.Drawing.Color.FromArgb(192, 57, 43),
            };

            _chkHideAfterSearch.Text = "模式 B：查找并选中后，弹窗确认，再执行隐藏未选中";
            _chkHideAfterSearch.Checked = false;
            _chkTestMode.Text = "模式 A：仅查找并选中，不隐藏";
            _chkTestMode.Checked = true;

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

            var grpScope = new GroupBox
            {
                Text = "搜索范围",
                Location = new Point(20, 120),
                Size = new Size(350, 80),
            };
            var lblScope = new Label
            {
                Text = "搜索范围由选择树当前蓝色选中节点决定，\n执行搜索时直接按该范围处理。",
                Location = new Point(12, 24),
                Size = new Size(320, CalculateContentHeight(this.Font, 2, 8)),
            };
            grpScope.Controls.Add(lblScope);

            var grpFuture = new GroupBox
            {
                Text = "扩展预留",
                Location = new Point(20, 220),
                Size = new Size(500, 120),
            };
            var lblFuture = new Label
            {
                Text = "此区域预留用于后续功能扩展。\n您可以直接在此文件中添加新的选项和配置。",
                Location = new Point(12, 24),
                Size = new Size(460, CalculateContentHeight(this.Font, 2, 12)),
                ForeColor = System.Drawing.Color.Gray,
            };
            grpFuture.Controls.Add(lblFuture);

            _tabOptions.Controls.Add(_chkHideAfterSearch);
            _tabOptions.Controls.Add(_chkTestMode);
            _tabOptions.Controls.Add(grpScope);
            _tabOptions.Controls.Add(grpFuture);
            _tabOptions.ResumeLayout();
        }

        private void BuildResultsTab()
        {
            _tabResults.SuspendLayout();

            _lblResultSummary = new Label
            {
                Dock = DockStyle.Top,
                Height = CalculateContentHeight(this.Font, 3, 20),
                Padding = new Padding(12),
                BackColor = System.Drawing.Color.LightYellow,
                BorderStyle = BorderStyle.FixedSingle,
            };

            var lblDetails = new Label
            {
                Text = "详细匹配结果：",
                Dock = DockStyle.Top,
                Height = CalculateContentHeight(this.Font, 1, 4),
                Padding = new Padding(4, 4, 4, 0),
            };

            _lstResultDetails = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F),
                IntegralHeight = false,
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
                Size = new Size(ScaleLogical(460), ScaleLogical(260)),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
            };

            var lblCat = new Label { Text = "分类（可选）：", Left = 12, Top = 12, Width = 100 };
            var txtCat = new TextBox { Text = category, Left = 120, Top = 10, Width = 310 };

            var lblProp = new Label { Text = "属性名：", Left = 12, Top = 44, Width = 100 };
            var txtProp = new TextBox { Text = property, Left = 120, Top = 42, Width = 310 };

            var lblTest = new Label { Text = "匹配方式：", Left = 12, Top = 76, Width = 100 };
            var cmbTest = new ComboBox
            {
                Left = 120, Top = 74, Width = 310, DropDownStyle = ComboBoxStyle.DropDownList,
            };
            cmbTest.Items.AddRange(new[] { "equals", "contains" });
            cmbTest.SelectedItem = test;

            var lblVal = new Label { Text = "查询值：", Left = 12, Top = 108, Width = 100 };
            var txtVal = new TextBox { Text = value, Left = 120, Top = 106, Width = 310 };

            var btnOk = new Button { Text = "确定", Left = 270, Top = 148, Width = 75, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "取消", Left = 355, Top = 148, Width = 75, DialogResult = DialogResult.Cancel };

            form.Controls.AddRange(new Control[] {
                lblCat, txtCat, lblProp, txtProp,
                lblTest, cmbTest, lblVal, txtVal,
                btnOk, btnCancel,
            });
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
            var diagnosticLog =
                LogService.CreateDiagnosticSession(_doc, _currentXmlPath, _conditions.Count);
            List<SearchResult> results = null;
            int totalMatched = 0;
            bool hideExecuted = false;

            diagnosticLog.LogMode(modeName);
            diagnosticLog.LogHideIntent(!isSelectOnlyMode);

            try
            {
                Cursor = Cursors.WaitCursor;
                _btnSearch.Enabled = false;

                List<ModelItem> scopeRoots = SnapshotCurrentSelection(_doc);
                if (scopeRoots.Count == 0)
                {
                    diagnosticLog.LogScopeInfo(scopeRoots, 0);
                    diagnosticLog.LogModelPrefixInfo(
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
                    diagnosticLog.LogScopeInfo(scopeRoots, 0);
                    diagnosticLog.LogModelPrefixInfo(
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
                    diagnosticLog.LogScopeInfo(scopeRoots, 0);
                    diagnosticLog.LogModelPrefixInfo(
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

                HashSet<ModelItem> scopeItems = ExpandItems(scopeRoots);
                diagnosticLog.LogScopeInfo(scopeRoots, scopeItems.Count);
                diagnosticLog.LogModelPrefixInfo(
                    modelPrefixes,
                    modelPrefix,
                    protectedName);
                if (scopeItems.Count == 0)
                {
                    diagnosticLog.LogDecision("Blocked: scopeItems count is 0.");
                    MessageBox.Show(this,
                        "选定范围为空，已取消执行。",
                        "傑出品·搜索范围",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                List<SearchResult> rawResults = ModelItemMatcher.MatchAll(_doc, _conditions);
                results = FilterResultsToScope(
                    rawResults,
                    scopeItems,
                    out int rawResultCount,
                    out int matchedItemsInScopeCount,
                    out int outOfScopeCount);

                diagnosticLog.LogXmlScopeResultStats(
                    rawResultCount,
                    matchedItemsInScopeCount,
                    outOfScopeCount);

                List<ModelItem> matchedItemsInScope =
                    MergeUniqueItems(results.SelectMany(r => r.MatchedItems));
                totalMatched = matchedItemsInScope.Count;
                int matchedConds = results.Count(r => r.MatchCount > 0);
                int unmatchedConds = results.Count - matchedConds;

                ShowResults(results, totalMatched, matchedConds, unmatchedConds);

                ProtectedKeepResult protectedKeepResult =
                    ProtectedKeepService.FindProtectedItems(_doc, protectedName);
                diagnosticLog.LogProtectedNodeStats(
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
                        diagnosticLog.LogHidePrecheck(0, 0);
                        diagnosticLog.LogHideBlocked();
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
                        diagnosticLog.LogDecision(
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

                    diagnosticLog.LogFinalKeepStats(
                        matchedItemsInScope.Count,
                        protectedKeepResult.ProtectedItems.Count,
                        finalKeepItems.Count,
                        actualSelectionCount,
                        willHide);

                    if (finalKeepItems.Count == 0)
                    {
                        diagnosticLog.LogDecision("Blocked: finalKeepItems count is 0.");
                        diagnosticLog.LogHidePrecheck(0, actualSelectionCount);
                        diagnosticLog.LogHideBlocked();
                        MessageBox.Show(this,
                            "最终保留集合为空，已取消隐藏。",
                            "傑出品·保护",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    if (actualSelectionCount == 0)
                    {
                        diagnosticLog.LogDecision("Blocked: CurrentSelection count is 0.");
                        diagnosticLog.LogHidePrecheck(finalKeepItems.Count, actualSelectionCount);
                        diagnosticLog.LogHideBlocked();
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

                        diagnosticLog.LogHidePrompt(
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
                diagnosticLog.LogException("执行搜索", ex);

                string detail = ex.Message;
                if (ex.InnerException != null)
                    detail += "\n\n详情：" + ex.InnerException.Message;

                MessageBox.Show(this, "操作失败：\n" + detail,
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                diagnosticLog.WriteToFile();
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
