using System.Windows;

namespace TextReaders.Views;

public partial class TextEditWindow : Window
{
    public string EditedText { get; private set; } = string.Empty;

    public TextEditWindow(string initialText)
    {
        InitializeComponent();
        ContentTextBox.Text = initialText;
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
