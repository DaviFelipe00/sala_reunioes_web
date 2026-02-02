using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;
using SalaReunioes.Web.Components;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Services;
using SalaReunioes.Web.Infrastructure.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 1. Banco de Dados
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Identity Core (Configuração leve)
builder.Services.AddIdentityCore<IdentityUser>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager();

// 3. Autenticação e Estado do Blazor
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState(); 

// 4. Interface e Real-time
builder.Services.AddMudServices();
builder.Services.AddSignalR();

// 5. Serviços
builder.Services.AddScoped<AgendamentoService>();

// 6. Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Executa o Seed do Admin ao iniciar
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.SeedAdminUser(scope.ServiceProvider);
}

// Configuração do pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery(); // Importante para formulários Blazor

app.UseAuthentication(); 
app.UseAuthorization();

app.MapHub<AgendamentoHub>("/agendamentoHub");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();