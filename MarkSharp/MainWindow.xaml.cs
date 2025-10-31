using System.Windows;
using System.Windows.Controls;
using Markdig;
using ModernWpf;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace MarkSharp
{
    public partial class MainWindow : Window
    {
        private bool _isDarkTheme = false;
        private readonly MarkdownPipeline _markdownPipeline;

        private bool _isTextBoxScrolling = false;
        private bool _isPreviewScrolling = false;

        public MainWindow()
        {
            InitializeComponent();

            _markdownPipeline = new MarkdownPipelineBuilder()
                                    .UseAdvancedExtensions()
                                    .Build();

            InitializeWebViewAsync();
        }

        private async void InitializeWebViewAsync()
        {
            await PreviewBrowser.EnsureCoreWebView2Async(null);

            PreviewBrowser.CoreWebView2.WebMessageReceived += HandlePreviewScrollMessage;

            await PreviewBrowser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                window.addEventListener('scroll', () => {
                    let scrollableHeight = document.documentElement.scrollHeight - window.innerHeight;
                    if (scrollableHeight <= 0) {
                        scrollPercent = 0;
                    } else {
                        scrollPercent = window.scrollY / scrollableHeight;
                    }
                    
                    window.chrome.webview.postMessage({ type: 'scroll', percent: scrollPercent });
                });
            ");

            UpdatePreview();
        }

        private void MarkdownTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            ThemeManager.SetRequestedTheme(this, _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light);
            UpdatePreview();
        }

        private async void MarkdownTextBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isPreviewScrolling) return;

            _isTextBoxScrolling = true;

            if (e.VerticalChange != 0 && MarkdownTextBox.ExtentHeight > MarkdownTextBox.ViewportHeight)
            {
                double scrollPercent = e.VerticalOffset / (MarkdownTextBox.ExtentHeight - MarkdownTextBox.ViewportHeight);

                if (PreviewBrowser != null && PreviewBrowser.CoreWebView2 != null)
                {
                    string script = $"window.scrollTo(0, (document.documentElement.scrollHeight - window.innerHeight) * {scrollPercent});";
                    await PreviewBrowser.CoreWebView2.ExecuteScriptAsync(script);
                }
            }

            _isTextBoxScrolling = false;
        }

        private void HandlePreviewScrollMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (_isTextBoxScrolling) return;

            _isPreviewScrolling = true;

            try
            {
                JsonDocument doc = JsonDocument.Parse(e.WebMessageAsJson);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("type", out JsonElement type) && type.GetString() == "scroll")
                {
                    if (root.TryGetProperty("percent", out JsonElement percentElement))
                    {
                        double scrollPercent = percentElement.GetDouble();

                        double newOffset = scrollPercent * (MarkdownTextBox.ExtentHeight - MarkdownTextBox.ViewportHeight);

                        if (!double.IsNaN(newOffset) && !double.IsInfinity(newOffset))
                        {
                            MarkdownTextBox.ScrollToVerticalOffset(newOffset);
                        }
                    }
                }
            }
            catch (JsonException)
            {

            }
            finally
            {
                _isPreviewScrolling = false;
            }
        }


        private void UpdatePreview()
        {
            if (PreviewBrowser == null || PreviewBrowser.CoreWebView2 == null)
            {
                return;
            }

            string markdown = MarkdownTextBox.Text;
            string html = Markdown.ToHtml(markdown, _markdownPipeline);

            string themeCss = _isDarkTheme
                ? "body { background-color: #2b2b2b; color: #f0f0f0; font-family: 'Segoe UI', sans-serif; }"
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
    }
}

