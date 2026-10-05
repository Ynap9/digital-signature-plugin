using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ks.plugin.external.Tray.Implements
{
    internal static class Shapes
    {
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal class RoundedPanel : Panel
    {
        private readonly int _radius;
        private readonly Color _fill;
        private readonly Color _border;

        public RoundedPanel(int radius, Color fill, Color border, Color background)
        {
            _radius = radius;
            _fill = fill;
            _border = border;
            BackColor = background;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Shapes.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), _radius);
            using var brush = new SolidBrush(_fill);
            using var pen = new Pen(_border);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }
    }

    internal class RoundedButton : Button
    {
        private readonly Color _fill;
        private readonly Color _hoverFill;
        private readonly Color _border;
        private bool _isHovering;

        public RoundedButton(string text, Color fill, Color hoverFill, Color border, Color foreground)
        {
            _fill = fill;
            _hoverFill = hoverFill;
            _border = border;
            Text = text;
            ForeColor = foreground;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI Semibold", 9F);
            var textSize = TextRenderer.MeasureText(text, Font);
            Size = new Size(Math.Max(88, textSize.Width + 36), 34);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovering = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Shapes.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8);
            using var brush = new SolidBrush(_isHovering ? _hoverFill : _fill);
            using var pen = new Pen(_border);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
