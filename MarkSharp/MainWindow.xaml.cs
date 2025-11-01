using System.Windows;
using ModernWpf;
using Microsoft.Win32;
using System.IO;
using System.Windows.Input;
using System.Windows.Controls;
using System.ComponentModel;

namespace MarkSharp {
    public partial class MainWindow : Window {
        private bool _isDarkTheme = false;

        public MainWindow() {
            InitializeComponent();
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            AddNewTab();
            UpdateWindowTitle();
        }

        private TabItem GetActiveTabItem() {
            return EditorTabControl.SelectedItem as TabItem;
        }

        private EditorTab GetActiveEditor() {
            var activeTab = GetActiveTabItem();
            return activeTab?.Content as EditorTab;
        }

        private void AddNewTab() {
            var newEditor = new EditorTab();
            newEditor.SetTheme(_isDarkTheme);

            newEditor.DirtyStateChanged += OnEditorDirtyStateChanged;
            newEditor.WordCountChanged += OnEditorWordCountChanged;

            var newTabItem = new TabItem
            {
                Content = newEditor
            };

            var tabHeader = new TabHeader(newEditor.FileName);
            newTabItem.Header = tabHeader;

            EditorTabControl.Items.Add(newTabItem);
            EditorTabControl.SelectedItem = newTabItem;

            UpdateStatusBar(newEditor);
        }

        private async void OpenFileInNewTab(string filePath) {
            var newEditor = new EditorTab();
            newEditor.SetTheme(_isDarkTheme);

            newEditor.DirtyStateChanged += OnEditorDirtyStateChanged;
            newEditor.WordCountChanged += OnEditorWordCountChanged;

            var newTabItem = new TabItem {
                Content = newEditor
            };

            var tabHeader = new TabHeader(Path.GetFileName(filePath));
            newTabItem.Header = tabHeader;

            EditorTabControl.Items.Add(newTabItem);
            EditorTabControl.SelectedItem = newTabItem;

            await newEditor.LoadFile(filePath);

            UpdateStatusBar(newEditor);
            UpdateWindowTitle();
        }

        private void CloseTabButton_Click(object sender, RoutedEventArgs e) {
            var button = sender as Button;
            var headerContext = button?.Tag as TabHeader;

            TabItem tabToClose = null;
            foreach (TabItem item in EditorTabControl.Items) {
                if (item.DataContext == headerContext) {
                    tabToClose = item;
                    break;
                }
            }

            if (tabToClose != null) {
                CloseTab(tabToClose);
            }
        }

        private void CloseTab(TabItem tabItem) {
            var editor = tabItem.Content as EditorTab;
            if (editor == null) return;

            if (editor.IsDirty) {
                var result = MessageBox.Show(
                    $"Do you want to save changes to {editor.FileName}?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel) {
                    return;
                }
                
                if (result == MessageBoxResult.Yes) {
                    editor.Save();
                }
            }

            editor.DirtyStateChanged -= OnEditorDirtyStateChanged;
            editor.WordCountChanged -= OnEditorWordCountChanged;

            EditorTabControl.Items.Remove(tabItem);
        }

