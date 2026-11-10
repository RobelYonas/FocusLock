using System.Text.Json.Serialization;

namespace FocusLock.Core.Settings;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(StatsData))]
internal sealed partial class FocusLockJsonContext : JsonSerializerContext
{
}
