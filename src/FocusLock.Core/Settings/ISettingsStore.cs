namespace FocusLock.Core.Settings;

public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
