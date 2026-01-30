using Microsoft.AspNetCore.SignalR;

namespace SalaReunioes.Web.Infrastructure.Hubs;

// O Hub serve como o "roteador" das mensagens em tempo real
public class AgendamentoHub : Hub
{
    public async Task NotificarAtualizacao()
    {
        await Clients.All.SendAsync("ReceberAtualizacao");
    }
}