using Microsoft.EntityFrameworkCore;
using SalaReunioes.Web.Domain.DTOs;
using SalaReunioes.Web.Infrastructure.Data;

namespace SalaReunioes.Web.Infrastructure.Services;

public class RelatorioService(IDbContextFactory<AppDbContext> dbFactory)
{
    // Mapeamento centralizado das cores (o mesmo do Dialog)
    private readonly Dictionary<string, string> _mapaCores = new()
    {
        { "#007ACC", "Azul (Padrão)" },
        { "#C62828", "Vermelho (Urgente)" },
        { "#2E7D32", "Verde (Interno)" },
        { "#F57C00", "Laranja (Brainstorm)" },
        { "#7B1FA2", "Roxo (Treinamento)" },
        { "#455A64", "Cinza (Manutenção)" }
    };

    public async Task<RelatorioDto> GerarRelatorioAsync()
    {
        using var context = await dbFactory.CreateDbContextAsync();
        
        // Filtro: Últimos 30 dias
        var inicioMes = DateTime.UtcNow.AddDays(-30);
        
        var dados = await context.Agendamentos
            .Include(a => a.Sala)
            .Where(a => a.Inicio >= inicioMes)
            .AsNoTracking()
            .ToListAsync();

        var relatorio = new RelatorioDto();

        if (dados.Count == 0) return relatorio;

        // 1. Tempo Médio
        var duracaoTotalMinutos = dados.Sum(x => (x.Fim - x.Inicio).TotalMinutes);
        relatorio.TempoMedioMinutos = (int)(duracaoTotalMinutos / dados.Count);

        // 2. Salas Mais Usadas (Top 5)
        // Corrigido para garantir que Valor seja double para o gráfico
        relatorio.SalasMaisUsadas = dados
            .GroupBy(x => x.Sala?.Nome ?? "Sem Sala")
            .Select(g => new DadoGrafico { 
                Nome = g.Key, 
                Valor = (double)g.Count() // Cast explícito para double
            })
            .OrderByDescending(x => x.Valor)
            .Take(5)
            .ToList();

        // 3. Preferência de "Tipo" (Cor) por Usuário
        relatorio.PreferenciaCores = dados
            .GroupBy(x => x.Responsavel)
            .Select(gUsuario => {
                // Pega a cor mais usada (Moda)
                var corMaisUsadaHex = gUsuario
                    .GroupBy(u => u.Cor)
                    .OrderByDescending(c => c.Count())
                    .First().Key;

                // Tenta traduzir o Hex para o Nome, senão usa o Hex
                var nomeCor = _mapaCores.ContainsKey(corMaisUsadaHex) 
                    ? _mapaCores[corMaisUsadaHex] 
                    : "Personalizado";

                return new UsuarioCorDto
                {
                    NomeUsuario = gUsuario.Key,
                    CorFavorita = corMaisUsadaHex, // Mantém o Hex para o visual (bolinha)
                    NomeCorFavorita = nomeCor,     // Novo campo para o texto (ex: "Urgente")
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