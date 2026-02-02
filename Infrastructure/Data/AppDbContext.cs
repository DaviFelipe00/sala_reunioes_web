using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SalaReunioes.Web.Domain.Entities;

namespace SalaReunioes.Web.Infrastructure.Data;

/// <summary>
/// Contexto do banco de dados configurado para suportar o ASP.NET Core Identity.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Sala> Salas => Set<Sala>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // OBRIGATÓRIO: Chama a configuração base do Identity para criar as tabelas de usuários e permissões
        base.OnModelCreating(modelBuilder);

        // Configura o relacionamento: Uma Sala tem muitos Agendamentos
        modelBuilder.Entity<Sala>()
            .HasMany(s => s.Agendamentos)
            .WithOne()
            .HasForeignKey(a => a.SalaId);

        // Seed Data: Criação das 6 salas padrão da Rio Ave
        // Usamos GUIDs fixos para garantir que o 'database update' não tente criar novas salas toda vez
        for (int i = 1; i <= 6; i++)
        {
            modelBuilder.Entity<Sala>().HasData(new Sala 
            { 
                Id = Guid.Parse($"00000000-0000-0000-0000-00000000000{i}"), 
                Nome = $"Sala {i}", 
                Capacidade = i <= 3 ? 12 : 8 // Exemplo: Salas 1 a 3 são maiores
            });
        }
    }
}