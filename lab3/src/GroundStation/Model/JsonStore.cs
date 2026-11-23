using System.Text.Json;
using System.Text.Json.Serialization;

namespace GroundStation.Model;

public sealed class StationData
{
    public List<User> Users { get; init; } = [];
    public List<Satellite> Satellites { get; init; } = [];
    public List<Antenna> Antennas { get; init; } = [];
    public List<SessionRow> Sessions { get; init; } = [];
    public List<Maintenance> Maintenance { get; init; } = [];
    public List<Conflict> Conflicts { get; init; } = [];
    public List<MaintenanceChange> MaintenanceChanges { get; init; } = [];
}

public interface IStationStore
{
    T Read<T>(Func<StationData, T> query);
    T Update<T>(Func<StationData, T> change);
}

/// <summary>Блокировка файла охватывает чтение, проверку и атомарную замену JSON.</summary>
public sealed class JsonStore : IStationStore
{
    private readonly string path;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonStore(string path)
    {
        this.path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
    }

    private FileStream Acquire()
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (true)
        {
            try { return new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (DateTime.UtcNow < deadline) { Thread.Sleep(20); }
        }
    }

    private StationData Load()
    {
        if (!File.Exists(path)) return new StationData();
        try
        {
            return JsonSerializer.Deserialize<StationData>(File.ReadAllText(path), Options)
                ?? throw new RuleException("Файл данных пуст или повреждён.");
        }
        catch (JsonException) { throw new RuleException("Файл данных повреждён. Восстановите резервную копию."); }
    }

    public T Read<T>(Func<StationData, T> query)
    {
        using var gate = Acquire();
        return query(Load());
    }

    public T Update<T>(Func<StationData, T> change)
    {
        using var gate = Acquire();
        var data = Load();
        var result = change(data);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(data, Options));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return result;
    }
}
