using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TextReaders.Models;
using TextReaders.ViewModels;

namespace TextReaders.Views;

public partial class LibraryWindow : Window
{
    private const double MinRestorableSize = 200;

    public LibraryWindow()
    {
        InitializeComponent();

        // DataContextChanged는 동기적으로 발생하므로(MainWindow와 동일한 패턴), Show() 전에 이미 크기/위치/선택이 반영된다.
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is ReaderViewModel viewModel)
            {
                ApplyGeometry(viewModel.LibraryWindowGeometry);
                RestoreSelection(viewModel);
            }
        };

        // 선택 항목이 화면에 보이도록 스크롤하는 건 창이 실제로 레이아웃된 뒤에야 가능하다.
        Loaded += (_, _) =>
        {
            if (DataContext is ReaderViewModel { SelectedLibraryEntry: { } selected })
            {
                EntryListBox.ScrollIntoView(selected);
            }
        };

        Closing += (_, _) =>
        {
            if (DataContext is ReaderViewModel viewModel)
            {
                viewModel.SaveLibraryWindowState(CaptureGeometry(), viewModel.SelectedLibraryEntry?.FilePath);
            }
        };
    }

    private void ApplyGeometry(WindowGeometry? geometry)
    {
        if (geometry is null || geometry.Width < MinRestorableSize || geometry.Height < MinRestorableSize)
        {
            return;
        }

        Left = geometry.Left;
        Top = geometry.Top;
        Width = geometry.Width;
        Height = geometry.Height;
        WindowStartupLocation = WindowStartupLocation.Manual;

        if (geometry.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private WindowGeometry CaptureGeometry()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        return new WindowGeometry
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = isMaximized,
        };
    }

    private static void RestoreSelection(ReaderViewModel viewModel)
    {
        var path = viewModel.LastSelectedLibraryEntryPath;
        if (path is null)
        {
            return;
        }

        var entry = viewModel.LibraryEntries.FirstOrDefault(e => e.FilePath == path);
        if (entry is not null)
        {
            viewModel.SelectedLibraryEntry = entry;
        }
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

    private void ShowMyBookInfoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel viewModel && GetEntryFromMenuItem(sender) is LibraryEntry entry)
        {
            MessageBox.Show(viewModel.GetMyBookInfoText(entry), "mybook 정보", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RemoveLibraryEntryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ReaderViewModel viewModel || GetEntryFromMenuItem(sender) is not LibraryEntry entry)
        {
            return;
        }

        var result = MessageBox.Show(
            $"'{entry.Title}'을(를) 라이브러리에서 삭제하시겠습니까?\n(실제 파일은 삭제되지 않고, 북마크/하이라이트/읽은 위치 기록만 사라집니다)",
            "text-readers", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            viewModel.RemoveLibraryEntryCommand.Execute(entry);
        }
    }

    private void SaveMyBookAsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ReaderViewModel viewModel || viewModel.CurrentBook is null)
        {
            MessageBox.Show("먼저 책을 열어주세요.", "text-readers", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new MyBookSaveDialog(viewModel.CurrentBook.Title, string.Empty) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            viewModel.SaveCurrentAsMyBook(dialog.BookTitle, dialog.Author);
            MessageBox.Show("mybook으로 저장했습니다.", "text-readers", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"mybook으로 저장하는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 라이브러리 창 전체가 드롭 대상이다 - 텍스트를 읽던 중이어도 창을 바꾸지 않고 목록에만 추가한다.
    private void LibraryWindow_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void LibraryWindow_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not ReaderViewModel viewModel || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        var addedCount = paths.Count(viewModel.AddFileToLibrary);

        if (addedCount > 0)
        {
            MessageBox.Show($"{addedCount}개 파일을 라이브러리에 추가했습니다.", "text-readers", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else if (paths.Length > 0)
        {
            MessageBox.Show("지원하는 파일(.txt, .md, .mybook)이 없습니다.", "text-readers", MessageBoxButton.OK, MessageBoxImage.Warning);
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
