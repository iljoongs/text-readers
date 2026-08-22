using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TextReaders.ViewModels;

namespace TextReaders;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // MasterPageNumber는 Binding으로 관찰할 수 없게 막혀 있는 DP라(런타임에 ArgumentException),
        // DependencyPropertyDescriptor로 값 변경을 관찰해 ViewModel에 직접 반영한다.
        DependencyPropertyDescriptor
            .FromProperty(FlowDocumentPageViewer.MasterPageNumberProperty, typeof(FlowDocumentPageViewer))
            ?.AddValueChanged(PageViewer, (_, _) =>
            {
                if (DataContext is ReaderViewModel viewModel)
                {
                    viewModel.CurrentPageNumber = PageViewer.MasterPageNumber;
                }
            });

        // DataContextChanged는 동기적으로 발생하므로, App.xaml.cs가 마지막 세션 복원을 위해
        // DataContext 설정 직후(Show() 호출 전)에 LoadBook을 호출하더라도 구독이 먼저 걸려 있다.
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is ReaderViewModel viewModel)
            {
                viewModel.NavigateToPageRequested += pageNumber =>
                    NavigationCommands.GoToPage.Execute(pageNumber, PageViewer);
            }
        };
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