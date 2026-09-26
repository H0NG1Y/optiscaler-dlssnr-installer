using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OptiScalerInstaller
{
    internal static class ModernTheme
    {
        internal static readonly Color Background = Color.FromArgb(244, 247, 251);
        internal static readonly Color Ink = Color.FromArgb(29, 42, 62);
        internal static readonly Color Muted = Color.FromArgb(105, 120, 142);
        internal static readonly Color Blue = Color.FromArgb(42, 104, 235);
        internal static readonly Color Border = Color.FromArgb(226, 233, 243);

        internal static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ModernCard : Panel
    {
        internal Color Surface = Color.White;
        internal Color BorderColor = ModernTheme.Border;
        internal int Radius = 14;

        internal ModernCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (Width < 2 || Height < 2) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath shape = ModernTheme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (Brush fill = new SolidBrush(Surface))
            using (Pen border = new Pen(BorderColor))
            {
                e.Graphics.FillPath(fill, shape);
                e.Graphics.DrawPath(border, shape);
            }
        }
    }

    internal sealed class ModernButton : Button
    {
        internal bool Primary;
        internal bool Quiet;
        private bool hovered;
        private bool pressed;

        internal ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = ModernTheme.Background;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.Opaque, false);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Transparent Button backgrounds replay neighbouring controls into the
            // buffer. Fill the complete surface, including the rounded corners.
            Color surface = ModernTheme.Background;
            for (Control ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                ModernCard card = ancestor as ModernCard;
                if (card != null) { surface = card.Surface; break; }
                if (ancestor.BackColor.A == 255) { surface = ancestor.BackColor; break; }
            }
            using (Brush background = new SolidBrush(surface))
                e.Graphics.FillRectangle(background, ClientRectangle);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size text = TextRenderer.MeasureText(Text, Font);
            float scale = Font.SizeInPoints / 9F;
            return new Size(Math.Max(MinimumSize.Width, text.Width + Padding.Horizontal + (int)(16 * scale)),
                Math.Max(MinimumSize.Height, text.Height + Padding.Vertical + (int)(12 * scale)));
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary ? ModernTheme.Blue : Quiet ? Color.Transparent : Color.FromArgb(241, 245, 251);
            Color ink = Primary ? Color.White : ModernTheme.Ink;
            if (!Enabled) { fill = Primary ? Color.FromArgb(203, 216, 238) : Color.FromArgb(244, 246, 249); ink = Color.FromArgb(157, 168, 183); }
            else if (pressed) fill = Primary ? Color.FromArgb(27, 80, 190) : Color.FromArgb(222, 231, 245);
            else if (hovered) fill = Primary ? Color.FromArgb(30, 89, 214) : Color.FromArgb(230, 237, 249);
            Rectangle bounds = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            using (GraphicsPath shape = ModernTheme.Rounded(bounds, 9))
            {
                if (fill != Color.Transparent) using (Brush brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, shape);
                if (Focused && Enabled) using (Pen focus = new Pen(Color.FromArgb(141, 179, 249), 2)) e.Graphics.DrawPath(focus, shape);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis |
                TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
        }
    }

    internal sealed class ModernComboBox : ComboBox
    {
        internal string Placeholder = "请选择";

        internal ModernComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            ItemHeight = 32;
            Height = 36;
            MinimumSize = new Size(80, 36);
            BackColor = Color.White;
            ForeColor = ModernTheme.Ink;
            IntegralHeight = false;
            MaxDropDownItems = 10;
            Margin = Padding.Empty;
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            ItemHeight = Math.Max(32, Font.Height + 13);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Bounds.Width <= 0 || e.Bounds.Height <= 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (Brush background = new SolidBrush(selected ? Color.FromArgb(234, 241, 255) : Color.White)) e.Graphics.FillRectangle(background, e.Bounds);
            string text = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : Placeholder;
            Rectangle textBounds = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, Math.Max(0, e.Bounds.Width - 18), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, Font, textBounds, Enabled ? ModernTheme.Ink : ModernTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void PaintClosed(Graphics graphics)
        {
            if (Width < 4 || Height < 4) return;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color surrounding = Parent == null || Parent.BackColor.A == 0 ? Color.White : Parent.BackColor;
            using (Brush background = new SolidBrush(surrounding)) graphics.FillRectangle(background, ClientRectangle);
            using (GraphicsPath shape = ModernTheme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 7))
            using (Brush fill = new SolidBrush(Enabled ? Color.FromArgb(249, 251, 254) : Color.FromArgb(244, 246, 249)))
            using (Pen border = new Pen(Focused && Enabled ? Color.FromArgb(129, 170, 246) : ModernTheme.Border))
            {
                graphics.FillPath(fill, shape);
                graphics.DrawPath(border, shape);
            }
            string text = SelectedIndex >= 0 ? GetItemText(SelectedItem) : Placeholder;
            Color ink = Enabled && SelectedIndex >= 0 ? ModernTheme.Ink : ModernTheme.Muted;
            TextRenderer.DrawText(graphics, text, Font, new Rectangle(10, 1, Math.Max(0, Width - 39), Height - 2), ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            int centerX = Width - 17, centerY = Height / 2;
            using (Pen arrow = new Pen(Enabled ? ModernTheme.Muted : Color.FromArgb(182, 191, 204), 1.7F))
                graphics.DrawLines(arrow, new[] { new Point(centerX - 4, centerY - 2), new Point(centerX, centerY + 2), new Point(centerX + 4, centerY - 2) });
        }

        protected override void WndProc(ref Message m)
        {
            const int WmPaint = 0x000F, WmPrint = 0x0317, WmPrintClient = 0x0318;
            base.WndProc(ref m);
            if (m.Msg == WmPaint)
            {
                using (Graphics graphics = Graphics.FromHwnd(Handle)) PaintClosed(graphics);
            }
            else if ((m.Msg == WmPrint || m.Msg == WmPrintClient) && m.WParam != IntPtr.Zero)
            {
                using (Graphics graphics = Graphics.FromHdc(m.WParam)) PaintClosed(graphics);
            }
        }
    }

    internal sealed class ModernTextBox : TextBox
    {
        const int EmSetcuebanner = 0x1501;
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private string placeholder;

        internal ModernTextBox()
        {
            BorderStyle = BorderStyle.None;
            BackColor = Color.FromArgb(249, 251, 254);
            ForeColor = ModernTheme.Ink;
            Font = new Font("Microsoft YaHei UI", 9F);
            Margin = Padding.Empty;
        }

        internal string Placeholder
        {
            get { return placeholder; }
            set { placeholder = value; ApplyCueBanner(); }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyCueBanner();
        }

        private void ApplyCueBanner()
        {
            if (!IsHandleCreated) return;
            SendMessage(Handle, EmSetcuebanner, (IntPtr)1, placeholder ?? "");
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); if (Parent != null) Parent.Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); if (Parent != null) Parent.Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            BackColor = Enabled ? Color.FromArgb(249, 251, 254) : Color.FromArgb(244, 246, 249);
        }

        protected override void WndProc(ref Message m)
        {
            const int WmPrint = 0x0317, WmPrintClient = 0x0318;
            base.WndProc(ref m);
            if ((m.Msg == WmPrint || m.Msg == WmPrintClient) && m.WParam != IntPtr.Zero)
            {
                using (Graphics graphics = Graphics.FromHdc(m.WParam))
                {
                    using (Brush background = new SolidBrush(BackColor)) graphics.FillRectangle(background, ClientRectangle);
                    bool empty = String.IsNullOrEmpty(Text);
                    TextRenderer.DrawText(graphics, empty ? (placeholder ?? "") : Text, Font, ClientRectangle,
                        empty ? Color.FromArgb(135, 148, 166) : ForeColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                }
            }
        }
    }

    /// <summary>
    /// Rounded chrome matching ModernComboBox. The hosted borderless control is positioned explicitly
    /// and vertically centred, so the native edit text sits mid-box and the 1px border stays visible.
    /// </summary>
    internal sealed class ModernInputChrome : Panel
    {
        private const int HorizontalInset = 12;
        private const int VerticalInset = 2;
        private readonly Control inner;

        internal ModernInputChrome(Control content)
        {
            inner = content;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 36;
            MinimumSize = new Size(80, 36);
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            content.Margin = Padding.Empty;
            content.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            Controls.Add(content);
        }

        internal Control Inner { get { return inner; } }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutInner();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutInner();
        }

        private void LayoutInner()
        {
            if (inner == null || inner.IsDisposed) return;
            int innerHeight = 0;
            TextBox box = inner as TextBox;
            if (box != null) innerHeight = box.PreferredHeight;
            else innerHeight = inner.Height;
            if (innerHeight <= 0 || innerHeight > Height - VerticalInset * 2)
                innerHeight = Math.Max(14, Height - VerticalInset * 2);
            int top = Math.Max(VerticalInset, (Height - innerHeight) / 2);
            int width = Math.Max(10, Width - HorizontalInset * 2);
            if (inner.Bounds != new Rectangle(HorizontalInset, top, width, innerHeight))
                inner.SetBounds(HorizontalInset, top, width, innerHeight);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Color surrounding = Parent == null || Parent.BackColor.A == 0 ? Color.White : Parent.BackColor;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(surrounding);
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            bool enabled = inner != null && inner.Enabled;
            Color fill = enabled ? Color.FromArgb(249, 251, 254) : Color.FromArgb(244, 246, 249);
            using (System.Drawing.Drawing2D.GraphicsPath shape = ModernTheme.Rounded(bounds, 7))
            using (Brush brush = new SolidBrush(fill))
                e.Graphics.FillPath(brush, shape);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            bool enabled = inner != null && inner.Enabled;
            bool focused = inner != null && inner.Focused;
            Color border = !enabled ? Color.FromArgb(220, 226, 235) : focused ? Color.FromArgb(129, 170, 246) : ModernTheme.Border;
            using (System.Drawing.Drawing2D.GraphicsPath shape = ModernTheme.Rounded(bounds, 7))
            using (Pen pen = new Pen(border))
                e.Graphics.DrawPath(pen, shape);
        }

        protected override void OnControlAdded(ControlEventArgs e) { base.OnControlAdded(e); if (e.Control != null) e.Control.EnabledChanged += delegate { Invalidate(); }; }
    }

    internal sealed class ModernLogBox : RichTextBox
    {
        protected override void WndProc(ref Message m)
        {
            const int WmPrint = 0x0317, WmPrintClient = 0x0318;
            base.WndProc(ref m);
            if ((m.Msg == WmPrint || m.Msg == WmPrintClient) && m.WParam != IntPtr.Zero)
            {
                using (Graphics graphics = Graphics.FromHdc(m.WParam))
                {
                    using (Brush background = new SolidBrush(BackColor)) graphics.FillRectangle(background, ClientRectangle);
                    string[] lines = Text.TrimEnd('\r', '\n').Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    int count = Math.Max(1, (Height - 6) / (Font.Height + 3));
                    int start = Math.Max(0, lines.Length - count), y = 3;
                    for (int i = start; i < lines.Length; i++)
                    {
                        TextRenderer.DrawText(graphics, lines[i], Font, new Rectangle(4, y, Math.Max(1, Width - 12), Font.Height + 3), ForeColor,
                            TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                        y += Font.Height + 3;
                    }
                }
            }
        }
    }

    internal sealed class ModernProgressBar : Control
    {
        private int value;
        private int pulse;
        private ProgressBarStyle style;
        private readonly Timer animation = new Timer();

        internal ModernProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 6;
            animation.Interval = 35;
            animation.Tick += delegate { pulse = (pulse + 3) % 140; Invalidate(); };
        }

        internal int Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        internal ProgressBarStyle Style { get { return style; } set { style = value; animation.Enabled = value == ProgressBarStyle.Marquee; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 4 || Height < 4) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, (Height - 6) / 2, Width - 1, 6);
            using (GraphicsPath shape = ModernTheme.Rounded(track, 3))
            using (Brush brush = new SolidBrush(Color.FromArgb(223, 232, 245))) e.Graphics.FillPath(brush, shape);
            int length = style == ProgressBarStyle.Marquee ? Width / 4 : Width * value / 100;
            int x = style == ProgressBarStyle.Marquee ? Width * pulse / 100 - Width / 4 : 0;
            Rectangle fill = Rectangle.Intersect(track, new Rectangle(x, track.Top, length, track.Height));
            if (fill.Width > 1) using (GraphicsPath shape = ModernTheme.Rounded(fill, 3))
                using (Brush brush = new SolidBrush(ModernTheme.Blue)) e.Graphics.FillPath(brush, shape);
        }

        protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
    }
}
