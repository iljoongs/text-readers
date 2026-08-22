using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TextReaders.Models;
using TextReaders.Services;

namespace TextReaders.ViewModels;

public partial class ReaderViewModel : ObservableObject
{
    private readonly IFileService _fileService;

    [ObservableProperty]
    private Book? _currentBook;

    [ObservableProperty]
    private FlowDocument? _document;

    public ReaderViewModel(IFileService fileService)
    {
        _fileService = fileService;
    }

    [RelayCommand]
    private void OpenFile()
    {
        try
        {
            var book = _fileService.OpenBookFromDialog();
            if (book is not null)
            {
                LoadBook(book);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일을 여는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void LoadBook(Book book)
    {
        CurrentBook = book;
        Document = BuildFlowDocument(book.Content);
    }

    private static FlowDocument BuildFlowDocument(string content)
    {
        var document = new FlowDocument();

        var paragraphBlocks = Regex.Split(content, @"\r?\n\s*\r?\n");
        foreach (var block in paragraphBlocks)
        {
            var normalized = Regex.Replace(block, @"\r?\n", " ").Trim();
            if (normalized.Length == 0)
            {
                continue;
            }

            document.Blocks.Add(new Paragraph(new Run(normalized)));
        }

        return document;
    }
}
