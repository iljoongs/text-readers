using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TextReaders.Models;
using TextReaders.ViewModels;

namespace TextReaders.Views;

public partial class LibraryWindow : Window
{
    public LibraryWindow()
    {
        InitializeComponent();
    }

    // 더블클릭일 때만 책을 연다 - 한 번 클릭은(ListBox 기본 동작으로) 카드를 선택만 한다.
    private void LibraryEntryCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            return;
        }

        if (DataContext is ReaderViewModel viewModel && ((FrameworkElement)sender).DataContext is LibraryEntry entry)
        {
            viewModel.OpenLibraryEntryCommand.Execute(entry);
            Close();
        }
    }

    // 오른쪽 버튼으로 팝업 메뉴를 열 때도 그 카드를 선택 상태로 만든다(ListBox는 오른쪽 클릭으로 선택하지 않으므로).
    private void LibraryEntryCard_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject element && FindAncestor<ListBoxItem>(element) is { } listBoxItem)
        {
            listBoxItem.IsSelected = true;
        }
    }

    private void SaveAsMyBookMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel viewModel && GetEntryFromMenuItem(sender) is LibraryEntry entry)
        {
            viewModel.SaveLibraryEntryAsMyBookCommand.Execute(entry);
        }
    }

    private void ConvertToMyBookMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel viewModel && GetEntryFromMenuItem(sender) is LibraryEntry entry)
        {
            viewModel.ConvertLibraryEntryToMyBookCommand.Execute(entry);
        }
    }

    private static LibraryEntry? GetEntryFromMenuItem(object sender) =>
        sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement element } }
            ? element.DataContext as LibraryEntry
            : null;

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
