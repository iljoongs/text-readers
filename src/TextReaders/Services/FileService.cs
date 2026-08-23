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

    public string? ShowOpenFileDialog()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "지원 파일 (*.txt;*.md;*.json)|*.txt;*.md;*.json|텍스트 파일 (*.txt)|*.txt|마크다운 파일 (*.md)|*.md|번들 파일 (*.json)|*.json",
            CheckFileExists = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
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
            IsMarkdown = Path.GetExtension(filePath).Equals(".md", StringComparison.OrdinalIgnoreCase),
        };
    }
}
