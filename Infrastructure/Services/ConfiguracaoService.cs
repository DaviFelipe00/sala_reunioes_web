using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SalaReunioes.Web.Domain.Entities;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Hubs;

namespace SalaReunioes.Web.Infrastructure.Services;

public class ConfiguracaoService(IDbContextFactory<AppDbContext> dbFactory, IHubContext<AgendamentoHub> hubContext)
{
    public async Task<ConfiguracaoSistema> ObterConfiguracaoAsync()
    {
        using var context = await dbFactory.CreateDbContextAsync();
        return await context.Configuracoes.FirstOrDefaultAsync() 
               ?? new ConfiguracaoSistema { HoraAbertura = 8, HoraFechamento = 18 };
    }

    public async Task AtualizarConfiguracaoAsync(ConfiguracaoSistema config)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        var configExistente = await context.Configuracoes.FirstOrDefaultAsync();

        if (configExistente == null) context.Configuracoes.Add(config);
        else
        {
            configExistente.HoraAbertura = config.HoraAbertura;
            configExistente.HoraFechamento = config.HoraFechamento;
        }

        await context.SaveChangesAsync();
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao"); 
    }
}