using System.Globalization;
using GroundStation.Controller;
using GroundStation.Model;

namespace GroundStation.View;

public sealed class ConsoleView(bool echoInput = false) : IStationView
{
    public string Text(string prompt)
    {
        Console.Write(prompt + ": ");
        var value = Console.ReadLine() ?? "exit";
        if (echoInput) Console.WriteLine(value);
        return value;
    }

    public string Secret(string prompt)
    {
        Console.Write(prompt + ": ");
        if (echoInput || Console.IsInputRedirected)
        {
            var line = Console.ReadLine() ?? "";
            if (echoInput) Console.WriteLine("********");
            return line;
        }
        var password = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return password.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (password.Length > 0) password.Length--; }
            else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar);
        }
    }

    public int Number(string prompt) => int.Parse(Text(prompt), CultureInfo.InvariantCulture);
    public double Decimal(string prompt) => double.Parse(Text(prompt), CultureInfo.InvariantCulture);
    public DateTimeOffset Time(string prompt) => DateTimeOffset.Parse(Text(prompt), CultureInfo.InvariantCulture);
    public void Message(string text) => Console.WriteLine(text);

    public void ShowCatalog(IReadOnlyList<Satellite> satellites, IReadOnlyList<Antenna> antennas)
    {
        foreach (var s in satellites) Console.WriteLine($"Аппарат {s.Id}: {s.Name}, {s.Band}");
        foreach (var a in antennas) Console.WriteLine($"Антенна {a.Id}: {a.Name}, {a.Band}, {a.State}");
    }

    public void ShowSessions(IReadOnlyList<SessionRow> sessions)
    {
        Console.WriteLine("ID | Аппарат | Антенна | Дата UTC         | Время       | Приоритет | Статус");
        foreach (var s in sessions)
            Console.WriteLine($"{s.Id,2} | {s.SatelliteId,7} | {s.AntennaId,7} | {s.Window.Start:yyyy-MM-dd}       | {s.Window.Start:HH:mm}–{s.Window.End:HH:mm} | {s.Priority,9} | {s.Status}");
    }

    public void ShowReport(StationReport report)
    {
        Console.WriteLine($"ОТЧЁТ {report.From:yyyy-MM-dd HH:mm} — {report.To:yyyy-MM-dd HH:mm} UTC");
        Console.WriteLine("Аппарат       | Всего | Завершено | Без потерь | Доля без потерь");
        foreach (var s in report.Satellites)
            Console.WriteLine($"{s.Name,-13} | {s.Total,5} | {s.Completed,9} | {s.WithoutLoss,10} | {(s.SuccessPercent is null ? "—" : s.SuccessPercent.Value.ToString("0.##", CultureInfo.InvariantCulture) + "%")}");
        Console.WriteLine("Загрузка (минуты сеансов / 1440 минут календарных суток):");
        foreach (var d in report.Load)
            Console.WriteLine($"{d.Day:yyyy-MM-dd}  {d.Antenna}: {d.Minutes:0.##} мин.; {d.Percent:0.##}%");
        Console.WriteLine($"Конфликтов с переносом: {report.Conflicts}; переносов обслуживания: {report.Postponements}.");
    }

    public void ShowJournal(IReadOnlyList<Conflict> conflicts, IReadOnlyList<MaintenanceChange> changes)
    {
        Console.WriteLine("ЖУРНАЛ КОНФЛИКТОВ");
        foreach (var c in conflicts)
            Console.WriteLine($"Сеанс {c.SessionId}: {c.OldWindow.Start:HH:mm} → {c.NewWindow.Start:HH:mm}; причина: {c.Reason}");
        Console.WriteLine("ПЕРЕНОСЫ ОБСЛУЖИВАНИЯ");
        foreach (var c in changes)
            Console.WriteLine($"Обслуживание {c.MaintenanceId}: {c.OldWindow.Start:HH:mm} → {c.NewWindow.Start:HH:mm}; причина: {c.Reason}");
    }
}
