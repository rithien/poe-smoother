using System.Windows;
using PoeEditor.UI.Helpers;

namespace PoeEditor.UI.Views;

/// <summary>
/// Interaction logic for PatcherWindow.xaml
/// </summary>
public partial class PatcherWindow : Window
{
    public PatcherWindow()
    {
        InitializeComponent();
        DarkModeHelper.ApplyDarkMode(this);
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
