namespace MeetupBot.Helpers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using Azure.Messaging.ServiceBus;
	using Microsoft.Azure;
	using Microsoft.Bot.Connector.Teams.Models;
	using System.Text.Json;

	public class ServiceBusProvider
	{
		private ServiceBusClient serviceBusClient;

		private string TopicName { get; set; }
		
		public ServiceBusProvider(ServiceBusClient serviceBusClient)
		{
			this.serviceBusClient = serviceBusClient;
		}

		public async Task SendPairingMessageAsync(string teamName, IList<TeamsChannelAccount> users)
		{
			var messageBody = new
			{
				Team = teamName,
				Users = users
			};

            var sender = serviceBusClient.CreateSender(TopicName);

            var message = new ServiceBusMessage(JsonSerializer.Serialize(messageBody));

			await sender.SendMessageAsync(message);
        }
	}
}