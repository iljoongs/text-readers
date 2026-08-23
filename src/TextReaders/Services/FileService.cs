using System.IO;
using Microsoft.Win32;
using TextReaders.Models;

namespace TextReaders.Services;

public sealed class FileService : IFileService
{
    private readonly IEncodingDetectionService _encodingDetectionService;

    public FileService(IEncodingDetectionService encodingDetectionService)
    {
        _encodingDetectionService = encodingDetectionService;
    }

    public Book? OpenBookFromDialog()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "지원 파일 (*.txt;*.md)|*.txt;*.md|텍스트 파일 (*.txt)|*.txt|마크다운 파일 (*.md)|*.md",
            CheckFileExists = true,
        };

        return dialog.ShowDialog() == true ? LoadBook(dialog.FileName) : null;
    }

    public Book LoadBook(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var encoding = _encodingDetectionService.DetectEncoding(bytes);
        var content = encoding.GetString(bytes);

        return new Book
        {
            FilePath = filePath,
            Title = Path.GetFileNameWithoutExtension(filePath),
            Content = content,
        };
    }
}
