using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenInSheets
{
    static class Ui
    {
        public const string Title = "Open in Sheets";

        public static void Error(string message)
        {
            MessageBox.Show(message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void Info(string message)
        {
            MessageBox.Show(message, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static bool Confirm(string message)
        {
            return MessageBox.Show(message, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                   == DialogResult.Yes;
        }
    }

    /// <summary>
    /// Shown while the upload runs. A second or two of nothing happening reads as
    /// "it's broken" to anyone who isn't expecting a network round trip.
    /// </summary>
    class SplashForm : Form
    {
        readonly Timer _timer;
        int _offset;

        const int TrackLeft = 24;
        const int TrackTop = 62;
        const int TrackHeight = 4;
        const int SegmentWidth = 110;

        public SplashForm(string message)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(400, 100);
            BackColor = Theme.Surface;
            TopMost = true;
            ShowInTaskbar = false;
            ControlBox = false;
            DoubleBuffered = true;

            using (GraphicsPath path = Theme.Rounded(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), 12))
                Region = new Region(path);

            Controls.Add(new Mark
            {
                Location = new Point(24, 24),
                Size = new Size(28, 28),
                BackColor = Theme.Surface
            });
            Controls.Add(Theme.Text(message, Theme.Body, Theme.Ink, 64, 30, 310, 20));

            _timer = new Timer();
            _timer.Interval = 16;
            _timer.Tick += delegate
            {
                _offset += 7;
                if (_offset > ClientSize.Width - TrackLeft * 2 + SegmentWidth) _offset = 0;
                Invalidate(new Rectangle(TrackLeft, TrackTop, ClientSize.Width - TrackLeft * 2, TrackHeight));
            };
            _timer.Start();

            FormClosed += delegate { _timer.Stop(); _timer.Dispose(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackWidth = ClientSize.Width - TrackLeft * 2;
            Theme.Panel(g, new Rectangle(TrackLeft, TrackTop, trackWidth, TrackHeight),
                        TrackHeight / 2, Theme.ControlFill, Color.Empty);

            // Indeterminate segment sliding left to right, clipped to the track.
            int x = TrackLeft + _offset - SegmentWidth;
            int start = Math.Max(x, TrackLeft);
            int end = Math.Min(x + SegmentWidth, TrackLeft + trackWidth);
            if (end > start)
                Theme.Panel(g, new Rectangle(start, TrackTop, end - start, TrackHeight),
                            TrackHeight / 2, Theme.Accent, Color.Empty);

            // Hairline edge, since a borderless white window otherwise floats with no shape.
            Theme.Panel(g, new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1),
                        12, Color.Transparent, Theme.Line);
        }
    }
}
