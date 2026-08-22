using System.IO;
using System.Text.Json;

namespace TextReaders.Services;

public static class JsonFileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static T Load<T>(string path, Func<T> defaultFactory)
    {
        if (!File.Exists(path))
        {
            return defaultFactory();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, Options) ?? defaultFactory();
        }
        catch (JsonException)
        {
            return defaultFactory();
        }
    }

    public static void Save<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
    }
}
