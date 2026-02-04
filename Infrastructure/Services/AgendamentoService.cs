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

    private const int DuracaoMaximaHoras = 4;

    // ==========================================
    // SEÇÃO DE CONFIGURAÇÃO
    // ==========================================

    public async Task<ConfiguracaoSistema> ObterConfiguracaoAsync()
    {
        using var context = await dbFactory.CreateDbContextAsync();
        return await context.Configuracoes.FirstOrDefaultAsync() 
               ?? new ConfiguracaoSistema { HoraAbertura = 8, HoraFechamento = 19 };
    }

    public async Task AtualizarConfiguracaoAsync(ConfiguracaoSistema config)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        context.Configuracoes.Update(config);
        await context.SaveChangesAsync();
    }

    // ==========================================
    // SEÇÃO DE LEITURA
    // ==========================================

    // NOVO MÉTODO: Essencial para o novo ReservaDialog (Grade de Horários)
    public async Task<List<Agendamento>> ObterPorDataESalaAsync(DateTime dataLocal, int salaId)
    {
        using var context = await dbFactory.CreateDbContextAsync();

        // 1. Define o início e fim do dia no TimeZone de Brasília
        var dataBrasilia = new DateTime(dataLocal.Year, dataLocal.Month, dataLocal.Day);
        var inicioDiaBr = TimeZoneInfo.ConvertTimeToUtc(dataBrasilia, BrasiliaTimeZone);
        var fimDiaBr = TimeZoneInfo.ConvertTimeToUtc(dataBrasilia.AddDays(1).AddTicks(-1), BrasiliaTimeZone);

        // 2. Busca agendamentos que colidem com esse dia
        return await context.Agendamentos
            .Where(a => a.SalaId == salaId && 
                        a.DataInicio < fimDiaBr && 
                        a.DataFim > inicioDiaBr)
            .AsNoTracking() // Otimização de performance para leitura
            .ToListAsync();
    }

    public async Task<List<Sala>> ListarSalasComAgendamentosAsync()
    {
        using var context = await dbFactory.CreateDbContextAsync();

        var agoraUtc = DateTime.UtcNow;

        return await context.Salas
            .Include(s => s.Agendamentos.Where(a => a.DataInicio >= agoraUtc))
            .OrderBy(s => s.Nome)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Agendamento>> ListarTodosAgendamentosAsync()
    {
        using var context = await dbFactory.CreateDbContextAsync();
        return await context.Agendamentos
            .Include(a => a.Sala)
            .OrderByDescending(a => a.DataInicio)
            .AsNoTracking()
            .ToListAsync();
    }

    // ==========================================
    // SEÇÃO DE ESCRITA (RESERVAS)
    // ==========================================

    public async Task<(bool Sucesso, string Mensagem)> ReservarAsync(Agendamento novo)
    {
        using var context = await dbFactory.CreateDbContextAsync();

        // 1. Normalização para UTC (Garante consistência no banco)
        if (novo.DataInicio.Kind != DateTimeKind.Utc)
            novo.DataInicio = novo.DataInicio.ToUniversalTime();
        
        if (novo.DataFim.Kind != DateTimeKind.Utc)
            novo.DataFim = novo.DataFim.ToUniversalTime();

        // 2. Validações Básicas
        if (novo.DataInicio >= novo.DataFim)
            return (false, "A hora de início deve ser anterior à hora de fim.");

        if (novo.DataInicio < DateTime.UtcNow.AddMinutes(-5)) // Tolerância de 5 min
            return (false, "Não é possível agendar reuniões no passado.");

        var duracao = novo.DataFim - novo.DataInicio;
        if (duracao.TotalHours > DuracaoMaximaHoras)
            return (false, $"A reserva não pode exceder {DuracaoMaximaHoras} horas.");

        // 3. Validação de Horário de Funcionamento
        var config = await context.Configuracoes.FirstOrDefaultAsync() 
                     ?? new ConfiguracaoSistema { HoraAbertura = 8, HoraFechamento = 19 };

        var inicioBr = TimeZoneInfo.ConvertTimeFromUtc(novo.DataInicio, BrasiliaTimeZone);
        var fimBr = TimeZoneInfo.ConvertTimeFromUtc(novo.DataFim, BrasiliaTimeZone);

        if (inicioBr.Hour < config.HoraAbertura || fimBr.Hour > config.HoraFechamento || (fimBr.Hour == config.HoraFechamento && fimBr.Minute > 0))
        {
            return (false, $"Reservas permitidas apenas entre {config.HoraAbertura:D2}:00 e {config.HoraFechamento:D2}:00.");
        }

        // 4. Verificação de Conflitos (Lock Otimista)
        // Verifica se existe algum agendamento para a MESMA sala que intercepte o horário
        bool conflito = await context.Agendamentos
            .AnyAsync(a => a.SalaId == novo.SalaId &&
                           a.Id != novo.Id && // Ignora a própria reserva se for edição
                           a.DataInicio < novo.DataFim && 
                           a.DataFim > novo.DataInicio);

        if (conflito)
            return (false, "Conflito de horário! Já existe uma reunião nesta sala e horário.");

        try 
        {
            if (novo.Id == 0)
                context.Agendamentos.Add(novo);
            else
                context.Agendamentos.Update(novo);

            await context.SaveChangesAsync();

            // Notifica todos os clientes conectados via SignalR
            await hubContext.Clients.All.SendAsync("ReceberAtualizacao");

            return (true, "Agendamento realizado com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao reservar: {ex.Message}");
            return (false, "Erro técnico ao salvar no banco de dados.");
        }
    }

    public async Task<bool> CancelarAgendamentoAsync(int agendamentoId)
    {
        using var context = await dbFactory.CreateDbContextAsync();

        var agendamento = await context.Agendamentos.FindAsync(agendamentoId);
        if (agendamento == null) return false;

        context.Agendamentos.Remove(agendamento);
        await context.SaveChangesAsync();

        await hubContext.Clients.All.SendAsync("ReceberAtualizacao");
        return true;
    }

    // ==========================================
    // SEÇÃO ADMINISTRATIVA
    // ==========================================

    public async Task AdicionarSalaAsync(Sala novaSala)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        context.Salas.Add(novaSala);
        await context.SaveChangesAsync();
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao");
    }

    public async Task<bool> ExcluirSalaAsync(int salaId)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        
        var sala = await context.Salas
            .Include(s => s.Agendamentos)
            .FirstOrDefaultAsync(s => s.Id == salaId);

        if (sala == null) return false;

        // Regra de negócio: Impedir exclusão se houver agendamentos futuros?
        // Por enquanto, exclui tudo (Cascade delete deve estar configurado no EF ou removemos aqui)
        if (sala.Agendamentos.Any())
            context.Agendamentos.RemoveRange(sala.Agendamentos);

        context.Salas.Remove(sala);
        await context.SaveChangesAsync();
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao");

        return true;
    }
}