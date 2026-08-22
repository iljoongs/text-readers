using System.Windows;
using System.Windows.Input;

namespace TextReaders;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void PreviousPageZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (NavigationCommands.PreviousPage.CanExecute(null, PageViewer))
        {
            NavigationCommands.PreviousPage.Execute(null, PageViewer);
        }
    }

    private void NextPageZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (NavigationCommands.NextPage.CanExecute(null, PageViewer))
        {
            NavigationCommands.NextPage.Execute(null, PageViewer);
        }
    }
}