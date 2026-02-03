using System.ComponentModel.DataAnnotations;

namespace SalaReunioes.Web.Domain.Entities;

public class ConfiguracaoSistema
{
    [Key]
    public Guid Id { get; set; }

    // Definimos os padrões 8 e 18 aqui, caso o banco esteja vazio
    public int HoraAbertura { get; set; } = 8; 
    public int HoraFechamento { get; set; } = 18;
}