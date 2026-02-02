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

        // --- CORREÇÃO DO MAPEAMENTO ---
        // Configuramos explicitamente que um Agendamento pertence a uma Sala.
        // O uso de .WithOne(a => a.Sala) impede que o EF crie a coluna fantasma 'SalaId1'.
        modelBuilder.Entity<Sala>()
            .HasMany(s => s.Agendamentos)
            .WithOne(a => a.Sala) 
            .HasForeignKey(a => a.SalaId)
            .OnDelete(DeleteBehavior.Cascade); // Exclui agendamentos se a sala for removida

        // --- SEED DATA ---
        // Usamos GUIDs fixos para garantir que o 'database update' seja idempotente
        for (int i = 1; i <= 6; i++)
        {
            modelBuilder.Entity<Sala>().HasData(new Sala 
            { 
                Id = Guid.Parse($"00000000-0000-0000-0000-00000000000{i}"), 
                Nome = $"Sala {i}", 
                Capacidade = i <= 3 ? 12 : 8 
            });
        }
    }
}