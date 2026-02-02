using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SalaReunioes.Web.Domain.Entities;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Hubs;

namespace SalaReunioes.Web.Infrastructure.Services;

public class AgendamentoService(AppDbContext context, IHubContext<AgendamentoHub> hubContext)
{
    // ==========================================
    // SEÇÃO DE AGENDAMENTOS (DASHBOARD & CALENDÁRIO)
    // ==========================================

    /// <summary>
    /// Lista todas as salas com reuniões de hoje em diante.
    /// Ideal para o Dashboard principal.
    /// </summary>
    public async Task<List<Sala>> ListarSalasComAgendamentosAsync()
    {
        // Define o início do dia atual em UTC para comparação no Postgres
        var hojeUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        return await context.Salas
            .Include(s => s.Agendamentos
                .Where(a => a.Inicio >= hojeUtc))
            .OrderBy(s => s.Nome)
            .ToListAsync();
    }

    /// <summary>
    /// Busca todos os agendamentos em um intervalo de datas.
    /// Essencial para o funcionamento do componente de Calendário.
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
    /// Realiza um novo agendamento com validação de conflitos e notificação em tempo real.
    /// </summary>
    public async Task<(bool Sucesso, string Mensagem)> ReservarAsync(Agendamento novo)
    {
        // Garante Kind Utc para evitar erros binários no timestamptz do Postgres
        novo.Inicio = DateTime.SpecifyKind(novo.Inicio, DateTimeKind.Utc);
        novo.Fim = DateTime.SpecifyKind(novo.Fim, DateTimeKind.Utc);

        if (novo.Inicio >= novo.Fim)
            return (false, "A hora de início deve ser anterior à hora de fim.");

        if (novo.Inicio < DateTime.UtcNow)
            return (false, "Não é possível agendar reuniões no passado.");

        // Verificação de sobreposição: (InícioA < FimB) E (FimA > InícioB)
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

            // Notifica todos os clientes conectados via SignalR
            await hubContext.Clients.All.SendAsync("ReceberAtualizacao");

            return (true, "Agendamento realizado com sucesso!");
        }
        catch (Exception)
        {
            return (false, "Erro técnico ao salvar no banco de dados.");
        }
    }

    /// <summary>
    /// Remove um agendamento e atualiza todos os dashboards em tempo real.
    /// </summary>
    public async Task<bool> CancelarAgendamentoAsync(Guid agendamentoId)
    {
        var agendamento = await context.Agendamentos.FindAsync(agendamentoId);
        if (agendamento == null) return false;

        context.Agendamentos.Remove(agendamento);
        await context.SaveChangesAsync();

        // Notifica a remoção para que as salas fiquem "Livre" instantaneamente na UI
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao");

        return true;
    }

    // ==========================================
    // SEÇÃO ADMINISTRATIVA (SISTEMA DE ADMIN)
    // ==========================================

    /// <summary>
    /// Adiciona uma nova sala ao sistema.
    /// </summary>
    public async Task AdicionarSalaAsync(Sala novaSala)
    {
        context.Salas.Add(novaSala);
        await context.SaveChangesAsync();
        
        // Notifica para que a nova sala apareça no dashboard imediatamente
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao");
    }

    /// <summary>
    /// Remove uma sala e limpa todos os seus agendamentos vinculados.
    /// </summary>
    public async Task<bool> ExcluirSalaAsync(Guid salaId)
    {
        var sala = await context.Salas
            .Include(s => s.Agendamentos)
            .FirstOrDefaultAsync(s => s.Id == salaId);

        if (sala == null) return false;

        context.Salas.Remove(sala);
        await context.SaveChangesAsync();

        // Notifica a remoção para todos os clientes
        await hubContext.Clients.All.SendAsync("ReceberAtualizacao");

        return true;
    }

    /// <summary>
    /// Lista todos os agendamentos registrados no banco (Histórico completo).
    /// </summary>
    public async Task<List<Agendamento>> ListarTodosAgendamentosAdminAsync()
    {
        return await context.Agendamentos
            .OrderByDescending(a => a.Inicio)
            .ToListAsync();
    }
}