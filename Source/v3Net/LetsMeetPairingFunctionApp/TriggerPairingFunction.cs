using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace LetsMeetPairingFunctionApp
{
    public class TriggerPairingFunction
    {
        const string MeetupBotUrl = "https://meetupbotappservice.azurewebsites.net/api/processnow";

        // HttpClient is intended to be instantiated once per application, rather than per-use.
        static readonly HttpClient client = new HttpClient();
        private ILogger logger;

        // First Monday of every month at 1700 hours UTC - "0 0 17 1-7 * MON"
        // Every minute - "0 * * * * *"
        [FunctionName("TriggerPairingFunction")]
        public async Task RunAsync([TimerTrigger("0 0 17 1-7 * MON")] TimerInfo myTimer, ILogger log)
        {
            logger = log;
            logger.LogInformation($"Lets Meet pairing function App started at: {DateTime.UtcNow} UTC");

            logger.LogInformation($"Get All registered teams");
            var teams = await GetAllTeamsAsync();

            logger.LogInformation($"Got {teams.Count} teams");

            await TriggerPairingAsync(teams);
        }

        private async Task TriggerPairingAsync(List<TeamInfo> teams)
        {
            foreach (var team in teams)
            {
                logger.LogInformation($"Sending request to pair team: {team}");
                try
                {
                    Uri uri = new Uri($"{MeetupBotUrl}/{team.Id}");
                    HttpResponseMessage response = await client.PostAsync(uri, null);
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError($"Exception pairing team {team.Teamname}. Details: {ex}");
                    throw;
                }

                // Wait for 10 minutes to pair the next team.
                // This allows us to use a cheaper, single instance of the App Service performing the pairing.
                // TODO: query backend to see when current pairing is complete instead of waiting for fixed time.
                // await Task.Delay(TimeSpan.FromMinutes(10));
            }
        }

        private async Task<List<TeamInfo>> GetAllTeamsAsync()
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync($"{MeetupBotUrl}");
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsAsync<List<TeamInfo>>();
            }
            catch (HttpRequestException ex)
            {
                logger.LogError($"Exception retrieving all teams. Details: {ex}");
                throw;
            }
        }
    }
}
