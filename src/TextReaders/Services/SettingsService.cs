using System.IO;
using TextReaders.Models;

namespace TextReaders.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "data", "settings.json");

    public AppSettings Load() => JsonFileStore.Load(FilePath, () => new AppSettings());

    public void Save(AppSettings settings) => JsonFileStore.Save(FilePath, settings);
}
