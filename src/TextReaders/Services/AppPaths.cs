using System.IO;
using Microsoft.Win32;
using TextReaders.Models;

namespace TextReaders.Services;

// 데이터 폴더 자체의 위치를 가리키는 포인터는 그 데이터 폴더 "밖"(실행 파일 옆)에 둔다.
// 포인터가 데이터 폴더 안에 있으면 "포인터를 읽으려면 먼저 데이터 폴더 위치를 알아야 하는" 순환 문제가 생긴다.
public static class AppPaths
{
    private static readonly string PointerFilePath = Path.Combine(AppContext.BaseDirectory, "data-location.json");

    public static string DefaultDataDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "data");

    public static string DataDirectory
    {
        get
        {
            var pointer = JsonFileStore.Load(PointerFilePath, () => new DataLocationPointer());
            return !string.IsNullOrWhiteSpace(pointer.DataDirectory) ? pointer.DataDirectory : DefaultDataDirectory;
        }
    }

    public static bool IsDefaultDataDirectory =>
        string.Equals(Path.GetFullPath(DataDirectory), Path.GetFullPath(DefaultDataDirectory), StringComparison.OrdinalIgnoreCase);

    public static string? ChooseDataDirectoryFromDialog()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "데이터 폴더 선택",
            InitialDirectory = DataDirectory,
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    // 기존 데이터 폴더의 내용을 newDirectory로 옮기고, 그 위치를 새 데이터 폴더로 지정한다.
    // newDirectory가 null/빈 문자열이면 기본 위치(DefaultDataDirectory)로 되돌린다.
    public static string ChangeDataDirectory(string? newDirectory)
    {
        var target = string.IsNullOrWhiteSpace(newDirectory) ? DefaultDataDirectory : Path.GetFullPath(newDirectory);
        var current = Path.GetFullPath(DataDirectory);

        if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        if (IsSameOrSubPath(current, target))
        {
            throw new InvalidOperationException("현재 데이터 폴더 안쪽 경로는 새 데이터 폴더로 지정할 수 없습니다.");
        }

        Directory.CreateDirectory(target);
        if (Directory.Exists(current))
        {
            CopyDirectoryRecursive(current, target);
            Directory.Delete(current, recursive: true);
        }

        var isDefault = string.Equals(target, Path.GetFullPath(DefaultDataDirectory), StringComparison.OrdinalIgnoreCase);
        JsonFileStore.Save(PointerFilePath, new DataLocationPointer { DataDirectory = isDefault ? null : target });

        return target;
    }

    // 기본 폴더(DefaultDataDirectory)의 데이터를 현재 데이터 폴더로 복사한다. 기본 폴더는 그대로 남겨두고,
    // 겹치는 파일은 기본 폴더 쪽 내용으로 덮어쓴다. 현재 폴더가 이미 기본 폴더거나 기본 폴더가 없으면 아무 일도 하지 않는다.
    public static void CopyDefaultDataToCurrentDirectory()
    {
        var current = Path.GetFullPath(DataDirectory);
        var defaultDir = Path.GetFullPath(DefaultDataDirectory);

        if (string.Equals(current, defaultDir, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(defaultDir))
        {
            return;
        }

        Directory.CreateDirectory(current);
        CopyDirectoryRecursive(defaultDir, current);
    }

    private static bool IsSameOrSubPath(string basePath, string candidate)
    {
        var baseWithSeparator = basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(baseWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyDirectoryRecursive(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectoryRecursive(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
