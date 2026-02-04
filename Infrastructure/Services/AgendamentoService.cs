using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SalaReunioes.Web.Domain.Entities;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Hubs;

namespace SalaReunioes.Web.Infrastructure.Services;

public class AgendamentoService(IDbContextFactory<AppDbContext> dbFactory, IHubContext<AgendamentoHub> hubContext)
{
    private static readonly TimeZoneInfo BrasiliaTimeZone = 
        TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");

    // ==========================================================
    // LEITURA (READ)
    // ==========================================================

    public async Task<List<Agendamento>> ObterPorDataESalaAsync(DateTime dataLocal, Guid salaId)
    {
        using var context = await dbFactory.CreateDbContextAsync();

        var dataBrasilia = new DateTime(dataLocal.Year, dataLocal.Month, dataLocal.Day);
        var inicioDia = TimeZoneInfo.ConvertTimeToUtc(dataBrasilia, BrasiliaTimeZone);
        var fimDia = TimeZoneInfo.ConvertTimeToUtc(dataBrasilia.AddDays(1).AddTicks(-1), BrasiliaTimeZone);

        return await context.Agendamentos
            .Where(a => a.SalaId == salaId && 
                        a.Inicio < fimDia && 
                        a.Fim > inicioDia)
            .AsNoTracking()
            .ToListAsync();
    }

    // --- CORREÇÃO 1: Método que faltava para o Calendário ---
    public async Task<List<Agendamento>> ListarAgendamentosCalendarioAsync(DateTime inicio, DateTime fim)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        
        // Garante que a comparação seja feita em UTC
        var inicioUtc = inicio.ToUniversalTime();
        var fimUtc = fim.ToUniversalTime();

        return await context.Agendamentos
            .Include(a => a.Sala) // Inclui dados da sala para exibir cor/nome
            .Where(a => a.Inicio < fimUtc && a.Fim > inicioUtc)
            .AsNoTracking()
            .ToListAsync();
    }

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

    public async Task<List<Agendamento>> ListarTodosAgendamentosAsync()
    {
        using var context = await dbFactory.CreateDbContextAsync();
        return await context.Agendamentos
            .Include(a => a.Sala)
            .OrderByDescending(a => a.Inicio)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ConfiguracaoSistema> ObterConfiguracaoAsync()
    {
        using var context = await dbFactory.CreateDbContextAsync();
        return await context.Configuracoes.FirstOrDefaultAsync() 
               ?? new ConfiguracaoSistema { HoraAbertura = 8, HoraFechamento = 19 };
    }

    // ==========================================================
    // ESCRITA (WRITE)
    // ==========================================================

    // --- CORREÇÃO 2: Método que faltava para Configurações ---
    public async Task AtualizarConfiguracaoAsync(ConfiguracaoSistema config)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        
        var configExistente = await context.Configuracoes.FirstOrDefaultAsync();

        if (configExistente == null)
        {
            // Se não existe, cria
            context.Configuracoes.Add(config);
        }
        else
        {
            // Se existe, atualiza os valores
            configExistente.HoraAbertura = config.HoraAbertura;
            configExistente.HoraFechamento = config.HoraFechamento;
            // Adicione outros campos aqui se houver
        }

        await context.SaveChangesAsync();
        // Avisa a todos para atualizar regras de horário
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao"); 
    }

    public async Task<(bool Sucesso, string Mensagem)> ReservarAsync(Agendamento novo)
    {
        using var context = await dbFactory.CreateDbContextAsync();

        // 1. Garantir UTC
        if (novo.Inicio.Kind != DateTimeKind.Utc)
            novo.Inicio = novo.Inicio.ToUniversalTime();
        if (novo.Fim.Kind != DateTimeKind.Utc)
            novo.Fim = novo.Fim.ToUniversalTime();

        // 2. Validações Básicas
        if (novo.Inicio >= novo.Fim)
            return (false, "A hora de início deve ser anterior ao fim.");

        if (novo.Inicio < DateTime.UtcNow.AddMinutes(-5))
            return (false, "Não é possível agendar no passado.");

        // 3. Validação de Horário Comercial
        var config = await context.Configuracoes.FirstOrDefaultAsync() 
                     ?? new ConfiguracaoSistema { HoraAbertura = 8, HoraFechamento = 19 };

        var inicioBr = TimeZoneInfo.ConvertTimeFromUtc(novo.Inicio, BrasiliaTimeZone);
        var fimBr = TimeZoneInfo.ConvertTimeFromUtc(novo.Fim, BrasiliaTimeZone);

        if (inicioBr.Hour < config.HoraAbertura || fimBr.Hour > config.HoraFechamento || (fimBr.Hour == config.HoraFechamento && fimBr.Minute > 0))
        {
            return (false, $"Reservas permitidas apenas entre {config.HoraAbertura:00}:00 e {config.HoraFechamento:00}:00.");
        }

        // 4. Checagem de Conflito
        bool conflito = await context.Agendamentos
            .AnyAsync(a => a.SalaId == novo.SalaId &&
                           a.Id != novo.Id && // Ignora ele mesmo na edição
                           a.Inicio < novo.Fim && 
                           a.Fim > novo.Inicio);

        if (conflito)
            return (false, "Já existe uma reunião agendada para este horário.");

        try
        {
            if (novo.Id == Guid.Empty)
                context.Agendamentos.Add(novo);
            else
                context.Agendamentos.Update(novo);

            await context.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("ReceberAtualizacao");
            return (true, "Agendado com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao salvar: {ex.Message}");
        }
    }

    public async Task<bool> CancelarAgendamentoAsync(Guid id)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        var agendamento = await context.Agendamentos.FindAsync(id);
        if (agendamento == null) return false;

        context.Agendamentos.Remove(agendamento);
        await context.SaveChangesAsync();
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao");
        return true;
    }
    
    // Métodos de Gerenciamento de Salas

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
        if(sala != null) {
            context.Salas.Remove(sala);
            await context.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("ReceberAtualizacao");
        }
    }
}