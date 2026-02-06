namespace SalaReunioes.Web.Domain.DTOs;

public class RelatorioDto
{
    public int TotalReunioes { get; set; }
    public int TempoMedioMinutos { get; set; }
    public List<DadoGrafico> SalasMaisUsadas { get; set; } = new();
    public List<UsuarioCorDto> PreferenciaCores { get; set; } = new();
}

public class DadoGrafico
{
    public string Nome { get; set; } = string.Empty;
    public double Valor { get; set; } // MudChart usa double
}

public class UsuarioCorDto
{
    public string NomeUsuario { get; set; } = string.Empty;
    public string CorFavorita { get; set; } = string.Empty; // Hexadecimal
    public string NomeCorFavorita { get; set; } = string.Empty; // Nome Amigável
    public int TotalAgendamentos { get; set; }
}