using Microsoft.EntityFrameworkCore;
using SalaReunioes.Web.Domain.Entities;
using SalaReunioes.Web.Infrastructure.Data;

namespace SalaReunioes.Web.Infrastructure.Services;

public class AgendamentoService(AppDbContext context)
{
    /// <summary>
    /// Lista todas as salas e inclui apenas os agendamentos de hoje em diante.
    /// Resolve o erro de timestamptz usando DateTime.UtcNow.Date.
    /// </summary>
    public async Task<List<Sala>> ListarSalasComAgendamentosAsync()
    {
        // Define o início do dia de hoje em UTC para comparação segura no Postgres
        var hojeUtc = DateTime.UtcNow.Date;

        return await context.Salas
            .Include(s => s.Agendamentos
                .Where(a => a.Inicio >= hojeUtc))
            .OrderBy(s => s.Nome)
            .ToListAsync();
    }

    /// <summary>
    /// Realiza um novo agendamento validando se não há conflito de horário.
    /// Força o Kind Utc para evitar erros de operação binária no banco.
    /// </summary>
    public async Task<(bool Sucesso, string Mensagem)> ReservarAsync(Agendamento novo)
    {
        // Garante que as datas vindas da UI sejam tratadas como UTC
        novo.Inicio = DateTime.SpecifyKind(novo.Inicio, DateTimeKind.Utc);
        novo.Fim = DateTime.SpecifyKind(novo.Fim, DateTimeKind.Utc);

        // Validação básica de horário
        if (novo.Inicio >= novo.Fim)
            return (false, "A hora de início deve ser anterior à hora de fim.");

        if (novo.Inicio < DateTime.UtcNow)
            return (false, "Não é possível agendar reuniões no passado.");

        // Verificação de conflitos
        // Lógica: (InícioA < FimB) E (FimA > InícioB)
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

    /// <summary>
    /// Remove um agendamento existente.
    /// </summary>
    public async Task<bool> CancelarAgendamentoAsync(Guid agendamentoId)
    {
        var agendamento = await context.Agendamentos.FindAsync(agendamentoId);
        if (agendamento == null) return false;

        context.Agendamentos.Remove(agendamento);
        await context.SaveChangesAsync();
        return true;
    }
}