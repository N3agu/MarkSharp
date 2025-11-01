using System.Windows;
using System.Windows.Controls;
using Markdig;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.IO;

namespace MarkSharp {

    public partial class EditorTab : UserControl {
        private readonly MarkdownPipeline _markdownPipeline;
        private bool _isTextBoxScrolling = false;
        private bool _isPreviewScrolling = false;
        private bool _isDarkTheme = false;

        public string CurrentFilePath { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsScrollSyncEnabled { get; set; } = true;

        public string FileName {
            get {
                return string.IsNullOrEmpty(CurrentFilePath) ? "Untitled" : Path.GetFileName(CurrentFilePath);
            }
        }

        public event Action<EditorTab> DirtyStateChanged;

        public event Action<EditorTab, int> WordCountChanged;

        public EditorTab() {
            InitializeComponent();

            _markdownPipeline = new MarkdownPipelineBuilder()
                                    .UseAdvancedExtensions()
                                    .Build();

            this.Loaded += (s, e) => InitializeWebViewAsync();
        }

        private async void InitializeWebViewAsync() {
            try {
                await PreviewBrowser.EnsureCoreWebView2Async(null);

                PreviewBrowser.CoreWebView2.WebMessageReceived += HandlePreviewScrollMessage;

                await PreviewBrowser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    window.addEventListener('scroll', () => {
                        let scrollableHeight = document.documentElement.scrollHeight - window.innerHeight;
                        let scrollPercent = 0; 
                        
                        if (scrollableHeight > 0) {
                            scrollPercent = window.scrollY / scrollableHeight;
                        }
                        
                        window.chrome.webview.postMessage({ type: 'scroll', percent: scrollPercent });
                    });
                ");

                UpdatePreview();
            } catch (Exception ex) {
                MessageBox.Show($"Error initializing WebView2: {ex.Message}", "WebView2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task LoadFile(string filePath) {
            try {
                CurrentFilePath = filePath;
                string content = await File.ReadAllTextAsync(CurrentFilePath);
                MarkdownTextBox.Text = content;
                IsDirty = false;
                DirtyStateChanged?.Invoke(this);
                UpdateWordCount();
                UpdatePreview();
            } catch (Exception ex) {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Save() {
            if (string.IsNullOrEmpty(CurrentFilePath)) {
                SaveAs();
            } else {
                try {
                    File.WriteAllText(CurrentFilePath, MarkdownTextBox.Text);
                    IsDirty = false;
                    DirtyStateChanged?.Invoke(this);
                } catch (Exception ex) {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void SaveAs() {
            SaveFileDialog saveDialog = new SaveFileDialog {
                Filter = "Markdown File (*.md)|*.md|All Files (*.*)|*.*",
                Title = "Save Markdown File",
                FileName = this.FileName
            };

            if (saveDialog.ShowDialog() == true) {
                try {
                    CurrentFilePath = saveDialog.FileName;
                    File.WriteAllText(CurrentFilePath, MarkdownTextBox.Text);
                    IsDirty = false;
                    DirtyStateChanged?.Invoke(this);
                } catch (Exception ex) {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void SetTheme(bool isDark) {
            _isDarkTheme = isDark;
            UpdatePreview();
        }

        public async Task ExportToPdfAsync(string filePath)
        {
            if (PreviewBrowser == null || PreviewBrowser.CoreWebView2 == null)
            {
                throw new InvalidOperationException("WebView is not initialized.");
            }

            await PreviewBrowser.CoreWebView2.PrintToPdfAsync(filePath, null);
        }

        public string GetSelfContainedHtml()
        {
            string htmlFragment = Markdig.Markdown.ToHtml(MarkdownTextBox.Text, _markdownPipeline);

            string fullHtml = $@"
                <!DOCTYPE html>
                <html lang=""en"">
                <head>
                    <meta charset=""utf-8"">
                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                    <title>{FileName}</title>
                </head>
                <body>
                    <div id=""wrapper"">
                        {htmlFragment}
                    </div>
                </body>
                </html>";

            return fullHtml;
        }

        public int GetWordCount() {
            string text = MarkdownTextBox.Text;
            char[] delimiters = new char[] { ' ', '\r', '\n' };
            string[] words = text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            return words.Length;
        }

        private void MarkdownTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            IsDirty = true;
            DirtyStateChanged?.Invoke(this);
            UpdatePreview();
            UpdateWordCount();
        }

        private async void MarkdownTextBox_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (!IsScrollSyncEnabled) return;
            if (_isTextBoxScrolling) return;

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
            if (!IsScrollSyncEnabled) return;
            if (_isTextBoxScrolling) return;
            _isPreviewScrolling = true;

            try {
                JsonDocument doc = JsonDocument.Parse(e.WebMessageAsJson);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("type", out JsonElement type) && type.GetString() == "scroll") {
                    if (root.TryGetProperty("percent", out JsonElement percentElement)) {
                        double scrollPercent = percentElement.GetDouble();
                        double newOffset = scrollPercent * (MarkdownTextBox.ExtentHeight - MarkdownTextBox.ViewportHeight);
                        if (!double.IsNaN(newOffset) && !double.IsInfinity(newOffset)) {
                            MarkdownTextBox.ScrollToVerticalOffset(newOffset);
                        }
                    }
                }
            }
            catch (JsonException) { }
            finally {
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
                            html, body {{ margin: 0; padding: 0; }}
                            {themeCss}
                            .content-wrapper {{ padding: 20px; }}
                            code {{ background-color: #80808030; padding: 2px 5px; border-radius: 4px; font-family: Consolas, monospace; }}
                            pre {{ background-color: #80808030; padding: 10px; border-radius: 4px; overflow-x: auto; }}
                            pre > code {{ background-color: transparent; padding: 0; }}
                            blockquote {{ border-left: 4px solid #80808080; padding-left: 10px; margin-left: 0; color: #808080; }}
                            table {{ border-collapse: collapse; width: auto; }}
                            th, td {{ border: 1px solid #80808080; padding: 8px; }}
                            th {{ background-color: #80808030; }}
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

        private void UpdateWordCount() {
            int wordCount = GetWordCount();
            WordCountChanged?.Invoke(this, wordCount);
        }
    }
}

