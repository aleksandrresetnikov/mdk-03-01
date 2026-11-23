from django.contrib import admin
from .models import Ticket


class TicketAdmin(admin.ModelAdmin):
    # Django Admin предоставляет формы создания, редактирования и удаления.
    # Доступ определяется учётной записью сотрудника и правами Django.
    date_hierarchy = 'created_at'
    # Фильтры справа от списка и поля, отображаемые в строке таблицы.
    list_filter = ('status', 'assignee')
    list_display = ('id', 'title', 'status', 'assignee', 'description', 'updated_at')
    # Строка поиска ищет совпадения в названии заявки и её статусе.
    search_fields = ['title', 'status']


# Регистрируем модель и её конфигурацию в стандартной административной панели.
admin.site.register(Ticket, TicketAdmin)
