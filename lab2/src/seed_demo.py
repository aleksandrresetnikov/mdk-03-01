"""Create deterministic local demonstration records; never use production data."""
import os

os.environ.setdefault('DJANGO_SETTINGS_MODULE', 'ticketSite.settings')
import django
django.setup()
from django.contrib.auth.models import User
from ticketApp.models import Ticket

user, _ = User.objects.get_or_create(username='reshetnikov')
user.first_name = 'Александр'
user.last_name = 'Решетников'
user.is_staff = user.is_superuser = True
user.set_password(os.environ.get('DEMO_PASSWORD', 'Lab1-Demo-2026!'))
user.save()

if not Ticket.objects.exists():
    samples = [
        ('Не открывается карточка заявки', 'На странице заявки появляется ошибка при переходе по ссылке.', 'To Do'),
        ('Настроить рабочее место', 'Подготовить браузер и проверить доступ к системе.', 'In Progress'),
        ('Проверить подключение принтера', 'Проверить очередь печати и подключение к локальной сети.', 'In Review'),
        ('Обновить справочную информацию', 'Исправить инструкцию по входу в административную панель.', 'Done'),
        ('Восстановить доступ сотруднику', 'Проверить учётную запись и назначенные разрешения.', 'To Do'),
        ('Уточнить описание обращения', 'Добавить последовательность действий для воспроизведения проблемы.', 'In Progress'),
        ('Проверить отображение последних заявок', 'На главной странице должны отображаться пять последних заявок.', 'To Do'),
    ]
    for title, description, status in samples:
        Ticket.objects.create(title=title, description=description, assignee=user, status=status)
print('Demo ready: user reshetnikov; 7 tickets on an empty database.')
