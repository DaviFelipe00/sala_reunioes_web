using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SalaReunioes.Web.Infrastructure.Data;
using SalaReunioes.Web.Infrastructure.Services;
using SalaReunioes.Web.Components; // ADICIONE ESTA LINHA

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar o Banco de Dados (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// 2. Registar o MudBlazor e Serviços de Negócio
builder.Services.AddMudServices();
builder.Services.AddScoped<AgendamentoService>();

// 3. Configurar Blazor
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

// O erro CS0246 deve desaparecer agora
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();