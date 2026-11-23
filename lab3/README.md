# Лабораторная работа №3

Решетников Александр Алексеевич, ИСПк-404-52-00.

Учёт сеансов наземной станции: планирование, приём данных, обслуживание и отчётность.

- [Отчёт](report.docx)
- [Редактируемые диаграммы](diagrams.drawio)
- [Исходный код](src)

## Запуск

```sh
cd src
dotnet build GroundStation/GroundStation.csproj
dotnet run --project GroundStation -- --init
dotnet run --project GroundStation
```

Нужен .NET 8 SDK. При инициализации задаются свои пароли `manager` и `operator`.
Хранилище создаётся в `src/data/station.json`; оператор закреплён за антенной 1.
