using System.Windows;

namespace TextReaders.Views;

public partial class EditTitleDialog : Window
{
    public string NewTitle { get; private set; } = string.Empty;

    public EditTitleDialog(string currentTitle)
    {
        InitializeComponent();
        TitleTextBox.Text = currentTitle;
        TitleTextBox.SelectAll();
        TitleTextBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        NewTitle = TitleTextBox.Text.Trim();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
