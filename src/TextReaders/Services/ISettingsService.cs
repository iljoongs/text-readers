using TextReaders.Models;

namespace TextReaders.Services;

public interface ISettingsService
{
    AppSettings Load();

    void Save(AppSettings settings);
}
