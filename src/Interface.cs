using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PortPilot {
    internal static class Surface {
        public static GraphicsPath Round(RectangleF r, float radius) {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
    }
    public class SoftPanel : Panel {
        public Color Fill = Color.White;
        public Color Outline = Color.FromArgb(226, 228, 233);
        public int Radius = 14;
        public SoftPanel() { DoubleBuffered = true; ResizeRedraw = true; BackColor = Color.White; }
        protected override void OnPaintBackground(PaintEventArgs e) {
            e.Graphics.Clear(Parent == null ? Color.FromArgb(245, 245, 247) : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96f;
            using (var path = Surface.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), Radius * s))
            using (var fill = new SolidBrush(Fill)) { e.Graphics.FillPath(fill, path); }
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Surface.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), Radius * DeviceDpi / 96f))
            using (var pen = new Pen(Outline)) { e.Graphics.DrawPath(pen, path); }
        }
    }
    public class SoftButton : Button {
        public bool Primary;
        public bool Selector;
        bool hovering;
        public SoftButton() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.White;
            ForeColor = Color.FromArgb(37, 39, 45); Cursor = Cursors.Hand; UseVisualStyleBackColor = false;
        }
        protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e) {
            float s = DeviceDpi / 96f;
            e.Graphics.Clear(Parent == null ? Color.FromArgb(245, 245, 247) : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = !Enabled ? Color.FromArgb(239, 240, 243) : Primary ? (hovering ? Color.FromArgb(0, 100, 218) : Color.FromArgb(0, 113, 227)) : hovering ? Color.FromArgb(248, 249, 251) : Color.White;
            Color foreground = !Enabled ? Color.FromArgb(145, 149, 158) : Primary ? Color.White : ForeColor;
            using (var path = Surface.Round(new RectangleF(s, s, Width - 2 * s, Height - 2 * s), 9 * s))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(Focused ? Color.FromArgb(0, 113, 227) : Primary && Enabled ? fill : Color.FromArgb(215, 218, 224), Focused ? 2 * s : s)) {
                e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path);
            }
            Rectangle text = Selector ? new Rectangle((int)(13 * s), 0, Width - (int)(43 * s), Height) : new Rectangle((int)(8 * s), 0, Width - (int)(16 * s), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, text, foreground, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | (Selector ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter));
            if (Selector) using (var pen = new Pen(Color.FromArgb(117, 122, 133), 1.5f * s)) {
                float x = Width - 20 * s, y = Height / 2f;
                e.Graphics.DrawLines(pen, new[] { new PointF(x - 3 * s, y - 2 * s), new PointF(x, y + 1 * s), new PointF(x + 3 * s, y - 2 * s) });
            }
        }
    }
    // All selectors use the same 40px surface as search and buttons.
    // Standard menus retain keyboard navigation and screen-reader labels.
    public sealed class SelectField : SoftButton {
        public readonly List<object> Items = new List<object>();
        int selected = -1;
        public event EventHandler SelectedIndexChanged;
        public int SelectedIndex {
            get { return selected; }
            set { if (value < 0 || value >= Items.Count || selected == value) return; selected = value; Text = Convert.ToString(Items[value]); Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }
        }
        public object SelectedItem { get { return selected < 0 ? null : Items[selected]; } }
        public SelectField() { Selector = true; AccessibleRole = AccessibleRole.ComboBox; }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Home || keyData == Keys.End || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e) {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up || e.KeyCode == Keys.Home || e.KeyCode == Keys.End) {
                SelectedIndex = e.KeyCode == Keys.Home ? 0 : e.KeyCode == Keys.End ? Items.Count - 1 : Math.Max(0, Math.Min(Items.Count - 1, selected + (e.KeyCode == Keys.Down ? 1 : -1)));
                e.Handled = true; e.SuppressKeyPress = true; return;
            }
            base.OnKeyDown(e);
        }
        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            var menu = new ContextMenuStrip { ShowImageMargin = false, Font = Font, AutoSize = true, BackColor = Color.White, Padding = new Padding(5) };
            for (int i = 0; i < Items.Count; i++) {
                int index = i;
                var item = new ToolStripMenuItem(Convert.ToString(Items[i])) { Checked = i == selected, AutoSize = false, Width = Math.Max(Width - 12, 175), Height = (int)(32 * DeviceDpi / 96f) };
                item.Click += delegate { SelectedIndex = index; Focus(); }; menu.Items.Add(item);
            }
            // Item clicks finish AFTER Closed; dispose on the next UI cycle.
            menu.Closed += delegate { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)delegate { menu.Dispose(); }); else menu.Dispose(); };
            menu.Show(this, new Point(0, Height + 4));
        }
    }
    public sealed class MetricCard : SoftPanel {
        public string Caption;
        public string Description;
        public Color Accent;
        int count;
        public int Count { get { return count; } set { count = value; Invalidate(); AccessibleName = Caption + ": " + value + ". " + Description; } }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); float s = DeviceDpi / 96f;
            using (var large = new Font("Segoe UI", 23, FontStyle.Regular))
            using (var title = new Font("Segoe UI", 10, FontStyle.Regular))
            using (var small = new Font("Segoe UI", 8.5f, FontStyle.Regular)) {
                TextRenderer.DrawText(e.Graphics, count.ToString(), large, new Point((int)(18 * s), (int)(7 * s)), Color.FromArgb(30, 32, 38));
                TextRenderer.DrawText(e.Graphics, Caption, title, new Point((int)(20 * s), (int)(49 * s)), Color.FromArgb(48, 52, 61));
                TextRenderer.DrawText(e.Graphics, Description, small, new Point((int)(20 * s), (int)(70 * s)), Color.FromArgb(120, 125, 135));
            }
            using (var brush = new SolidBrush(Accent)) { e.Graphics.FillEllipse(brush, Width - 32 * s, 22 * s, 9 * s, 9 * s); }
        }
    }
}
