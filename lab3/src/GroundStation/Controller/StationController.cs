using GroundStation.Model;

namespace GroundStation.Controller;

/// <summary>Представление сообщает ввод и отображает готовые результаты.</summary>
public interface IStationView
{
    string Text(string prompt);
    string Secret(string prompt);
    int Number(string prompt);
    double Decimal(string prompt);
    DateTimeOffset Time(string prompt);
    void Message(string text);
    void ShowCatalog(IReadOnlyList<Satellite> satellites, IReadOnlyList<Antenna> antennas);
    void ShowSessions(IReadOnlyList<SessionRow> sessions);
    void ShowReport(StationReport report);
    void ShowJournal(IReadOnlyList<Conflict> conflicts, IReadOnlyList<MaintenanceChange> changes);
}

/// <summary>Связывает команды интерфейса со сценариями модели; хранилище не открывает.</summary>
public sealed class StationController(StationService service, IStationView view)
{
    public void Run()
    {
        view.Message("Наземная станция — учёт сеансов связи. Время в UTC.");
        while (true)
        {
            view.Message("login | catalog | list | plan | finish | maintenance | postpone | journal | report | logout | exit");
            var command = view.Text("Команда");
            if (command == "exit") break;
            Execute(command);
        }
    }

    private TimeWindow Window(string name) => new(view.Time(name + ": начало"), view.Time(name + ": окончание"));

    public void Execute(string command)
    {
        try
        {
            switch (command)
            {
                case "login":
                    var identity = service.Login(view.Text("Логин"), view.Secret("Пароль"));
                    view.Message($"Вход выполнен: {identity.Login}, {identity.Role}.");
                    break;
                case "catalog": view.ShowCatalog(service.Satellites(), service.Antennas()); break;
                case "list": view.ShowSessions(service.Sessions()); break;
                case "plan":
                    var satelliteId = view.Number("ID аппарата");
                    var window = Window("Основное окно");
                    var next = Window("Резервное окно");
                    var priority = view.Number("Приоритет (1–3)");
                    var elevation = view.Decimal("Угол места (0–90)");
                    var result = service.Schedule(new(satelliteId, window, next, priority, elevation, view.Text("Тип данных")));
                    view.Message($"Сеанс №{result.Id}: антенна {result.AntennaId}, {result.Window.Start:HH:mm}–{result.Window.End:HH:mm}, {result.Status}.");
                    break;
                case "finish":
                    var id = view.Number("ID сеанса");
                    var expected = view.Number("Ожидалось пакетов");
                    var received = view.Number("Принято пакетов");
                    var payload = view.Text("Данные (учебный текст)");
                    var digest = view.Text("SHA256 отправителя");
                    view.Message(service.Finish(id, expected, received, payload, digest));
                    break;
                case "maintenance":
                    var antennaId = view.Number("ID антенны");
                    var maintenanceWindow = Window("Обслуживание");
                    var maintenance = service.PlanMaintenance(antennaId, maintenanceWindow, view.Text("Вид обслуживания"));
                    view.Message($"Обслуживание №{maintenance.Id} запланировано.");
                    break;
                case "postpone":
                    var maintenanceId = view.Number("ID обслуживания");
                    var newWindow = Window("Новый срок");
                    service.PostponeMaintenance(maintenanceId, newWindow, view.Text("Причина"));
                    view.Message("Обслуживание перенесено; причина и прежний срок сохранены.");
                    break;
                case "report": view.ShowReport(service.Report(view.Time("Начало периода"), view.Time("Конец периода"))); break;
                case "journal":
                    var journal = service.Journal();
                    view.ShowJournal(journal.Conflicts, journal.Changes);
                    break;
                case "logout": service.Logout(); view.Message("Выход выполнен."); break;
                default: view.Message("Неизвестная команда."); break;
            }
        }
        catch (RuleException e) { view.Message("Ошибка: " + e.Message); }
        catch (FormatException) { view.Message("Ошибка: неверный формат числа или даты."); }
        catch (OverflowException) { view.Message("Ошибка: число вне допустимого диапазона."); }
        catch (IOException) { view.Message("Ошибка доступа к данным. Файл не перезаписан; повторите действие."); }
    }
}
