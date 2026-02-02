using Heron.MudCalendar;
using MudBlazor;

namespace SalaReunioes.Web.Domain.Entities;

public class CustomCalendarItem : CalendarItem
{
    // Adicionamos a cor da sala e o nome para facilitar o acesso no template
    public Color SalaColor { get; set; } = Color.Default;
    public string NomeSala { get; set; } = string.Empty;
}