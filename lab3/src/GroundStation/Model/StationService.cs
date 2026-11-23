using System.Security.Cryptography;

namespace GroundStation.Model;

/// <summary>Сценарии предметной области, права и правила размещения сеансов.</summary>
public sealed class StationService(IStationStore store)
{
    private Identity? current;
    private int failedLogins;
    private DateTimeOffset lockedUntil;
    private static string Hash(string password, byte[] salt) => Convert.ToHexString(
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32));

    public void Initialize(string managerPassword, string operatorPassword)
    {
        if (managerPassword.Length < 10 || operatorPassword.Length < 10)
            throw new RuleException("Пароли должны содержать не менее 10 символов.");
        store.Update(data =>
        {
            if (data.Users.Count != 0) throw new RuleException("Хранилище уже инициализировано.");
            foreach (var (id, login, role, antenna, password) in new[]
            {
                (1, "manager", Role.Manager, (int?)null, managerPassword),
                (2, "operator", Role.Operator, (int?)1, operatorPassword)
            })
            {
                var salt = RandomNumberGenerator.GetBytes(16);
                data.Users.Add(new User(id, login, role, antenna, Convert.ToHexString(salt), Hash(password, salt)));
            }
            data.Satellites.AddRange([new(1, "Учебный X-1", Band.X), new(2, "Учебный X-2", Band.X), new(3, "Учебный X-3", Band.X), new(4, "Учебный S-1", Band.S)]);
            data.Antennas.AddRange([new(1, "АК-1", Band.X, AntennaState.Ready), new(2, "АК-2", Band.X, AntennaState.Ready), new(3, "АК-3", Band.S, AntennaState.Faulty)]);
            return true;
        });
    }

    /// <summary>Проверяет пароль и устанавливает личность без передачи роли из интерфейса.</summary>
    public Identity Login(string login, string password)
    {
        current = null;
        if (DateTimeOffset.UtcNow < lockedUntil) throw new RuleException("Вход временно заблокирован на 30 секунд.");
        var user = store.Read(d => d.Users.SingleOrDefault(u => u.Login == login.Trim()));
        // Фиктивный расчёт не даёт пропустить PBKDF2 при неизвестном логине.
        var salt = user is null ? new byte[16] : Convert.FromHexString(user.Salt);
        var actual = Convert.FromHexString(Hash(password, salt));
        var expected = user is null ? new byte[32] : Convert.FromHexString(user.PasswordHash);
        if (user is null || !CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            if (++failedLogins >= 5) { lockedUntil = DateTimeOffset.UtcNow.AddSeconds(30); failedLogins = 0; }
            throw new RuleException("Неверный логин или пароль.");
        }
        failedLogins = 0;
        return current = new(user.Id, user.Login, user.Role, user.AntennaId);
    }

    public void Logout() => current = null;
    private Identity Require(bool manager = false)
    {
        if (current is null) throw new RuleException("Сначала войдите в систему.");
        if (manager && current.Role != Role.Manager) throw new RuleException("Действие доступно руководителю.");
        return current;
    }

    public IReadOnlyList<Antenna> Antennas() { Require(); return store.Read(d => d.Antennas.ToArray()); }
    public IReadOnlyList<Satellite> Satellites() { Require(); return store.Read(d => d.Satellites.ToArray()); }
    public IReadOnlyList<SessionRow> Sessions()
    {
        var user = Require();
        return store.Read(d => d.Sessions.Where(s => user.Role == Role.Manager || s.AntennaId == user.AntennaId)
            .OrderBy(s => s.Window.Start).ToArray());
    }

    private static Antenna? Free(StationData data, IEnumerable<SessionRow> sessions, Band band, TimeWindow window) =>
        data.Antennas.Where(a => a.State == AntennaState.Ready && a.Band == band).OrderBy(a => a.Id)
            .FirstOrDefault(a => !sessions.Any(s => s.AntennaId == a.Id && s.Window.Overlaps(window)) &&
                !data.Maintenance.Any(m => m.AntennaId == a.Id && m.Window.Overlaps(window)));

    /// <summary>Размещает сеанс, при нехватке антенн переносит менее важные в резервные окна.</summary>
    public SessionRow Schedule(SessionRequest request)
    {
        var user = Require(manager: true);
        request.Validate();
        return store.Update(data =>
        {
            var satellite = data.Satellites.SingleOrDefault(s => s.Id == request.SatelliteId)
                ?? throw new RuleException("Космический аппарат не найден.");
            if (data.Sessions.Any(s => s.SatelliteId == request.SatelliteId && s.Window.Overlaps(request.Window)))
                throw new RuleException("У аппарата уже есть сеанс в этом окне.");
            var id = data.Sessions.Select(s => s.Id).DefaultIfEmpty().Max() + 1;
            SessionRow Create(Antenna a, TimeWindow w, TimeWindow? next) => new(id, satellite.Id, a.Id, w,
                next, request.Priority, request.Elevation, Guard.Text(request.DataType, "Тип данных", 80), SessionStatus.Planned);
            var antenna = Free(data, data.Sessions, satellite.Band, request.Window);
            if (antenna is not null)
            {
                var created = Create(antenna, request.Window, request.NextWindow);
                data.Sessions.Add(created);
                return created;
            }
            // Пробуем освободить одну антенну, перенеся все её менее важные сеансы.
            foreach (var candidate in data.Antennas.Where(a => a.State == AntennaState.Ready && a.Band == satellite.Band).OrderBy(a => a.Id))
            {
                if (data.Maintenance.Any(m => m.AntennaId == candidate.Id && m.Window.Overlaps(request.Window))) continue;
                var blocked = data.Sessions.Where(s => s.AntennaId == candidate.Id && s.Window.Overlaps(request.Window)).ToArray();
                if (blocked.Any(s => s.Status != SessionStatus.Planned || s.Priority >= request.Priority || s.NextWindow is null)) continue;
                var trial = data.Sessions.Except(blocked).ToList();
                var created = Create(candidate, request.Window, request.NextWindow);
                trial.Add(created);
                var moves = new List<(SessionRow Before, SessionRow After)>();
                foreach (var old in blocked.OrderByDescending(s => s.Priority).ThenBy(s => s.Id))
                {
                    var band = data.Satellites.Single(s => s.Id == old.SatelliteId).Band;
                    var next = old.NextWindow!.Value;
                    if (trial.Any(s => s.SatelliteId == old.SatelliteId && s.Window.Overlaps(next))) break;
                    var free = Free(data, trial, band, next);
                    if (free is null) break;
                    var moved = new Session(old);
                    moved.Move(free.Id, next);
                    moves.Add((old, moved.ToRow()));
                    trial.Add(moved.ToRow());
                }
                if (moves.Count != blocked.Length) continue;
                data.Sessions.Clear(); data.Sessions.AddRange(trial);
                foreach (var (before, after) in moves) LogMove(data, before, after, id, user.Id);
                return created;
            }
            var alternate = request.NextWindow;
            if (data.Sessions.Any(s => s.SatelliteId == satellite.Id && s.Window.Overlaps(alternate)))
                throw new RuleException("Основное и резервное окна заняты: ничего не изменено.");
            antenna = Free(data, data.Sessions, satellite.Band, alternate);
            if (antenna is null) throw new RuleException("Нет свободной совместимой антенны в обоих окнах.");
            var deferred = Create(antenna, alternate, null);
            data.Sessions.Add(deferred);
            LogMove(data, deferred with { Window = request.Window }, deferred, null, user.Id);
            return deferred;
        });
    }

    private static void LogMove(StationData data, SessionRow before, SessionRow after, int? trigger, int userId) =>
        data.Conflicts.Add(new(data.Conflicts.Count + 1, before.Id, trigger, before.AntennaId,
            before.Window, after.Window, "Антенны заняты; менее приоритетный сеанс перенесён в резервное окно.", userId));

    /// <summary>Фиксирует единственный результат сеанса после проверки прав, пакетов и SHA256.</summary>
    public string Finish(int id, int expected, int received, string payload, string digest)
    {
        var user = Require();
        return store.Update(data =>
        {
            var index = data.Sessions.FindIndex(s => s.Id == id);
            if (index < 0) throw new RuleException("Сеанс не найден.");
            var row = data.Sessions[index];
            if (user.Role == Role.Operator && user.AntennaId != row.AntennaId)
                throw new RuleException("Оператор может завершать только сеансы своей антенны.");
            var reception = Reception.Create(expected, received, payload, digest);
            var session = new Session(row);
            session.Finish(reception);
            data.Sessions[index] = session.ToRow();
            return reception.Description;
        });
    }

    private static void CheckMaintenance(StationData data, int antennaId, TimeWindow window, int? exceptId = null)
    {
        if (!data.Antennas.Any(a => a.Id == antennaId)) throw new RuleException("Антенна не найдена.");
        if (data.Sessions.Any(s => s.AntennaId == antennaId && s.Window.Overlaps(window)) ||
            data.Maintenance.Any(m => m.Id != exceptId && m.AntennaId == antennaId && m.Window.Overlaps(window)))
            throw new RuleException("Обслуживание пересекается с сеансом или другим обслуживанием.");
    }

    /// <summary>Бронирует свободное окно антенны для обслуживания.</summary>
    public Maintenance PlanMaintenance(int antennaId, TimeWindow window, string kind)
    {
        Require(manager: true);
        if (window.End <= window.Start) throw new RuleException("Не задано окно обслуживания.");
        kind = Guard.Text(kind, "Вид обслуживания");
        return store.Update(data =>
        {
            CheckMaintenance(data, antennaId, window);
            var maintenance = new Maintenance(data.Maintenance.Count + 1, antennaId, window, kind);
            data.Maintenance.Add(maintenance);
            return maintenance;
        });
    }

    /// <summary>Переносит обслуживание на более поздний срок с записью прежнего окна и причины.</summary>
    public Maintenance PostponeMaintenance(int id, TimeWindow window, string reason)
    {
        var user = Require(manager: true);
        if (window.End <= window.Start) throw new RuleException("Не задано новое окно обслуживания.");
        reason = Guard.Text(reason, "Причина переноса");
        return store.Update(data =>
        {
            var i = data.Maintenance.FindIndex(m => m.Id == id);
            if (i < 0) throw new RuleException("Обслуживание не найдено.");
            var old = data.Maintenance[i];
            if (window.Start <= old.Window.Start) throw new RuleException("Новая дата должна быть позже исходной.");
            CheckMaintenance(data, old.AntennaId, window, id);
            var updated = old with { Window = window };
            data.Maintenance[i] = updated;
            data.MaintenanceChanges.Add(new(data.MaintenanceChanges.Count + 1, id, old.Window, window, reason, user.Id));
            return updated;
        });
    }

    public (IReadOnlyList<Conflict> Conflicts, IReadOnlyList<MaintenanceChange> Changes) Journal()
    {
        Require(manager: true);
        return store.Read(d => ((IReadOnlyList<Conflict>)d.Conflicts.ToArray(), (IReadOnlyList<MaintenanceChange>)d.MaintenanceChanges.ToArray()));
    }

    /// <summary>Считает итоги по началу сеанса и загрузку с обрезкой интервалов по границам суток UTC.</summary>
    public StationReport Report(DateTimeOffset from, DateTimeOffset to)
    {
        Require(manager: true);
        if (from.Offset != TimeSpan.Zero || to.Offset != TimeSpan.Zero || to <= from || to - from > TimeSpan.FromDays(31))
            throw new RuleException("Период отчёта: от 1 секунды до 31 суток, UTC.");
        return store.Read(data =>
        {
            var selected = data.Sessions.Where(s => s.Window.Start >= from && s.Window.Start < to).ToArray();
            var summary = data.Satellites.Select(sat =>
            {
                var rows = selected.Where(s => s.SatelliteId == sat.Id).ToArray();
                var completed = rows.Count(s => s.Status != SessionStatus.Planned);
                var clean = rows.Count(s => s.Status == SessionStatus.Completed);
                return new SatelliteSummary(sat.Name, rows.Length, completed, clean,
                    completed == 0 ? null : Math.Round(100m * clean / completed, 2));
            }).ToArray();
            var load = new List<DayLoad>();
            for (var day = from.UtcDateTime.Date; day < to.UtcDateTime; day = day.AddDays(1))
            foreach (var ant in data.Antennas)
            {
                var left = new DateTimeOffset(day, TimeSpan.Zero); if (left < from) left = from;
                var right = new DateTimeOffset(day.AddDays(1), TimeSpan.Zero); if (right > to) right = to;
                var minutes = data.Sessions.Where(s => s.AntennaId == ant.Id).Sum(s =>
                    Math.Max(0, (Min(s.Window.End, right) - Max(s.Window.Start, left)).TotalMinutes));
                load.Add(new(ant.Name, DateOnly.FromDateTime(day), minutes, Math.Round(100 * minutes / 1440, 2)));
            }
            return new StationReport(from, to, summary, load,
                data.Conflicts.Count(c => c.OldWindow.Start >= from && c.OldWindow.Start < to),
                data.MaintenanceChanges.Count(c => c.OldWindow.Start >= from && c.OldWindow.Start < to));
        });
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
