using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ks.plugin.external.Tray.Implements
{
    internal enum LogLevelKind
    {
        Debug,
        Info,
        Warn,
        Error,
    }

    internal sealed record LogEntry(string Time, LogLevelKind Level, string Source, string Message, string RawLine);

    /// <summary>
    /// Fully owner-drawn log list. DataGridView was dropped because its stock painter keeps adding 3D cell
    /// borders on partial repaints, so the grid lines flickered in and out.
    /// </summary>
    internal class LogGrid : ListBox
    {
        private const int TimeX = 8;
        private const int LevelX = 92;
        private const int SourceX = 168;
        private const int MessageX = 326;
        private const int RowHeight = 28;

        private static readonly Color Card = Color.White;
        private static readonly Color RowLine = Color.FromArgb(241, 245, 249);
        private static readonly Color Selection = Color.FromArgb(239, 246, 255);
        private static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        private static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
        private static readonly Color TextSubtle = Color.FromArgb(148, 163, 184);

        private const TextFormatFlags CellFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

        private readonly Font _monoFont;
        private readonly Font _textFont = new("Segoe UI", 9F);
        private readonly Font _badgeFont = new("Segoe UI Semibold", 7.5F);
        private readonly Font _headerFont = new("Segoe UI Semibold", 7.5F);

        public LogGrid(string monoFamily)
        {
            _monoFont = new Font(monoFamily, 8.5F);
            Dock = DockStyle.Fill;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = RowHeight;
            BorderStyle = BorderStyle.None;
            IntegralHeight = false;
            SelectionMode = SelectionMode.MultiExtended;
            BackColor = Card;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        public bool IsScrolledToBottom
        {
            get
            {
                var visible = Math.Max(1, ClientSize.Height / ItemHeight);
                return Items.Count == 0 || TopIndex + visible >= Items.Count - 1;
            }
        }

        public Control CreateHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = LogicalToDeviceUnits(30), BackColor = Card };
            header.Paint += (_, e) =>
            {
                var height = header.Height - 1;
                DrawHeaderText(e.Graphics, "THỜI GIAN", TimeX, LevelX, height);
                DrawHeaderText(e.Graphics, "MỨC", LevelX, SourceX, height);
                DrawHeaderText(e.Graphics, "NGUỒN", SourceX, MessageX, height);
                TextRenderer.DrawText(e.Graphics, "NỘI DUNG", _headerFont,
                    new Rectangle(Scale(MessageX), 0, header.Width - Scale(MessageX), height), TextSubtle, CellFlags);
                using var line = new Pen(RowLine);
                e.Graphics.DrawLine(line, 0, height, header.Width, height);
            };
            header.Resize += (_, _) => header.Invalidate();
            return header;
        }

        public void AddEntry(LogEntry entry) => Items.Add(entry);

        public void ScrollToEnd()
        {
            if (Items.Count > 0)
            {
                TopIndex = Items.Count - 1;
            }
        }

        public string GetSelectedText() =>
            string.Join(Environment.NewLine, SelectedItems.Cast<LogEntry>().Select(x => x.RawLine));

        public static (Color Text, Color Badge) GetLevelColors(LogLevelKind level) => level switch
        {
            LogLevelKind.Info => (Color.FromArgb(29, 78, 216), Color.FromArgb(219, 234, 254)),
            LogLevelKind.Warn => (Color.FromArgb(180, 83, 9), Color.FromArgb(254, 243, 199)),
            LogLevelKind.Error => (Color.FromArgb(185, 28, 28), Color.FromArgb(254, 226, 226)),
            _ => (Color.FromArgb(71, 85, 105), Color.FromArgb(241, 245, 249)),
        };

        private static string GetLevelText(LogLevelKind level) => level switch
        {
            LogLevelKind.Info => "INFO",
            LogLevelKind.Warn => "WARN",
            LogLevelKind.Error => "ERROR",
            _ => "DEBUG",
        };

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count || Items[e.Index] is not LogEntry entry)
            {
                return;
            }

            var g = e.Graphics;
            var bounds = e.Bounds;
            var isSelected = (e.State & DrawItemState.Selected) != 0;

            using (var background = new SolidBrush(isSelected ? Selection : Card))
            {
                g.FillRectangle(background, bounds);
            }

            var height = bounds.Height - 1;
            TextRenderer.DrawText(g, entry.Time, _monoFont, Cell(bounds, TimeX, LevelX, height), TextSubtle, CellFlags);
            DrawBadge(g, bounds, entry.Level);
            TextRenderer.DrawText(g, entry.Source, _textFont, Cell(bounds, SourceX, MessageX, height), TextMuted, CellFlags);

            var messageColor = entry.Level is LogLevelKind.Warn or LogLevelKind.Error
                ? GetLevelColors(entry.Level).Text
                : TextPrimary;
            var messageBounds = new Rectangle(bounds.X + Scale(MessageX), bounds.Y, bounds.Width - Scale(MessageX) - Scale(8), height);
            TextRenderer.DrawText(g, entry.Message, _textFont, messageBounds, messageColor, CellFlags);

            using var line = new Pen(RowLine);
            g.DrawLine(line, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C && SelectedItems.Count > 0)
            {
                Clipboard.SetText(GetSelectedText());
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _monoFont.Dispose();
                _textFont.Dispose();
                _badgeFont.Dispose();
                _headerFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private int Scale(int logical) => LogicalToDeviceUnits(logical);

        private Rectangle Cell(Rectangle row, int from, int to, int height) =>
            new(row.X + Scale(from), row.Y, Scale(to - from) - Scale(8), height);

        private void DrawHeaderText(Graphics g, string text, int from, int to, int height) =>
            TextRenderer.DrawText(g, text, _headerFont, new Rectangle(Scale(from), 0, Scale(to - from), height), TextSubtle, CellFlags);

        private void DrawBadge(Graphics g, Rectangle row, LogLevelKind level)
        {
            var text = GetLevelText(level);
            var (foreground, badge) = GetLevelColors(level);
            var size = TextRenderer.MeasureText(text, _badgeFont);
            var pillHeight = Scale(18);
            var pill = new Rectangle(row.X + Scale(LevelX), row.Y + (row.Height - pillHeight) / 2, size.Width + Scale(10), pillHeight);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Shapes.RoundedRect(pill, pillHeight / 2))
            using (var brush = new SolidBrush(badge))
            {
                g.FillPath(brush, path);
            }
            g.SmoothingMode = SmoothingMode.Default;
            TextRenderer.DrawText(g, text, _badgeFont, pill, foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
