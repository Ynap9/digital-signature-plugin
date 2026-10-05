using ks.plugin.external.Logging.Interfaces;
using ks.plugin.external.Tray.Interfaces;
using ks.plugin.shared.Constants;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace ks.plugin.external.Tray.Implements
{
    public class StatusWindow : Form, IStatusWindow
    {
        private const int MaxDisplayedLines = 1000;
        private const int FlushIntervalMs = 300;
        private const int CardRadius = 12;

        private static readonly Color AccentStart = Color.FromArgb(37, 99, 235);
        private static readonly Color AccentEnd = Color.FromArgb(29, 78, 216);
        private static readonly Color AccentHover = Color.FromArgb(30, 64, 175);
        private static readonly Color Surface = Color.FromArgb(241, 244, 249);
        private static readonly Color Card = Color.White;
        private static readonly Color Border = Color.FromArgb(222, 228, 238);
        private static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        private static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
        private static readonly Color TextSubtle = Color.FromArgb(148, 163, 184);
        private static readonly Color Success = Color.FromArgb(22, 163, 74);

        private readonly ILogBuffer _logBuffer;
        private readonly ConcurrentQueue<string> _pendingLines = new();
        private readonly LogGrid _logGrid;
        private readonly Label _environmentValue = new();
        private readonly System.Windows.Forms.Timer _flushTimer;
        private readonly Bitmap _logo;
        private bool _isStale = true;

        public StatusWindow(Icon icon, ILogBuffer logBuffer)
        {
            _logBuffer = logBuffer;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = PluginConstants.Ten;
            Icon = icon;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(860, 600);
            MinimumSize = new Size(680, 500);
            Font = new Font("Segoe UI", 9F);
            BackColor = Surface;
            ForeColor = TextPrimary;
            DoubleBuffered = true;

            _logo = new Icon(icon, 48, 48).ToBitmap();
            var monoFamily = FontFamily.Families.Any(f => f.Name == "Cascadia Mono") ? "Cascadia Mono" : "Consolas";
            _logGrid = new LogGrid(monoFamily);

            Controls.Add(BuildLogSection());
            Controls.Add(BuildActions());
            Controls.Add(BuildStatusBar());
            Controls.Add(BuildCards());
            Controls.Add(BuildHeader());

            _logBuffer.LineAdded += line => _pendingLines.Enqueue(line);

            _flushTimer = new System.Windows.Forms.Timer { Interval = FlushIntervalMs };
            _flushTimer.Tick += (_, _) => FlushPendingLines();
            _flushTimer.Start();

            _ = Handle;
        }

        public void SetEnvironment(string environmentName)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => SetEnvironment(environmentName));
                return;
            }

            _environmentValue.Text = environmentName;
        }

        public void ShowWindow()
        {
            if (InvokeRequired)
            {
                BeginInvoke(ShowWindow);
                return;
            }

            if (_isStale)
            {
                ReloadLog();
            }

            Show();
            _logGrid.ScrollToEnd();
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            Activate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Only a real shutdown may dispose the form: the tray keeps calling ShowWindow on it.
            if (e.CloseReason is not (CloseReason.WindowsShutDown or CloseReason.ApplicationExitCall))
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _flushTimer.Dispose();
                _logo.Dispose();
            }

            base.Dispose(disposing);
        }

        private Panel BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 92 };
            var chipText = $"v{PluginConstants.PhienBan}";
            var chipFont = new Font("Segoe UI Semibold", 9F);

            header.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new LinearGradientBrush(header.ClientRectangle, AccentStart, AccentEnd, LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(brush, header.ClientRectangle);
                }

                using (var logoPath = Shapes.RoundedRect(new Rectangle(24, 18, 56, 56), 14))
                {
                    g.FillPath(Brushes.White, logoPath);
                }
                g.DrawImage(_logo, 28, 22, 48, 48);

                var chipSize = TextRenderer.MeasureText(chipText, chipFont);
                var chip = new Rectangle(header.Width - chipSize.Width - 44, 30, chipSize.Width + 20, chipSize.Height + 8);
                using (var chipPath = Shapes.RoundedRect(chip, chip.Height / 2))
                using (var chipBrush = new SolidBrush(Color.FromArgb(48, 255, 255, 255)))
                {
                    g.FillPath(chipBrush, chipPath);
                }
                TextRenderer.DrawText(g, chipText, chipFont, chip, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            header.Resize += (_, _) => header.Invalidate();
            header.Disposed += (_, _) => chipFont.Dispose();

            header.Controls.Add(new Label
            {
                Text = PluginConstants.Ten,
                Font = new Font("Segoe UI Semibold", 16F),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(94, 18),
            });
            header.Controls.Add(new Label
            {
                Text = "Cầu nối an toàn giữa trang web ký số và USB token",
                ForeColor = Color.FromArgb(219, 234, 254),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(96, 54),
            });

            return header;
        }

        private Control BuildCards()
        {
            var cards = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 100,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(18, 16, 8, 2),
                BackColor = Surface,
            };
            cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            for (var i = 0; i < 4; i++)
            {
                cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }

            cards.Controls.Add(BuildCard("TRẠNG THÁI", "●  Đang chạy", Success, null), 0, 0);
            cards.Controls.Add(BuildCard("ĐỊA CHỈ", $"127.0.0.1:{PluginConstants.Port}", TextPrimary, null), 1, 0);
            cards.Controls.Add(BuildCard("MÔI TRƯỜNG", "Production", TextPrimary, _environmentValue), 2, 0);
            cards.Controls.Add(BuildCard("KHỞI ĐỘNG LÚC", DateTime.Now.ToString("HH:mm · dd/MM/yyyy"), TextPrimary, null), 3, 0);
            return cards;
        }

        private static Control BuildCard(string caption, string value, Color valueColor, Label? valueLabel)
        {
            var card = new RoundedPanel(CardRadius, Card, Border, Surface)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 10, 0),
                Padding = new Padding(14, 12, 10, 8),
            };

            valueLabel ??= new Label();
            valueLabel.Text = value;
            valueLabel.Font = new Font("Segoe UI Semibold", 11F);
            valueLabel.ForeColor = valueColor;
            valueLabel.BackColor = Card;
            valueLabel.Dock = DockStyle.Top;
            valueLabel.Height = 28;
            valueLabel.AutoEllipsis = true;

            card.Controls.Add(valueLabel);
            card.Controls.Add(new Label
            {
                Text = caption,
                Font = new Font("Segoe UI Semibold", 7.5F),
                ForeColor = TextMuted,
                BackColor = Card,
                Dock = DockStyle.Top,
                Height = 20,
            });
            return card;
        }

        private Control BuildLogSection()
        {
            var titleRow = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Card };
            titleRow.Controls.Add(new Label
            {
                Text = "Nhật ký hoạt động",
                Font = new Font("Segoe UI Semibold", 10.5F),
                ForeColor = TextPrimary,
                BackColor = Card,
                AutoSize = true,
                Location = new Point(2, 0),
            });
            titleRow.Controls.Add(new Label
            {
                Text = $"{MaxDisplayedLines} dòng gần nhất · chỉ lưu trong bộ nhớ, không ghi xuống đĩa",
                Font = new Font("Segoe UI", 8F),
                ForeColor = TextSubtle,
                BackColor = Card,
                AutoSize = true,
                Location = new Point(3, 22),
            });

            var logCard = new RoundedPanel(CardRadius, Card, Border, Surface)
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 12, 10, 10),
            };
            logCard.Controls.Add(_logGrid);
            logCard.Controls.Add(_logGrid.CreateHeader());
            logCard.Controls.Add(titleRow);

            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 0), BackColor = Surface };
            host.Controls.Add(logCard);
            return host;
        }

        private Control BuildActions()
        {
            var actions = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Surface, Padding = new Padding(18, 13, 18, 13) };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                WrapContents = false,
                BackColor = Surface,
            };
            var copyButton = new RoundedButton("Sao chép nhật ký", Card, Surface, Border, TextPrimary)
            {
                Margin = new Padding(0, 0, 8, 0),
            };
            copyButton.Click += (_, _) => CopyLog();
            var hideButton = new RoundedButton("Ẩn", AccentEnd, AccentHover, AccentEnd, Color.White)
            {
                Margin = new Padding(0),
            };
            hideButton.Click += (_, _) => Hide();
            buttons.Controls.Add(copyButton);
            buttons.Controls.Add(hideButton);

            actions.Controls.Add(buttons);
            return actions;
        }

        private static Control BuildStatusBar()
        {
            var entry = Assembly.GetEntryAssembly();
            var copyright = entry?.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
            var company = entry?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
            var fileVersion = entry?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? PluginConstants.PhienBan;

            var bar = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Card, Padding = new Padding(20, 0, 20, 0) };
            bar.Paint += (_, e) =>
            {
                using var pen = new Pen(Border);
                e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
            };

            var font = new Font("Segoe UI", 8.25F);
            bar.Controls.Add(new Label
            {
                Text = $"{copyright}. Bảo lưu mọi quyền.",
                Font = font,
                ForeColor = TextMuted,
                BackColor = Card,
                Dock = DockStyle.Left,
                AutoSize = true,
                Padding = new Padding(0, 10, 0, 0),
            });
            bar.Controls.Add(new Label
            {
                Text = $"{PluginConstants.Ten} {PluginConstants.PhienBan}  ·  Bản dựng {fileVersion}  ·  Phát hành bởi {company}",
                Font = font,
                ForeColor = TextSubtle,
                BackColor = Card,
                Dock = DockStyle.Right,
                AutoSize = true,
                Padding = new Padding(0, 10, 0, 0),
            });
            return bar;
        }

        private void FlushPendingLines()
        {
            if (_pendingLines.IsEmpty)
            {
                return;
            }

            if (!Visible)
            {
                _pendingLines.Clear();
                _isStale = true;
                return;
            }

            var followTail = _logGrid.IsScrolledToBottom;
            _logGrid.BeginUpdate();
            while (_pendingLines.TryDequeue(out var line))
            {
                AddLine(line);
            }

            while (_logGrid.Items.Count > MaxDisplayedLines)
            {
                _logGrid.Items.RemoveAt(0);
            }

            if (followTail)
            {
                _logGrid.ScrollToEnd();
            }
            _logGrid.EndUpdate();
        }

        private void ReloadLog()
        {
            _pendingLines.Clear();
            _logGrid.BeginUpdate();
            _logGrid.Items.Clear();
            foreach (var line in _logBuffer.GetLines())
            {
                AddLine(line);
            }
            _logGrid.ScrollToEnd();
            _logGrid.EndUpdate();
            _isStale = false;
        }

        /// <summary>Expects the LogBufferProvider format: "HH:mm:ss lvl Category: message".</summary>
        private void AddLine(string line)
        {
            if (line.Length < 15 || line[8] != ' ' || line[13] != ' ')
            {
                _logGrid.AddEntry(new LogEntry(string.Empty, LogLevelKind.Debug, string.Empty, line, line));
                return;
            }

            var level = line.Substring(9, 4) switch
            {
                "info" => LogLevelKind.Info,
                "warn" => LogLevelKind.Warn,
                "fail" or "crit" => LogLevelKind.Error,
                _ => LogLevelKind.Debug,
            };

            var rest = line[14..];
            var separator = rest.IndexOf(": ", StringComparison.Ordinal);
            var source = separator > 0 ? rest[..separator] : string.Empty;
            var message = separator > 0 ? rest[(separator + 2)..] : rest;

            _logGrid.AddEntry(new LogEntry(line[..8], level, source, message, line));
        }

        private void CopyLog()
        {
            var lines = _logBuffer.GetLines();
            if (lines.Count > 0)
            {
                Clipboard.SetText(string.Join(Environment.NewLine, lines));
            }
        }
    }
}
