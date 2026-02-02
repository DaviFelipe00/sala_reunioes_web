using Microsoft.AspNetCore.Identity;

namespace SalaReunioes.Web.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAdminUser(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        // Verifique se o admin já existe para evitar duplicados
        var adminEmail = "admin@rioave.com.br"; // Defina o email do admin
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            var user = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            // Define a password (em produção, use variáveis de ambiente ou Secrets)
            string adminPassword = "Rioave@2026"; 
            
            var result = await userManager.CreateAsync(user, adminPassword);

            if (result.Succeeded)
            {
                // Opcional: Adicionar a um Role "Admin" se estiver a usar Roles
                // await userManager.AddToRoleAsync(user, "Admin");
                Console.WriteLine("Usuário Admin criado com sucesso!");
            }
        }
    }
}