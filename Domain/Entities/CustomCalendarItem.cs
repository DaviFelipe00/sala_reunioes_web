using Heron.MudCalendar;
// Adicione este using para usar o enum Color, caso queira manter SalaColor
using MudBlazor; 

namespace SalaReunioes.Web.Domain.Entities;

// Herda de CalendarItem para usar no componente do Heron
public class CustomCalendarItem : CalendarItem
{
    // Nome da sala para exibição no card
    public string NomeSala { get; set; } = string.Empty;

    // Mantemos a SalaColor para outros usos, se necessário
    public Color SalaColor { get; set; }

    // [NOVO] Propriedade para armazenar a cor HEX aleatória do evento
    public string CorHex { get; set; } = "#1976D2"; // Valor padrão (Azul Primary)
}