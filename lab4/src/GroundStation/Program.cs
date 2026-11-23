using System.Security.Cryptography;
using System.Text;
using GroundStation.Controller;
using GroundStation.Model;
using GroundStation.View;

Console.OutputEncoding = Encoding.UTF8;
var mode = args.FirstOrDefault() ?? "";
var path = args.Skip(1).FirstOrDefault() ?? "data/station.json";
var service = new StationService(new JsonStore(path));
try
{
    if (mode == "--init")
    {
        var view = new ConsoleView();
        service.Initialize(view.Secret("Задайте пароль manager (не менее 10 символов)"),
            view.Secret("Задайте пароль operator (не менее 10 символов)"));
        view.Message("Хранилище создано. Учётные записи: manager, operator (антенна 1).");
    }
    else if (mode == "--demo")
    {
        if (File.Exists(path)) throw new RuleException("Для демонстрации укажите путь к новому файлу.");
        service.Initialize("Demo-Manager-2026!", "Demo-Operator-2026!");
        string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        var script = string.Join('\n', new[]
        {
            "login", "manager", "Demo-Manager-2026!", "catalog",
            "plan", "1", "2026-11-15T10:00Z", "2026-11-15T10:10Z", "2026-11-15T11:40Z", "2026-11-15T11:50Z", "1", "35", "Телеметрия",
            "plan", "2", "2026-11-15T10:00Z", "2026-11-15T10:10Z", "2026-11-15T11:40Z", "2026-11-15T11:50Z", "1", "40", "Целевая информация",
            "plan", "3", "2026-11-15T10:00Z", "2026-11-15T10:10Z", "2026-11-15T11:40Z", "2026-11-15T11:50Z", "3", "45", "Телеметрия", "list",
            "finish", "3", "10", "9", "DEMO-3", Hash("DEMO-3"),
            "finish", "2", "10", "10", "DEMO-2", Hash("DEMO-2"),
            "maintenance", "1", "2026-11-15T10:30Z", "2026-11-15T11:00Z", "Профилактика",
            "postpone", "1", "2026-11-15T12:00Z", "2026-11-15T12:30Z", "Высокая загрузка станции",
            "journal", "report", "2026-11-15T00:00Z", "2026-11-16T00:00Z", "list", "exit"
        });
        Console.SetIn(new StringReader(script));
        new StationController(service, new ConsoleView(echoInput: true)).Run();
    }
    else if (mode is "" or "--data") new StationController(service, new ConsoleView(echoInput: Console.IsInputRedirected)).Run();
    else throw new RuleException("Запуск: --init [файл], --demo [новый файл], --data [файл].");
}
catch (RuleException e) { Console.Error.WriteLine(e.Message); Environment.ExitCode = 1; }
catch (IOException) { Console.Error.WriteLine("Нет доступа к файлу данных."); Environment.ExitCode = 1; }
