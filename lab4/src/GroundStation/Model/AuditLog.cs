namespace GroundStation.Model;

public enum AuditKind { AntennaStateChanged, MaintenancePostponed }

public sealed record AuditEntry(int Id, int AntennaId, AuditKind Kind, int ObjectId,
    DateTimeOffset? OccurredAt, int UserId, string Reason,
    AntennaState? OldState = null, AntennaState? NewState = null,
    TimeWindow? OldWindow = null, TimeWindow? NewWindow = null,
    int? SourceChangeId = null, bool ImportedLegacy = false);

public sealed record AuditFilter(int? AntennaId = null, string? Reason = null,
    DateTimeOffset? From = null, DateTimeOffset? To = null)
{
    public void Validate()
    {
        if (AntennaId is <= 0) throw new RuleException("ID антенны должен быть положительным.");
        if (Reason is not null) Guard.Text(Reason, "Причина для поиска");
        if (From?.Offset != null && From.Value.Offset != TimeSpan.Zero ||
            To?.Offset != null && To.Value.Offset != TimeSpan.Zero)
            throw new RuleException("Период аудита задаётся в UTC.");
        if (From.HasValue && To.HasValue && From >= To)
            throw new RuleException("Начало периода должно быть раньше конца.");
    }

    public bool Matches(AuditEntry e) =>
        (!AntennaId.HasValue || e.AntennaId == AntennaId) &&
        (Reason is null || e.Reason.Contains(Reason.Trim(), StringComparison.OrdinalIgnoreCase)) &&
        (!From.HasValue || e.OccurredAt >= From) &&
        (!To.HasValue || e.OccurredAt < To);
}

/// <summary>Записи добавляются в ту же StationData, что и предметное изменение.</summary>
internal static class AuditLog
{
    public static void Append(StationData data, int antennaId, AuditKind kind, int objectId,
        DateTimeOffset time, int userId, string reason, AntennaState? oldState = null,
        AntennaState? newState = null, TimeWindow? oldWindow = null,
        TimeWindow? newWindow = null, int? sourceChangeId = null)
    {
        if (time.Offset != TimeSpan.Zero) throw new RuleException("Часы аудита должны возвращать UTC.");
        reason = Guard.Text(reason, "Причина");
        var id = data.AuditEntries.Select(e => e.Id).DefaultIfEmpty().Max() + 1;
        data.AuditEntries.Add(new(id, antennaId, kind, objectId, time, userId, reason,
            oldState, newState, oldWindow, newWindow, sourceChangeId));
    }

    public static int ImportLegacy(StationData data)
    {
        var imported = 0;
        foreach (var change in data.MaintenanceChanges)
        {
            if (data.AuditEntries.Any(e => e.SourceChangeId == change.Id)) continue;
            var maintenance = data.Maintenance.SingleOrDefault(m => m.Id == change.MaintenanceId)
                ?? throw new RuleException("История ссылается на отсутствующее обслуживание.");
            var id = data.AuditEntries.Select(e => e.Id).DefaultIfEmpty().Max() + 1;
            // В лабе 3 время события не хранилось: не подменяем его временем импорта.
            data.AuditEntries.Add(new(id, maintenance.AntennaId, AuditKind.MaintenancePostponed,
                maintenance.Id, null, change.UserId, change.Reason,
                OldWindow: change.OldWindow, NewWindow: change.NewWindow,
                SourceChangeId: change.Id, ImportedLegacy: true));
            imported++;
        }
        return imported;
    }
}
