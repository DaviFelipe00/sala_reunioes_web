using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MudBlazor.Services;
using SalaReunioes.Web.Components;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Services;
using SalaReunioes.Web.Infrastructure.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração do Banco de Dados (COM FACTORY)
// Alterado para AddDbContextFactory para suportar concorrência no Blazor Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Configuração do ASP.NET Core Identity
builder.Services.AddIdentityCore<IdentityUser>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager();

// 3. Autenticação e Autorização
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState(); 

// 4. Interface e Real-time (SignalR e MudBlazor)
builder.Services.AddMudServices();
builder.Services.AddSignalR();

// 5. Serviços de Negócio
// Mantemos Scoped pois o serviço agora usa a Factory internamente
builder.Services.AddScoped<AgendamentoService>();

// 6. Configurar componentes Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// --- Inicialização de Dados (Seed) ---
using (var scope = app.Services.CreateScope())
{
    // O Seed precisa de um DbContext normal, que a Factory também disponibiliza via Scoped
    await DbInitializer.SeedAdminUser(scope.ServiceProvider);
}

// 7. Pipeline de requisições HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Ordem correta dos middlewares de segurança
app.UseAntiforgery(); 

app.UseAuthentication(); 
app.UseAuthorization();

// --- Endpoints de Autenticação (Necessários para Blazor Server) ---

// Endpoint de Login
app.MapPost("Account/Login", async (
    [FromForm] string UserName, 
    [FromForm] string Password, 
    SignInManager<IdentityUser> signInManager) =>
{
    var result = await signInManager.PasswordSignInAsync(UserName, Password, isPersistent: true, lockoutOnFailure: false);
    
    if (result.Succeeded)
    {
        // Redireciona para o dashboard administrativo após login
        return Results.Redirect("/");
    }
    
    return Results.Redirect("/login?error=1");
})
.DisableAntiforgery();

// Endpoint de Logout
app.MapPost("Account/Logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
})
.DisableAntiforgery();

// 8. Mapeamento de Hubs e Componentes
app.MapHub<AgendamentoHub>("/agendamentoHub");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();