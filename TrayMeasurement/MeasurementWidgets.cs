using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Color = System.Drawing.Color;

namespace JiePinPai.Navisworks.TrayMeasurement
{
    internal enum ModeGlyph { List, Pointer }

    internal enum SummaryTone { Muted, Accent, Success, Warning }

    /// <summary>
    /// 测量方式卡片：图标 + 标题 + 说明 + 单选标记。悬停与选中颜色平滑过渡；
    /// 禁用时选中卡片保留淡化的强调色，避免看起来像“失效”。
    /// </summary>
    internal sealed class ModeCard : Control
    {
        private readonly ModeGlyph _glyph;
        private readonly string _description;
        private readonly Func<bool> _step;
        private bool _active;
        private bool _hover;
        private bool _animating;
        private float _hoverT;
        private float _activeT;

        public ModeCard(string title, string description, ModeGlyph glyph)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            Text = title;
            _description = description;
            _glyph = glyph;
            _step = Step;
            TabStop = true;
            Cursor = Cursors.Hand;
            Font = UiTheme.TitleFont;
            BackColor = UiTheme.Canvas;
            AccessibleRole = AccessibleRole.RadioButton;
        }

        public bool Active
        {
            get { return _active; }
            set { if (_active == value) return; _active = value; Animate(); }
        }

        private static int S(int pixels) => UiTheme.Scale(pixels);

        private void Animate()
        {
            if (!IsHandleCreated || !Visible)
            {
                _hoverT = _hover ? 1 : 0; _activeT = _active ? 1 : 0;
                Invalidate(); return;
            }
            if (_animating) return;
            _animating = true;
            UiAnimator.Run(_step);
        }

