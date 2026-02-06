using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using MudBlazor.Services;
using SalaReunioes.Web.Components;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Services;
using SalaReunioes.Web.Infrastructure.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. Configuração de Serviços (DI)
// ==========================================

// Configuração do Banco de Dados (COM FACTORY)
// Usar Factory é crucial no Blazor Server para evitar o erro "DbContext already being used"
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configuração do ASP.NET Core Identity
builder.Services.AddIdentityCore<IdentityUser>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// Autenticação e Autorização
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState(); 

// Interface e Real-time (SignalR e MudBlazor)
builder.Services.AddMudServices();
builder.Services.AddSignalR();

// Serviços de Negócio
builder.Services.AddScoped<ConfiguracaoService>();
builder.Services.AddScoped<SalaService>();
builder.Services.AddScoped<AgendamentoService>();
builder.Services.AddScoped<RelatorioService>();

// Componentes Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// ==========================================
// 2. Configuração de Localização (PT-BR)
// ==========================================
var supportedCultures = new[] { new CultureInfo("pt-BR") };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("pt-BR")
    .AddSupportedCultures("pt-BR")
    .AddSupportedUICultures("pt-BR");

app.UseRequestLocalization(localizationOptions);

// ==========================================
// 3. Inicialização de Dados (MIGRATE + SEED)
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("🚀 Inicializando migração do banco de dados...");
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var context = factory.CreateDbContext();

        await context.Database.MigrateAsync();
        logger.LogInformation("✅ Migração concluída com sucesso!");

        logger.LogInformation("🌱 Iniciando Seed de dados...");
        // Certifique-se que sua classe DbInitializer aceita IServiceProvider ou o contexto correto
        await DbInitializer.SeedAdminUser(services);
        logger.LogInformation("✅ Seed concluído.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "🛑 ERRO CRÍTICO: Falha ao migrar ou inicializar o banco de dados.");
    }
}

// ==========================================
// 4. Pipeline de Requisições HTTP
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // O valor default do HSTS é 30 dias. Você pode querer alterar isso para produção.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Ordem crítica: Antiforgery -> AuthN -> AuthZ
app.UseAntiforgery(); 
app.UseAuthentication(); 
app.UseAuthorization();

// ==========================================
// 5. Endpoints
// ==========================================

// Endpoint de Login (Formulário Simples)
app.MapPost("Account/Login", async (
    [FromForm] string UserName, 
    [FromForm] string Password, 
    [FromQuery] string? ReturnUrl,
    SignInManager<IdentityUser> signInManager) =>
{
    var result = await signInManager.PasswordSignInAsync(UserName, Password, isPersistent: true, lockoutOnFailure: false);
    
    if (result.Succeeded)
    {
        return Results.Redirect(ReturnUrl ?? "/");
    }
    
    return Results.Redirect("/login?error=1");
})
.DisableAntiforgery(); // Desativado aqui para facilitar form post simples, mas idealmente deve-se enviar o token

// Endpoint de Logout
app.MapPost("Account/Logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
})
.DisableAntiforgery();

// Hubs e Componentes Blazor
app.MapHub<AgendamentoHub>("/agendamentoHub");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();