namespace SalaReunioes.Web.Domain.Entities;

public class Agendamento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SalaId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Responsavel { get; set; } = string.Empty;
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
}