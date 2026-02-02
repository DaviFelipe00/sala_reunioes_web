using Heron.MudCalendar;
using MudBlazor;

namespace SalaReunioes.Web.Domain.Entities;

public class CustomCalendarItem : CalendarItem
{
    public Color SalaColor { get; set; } = Color.Default;
    public string NomeSala { get; set; } = string.Empty;
}