using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SalaReunioes.Web.Components;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Services;
using SalaReunioes.Web.Infrastructure.Hubs; // Namespace para o Hub de SignalR

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração do Banco de Dados (PostgreSQL)
// Recupera a string de conexão definida no appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Registrar o MudBlazor e SignalR
builder.Services.AddMudServices();
builder.Services.AddSignalR(); // Adiciona o serviço para atualizações em tempo real

// 3. Registrar os Serviços de Negócio
builder.Services.AddScoped<AgendamentoService>();

// 4. Configurar componentes Blazor e Interatividade Server-side
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configuração do pipeline de requisições HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Suporte para arquivos na wwwroot (como a logo rioave.png)
app.UseAntiforgery();

// 5. Mapear o Hub do SignalR
// Define o endpoint para a comunicação instantânea entre os clientes
app.MapHub<AgendamentoHub>("/agendamentoHub");

// 6. Configurar o Render Mode do Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();