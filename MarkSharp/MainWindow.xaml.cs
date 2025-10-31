using System.Windows;
using System.Windows.Controls;
using Markdig;
using ModernWpf;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.IO;
using System.Windows.Input;

namespace MarkSharp {
    public partial class MainWindow : Window {
        private readonly MarkdownPipeline _markdownPipeline;

        private bool _isDarkTheme = false;
        private bool _isDirty = false;
        private bool _isScrollSyncEnabled = true;
        private bool _isTextBoxScrolling = false;
        private bool _isPreviewScrolling = false;

        private string _currentFilePath = null;

        public MainWindow() {
            InitializeComponent();

            _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            InitializeWebViewAsync();
            UpdateWindowTitle();
            UpdateWordCount();
        }

        private async void InitializeWebViewAsync() {
            await PreviewBrowser.EnsureCoreWebView2Async(null);

            PreviewBrowser.CoreWebView2.WebMessageReceived += HandlePreviewScrollMessage;

            await PreviewBrowser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                window.addEventListener('scroll', () => {
                    let scrollableHeight = document.documentElement.scrollHeight - window.innerHeight;
                    let scrollPercent = 0; // Default to 0
                    
                    if (scrollableHeight > 0) {
                        scrollPercent = window.scrollY / scrollableHeight;
                    }
                    
                    window.chrome.webview.postMessage({ type: 'scroll', percent: scrollPercent });
                });
            ");

            UpdatePreview();
        }

        private void SyncScrollCheckBox_Click(object sender, RoutedEventArgs e) {
            _isScrollSyncEnabled = SyncScrollCheckBox.IsChecked ?? true;
        }

