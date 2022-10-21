using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using Microsoft.Azure;
using Newtonsoft.Json;

namespace TriggerPairingWebApp.Models
{
	public class TeamsDataProvider
	{
        public static List<TeamInfo> GetAllTeams()
        {
            var baseUri = CloudConfigurationManager.GetSetting("MeetupBotAppUri") ?? "https://meetupbotappservice.azurewebsites.net";

            System.Diagnostics.Trace.TraceInformation($"Creating a request for {baseUri}/api/processnow");

            // get all 
            WebRequest webRequest = WebRequest.Create($"{baseUri}/api/processnow");

            webRequest.Method = "GET";
            var response = webRequest.GetResponse();

            System.Diagnostics.Trace.TraceInformation($"Response: {JsonConvert.SerializeObject(response)}");

            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                string json = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<List<TeamInfo>>(json);
            }
        }
    }
}