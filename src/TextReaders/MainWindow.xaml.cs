using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
