from django.shortcuts import render
from .models import Ticket
from django.http import HttpResponse


def index(request):
    # Публичная страница показывает только пять последних созданных заявок.
    # QuerySet ленивый: выборка выполняется при обращении к нему в шаблоне.
    tickets = Ticket.objects.order_by('-created_at')[:5]
    return render(request, 'index.html', {'tickets': tickets})


def ticket_by_id(request, ticket_id):
    # Получаем одну заявку по первичному ключу и передаём её в HTML-шаблон.
    # get() выбрасывает DoesNotExist для неизвестного id; исходник его не ловит.
    ticket = Ticket.objects.get(pk=ticket_id)
    return render(request, 'ticket_by_id.html', {'ticket': ticket})
