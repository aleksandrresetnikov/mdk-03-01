using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace GroundStation.Model;

public enum Role { Manager, Operator }
public enum Band { S, X }
public enum AntennaState { Ready, Maintenance, Faulty }
public enum SessionStatus { Planned, Completed, CompletedWithLoss }

public sealed class RuleException(string message) : Exception(message);

public readonly record struct TimeWindow
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    [JsonConstructor]
    public TimeWindow(DateTimeOffset start, DateTimeOffset end)
    {
        if (start.Offset != TimeSpan.Zero || end.Offset != TimeSpan.Zero)
            throw new RuleException("Время должно быть указано в UTC (+00:00).");
        if (end <= start || end - start > TimeSpan.FromHours(4))
            throw new RuleException("Длительность должна быть больше нуля и не более 4 часов.");
        Start = start;
        End = end;
    }

    public bool Overlaps(TimeWindow other) => Start < other.End && other.Start < End;
    [JsonIgnore]
    public double Minutes => (End - Start).TotalMinutes;
}

internal static class Guard
{
    public static string Text(string value, string field, int limit = 200)
    {
        value = value.Trim();
        if (value.Length == 0 || value.Length > limit || value.Any(char.IsControl))
            throw new RuleException($"{field}: требуется от 1 до {limit} печатных символов.");
        return value;
    }
}

public sealed record Satellite(int Id, string Name, Band Band);
public sealed record Antenna(int Id, string Name, Band Band, AntennaState State);
public sealed record User(int Id, string Login, Role Role, int? AntennaId, string Salt, string PasswordHash);
public sealed record Identity(int Id, string Login, Role Role, int? AntennaId);

/// <summary>Заявка содержит основное и одно резервное окно из внешнего расчёта орбиты.</summary>
public sealed record SessionRequest(int SatelliteId, TimeWindow Window, TimeWindow NextWindow,
    int Priority, double Elevation, string DataType)
{
    public void Validate()
    {
        if (Priority is < 1 or > 3) throw new RuleException("Приоритет: 1, 2 или 3.");
        if (!double.IsFinite(Elevation) || Elevation is < 0 or > 90)
            throw new RuleException("Угол места: от 0 до 90 градусов.");
        if (Window.End <= Window.Start || NextWindow.End <= NextWindow.Start)
            throw new RuleException("Не заданы окна связи.");
        if (NextWindow.Start < Window.End || NextWindow.End - NextWindow.Start != Window.End - Window.Start)
            throw new RuleException("Резервное окно должно быть позже основного и той же длительности.");
        Guard.Text(DataType, "Тип данных", 80);
    }
}

/// <summary>Общая проверка целостности; результат определяет статус полиморфно.</summary>
public abstract class Reception
{
    public int Expected { get; }
    public int Received { get; }
    public string Payload { get; }
    public string Digest { get; }
    public decimal LossPercent => Math.Round(100m * (Expected - Received) / Expected, 2);
    public abstract SessionStatus Status { get; }
    public abstract string Description { get; }

    private protected Reception(int expected, int received, string payload, string digest)
    {
        if (expected <= 0 || received < 0 || received > expected)
            throw new RuleException("Ожидалось > 0 пакетов; принято от 0 до ожидаемого числа.");
        if (payload.Length > 4096) throw new RuleException("Учебный фрагмент данных: не более 4096 символов.");
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        if (digest.Length != 64 || !digest.All(Uri.IsHexDigit) ||
            !CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(digest)))
            throw new RuleException("Контрольная сумма SHA256 не совпадает: результат не сохранён.");
        Expected = expected;
        Received = received;
        Payload = payload;
        Digest = Convert.ToHexString(actual);
    }

    public static Reception Create(int expected, int received, string payload, string digest) =>
        received == expected ? new CompleteReception(expected, received, payload, digest)
                             : new PartialReception(expected, received, payload, digest);
}

public sealed class CompleteReception : Reception
{
    internal CompleteReception(int expected, int received, string payload, string digest)
        : base(expected, received, payload, digest)
    {
        if (expected != received) throw new RuleException("Полный приём должен содержать все пакеты.");
    }
    public override SessionStatus Status => SessionStatus.Completed;
    public override string Description => "Выполнен без потерь";
}

public sealed class PartialReception : Reception
{
    internal PartialReception(int expected, int received, string payload, string digest)
        : base(expected, received, payload, digest)
    {
        if (received >= expected) throw new RuleException("Частичный приём должен содержать потери.");
    }
    public override SessionStatus Status => SessionStatus.CompletedWithLoss;
    public override string Description => $"Выполнен с нарушениями; потери {LossPercent:0.##}%";
}

public sealed class Session
{
    public int Id { get; }
    public int SatelliteId { get; }
    public int AntennaId { get; private set; }
    public TimeWindow Window { get; private set; }
    public TimeWindow? NextWindow { get; private set; }
    public int Priority { get; }
    public double Elevation { get; }
    public string DataType { get; }
    public SessionStatus Status { get; private set; }
    public Reception? Reception { get; private set; }

    internal Session(SessionRow row)
    {
        Id = row.Id;
        SatelliteId = row.SatelliteId;
        AntennaId = row.AntennaId;
        Window = row.Window;
        NextWindow = row.NextWindow;
        Priority = row.Priority;
        Elevation = row.Elevation;
        DataType = row.DataType;
        Status = row.Status;
        if (row.Expected.HasValue)
            Reception = Reception.Create(row.Expected.Value, row.Received!.Value, row.Payload!, row.Digest!);
    }

    internal void Move(int antennaId, TimeWindow window)
    {
        if (Status != SessionStatus.Planned) throw new RuleException("Выполненный сеанс нельзя переносить.");
        AntennaId = antennaId;
        Window = window;
        NextWindow = null;
    }

    public void Finish(Reception reception)
    {
        if (Status != SessionStatus.Planned) throw new RuleException("Результат сеанса уже зафиксирован.");
        Reception = reception;
        Status = reception.Status;
    }

    internal SessionRow ToRow() => new(Id, SatelliteId, AntennaId, Window, NextWindow, Priority,
        Elevation, DataType, Status, Reception?.Expected, Reception?.Received, Reception?.Payload, Reception?.Digest);
}

public sealed record SessionRow(int Id, int SatelliteId, int AntennaId, TimeWindow Window,
    TimeWindow? NextWindow, int Priority, double Elevation, string DataType, SessionStatus Status,
    int? Expected = null, int? Received = null, string? Payload = null, string? Digest = null);
public sealed record Maintenance(int Id, int AntennaId, TimeWindow Window, string Kind);
public sealed record Conflict(int Id, int SessionId, int? TriggerSessionId, int AntennaId,
    TimeWindow OldWindow, TimeWindow NewWindow, string Reason, int UserId);
public sealed record MaintenanceChange(int Id, int MaintenanceId, TimeWindow OldWindow,
    TimeWindow NewWindow, string Reason, int UserId);

public sealed record SatelliteSummary(string Name, int Total, int Completed, int WithoutLoss, decimal? SuccessPercent);
public sealed record DayLoad(string Antenna, DateOnly Day, double Minutes, double Percent);
public sealed record StationReport(DateTimeOffset From, DateTimeOffset To,
    IReadOnlyList<SatelliteSummary> Satellites, IReadOnlyList<DayLoad> Load, int Conflicts, int Postponements);
