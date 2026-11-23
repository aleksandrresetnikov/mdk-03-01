from django.db import models
from django.contrib.auth.models import User


class TicketStatus(models.TextChoices):
    # Это допустимые значения поля, а не автомат состояний.
    # Последовательность переходов между статусами в исходнике не ограничена.
    TO_DO = 'To Do'
    IN_PROGRESS = 'In Progress'
    IN_REVIEW = 'In Review'
    DONE = 'Done'


class Ticket(models.Model):
    # Первичный ключ id добавляется Django автоматически как BigAutoField.
    title = models.CharField(max_length=100)
    # Исполнитель необязателен. CASCADE означает, что удаление пользователя
    # удаляет и назначенные ему заявки; это поведение исходного проекта.
    assignee = models.ForeignKey(User, null=True, blank=True, on_delete=models.CASCADE)
    # choices формирует список в форме, default применяется к новой заявке.
    status = models.CharField(max_length=25, choices=TicketStatus.choices, default=TicketStatus.TO_DO)
    description = models.TextField()
    # Дата создания устанавливается один раз; дата изменения — при save().
    created_at = models.DateTimeField('created at', auto_now_add=True)
    updated_at = models.DateTimeField('updated at', auto_now=True)
