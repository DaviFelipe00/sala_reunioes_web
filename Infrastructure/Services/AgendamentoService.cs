using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SalaReunioes.Web.Domain.Entities;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Hubs;

namespace SalaReunioes.Web.Infrastructure.Services;

// 1. Injetamos o IDbContextFactory em vez do AppDbContext direto
public class AgendamentoService(IDbContextFactory<AppDbContext> dbFactory, IHubContext<AgendamentoHub> hubContext)
{
    private static readonly TimeZoneInfo BrasiliaTimeZone = 
        TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");

    private const int DuracaoMaximaHoras = 4;
    private const int HoraInicioComercial = 8;
    private const int HoraFimComercial = 19;

    // ==========================================
    // SEÇÃO DE LEITURA (DASHBOARD & CALENDÁRIO)
    // ==========================================

    public async Task<List<Sala>> ListarSalasComAgendamentosAsync()
    {
        // 2. O padrão "using" garante que a conexão fecha logo após o return
        using var context = dbFactory.CreateDbContext();

        var agoraBrasilia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrasiliaTimeZone);
        var hojeUtc = TimeZoneInfo.ConvertTimeToUtc(agoraBrasilia.Date, BrasiliaTimeZone);

        return await context.Salas
            .Include(s => s.Agendamentos.Where(a => a.Inicio >= hojeUtc))
            .OrderBy(s => s.Nome)
            .ToListAsync();
    }

    public async Task<List<Agendamento>> ListarAgendamentosCalendarioAsync()
    {
        using var context = dbFactory.CreateDbContext();

        return await context.Agendamentos
            .Include(a => a.Sala)
            .OrderBy(a => a.Inicio)
            .ToListAsync();
    }

    public async Task<List<Agendamento>> ObterAgendamentosPorPeriodoAsync(DateTime inicio, DateTime fim)
    {
        using var context = dbFactory.CreateDbContext();

        var inicioUtc = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
        var fimUtc = DateTime.SpecifyKind(fim, DateTimeKind.Utc);

        return await context.Agendamentos
            .Where(a => a.Inicio >= inicioUtc && a.Inicio <= fimUtc)
            .OrderBy(a => a.Inicio)
            .ToListAsync();
    }

    // ==========================================
    // SEÇÃO DE ESCRITA (RESERVAS)
    // ==========================================

    public async Task<(bool Sucesso, string Mensagem)> ReservarAsync(Agendamento novo)
    {
        // Criamos um contexto isolado apenas para esta transação
        using var context = dbFactory.CreateDbContext();

        // 1. Normalização
        novo.Inicio = DateTime.SpecifyKind(novo.Inicio, DateTimeKind.Utc);
        novo.Fim = DateTime.SpecifyKind(novo.Fim, DateTimeKind.Utc);

        // 2. Validações de Regra de Negócio (Sem acesso ao banco)
        if (novo.Inicio >= novo.Fim)
            return (false, "A hora de início deve ser anterior à hora de fim.");

        if (novo.Inicio < DateTime.UtcNow)
            return (false, "Não é possível agendar reuniões no passado.");

        var duracao = novo.Fim - novo.Inicio;
        if (duracao.TotalHours > DuracaoMaximaHoras)
            return (false, $"A reserva não pode exceder {DuracaoMaximaHoras} horas.");

        var inicioBr = TimeZoneInfo.ConvertTimeFromUtc(novo.Inicio, BrasiliaTimeZone);
        var fimBr = TimeZoneInfo.ConvertTimeFromUtc(novo.Fim, BrasiliaTimeZone);

        if (inicioBr.Hour < HoraInicioComercial || fimBr.Hour > HoraFimComercial || (fimBr.Hour == HoraFimComercial && fimBr.Minute > 0))
            return (false, $"As reservas devem ser feitas entre {HoraInicioComercial:D2}:00 e {HoraFimComercial:D2}:00.");

        // 3. Verificação de Conflitos (Acesso ao banco)
        var conflito = await context.Agendamentos
            .AnyAsync(a => a.SalaId == novo.SalaId && 
                           a.Inicio < novo.Fim && 
                           a.Fim > novo.Inicio);

        if (conflito)
            return (false, "Já existe uma reunião agendada para este horário nesta sala.");

        try 
        {
            context.Agendamentos.Add(novo);
            await context.SaveChangesAsync();

            // Notifica via SignalR (fora do contexto do banco)
            await hubContext.Clients.All.SendAsync("ReceberAtualizacao");

            return (true, "Agendamento realizado com sucesso!");
        }
        catch (Exception ex)
        {
            // Logar o erro real no console para debug
            Console.WriteLine($"Erro ao reservar: {ex.Message}");
            return (false, "Erro técnico ao salvar no banco de dados.");
        }
    }

    public async Task<bool> CancelarAgendamentoAsync(Guid agendamentoId)
    {
        using var context = dbFactory.CreateDbContext();

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
        using var context = dbFactory.CreateDbContext();
        context.Salas.Add(novaSala);
        await context.SaveChangesAsync();
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao");
    }

    public async Task<bool> ExcluirSalaAsync(Guid salaId)
    {
        using var context = dbFactory.CreateDbContext();
        
        var sala = await context.Salas
            .Include(s => s.Agendamentos)
            .FirstOrDefaultAsync(s => s.Id == salaId);

        if (sala == null) return false;

        context.Salas.Remove(sala);
        await context.SaveChangesAsync();
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao");

        return true;
    }

    public async Task<List<Agendamento>> ListarTodosAgendamentosAdminAsync()
    {
        using var context = dbFactory.CreateDbContext();
        return await context.Agendamentos
            .OrderByDescending(a => a.Inicio)
            .ToListAsync();
    }
}