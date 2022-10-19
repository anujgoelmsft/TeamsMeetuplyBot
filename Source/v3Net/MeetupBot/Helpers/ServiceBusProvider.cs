namespace MeetupBot.Helpers
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Azure.Messaging.ServiceBus;
	using Microsoft.Bot.Connector.Teams.Models;
	using Microsoft.Azure;
	using System.Diagnostics;
	using Newtonsoft.Json;

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

            Trace.TraceInformation($"Creating sender using topic {TopicName}");
            try
            {

                var sender = serviceBusClient.CreateSender(TopicName);

	            Trace.TraceInformation($"Serializing message for the team {teamName}");

			    var message = new ServiceBusMessage(JsonConvert.SerializeObject(messageBody));

                Trace.TraceInformation($"Sending serialized message {message.Body}");

                await sender.SendMessageAsync(message);
            } catch (Exception e)
			{
				Trace.TraceError($"Error sending message to service bus: {e}");
			}
        }

		internal static ServiceBusProvider GetInstance()
		{
			var serviceBusClient = new ServiceBusClient(CloudConfigurationManager.GetSetting("ServiceBusConnectionString"));

			var topic = CloudConfigurationManager.GetSetting("ServiceBusTopicName");

            Trace.TraceInformation($"Using topic {topic}");

            return new ServiceBusProvider(serviceBusClient)
			{
				TopicName = topic
			};
		}
	}
}