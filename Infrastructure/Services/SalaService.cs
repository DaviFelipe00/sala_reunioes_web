using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SalaReunioes.Web.Domain.Entities;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Hubs;

namespace SalaReunioes.Web.Infrastructure.Services;

public class SalaService(IDbContextFactory<AppDbContext> dbFactory, IHubContext<AgendamentoHub> hubContext)
{
    // Mantemos este para compatibilidade com outras telas, se houver
    public async Task<List<Sala>> ListarSalasComAgendamentosAsync()
    {
        return await ListarAgendaDoDiaAsync(DateTime.Today);
    }

    // --- NOVO MÉTODO PRINCIPAL ---
    public async Task<List<Sala>> ListarAgendaDoDiaAsync(DateTime data)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        
        // Define o intervalo de 00:00:00 a 23:59:59 da data solicitada (em UTC)
        var inicioDia = data.Date.ToUniversalTime();
        var fimDia = inicioDia.AddDays(1).AddTicks(-1);

        return await context.Salas
            .Include(s => s.Agendamentos
                .Where(a => a.Inicio >= inicioDia && a.Inicio <= fimDia)) // Filtra EXATAMENTE o dia
            .OrderBy(s => s.Nome)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AdicionarSalaAsync(Sala sala)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        context.Salas.Add(sala);
        await context.SaveChangesAsync();
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao");
    }

    public async Task ExcluirSalaAsync(Guid id)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        var sala = await context.Salas.FindAsync(id);
        if (sala != null)
        {
            context.Salas.Remove(sala);
            await context.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("ReceberAtualizacao");
        }
    }
}