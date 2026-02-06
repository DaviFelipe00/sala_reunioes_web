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
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. Configuração de Serviços
// ==========================================

// Banco de Dados
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Identity Core (Configuração de Senhas e Usuários)
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

// === AUTENTICAÇÃO (CORRIGIDO) ===
// Guardamos o builder para encadear as configurações corretamente
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

// Adiciona o suporte a Cookies do Identity
authBuilder.AddIdentityCookies();

// Adiciona o suporte à Conta Microsoft
authBuilder.AddMicrosoftAccount(microsoftOptions =>
{
    // Lê as chaves do appsettings.json ou usa valores de placeholder para não quebrar
    microsoftOptions.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"] ?? "ID_PENDENTE";
    microsoftOptions.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"] ?? "SECRET_PENDENTE";
});
// ================================

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState(); 

// MudBlazor e SignalR
builder.Services.AddMudServices();
builder.Services.AddSignalR();

// Seus Serviços de Negócio
builder.Services.AddScoped<ConfiguracaoService>();
builder.Services.AddScoped<SalaService>();
builder.Services.AddScoped<AgendamentoService>();
builder.Services.AddScoped<RelatorioService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// ==========================================
// 2. Configurações de Request (Middleware)
// ==========================================

var supportedCultures = new[] { new CultureInfo("pt-BR") };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("pt-BR")
    .AddSupportedCultures("pt-BR")
    .AddSupportedUICultures("pt-BR"));

// Inicialização e Seed do Banco de Dados
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var context = factory.CreateDbContext();
        await context.Database.MigrateAsync();
        await DbInitializer.SeedAdminUser(services);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro no banco: {ex.Message}");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// 3. ENDPOINTS
// ==========================================

// Endpoint de Login Externo (Microsoft)
// CORREÇÃO CRÍTICA: Adicionado [FromForm] para ler os dados enviados pelo formulário HTML
app.MapPost("/Account/ExternalLogin", (
    [FromForm] string provider, 
    [FromForm] string returnUrl, 
    SignInManager<IdentityUser> signInManager) =>
{
    var redirectUrl = $"/Account/ExternalCallback?returnUrl={returnUrl}";
    var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
    return Results.Challenge(properties, new[] { provider });
});

// Endpoint de Callback (Retorno da Microsoft)
app.MapGet("/Account/ExternalCallback", async (string returnUrl, SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager) =>
{
    var info = await signInManager.GetExternalLoginInfoAsync();
    if (info == null) return Results.Redirect("/?error=external-login-failed");

    // Tenta logar se o usuário já existe e está vinculado
    var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true);
    if (result.Succeeded) return Results.Redirect(returnUrl);

    // Se não existe, cria o usuário
    var email = info.Principal.FindFirstValue(ClaimTypes.Email);
    if (email != null)
    {
        var user = await userManager.FindByEmailAsync(email);
        
        if (user == null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user);
        }

        // Vincula o login externo ao usuário
        await userManager.AddLoginAsync(user, info);
        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.Redirect(returnUrl);
    }

    return Results.Redirect("/?error=create-user-failed");
});

// Endpoint de Login Tradicional (Admin)
app.MapPost("Account/Login", async (
    [FromForm] string UserName, 
    [FromForm] string Password, 
    [FromQuery] string? ReturnUrl,
    SignInManager<IdentityUser> signInManager) =>
{
    var result = await signInManager.PasswordSignInAsync(UserName, Password, isPersistent: true, lockoutOnFailure: false);
    if (result.Succeeded) return Results.Redirect(ReturnUrl ?? "/dashboard");
    return Results.Redirect("/?error=1");
})
.DisableAntiforgery();

// Endpoint de Logout
app.MapPost("Account/Logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
})
.DisableAntiforgery();

app.MapHub<AgendamentoHub>("/agendamentoHub");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();