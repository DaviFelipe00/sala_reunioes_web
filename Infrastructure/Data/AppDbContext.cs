using Microsoft.EntityFrameworkCore;
using SalaReunioes.Web.Domain.Entities;

namespace SalaReunioes.Web.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Sala> Salas => Set<Sala>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configurações adicionais se necessário
        modelBuilder.Entity<Sala>().HasMany(s => s.Agendamentos).WithOne().HasForeignKey(a => a.SalaId);
    }
}