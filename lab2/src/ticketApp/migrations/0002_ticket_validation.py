import django.core.validators
import django.db.models.deletion
from django.conf import settings
from django.db import migrations, models


def normalize_statuses(apps, schema_editor):
    Ticket = apps.get_model('ticketApp', 'Ticket')
    mapping = {'to_do': 'To Do', 'in_progress': 'In Progress', 'in_review': 'In Review', 'done': 'Done'}
    for old, new in mapping.items():
        Ticket.objects.using(schema_editor.connection.alias).filter(status=old).update(status=new)


def restore_statuses(apps, schema_editor):
    Ticket = apps.get_model('ticketApp', 'Ticket')
    mapping = {'To Do': 'to_do', 'In Progress': 'in_progress', 'In Review': 'in_review', 'Done': 'done'}
    for old, new in mapping.items():
        Ticket.objects.using(schema_editor.connection.alias).filter(status=old).update(status=new)


class Migration(migrations.Migration):
    dependencies = [
        ('ticketApp', '0001_initial'),
        migrations.swappable_dependency(settings.AUTH_USER_MODEL),
    ]

    operations = [
        # В начальной миграции хранились коды to_do, хотя models.py уже использует To Do.
        migrations.RunPython(normalize_statuses, restore_statuses),
        migrations.AlterField(
            model_name='ticket', name='status',
            field=models.CharField(choices=[('To Do', 'To Do'), ('In Progress', 'In Progress'), ('In Review', 'In Review'), ('Done', 'Done')], default='To Do', max_length=25),
        ),
        migrations.AlterField(
            model_name='ticket', name='title',
            field=models.CharField(max_length=100, validators=[django.core.validators.MinLengthValidator(5)]),
        ),
        migrations.AlterField(
            model_name='ticket', name='description',
            field=models.TextField(validators=[django.core.validators.MinLengthValidator(10), django.core.validators.MaxLengthValidator(2000)]),
        ),
        migrations.AlterField(
            model_name='ticket', name='assignee',
            field=models.ForeignKey(blank=True, null=True, on_delete=django.db.models.deletion.SET_NULL, to=settings.AUTH_USER_MODEL),
        ),
    ]
