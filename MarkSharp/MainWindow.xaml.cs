using System.Windows;
using System.Windows.Controls;
using Markdig;
using ModernWpf;

namespace MarkSharp {
    public partial class MainWindow : Window {
        private bool _isDarkTheme = false;
        private readonly MarkdownPipeline _markdownPipeline;

        public MainWindow() {
            InitializeComponent();

            _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

            InitializeWebViewAsync();
        }

        private async void InitializeWebViewAsync() {
            await PreviewBrowser.EnsureCoreWebView2Async(null);
            UpdatePreview();
        }

        private void MarkdownTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            UpdatePreview();
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e) {
            _isDarkTheme = !_isDarkTheme;

            ThemeManager.SetRequestedTheme(this, _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light);

            UpdatePreview();
        }

        private void UpdatePreview() {
            if (PreviewBrowser == null || PreviewBrowser.CoreWebView2 == null) {
                return;
            }

            string markdown = MarkdownTextBox.Text;

            string html = Markdown.ToHtml(markdown, _markdownPipeline);

            string themeCss = _isDarkTheme
                ? "body { background-color: #000000; color: #f0f0f0; font-family: 'Segoe UI', sans-serif; padding: 20px; }"
                : "body { background-color: #ffffff; color: #0f0f0f; font-family: 'Segoe UI', sans-serif; padding: 20px; }";

            string finalHtml = $@"
                <html>
                    <head>
                        <style>
                            {themeCss}
                            /* Add some extra styling for common markdown elements */
                            code {{
                                background-color: #80808030; /* Semi-transparent grey */
                                padding: 2px 5px;
                                border-radius: 4px;
                                font-family: Consolas, monospace;
                            }}
                            pre {{
                                background-color: #80808030;
                                padding: 10px;
                                border-radius: 4px;
                                overflow-x: auto; /* Allow horizontal scrolling for code blocks */
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
                        {html}
                    </body>
                </html>";

            PreviewBrowser.NavigateToString(finalHtml);
        }
    }
}

