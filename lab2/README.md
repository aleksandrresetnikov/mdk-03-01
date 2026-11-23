# Лабораторная работа №2

Решетников Александр Алексеевич, ИСПк-404-52-00.

Проверка и исправление обработки данных и доступа в ticketApp.

- [Отчёт](report.docx)
- [Редактируемые диаграммы](diagrams.drawio)
- [Исходный код](src)

## Запуск

```sh
cd src
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r requirements.txt
python manage.py migrate
python seed_demo.py
python manage.py runserver 127.0.0.1:8000
```

Открыть http://127.0.0.1:8000. Django Admin: http://127.0.0.1:8000/admin/.
Учебный пользователь: `reshetnikov`, пароль: `Lab1-Demo-2026!`.

Исходный проект: https://github.com/asankova/ticketApp.
