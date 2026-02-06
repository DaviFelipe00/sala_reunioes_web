using Microsoft.EntityFrameworkCore;
using SalaReunioes.Web.Domain.DTOs;
using SalaReunioes.Web.Infrastructure.Data;

namespace SalaReunioes.Web.Infrastructure.Services;

public class RelatorioService(IDbContextFactory<AppDbContext> dbFactory)
{
    // --- MAPEAMENTO DE CORES (Baseado na entrada do ReservaDialog) ---
    private readonly Dictionary<string, string> _mapaCores = new()
    {
        { "#007ACC", "Alinhamento" },   // Azul
        { "#C62828", "Urgente" },       // Vermelho
        { "#2E7D32", "Cliente" },       // Verde
        { "#F57C00", "Planejamento" },  // Laranja
        { "#7B1FA2", "Treinamento" }    // Roxo
    };

    public async Task<RelatorioDto> GerarRelatorioAsync()
    {
        using var context = await dbFactory.CreateDbContextAsync();
        
        // Filtra os últimos 30 dias para métricas mensais
        var inicioMes = DateTime.UtcNow.AddDays(-30);
        
        var dados = await context.Agendamentos
            .Include(a => a.Sala)
            .Where(a => a.Inicio >= inicioMes)
            .AsNoTracking()
            .ToListAsync();

        var relatorio = new RelatorioDto();

        if (dados.Count == 0) return relatorio;

        // 1. KPI: Tempo Médio
        var duracaoTotalMinutos = dados.Sum(x => (x.Fim - x.Inicio).TotalMinutes);
        relatorio.TempoMedioMinutos = (int)(duracaoTotalMinutos / dados.Count);

        // 2. Gráfico: Salas Mais Usadas
        relatorio.SalasMaisUsadas = dados
            .GroupBy(x => x.Sala?.Nome ?? "Sem Sala")
            .Select(g => new DadoGrafico { 
                Nome = g.Key, 
                Valor = (double)g.Count() 
            })
            .OrderByDescending(x => x.Valor)
            .Take(5)
            .ToList();

        // 3. Tabela: Preferência de Cor por Usuário (Traduzida)
        relatorio.PreferenciaCores = dados
            .GroupBy(x => x.Responsavel)
            .Select(gUsuario => {
                // Descobre a cor mais frequente desse usuário
                var corMaisUsadaHex = gUsuario
                    .GroupBy(u => u.Cor)
                    .OrderByDescending(c => c.Count())
                    .First().Key;

                // TRADUÇÃO: Busca o nome legível no dicionário
                // Se a cor não estiver no mapa (ex: uma cor antiga ou personalizada), exibe "Outro"
                var nomeCorLegivel = _mapaCores.ContainsKey(corMaisUsadaHex) 
                    ? _mapaCores[corMaisUsadaHex] 
                    : "Outro / Personalizado"; 

                return new UsuarioCorDto
                {
                    NomeUsuario = gUsuario.Key,
                    CorFavorita = corMaisUsadaHex,    // Usado para o ícone visual (bolinha)
                    NomeCorFavorita = nomeCorLegivel, // Usado para o texto ("Alinhamento", etc)
                    TotalAgendamentos = gUsuario.Count()
                };
            })
            .OrderByDescending(u => u.TotalAgendamentos)
            .ToList();

        // 4. Total Geral
        relatorio.TotalReunioes = dados.Count;

        return relatorio;
    }
}