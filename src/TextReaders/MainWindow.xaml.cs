using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using TextReaders.Models;
using TextReaders.ViewModels;

namespace TextReaders;

public partial class MainWindow : Window
{
    private const int PageTurnAnimationMilliseconds = 280;

    private bool _isAnimatingPageTurn;
    private Views.LibraryWindow? _libraryWindow;
    private Views.SettingsWindow? _settingsWindow;

    public MainWindow()
    {
        InitializeComponent();

        PreviewKeyDown += Window_PreviewKeyDown;

        // MasterPageNumber/PageCount는 Binding으로 관찰할 수 없게 막혀 있는 DP라(런타임에 ArgumentException),
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

        DependencyPropertyDescriptor
            .FromProperty(FlowDocumentPageViewer.PageCountProperty, typeof(FlowDocumentPageViewer))
            ?.AddValueChanged(PageViewer, (_, _) =>
            {
                if (DataContext is ReaderViewModel viewModel)
                {
                    viewModel.PageCount = PageViewer.PageCount;
                }
            });

        // DataContextChanged는 동기적으로 발생하므로, App.xaml.cs가 마지막 세션 복원을 위해
        // DataContext 설정 직후(Show() 호출 전)에 LoadBook을 호출하더라도 구독이 먼저 걸려 있다.
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is ReaderViewModel viewModel)
            {
                // 세션 복원 시 위치 이동은 애니메이션 없이 즉시 이동한다.
                viewModel.NavigateToPageRequested += pageNumber =>
                    NavigationCommands.GoToPage.Execute(pageNumber, PageViewer);

                // 목차/검색처럼 "본문 어딘가"로 이동할 때는 그 문단이 몇 페이지에 있는지
                // 페이지네이터에게 물어본 뒤 이동한다. GetPageNumber는 0-base라 +1 보정한다.
                viewModel.NavigateToParagraphRequested += paragraph =>
                {
                    if (PageViewer.Document is IDocumentPaginatorSource source &&
                        source.DocumentPaginator is DynamicDocumentPaginator paginator)
                    {
                        var pageNumber = paginator.GetPageNumber(paragraph.ContentStart) + 1;
                        NavigationCommands.GoToPage.Execute(pageNumber, PageViewer);
                    }
                };
            }
        };
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
            case Key.PageUp:
                GoToAdjacentPage(forward: false);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.PageDown:
                GoToAdjacentPage(forward: true);
                e.Handled = true;
                break;
        }
    }

    private void PreviousPageZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => GoToAdjacentPage(forward: false);

    private void NextPageZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => GoToAdjacentPage(forward: true);

    private void LibraryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_libraryWindow is null)
        {
            _libraryWindow = new Views.LibraryWindow { Owner = this, DataContext = DataContext };
            _libraryWindow.Closed += (_, _) => _libraryWindow = null;
            _libraryWindow.Show();
        }
        else
        {
            _libraryWindow.Activate();
        }
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new Views.SettingsWindow { Owner = this, DataContext = DataContext };
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }

    private void GoToBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel viewModel && ((FrameworkElement)sender).DataContext is Bookmark bookmark)
        {
            viewModel.GoToBookmarkCommand.Execute(bookmark);
            BookmarkListToggle.IsChecked = false;
        }
    }

    private void RemoveBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel viewModel && ((FrameworkElement)sender).DataContext is Bookmark bookmark)
        {
            viewModel.RemoveBookmarkCommand.Execute(bookmark);
        }
    }

    private void GoToTocEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel viewModel && ((FrameworkElement)sender).DataContext is TocEntry entry)
        {
            viewModel.GoToTocEntryCommand.Execute(entry);
            TocListToggle.IsChecked = false;
        }
    }

    // 하이라이트는 한 문단 안에서 선택한 구간만 지원한다(문단 간 오프셋 매핑을 단순하게 유지하기 위해).
    private void AddHighlightButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ReaderViewModel viewModel || PageViewer.Document is not FlowDocument document)
        {
            return;
        }

        var selection = PageViewer.Selection;
        if (selection.IsEmpty)
        {
            MessageBox.Show("먼저 하이라이트할 텍스트를 선택하세요.", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var paragraphs = document.Blocks.OfType<Paragraph>().ToList();
        var paragraphIndex = paragraphs.FindIndex(p =>
            p.ContentStart.CompareTo(selection.Start) <= 0 && p.ContentEnd.CompareTo(selection.End) >= 0);

        if (paragraphIndex < 0)
        {
            MessageBox.Show("여러 문단에 걸친 선택은 지원하지 않습니다. 한 문단 안에서 선택해주세요.", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var paragraph = paragraphs[paragraphIndex];
        var startOffset = new TextRange(paragraph.ContentStart, selection.Start).Text.Length;
        var length = selection.Text.Length;

        var dialog = new Views.NoteDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        viewModel.AddHighlight(paragraphIndex, startOffset, length, dialog.NoteText);
    }

    // 현재 페이지에 해당하는 첫 문단을 찾아 그 문단부터 TTS 재생을 시작한다.
    // 페이지네이터는 View(PageViewer)에만 있으므로 이 탐색은 코드비하인드에서 한다(목차/검색 이동과 동일한 패턴).
    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ReaderViewModel viewModel || PageViewer.Document is not FlowDocument document)
        {
            return;
        }

        var paragraphs = document.Blocks.OfType<Paragraph>().ToList();
        if (paragraphs.Count == 0)
        {
            return;
        }

        var startIndex = 0;
        if (PageViewer.Document is IDocumentPaginatorSource source &&
            source.DocumentPaginator is DynamicDocumentPaginator paginator)
        {
            var found = paragraphs.FindIndex(p => paginator.GetPageNumber(p.ContentStart) + 1 >= viewModel.CurrentPageNumber);
            if (found >= 0)
            {
                startIndex = found;
            }
        }

        viewModel.StartReading(paragraphs, startIndex);
    }

    private void GoToAdjacentPage(bool forward)
    {
        if (_isAnimatingPageTurn)
        {
            return;
        }

        var command = forward ? NavigationCommands.NextPage : NavigationCommands.PreviousPage;
        if (!command.CanExecute(null, PageViewer))
        {
            return;
        }

        AnimatePageTurn(forward, () => command.Execute(null, PageViewer));
    }

    // FlowDocumentPageViewer는 페이지 렌더링을 직접 제어할 수 없어, 전환 직전 현재 화면을
    // 이미지로 캡처해 그 위에 슬라이드/페이드 애니메이션을 거는 방식으로 "페이지 넘김" 느낌을 낸다.
    private void AnimatePageTurn(bool forward, Action executeNavigation)
    {
        var width = PageViewer.ActualWidth;
        var height = PageViewer.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            executeNavigation();
            return;
        }

        _isAnimatingPageTurn = true;

        var snapshot = new RenderTargetBitmap(
            (int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        snapshot.Render(PageViewer);

        SnapshotOverlay.Width = width;
        SnapshotOverlay.Height = height;
        SnapshotOverlay.Source = snapshot;
        SnapshotOverlay.Opacity = 1;

        var transform = new TranslateTransform(0, 0);
        SnapshotOverlay.RenderTransform = transform;
        SnapshotOverlay.Visibility = Visibility.Visible;

        // 실제 페이지 전환은 스냅샷 뒤에서 즉시 일어난다.
        executeNavigation();

        var distance = forward ? -width : width;
        var duration = TimeSpan.FromMilliseconds(PageTurnAnimationMilliseconds);

        var slide = new DoubleAnimation(0, distance, duration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        slide.Completed += (_, _) =>
        {
            SnapshotOverlay.Visibility = Visibility.Collapsed;
            SnapshotOverlay.Source = null;
            _isAnimatingPageTurn = false;
        };

        var fade = new DoubleAnimation(1, 0, duration);

        transform.BeginAnimation(TranslateTransform.XProperty, slide);
        SnapshotOverlay.BeginAnimation(OpacityProperty, fade);
    }
}