        private bool Step()
        {
            if (IsDisposed) return _animating = false;
            bool done = UiAnimator.Approach(ref _hoverT, _hover && Enabled ? 1 : 0, 0.3f)
                & UiAnimator.Approach(ref _activeT, _active ? 1 : 0, 0.25f);
            Invalidate();
            if (done) _animating = false;
            return !done;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Animate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Animate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Cursor = Enabled ? Cursors.Hand : Cursors.Default; Animate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && CanFocus) Focus();
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || keyData == Keys.Enter || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { e.Handled = true; OnClick(EventArgs.Empty); }
        }

        private Color Dim(Color color) => Enabled ? color : UiAnimator.Lerp(color, UiTheme.Canvas, 0.45f);

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            var outer = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = S(8);
            Color back = UiAnimator.Lerp(UiAnimator.Lerp(UiTheme.Surface, UiTheme.SurfaceMuted, _hoverT), UiTheme.AccentSoft, _activeT);
            Color border = UiAnimator.Lerp(UiAnimator.Lerp(UiTheme.Border, UiTheme.TextDisabled, _hoverT), UiTheme.Accent, _activeT);
            // 选中卡片使用 2px 强调描边。
            int stroke = _activeT > 0.5f ? 2 : 1;
            UiTheme.FillRoundedRectangle(g, Dim(border), outer, radius);
            UiTheme.FillRoundedRectangle(g, Dim(back), Rectangle.Inflate(outer, -stroke, -stroke), radius - stroke);

            int tile = S(38);
            var tileRect = new Rectangle(S(16), (Height - tile) / 2, tile, tile);
            UiTheme.FillRoundedRectangle(g, Dim(UiAnimator.Lerp(UiTheme.HeaderBack, UiTheme.Accent, _activeT)), tileRect, S(8));
            DrawGlyph(g, tileRect, Dim(UiAnimator.Lerp(UiTheme.TextMuted, Color.White, _activeT)));

            int radio = S(18);
            var radioRect = new Rectangle(Width - S(18) - radio, (Height - radio) / 2, radio, radio);
            int textLeft = tileRect.Right + S(14);
            int textWidth = Math.Max(0, radioRect.Left - S(12) - textLeft);
            int titleH = UiTheme.TextHeight(UiTheme.TitleFont);
            int descH = UiTheme.TextHeight(UiTheme.BodyFont);
            int top = (Height - titleH - S(3) - descH) / 2;
            const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, Text, UiTheme.TitleFont, new Rectangle(textLeft, top, textWidth, titleH),
                Dim(UiAnimator.Lerp(UiTheme.Text, UiTheme.AccentPressed, _activeT)), flags);
            TextRenderer.DrawText(g, _description, UiTheme.BodyFont, new Rectangle(textLeft, top + titleH + S(3), textWidth, descH),
                Dim(UiTheme.TextMuted), flags);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var ring = new Pen(Dim(UiAnimator.Lerp(UiTheme.BorderStrong, UiTheme.Accent, _activeT)), Math.Max(1.5f, S(2) * 0.75f)))
                g.DrawEllipse(ring, radioRect.X + 1, radioRect.Y + 1, radioRect.Width - 2, radioRect.Height - 2);
            if (_activeT > 0.01f)
            {
                float inset = (1 - _activeT) * radio / 2f;
                using (var fill = new SolidBrush(Dim(Color.FromArgb((int)(255 * _activeT), UiTheme.Accent))))
                    g.FillEllipse(fill, radioRect.X + inset, radioRect.Y + inset, radio - inset * 2, radio - inset * 2);
                float u = radio / 18f;
                using (var check = new Pen(Color.FromArgb((int)(255 * _activeT), Color.White), 1.8f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(check, new[] {
                        new PointF(radioRect.X + 5f * u, radioRect.Y + 9.5f * u),
                        new PointF(radioRect.X + 8f * u, radioRect.Y + 12.5f * u),
                        new PointF(radioRect.X + 13f * u, radioRect.Y + 6.5f * u) });
            }

            if (Focused && ShowFocusCues)
                using (var focus = new Pen(Color.FromArgb(120, UiTheme.Accent), S(2)))
                    g.DrawRectangle(focus, 2.5f, 2.5f, Width - 6f, Height - 6f);
        }

        private void DrawGlyph(Graphics g, Rectangle box, Color color)
        {
            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float u = box.Width / 38f, cx = box.X + box.Width / 2f, cy = box.Y + box.Height / 2f;
            using (var pen = new Pen(color, 1.8f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(color))
            {
                if (_glyph == ModeGlyph.List)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float y = cy + i * 6.5f * u;
                        g.FillEllipse(brush, cx - 9.5f * u, y - 1.7f * u, 3.4f * u, 3.4f * u);
                        g.DrawLine(pen, cx - 3.5f * u, y, cx + (i == 1 ? 5f : 9f) * u, y);
                    }
                }
                else
                {
                    var arrow = new[]
                    {
                        new PointF(cx - 5.5f * u, cy - 9f * u), new PointF(cx - 5.5f * u, cy + 6.5f * u),
                        new PointF(cx - 1.5f * u, cy + 2.8f * u), new PointF(cx + 1.6f * u, cy + 9.2f * u),
                        new PointF(cx + 4.4f * u, cy + 7.9f * u), new PointF(cx + 1.4f * u, cy + 1.6f * u),
                        new PointF(cx + 6.6f * u, cy + 1.6f * u)
                    };
                    g.DrawPolygon(pen, arrow);
                }
            }
            g.SmoothingMode = previous;
        }
    }

    /// <summary>
    /// 测量概览卡片：总长（数值滚动过渡）、三项计数、状态文字与分段进度条。
    /// 测量进行中进度条带一道缓慢流动的高光，表示仍在工作。
    /// </summary>
    internal sealed class MeasurementSummary : Control
    {
        private readonly Font _totalFont = new Font("Segoe UI Semibold", 26F);
        private readonly Font _unitFont = new Font("Segoe UI Semibold", 13F);
        private readonly Font _statFont = new Font("Segoe UI Semibold", 15F);
        private readonly Stopwatch _countClock = new Stopwatch();
        private readonly Stopwatch _shimmerClock = Stopwatch.StartNew();
        private readonly Func<bool> _step;
        private bool _animating;
        private double? _total;
        private double _fromTotal, _shownTotal;
        private int _measured, _review, _remaining, _done, _all;
        private float _measuredFraction, _reviewFraction, _shownMeasured, _shownReview;
        private bool _running;
        private string _status = string.Empty;
        private SummaryTone _tone;
        private string _source = string.Empty;

        public MeasurementSummary()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Canvas;
            _step = Step;
        }

        private static int S(int pixels) => UiTheme.Scale(pixels);

        public string SourceText
        {
            get { return _source; }
            set { _source = value ?? string.Empty; Invalidate(); }
        }

        public void SetValues(double? total, int measured, int review, int remaining, int done, bool running,
            string status, SummaryTone tone)
        {
            if (total != _total)
            {
                _fromTotal = total.HasValue && _total.HasValue ? _shownTotal : 0;
                _total = total;
                _countClock.Restart();
            }
            _measured = measured; _review = review; _remaining = remaining;
            _all = measured + review + remaining; _done = done; _running = running;
            _measuredFraction = _all == 0 ? 0 : (float)measured / _all;
            _reviewFraction = _all == 0 ? 0 : (float)review / _all;
            _status = status ?? string.Empty; _tone = tone;
            AccessibleName = $"直桥架总长 {FormatTotal(total ?? 0)} 米，已测 {measured}，待复核 {review}，未测 {remaining}。{status}";
            Animate();
        }

        private static string FormatTotal(double value) => MeasurementBatchPolicy.FormatMetres(value);

        private void Animate()
        {
            if (!IsHandleCreated || !Visible)
            {
                _shownTotal = _total ?? 0; _shownMeasured = _measuredFraction; _shownReview = _reviewFraction;
                Invalidate(); return;
            }
            Invalidate();
            if (_animating) return;
            _animating = true;
            UiAnimator.Run(_step);
        }

        private bool Step()
        {
            if (IsDisposed) return _animating = false;
            bool counting = false;
            if (_countClock.IsRunning)
            {
                float t = UiAnimator.EaseOut((float)_countClock.Elapsed.TotalMilliseconds / 450f);
                double to = _total ?? 0;
                _shownTotal = _fromTotal + (to - _fromTotal) * t;
                counting = t < 1f;
                if (!counting) { _shownTotal = to; _countClock.Reset(); }
            }
            bool settled = UiAnimator.Approach(ref _shownMeasured, _measuredFraction, 0.2f)
                & UiAnimator.Approach(ref _shownReview, _reviewFraction, 0.2f);
            Invalidate();
            bool keep = counting || !settled || _running;
            if (!keep) _animating = false;
            return keep;
        }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible) Animate(); }

        // 进度条与圆点使用比状态标签文字更明亮的同色系，在细条上更易辨认。
        private static readonly Color MeasuredInk = Color.FromArgb(22, 163, 74);
        private static readonly Color ReviewInk = Color.FromArgb(234, 120, 32);

        /// <summary>按当前字体计算卡片所需高度，供布局使用。</summary>
        public int PreferredCardHeight
        {
            get
            {
                int bodyH = UiTheme.TextHeight(UiTheme.BodyFont);
                int totalH = TextRenderer.MeasureText("0", _totalFont, Size.Empty, TextFormatFlags.NoPadding).Height;
                return S(14) + bodyH + S(4) + totalH + S(8) + S(6) + S(10) + bodyH + S(16);
            }
        }

        private static Color ToneColor(SummaryTone tone)
        {
            switch (tone)
            {
                case SummaryTone.Accent: return UiTheme.Accent;
                case SummaryTone.Success: return MeasuredInk;
                case SummaryTone.Warning: return ReviewInk;
                default: return UiTheme.TextDisabled;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            var card = new Rectangle(0, 0, Width - 1, Height - 1);
            UiTheme.FillRoundedRectangle(g, UiTheme.Border, card, S(8));
            UiTheme.FillRoundedRectangle(g, UiTheme.Surface, Rectangle.Inflate(card, -1, -1), S(8) - 1);

            int pad = S(20);
            int inner = Width - pad * 2;
            const TextFormatFlags line = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            int bodyH = UiTheme.TextHeight(UiTheme.BodyFont);

            // 第一行：标题 + 测量来源。
            int y = S(14);
            Size caption = TextRenderer.MeasureText(g, "直桥架总长", UiTheme.BodyStrongFont, Size.Empty, line);
            TextRenderer.DrawText(g, "直桥架总长", UiTheme.BodyStrongFont, new Point(pad, y), UiTheme.TextBody, line);
            int sourceLeft = pad + caption.Width + S(24);
            TextRenderer.DrawText(g, _source, UiTheme.BodyFont, new Rectangle(sourceLeft, y, Math.Max(0, Width - pad - sourceLeft), bodyH),
                UiTheme.TextMuted, line | TextFormatFlags.Right);

            // 第二行：总长 + 计数。
            int y2 = y + bodyH + S(4);
            string total = _total.HasValue ? FormatTotal(_shownTotal) : "—";
            Size totalSize = TextRenderer.MeasureText(g, total, _totalFont, Size.Empty, TextFormatFlags.NoPadding);
            Color totalColor = _total.HasValue ? UiTheme.Text : UiTheme.TextDisabled;
            TextRenderer.DrawText(g, total, _totalFont, new Point(pad - S(2), y2), totalColor, TextFormatFlags.NoPadding);
            Size unitSize = TextRenderer.MeasureText(g, "m", _unitFont, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "m", _unitFont, new Point(pad + totalSize.Width + S(4), y2 + totalSize.Height - unitSize.Height - S(7)),
                UiTheme.TextMuted, TextFormatFlags.NoPadding);

            Color measuredColor = MeasuredInk, reviewColor = ReviewInk;
            int statWidth = S(96);
            DrawStat(g, Width - pad - statWidth * 3, y2 + totalSize.Height, statWidth, "已测", _measured, measuredColor);
            DrawStat(g, Width - pad - statWidth * 2, y2 + totalSize.Height, statWidth, "待复核", _review, reviewColor);
            DrawStat(g, Width - pad - statWidth, y2 + totalSize.Height, statWidth, "未测", _remaining, UiTheme.TextDisabled);

            // 第三行：状态 + 百分比；第四行：分段进度条。
            int trackH = S(6);
            int trackY = y2 + totalSize.Height + S(8);
            int statusY = trackY + trackH + S(10);
            string percent = _all == 0 ? string.Empty : $"{Math.Round(100.0 * Math.Min(_done, _all) / _all):0}%";
            const TextFormatFlags plain = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            Size percentSize = TextRenderer.MeasureText(g, percent, UiTheme.BodyStrongFont, Size.Empty, plain);
            TextRenderer.DrawText(g, percent, UiTheme.BodyStrongFont, new Point(Width - pad - percentSize.Width, statusY), UiTheme.TextBody, plain);
            int dot = S(8);
            Color tone = ToneColor(_tone);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(tone))
                g.FillEllipse(brush, pad, statusY + (bodyH - dot) / 2, dot, dot);
            if (_running)
            {
                // 运行中的状态点外扩一圈呼吸光晕。
                float pulse = (float)((_shimmerClock.Elapsed.TotalMilliseconds % 1200) / 1200);
                float grow = dot * pulse;
                using (var halo = new SolidBrush(Color.FromArgb((int)(110 * (1 - pulse)), tone)))
                    g.FillEllipse(halo, pad - grow / 2, statusY + (bodyH - dot) / 2 - grow / 2, dot + grow, dot + grow);
            }
            g.SmoothingMode = SmoothingMode.None;
            TextRenderer.DrawText(g, _status, UiTheme.BodyFont,
                new Rectangle(pad + dot + S(8), statusY, Math.Max(0, inner - dot - S(16) - percentSize.Width), bodyH),
                _tone == SummaryTone.Muted ? UiTheme.TextMuted : UiTheme.TextBody, line);

            var track = new Rectangle(pad, trackY, inner, trackH);
            UiTheme.FillRoundedRectangle(g, UiTheme.HeaderBack, track, trackH / 2);
            int measuredW = (int)Math.Round(inner * Clamp(_shownMeasured));
            int reviewW = (int)Math.Round(inner * Clamp(_shownReview));
            int filled = Math.Min(inner, measuredW + reviewW);
            if (filled <= 0) return;
            var fillRect = new Rectangle(pad, trackY, filled, trackH);
            using (var path = RoundedPath(fillRect, trackH / 2))
            {
                Region previous = g.Clip;
                g.SetClip(path, CombineMode.Intersect);
                using (var green = new SolidBrush(measuredColor))
                    g.FillRectangle(green, pad, trackY, measuredW, trackH);
                using (var orange = new SolidBrush(reviewColor))
                    g.FillRectangle(orange, pad + measuredW, trackY, filled - measuredW, trackH);
                if (_running)
                {
                    int band = S(90);
                    float phase = (float)((_shimmerClock.Elapsed.TotalMilliseconds % 1400) / 1400);
                    int bx = pad - band + (int)((filled + band) * phase);
                    var bandRect = new Rectangle(bx, trackY, band, trackH);
                    using (var shine = new LinearGradientBrush(bandRect, Color.Transparent, Color.Transparent, LinearGradientMode.Horizontal))
                    {
                        shine.InterpolationColors = new ColorBlend
                        {
                            Colors = new[] { Color.FromArgb(0, Color.White), Color.FromArgb(120, Color.White), Color.FromArgb(0, Color.White) },
                            Positions = new[] { 0f, .5f, 1f }
                        };
                        g.FillRectangle(shine, bandRect);
                    }
                }
                g.Clip = previous;
            }
        }

        // 计数块底边与总长数字底边对齐。
        private void DrawStat(Graphics g, int x, int bottom, int width, string label, int value, Color color)
        {
            const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            int labelH = UiTheme.TextHeight(UiTheme.BodyFont);
            int dot = S(7);
            Size labelSize = TextRenderer.MeasureText(g, label, UiTheme.BodyFont, Size.Empty, flags);
            int labelX = x + width - labelSize.Width;
            string text = value.ToString();
            Size valueSize = TextRenderer.MeasureText(g, text, _statFont, Size.Empty, flags);
            int valueY = bottom - valueSize.Height - S(3);
            int y = valueY - labelH - S(4);
            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(color))
                g.FillEllipse(brush, labelX - dot - S(6), y + (labelH - dot) / 2, dot, dot);
            g.SmoothingMode = previous;
            TextRenderer.DrawText(g, label, UiTheme.BodyFont, new Point(labelX, y), UiTheme.TextMuted, flags);
            TextRenderer.DrawText(g, text, _statFont, new Point(x + width - valueSize.Width, valueY),
                value == 0 ? UiTheme.TextDisabled : UiTheme.Text, flags);
        }

        private static float Clamp(float value) => Math.Max(0f, Math.Min(1f, value));

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _totalFont.Dispose(); _unitFont.Dispose(); _statFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
