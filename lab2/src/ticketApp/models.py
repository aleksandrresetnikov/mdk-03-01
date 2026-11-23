from django.core.validators import MinLengthValidator, MaxLengthValidator
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
    title = models.CharField(max_length=100, validators=[MinLengthValidator(5)])
    # Удаление исполнителя не должно удалять историю заявок.
    assignee = models.ForeignKey(User, null=True, blank=True, on_delete=models.SET_NULL)
    # choices формирует список в форме, default применяется к новой заявке.
    status = models.CharField(max_length=25, choices=TicketStatus.choices, default=TicketStatus.TO_DO)
    description = models.TextField(validators=[MinLengthValidator(10), MaxLengthValidator(2000)])
    # Дата создания устанавливается один раз; дата изменения — при save().
    created_at = models.DateTimeField('created at', auto_now_add=True)
    updated_at = models.DateTimeField('updated at', auto_now=True)

    def save(self, *args, **kwargs):
        # Валидация нужна и при сохранении из Python, минуя ModelForm.
        # QuerySet.update() и bulk_create() этот метод не вызывают.
        self.title = self.title.strip()
        self.description = self.description.strip()
        self.full_clean()
        return super().save(*args, **kwargs)
