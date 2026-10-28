using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MediaPort
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(12, 18, 29);
        private static readonly Color Panel = Color.FromArgb(20, 29, 44);
        private static readonly Color Panel2 = Color.FromArgb(27, 38, 56);
        private static readonly Color Border = Color.FromArgb(48, 63, 85);
        private static readonly Color TextMain = Color.FromArgb(241, 245, 249);
        private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
        private static readonly Color Accent = Color.FromArgb(45, 212, 191);
        private static readonly Color AccentDark = Color.FromArgb(19, 150, 135);
        private static readonly Color Danger = Color.FromArgb(248, 113, 113);

        private TextBox _urls;
        private TextBox _destination;
        private ComboBox _mode;
        private ComboBox _quality;
        private ComboBox _audioFormat;
        private ComboBox _cookies;
        private CheckBox _playlist;
        private CheckBox _subtitles;
        private CheckBox _thumbnail;
        private CheckBox _metadata;
        private CheckBox _sponsorBlock;
        private CheckBox _openFolder;
        private Button _advancedButton;
        private AdvancedOptionsDialog _advancedDialog;
        private List<string> _advancedArguments = new List<string>();
        private Button _download;
        private Button _cancel;
        private ProgressBar _progress;
        private Label _status;
        private Label _metrics;
        private RichTextBox _log;
        private Panel _formatCard;
        private Process _process;
        private CancellationTokenSource _cancelSource;
        private string _toolFolder;
        private bool _busy;
        private int _playlistCount;
        private int _playlistIndex;
        private int _lastLoggedPlaylistIndex;

        private sealed class LinkCheckResult
        {
            public bool IsValid;
            public string Title;
            public string Error;
        }

        public MainForm()
        {
            Text = "MediaPort";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(940, 700);
            Size = new Size(1080, 780);
            BackColor = Bg;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = CreateAppIcon();
            BuildInterface();
            Shown += async delegate { await PrepareToolsAsync(); };
            FormClosing += OnFormClosing;
        }

        private void BuildInterface()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Bg,
                Padding = new Padding(28, 22, 28, 22),
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Bg,
                Margin = new Padding(0, 6, 0, 12)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            content.Controls.Add(BuildInputCard(), 0, 0);
            content.Controls.Add(BuildSettingsCard(), 1, 0);
            root.Controls.Add(content, 0, 1);

            root.Controls.Add(BuildActionBar(), 0, 2);
            root.Controls.Add(BuildLogCard(), 0, 3);
        }

        private Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
            var logo = new LogoControl { Location = new Point(2, 12), Size = new Size(56, 56) };
            var title = new Label
            {
                Text = "MediaPort",
                ForeColor = TextMain,
                Font = new Font("Segoe UI Semibold", 22f),
                AutoSize = true,
                Location = new Point(74, 8)
            };
            var subtitle = new Label
            {
                Text = "Video, audio and playlists — without the command line",
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(77, 51)
            };
            var badge = new Label
            {
                Text = "  yt-dlp powered  ",
                ForeColor = Accent,
                BackColor = Color.FromArgb(19, 78, 74),
                AutoSize = true,
                Padding = new Padding(6, 5, 6, 5),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(830, 24)
            };
            header.Resize += delegate { badge.Left = header.ClientSize.Width - badge.Width; };
            header.Controls.Add(logo);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(badge);
            return header;
        }

        private Control BuildInputCard()
        {
            var card = Card("Links", "Paste one address per line");
            card.Margin = new Padding(0, 0, 10, 0);

            _urls = new TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(13, 20, 32),
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5f),
                Location = new Point(22, 78),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Size = new Size(500, 250)
            };
            _urls.Text = "";

            var paste = SmallButton("Paste", 90);
            paste.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            paste.Click += delegate
            {
                try { if (Clipboard.ContainsText()) _urls.Text = Clipboard.GetText().Trim(); }
                catch { }
            };

            var clear = SmallButton("Clear", 82);
            clear.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            clear.Click += delegate { _urls.Clear(); };

            var helper = new Label
            {
                Text = "Supports thousands of video sites",
                ForeColor = TextMuted,
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            card.Controls.Add(_urls);
            card.Controls.Add(paste);
            card.Controls.Add(clear);
            card.Controls.Add(helper);
            card.Resize += delegate
            {
                _urls.Size = new Size(card.ClientSize.Width - 44, card.ClientSize.Height - 148);
                paste.Location = new Point(22, card.ClientSize.Height - 54);
                clear.Location = new Point(120, card.ClientSize.Height - 54);
                helper.Location = new Point(218, card.ClientSize.Height - 44);
            };
            return card;
        }

        private Control BuildSettingsCard()
        {
            _formatCard = Card("Download settings", "Choose what MediaPort should save");
            _formatCard.Margin = new Padding(10, 0, 0, 0);
            var y = 76;

            _mode = AddCombo(_formatCard, "Type", new[] { "Video", "Audio only" }, ref y);
            _mode.SelectedIndexChanged += delegate { UpdateModeControls(); };
            _quality = AddCombo(_formatCard, "Quality", new[] { "Best available", "2160p (4K)", "1440p", "1080p", "720p", "480p", "360p" }, ref y);
            _audioFormat = AddCombo(_formatCard, "Audio format", new[] { "MP3", "M4A", "Opus", "FLAC", "WAV" }, ref y);
            _cookies = AddCombo(_formatCard, "Browser cookies", new[] { "None", "Chrome", "Edge", "Firefox", "Brave", "Opera", "Vivaldi" }, ref y);

            var advancedLabel = new Label { Text = "More options", ForeColor = TextMuted, AutoSize = true, Location = new Point(22, y + 7) };
            _advancedButton = SmallButton("Advanced options…", 242);
            _advancedButton.Location = new Point(145, y - 2);
            _advancedButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _advancedButton.TextAlign = ContentAlignment.MiddleLeft;
            _advancedButton.Padding = new Padding(8, 0, 0, 0);
            _advancedButton.Click += ShowAdvancedOptions;
            _formatCard.Controls.Add(advancedLabel);
            _formatCard.Controls.Add(_advancedButton);
            _formatCard.Resize += delegate { _advancedButton.Width = Math.Max(130, _formatCard.ClientSize.Width - 167); };
            y += 39;

            var options = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Location = new Point(20, y + 2),
                Size = new Size(390, 104),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = Panel,
                Padding = new Padding(0)
            };
            _playlist = AddCheck(options, "Playlist", true);
            _subtitles = AddCheck(options, "Embed subtitles", false);
            _thumbnail = AddCheck(options, "Embed thumbnail", true);
            _metadata = AddCheck(options, "Metadata & chapters", true);
            _sponsorBlock = AddCheck(options, "Remove sponsors", false);
            _openFolder = AddCheck(options, "Open folder when done", true);
            _formatCard.Controls.Add(options);
            _formatCard.Resize += delegate { options.Width = _formatCard.ClientSize.Width - 40; };
            UpdateModeControls();
            return _formatCard;
        }

        private Control BuildActionBar()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Bg,
                Padding = new Padding(0, 8, 0, 6),
                ColumnCount = 4,
                RowCount = 2
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            var destinationLabel = new Label { Text = "Save to", ForeColor = TextMuted, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };
            _destination = new TextBox
            {
                Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                BackColor = Panel2,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 10, 7)
            };
            var browse = SmallButton("Browse…", 90);
            browse.Dock = DockStyle.Fill;
            browse.Margin = new Padding(0, 1, 10, 3);
            browse.Click += BrowseDestination;

            _download = AccentButton("Download", 145);
            _download.Dock = DockStyle.Fill;
            _download.Margin = new Padding(0, 0, 10, 2);
            _download.Click += async delegate { await StartDownloadAsync(); };

            _cancel = SmallButton("Cancel", 90);
            _cancel.Dock = DockStyle.Fill;
            _cancel.Margin = new Padding(0, 1, 0, 3);
            _cancel.Enabled = false;
            _cancel.Click += delegate { CancelDownload(); };

            panel.Controls.Add(destinationLabel, 0, 0);
            panel.SetColumnSpan(destinationLabel, 4);
            panel.Controls.Add(_destination, 0, 1);
            panel.Controls.Add(browse, 1, 1);
            panel.Controls.Add(_download, 2, 1);
            panel.Controls.Add(_cancel, 3, 1);
            return panel;
        }

        private Control BuildLogCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Panel, Padding = new Padding(18, 13, 18, 12), Margin = new Padding(0) };
            _status = new Label { Text = "Getting things ready…", ForeColor = TextMain, AutoSize = true, Location = new Point(18, 12), Font = new Font("Segoe UI Semibold", 10f) };
            _metrics = new Label { Text = "", ForeColor = TextMuted, AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _progress = new ProgressBar { Minimum = 0, Maximum = 1000, Value = 0, Style = ProgressBarStyle.Continuous, Location = new Point(18, 39), Height = 9, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            _log = new RichTextBox
            {
                ReadOnly = true,
                DetectUrls = false,
                BorderStyle = BorderStyle.None,
                BackColor = Panel,
                ForeColor = TextMuted,
                Font = new Font("Consolas", 8.5f),
                Location = new Point(18, 58),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            card.Controls.Add(_status);
            card.Controls.Add(_metrics);
            card.Controls.Add(_progress);
            card.Controls.Add(_log);
            card.Resize += delegate
            {
                _metrics.Location = new Point(card.ClientSize.Width - _metrics.Width - 18, 13);
                _progress.Width = card.ClientSize.Width - 36;
                _log.Size = new Size(card.ClientSize.Width - 36, card.ClientSize.Height - 66);
            };
            return card;
        }

        private Panel Card(string title, string subtitle)
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Panel, Padding = new Padding(22) };
            p.Controls.Add(new Label { Text = title, ForeColor = TextMain, Font = new Font("Segoe UI Semibold", 13f), AutoSize = true, Location = new Point(20, 18) });
            p.Controls.Add(new Label { Text = subtitle, ForeColor = TextMuted, AutoSize = true, Location = new Point(22, 48) });
            return p;
        }

        private ComboBox AddCombo(Control parent, string caption, string[] values, ref int y)
        {
            var label = new Label { Text = caption, ForeColor = TextMuted, AutoSize = true, Location = new Point(22, y + 7) };
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Panel2,
                ForeColor = TextMain,
                Location = new Point(145, y),
                Width = 242,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            combo.Items.AddRange(values);
            combo.SelectedIndex = 0;
            parent.Controls.Add(label);
            parent.Controls.Add(combo);
            parent.Resize += delegate { combo.Width = Math.Max(130, parent.ClientSize.Width - 167); };
            y += 39;
            return combo;
        }

        private CheckBox AddCheck(Control parent, string text, bool value)
        {
            var check = new CheckBox
            {
                Text = text,
                Checked = value,
                ForeColor = TextMain,
                AutoSize = false,
                Height = 24,
                Margin = new Padding(0, 4, 8, 4),
                Width = 162,
                FlatStyle = FlatStyle.Flat
            };
            parent.Controls.Add(check);
            return check;
        }

        private Button SmallButton(string text, int width)
        {
            var b = new Button
            {
                Text = text,
                Width = width,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Panel2,
                ForeColor = TextMain,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 52, 73);
            return b;
        }

        private Button AccentButton(string text, int width)
        {
            var b = SmallButton(text, width);
            b.Height = 38;
            b.BackColor = AccentDark;
            b.FlatAppearance.BorderColor = Accent;
            b.Font = new Font("Segoe UI Semibold", 10f);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 184, 166);
            return b;
        }

        private void UpdateModeControls()
        {
            if (_mode == null) return;
            bool audio = _mode.SelectedIndex == 1;
            _quality.Enabled = !audio;
            _audioFormat.Enabled = audio;
        }

        private void ShowAdvancedOptions(object sender, EventArgs e)
        {
            if (_advancedDialog == null || _advancedDialog.IsDisposed)
                _advancedDialog = new AdvancedOptionsDialog();
            if (_advancedDialog.ShowDialog(this) == DialogResult.OK)
            {
                _advancedArguments = _advancedDialog.GetArguments();
                int count = _advancedDialog.SelectedOptionCount;
                _advancedButton.Text = count == 0 ? "Advanced options…" : "Advanced options…  (" + count + " selected)";
            }
        }

        private async Task PrepareToolsAsync()
        {
            try
            {
                _toolFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MediaPort", "runtime");
                Directory.CreateDirectory(_toolFolder);
                await ExtractResourceAsync("MediaPort.yt-dlp.exe", Path.Combine(_toolFolder, "yt-dlp.exe"));
                await ExtractResourceAsync("MediaPort.ffmpeg.exe", Path.Combine(_toolFolder, "ffmpeg.exe"));
                SetStatus("Ready — paste a link to begin", "", 0);
                AppendLog("MediaPort is ready. Tools are stored privately in your user profile.");
            }
            catch (Exception ex)
            {
                SetStatus("Could not prepare the download engine", "", 0);
                AppendLog(ex.Message);
                MessageBox.Show(this, "MediaPort could not unpack its built-in tools.\n\n" + ex.Message, "Startup problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ExtractResourceAsync(string resourceName, string outputPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var input = assembly.GetManifestResourceStream(resourceName))
            {
                if (input == null) throw new InvalidOperationException("Missing built-in component: " + resourceName);
                bool needsWrite = !File.Exists(outputPath) || new FileInfo(outputPath).Length != input.Length;
                if (!needsWrite) return;
                string temp = outputPath + ".new";
                using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    await input.CopyToAsync(output);
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(temp, outputPath);
            }
        }

        private async Task StartDownloadAsync()
        {
            if (_busy) return;
            var urls = GetUrls();
            if (urls.Count == 0)
            {
                MessageBox.Show(this, "Paste at least one valid web address first.", "No links", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _urls.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(_destination.Text))
            {
                MessageBox.Show(this, "Choose a destination folder.", "No destination", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Directory.CreateDirectory(_destination.Text.Trim());
            ToggleBusy(true);
            _cancelSource = new CancellationTokenSource();
            _log.Clear();
            try
            {
                urls = await ValidateLinksAsync(urls, _cancelSource.Token);
                _urls.Lines = urls.ToArray();
            }
            catch (OperationCanceledException)
            {
                ToggleBusy(false);
                SetStatus("Cancelled", "", 0);
                return;
            }
            if (urls.Count == 0)
            {
                ToggleBusy(false);
                SetStatus("No supported links", "Invalid entries were removed", 0);
                AppendLog("No supported media links were found. Nothing was downloaded.");
                return;
            }
            bool success = true;
            try
            {
                for (int i = 0; i < urls.Count; i++)
                {
                    if (_cancelSource.IsCancellationRequested) { success = false; break; }
                    _playlistCount = 0;
                    _playlistIndex = 0;
                    _lastLoggedPlaylistIndex = 0;
                    if (_playlist.Checked)
                    {
                        SetStatus("Reading playlist details…", "This can take a moment", 0);
                        var titles = await ProbePlaylistAsync(urls[i], _cancelSource.Token);
                        if (titles.Count > 1)
                        {
                            _playlistCount = titles.Count;
                            AppendLog("Playlist found — " + titles.Count + " videos");
                            for (int titleIndex = 0; titleIndex < titles.Count; titleIndex++)
                                AppendLog("  " + (titleIndex + 1) + ". " + titles[titleIndex]);
                            AppendLog("────────────────────────────────────────");
                        }
                    }
                    SetStatus("Downloading item " + (i + 1) + " of " + urls.Count, "Starting…", 0);
                    AppendLog("▶ " + urls[i]);
                    int exitCode = await RunDownloadAsync(urls[i], _cancelSource.Token);
                    if (exitCode != 0)
                    {
                        success = false;
                        if (!_cancelSource.IsCancellationRequested)
                            AppendLog("Download failed with exit code " + exitCode + ". See the messages above.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                success = false;
            }
            catch (Exception ex)
            {
                success = false;
                AppendLog("Error: " + ex.Message);
            }
            finally
            {
                ToggleBusy(false);
                if (_cancelSource != null && _cancelSource.IsCancellationRequested)
                    SetStatus("Cancelled", "", 0);
                else if (success)
                {
                    SetStatus("All downloads finished", "Saved to " + _destination.Text.Trim(), 1000);
                    if (_openFolder.Checked) OpenDestination();
                }
                else
                    SetStatus("Finished with one or more errors", "Check the activity log", 0);
            }
        }

        private Task<int> RunDownloadAsync(string url, CancellationToken token)
        {
            var completion = new TaskCompletionSource<int>();
            var args = BuildArguments(url);
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(_toolFolder, "yt-dlp.exe"),
                Arguments = JoinArguments(args),
                WorkingDirectory = _destination.Text.Trim(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.EnvironmentVariables["PATH"] = _toolFolder + ";" + Environment.GetEnvironmentVariable("PATH");

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process = process;
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) HandleOutput(e.Data); };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) HandleOutput(e.Data); };
            process.Exited += delegate
            {
                try { completion.TrySetResult(process.ExitCode); }
                catch { completion.TrySetResult(-1); }
            };
            if (!process.Start()) throw new InvalidOperationException("The download engine could not be started.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            token.Register(delegate
            {
                KillProcessTree(process);
                completion.TrySetCanceled();
            });
            return completion.Task;
        }

        private async Task<List<string>> ValidateLinksAsync(List<string> urls, CancellationToken token)
        {
            var valid = new List<string>();
            AppendLog("Checking " + urls.Count + " link" + (urls.Count == 1 ? "" : "s") + " before download…");
            for (int i = 0; i < urls.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                SetStatus("Checking link " + (i + 1) + " of " + urls.Count, "Verifying site support", 0);
                var result = await ProbeLinkAsync(urls[i], token);
                if (result.IsValid)
                {
                    valid.Add(urls[i]);
                    AppendLog("Accepted: " + (string.IsNullOrWhiteSpace(result.Title) ? urls[i] : result.Title));
                }
                else
                {
                    AppendLog("Rejected: " + urls[i]);
                    if (!string.IsNullOrWhiteSpace(result.Error)) AppendLog("  " + result.Error);
                }
            }
            AppendLog(valid.Count + " of " + urls.Count + " links accepted.");
            AppendLog("────────────────────────────────────────");
            return valid;
        }

        private Task<LinkCheckResult> ProbeLinkAsync(string url, CancellationToken token)
        {
            var completion = new TaskCompletionSource<LinkCheckResult>();
            string title = null;
            string error = null;
            var args = new List<string>
            {
                "--flat-playlist", "--skip-download", "--no-warnings", "--playlist-end", "1",
                "--print", "CLIPVALID|%(extractor_key)s|%(title)s",
                _playlist.Checked ? "--yes-playlist" : "--no-playlist"
            };
            if (_cookies.SelectedIndex > 0)
            {
                args.Add("--cookies-from-browser");
                args.Add(_cookies.Text.ToLowerInvariant());
            }
            args.Add(url);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(_toolFolder, "yt-dlp.exe"),
                    Arguments = JoinArguments(args),
                    WorkingDirectory = _destination.Text.Trim(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true
            };
            _process = process;
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null || !e.Data.StartsWith("CLIPVALID|", StringComparison.Ordinal)) return;
                string[] parts = e.Data.Split(new[] { '|' }, 3);
                if (parts.Length == 3) title = parts[2].Trim();
            };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) error = CleanLine(e.Data).Trim();
            };
            process.Exited += delegate
            {
                try
                {
                    process.WaitForExit();
                    completion.TrySetResult(new LinkCheckResult
                    {
                        IsValid = process.ExitCode == 0 && !string.IsNullOrWhiteSpace(title),
                        Title = title,
                        Error = error
                    });
                }
                catch (Exception ex)
                {
                    completion.TrySetResult(new LinkCheckResult { IsValid = false, Error = ex.Message });
                }
            };
            if (!process.Start()) throw new InvalidOperationException("The link checker could not be started.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            token.Register(delegate
            {
                KillProcessTree(process);
                completion.TrySetCanceled();
            });
            return completion.Task;
        }

        private Task<List<string>> ProbePlaylistAsync(string url, CancellationToken token)
        {
            var completion = new TaskCompletionSource<List<string>>();
            var titles = new List<string>();
            var args = new List<string>
            {
                "--flat-playlist", "--skip-download", "--no-warnings", "--yes-playlist",
                "--print", "CLIPITEM|%(playlist_index)s|%(playlist_count)s|%(title)s"
            };
            if (_cookies.SelectedIndex > 0)
            {
                args.Add("--cookies-from-browser");
                args.Add(_cookies.Text.ToLowerInvariant());
            }
            args.Add(url);

            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(_toolFolder, "yt-dlp.exe"),
                Arguments = JoinArguments(args),
                WorkingDirectory = _destination.Text.Trim(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process = process;
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null || !e.Data.StartsWith("CLIPITEM|", StringComparison.Ordinal)) return;
                string[] parts = e.Data.Split(new[] { '|' }, 4);
                if (parts.Length == 4)
                {
                    lock (titles) titles.Add(parts[3].Trim());
                }
            };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) HandleOutput(e.Data);
            };
            process.Exited += delegate
            {
                try
                {
                    process.WaitForExit();
                    lock (titles) completion.TrySetResult(new List<string>(titles));
                }
                catch { completion.TrySetResult(new List<string>()); }
            };
            if (!process.Start()) throw new InvalidOperationException("The playlist could not be inspected.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            token.Register(delegate
            {
                KillProcessTree(process);
                completion.TrySetCanceled();
            });
            return completion.Task;
        }

        private void KillProcessTree(Process process)
        {
            if (process == null) return;
            int pid;
            try
            {
                if (process.HasExited) return;
                pid = process.Id;
            }
            catch { return; }

            try
            {
                using (var killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/PID " + pid + " /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }))
                {
                    if (killer != null) killer.WaitForExit(5000);
                }
            }
            catch
            {
                try { if (!process.HasExited) process.Kill(); }
                catch { }
            }
        }

        private List<string> BuildArguments(string url)
        {
            var a = new List<string>();
            a.Add("--newline");
            a.Add("--no-colors");
            a.Add("--windows-filenames");
            a.Add("--ffmpeg-location"); a.Add(_toolFolder);
            a.Add("--progress-template"); a.Add("download:CLIPNEST|%(playlist_index)s|%(playlist_count)s|%(title)s|%(progress._percent_str)s|%(progress._speed_str)s|%(progress._eta_str)s");
            a.Add("--output"); a.Add(Path.Combine(_destination.Text.Trim(), "%(title).180B [%(id)s].%(ext)s"));

            a.Add(_playlist.Checked ? "--yes-playlist" : "--no-playlist");
            if (_mode.SelectedIndex == 1)
            {
                a.Add("--extract-audio");
                a.Add("--audio-format"); a.Add(_audioFormat.Text.ToLowerInvariant());
                a.Add("--audio-quality"); a.Add("0");
            }
            else
            {
                a.Add("--format"); a.Add(GetVideoFormat());
                a.Add("--merge-output-format"); a.Add("mp4");
            }
            if (_subtitles.Checked)
            {
                a.Add("--write-subs"); a.Add("--write-auto-subs"); a.Add("--sub-langs"); a.Add("all,-live_chat"); a.Add("--embed-subs");
            }
            if (_thumbnail.Checked) { a.Add("--embed-thumbnail"); a.Add("--convert-thumbnails"); a.Add("jpg"); }
            if (_metadata.Checked) { a.Add("--embed-metadata"); a.Add("--embed-chapters"); }
            if (_sponsorBlock.Checked) { a.Add("--sponsorblock-remove"); a.Add("default"); }
            if (_cookies.SelectedIndex > 0)
            {
                a.Add("--cookies-from-browser"); a.Add(_cookies.Text.ToLowerInvariant());
            }
            if (_advancedArguments.Count > 0)
                a.AddRange(_advancedArguments);
            a.Add(url);
            return a;
        }

        private string GetVideoFormat()
        {
            if (_quality.SelectedIndex == 0) return "bv*+ba/b";
            var m = Regex.Match(_quality.Text, @"(\d+)p");
            string h = m.Success ? m.Groups[1].Value : "1080";
            return "bv*[height<=" + h + "]+ba/b[height<=" + h + "]";
        }

        private void HandleOutput(string line)
        {
            if (IsDisposed) return;
            BeginInvoke((Action)delegate
            {
                if (line.StartsWith("CLIPNEST|", StringComparison.Ordinal))
                {
                    var match = Regex.Match(line, @"^CLIPNEST\|([^|]*)\|([^|]*)\|(.*)\|([^|]*)\|([^|]*)\|([^|]*)$");
                    if (!match.Success) return;
                    int index;
                    int count;
                    int.TryParse(match.Groups[1].Value.Trim(), out index);
                    int.TryParse(match.Groups[2].Value.Trim(), out count);
                    string title = match.Groups[3].Value.Trim();
                    double percent;
                    string raw = match.Groups[4].Value.Replace("%", "").Trim();
                    double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out percent);
                    if (count > 0) _playlistCount = count;
                    if (index > 0) _playlistIndex = index;
                    int progressValue = (int)(percent * 10);
                    if (_playlistCount > 1 && _playlistIndex > 0)
                        progressValue = (int)(((_playlistIndex - 1) + percent / 100.0) / _playlistCount * 1000.0);
                    _progress.Value = Math.Max(0, Math.Min(1000, progressValue));
                    if (_playlistCount > 1 && _playlistIndex > 0)
                    {
                        _status.Text = "Playlist " + _playlistIndex + " of " + _playlistCount + " — " + title;
                        if (_lastLoggedPlaylistIndex != _playlistIndex)
                        {
                            _lastLoggedPlaylistIndex = _playlistIndex;
                            AppendLog("Downloading " + _playlistIndex + "/" + _playlistCount + ": " + title);
                        }
                    }
                    string speed = match.Groups[5].Value.Trim();
                    string eta = match.Groups[6].Value.Trim();
                    _metrics.Text = percent.ToString("0.0", CultureInfo.InvariantCulture) + "%" + (speed.Length > 0 ? "  •  " + speed : "") + (eta.Length > 0 ? "  •  " + eta + " remaining" : "");
                    _metrics.Left = _metrics.Parent.ClientSize.Width - _metrics.Width - 18;
                }
                else
                {
                    AppendLog(CleanLine(line));
                    if (line.IndexOf("[download] Destination:", StringComparison.OrdinalIgnoreCase) >= 0)
                        _status.Text = "Receiving media…";
                    else if (line.IndexOf("[Merger]", StringComparison.OrdinalIgnoreCase) >= 0)
                        _status.Text = "Merging video and audio…";
                    else if (line.IndexOf("[ExtractAudio]", StringComparison.OrdinalIgnoreCase) >= 0)
                        _status.Text = "Converting audio…";
                }
            });
        }

        private string CleanLine(string line)
        {
            return Regex.Replace(line, "\\x1B\\[[0-?]*[ -/]*[@-~]", "");
        }

        private void SetStatus(string text, string metrics, int progress)
        {
            _status.Text = text;
            _metrics.Text = metrics;
            _progress.Value = Math.Max(0, Math.Min(1000, progress));
            if (_metrics.Parent != null) _metrics.Left = _metrics.Parent.ClientSize.Width - _metrics.Width - 18;
        }

        private void AppendLog(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _log.AppendText(text.Trim() + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        private void ToggleBusy(bool busy)
        {
            _busy = busy;
            _download.Enabled = !busy;
            _cancel.Enabled = busy;
            _urls.Enabled = !busy;
            _download.Text = busy ? "Downloading…" : "Download";
        }

        private void CancelDownload()
        {
            SetStatus("Cancelling…", "Stopping yt-dlp and FFmpeg", 0);
            if (_cancelSource != null && !_cancelSource.IsCancellationRequested) _cancelSource.Cancel();
        }

        private List<string> GetUrls()
        {
            var result = new List<string>();
            foreach (string raw in _urls.Lines)
            {
                string value = raw.Trim();
                Uri uri;
                if (Uri.TryCreate(value, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    result.Add(value);
            }
            return result;
        }

        private void BrowseDestination(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = "Choose where downloads should be saved", SelectedPath = _destination.Text })
                if (dialog.ShowDialog(this) == DialogResult.OK) _destination.Text = dialog.SelectedPath;
        }

        private void OpenDestination()
        {
            try { Process.Start(new ProcessStartInfo { FileName = _destination.Text.Trim(), UseShellExecute = true }); }
            catch { }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_busy) return;
            if (MessageBox.Show(this, "A download is still running. Exit and cancel it?", "Exit MediaPort", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            CancelDownload();
        }

        private static string JoinArguments(IEnumerable<string> args)
        {
            var sb = new StringBuilder();
            foreach (string arg in args)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(QuoteArgument(arg));
            }
            return sb.ToString();
        }

        private static string QuoteArgument(string arg)
        {
            if (arg.Length > 0 && !Regex.IsMatch(arg, "[\\s\\\"]")) return arg;
            var sb = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\') slashes++;
                else if (c == '"') { sb.Append('\\', slashes * 2 + 1); sb.Append(c); slashes = 0; }
                else { sb.Append('\\', slashes); sb.Append(c); slashes = 0; }
            }
            sb.Append('\\', slashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        internal static IEnumerable<string> ParseCommandLine(string commandLine)
        {
            var matches = Regex.Matches(commandLine, @"(?:[^\s\""']+|\""[^\""\r\n]*\""|'[^'\r\n]*')+");
            foreach (Match match in matches)
            {
                string v = match.Value;
                if (v.Length >= 2 && ((v[0] == '"' && v[v.Length - 1] == '"') || (v[0] == '\'' && v[v.Length - 1] == '\'')))
                    v = v.Substring(1, v.Length - 2);
                yield return v;
            }
        }

        private static Icon CreateAppIcon()
        {
            using (var bitmap = new Bitmap(64, 64))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(Color.FromArgb(19, 150, 135))) g.FillEllipse(b, 2, 2, 60, 60);
                Point[] p = { new Point(27, 19), new Point(27, 45), new Point(47, 32) };
                using (var b = new SolidBrush(Color.White)) g.FillPolygon(b, p);
                return Icon.FromHandle(bitmap.GetHicon());
            }
        }
    }

    internal sealed class AdvancedOptionsDialog : Form
    {
        private static readonly Color Bg = Color.FromArgb(12, 18, 29);
        private static readonly Color Panel = Color.FromArgb(20, 29, 44);
        private static readonly Color Panel2 = Color.FromArgb(27, 38, 56);
        private static readonly Color Border = Color.FromArgb(48, 63, 85);
        private static readonly Color TextMain = Color.FromArgb(241, 245, 249);
        private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
        private static readonly Color Accent = Color.FromArgb(45, 212, 191);

        private ComboBox _fragments;
        private ComboBox _rate;
        private ComboBox _retries;
        private NumericUpDown _playlistLimit;
        private CheckBox _archive;
        private CheckBox _keepOriginal;
        private CheckBox _infoJson;
        private CheckBox _description;
        private CheckBox _noLive;
        private CheckBox _slowRequests;
        private TextBox _custom;

        public AdvancedOptionsDialog()
        {
            Text = "MediaPort — Advanced options";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(660, 720);
            MinimumSize = new Size(620, 620);
            BackColor = Bg;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            BuildInterface();
        }

        public int SelectedOptionCount
        {
            get
            {
                int count = 0;
                if (_fragments.SelectedIndex > 0) count++;
                if (_rate.SelectedIndex > 0) count++;
                if (_retries.SelectedIndex > 0) count++;
                if (_playlistLimit.Value > 0) count++;
                if (_archive.Checked) count++;
                if (_keepOriginal.Checked) count++;
                if (_infoJson.Checked) count++;
                if (_description.Checked) count++;
                if (_noLive.Checked) count++;
                if (_slowRequests.Checked) count++;
                if (!string.IsNullOrWhiteSpace(_custom.Text)) count++;
                return count;
            }
        }

        public List<string> GetArguments()
        {
            var args = new List<string>();
            if (_fragments.SelectedIndex > 0)
            {
                string[] values = { "", "4", "8", "16" };
                args.Add("--concurrent-fragments"); args.Add(values[_fragments.SelectedIndex]);
            }
            if (_rate.SelectedIndex > 0)
            {
                string[] values = { "", "1M", "2M", "5M", "10M" };
                args.Add("--limit-rate"); args.Add(values[_rate.SelectedIndex]);
            }
            if (_retries.SelectedIndex > 0)
            {
                string[] values = { "", "3", "20", "infinite" };
                args.Add("--retries"); args.Add(values[_retries.SelectedIndex]);
            }
            if (_playlistLimit.Value > 0)
            {
                args.Add("--playlist-end"); args.Add(((int)_playlistLimit.Value).ToString(CultureInfo.InvariantCulture));
            }
            if (_archive.Checked)
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MediaPort");
                Directory.CreateDirectory(folder);
                args.Add("--download-archive"); args.Add(Path.Combine(folder, "download-archive.txt"));
            }
            if (_keepOriginal.Checked) args.Add("--keep-video");
            if (_infoJson.Checked) args.Add("--write-info-json");
            if (_description.Checked) args.Add("--write-description");
            if (_noLive.Checked) { args.Add("--match-filter"); args.Add("!is_live"); }
            if (_slowRequests.Checked)
            {
                args.Add("--sleep-interval"); args.Add("1");
                args.Add("--max-sleep-interval"); args.Add("3");
            }
            if (!string.IsNullOrWhiteSpace(_custom.Text)) args.AddRange(MainForm.ParseCommandLine(_custom.Text));
            return args;
        }

        private void BuildInterface()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            Controls.Add(root);

            var header = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(24, 18, 24, 10) };
            header.Controls.Add(new Label
            {
                Text = "Advanced options",
                ForeColor = TextMain,
                Font = new Font("Segoe UI Semibold", 18f),
                AutoSize = true,
                Location = new Point(22, 16)
            });
            header.Controls.Add(new Label
            {
                Text = "Optional controls for speed, reliability and extra files. Defaults are best for most people.",
                ForeColor = TextMuted,
                AutoSize = false,
                Location = new Point(25, 56),
                Size = new Size(580, 38)
            });
            root.Controls.Add(header, 0, 0);

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Panel,
                Padding = new Padding(22, 12, 22, 20),
                Margin = new Padding(22, 0, 22, 0)
            };
            flow.Resize += delegate
            {
                foreach (Control control in flow.Controls) control.Width = Math.Max(480, flow.ClientSize.Width - 48);
            };
            root.Controls.Add(flow, 0, 1);

            flow.Controls.Add(SectionLabel("PERFORMANCE & RELIABILITY"));
            _fragments = Combo(new[] { "Default (recommended)", "4 fragments", "8 fragments", "16 fragments" });
            flow.Controls.Add(ComboOption("Parallel fragments", "Downloads pieces of one video at the same time. Higher values can be faster but may be less reliable.", _fragments));
            _rate = Combo(new[] { "Unlimited (recommended)", "1 MB/s", "2 MB/s", "5 MB/s", "10 MB/s" });
            flow.Controls.Add(ComboOption("Download speed limit", "Caps bandwidth used by MediaPort. Leave unlimited unless downloads affect other internet activity.", _rate));
            _retries = Combo(new[] { "Default (10 retries)", "3 retries", "20 retries", "Keep retrying" });
            flow.Controls.Add(ComboOption("Retry failed connections", "Controls how many times MediaPort retries temporary network failures.", _retries));

            flow.Controls.Add(SectionLabel("PLAYLISTS & DUPLICATES"));
            _playlistLimit = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 10000,
                Value = 0,
                Width = 150,
                BackColor = Panel2,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle
            };
            flow.Controls.Add(ComboOption("Maximum playlist videos", "0 means download the entire playlist. Enter a number to stop after that many videos.", _playlistLimit));
            _archive = CheckOption(flow, "Skip previously downloaded videos", "Remembers completed video IDs and avoids downloading them again in the future.");
            _noLive = CheckOption(flow, "Skip live broadcasts", "Downloads normal and completed videos, but ignores streams that are currently live.");

            flow.Controls.Add(SectionLabel("EXTRA FILES"));
            _keepOriginal = CheckOption(flow, "Keep original media after conversion", "Preserves the source file when audio is converted or video and audio are merged.");
            _infoJson = CheckOption(flow, "Save technical information (.json)", "Writes a machine-readable file containing the title, channel, formats and other metadata.");
            _description = CheckOption(flow, "Save video description (.description)", "Stores the video's text description beside the downloaded media.");
            _slowRequests = CheckOption(flow, "Pause briefly between requests", "Waits 1–3 seconds between requests. Slower, but can reduce temporary site limits.");

            flow.Controls.Add(SectionLabel("EXPERT ONLY"));
            var expert = new Panel { Width = 560, Height = 100, BackColor = Panel, Margin = new Padding(0, 2, 0, 8) };
            expert.Controls.Add(new Label { Text = "Custom yt-dlp arguments", ForeColor = TextMain, AutoSize = true, Location = new Point(0, 4) });
            expert.Controls.Add(new Label
            {
                Text = "Leave this blank unless you know the exact yt-dlp command option. Incorrect values can make downloads fail.",
                ForeColor = Color.FromArgb(251, 191, 36),
                AutoSize = false,
                Location = new Point(0, 27),
                Size = new Size(540, 34)
            });
            _custom = new TextBox { BackColor = Panel2, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle, Location = new Point(0, 66), Width = 540, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            expert.Controls.Add(_custom);
            flow.Controls.Add(expert);

            var footer = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(22, 14, 22, 14) };
            var reset = DialogButton("Reset defaults", 120, false);
            reset.Location = new Point(22, 17);
            reset.Click += delegate { ResetDefaults(); };
            var cancel = DialogButton("Cancel", 90, false);
            cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            var save = DialogButton("Use these options", 150, true);
            save.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            footer.Controls.Add(reset);
            footer.Controls.Add(cancel);
            footer.Controls.Add(save);
            footer.Resize += delegate
            {
                save.Location = new Point(footer.ClientSize.Width - save.Width - 22, 14);
                cancel.Location = new Point(save.Left - cancel.Width - 10, 14);
            };
            cancel.DialogResult = DialogResult.Cancel;
            save.DialogResult = DialogResult.OK;
            AcceptButton = save;
            CancelButton = cancel;
            root.Controls.Add(footer, 0, 2);
        }

        private Label SectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = Accent,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Width = 560,
                Height = 30,
                Margin = new Padding(0, 12, 0, 2),
                TextAlign = ContentAlignment.BottomLeft
            };
        }

        private ComboBox Combo(string[] values)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Panel2, ForeColor = TextMain, Width = 190 };
            combo.Items.AddRange(values);
            combo.SelectedIndex = 0;
            return combo;
        }

        private Panel ComboOption(string title, string description, Control selector)
        {
            var row = new Panel { Width = 560, Height = 76, BackColor = Panel, Margin = new Padding(0) };
            row.Controls.Add(new Label { Text = title, ForeColor = TextMain, Font = new Font("Segoe UI Semibold", 9.5f), AutoSize = true, Location = new Point(0, 6) });
            row.Controls.Add(new Label { Text = description, ForeColor = TextMuted, AutoSize = false, Location = new Point(0, 31), Size = new Size(335, 42) });
            selector.Location = new Point(360, 12);
            selector.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            row.Controls.Add(selector);
            row.Resize += delegate { selector.Left = row.ClientSize.Width - selector.Width; };
            return row;
        }

        private CheckBox CheckOption(Control parent, string title, string description)
        {
            var row = new Panel { Width = 560, Height = 66, BackColor = Panel, Margin = new Padding(0) };
            var check = new CheckBox { Text = title, ForeColor = TextMain, FlatStyle = FlatStyle.Flat, AutoSize = true, Location = new Point(0, 5) };
            row.Controls.Add(check);
            row.Controls.Add(new Label { Text = description, ForeColor = TextMuted, AutoSize = false, Location = new Point(23, 29), Size = new Size(520, 34), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top });
            parent.Controls.Add(row);
            return check;
        }

        private Button DialogButton(string text, int width, bool accent)
        {
            var button = new Button { Text = text, Width = width, Height = 38, FlatStyle = FlatStyle.Flat, ForeColor = TextMain, BackColor = accent ? Color.FromArgb(19, 150, 135) : Panel2, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = accent ? Accent : Border;
            return button;
        }

        private void ResetDefaults()
        {
            _fragments.SelectedIndex = 0;
            _rate.SelectedIndex = 0;
            _retries.SelectedIndex = 0;
            _playlistLimit.Value = 0;
            _archive.Checked = false;
            _keepOriginal.Checked = false;
            _infoJson.Checked = false;
            _description.Checked = false;
            _noLive.Checked = false;
            _slowRequests.Checked = false;
            _custom.Clear();
        }
    }

    internal sealed class LogoControl : Control
    {
        public LogoControl()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new LinearGradientBrush(ClientRectangle, Color.FromArgb(45, 212, 191), Color.FromArgb(59, 130, 246), 45f))
                e.Graphics.FillEllipse(b, 1, 1, Width - 3, Height - 3);
            Point[] p = { new Point(19, 14), new Point(19, 34), new Point(35, 24) };
            using (var b = new SolidBrush(Color.White)) e.Graphics.FillPolygon(b, p);
        }
    }
}
