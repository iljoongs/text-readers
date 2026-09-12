using System.Windows;

namespace TextReaders.Views;

public partial class MyBookSaveDialog : Window
{
    public string BookTitle { get; private set; } = string.Empty;

    public string Author { get; private set; } = string.Empty;

    public MyBookSaveDialog(string initialTitle, string initialAuthor)
    {
        InitializeComponent();
        TitleTextBox.Text = initialTitle;
        AuthorTextBox.Text = initialAuthor;
        TitleTextBox.SelectAll();
        TitleTextBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        BookTitle = TitleTextBox.Text.Trim();
        Author = AuthorTextBox.Text.Trim();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
