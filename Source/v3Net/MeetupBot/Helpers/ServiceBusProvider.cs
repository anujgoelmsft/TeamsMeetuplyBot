namespace MeetupBot.Helpers
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Azure.Messaging.ServiceBus;
	using Microsoft.Bot.Connector.Teams.Models;
	using System.Text.Json;
	using Microsoft.Azure;

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

		internal static ServiceBusProvider GetInstance()
		{
			var serviceBusClient = new ServiceBusClient(CloudConfigurationManager.GetSetting("ServiceBusConnectionString"));

			return new ServiceBusProvider(serviceBusClient)
			{
				TopicName = CloudConfigurationManager.GetSetting("ServiceBusTopicName")
			};
		}
	}
}