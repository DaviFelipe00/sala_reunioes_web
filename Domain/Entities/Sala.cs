namespace SalaReunioes.Web.Domain.Entities;

public class Sala
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = string.Empty; // Ex: "Sala 1"
    public int Capacidade { get; set; }
    public List<Agendamento> Agendamentos { get; set; } = [];
}