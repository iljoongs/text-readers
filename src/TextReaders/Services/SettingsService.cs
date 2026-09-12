using System.IO;
using TextReaders.Models;

namespace TextReaders.Services;

public sealed class SettingsService : ISettingsService
{
    private static string FilePath => Path.Combine(AppPaths.DataDirectory, "settings.json");

    public AppSettings Load() => JsonFileStore.Load(FilePath, () => new AppSettings());

    public void Save(AppSettings settings) => JsonFileStore.Save(FilePath, settings);
}
