using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PoeEditor.Core.Models;
using PoeEditor.UI.Helpers;
using PoeEditor.UI.ViewModels;
using PoeEditor.UI.Views;

namespace PoeEditor.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DarkModeHelper.ApplyDarkMode(this);
    }

    private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel vm && e.NewValue is VirtualFileEntry entry)
        {
            vm.SelectedEntry = entry;
        }
    }

    private void ElasticPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is PasswordBox passwordBox)
        {
            vm.ElasticsearchPassword = passwordBox.Password;
        }
    }
    
    private void OpenPatcherWindow_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var patcherWindow = new PatcherWindow
            {
                DataContext = vm,
                Owner = this
            };
            patcherWindow.ShowDialog();
        }
    }

    private static readonly HashSet<string> PreviewableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".hlsl", ".inc", ".ffx", ".ot", ".it", ".ao",
        ".otc", ".itc", ".aoc", ".txt", ".mat", ".mtp", ".sm", ".amd", ".json"
    };

    private async void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item &&
            item.DataContext is VirtualFileEntry entry &&
            !entry.IsDirectory)
        {
            var ext = Path.GetExtension(entry.Name);
            if (PreviewableExtensions.Contains(ext))
            {
                e.Handled = true;
                await OpenFilePreviewAsync(entry);
            }
        }
    }

    private async Task OpenFilePreviewAsync(VirtualFileEntry entry)
    {
        if (DataContext is MainViewModel vm)
        {
            var previewWindow = new FilePreviewWindow
            {
                Owner = this
            };
            await previewWindow.LoadFileAsync(vm, entry);
            previewWindow.ShowDialog();
        }
    }
}