using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SalaReunioes.Web.Domain.Entities;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Hubs;

namespace SalaReunioes.Web.Infrastructure.Services;

public class AgendamentoService(AppDbContext context, IHubContext<AgendamentoHub> hubContext)
{
    // Define o fuso horário oficial da Rio Ave (Brasília)
    private static readonly TimeZoneInfo BrasiliaTimeZone = 
        TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");

    // Configurações de Regra de Negócio
    private const int DuracaoMaximaHoras = 4;
    private const int HoraInicioComercial = 8;
    private const int HoraFimComercial = 19;

    // ==========================================
    // SEÇÃO DE AGENDAMENTOS (DASHBOARD & CALENDÁRIO)
    // ==========================================

    // Método otimizado para a HOME (Apenas Futuros)
    public async Task<List<Sala>> ListarSalasComAgendamentosAsync()
    {
        // Obtém o início do dia atual no fuso de Brasília e converte para UTC para o banco
        var agoraBrasilia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrasiliaTimeZone);
        var hojeUtc = TimeZoneInfo.ConvertTimeToUtc(agoraBrasilia.Date, BrasiliaTimeZone);

        return await context.Salas
            .Include(s => s.Agendamentos
                .Where(a => a.Inicio >= hojeUtc))
            .OrderBy(s => s.Nome)
            .ToListAsync();
    }

    // [NOVO] Método específico para o CALENDÁRIO (Traz tudo + Nome da Sala)
    public async Task<List<Agendamento>> ListarAgendamentosCalendarioAsync()
    {
        return await context.Agendamentos
            .Include(a => a.Sala) // Fundamental para o calendário saber de qual sala é a reunião
            .OrderBy(a => a.Inicio)
            .ToListAsync();
    }

    public async Task<List<Agendamento>> ObterAgendamentosPorPeriodoAsync(DateTime inicio, DateTime fim)
    {
        var inicioUtc = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
        var fimUtc = DateTime.SpecifyKind(fim, DateTimeKind.Utc);

        return await context.Agendamentos
            .Where(a => a.Inicio >= inicioUtc && a.Inicio <= fimUtc)
            .OrderBy(a => a.Inicio)
            .ToListAsync();
    }

    public async Task<(bool Sucesso, string Mensagem)> ReservarAsync(Agendamento novo)
    {
        // 1. Normalização de Datas para UTC (Exigência do PostgreSQL timestamptz)
        novo.Inicio = DateTime.SpecifyKind(novo.Inicio, DateTimeKind.Utc);
        novo.Fim = DateTime.SpecifyKind(novo.Fim, DateTimeKind.Utc);

        // 2. Validações Básicas e de Passado
        if (novo.Inicio >= novo.Fim)
            return (false, "A hora de início deve ser anterior à hora de fim.");

        if (novo.Inicio < DateTime.UtcNow)
            return (false, "Não é possível agendar reuniões no passado.");

        // 3. Validação de Duração Máxima
        var duracao = novo.Fim - novo.Inicio;
        if (duracao.TotalHours > DuracaoMaximaHoras)
            return (false, $"A reserva não pode exceder {DuracaoMaximaHoras} horas.");

        // 4. Validação de Horário Comercial (em fuso Brasília)
        var inicioBr = TimeZoneInfo.ConvertTimeFromUtc(novo.Inicio, BrasiliaTimeZone);
        var fimBr = TimeZoneInfo.ConvertTimeFromUtc(novo.Fim, BrasiliaTimeZone);

        if (inicioBr.Hour < HoraInicioComercial || fimBr.Hour > HoraFimComercial || (fimBr.Hour == HoraFimComercial && fimBr.Minute > 0))
            return (false, $"As reservas devem ser feitas entre {HoraInicioComercial:D2}:00 e {HoraFimComercial:D2}:00.");

        // 5. Verificação de Conflitos (Sobreposição)
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

            // Notifica em tempo real via SignalR
            await hubContext.Clients.All.SendAsync("ReceberAtualizacao");

            return (true, "Agendamento realizado com sucesso!");
        }
        catch (Exception)
        {
            // Em produção, aqui entraria um log (ex: Serilog)
            return (false, "Erro técnico ao salvar no banco de dados.");
        }
    }

    public async Task<bool> CancelarAgendamentoAsync(Guid agendamentoId)
    {
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
        context.Salas.Add(novaSala);
        await context.SaveChangesAsync();
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao");
    }

    public async Task<bool> ExcluirSalaAsync(Guid salaId)
    {
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
        return await context.Agendamentos
            .OrderByDescending(a => a.Inicio)
            .ToListAsync();
    }
}