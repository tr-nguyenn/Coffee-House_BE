using CoffeeHouse.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace CoffeeHouse.API.Hubs
{
    public class KitchenHubService : IKitchenHubService
    {
        private readonly IHubContext<KitchenHub> _hubContext;

        public KitchenHubService(IHubContext<KitchenHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendRefreshKitchenTicketsMessageAsync()
        {
            await _hubContext.Clients.All.SendAsync("RefreshKitchenTickets");
        }
    }
}
