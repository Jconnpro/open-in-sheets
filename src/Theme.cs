using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenInSheets
{
    /// <summary>
    /// Palette, type scale, and the handful of custom-drawn controls the windows are
    /// built from. Everything here is GDI+ only - no packages, no resources, so the
    /// whole app stays one small file.
    /// </summary>
    static class Theme
    {
        public static readonly Color Page = Color.FromArgb(246, 247, 249);
        public static readonly Color Surface = Color.White;
        public static readonly Color Line = Color.FromArgb(226, 229, 234);
        public static readonly Color Ink = Color.FromArgb(26, 29, 33);
        public static readonly Color Muted = Color.FromArgb(107, 114, 128);
        public static readonly Color Accent = Color.FromArgb(15, 157, 88);
        public static readonly Color AccentHover = Color.FromArgb(12, 128, 70);
        public static readonly Color Danger = Color.FromArgb(179, 38, 30);
        public static readonly Color WarnInk = Color.FromArgb(146, 84, 14);
        public static readonly Color WarnFill = Color.FromArgb(254, 249, 231);
        public static readonly Color WarnLine = Color.FromArgb(240, 224, 168);
        public static readonly Color Idle = Color.FromArgb(156, 163, 175);
        public static readonly Color ControlFill = Color.FromArgb(243, 244, 246);
        public static readonly Color ControlLine = Color.FromArgb(209, 213, 219);

        public static readonly Font Title = new Font("Segoe UI Semibold", 17f);
        public static readonly Font Heading = new Font("Segoe UI Semibold", 10.5f);
        public static readonly Font Body = new Font("Segoe UI", 9.75f);
        public static readonly Font Small = new Font("Segoe UI", 9f);

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void Panel(Graphics g, Rectangle r, int radius, Color fill, Color border)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Rounded(r, radius))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
                if (border != Color.Empty)
                    using (Pen pen = new Pen(border)) g.DrawPath(pen, path);
            }
        }

        public static Label Text(string text, Font font, Color color, int x, int y, int w, int h)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.SetBounds(x, y, w, h);
            return label;
        }
    }

    /// <summary>A flat white card with a hairline border and rounded corners.</summary>
    class Card : Panel
    {
        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Page);
            Theme.Panel(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), 10, Theme.Surface, Theme.Line);
        }
    }

    /// <summary>The amber "one more step" callout inside a card.</summary>
    class Callout : Panel
    {
        readonly Label _text;

        public Callout()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;

            _text = Theme.Text("", Theme.Small, Theme.WarnInk, 14, 11, 10, 10);
            Controls.Add(_text);
        }

        public string Message
        {
            get { return _text.Text; }
            set
            {
                _text.Text = value;
                _text.SetBounds(14, 11, Width - 28, Height - 22);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Surface);
            Theme.Panel(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), 8, Theme.WarnFill, Theme.WarnLine);
        }
    }

    /// <summary>Status dot: green when done, amber when the user still has to act.</summary>
    class Dot : Control
    {
        Color _color = Theme.Idle;

        public Dot()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Size = new Size(10, 10);
        }

        public Color Color
        {
            get { return _color; }
            set { _color = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Surface);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(_color))
                e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
        }
    }

    /// <summary>The little app mark in the header - a rounded tile with a grid on it.</summary>
    class Mark : Control
    {
        public Mark()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Page;
            Size = new Size(40, 40);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Page);
            Theme.Panel(g, new Rectangle(0, 0, Width - 1, Height - 1), 10, Theme.Accent, Color.Empty);

            using (Pen pen = new Pen(Color.White, 1.6f))
            {
                int left = 10, right = Width - 10, top = 11, bottom = Height - 11;
                int midY = (top + bottom) / 2;
                g.DrawRectangle(pen, left, top, right - left, bottom - top);
                g.DrawLine(pen, left, midY, right, midY);
                g.DrawLine(pen, (left + right) / 2, top, (left + right) / 2, bottom);
            }
        }
    }

    /// <summary>Flat rounded button, owner-drawn so it doesn't inherit Win32 chrome.</summary>
    class FlatButton : Button
    {
        public Color Fill = Theme.Surface;
        public Color Hover = Theme.ControlFill;
        public Color Edge = Theme.ControlLine;
        public Color Foreground = Color.FromArgb(55, 65, 81);

        bool _hot;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            BackColor = Theme.Surface;
            Height = 36;
        }

        public static FlatButton Primary(string text)
        {
            FlatButton b = new FlatButton();
            b.Text = text;
            b.Style(true);
            return b;
        }

        public static FlatButton Secondary(string text)
        {
            FlatButton b = new FlatButton();
            b.Text = text;
            b.Style(false);
            return b;
        }

        /// <summary>
        /// Filled green reads as "do this next". Once a step is done its button drops
        /// back to secondary, so the only emphasis on screen is on work still outstanding.
        /// </summary>
        public void Style(bool primary)
        {
            if (primary)
            {
                Fill = Theme.Accent;
                Hover = Theme.AccentHover;
                Edge = Color.Empty;
                Foreground = Color.White;
            }
            else
            {
                Fill = Theme.Surface;
                Hover = Theme.ControlFill;
                Edge = Theme.ControlLine;
                Foreground = Color.FromArgb(55, 65, 81);
            }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hot = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            Color fill = !Enabled ? Theme.ControlFill : (_hot ? Hover : Fill);
            Theme.Panel(g, new Rectangle(0, 0, Width - 1, Height - 1), 6, fill, Enabled ? Edge : Theme.Line);

            TextRenderer.DrawText(g, Text, Font, ClientRectangle,
                Enabled ? Foreground : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Footer text link. Underlines on hover instead of always, like a modern app.</summary>
    class TextLink : Label
    {
        readonly Color _resting;

        public TextLink(string text, Color color)
        {
            Text = text;
            _resting = color;
            ForeColor = color;
            Font = Theme.Small;
            BackColor = Color.Transparent;
            AutoSize = true;
            Cursor = Cursors.Hand;

            MouseEnter += delegate
            {
                Font = new Font(Theme.Small, FontStyle.Underline);
                ForeColor = ControlPaint.Dark(_resting, 0.15f);
            };
            MouseLeave += delegate
            {
                Font = Theme.Small;
                ForeColor = _resting;
            };
        }
    }
}
