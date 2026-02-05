using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SalaReunioes.Web.Domain.Entities;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Hubs;
using System.Runtime.InteropServices;
using System.Data; // Necessário para IsolationLevel

namespace SalaReunioes.Web.Infrastructure.Services;

public class AgendamentoService(
    IDbContextFactory<AppDbContext> dbFactory, 
    IHubContext<AgendamentoHub> hubContext,
    ConfiguracaoService configService)
{
    private static readonly TimeZoneInfo BrasiliaTimeZone = 
        TimeZoneInfo.FindSystemTimeZoneById(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
            ? "E. South America Standard Time" : "America/Recife");

    public async Task<List<Agendamento>> ListarAgendamentosCalendarioAsync(DateTime inicio, DateTime fim)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        
        // Garante conversão para UTC para busca correta no banco
        var inicioUtc = inicio.ToUniversalTime();
        var fimUtc = fim.ToUniversalTime();

        return await context.Agendamentos
            .Include(a => a.Sala)
            .Where(a => a.Inicio < fimUtc && a.Fim > inicioUtc)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<(bool Sucesso, string Mensagem)> ReservarAsync(Agendamento novo)
    {
        using var context = await dbFactory.CreateDbContextAsync();
        
        // 1. Inicia Transação (Blindagem contra dupla reserva no mesmo milissegundo)
        using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            // Normaliza datas para UTC (Boas práticas)
            if (novo.Inicio.Kind != DateTimeKind.Utc) novo.Inicio = novo.Inicio.ToUniversalTime();
            if (novo.Fim.Kind != DateTimeKind.Utc) novo.Fim = novo.Fim.ToUniversalTime();

            // 2. Validação de Horário de Funcionamento (Regra de Negócio)
            var config = await configService.ObterConfiguracaoAsync();
            var inicioBr = TimeZoneInfo.ConvertTimeFromUtc(novo.Inicio, BrasiliaTimeZone);
            var fimBr = TimeZoneInfo.ConvertTimeFromUtc(novo.Fim, BrasiliaTimeZone);

            if (inicioBr.Hour < config.HoraAbertura || 
                fimBr.Hour > config.HoraFechamento || 
                (fimBr.Hour == config.HoraFechamento && fimBr.Minute > 0))
            {
                return (false, $"Reservas permitidas apenas entre {config.HoraAbertura:00}:00 e {config.HoraFechamento:00}:00.");
            }

            // 3. Verificação de Conflito (Dentro da transação = Seguro)
            bool conflito = await context.Agendamentos.AnyAsync(a => 
                a.SalaId == novo.SalaId && 
                a.Id != novo.Id && // Ignora a si mesmo na edição
                a.Inicio < novo.Fim && 
                a.Fim > novo.Inicio);

            if (conflito) 
            {
                return (false, "❌ Já existe uma reunião agendada neste horário (conflito detectado).");
            }

            // 4. Salvar
            if (novo.Id == Guid.Empty) 
            { 
                novo.Id = Guid.NewGuid(); 
                context.Agendamentos.Add(novo); 
            }
            else 
            {
                context.Agendamentos.Update(novo);
            }

            await context.SaveChangesAsync();
            
            // Confirma a transação apenas se tudo deu certo
            await transaction.CommitAsync();

            // Notifica usuários em tempo real (Fora da transação para não travar o banco)
            await hubContext.Clients.All.SendAsync("ReceberAtualizacao");
            
            return (true, "Agendado com sucesso!");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(); // Desfaz tudo se der erro
            return (false, $"Erro crítico ao salvar: {ex.Message}");
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
}