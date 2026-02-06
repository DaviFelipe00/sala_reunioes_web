using System.ComponentModel.DataAnnotations;

namespace SalaReunioes.Web.Domain.Entities;

public class Agendamento
{
    [Key]
    public Guid Id { get; set; } 

    [Required(ErrorMessage = "A sala deve ser selecionada.")]
    public Guid SalaId { get; set; }

    [Required(ErrorMessage = "O título da reunião é obrigatório.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "O título deve ter entre 3 e 100 caracteres.")]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome do responsável é obrigatório.")]
    [StringLength(50, ErrorMessage = "O nome do responsável é muito longo.")]
    [Display(Name = "Responsável")]
    public string Responsavel { get; set; } = string.Empty;

    [Required(ErrorMessage = "A data e hora de início são obrigatórias.")]
    [Display(Name = "Início")]
    public DateTime Inicio { get; set; }

    [Required(ErrorMessage = "A data e hora de término são obrigatórias.")]
    [Display(Name = "Fim")]
    public DateTime Fim { get; set; }

    // --- CAMPO NOVO PARA PERSONALIZAÇÃO ---
    [StringLength(7)] // Garante o formato hexadecimal (#RRGGBB)
    public string Cor { get; set; } = "#007ACC"; // Define um azul padrão caso o usuário não escolha

    // Propriedade de navegação
    public virtual Sala? Sala { get; set; }
}