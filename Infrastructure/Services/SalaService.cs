using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SalaReunioes.Web.Domain.Entities;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Hubs;

namespace SalaReunioes.Web.Infrastructure.Services;

public class SalaService(IDbContextFactory<AppDbContext> dbFactory, IHubContext<AgendamentoHub> hubContext)
{
    public async Task<List<Sala>> ListarSalasComAgendamentosAsync()
    {
        using var context = await dbFactory.CreateDbContextAsync();
        var agoraUtc = DateTime.UtcNow;

        return await context.Salas
            .Include(s => s.Agendamentos.Where(a => a.Inicio >= agoraUtc))
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