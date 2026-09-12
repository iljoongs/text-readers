using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TextReaders.Models;

namespace TextReaders.Services;

// 책 한 권 = 표준 ZIP(.mybook) 파일 하나. 확장자를 .zip으로 바꾸면 일반 압축 프로그램으로도 열람 가능해야 하므로
// System.IO.Compression의 표준 ZipArchive만 사용하고, 독자 압축 포맷은 두지 않는다.
public sealed class BookStorageService : IBookStorageService
{
    private const string BookFileExtension = ".mybook";
    private const int HashLength = 32;

    private static string BooksDirectory => Path.Combine(AppPaths.DataDirectory, "Books");
    private static string IndexFilePath => Path.Combine(BooksDirectory, "index.json");

    private static readonly JsonSerializerOptions MetadataOptions = new() { WriteIndented = true };

    public string SaveBook(string content, string title, string author)
    {
        var hash = ComputeHash(content);
        var filePath = GetBookFilePath(hash);
        var addedAt = DateTime.Now;

        Directory.CreateDirectory(BooksDirectory);
        WriteBookFile(filePath, content, new BookMetadata
        {
            Title = title,
            Author = author,
            AddedAt = addedAt,
            Sha256 = hash,
        });

        var index = LoadIndex();
        index[hash] = new BookIndexEntry { Title = title, Author = author, AddedAt = addedAt };
        SaveIndex(index);

        return filePath;
    }

    public string LoadBook(string filePathOrHash)
    {
        var filePath = ResolveFilePath(filePathOrHash);

        using var archive = ZipFile.OpenRead(filePath);
        var entry = archive.GetEntry("content.txt")
            ?? throw new InvalidDataException($"'{filePath}' 안에 content.txt 항목이 없습니다.");

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public void DeleteBook(string filePathOrHash)
    {
        var filePath = ResolveFilePath(filePathOrHash);
        var hash = Path.GetFileNameWithoutExtension(filePath);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        var index = LoadIndex();
        if (index.Remove(hash))
        {
            SaveIndex(index);
        }
    }

    public IReadOnlyDictionary<string, BookIndexEntry> GetIndex() => LoadIndex();

    private static void WriteBookFile(string filePath, string content, BookMetadata metadata)
    {
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

        var contentEntry = archive.CreateEntry("content.txt", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(contentEntry.Open(), new UTF8Encoding(false)))
        {
            writer.Write(content);
        }

        var metadataEntry = archive.CreateEntry("metadata.json", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(metadataEntry.Open(), new UTF8Encoding(false)))
        {
            writer.Write(JsonSerializer.Serialize(metadata, MetadataOptions));
        }
    }

    private static string ComputeHash(string content)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes).ToLowerInvariant()[..HashLength];
    }

    private static string GetBookFilePath(string hash) => Path.Combine(BooksDirectory, hash + BookFileExtension);

    private static string ResolveFilePath(string filePathOrHash) =>
        File.Exists(filePathOrHash) ? filePathOrHash : GetBookFilePath(filePathOrHash);

    private static Dictionary<string, BookIndexEntry> LoadIndex() =>
        JsonFileStore.Load(IndexFilePath, () => new Dictionary<string, BookIndexEntry>());

    private static void SaveIndex(Dictionary<string, BookIndexEntry> index) =>
        JsonFileStore.Save(IndexFilePath, index);
}
