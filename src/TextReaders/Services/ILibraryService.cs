namespace TextReaders.Services;

public interface ILibraryService
{
    string? GetLastOpenedFilePath();

    int GetLastPageIndex(string filePath);

    void UpdatePosition(string filePath, int pageIndex);
}
