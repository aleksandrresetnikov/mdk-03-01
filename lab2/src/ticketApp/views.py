from django.contrib.auth.decorators import login_required, permission_required
from django.shortcuts import get_object_or_404, render
from .models import Ticket


# Заявки в учебной доработке считаются внутренними: нужны вход и право просмотра.
@login_required
@permission_required('ticketApp.view_ticket', raise_exception=True)
def index(request):
    # Публичная страница показывает только пять последних созданных заявок.
    # QuerySet ленивый: выборка выполняется при обращении к нему в шаблоне.
    tickets = Ticket.objects.order_by('-created_at')[:5]
    return render(request, 'index.html', {'tickets': tickets})


@login_required
@permission_required('ticketApp.view_ticket', raise_exception=True)
def ticket_by_id(request, ticket_id):
    # Получаем одну заявку по первичному ключу и передаём её в HTML-шаблон.
    # Неизвестный идентификатор возвращает 404 вместо исключения и HTTP 500.
    ticket = get_object_or_404(Ticket, pk=ticket_id)
    return render(request, 'ticket_by_id.html', {'ticket': ticket})
