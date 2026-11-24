using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RWDE.UI.Windows
{
    /// <summary>
    /// The cycle length slider: two handles on one track, the range between them highlighted, on the
    /// piecewise scale of <see cref="CycleSliderScale"/>. Click or drag a handle; the arrow keys move
    /// the last one touched (Page Up/Down for bigger steps) and Space switches handles.
    /// </summary>
    public sealed class CycleRangeSlider : Control
    {
        // Sizes follow the monitor DPI; the height also follows the font so the scale labels fit.
        int HandleRadius => LogicalToDeviceUnits(8);
        int TrackInset => HandleRadius + LogicalToDeviceUnits(4);
        int TrackThickness => Math.Max(2, LogicalToDeviceUnits(4));

        int low = Configuration.Settings.GameCycleTimeMin;
        int high = Configuration.Settings.GameCycleTimeMax;
        bool dragging;
        bool highActive;

        public CycleRangeSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            TabStop = true;
            FitHeight();
            MinimumSize = new Size(LogicalToDeviceUnits(120), 0);
        }

        /// <summary>Raised while a handle moves (drag or key).</summary>
        public event EventHandler ValuesChanged;

        /// <summary>Raised when a move is finished: mouse released or key pressed.</summary>
        public event EventHandler ValuesCommitted;

        /// <summary>The shorter cycle length, in seconds.</summary>
        public int LowSeconds => low;

        /// <summary>The longer cycle length, in seconds.</summary>
        public int HighSeconds => high;

        /// <summary>Sets both values without raising events (for loading settings).</summary>
        public void SetValues(int lowSeconds, int highSeconds)
        {
            lowSeconds = Math.Clamp(lowSeconds, CycleSliderScale.Min, CycleSliderScale.Max);
            highSeconds = Math.Clamp(highSeconds, CycleSliderScale.Min, CycleSliderScale.Max);
            low = Math.Min(lowSeconds, highSeconds);
            high = Math.Max(lowSeconds, highSeconds);
            Invalidate();
        }

        Rectangle Track => new Rectangle(TrackInset, TrackInset - TrackThickness / 2, Math.Max(1, Width - 2 * TrackInset), TrackThickness);

        int XFor(int seconds) => Track.Left + (int)Math.Round(CycleSliderScale.ToFraction(seconds) * Track.Width);

        double FractionAt(int x) => (double)(x - Track.Left) / Track.Width;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            var track = Track;

            using (var trackBrush = new SolidBrush(SystemColors.ControlDark))
                g.FillRectangle(trackBrush, track);

            int lx = XFor(low), hx = XFor(high);
            using (var rangeBrush = new SolidBrush(Enabled ? SystemColors.Highlight : SystemColors.GrayText))
                g.FillRectangle(rangeBrush, lx, track.Top - 1, Math.Max(1, hx - lx), track.Height + 2);

            // Scale marks: the ends and the 1 h point where the scale changes.
            int hourX = track.Left + (int)Math.Round(CycleSliderScale.HourMark * track.Width);
            using (var pen = new Pen(SystemColors.ControlDarkDark))
            using (var textBrush = new SolidBrush(SystemColors.GrayText))
            {
                int markTop = track.Bottom + HandleRadius - 2;
                foreach (var (x, label, align) in new[]
                {
                    (track.Left, "1 min", StringAlignment.Near),
                    (hourX, "1 h", StringAlignment.Center),
                    (track.Right, "23 h 59 min", StringAlignment.Far),
                })
                {
                    g.DrawLine(pen, x, markTop, x, markTop + 3);
                    var format = new StringFormat { Alignment = align };
                    float anchor = align == StringAlignment.Near ? x - HandleRadius : align == StringAlignment.Far ? x + HandleRadius : x;
                    g.DrawString(label, Font, textBrush, anchor, markTop + 3, format);
                }
            }

            DrawHandle(g, lx, track, !highActive && Focused);
            DrawHandle(g, hx, track, highActive && Focused);
        }

        void DrawHandle(Graphics g, int x, Rectangle track, bool focused)
        {
            var rect = new Rectangle(x - HandleRadius, track.Top + track.Height / 2 - HandleRadius, HandleRadius * 2, HandleRadius * 2);
            using (var fill = new SolidBrush(Enabled ? SystemColors.Window : SystemColors.Control))
                g.FillEllipse(fill, rect);
            using (var border = new Pen(Enabled ? SystemColors.Highlight : SystemColors.GrayText, focused ? 3f : 2f))
                g.DrawEllipse(border, rect);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            int lx = XFor(low), hx = XFor(high);
            int dl = Math.Abs(e.X - lx), dh = Math.Abs(e.X - hx);
            // On a tie (handles on top of each other) pick by which side was clicked.
            if (dl != dh) highActive = dh < dl;
            else if (high == CycleSliderScale.Max) highActive = false;
            else if (low == CycleSliderScale.Min) highActive = true;
            else highActive = e.X >= hx;
            dragging = true;
            Capture = true;
            MoveActiveTo(CycleSliderScale.ToSeconds(FractionAt(e.X)));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) MoveActiveTo(CycleSliderScale.ToSeconds(FractionAt(e.X)));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            EndDrag();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            // Capture lost mid-drag (the button released over another app, a dialog popping up): the
            // MouseUp will never come, so end the drag where the handle is instead of letting it follow
            // the cursor with no button held.
            EndDrag();
        }

        /// <summary>Ends a drag and commits the values; does nothing when no drag is running.</summary>
        void EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            Capture = false;
            ValuesCommitted?.Invoke(this, EventArgs.Empty);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
                case Keys.PageUp: case Keys.PageDown: case Keys.Home: case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int current = highActive ? high : low;
            int next;
            switch (e.KeyCode)
            {
                case Keys.Right: case Keys.Up: next = CycleSliderScale.Step(current, +1, false); break;
                case Keys.Left: case Keys.Down: next = CycleSliderScale.Step(current, -1, false); break;
                case Keys.PageUp: next = CycleSliderScale.Step(current, +1, true); break;
                case Keys.PageDown: next = CycleSliderScale.Step(current, -1, true); break;
                case Keys.Home: next = CycleSliderScale.Min; break;
                case Keys.End: next = CycleSliderScale.Max; break;
                case Keys.Space: highActive = !highActive; Invalidate(); e.Handled = true; return;
                default: return;
            }
            e.Handled = true;
            if (MoveActiveTo(next)) ValuesCommitted?.Invoke(this, EventArgs.Empty);
        }

        void FitHeight() => Height = TrackInset + HandleRadius + 3 + Font.Height + LogicalToDeviceUnits(6);

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); FitHeight(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); FitHeight(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        /// <summary>Moves the active handle, never past the other one. Returns true when a value changed.</summary>
        bool MoveActiveTo(int seconds)
        {
            int newLow = low, newHigh = high;
            if (highActive) newHigh = Math.Max(seconds, low);
            else newLow = Math.Min(seconds, high);
            if (newLow == low && newHigh == high) return false;
            low = newLow;
            high = newHigh;
            Invalidate();
            ValuesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}
