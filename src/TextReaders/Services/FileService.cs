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
        // GetString은 BOM 바이트도 그대로 디코딩해 문자열 맨 앞에 U+FEFF를 남긴다 - 있으면 제거한다.
        // (그대로 두면 "제목:"/"제N장" 같은 첫 줄 패턴 인식이 깨진다.)
        var content = encoding.GetString(bytes).TrimStart('﻿');

        return new Book
        {
            FilePath = filePath,
            Title = TitleDetector.DetectTitle(content) ?? Path.GetFileNameWithoutExtension(filePath),
            Content = content,
            IsMarkdown = Path.GetExtension(filePath).Equals(".md", StringComparison.OrdinalIgnoreCase),
        };
    }
}
