using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using PoeEditor.Core.Models;
using PoeEditor.UI.Helpers;
using PoeEditor.UI.ViewModels;

namespace PoeEditor.UI.Views;

/// <summary>
/// File preview and edit window for virtual file contents.
/// </summary>
public partial class FilePreviewWindow : Window
{
    private static readonly HashSet<string> Utf16Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ot", ".it", ".ao", ".otc", ".itc", ".aoc"
    };

    private MainViewModel? _viewModel;
    private string _virtualPath = "";
    private string _fileExtension = "";
    private string _originalContent = "";
    private bool _isDirty;
    private bool _isLoading;

    // Search state
    private List<int> _searchMatches = new();
    private int _currentMatchIndex = -1;
    private string _lastSearchText = "";

    public FilePreviewWindow()
    {
        InitializeComponent();
        DarkModeHelper.ApplyDarkMode(this);
        Closing += FilePreviewWindow_Closing;
    }

    /// <summary>
    /// Loads and displays the content of a virtual file.
    /// </summary>
    public async Task LoadFileAsync(MainViewModel viewModel, VirtualFileEntry entry)
    {
        _viewModel = viewModel;
        _virtualPath = entry.FullPath;
        _fileExtension = Path.GetExtension(entry.Name);

        Title = $"File Editor - {entry.Name}";
        FileNameText.Text = entry.Name;
        FilePathText.Text = entry.FullPath;
        FileSizeText.Text = FormatFileSize(entry.Size);

        ShowLoading();
        _isLoading = true;

        try
        {
            var content = await viewModel.ReadFileContentAsync(entry.FullPath);
            var (text, encodingName, encoding) = DecodeContent(content, _fileExtension);

            _detectedEncoding = encoding;
            EncodingText.Text = encodingName;
            _originalContent = text;
            ContentTextBox.Text = text;

            var lineCount = text.Split('\n').Length;
            LineCountText.Text = $"{lineCount:N0} lines";

            _isDirty = false;
            UpdateDirtyState();

            ShowContent();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static (string text, string encodingName, Encoding encoding) DecodeContent(ReadOnlyMemory<byte> data, string extension)
    {
        var span = data.Span;

        // Check for UTF-16 LE BOM (FF FE)
        if (span.Length >= 2 && span[0] == 0xFF && span[1] == 0xFE)
        {
            return (Encoding.Unicode.GetString(span), "UTF-16 LE (BOM)", Encoding.Unicode);
        }

        // Check for UTF-16 BE BOM (FE FF)
        if (span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF)
        {
            return (Encoding.BigEndianUnicode.GetString(span), "UTF-16 BE (BOM)", Encoding.BigEndianUnicode);
        }

        // Check for UTF-8 BOM (EF BB BF)
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            return (Encoding.UTF8.GetString(span), "UTF-8 (BOM)", Encoding.UTF8);
        }

        // For known UTF-16 extensions without BOM, detect by null byte pattern
        if (Utf16Extensions.Contains(extension))
        {
            return (Encoding.Unicode.GetString(span), "UTF-16 LE", Encoding.Unicode);
        }

        // Heuristic: check for null bytes pattern typical of UTF-16 LE
        if (span.Length >= 4)
        {
            int nullCount = 0;
            int sampleSize = Math.Min(100, span.Length / 2);

            for (int i = 1; i < sampleSize * 2; i += 2)
            {
                if (span[i] == 0x00) nullCount++;
            }

            if (nullCount > sampleSize * 0.8)
            {
                return (Encoding.Unicode.GetString(span), "UTF-16 LE (detected)", Encoding.Unicode);
            }
        }

        return (Encoding.UTF8.GetString(span), "UTF-8", Encoding.UTF8);
    }

    private Encoding _detectedEncoding = Encoding.UTF8;

    private ReadOnlyMemory<byte> EncodeContent(string text)
    {
        return _detectedEncoding.GetBytes(text);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} bytes";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    private void ShowLoading()
    {
        LoadingPanel.Visibility = Visibility.Visible;
        ContentPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowContent()
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;
        ErrorPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        ErrorText.Text = message;
    }

    private void UpdateDirtyState()
    {
        SaveButton.IsEnabled = _isDirty;
        ModifiedIndicator.Visibility = _isDirty ? Visibility.Visible : Visibility.Collapsed;
        Title = _isDirty ? $"File Editor - {FileNameText.Text} *" : $"File Editor - {FileNameText.Text}";
    }

    private void ContentTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isLoading) return;

        _isDirty = ContentTextBox.Text != _originalContent;
        UpdateDirtyState();

        var lineCount = ContentTextBox.Text.Split('\n').Length;
        LineCountText.Text = $"{lineCount:N0} lines";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || string.IsNullOrEmpty(_virtualPath)) return;

        try
        {
            SaveButton.IsEnabled = false;
            SaveButton.Content = "Saving...";

            var content = EncodeContent(ContentTextBox.Text);
            await _viewModel.WriteFileContentAsync(_virtualPath, content);

            // Save to disk immediately
            await _viewModel.SaveArchiveAsync();

            _originalContent = ContentTextBox.Text;
            _isDirty = false;
            UpdateDirtyState();

            // Update file size display
            FileSizeText.Text = FormatFileSize(content.Length);

            MessageBox.Show(
                "File saved successfully.\n\n" +
                "Note: If you want to use patchers, you need to close and reopen the archive first.",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SaveButton.Content = "Save";
            UpdateDirtyState();
        }
    }

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ContentTextBox.Text))
        {
            Clipboard.SetText(ContentTextBox.Text);
            MessageBox.Show("Content copied to clipboard.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void FilePreviewWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isDirty)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    Save_Click(this, new RoutedEventArgs());
                    break;
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;
                // No - just close without saving
            }
        }
    }

    #region Search Functionality

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+F opens search
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ShowSearchPanel();
            e.Handled = true;
        }
        // F3 - Find Next
        else if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (SearchPanel.Visibility != Visibility.Visible)
                ShowSearchPanel();
            else
                FindNext();
            e.Handled = true;
        }
        // Shift+F3 - Find Previous
        else if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            if (SearchPanel.Visibility != Visibility.Visible)
                ShowSearchPanel();
            else
                FindPrevious();
            e.Handled = true;
        }
        // Escape closes search panel if visible
        else if (e.Key == Key.Escape && SearchPanel.Visibility == Visibility.Visible)
        {
            HideSearchPanel();
            e.Handled = true;
        }
    }

    private void ShowSearchPanel()
    {
        SearchPanel.Visibility = Visibility.Visible;
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    private void HideSearchPanel()
    {
        SearchPanel.Visibility = Visibility.Collapsed;
        _searchMatches.Clear();
        _currentMatchIndex = -1;
        SearchResultText.Text = "";
        ContentTextBox.Focus();
    }

    private void CloseSearch_Click(object sender, RoutedEventArgs e)
    {
        HideSearchPanel();
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        FindNext();
    }

    private void FindPrevious_Click(object sender, RoutedEventArgs e)
    {
        FindPrevious();
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
                FindPrevious();
            else
                FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideSearchPanel();
            e.Handled = true;
        }
    }

    private void PerformSearch()
    {
        var searchText = SearchTextBox.Text;
        if (string.IsNullOrEmpty(searchText))
        {
            _searchMatches.Clear();
            _currentMatchIndex = -1;
            SearchResultText.Text = "";
            return;
        }

        // Only re-search if text changed
        if (searchText != _lastSearchText)
        {
            _lastSearchText = searchText;
            _searchMatches.Clear();
            _currentMatchIndex = -1;

            var content = ContentTextBox.Text;
            var index = 0;

            while ((index = content.IndexOf(searchText, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                _searchMatches.Add(index);
                index += searchText.Length;
            }
        }

        UpdateSearchResultText();
    }

    private void FindNext()
    {
        PerformSearch();

        if (_searchMatches.Count == 0)
        {
            SearchResultText.Text = "No matches";
            return;
        }

        _currentMatchIndex++;
        if (_currentMatchIndex >= _searchMatches.Count)
            _currentMatchIndex = 0;

        NavigateToMatch();
    }

    private void FindPrevious()
    {
        PerformSearch();

        if (_searchMatches.Count == 0)
        {
            SearchResultText.Text = "No matches";
            return;
        }

        _currentMatchIndex--;
        if (_currentMatchIndex < 0)
            _currentMatchIndex = _searchMatches.Count - 1;

        NavigateToMatch();
    }

    private void NavigateToMatch()
    {
        if (_currentMatchIndex < 0 || _currentMatchIndex >= _searchMatches.Count)
            return;

        var position = _searchMatches[_currentMatchIndex];
        var searchText = SearchTextBox.Text;

        ContentTextBox.Focus();
        ContentTextBox.Select(position, searchText.Length);

        // Scroll to selection
        var charIndex = ContentTextBox.GetCharacterIndexFromLineIndex(
            ContentTextBox.GetLineIndexFromCharacterIndex(position));
        ContentTextBox.ScrollToLine(ContentTextBox.GetLineIndexFromCharacterIndex(position));

        UpdateSearchResultText();
    }

    private void UpdateSearchResultText()
    {
        if (_searchMatches.Count == 0)
        {
            SearchResultText.Text = "No matches";
        }
        else
        {
            SearchResultText.Text = $"{_currentMatchIndex + 1}/{_searchMatches.Count}";
        }
    }

    #endregion
}
