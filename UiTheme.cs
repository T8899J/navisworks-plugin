using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace JiePinPai.Navisworks
{
    /// <summary>按钮语义层级：主操作、次要操作、危险操作、无边框辅助操作。</summary>
    internal enum ButtonKind
    {
        Primary,
        Secondary,
        Danger,
        Ghost,
    }

    /// <summary>
    /// 插件界面的唯一视觉来源：调色板、字体、间距与通用控件样式。
    /// 字体为进程级共享实例，不随窗口释放。
    /// </summary>
    internal static class UiTheme
    {
        private const int BaseDpi = 96;
        private static readonly Lazy<float> DpiScale = new Lazy<float>(() =>
        {
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
                return graphics.DpiX / BaseDpi;
        });

        // ── 中性色 ──
        public static readonly Color Canvas = Color.FromArgb(246, 247, 249);
        public static readonly Color Surface = Color.White;
        public static readonly Color SurfaceMuted = Color.FromArgb(248, 250, 252);
        public static readonly Color HeaderBack = Color.FromArgb(241, 245, 249);
        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Color BorderStrong = Color.FromArgb(203, 213, 225);
        public static readonly Color Text = Color.FromArgb(15, 23, 42);
        public static readonly Color TextBody = Color.FromArgb(51, 65, 85);
        public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
        public static readonly Color TextDisabled = Color.FromArgb(148, 163, 184);

        // ── 强调色 ──
        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
        public static readonly Color AccentPressed = Color.FromArgb(30, 64, 175);
        public static readonly Color AccentSoft = Color.FromArgb(239, 246, 255);
        public static readonly Color Selection = Color.FromArgb(219, 234, 254);

        // ── 危险色 ──
        public static readonly Color DangerText = Color.FromArgb(185, 28, 28);
        public static readonly Color DangerBorder = Color.FromArgb(252, 165, 165);
        public static readonly Color DangerSoft = Color.FromArgb(254, 242, 242);
        public static readonly Color DangerPressed = Color.FromArgb(254, 226, 226);

        // ── 字体 ──
        public static readonly Font BodyFont = new Font("Microsoft YaHei UI", 9F);
        public static readonly Font BodyStrongFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        public static readonly Font TitleFont = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
        public static readonly Font BrandFont = new Font("Segoe UI Semibold", 15F, FontStyle.Regular);
        public static readonly Font KeyFont = new Font("Segoe UI", 8.25F, FontStyle.Regular);
        public static readonly Font CaptionFont = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold);

        public static int Scale(int logicalPixels)
        {
            return (int)Math.Ceiling(logicalPixels * DpiScale.Value);
        }

        public static int TextHeight(Font font)
        {
            return TextRenderer.MeasureText("中文Ag", font).Height;
        }

        /// <summary>所有按钮与单行输入控件共用的高度。</summary>
        public static int ControlHeight
        {
            get { return TextHeight(BodyFont) + Scale(14); }
        }

        public static void GetStatusColors(
            SearchResultStatus status,
            out Color back,
            out Color fore)
        {
            switch (status)
            {
                case SearchResultStatus.Found:
                    back = Color.FromArgb(220, 252, 231);
                    fore = Color.FromArgb(22, 101, 52);
                    break;
                case SearchResultStatus.NotFound:
                    back = Color.FromArgb(254, 226, 226);
                    fore = Color.FromArgb(153, 27, 27);
                    break;
                case SearchResultStatus.Duplicate:
                    back = Color.FromArgb(255, 237, 213);
                    fore = Color.FromArgb(154, 52, 18);
                    break;
                case SearchResultStatus.ConditionInvalid:
                    back = Color.FromArgb(254, 243, 199);
                    fore = Color.FromArgb(133, 77, 14);
                    break;
                default:
                    back = HeaderBack;
                    fore = TextBody;
                    break;
            }
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Border;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.RowHeadersVisible = false;
            grid.AllowUserToResizeRows = false;
            grid.Font = BodyFont;

            var cellPadding = new Padding(Scale(8), 0, Scale(8), 0);
            DataGridViewCellStyle header = grid.ColumnHeadersDefaultCellStyle;
            header.BackColor = HeaderBack;
            header.ForeColor = TextMuted;
            header.SelectionBackColor = HeaderBack;
            header.SelectionForeColor = TextMuted;
            header.Font = BodyStrongFont;
            header.Alignment = DataGridViewContentAlignment.MiddleLeft;
            header.Padding = cellPadding;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = TextHeight(BodyStrongFont) + Scale(14);

            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = TextBody;
            grid.DefaultCellStyle.SelectionBackColor = Selection;
            grid.DefaultCellStyle.SelectionForeColor = Text;
            grid.DefaultCellStyle.Padding = cellPadding;
            grid.AlternatingRowsDefaultCellStyle.BackColor = SurfaceMuted;
            grid.RowTemplate.Height = TextHeight(BodyFont) + Scale(14);

            AttachRowHover(grid);

            // 自绘表头与状态标签时避免闪烁。
            typeof(DataGridView)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(grid, true, null);
        }

        /// <summary>鼠标悬停的行使用浅色底，选中行不受影响。</summary>
        private static void AttachRowHover(DataGridView grid)
        {
            int hoverRow = -1;
            Color hoverBack = Color.FromArgb(240, 244, 249);
            void SetHover(int row)
            {
                if (row == hoverRow)
                    return;
                int old = hoverRow;
                hoverRow = row;
                if (old >= 0 && old < grid.RowCount)
                    grid.InvalidateRow(old);
                if (row >= 0 && row < grid.RowCount)
                    grid.InvalidateRow(row);
            }
            grid.CellMouseEnter += (s, e) => SetHover(e.RowIndex);
            grid.MouseLeave += (s, e) => SetHover(-1);
            grid.RowsRemoved += (s, e) => hoverRow = -1;
            grid.CellPainting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex == hoverRow
                    && e.RowIndex < grid.RowCount && !grid.Rows[e.RowIndex].Selected)
                    e.CellStyle.BackColor = hoverBack;
            };
        }

        /// <summary>空状态插图样式。</summary>
        public enum EmptyIcon
        {
            None,
            Document,
            Search,
        }

        /// <summary>表格为空时居中绘制线稿插图与引导文字（首行加粗作标题）。</summary>
        public static void AttachEmptyState(DataGridView grid, Func<string> message, EmptyIcon icon = EmptyIcon.None)
        {
            grid.Paint += (sender, e) =>
            {
                if (grid.Rows.Count > 0)
                    return;
                string text = message();
                if (string.IsNullOrEmpty(text))
                    return;

                int top = grid.ColumnHeadersVisible ? grid.ColumnHeadersHeight : 0;
                int width = grid.ClientSize.Width;
                int height = grid.ClientSize.Height - top;

                string title = text;
                string body = null;
                int split = text.IndexOf("\n\n", StringComparison.Ordinal);
                if (split > 0)
                {
                    title = text.Substring(0, split);
                    body = text.Substring(split + 2);
                }

                var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
                int maxWidth = Math.Max(Scale(200), Math.Min(width - Scale(40), Scale(520)));
                Font titleFont = body != null ? BodyStrongFont : BodyFont;
                Size titleSize = TextRenderer.MeasureText(e.Graphics, title, titleFont, new Size(maxWidth, 0), flags);
                Size bodySize = body == null ? Size.Empty
                    : TextRenderer.MeasureText(e.Graphics, body, BodyFont, new Size(maxWidth, 0), flags);
                int iconSize = icon == EmptyIcon.None ? 0 : Scale(44);
                int gap = Scale(14);
                int total = iconSize + (iconSize > 0 ? gap : 0) + titleSize.Height
                    + (body != null ? Scale(6) + bodySize.Height : 0);
                int y = top + Math.Max(Scale(12), (height - total) / 2);

                if (iconSize > 0)
                {
                    DrawEmptyIcon(e.Graphics, icon, new Rectangle((width - iconSize) / 2, y, iconSize, iconSize));
                    y += iconSize + gap;
                }
                TextRenderer.DrawText(e.Graphics, title, titleFont,
                    new Rectangle((width - maxWidth) / 2, y, maxWidth, titleSize.Height),
                    body != null ? TextBody : TextMuted, flags);
                if (body != null)
                {
                    y += titleSize.Height + Scale(6);
                    TextRenderer.DrawText(e.Graphics, body, BodyFont,
                        new Rectangle((width - maxWidth) / 2, y, maxWidth, bodySize.Height), TextMuted, flags);
                }
            };
            grid.Resize += (sender, e) => grid.Invalidate();
        }

        private static void DrawEmptyIcon(Graphics g, EmptyIcon icon, Rectangle box)
        {
            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // 浅色圆形底 + 线稿图标。
            using (var halo = new SolidBrush(HeaderBack))
                g.FillEllipse(halo, box);
            using (var pen = new Pen(TextDisabled, Math.Max(1.5f, Scale(2) * 0.8f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            })
            {
                float cx = box.X + box.Width / 2f;
                float cy = box.Y + box.Height / 2f;
                float u = box.Width / 44f;
                if (icon == EmptyIcon.Document)
                {
                    var page = new RectangleF(cx - 8 * u, cy - 11 * u, 16 * u, 22 * u);
                    g.DrawRectangle(pen, page.X, page.Y, page.Width, page.Height);
                    for (int i = 0; i < 3; i++)
                    {
                        float ly = page.Y + (6 + i * 5) * u;
                        g.DrawLine(pen, page.X + 4 * u, ly, page.Right - (i == 2 ? 7 : 4) * u, ly);
                    }
                }
                else
                {
                    float r = 7 * u;
                    g.DrawEllipse(pen, cx - r - 2 * u, cy - r - 2 * u, r * 2, r * 2);
                    g.DrawLine(pen, cx + 3 * u, cy + 3 * u, cx + 9 * u, cy + 9 * u);
                }
            }
            g.SmoothingMode = previous;
        }

        /// <summary>
        /// 在宿主上围绕子控件画 1px 浅色边框，替代系统 FixedSingle 的深色硬边。
        /// 子控件需保留至少 1px 外边距。
        /// </summary>
        public static void AttachHairlineBorder(Control host, Control child)
        {
            host.Paint += (sender, e) =>
            {
                if (!child.Visible)
                    return;
                Rectangle bounds = child.Bounds;
                bounds.Inflate(1, 1);
                using (var pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            };
            child.SizeChanged += (sender, e) => host.Invalidate();
            child.LocationChanged += (sender, e) => host.Invalidate();
        }

        /// <summary>沿控件顶边或底边画 1px 分隔线。</summary>
        public static void AttachEdgeLine(Control control, bool top)
        {
            control.Paint += (sender, e) =>
            {
                int y = top ? 0 : control.Height - 1;
                using (var pen = new Pen(Border))
                    e.Graphics.DrawLine(pen, 0, y, control.Width, y);
            };
            control.Resize += (sender, e) => control.Invalidate();
        }

        /// <summary>按钮组之间的竖向分隔线。</summary>
        public static Control CreateDivider()
        {
            return new Panel
            {
                Width = 1,
                Height = ControlHeight - Scale(12),
                BackColor = BorderStrong,
                Margin = new Padding(Scale(4), Scale(6), Scale(12), Scale(6)),
            };
        }

        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>为输入框设置灰色占位提示（Win32 cue banner）。</summary>
        public static void SetPlaceholder(TextBox textBox, string text)
        {
            void Apply() => SendMessage(textBox.Handle, EM_SETCUEBANNER, IntPtr.Zero, text);
            if (textBox.IsHandleCreated)
                Apply();
            else
                textBox.HandleCreated += (sender, e) => Apply();
        }

        public static void FillRoundedRectangle(Graphics graphics, Color color, Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            using (var path = new GraphicsPath())
            using (var brush = new SolidBrush(color))
            {
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                SmoothingMode previous = graphics.SmoothingMode;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillPath(brush, path);
                graphics.SmoothingMode = previous;
            }
        }
    }

    /// <summary>
    /// 全局动画时钟：所有控件共用一个 60fps 计时器，步进函数返回 false 即移除。
    /// 没有进行中的动画时计时器停止，不占 CPU。
    /// </summary>
    internal static class UiAnimator
    {
        private static readonly List<Func<bool>> Steps = new List<Func<bool>>();
        private static Timer _timer;

        public static void Run(Func<bool> step)
        {
            if (Steps.Contains(step))
                return;
            Steps.Add(step);
            if (_timer == null)
            {
                _timer = new Timer { Interval = 15 };
                _timer.Tick += (s, e) => Tick();
            }
            _timer.Start();
        }

        private static void Tick()
        {
            foreach (Func<bool> step in Steps.ToArray())
            {
                bool keep;
                try
                {
                    keep = step();
                }
                catch (ObjectDisposedException)
                {
                    keep = false;
                }
                if (!keep)
                    Steps.Remove(step);
            }
            if (Steps.Count == 0)
                _timer.Stop();
        }

        public static float EaseOut(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            float inv = 1 - t;
            return 1 - inv * inv * inv;
        }

        public static Color Lerp(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        /// <summary>颜色按比例逼近目标；足够接近时直接到位并返回 true。</summary>
        public static bool Approach(ref Color current, Color target, float rate)
        {
            Color next = Lerp(current, target, rate);
            bool close = Math.Abs(next.R - target.R) <= 2 && Math.Abs(next.G - target.G) <= 2
                && Math.Abs(next.B - target.B) <= 2 && Math.Abs(next.A - target.A) <= 2;
            current = close ? target : next;
            return close;
        }

        public static bool Approach(ref float current, float target, float rate)
        {
            float next = current + (target - current) * rate;
            bool close = Math.Abs(target - next) < 0.02f;
            current = close ? target : next;
            return close;
        }
    }

    /// <summary>
    /// 自绘圆角按钮：按语义层级着色，悬停、按下、禁用、激活之间颜色平滑过渡；
    /// 键盘聚焦时显示强调色焦点环。<see cref="Active"/> 用于分段筛选与展开态。
    /// </summary>
    internal sealed class ThemedButton : Button
    {
        private ButtonKind _kind;
        private bool _active;
        private bool _hover;
        private bool _pressed;
        private bool _animating;
        private Color _back;
        private Color _fore;
        private Color _border;
        private readonly Func<bool> _step;

        public ThemedButton(ButtonKind kind)
        {
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.SupportsTransparentBackColor,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            TextAlign = ContentAlignment.MiddleCenter;
            Padding = new Padding(UiTheme.Scale(10), 0, UiTheme.Scale(10), 0);
            MinimumSize = new Size(UiTheme.Scale(72), UiTheme.ControlHeight);
            Margin = new Padding(0, 0, UiTheme.Scale(8), 0);
            Cursor = Cursors.Hand;
            _kind = kind;
            Font = kind == ButtonKind.Primary ? UiTheme.BodyStrongFont : UiTheme.BodyFont;
            _step = Step;
            GetTarget(out _back, out _fore, out _border);
        }

        public ButtonKind Kind
        {
            get { return _kind; }
            set { _kind = value; Animate(); }
        }

        public bool Active
        {
            get { return _active; }
            set
            {
                if (_active == value)
                    return;
                _active = value;
                Animate();
            }
        }

        private void GetTarget(out Color back, out Color fore, out Color border)
        {
            if (!Enabled)
            {
                back = _kind == ButtonKind.Ghost ? Color.Transparent : UiTheme.SurfaceMuted;
                fore = UiTheme.TextDisabled;
                border = _kind == ButtonKind.Ghost ? Color.Transparent : UiTheme.Border;
                return;
            }

            switch (_kind)
            {
                case ButtonKind.Primary:
                    back = _pressed ? UiTheme.AccentPressed : _hover ? UiTheme.AccentHover : UiTheme.Accent;
                    fore = Color.White;
                    border = back;
                    break;
                case ButtonKind.Danger:
                    back = _pressed ? UiTheme.DangerPressed : _hover ? UiTheme.DangerSoft : UiTheme.Surface;
                    fore = UiTheme.DangerText;
                    border = _hover ? UiTheme.DangerText : UiTheme.DangerBorder;
                    break;
                case ButtonKind.Ghost:
                    back = _pressed ? UiTheme.Border : _hover ? UiTheme.HeaderBack : Color.Transparent;
                    fore = _hover ? UiTheme.Text : UiTheme.TextBody;
                    border = Color.Transparent;
                    break;
                default:
                    if (_active)
                    {
                        back = _pressed ? UiTheme.Selection : _hover ? Color.FromArgb(226, 236, 254) : UiTheme.AccentSoft;
                        fore = UiTheme.Accent;
                        border = UiTheme.Accent;
                    }
                    else
                    {
                        back = _pressed ? UiTheme.Border : _hover ? UiTheme.HeaderBack : UiTheme.Surface;
                        fore = _hover ? UiTheme.Text : UiTheme.TextBody;
                        border = _hover ? UiTheme.TextDisabled : UiTheme.BorderStrong;
                    }
                    break;
            }
        }

        private void Animate()
        {
            if (!IsHandleCreated || !Visible)
            {
                GetTarget(out _back, out _fore, out _border);
                Invalidate();
                return;
            }
            if (_animating)
                return;
            _animating = true;
            UiAnimator.Run(_step);
        }

        private bool Step()
        {
            if (IsDisposed)
                return _animating = false;
            GetTarget(out Color back, out Color fore, out Color border);
            // 按下反馈更快，其余过渡柔和。
            float rate = _pressed ? 0.55f : 0.28f;
            bool done = UiAnimator.Approach(ref _back, back, rate)
                & UiAnimator.Approach(ref _fore, fore, rate)
                & UiAnimator.Approach(ref _border, border, rate);
            Invalidate();
            if (done)
                _animating = false;
            return !done;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Animate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _pressed = false;
            Animate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (mevent.Button == MouseButtons.Left)
            {
                _pressed = true;
                Animate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _pressed = false;
            Animate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled)
                _hover = _pressed = false;
            Animate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        private Color ResolveParentBack()
        {
            Control parent = Parent;
            while (parent != null && parent.BackColor.A < 255)
                parent = parent.Parent;
            return parent?.BackColor ?? UiTheme.Surface;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // 背景在 OnPaint 中一次画完，避免闪烁。
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            Color parentBack = ResolveParentBack();
            g.Clear(parentBack);
            int radius = UiTheme.Scale(4);
            var outer = new Rectangle(0, 0, Width - 1, Height - 1);
            Color back = _back.A < 255 ? UiAnimator.Lerp(parentBack, Color.FromArgb(255, _back), _back.A / 255f) : _back;
            if (_border.A > 0 && _border.ToArgb() != back.ToArgb())
            {
                Color border = _border.A < 255 ? UiAnimator.Lerp(parentBack, Color.FromArgb(255, _border), _border.A / 255f) : _border;
                UiTheme.FillRoundedRectangle(g, border, outer, radius);
                UiTheme.FillRoundedRectangle(g, back, Rectangle.Inflate(outer, -1, -1), Math.Max(1, radius - 1));
            }
            else if (back.ToArgb() != parentBack.ToArgb())
            {
                UiTheme.FillRoundedRectangle(g, back, outer, radius);
            }

            if (Focused && ShowFocusCues)
            {
                SmoothingMode previous = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(140, UiTheme.Accent), UiTheme.Scale(2)))
                    g.DrawRectangle(pen, 1.5f, 1.5f, Width - 4f, Height - 4f);
                g.SmoothingMode = previous;
            }

            Rectangle textBounds = new Rectangle(
                Padding.Left, 0, Math.Max(0, Width - Padding.Horizontal), Height);
            TextRenderer.DrawText(g, Text, Font, textBounds, _fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.HidePrefix);
        }
    }

    /// <summary>页头导航页签：文字颜色随悬停与选中平滑过渡；下划线由 <see cref="NavIndicator"/> 统一绘制。</summary>
    internal sealed class NavTab : Control
    {
        private bool _active;
        private bool _hover;
        private bool _animating;
        private Color _fore;
        private readonly Func<bool> _step;

        public NavTab()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw,
                true);
            Font = UiTheme.BodyStrongFont;
            BackColor = UiTheme.Surface;
            Cursor = Cursors.Hand;
            Margin = new Padding(0, 0, UiTheme.Scale(24), 0);
            _step = Step;
            _fore = TargetFore();
        }

        public bool Active
        {
            get { return _active; }
            set
            {
                if (_active == value)
                    return;
                _active = value;
                Animate();
            }
        }

        private Color TargetFore()
        {
            return !Enabled ? UiTheme.TextDisabled
                : _active ? UiTheme.Accent
                : _hover ? UiTheme.Text
                : UiTheme.TextMuted;
        }

        private void Animate()
        {
            if (!IsHandleCreated)
            {
                _fore = TargetFore();
                Invalidate();
                return;
            }
            if (_animating)
                return;
            _animating = true;
            UiAnimator.Run(_step);
        }

        private bool Step()
        {
            if (IsDisposed)
                return _animating = false;
            bool done = UiAnimator.Approach(ref _fore, TargetFore(), 0.25f);
            Invalidate();
            if (done)
                _animating = false;
            return !done;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Size textSize = TextRenderer.MeasureText(Text, Font);
            Size = new Size(textSize.Width + UiTheme.Scale(4), textSize.Height + UiTheme.Scale(18));
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Animate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Animate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Animate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            int barHeight = UiTheme.Scale(3);
            TextRenderer.DrawText(e.Graphics, Text, Font,
                new Rectangle(0, 0, Width, Height - barHeight), _fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>
    /// 导航下划线：在页签之间平滑滑动并伸缩宽度（220ms，先快后慢）。
    /// 作为页头的直接子控件，覆盖在页签底部。
    /// </summary>
    internal sealed class NavIndicator : Control
    {
        private const float DurationMs = 220f;
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private Rectangle _from;
        private Rectangle _to;
        private bool _animating;
        private bool _placed;
        private readonly Func<bool> _step;

        public NavIndicator()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = UiTheme.Accent;
            Height = UiTheme.Scale(3);
            _step = Step;
        }

        /// <summary>移动到目标页签下方；首次定位直接到位，之后动画滑动。</summary>
        public void MoveTo(Rectangle target, bool animate)
        {
            if (target == _to && _placed)
                return;
            if (!_placed || !animate)
            {
                _placed = true;
                _from = _to = target;
                Bounds = target;
                return;
            }
            _from = Bounds;
            _to = target;
            _clock.Restart();
            if (_animating)
                return;
            _animating = true;
            UiAnimator.Run(_step);
        }

        private bool Step()
        {
            if (IsDisposed)
                return _animating = false;
            float t = UiAnimator.EaseOut((float)_clock.Elapsed.TotalMilliseconds / DurationMs);
            Bounds = new Rectangle(
                (int)Math.Round(_from.X + (_to.X - _from.X) * t),
                _to.Y,
                (int)Math.Round(_from.Width + (_to.Width - _from.Width) * t),
                _to.Height);
            if (t >= 1f)
            {
                _animating = false;
                return false;
            }
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Enabled ? UiTheme.Accent : UiTheme.BorderStrong);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }
    }

    /// <summary>
    /// 页面切换过渡：覆盖在内容区上，旧页面淡出、新页面淡入并轻微上移（180ms），结束后自动移除。
    /// </summary>
    internal sealed class PageTransition : Control
    {
        private const float DurationMs = 180f;
        private readonly Bitmap _from;
        private readonly Bitmap _to;
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private float _t;

        private PageTransition(Bitmap from, Bitmap to)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _from = from;
            _to = to;
        }

        /// <summary>截取宿主当前画面；宿主不可见或尺寸为零时返回 null。</summary>
        public static Bitmap Snapshot(Control host)
        {
            if (host == null || !host.IsHandleCreated || !host.Visible || host.Width <= 0 || host.Height <= 0)
                return null;
            var bitmap = new Bitmap(host.Width, host.Height);
            host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, host.Size));
            return bitmap;
        }

        public static PageTransition Play(Control host, Bitmap from, Bitmap to)
        {
            if (from == null || to == null || host.Parent == null)
            {
                from?.Dispose();
                to?.Dispose();
                return null;
            }
            var overlay = new PageTransition(from, to) { Bounds = host.Bounds };
            host.Parent.Controls.Add(overlay);
            overlay.BringToFront();
            overlay._clock.Start();
            UiAnimator.Run(overlay.Step);
            return overlay;
        }

        private bool Step()
        {
            if (IsDisposed)
                return false;
            _t = UiAnimator.EaseOut((float)_clock.Elapsed.TotalMilliseconds / DurationMs);
            if (_t >= 1f)
            {
                Finish();
                return false;
            }
            Invalidate();
            return true;
        }

        /// <summary>立即结束（如连续切换时），移除覆盖层并释放截图。</summary>
        public void Finish()
        {
            if (IsDisposed)
                return;
            Parent?.Controls.Remove(this);
            Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawImageUnscaled(_from, 0, 0);
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = _t };
                attributes.SetColorMatrix(matrix);
                int offset = (int)Math.Round(UiTheme.Scale(8) * (1 - _t));
                g.DrawImage(_to,
                    new Rectangle(0, offset, _to.Width, _to.Height),
                    0, 0, _to.Width, _to.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _from.Dispose();
                _to.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 浮动提示：圆角深色小窗，跟随宿主窗口底部居中；淡入上浮、停留后淡出。
    /// 不抢焦点、鼠标可穿透，不打断用户操作。
    /// </summary>
    internal sealed class ToastWindow : Form
    {
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;
        private const float InMs = 160f;
        private const float HoldMs = 3200f;
        private const float OutMs = 240f;

        private readonly Form _host;
        private readonly int _bottomInset;
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private bool _animating;
        private float _skipInMs;
        private readonly Func<bool> _step;

        public ToastWindow(Form host, int bottomInset)
        {
            _host = host;
            _bottomInset = bottomInset;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = UiTheme.Text;
            ForeColor = Color.White;
            Font = UiTheme.BodyFont;
            DoubleBuffered = true;
            Opacity = 0;
            _step = Step;
            host.LocationChanged += (s, e) => Reposition(0);
            host.SizeChanged += (s, e) => Reposition(0);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
                return cp;
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int corner = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            }
            catch (DllNotFoundException)
            {
                // 旧系统保持直角。
            }
        }

        public void ShowMessage(string message)
        {
            if (_host.IsDisposed || !_host.Visible || _host.WindowState == FormWindowState.Minimized)
                return;
            Text = message;
            Size text = TextRenderer.MeasureText(message, Font);
            ClientSize = new Size(text.Width + UiTheme.Scale(32), text.Height + UiTheme.Scale(18));
            Reposition(UiTheme.Scale(8));
            if (!Visible)
                Show(_host);
            // 已在显示时只刷新文字并重新计时停留，不重复淡入。
            _skipInMs = Opacity > 0.9 ? InMs : 0f;
            _clock.Restart();
            Invalidate();
            if (_animating)
                return;
            _animating = true;
            UiAnimator.Run(_step);
        }

        private void Reposition(int rise)
        {
            if (_host.IsDisposed)
                return;
            Point origin = _host.PointToScreen(Point.Empty);
            Left = origin.X + (_host.ClientSize.Width - Width) / 2;
            Top = origin.Y + _host.ClientSize.Height - _bottomInset - Height + rise;
        }

        private bool Step()
        {
            if (IsDisposed || _host.IsDisposed)
                return _animating = false;
            float ms = (float)_clock.Elapsed.TotalMilliseconds + _skipInMs;
            if (ms < InMs)
            {
                float t = UiAnimator.EaseOut(ms / InMs);
                Opacity = Math.Max(Opacity, t * 0.96);
                Reposition((int)Math.Round(UiTheme.Scale(8) * (1 - t)));
            }
            else if (ms < InMs + HoldMs)
            {
                Opacity = 0.96;
                Reposition(0);
            }
            else if (ms < InMs + HoldMs + OutMs)
            {
                float t = (ms - InMs - HoldMs) / OutMs;
                Opacity = 0.96 * (1 - t);
            }
            else
            {
                Opacity = 0;
                Hide();
                _animating = false;
                return false;
            }
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>
    /// 模态弹窗骨架：可选强调色标题栏、白色内容区、底部灰色按钮栏（按钮右对齐）。
    /// </summary>
    internal sealed class ThemedDialog : Form
    {
        private readonly FlowLayoutPanel _buttons;

        public ThemedDialog(string title, Size clientSize)
        {
            Text = title;
            ClientSize = clientSize;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            Font = UiTheme.BodyFont;
            BackColor = UiTheme.Surface;
            ForeColor = UiTheme.TextBody;

            Content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = new Padding(UiTheme.Scale(24), UiTheme.Scale(20), UiTheme.Scale(24), UiTheme.Scale(16)),
            };

            _buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = UiTheme.Canvas,
                Padding = new Padding(0),
                Margin = new Padding(0),
            };
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                BackColor = UiTheme.Canvas,
                Padding = new Padding(UiTheme.Scale(24), UiTheme.Scale(12), UiTheme.Scale(24), UiTheme.Scale(12)),
                Height = UiTheme.ControlHeight + UiTheme.Scale(24),
            };
            footer.Controls.Add(_buttons);
            Controls.Add(Content);
            // Dock 按集合逆序计算：后加入者先占位，分隔线位于按钮栏之上。
            Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = UiTheme.Border });
            Controls.Add(footer);
        }

        /// <summary>内容区，调用方向其中添加控件。</summary>
        public Panel Content { get; }

        private bool _fadeIn;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // 打开时 140ms 淡入；调用方已自定义透明度时不干预。
            if (Opacity >= 1)
            {
                _fadeIn = true;
                Opacity = 0;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!_fadeIn)
                return;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            UiAnimator.Run(() =>
            {
                if (IsDisposed)
                    return false;
                float t = UiAnimator.EaseOut((float)clock.Elapsed.TotalMilliseconds / 140f);
                Opacity = t;
                return t < 1f;
            });
        }

        /// <summary>在内容区顶部加标题与可选说明；danger 为 true 时显示红色标题栏。</summary>
        public void SetHeading(string heading, string description, bool danger = false)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = danger ? UiTheme.DangerSoft : UiTheme.Surface,
                Padding = danger
                    ? new Padding(UiTheme.Scale(24), UiTheme.Scale(16), UiTheme.Scale(24), UiTheme.Scale(16))
                    : new Padding(UiTheme.Scale(24), UiTheme.Scale(20), UiTheme.Scale(24), 0),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.Controls.Add(new Label
            {
                Text = heading,
                Font = UiTheme.TitleFont,
                ForeColor = danger ? UiTheme.DangerText : UiTheme.Text,
                AutoSize = true,
                Margin = new Padding(0),
            });
            if (!string.IsNullOrEmpty(description))
            {
                layout.Controls.Add(new Label
                {
                    Text = description,
                    AutoSize = true,
                    MaximumSize = new Size(ClientSize.Width - UiTheme.Scale(48), 0),
                    ForeColor = UiTheme.TextMuted,
                    Margin = new Padding(0, UiTheme.Scale(4), 0, 0),
                });
            }
            // Dock 按集合逆序计算：分隔线先加入、标题后加入，标题才会在分隔线之上。
            if (danger)
                Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UiTheme.DangerBorder });
            Controls.Add(layout);
        }

        /// <summary>按从右到左的顺序添加按钮：先加入的最靠右。</summary>
        public ThemedButton AddButton(string text, ButtonKind kind, DialogResult result)
        {
            var button = new ThemedButton(kind)
            {
                Text = text,
                DialogResult = result,
                MinimumSize = new Size(UiTheme.Scale(96), UiTheme.ControlHeight),
                Margin = new Padding(0, 0, _buttons.Controls.Count == 0 ? 0 : UiTheme.Scale(8), 0),
            };
            _buttons.Controls.Add(button);
            return button;
        }
    }

    /// <summary>
    /// 菜单统一样式：白底细边框、圆角浅蓝悬停、强调色对勾、右侧灰色说明文字。
    /// </summary>
    internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        public ThemedMenuRenderer() : base(new ThemedMenuColors())
        {
            RoundedEdges = false;
        }

        /// <summary>应用到菜单，并统一字体与行高。</summary>
        public static void Apply(ContextMenuStrip menu)
        {
            menu.Renderer = new ThemedMenuRenderer();
            menu.Font = UiTheme.BodyFont;
            menu.Padding = new Padding(UiTheme.Scale(4));
            menu.BackColor = UiTheme.Surface;
            menu.ItemAdded += (s, e) => StyleItem(e.Item);
            foreach (ToolStripItem item in menu.Items)
                StyleItem(item);
        }

        private static void StyleItem(ToolStripItem item)
        {
            item.ForeColor = UiTheme.Text;
            if (item is ToolStripMenuItem)
            {
                item.Padding = new Padding(UiTheme.Scale(4), UiTheme.Scale(5), UiTheme.Scale(4), UiTheme.Scale(5));
                item.AutoSize = true;
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
                return;
            var bounds = new Rectangle(UiTheme.Scale(2), 0, e.Item.Width - UiTheme.Scale(4), e.Item.Height);
            UiTheme.FillRoundedRectangle(e.Graphics, UiTheme.AccentSoft, bounds, UiTheme.Scale(4));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            bool secondary = e.Item is ToolStripMenuItem && IsShortcutText(e);
            e.TextColor = !e.Item.Enabled ? UiTheme.TextDisabled
                : secondary ? UiTheme.TextMuted
                : e.Item.Selected ? UiTheme.Accent
                : UiTheme.Text;
            if (secondary)
            {
                // 系统默认把快捷键文字居中在右侧栏里；改为右对齐，列边缘整齐。
                Rectangle r = e.TextRectangle;
                int right = e.Item.Width - UiTheme.Scale(10);
                e.TextRectangle = new Rectangle(r.Left, r.Top, Math.Max(r.Width, right - r.Left), r.Height);
                e.TextFormat = (e.TextFormat & ~TextFormatFlags.HorizontalCenter) | TextFormatFlags.Right;
            }
            base.OnRenderItemText(e);
        }

        // 右侧的快捷键/说明文字用灰色，与主文字区分层级。
        private static bool IsShortcutText(ToolStripItemTextRenderEventArgs e)
        {
            var item = (ToolStripMenuItem)e.Item;
            return !string.IsNullOrEmpty(item.ShortcutKeyDisplayString)
                && e.Text == item.ShortcutKeyDisplayString;
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // 只画强调色对勾，不要系统的蓝色方框。
            Rectangle r = e.ImageRectangle;
            int size = Math.Min(r.Width, r.Height);
            int x = r.Left + (r.Width - size) / 2;
            int y = r.Top + (r.Height - size) / 2;
            var points = new[]
            {
                new PointF(x + size * 0.22f, y + size * 0.52f),
                new PointF(x + size * 0.42f, y + size * 0.72f),
                new PointF(x + size * 0.78f, y + size * 0.30f),
            };
            SmoothingMode previous = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(UiTheme.Accent, UiTheme.Scale(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                e.Graphics.DrawLines(pen, points);
            e.Graphics.SmoothingMode = previous;
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(UiTheme.Border))
                e.Graphics.DrawLine(pen, UiTheme.Scale(8), y, e.Item.Width - UiTheme.Scale(8), y);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // 对勾栏与菜单同底色，不画灰色竖条。
        }

        private sealed class ThemedMenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => UiTheme.Surface;
            public override Color MenuBorder => UiTheme.BorderStrong;
            public override Color MenuItemBorder => Color.Transparent;
            public override Color ImageMarginGradientBegin => UiTheme.Surface;
            public override Color ImageMarginGradientMiddle => UiTheme.Surface;
            public override Color ImageMarginGradientEnd => UiTheme.Surface;
        }
    }

    /// <summary>
    /// 搜索框：细边框容器 + 放大镜 + 无边框输入 + 清除按钮；聚焦时边框变为强调色。
    /// 输入防抖 150ms 后触发 <see cref="SearchTextChanged"/>，避免大结果集逐字重建。
    /// </summary>
    internal sealed class SearchBox : Control
    {
        private readonly TextBox _input;
        private readonly Label _clear;
        private readonly Timer _debounce;
        private bool _focused;
        private float _focusT;
        private bool _animating;

        public event EventHandler SearchTextChanged;
        public event EventHandler EnterPressed;
        public event EventHandler DownPressed;

        public SearchBox(string placeholder)
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw,
                true);
            BackColor = UiTheme.Surface;
            Height = UiTheme.ControlHeight;
            Cursor = Cursors.IBeam;

            _input = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.Text,
                Font = UiTheme.BodyFont,
            };
            UiTheme.SetPlaceholder(_input, placeholder);
            _clear = new Label
            {
                Text = "✕",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = UiTheme.TextMuted,
                BackColor = UiTheme.Surface,
                Cursor = Cursors.Hand,
                Visible = false,
            };
            Controls.Add(_input);
            Controls.Add(_clear);

            _debounce = new Timer { Interval = 150 };
            _debounce.Tick += (s, e) =>
            {
                _debounce.Stop();
                SearchTextChanged?.Invoke(this, EventArgs.Empty);
            };
            _input.TextChanged += (s, e) =>
            {
                _clear.Visible = _input.TextLength > 0;
                _debounce.Stop();
                _debounce.Start();
            };
            _input.GotFocus += (s, e) => { _focused = true; AnimateFocus(); };
            _input.LostFocus += (s, e) => { _focused = false; AnimateFocus(); };
            _input.KeyDown += Input_KeyDown;
            _clear.Click += (s, e) => { Clear(); _input.Focus(); };
            _clear.MouseEnter += (s, e) => _clear.ForeColor = UiTheme.Text;
            _clear.MouseLeave += (s, e) => _clear.ForeColor = UiTheme.TextMuted;
        }

        private void AnimateFocus()
        {
            if (_animating)
                return;
            _animating = true;
            UiAnimator.Run(() =>
            {
                if (IsDisposed)
                    return _animating = false;
                bool done = UiAnimator.Approach(ref _focusT, _focused ? 1f : 0f, 0.3f);
                Invalidate();
                if (done)
                    _animating = false;
                return !done;
            });
        }

        public string SearchText
        {
            get { return _input.Text.Trim(); }
        }

        public void Clear()
        {
            if (_input.TextLength == 0)
                return;
            _input.Clear();
            // 清空立即生效，不等防抖。
            _debounce.Stop();
            SearchTextChanged?.Invoke(this, EventArgs.Empty);
        }

        public void FocusAndSelectAll()
        {
            _input.Focus();
            _input.SelectAll();
        }

        /// <summary>从外部（如结果列表）转入的键入字符：追加到末尾并聚焦。</summary>
        public void AppendAndFocus(char c)
        {
            _input.Focus();
            _input.SelectionStart = _input.TextLength;
            _input.SelectedText = c.ToString();
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    // 先应用尚未触发的防抖过滤，再执行定位。
                    if (_debounce.Enabled)
                    {
                        _debounce.Stop();
                        SearchTextChanged?.Invoke(this, EventArgs.Empty);
                    }
                    EnterPressed?.Invoke(this, EventArgs.Empty);
                    break;
                case Keys.Escape when _input.TextLength > 0:
                    Clear();
                    break;
                case Keys.Down:
                    DownPressed?.Invoke(this, EventArgs.Empty);
                    break;
                default:
                    return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _input.Focus();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            // 构造期间设置 Height 会先触发布局，此时子控件尚未创建。
            if (_input == null || _clear == null)
                return;
            int iconArea = UiTheme.Scale(30);
            int clearSize = Height - UiTheme.Scale(8);
            _clear.Bounds = new Rectangle(Width - clearSize - UiTheme.Scale(4), UiTheme.Scale(4), clearSize, clearSize);
            int inputRight = Width - clearSize - UiTheme.Scale(8);
            _input.Bounds = new Rectangle(
                iconArea,
                (Height - _input.PreferredHeight) / 2,
                Math.Max(0, inputRight - iconArea),
                _input.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Canvas);
            SmoothingMode previous = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var outer = new Rectangle(0, 0, Width - 1, Height - 1);
            // 聚焦时外圈浅蓝光晕 + 强调色边框，随焦点渐变。
            if (_focusT > 0)
                UiTheme.FillRoundedRectangle(e.Graphics, UiAnimator.Lerp(Parent?.BackColor ?? UiTheme.Canvas, UiTheme.Selection, _focusT), outer, UiTheme.Scale(5));
            UiTheme.FillRoundedRectangle(e.Graphics, UiAnimator.Lerp(UiTheme.BorderStrong, UiTheme.Accent, _focusT),
                Rectangle.Inflate(outer, -1, -1), UiTheme.Scale(4));
            var inner = Rectangle.Inflate(outer, -2, -2);
            UiTheme.FillRoundedRectangle(e.Graphics, UiTheme.Surface, inner, UiTheme.Scale(3));

            // 放大镜：圆 + 斜柄。
            float cx = UiTheme.Scale(15), cy = Height / 2f - UiTheme.Scale(1);
            float r = UiTheme.Scale(5);
            using (var pen = new Pen(UiAnimator.Lerp(UiTheme.TextMuted, UiTheme.Accent, _focusT), UiTheme.Scale(1) + 0.5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            })
            {
                e.Graphics.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                float d = r * 0.72f;
                e.Graphics.DrawLine(pen, cx + d, cy + d, cx + d + UiTheme.Scale(4), cy + d + UiTheme.Scale(4));
            }
            e.Graphics.SmoothingMode = previous;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _debounce.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>选项面板中的一项：标题、说明与可选标签（如“推荐”）。</summary>
    internal sealed class PickerOption
    {
        public PickerOption(string title, string description, string badge = null, bool enabled = true)
        {
            Title = title;
            Description = description;
            Badge = badge;
            Enabled = enabled;
        }

        public string Title { get; }
        public string Description { get; }
        public string Badge { get; }
        public bool Enabled { get; }
    }

    /// <summary>
    /// 浮动选项面板：淡入并上滑出现、悬停底色平滑过渡、标题与说明两行排版。
    /// selected ≥ 0 时为单选面板（显示单选指示），-1 时为动作列表。
    /// 失焦、Esc 或选择后淡出关闭；支持 ↑↓ 与 Enter，禁用项不可选。
    /// </summary>
    internal sealed class OptionPicker : Form
    {
        private const int ShowMs = 170;
        private const int CloseMs = 110;
        private const int CS_DROPSHADOW = 0x20000;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWCP_ROUND = 2;

        private readonly PickerOption[] _options;
        private readonly int _selected;
        private readonly Rectangle[] _rows;
        private readonly float[] _hover;
        private readonly Timer _timer;
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private int _hoverIndex = -1;
        private int _targetTop;
        private bool _closing;
        private bool _systemBorder;
        private int _captionHeight;

        /// <summary>选择的项；未选择为 -1。会弹窗的动作应在面板关闭后再执行。</summary>
        public int ChosenIndex { get; private set; } = -1;

        private bool ShowIndicator
        {
            get { return _selected >= 0; }
        }

        private int TextOffset
        {
            get { return ShowIndicator ? UiTheme.Scale(16) + UiTheme.Scale(10) : 0; }
        }

        public event EventHandler<int> Chosen;

        public OptionPicker(string caption, PickerOption[] options, int selected)
        {
            _options = options;
            _selected = selected;
            _rows = new Rectangle[options.Length];
            _hover = new float[options.Length];
            Text = caption;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            BackColor = UiTheme.Surface;
            Font = UiTheme.BodyFont;
            DoubleBuffered = true;
            Opacity = 0;

            LayoutRows();
            _timer = new Timer { Interval = 15 };
            _timer.Tick += (s, e) => Animate();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return false; }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Windows 11 圆角与边框色；旧系统调用失败时自绘边框。
            try
            {
                int corner = DWMWCP_ROUND;
                int border = ColorTranslator.ToWin32(UiTheme.Border);
                _systemBorder =
                    DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int)) == 0
                    && DwmSetWindowAttribute(Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int)) == 0;
            }
            catch (DllNotFoundException)
            {
                _systemBorder = false;
            }
        }

        private void LayoutRows()
        {
            int pad = UiTheme.Scale(6);
            int rowPadX = UiTheme.Scale(12);
            int rowPadY = UiTheme.Scale(10);
            int titleH = UiTheme.TextHeight(UiTheme.BodyStrongFont);
            int descH = UiTheme.TextHeight(UiTheme.BodyFont);
            _captionHeight = string.IsNullOrEmpty(Text) ? 0 : UiTheme.TextHeight(UiTheme.CaptionFont) + UiTheme.Scale(10);

            int contentWidth = 0;
            foreach (PickerOption option in _options)
            {
                int title = TextRenderer.MeasureText(option.Title, UiTheme.BodyStrongFont).Width;
                if (!string.IsNullOrEmpty(option.Badge))
                    title += BadgeSize(option.Badge).Width + UiTheme.Scale(8);
                int desc = TextRenderer.MeasureText(option.Description ?? string.Empty, UiTheme.BodyFont).Width;
                contentWidth = Math.Max(contentWidth, Math.Max(title, desc));
            }
            int rowWidth = Math.Max(UiTheme.Scale(260), rowPadX * 2 + TextOffset + contentWidth);
            int rowHeight = rowPadY * 2 + titleH + UiTheme.Scale(2) + descH;

            int y = pad + _captionHeight;
            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i] = new Rectangle(pad, y, rowWidth, rowHeight);
                y += rowHeight + UiTheme.Scale(2);
            }
            ClientSize = new Size(rowWidth + pad * 2, y - UiTheme.Scale(2) + pad);
        }

        private static Size BadgeSize(string badge)
        {
            Size text = TextRenderer.MeasureText(badge, UiTheme.CaptionFont, Size.Empty, TextFormatFlags.NoPadding);
            return new Size(text.Width + UiTheme.Scale(10), text.Height + UiTheme.Scale(4));
        }

        /// <summary>默认在锚点上方弹出（空间不足时改到下方）；openBelow 固定向下，alignRight 右边对齐。</summary>
        public void ShowNear(Control anchor, IWin32Window owner, bool alignRight = false, bool openBelow = false)
        {
            Point origin = anchor.PointToScreen(Point.Empty);
            Rectangle work = Screen.FromControl(anchor).WorkingArea;
            int gap = UiTheme.Scale(6);
            int above = origin.Y - Height - gap;
            _targetTop = !openBelow && above >= work.Top ? above : origin.Y + anchor.Height + gap;
            int desired = alignRight ? origin.X + anchor.Width - Width : origin.X;
            int left = Math.Max(work.Left, Math.Min(desired, work.Right - Width));
            // 从目标位置下方 8px 开始上滑。
            Location = new Point(left, _targetTop + UiTheme.Scale(8));
            Show(owner);
            _clock.Restart();
            _timer.Start();
        }

        private static float EaseOut(float t)
        {
            float inv = 1 - t;
            return 1 - inv * inv * inv;
        }

        private void Animate()
        {
            bool busy = false;
            float elapsed = (float)_clock.Elapsed.TotalMilliseconds;
            if (_closing)
            {
                float p = Math.Min(1f, elapsed / CloseMs);
                Opacity = 1 - p;
                Top = _targetTop + (int)(UiTheme.Scale(4) * p);
                if (p >= 1f)
                {
                    _timer.Stop();
                    Close();
                    return;
                }
                busy = true;
            }
            else if (elapsed < ShowMs)
            {
                float e = EaseOut(elapsed / ShowMs);
                Opacity = e;
                Top = _targetTop + (int)Math.Round(UiTheme.Scale(8) * (1 - e));
                busy = true;
            }
            else if (Opacity < 1 || Top != _targetTop)
            {
                Opacity = 1;
                Top = _targetTop;
            }

            bool repaint = false;
            for (int i = 0; i < _hover.Length; i++)
            {
                float target = i == _hoverIndex ? 1f : 0f;
                float delta = target - _hover[i];
                if (Math.Abs(delta) < 0.02f)
                {
                    if (_hover[i] != target)
                    {
                        _hover[i] = target;
                        repaint = true;
                    }
                    continue;
                }
                _hover[i] += delta * 0.3f;
                repaint = busy = true;
            }
            if (repaint)
                Invalidate();
            if (!busy)
                _timer.Stop();
        }

        private void SetHover(int index)
        {
            if (index >= 0 && !_options[index].Enabled)
                index = -1;
            if (index == _hoverIndex)
                return;
            _hoverIndex = index;
            if (!_timer.Enabled)
                _timer.Start();
        }

        private void Choose(int index)
        {
            if (_closing || index < 0 || !_options[index].Enabled)
                return;
            ChosenIndex = index;
            Chosen?.Invoke(this, index);
            BeginClose();
        }

        private void BeginClose()
        {
            if (_closing || IsDisposed)
                return;
            _closing = true;
            _targetTop = Top;
            _clock.Restart();
            _timer.Start();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            BeginClose();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SetHover(Array.FindIndex(_rows, r => r.Contains(e.Location)));
            Cursor = _hoverIndex >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetHover(-1);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
                Choose(Array.FindIndex(_rows, r => r.Contains(e.Location)));
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            int current = _hoverIndex >= 0 ? _hoverIndex : _selected;
            switch (keyData)
            {
                case Keys.Up:
                    SetHover(NextEnabled(current, -1));
                    return true;
                case Keys.Down:
                case Keys.Tab:
                    SetHover(NextEnabled(current, 1));
                    return true;
                case Keys.Enter:
                case Keys.Space:
                    Choose(current);
                    return true;
                case Keys.Escape:
                    BeginClose();
                    return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        private int NextEnabled(int from, int step)
        {
            int count = _options.Length;
            int index = from < 0 ? (step > 0 ? -1 : count) : from;
            for (int i = 0; i < count; i++)
            {
                index = (index + step + count) % count;
                if (_options[index].Enabled)
                    return index;
            }
            return -1;
        }

        private static Color Lerp(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(UiTheme.Surface);

            if (_captionHeight > 0)
            {
                TextRenderer.DrawText(g, Text, UiTheme.CaptionFont,
                    new Rectangle(_rows[0].Left + UiTheme.Scale(12), UiTheme.Scale(8), Width, _captionHeight),
                    UiTheme.TextMuted, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);
            }

            for (int i = 0; i < _options.Length; i++)
                PaintRow(g, i);

            if (!_systemBorder)
            {
                using (var pen = new Pen(UiTheme.BorderStrong))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private void PaintRow(Graphics g, int i)
        {
            PickerOption option = _options[i];
            Rectangle row = _rows[i];
            bool selected = i == _selected;
            bool enabled = option.Enabled;
            float h = _hover[i];

            Color restBack = selected ? UiTheme.AccentSoft : UiTheme.Surface;
            Color hoverBack = selected ? Color.FromArgb(226, 236, 254) : UiTheme.HeaderBack;
            Color back = Lerp(restBack, hoverBack, h);
            if (back.ToArgb() != UiTheme.Surface.ToArgb())
                UiTheme.FillRoundedRectangle(g, back, row, UiTheme.Scale(6));

            int padX = UiTheme.Scale(12);
            int padY = UiTheme.Scale(10);
            int titleH = UiTheme.TextHeight(UiTheme.BodyStrongFont);

            if (ShowIndicator)
            {
                // 单选指示：选中为强调色圆环 + 实心点，未选中为灰环（悬停时加深）。
                int size = UiTheme.Scale(16);
                var ring = new RectangleF(row.Left + padX, row.Top + padY + (titleH - size) / 2f, size, size);
                SmoothingMode previous = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Color ringColor = selected ? UiTheme.Accent : Lerp(UiTheme.BorderStrong, UiTheme.TextMuted, h);
                using (var brush = new SolidBrush(UiTheme.Surface))
                    g.FillEllipse(brush, ring);
                using (var pen = new Pen(ringColor, selected ? UiTheme.Scale(2) : 1.5f))
                    g.DrawEllipse(pen, ring.X + 1, ring.Y + 1, ring.Width - 2, ring.Height - 2);
                if (selected)
                {
                    float dot = size * 0.42f;
                    using (var brush = new SolidBrush(UiTheme.Accent))
                        g.FillEllipse(brush, ring.X + (size - dot) / 2, ring.Y + (size - dot) / 2, dot, dot);
                }
                g.SmoothingMode = previous;
            }

            int textX = row.Left + padX + TextOffset;
            Color titleColor = !enabled ? UiTheme.TextDisabled
                : selected ? UiTheme.Accent
                : Lerp(UiTheme.Text, UiTheme.Accent, h * 0.6f);
            TextRenderer.DrawText(g, option.Title, UiTheme.BodyStrongFont,
                new Point(textX, row.Top + padY), titleColor, TextFormatFlags.NoPadding);

            if (!string.IsNullOrEmpty(option.Badge))
            {
                int titleW = TextRenderer.MeasureText(g, option.Title, UiTheme.BodyStrongFont, Size.Empty, TextFormatFlags.NoPadding).Width;
                Size badgeSize = BadgeSize(option.Badge);
                var badge = new Rectangle(
                    textX + titleW + UiTheme.Scale(8),
                    row.Top + padY + (titleH - badgeSize.Height) / 2,
                    badgeSize.Width,
                    badgeSize.Height);
                UiTheme.FillRoundedRectangle(g, enabled ? UiTheme.Selection : UiTheme.HeaderBack, badge, badge.Height / 2);
                TextRenderer.DrawText(g, option.Badge, UiTheme.CaptionFont, badge,
                    enabled ? UiTheme.Accent : UiTheme.TextDisabled,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            TextRenderer.DrawText(g, option.Description ?? string.Empty, UiTheme.BodyFont,
                new Point(textX, row.Top + padY + titleH + UiTheme.Scale(2)),
                enabled ? UiTheme.TextMuted : UiTheme.TextDisabled, TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>快捷键小方块：浅灰底、细边框、居中键名。</summary>
    internal sealed class KeyCap : Control
    {
        public KeyCap(string text)
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.SupportsTransparentBackColor,
                true);
            Font = UiTheme.KeyFont;
            BackColor = UiTheme.Surface;
            Text = text;
            Size textSize = TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding);
            Size = new Size(
                Math.Max(textSize.Width + UiTheme.Scale(12), UiTheme.Scale(22)),
                textSize.Height + UiTheme.Scale(6));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            var bounds = new Rectangle(0, 0, Width - 1, Height - 2);
            UiTheme.FillRoundedRectangle(e.Graphics, UiTheme.BorderStrong, new Rectangle(0, 1, Width - 1, Height - 1), UiTheme.Scale(3));
            UiTheme.FillRoundedRectangle(e.Graphics, UiTheme.HeaderBack, bounds, UiTheme.Scale(3));
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, UiTheme.TextBody,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>步骤序号：强调色圆形底 + 白色数字。</summary>
    internal sealed class StepBadge : Control
    {
        private readonly int _number;

        public StepBadge(int number)
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint,
                true);
            _number = number;
            BackColor = UiTheme.Surface;
            Font = UiTheme.BodyStrongFont;
            int size = UiTheme.Scale(24);
            Size = new Size(size, size);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            SmoothingMode previous = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(UiTheme.Accent))
                e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
            e.Graphics.SmoothingMode = previous;
            TextRenderer.DrawText(e.Graphics, _number.ToString(), Font, ClientRectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>品牌字标：文字 + 紧贴末字母基线的强调色圆点。</summary>
    internal sealed class BrandMark : Control
    {
        private const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

        public BrandMark(string text)
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint,
                true);
            Font = UiTheme.BrandFont;
            BackColor = UiTheme.Surface;
            Text = text;
            Size textSize = TextRenderer.MeasureText(text, Font, Size.Empty, Flags);
            Size = new Size(textSize.Width + DotSize + UiTheme.Scale(3), textSize.Height);
        }

        private int DotSize
        {
            get { return UiTheme.Scale(5); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            Size textSize = TextRenderer.MeasureText(e.Graphics, Text, Font, Size.Empty, Flags);
            TextRenderer.DrawText(e.Graphics, Text, Font, Point.Empty, UiTheme.Text, Flags);

            // 圆点底边落在字母基线上：基线 = 行高 - 下行高度。
            FontFamily family = Font.FontFamily;
            float descent = Font.Size * family.GetCellDescent(Font.Style) / family.GetEmHeight(Font.Style)
                * e.Graphics.DpiY / 72F;
            int baseline = textSize.Height - (int)Math.Round(descent);
            var dot = new Rectangle(textSize.Width + UiTheme.Scale(2), baseline - DotSize, DotSize, DotSize);
            SmoothingMode previous = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(UiTheme.Accent))
                e.Graphics.FillEllipse(brush, dot);
            e.Graphics.SmoothingMode = previous;
        }
    }

    /// <summary>隐藏系统页签条的 TabControl，页签由 <see cref="NavTab"/> 负责。</summary>
    internal sealed class HeaderlessTabControl : TabControl
    {
        private const int TCM_ADJUSTRECT = 0x1328;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == TCM_ADJUSTRECT && !DesignMode)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }
    }
}