        private void MarkdownTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            _isDirty = true;
            UpdateWindowTitle();
            UpdatePreview();
            UpdateWordCount();
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e) {
            _isDarkTheme = !_isDarkTheme;
            var newTheme = _isDarkTheme ? ApplicationTheme.Dark : ApplicationTheme.Light;
            ThemeManager.Current.ApplicationTheme = newTheme;

            ThemeToggleIcon.Glyph = _isDarkTheme ? "\uEC8A" : "\uE708";

            UpdatePreview();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e) {
            bool isCtrlPressed = (e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool isShiftPressed = (e.KeyboardDevice.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (isCtrlPressed) {
                switch (e.Key) {
                    case Key.O: // Ctrl + O
                        OpenFileButton_Click(sender, e);
                        e.Handled = true;
                        break;

                    case Key.S:
                        if (isShiftPressed) {
                            // Ctrl + Shift + S
                            SaveAsButton_Click(sender, e);
                        } else {
                            // Ctrl + S
                            SaveButton_Click(sender, e);
                        }
                        e.Handled = true;
                        break;
                }
            }
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "Markdown Files (*.md;*.mdown;*.markdown)|*.md;*.mdown;*.markdown|All Files (*.*)|*.*",
                Title = "Open Markdown File"
            };

            if (openDialog.ShowDialog() == true) {
                try {
                    _currentFilePath = openDialog.FileName;
                    string content = File.ReadAllText(_currentFilePath);
                    MarkdownTextBox.Text = content;

                    _isDirty = false;
                    UpdateWindowTitle();
                    UpdatePreview();
                    UpdateWordCount();
                } catch (Exception ex) {
                    MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e) {
            if (string.IsNullOrEmpty(_currentFilePath)) {
                SaveFileAs();
            } else {
                try {
                    File.WriteAllText(_currentFilePath, MarkdownTextBox.Text);
                    _isDirty = false;
                    UpdateWindowTitle();
                } catch (Exception ex) {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileAs();
        }

        private async void MarkdownTextBox_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (!_isScrollSyncEnabled) return;

            if (_isPreviewScrolling) return;

            _isTextBoxScrolling = true;

            if (e.VerticalChange != 0 && MarkdownTextBox.ExtentHeight > MarkdownTextBox.ViewportHeight) {
                double scrollPercent = e.VerticalOffset / (MarkdownTextBox.ExtentHeight - MarkdownTextBox.ViewportHeight);

                if (PreviewBrowser != null && PreviewBrowser.CoreWebView2 != null) {
                    string script = $"window.scrollTo(0, (document.documentElement.scrollHeight - window.innerHeight) * {scrollPercent});";
                    await PreviewBrowser.CoreWebView2.ExecuteScriptAsync(script);
                }
            }

            _isTextBoxScrolling = false;
        }

        private void HandlePreviewScrollMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e) {
            if (!_isScrollSyncEnabled) return;

            if (_isTextBoxScrolling) return;

            _isPreviewScrolling = true;

            try {
                JsonDocument doc = JsonDocument.Parse(e.WebMessageAsJson);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("type", out JsonElement type) && type.GetString() == "scroll") {
                    if (root.TryGetProperty("percent", out JsonElement percentElement)) {
                        double scrollPercent = percentElement.GetDouble();

                        double newOffset = scrollPercent * (MarkdownTextBox.ExtentHeight - MarkdownTextBox.ViewportHeight);

                        if (!double.IsNaN(newOffset) && !double.IsInfinity(newOffset))
                        {
                            MarkdownTextBox.ScrollToVerticalOffset(newOffset);
                        }
                    }
                }
            } catch (JsonException)
            {

            } finally {
                _isPreviewScrolling = false;
            }
        }

        private void UpdatePreview() {
            if (PreviewBrowser == null || PreviewBrowser.CoreWebView2 == null) {
                return;
            }

            string markdown = MarkdownTextBox.Text;
            string html = Markdown.ToHtml(markdown, _markdownPipeline);

            string themeCss = _isDarkTheme
                ? "body { background-color: #000000; color: #f0f0f0; font-family: 'Segoe UI', sans-serif; }"
                : "body { background-color: #ffffff; color: #0f0f0f; font-family: 'Segoe UI', sans-serif; }";

            string finalHtml = $@"
                <html>
                    <head>
                        <style>
                            html, body {{
                                margin: 0;
                                padding: 0;
                            }}
                            {themeCss}
                            .content-wrapper {{
                                padding: 20px;
                            }}
                            code {{
                                background-color: #80808030;
                                padding: 2px 5px;
                                border-radius: 4px;
                                font-family: Consolas, monospace;
                            }}
                            pre {{
                                background-color: #80808030;
                                padding: 10px;
                                border-radius: 4px;
                                overflow-x: auto;
                            }}
                            pre > code {{
                                background-color: transparent;
                                padding: 0;
                            }}
                            blockquote {{
                                border-left: 4px solid #80808080;
                                padding-left: 10px;
                                margin-left: 0;
                                color: #808080;
                            }}
                            table {{
                                border-collapse: collapse;
                                width: auto;
                            }}
                            th, td {{
                                border: 1px solid #80808080;
                                padding: 8px;
                            }}
                            th {{
                                background-color: #80808030;
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='content-wrapper'>
                            {html}
                        </div>
                    </body>
                </html>";

            PreviewBrowser.NavigateToString(finalHtml);
        }

        private void SaveFileAs() {
            SaveFileDialog saveDialog = new SaveFileDialog {
                Filter = "Markdown File (*.md)|*.md|All Files (*.*)|*.*",
                Title = "Save Markdown File"
            };

            if (saveDialog.ShowDialog() == true) {
                try {
                    _currentFilePath = saveDialog.FileName;
                    File.WriteAllText(_currentFilePath, MarkdownTextBox.Text);
                    _isDirty = false;
                    UpdateWindowTitle();
                } catch (Exception ex) {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateWindowTitle() {
            string fileName = "Untitled";
            if (!string.IsNullOrEmpty(_currentFilePath)) {
                fileName = Path.GetFileName(_currentFilePath);
            }

            string dirtyMark = _isDirty ? "*" : "";

            Title = $"{dirtyMark}{fileName} - Mark#";
        }

        private void UpdateWordCount() {
            string text = MarkdownTextBox.Text;

            char[] delimiters = new char[] { ' ', '\r', '\n' };
            string[] words = text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

            int wordCount = words.Length;

            WordCountLabel.Text = $"{wordCount} words";
        }
    }
}

