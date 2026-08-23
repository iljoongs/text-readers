using System.Windows;
using System.Windows.Input;
using TextReaders.Models;
using TextReaders.ViewModels;

namespace TextReaders.Views;

public partial class LibraryWindow : Window
{
    public LibraryWindow()
    {
        InitializeComponent();
    }

    private void LibraryEntryCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ReaderViewModel viewModel && ((FrameworkElement)sender).DataContext is LibraryEntry entry)
        {
            viewModel.OpenLibraryEntryCommand.Execute(entry);
            Close();
        }
    }
}