        private void NewFileButton_Click(object sender, RoutedEventArgs e) {
            AddNewTab();
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openDialog = new OpenFileDialog {
                Filter = "Markdown Files (*.md;*.mdown;*.markdown)|*.md;*.mdown;*.markdown|All Files (*.*)|*.*",
                Title = "Open Markdown File",
                Multiselect = true // open multiple files
            };

            if (openDialog.ShowDialog() == true) {
                foreach (string file in openDialog.FileNames) {
                    OpenFileInNewTab(file);
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e) {
            var activeEditor = GetActiveEditor();
            activeEditor?.Save();
            UpdateWindowTitle();
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e) {
            var activeEditor = GetActiveEditor();
            activeEditor?.SaveAs();
            UpdateWindowTitle();
        }

        private void BoldButton_Click(object sender, RoutedEventArgs e)
        {
            GetActiveEditor()?.ToggleBold();
        }

        private void ItalicButton_Click(object sender, RoutedEventArgs e)
        {
            GetActiveEditor()?.ToggleItalic();
        }

        private void StrikethroughButton_Click(object sender, RoutedEventArgs e)
        {
            GetActiveEditor()?.ToggleStrikethrough();
        }

        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            GetActiveEditor()?.InsertLink();
        }

        private void CodeButton_Click(object sender, RoutedEventArgs e)
        {
            GetActiveEditor()?.ToggleCode();
        }

        private void QuoteButton_Click(object sender, RoutedEventArgs e)
        {
            GetActiveEditor()?.ToggleBlockquote();
        }

        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            var activeEditor = GetActiveEditor();
            if (activeEditor == null) return;

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                Title = "Print to PDF",
                FileName = Path.ChangeExtension(activeEditor.FileName, ".pdf")
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    await activeEditor.ExportToPdfAsync(saveDialog.FileName);
                    MessageBox.Show($"Successfully printed to {saveDialog.FileName}", "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not print to PDF: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportHtmlButton_Click(object sender, RoutedEventArgs e)
        {
            var activeEditor = GetActiveEditor();
            if (activeEditor == null) return;

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "HTML Document (*.html;*.htm)|*.html;*.htm",
                Title = "Export as HTML",
                FileName = Path.ChangeExtension(activeEditor.FileName, ".html")
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    string fullHtml = activeEditor.GetSelfContainedHtml();
                    File.WriteAllText(saveDialog.FileName, fullHtml);
                    MessageBox.Show($"Successfully exported to {saveDialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not export to HTML: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e) {
            _isDarkTheme = !_isDarkTheme;
            var newTheme = _isDarkTheme ? ApplicationTheme.Dark : ApplicationTheme.Light;
            ThemeManager.Current.ApplicationTheme = newTheme;
            ThemeToggleIcon.Glyph = _isDarkTheme ? "\uEC8A" : "\uE708";

            foreach (TabItem tab in EditorTabControl.Items) {
                (tab.Content as EditorTab)?.SetTheme(_isDarkTheme);
            }
        }

        private void SyncScrollCheckBox_Click(object sender, RoutedEventArgs e) {
            var activeEditor = GetActiveEditor();
            if (activeEditor != null) {
                activeEditor.IsScrollSyncEnabled = SyncScrollCheckBox.IsChecked ?? true;
            }
        }

        private void EditorTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count > 0) {
                var activeEditor = GetActiveEditor();
                UpdateStatusBar(activeEditor);
                UpdateWindowTitle();
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e) {
            bool isCtrlPressed = (e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool isShiftPressed = (e.KeyboardDevice.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (isCtrlPressed) {
                switch (e.Key) {
                    // File operations
                    case Key.N: // Ctrl+N
                        NewFileButton_Click(sender, e);
                        e.Handled = true;
                        break;

                    case Key.O: // Ctrl+O
                        OpenFileButton_Click(sender, e);
                        e.Handled = true;
                        break;

                    case Key.S:
                        if (isShiftPressed) // Ctrl+Shift+S
                            SaveAsButton_Click(sender, e);
                        else // Ctrl+S
                            SaveButton_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.W: // Ctrl+W
                        var activeTab = GetActiveTabItem();
                        if (activeTab != null)
                        {
                            CloseTab(activeTab);
                            e.Handled = true;
                        }
                        break;

                    // Formatting
                    case Key.B: // Ctrl+B
                        GetActiveEditor()?.ToggleBold();
                        e.Handled = true;
                        break;

                    case Key.I: // Ctrl+I
                        GetActiveEditor()?.ToggleItalic();
                        e.Handled = true;
                        break;

                    case Key.K: // Ctrl+K
                        GetActiveEditor()?.InsertLink();
                        e.Handled = true;
                        break;

                    // Export
                    case Key.P: // Ctrl+P
                        PrintButton_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.E:
                        if (isShiftPressed) // Ctrl+Shift+E
                        {
                            ExportHtmlButton_Click(sender, e);
                            e.Handled = true;
                        }
                        break;
                }
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e) {
            var tabsToClose = new List<TabItem>(EditorTabControl.Items.Cast<TabItem>());

            foreach (var tab in tabsToClose) {
                CloseTab(tab);
            }

            if (EditorTabControl.Items.Count > 0) {
                e.Cancel = true;
            }
        }

        private void OnEditorDirtyStateChanged(EditorTab editor) {
            foreach (TabItem tab in EditorTabControl.Items) {
                if (tab.Content == editor) {
                    var header = tab.Header as TabHeader;
                    if (header != null) {
                        string dirtyMark = editor.IsDirty ? "*" : "";
                        header.HeaderText = $"{editor.FileName}{dirtyMark}";
                    }
                    break;
                }
            }
            UpdateWindowTitle();
        }

        private void OnEditorWordCountChanged(EditorTab sender, int wordCount) {
            if (GetActiveEditor() == sender) {
                WordCountLabel.Text = $"{wordCount} words";
            }
        }

        private void UpdateWindowTitle() {
            var activeEditor = GetActiveEditor();
            if (activeEditor != null) {
                string dirtyMark = activeEditor.IsDirty ? "*" : "";
                Title = $"{dirtyMark}{activeEditor.FileName} - Mark#";
            } else {
                Title = "Mark#";
            }
        }
        private void UpdateStatusBar(EditorTab activeEditor) {
            if (activeEditor != null) {
                WordCountLabel.Text = $"{activeEditor.GetWordCount()} words";
                SyncScrollCheckBox.IsChecked = activeEditor.IsScrollSyncEnabled;
            } else {
                WordCountLabel.Text = "0 words";
                SyncScrollCheckBox.IsChecked = false;
            }
        }
    }

    public class TabHeader : INotifyPropertyChanged {
        private string _headerText;
        public string HeaderText {
            get => _headerText;
            set {
                _headerText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderText)));
            }
        }

        public TabHeader(string headerText) {
            HeaderText = headerText;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}


