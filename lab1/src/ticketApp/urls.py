from django.urls import path
from . import views


# Публичные маршруты: список и карточка. CRUD выполняется в Django Admin.
urlpatterns = [
    path('', views.index, name='index'),
    # Конвертер int проверяет тип идентификатора, но не наличие записи в БД.
    path('ticket/<int:ticket_id>', views.ticket_by_id, name='ticket_by_id'),
]
