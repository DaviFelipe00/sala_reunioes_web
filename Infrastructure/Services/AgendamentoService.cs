using Microsoft.EntityFrameworkCore;
using SalaReunioes.Web.Domain.Entities;
using SalaReunioes.Web.Infrastructure.Data;

namespace SalaReunioes.Web.Infrastructure.Services;

public class AgendamentoService(AppDbContext context)
{
    /// <summary>
    /// Lista todas as salas com reuniões de hoje em diante (Ideal para o Dashboard).
    /// </summary>
    public async Task<List<Sala>> ListarSalasComAgendamentosAsync()
    {
        var hojeUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        return await context.Salas
            .Include(s => s.Agendamentos
                .Where(a => a.Inicio >= hojeUtc))
            .OrderBy(s => s.Nome)
            .ToListAsync();
    }

    /// <summary>
    /// Busca todos os agendamentos num intervalo específico (Essencial para o Calendário).
    /// </summary>
    public async Task<List<Agendamento>> ObterAgendamentosPorPeriodoAsync(DateTime inicio, DateTime fim)
    {
        var inicioUtc = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
        var fimUtc = DateTime.SpecifyKind(fim, DateTimeKind.Utc);

        return await context.Agendamentos
            .Where(a => a.Inicio >= inicioUtc && a.Inicio <= fimUtc)
            .OrderBy(a => a.Inicio)
            .ToListAsync();
    }

    /// <summary>
    /// Realiza um novo agendamento validando conflitos e forçando UTC.
    /// </summary>
    public async Task<(bool Sucesso, string Mensagem)> ReservarAsync(Agendamento novo)
    {
        // Garante Kind Utc para compatibilidade com PostgreSQL timestamptz
        novo.Inicio = DateTime.SpecifyKind(novo.Inicio, DateTimeKind.Utc);
        novo.Fim = DateTime.SpecifyKind(novo.Fim, DateTimeKind.Utc);

        if (novo.Inicio >= novo.Fim)
            return (false, "A hora de início deve ser anterior à hora de fim.");

        if (novo.Inicio < DateTime.UtcNow)
            return (false, "Não é possível agendar reuniões no passado.");

        // Lógica de sobreposição: (InícioA < FimB) E (FimA > InícioB)
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
            return (true, "Agendamento realizado com sucesso!");
        }
        catch (Exception)
        {
            return (false, "Erro técnico ao salvar no banco de dados.");
        }
    }

    public async Task<bool> CancelarAgendamentoAsync(Guid agendamentoId)
    {
        var agendamento = await context.Agendamentos.FindAsync(agendamentoId);
        if (agendamento == null) return false;

        context.Agendamentos.Remove(agendamento);
        await context.SaveChangesAsync();
        return true;
    }
}