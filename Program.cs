using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
// Importante: No EasyPanel, isso lerá a variável de ambiente 'ConnectionStrings__DefaultConnection'
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
.AddSignInManager();

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
builder.Services.AddScoped<AgendamentoService>();

// Componentes Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// ==========================================
// 2. Inicialização de Dados (MIGRATE + SEED)
// ==========================================
// Esse bloco garante que o banco seja criado automaticamente no EasyPanel
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("🚀 Inicializando migração do banco de dados...");

        // Como usamos Factory, criamos um contexto temporário apenas para a migração
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var context = factory.CreateDbContext();

        // Aplica as migrações pendentes (cria tabelas se não existirem)
        await context.Database.MigrateAsync();
        logger.LogInformation("✅ Migração concluída com sucesso!");

        // Executa o Seed de dados (Admin User)
        logger.LogInformation("🌱 Iniciando Seed de dados...");
        await DbInitializer.SeedAdminUser(services);
        logger.LogInformation("✅ Seed concluído.");
    }
    catch (Exception ex)
    {
        // Esse erro aparecerá em VERMELHO nos logs do EasyPanel
        logger.LogError(ex, "🛑 ERRO CRÍTICO: Falha ao migrar ou inicializar o banco de dados.");
    }
}

// ==========================================
// 3. Pipeline de Requisições HTTP
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // Hsts adiciona segurança estrita de transporte (bom para produção)
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Ordem crítica: Antiforgery -> AuthN -> AuthZ
app.UseAntiforgery(); 
app.UseAuthentication(); 
app.UseAuthorization();

// ==========================================
// 4. Endpoints
// ==========================================

// Endpoint de Login (Formulário tradicional para escrever o Cookie)
app.MapPost("Account/Login", async (
    [FromForm] string UserName, 
    [FromForm] string Password, 
    SignInManager<IdentityUser> signInManager) =>
{
    var result = await signInManager.PasswordSignInAsync(UserName, Password, isPersistent: true, lockoutOnFailure: false);
    
    if (result.Succeeded)
    {
        return Results.Redirect("/");
    }
    
    return Results.Redirect("/login?error=1");
})
.DisableAntiforgery(); // Cuidado em produção (revisar se o form envia o token)

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