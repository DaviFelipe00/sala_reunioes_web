using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SalaReunioes.Web.Domain.Entities;

namespace SalaReunioes.Web.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAdminUser(IServiceProvider serviceProvider)
    {
        // Cria um escopo temporário para obter os serviços
        using var scope = serviceProvider.CreateScope();
        
        // 1. Serviços Necessários
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ==========================================
        // 1. SEED DE USUÁRIO ADMINISTRADOR
        // ==========================================
        var adminEmail = "admin@rioave.com.br";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            var user = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            // Senha forte padrão
            string adminPassword = "Rioave@2026"; 
            
            var result = await userManager.CreateAsync(user, adminPassword);

            if (result.Succeeded)
            {
                Console.WriteLine("✅ Usuário Admin criado com sucesso!");
            }
            else
            {
                Console.WriteLine($"❌ Erro ao criar Admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        // ==========================================
        // 2. SEED DE CONFIGURAÇÕES DO SISTEMA (Horários)
        // ==========================================
        // Verifica se já existe alguma configuração no banco
        if (!await context.Configuracoes.AnyAsync())
        {
            Console.WriteLine("⚙️ Criando configurações padrão do sistema...");
            
            context.Configuracoes.Add(new ConfiguracaoSistema 
            { 
                Id = Guid.NewGuid(),
                HoraAbertura = 8,  // Padrão: 08:00
                HoraFechamento = 18 // Padrão: 18:00
            });
            
            await context.SaveChangesAsync();
            Console.WriteLine("✅ Configurações padrão aplicadas!");
        }
    }
}