from django import forms
from .models import Ticket


class TicketAdminForm(forms.ModelForm):
    class Meta:
        model = Ticket
        fields = ['title', 'assignee', 'status', 'description']
        widgets = {
            'title': forms.TextInput(attrs={'minlength': 5, 'maxlength': 100, 'size': 60}),
            'description': forms.Textarea(attrs={'minlength': 10, 'maxlength': 2000, 'rows': 6}),
        }
