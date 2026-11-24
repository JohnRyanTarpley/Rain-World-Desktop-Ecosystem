using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace RWDE.UI.Windows
{
    /// <summary>
    /// A button that must be held (mouse or Space) for <see cref="HoldToConfirm.Duration"/> before it
    /// acts. It fills from left to right while held; letting go early empties it and does nothing.
    /// </summary>
    public sealed class HoldButton : Control
    {
        readonly HoldToConfirm hold;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Timer timer = new Timer { Interval = 30 };

        public HoldButton(TimeSpan duration)
        {
            hold = new HoldToConfirm(duration);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            TabStop = true;
            Size = new Size(220, 32);
            timer.Tick += (s, e) => Tick();
        }

        /// <summary>Raised once when the hold completes.</summary>
        public event EventHandler Confirmed;

        public double Progress => hold.Progress;

        void Begin()
        {
            if (!Enabled) return;
            hold.Press(clock.Elapsed);
            timer.Start();
            Invalidate();
        }

        void Cancel()
        {
            hold.Release();
            timer.Stop();
            Invalidate();
        }

        void Tick()
        {
            if (hold.Update(clock.Elapsed)) Confirmed?.Invoke(this, EventArgs.Empty);
            if (!hold.IsHolding) timer.Stop();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            Capture = true;
            Begin();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Capture = false;
            Cancel();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && hold.IsHolding) Cancel();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space) { Begin(); e.Handled = true; }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode == Keys.Space) { Cancel(); e.Handled = true; }
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Cancel();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled) Cancel();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var back = new SolidBrush(SystemColors.Control)) g.FillRectangle(back, rect);
            int fill = (int)Math.Round(hold.Progress * rect.Width);
            if (fill > 0)
                using (var bar = new SolidBrush(Color.FromArgb(200, 70, 60))) g.FillRectangle(bar, 0, 0, fill, Height);
            using (var border = new Pen(Focused ? SystemColors.Highlight : SystemColors.ControlDark, Focused ? 2f : 1f))
                g.DrawRectangle(border, rect);

            string label = Text;
            if (hold.IsHolding)
            {
                var left = hold.Duration.TotalSeconds * (1 - hold.Progress);
                label = $"Keep holding… {left:0.0} s";
            }
            TextRenderer.DrawText(g, label, Font, rect, Enabled ? SystemColors.ControlText : SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
