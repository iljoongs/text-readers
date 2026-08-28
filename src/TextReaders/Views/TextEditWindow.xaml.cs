using System.Windows;

namespace TextReaders.Views;

public partial class TextEditWindow : Window
{
    public string EditedText { get; private set; } = string.Empty;

    public TextEditWindow(string initialText, int scrollToCharacterIndex = 0)
    {
        InitializeComponent();
        ContentTextBox.Text = initialText;

        // 줄 정보(GetLineIndexFromCharacterIndex)는 레이아웃이 끝난 뒤에야 유효하므로 Loaded에서 스크롤한다.
        Loaded += (_, _) =>
        {
            var caretIndex = Math.Clamp(scrollToCharacterIndex, 0, ContentTextBox.Text.Length);
            ContentTextBox.CaretIndex = caretIndex;
            var line = ContentTextBox.GetLineIndexFromCharacterIndex(caretIndex);
            ContentTextBox.ScrollToLine(Math.Max(0, line - 2));
            ContentTextBox.Focus();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        EditedText = ContentTextBox.Text;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
