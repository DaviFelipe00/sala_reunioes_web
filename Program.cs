using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity; // Adicionado para Identity
using MudBlazor.Services;
using SalaReunioes.Web.Components;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Services;
using SalaReunioes.Web.Infrastructure.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração do Banco de Dados (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Configuração do ASP.NET Core Identity (Sistema de Login)
// Configuramos políticas de senha simplificadas para ambiente interno
builder.Services.AddIdentityCore<IdentityUser>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager();

// 3. Configurar Autenticação e Autorização
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState(); // OBRIGATÓRIO para gerenciar estado de login no Blazor

// 4. Registrar o MudBlazor e SignalR
builder.Services.AddMudServices();
builder.Services.AddSignalR(); //

// 5. Registrar os Serviços de Negócio
builder.Services.AddScoped<AgendamentoService>(); //

// 6. Configurar componentes Blazor e Interatividade Server-side
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(); //

var app = builder.Build();

// Configuração do pipeline de requisições HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true); //
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); //
app.UseAntiforgery(); //

// OBRIGATÓRIO: Adicionar os middlewares de segurança na ordem correta
app.UseAuthentication(); 
app.UseAuthorization();

// 7. Mapear o Hub do SignalR
app.MapHub<AgendamentoHub>("/agendamentoHub"); //

// 8. Configurar o Render Mode do Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode(); //

app.Run();