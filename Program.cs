using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Globalization;
using System.Security.Claims;
using MudBlazor.Services;
using SalaReunioes.Web.Components;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Services;
using SalaReunioes.Web.Infrastructure.Hubs;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. Configuração de Serviços (DI)
// ==========================================

// Configuração do Banco de Dados (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configuração do ASP.NET Core Identity (Usuários e Senhas)
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

// --- AUTENTICAÇÃO (Cookies + Microsoft) ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies() // Gerencia os cookies do Identity
.ApplicationCookie!.Configure(opt => opt.LoginPath = "/login"); // Redireciona não logados

// Configuração do Login Microsoft com Restrição de Domínio
builder.Services.AddAuthentication()
    .AddMicrosoftAccount(microsoftOptions =>
    {
        // Pega do appsettings.json ou usa valores de desenvolvimento/segredo
        microsoftOptions.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"] ?? throw new Exception("ClientId Microsoft não configurado!");
        microsoftOptions.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"] ?? throw new Exception("ClientSecret Microsoft não configurado!");

        // EVENTO CRÍTICO: Validação do Domínio Rio Ave
        microsoftOptions.Events.OnCreatingTicket = context =>
        {
            var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(email) || !email.EndsWith("@rioave.com.br", StringComparison.OrdinalIgnoreCase))
            {
                context.Fail("Acesso permitido apenas para contas @rioave.com.br");
            }

            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState(); 

// Interface e Real-time
builder.Services.AddMudServices();
builder.Services.AddSignalR();

// Serviços de Negócio
builder.Services.AddScoped<ConfiguracaoService>();
builder.Services.AddScoped<SalaService>();
builder.Services.AddScoped<AgendamentoService>();
builder.Services.AddScoped<RelatorioService>();

// Validadores (Opcional - se você seguiu a dica do FluentValidation)
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

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

        // Aplica migrações pendentes automaticamente
        await context.Database.MigrateAsync();
        logger.LogInformation("✅ Migração concluída.");

        logger.LogInformation("🌱 Iniciando Seed de dados...");
        await DbInitializer.SeedAdminUser(services);
        logger.LogInformation("✅ Seed concluído.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "🛑 ERRO CRÍTICO: Falha ao inicializar o banco de dados.");
    }
}

// ==========================================
// 4. Pipeline HTTP
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Ordem Obrigatória: Antiforgery -> AuthN -> AuthZ
app.UseAntiforgery(); 
app.UseAuthentication(); 
app.UseAuthorization();

// ==========================================
// 5. Endpoints
// ==========================================

// Login Padrão (Admin/Senha)
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
.DisableAntiforgery(); 

// --- NOVO: Login Microsoft (Endpoint de Desafio) ---
app.MapGet("Account/LoginMicrosoft", (string? returnUrl) =>
{
    var properties = new AuthenticationProperties
    {
        RedirectUri = returnUrl ?? "/"
    };
    
    // Inicia o fluxo OAuth com a Microsoft
    return Results.Challenge(properties, new[] { "Microsoft" });
});

// Logout
app.MapPost("Account/Logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
})
.DisableAntiforgery();

// Mapeamento Blazor e Hubs
app.MapHub<AgendamentoHub>("/agendamentoHub");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();