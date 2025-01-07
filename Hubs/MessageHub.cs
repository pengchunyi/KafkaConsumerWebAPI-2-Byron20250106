using Microsoft.AspNetCore.SignalR;

namespace KafkaConsumerWebAPI.Hubs
{
	public class MessageHub : Hub
	{
		// 用於向所有連接的客戶端發送消息
		public async Task SendMessage(string user, string message)
		{
			await Clients.All.SendAsync("ReceiveMessage", user, message);
		}
	}
}
