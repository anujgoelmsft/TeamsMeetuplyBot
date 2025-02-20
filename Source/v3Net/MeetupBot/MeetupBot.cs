﻿namespace MeetupBot
{
    using Helpers;
    using Helpers.AdaptiveCards;
    using Microsoft.Azure;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams;
    using Microsoft.Bot.Connector.Teams.Models;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public static class MeetupBot
    {
        public static async Task<int> MakePairsAndNotifyAsync(string teamId)
        {
            Stopwatch watch = Stopwatch.StartNew();

            string executionId = Guid.NewGuid().ToString();

            // Find the team with this team id.
            //     Get all members in the team
            //     Remove the members who have opted out of pairing
            //     Match each member with someone else
            //     Save this pair
            //     Add the member to DB if not already done
            // Now notify each pair found in 1:1 and ask them to reach out to the other person
            // When contacting the user in 1:1, give them the button to opt-out.

            TeamInstallInfo team = new TeamInstallInfo();

            List<TeamInstallInfo> teams = await GetAllTeamsInfoAsync().ConfigureAwait(false);
            team = teams.FirstOrDefault(t => t.Id == teamId);

            if (team == null)
            {
                Trace.TraceError($"No team found with Id: [{teamId}]. Return.");
                return -1;
            }

            Trace.TraceInformation($"Create Pairs and send notifications for team: [{team.Teamname}]. ExecutionId: {executionId}");

            await SetTeamPairingStatusAsync(team, PairingStatus.Pairing);

            Trace.TraceInformation($"Creating Pairs...");
            var countPairsNotified = 0;
            var maxPairUpsPerTeam = Convert.ToInt32(CloudConfigurationManager.GetSetting("MaxPairUpsPerTeam"));

            try
            {
                var optInStatuses = await MeetupBotDataProvider.GetUserOptInStatusesAsync(team.TenantId);
                Trace.TraceInformation($"Found [{optInStatuses.Count}] users in the DB ");

                // This gets the opted in users from Database plus the new members in the team.
                var optedInUsers = await GetOptedInUsers(team, optInStatuses);

                var pairs = (await MakePairsAsync(team, optedInUsers, optInStatuses)).Take(maxPairUpsPerTeam);

                foreach (var pair in pairs)
                {
                    await NotifyPair(team.ServiceUrl, team.TenantId, team.Teamname, pair, executionId).ConfigureAwait(false);
                    await MeetupBotDataProvider.StorePairup(team.TenantId, optInStatuses, pair.Item1, pair.Item2).ConfigureAwait(false);
                    countPairsNotified++;
                }

                await SetTeamPairingStatusAsync(team, PairingStatus.Paired);
            }
            catch (UnauthorizedAccessException uae)
            {
                Trace.TraceError($"Failed to process a team: {team} due to error {uae}");
            }

            watch.Stop();
            var timeElapsed = Math.Round(watch.Elapsed.TotalSeconds);
            Trace.TraceInformation($"{countPairsNotified} pairs created for team: {team.Teamname} in {timeElapsed} seconds");
            return countPairsNotified;
        }

        public static async Task<int> WelcomeAllUsersAsync(string teamId)
        {
            Stopwatch watch = Stopwatch.StartNew();

            // Find the team with this team id.
            //     Get all members in the team
            //     Remove the members who have opted out of pairing
            //     Match each member with someone else
            //     Save this pair
            //     Add the member to DB if not already done
            // Now notify each pair found in 1:1 and ask them to reach out to the other person
            // When contacting the user in 1:1, give them the button to opt-out.

            TeamInstallInfo team = new TeamInstallInfo();

            List<TeamInstallInfo> teams = await GetAllTeamsInfoAsync().ConfigureAwait(false);
            team = teams.FirstOrDefault(t => t.Id == teamId);

            if (team == null)
            {
                Trace.TraceError($"No team found with Id: [{teamId}]. Return.");
                return -1;
            }

            Trace.TraceInformation($"Welcome all users and send notifications for team: [{team.Teamname}]");

            int usersWelcomed = 0;

            try
            {
                var members = await GetTeamMembers(team.ServiceUrl, team.TeamId, team.TenantId);

                var users = await MeetupBotDataProvider.GetUserOptInStatusesAsync(team.TenantId);

                foreach (var member in members)
                {
                    var isBot = string.IsNullOrEmpty(member.Surname);
                    users.TryGetValue(member.ObjectId, out UserInfo user);

                    Trace.TraceInformation($"User ids: {string.Join(", ", users.Keys)}");

                    if ((user == null || !user.HasBeenWelcomed) && !isBot)
                    {
                        Trace.TraceInformation($"Sending welcome message to [{member.ObjectId} {member.Name}]");
                        usersWelcomed++;
                        await MeetupBot.WelcomeUser(team.ServiceUrl, member, team.TenantId, team.TeamId);
                    } 
                    else
                    {
                        Trace.TraceInformation($"Skipping welcome message to [{member.ObjectId} {member.Name}]");
                    }
                }
            }
            catch (UnauthorizedAccessException uae)
            {
                Trace.TraceError($"Failed to process a team: {team} due to error {uae}");
            }

            watch.Stop();
            var timeElapsed = Math.Round(watch.Elapsed.TotalSeconds);
            Trace.TraceInformation($"{usersWelcomed} welcomed for team: {team.Teamname} in {timeElapsed} seconds");
            return usersWelcomed;
        }

        public static async Task<List<TeamInstallInfo>> GetAllTeamsInfoAsync()
        {
            Trace.TraceInformation($"Get All Teams where the bot is registered.");
            var teams = await MeetupBotDataProvider.GetAllTeamsInfoAsync().ConfigureAwait(false);
            Trace.TraceInformation($"Found {teams.Count} teams");

            return teams;
        }

        public static async Task<TeamInstallInfo> GetTeamInfoAsync(string teamId)
        {
            Trace.TraceInformation($"Get info about team {teamId}.");
            var team = await MeetupBotDataProvider.GetTeamInfoAsync(teamId).ConfigureAwait(false);
            
            return team;
        }

        private static async Task<string> GetTeamNameAsync(string serviceUrl, string teamId)
        {
            MicrosoftAppCredentials.TrustServiceUrl(serviceUrl);

            using (var client = new ConnectorClient(new Uri(serviceUrl)))
            {
                var teamsConnectorClient = client.GetTeamsConnectorClient();
                var teamDetailsResult = await teamsConnectorClient.Teams.FetchTeamDetailsAsync(teamId);
                return teamDetailsResult.Name;
            }
        }

        private static async Task NotifyPair(string serviceUrl, string tenantId, string teamName, Tuple<TeamsChannelAccount, TeamsChannelAccount> pair, string executionId)
        {
            var teamsPerson1 = pair.Item1.AsTeamsChannelAccount();
            var teamsPerson2 = pair.Item2.AsTeamsChannelAccount();

            // Fill in person2's info in the card for person1
            // Also we will nominate person 1 to schedule the meeting. So we create a different card. 
            var cardForPerson1 = PairUpNotificationAdaptiveCard.GetCard(isPerson1: true, teamName, teamsPerson2.Name, teamsPerson2.GivenName, teamsPerson1.GivenName, teamsPerson2.UserPrincipalName);

            // Fill in person1's info in the card for person2
            var cardForPerson2 = PairUpNotificationAdaptiveCard.GetCard(isPerson1: false, teamName, teamsPerson1.Name, teamsPerson1.GivenName, teamsPerson2.GivenName, teamsPerson1.UserPrincipalName);

            string person1Alias = teamsPerson1.Email.Split('@')[0];
            Trace.TraceInformation($"Send notification to user with alias: {person1Alias} for team: {teamName}. ExecutionId: {executionId}");
            await NotifyUser(serviceUrl, cardForPerson1, teamsPerson1, tenantId).ConfigureAwait(false);

            string person2Alias = teamsPerson2.Email.Split('@')[0];
            Trace.TraceInformation($"Send notification to user with alias: {person2Alias} for team: {teamName}. ExecutionId: {executionId}");
            await NotifyUser(serviceUrl, cardForPerson2, teamsPerson2, tenantId).ConfigureAwait(false);

            var serviceBusEnabled = CloudConfigurationManager.GetSetting("ServiceBusEnabled") ?? "false";

            Trace.TraceInformation($"Checking if ServiceBusEnabled: {serviceBusEnabled}");

            if (serviceBusEnabled?.Equals("true") ?? false)
            {
                var serviceBus = ServiceBusProvider.GetInstance();

                await serviceBus.SendPairingMessageAsync(teamName, new List<TeamsChannelAccount>() { teamsPerson1, teamsPerson2 });
            }
        }

        public static async Task WelcomeUser(string serviceUrl, ChannelAccount member, string tenantId, string conversationId)
        {
            Trace.TraceInformation($"Welcome User - Getting team info for id {conversationId}");

            var teamName = await GetTeamNameAsync(serviceUrl, conversationId);

            Trace.TraceInformation($"Welcome User - Team found name: {teamName}, getting team members.");

            var allMembers = await GetTeamMembers(serviceUrl, conversationId, tenantId);

            Trace.TraceInformation($"Welcome User - Team members retrieved {allMembers.Length} found.");

            Trace.TraceInformation($"Welcome User - Constructing welcome card for user id  {member?.Id} {member?.Name} and team {teamName}");

            try
            {
                var welcomeMessageCard = WelcomeNewMemberCard.GetCard(teamName, member?.Name);

                Trace.TraceInformation($"Notify User: [{member?.Name}] about addition to the team: [{teamName}]");
                await NotifyUser(serviceUrl, welcomeMessageCard, member, tenantId);

                Trace.TraceInformation($"Marking user as welcomed [{member?.AsTeamsChannelAccount().ObjectId}]");
                await MeetupBotDataProvider.SetUserAsWelcomed(tenantId, member?.AsTeamsChannelAccount());
            } catch(Exception e)
            {
                Trace.TraceError(e.ToString());
                throw;
            }
        }

        private static async Task NotifyUser(string serviceUrl, string cardToSend, ChannelAccount user, string tenantId)
        {
            var me = new ChannelAccount()
            {
                Id = CloudConfigurationManager.GetSetting("MicrosoftAppId"),
                Name = "MeetupBot"
            };

            MicrosoftAppCredentials.TrustServiceUrl(serviceUrl);

            // Create 1:1 with user
            using (var connectorClient = new ConnectorClient(new Uri(serviceUrl)))
            {
                // ensure conversation exists
                var response = connectorClient.Conversations.CreateOrGetDirectConversation(me, user, tenantId);

                // construct the activity we want to post
                var activity = new Microsoft.Bot.Connector.Activity()
                {
                    Type = ActivityTypes.Message,
                    Conversation = new ConversationAccount()
                    {
                        Id = response.Id,
                    },
                    Attachments = new List<Attachment>()
                    {
                        new Attachment()
                        {
                            ContentType = "application/vnd.microsoft.card.adaptive",
                            Content = JsonConvert.DeserializeObject(cardToSend),
                        }
                    }
                };

                var isTesting = Boolean.Parse(CloudConfigurationManager.GetSetting("Testing"));
                
                if (isTesting)
                {
                    Trace.TraceInformation($"Skip sending notification to [{user.Name}] in testing mode.");
                }
                else
                {
                    // shoot the activity over
                    // added try catch because if user has set "Block conversations with bots"
                    try
                    {
                        await connectorClient.Conversations.SendToConversationAsync(activity, response.Id).ConfigureAwait(false);
                        Trace.TraceInformation($"Notification sent to [{user.Name}]");
                    }
                    catch (UnauthorizedAccessException uae)
                    {
                        Trace.TraceError($"Failed to notify user due to error {uae.ToString()}");
                    }
                }
            }
        }

        public static async Task SaveTeam(TeamInstallInfo teamInfo, TeamUpdateType status)
        {
            await MeetupBotDataProvider.SaveTeamStatusAsync(teamInfo, status);
        }

        public static async Task OptOutUser(string tenantId, TeamsChannelAccount user)
        {
            await MeetupBotDataProvider.SetUserOptInStatus(tenantId, user, false);
        }

        public static async Task OptInUser(string tenantId, TeamsChannelAccount user)
        {
            await MeetupBotDataProvider.SetUserOptInStatus(tenantId, user, true);
        }

        private static async Task<TeamsChannelAccount[]> GetTeamMembers(string serviceUrl, string teamId, string tenantId)
        {
            
            MicrosoftAppCredentials.TrustServiceUrl(serviceUrl);
            
            using (var connector = new ConnectorClient(new Uri(serviceUrl)))
            {
                try
                {
                    // Pull the roster of specified team and then remove everyone who has opted out explicitly
                    //var members = await connector.Conversations.GetTeamsConversationMembersAsync(teamId, tenantId);
                    var members = await connector.Conversations.GetConversationMembersAsync(teamId);
                    
                    return members.Select(m => m.AsTeamsChannelAccount()).ToArray();
                } catch (Exception e)
                {
                    Trace.TraceError(e.ToString());
                    return null;
                }
            }
        }

        private static async Task<List<TeamsChannelAccount>> GetOptedInUsers(TeamInstallInfo teamInfo, Dictionary<string, UserInfo> optInInfo)
        {
            var optedInUsers = new List<TeamsChannelAccount>();

            var members = await GetTeamMembers(teamInfo.ServiceUrl, teamInfo.TeamId, teamInfo.TenantId);

            foreach (var member in members)
            {
                var isBot = string.IsNullOrEmpty(member.Surname);
                optInInfo.TryGetValue(member.ObjectId, out UserInfo optInStatus);

                if (!isBot && 
                    (optInStatus?.OptedIn ?? teamInfo.OptMode.Equals(TeamInstallInfo.OptInMode)))
                {
                    optedInUsers.Add(member);
                }
            }

            Trace.TraceInformation($"Found [{optedInUsers.Count}] opted in users in the Team: [{teamInfo.Teamname}]");
            return optedInUsers;
        }

        private static async Task<List<Tuple<TeamsChannelAccount, TeamsChannelAccount>>> MakePairsAsync(TeamInstallInfo team, List<TeamsChannelAccount> incomingUsers, Dictionary<string, UserInfo> optInInfo)
        {
            Trace.TraceInformation($"Making pairs for [{incomingUsers.Count}] users.");

            // Update Team Status to Pairing
            await SetTeamPairingStatusAsync(team, PairingStatus.Pairing);

            int attempts = 0;
            int maxAttempts = 5;
            List<Tuple<TeamsChannelAccount, TeamsChannelAccount>> pairs;

            // For Pairing, we only look ahead on the users list. So it is possible we have already met all ahead of us but none behind us.
            // Another Attemt with shuffling the users solves that problem.
            while (attempts < maxAttempts)
            {
                attempts++;
                Trace.TraceInformation($"Attempt [{attempts}] to make pairs. Max Attempts: [{maxAttempts}]");

                // Deep copy the users so we can mess with the list
                var users = JsonConvert.DeserializeObject<List<TeamsChannelAccount>>(JsonConvert.SerializeObject(incomingUsers));
                pairs = MakePairsInternal(users, optInInfo);

                if (pairs != null)
                {
                    return pairs;
                }

                Trace.TraceWarning($"Attempt [{attempts}] failed. Retrying until Max attempts = [{maxAttempts}]");
            }

            pairs = MakePairsInternal(incomingUsers, optInInfo, canTryAgain: false);

            return pairs;
        }

        private static async Task SetTeamPairingStatusAsync(TeamInstallInfo team, PairingStatus status)
        {
            // Create a new team object as CosmosDB does not like updating the existing one.
            var updatedTeam = new TeamInstallInfo
            {
                Id = team.Id,
                TeamId = team.TeamId,
                Teamname = team.Teamname,
                TenantId = team.TenantId,
                ServiceUrl = team.ServiceUrl,
                PairingStatus = status.ToString(),
                LastPairedAtUTC = status == PairingStatus.Paired ? DateTime.Now.ToString() : team.LastPairedAtUTC
            };

            _ = await MeetupBotDataProvider.SaveTeamStatusAsync(updatedTeam, TeamUpdateType.PairingInfo);
        }

        private static List<Tuple<TeamsChannelAccount, TeamsChannelAccount>> MakePairsInternal(List<TeamsChannelAccount> users, Dictionary<string, UserInfo> optInInfo, bool canTryAgain = true)
        {
            var pairs = new List<Tuple<TeamsChannelAccount, TeamsChannelAccount>>();

            Randomize<TeamsChannelAccount>(users);
            int repeatedMatch = 0;

            var isTesting = Boolean.Parse(CloudConfigurationManager.GetSetting("Testing"));

            while (users.Count > 1)
            {
                TeamsChannelAccount user1 = users[0];
                optInInfo.TryGetValue(user1.ObjectId, out var user1OptIn);
                TeamsChannelAccount user2 = null;
                int pointer = 1;

                // Find the first person they haven't been paired with recently.
                while (user2 == null && pointer < users.Count)
                {
                    if (user1OptIn?.RecentPairUps == null || !user1OptIn.RecentPairUps.Contains(users[pointer].ObjectId) || isTesting)
                    {
                        // Either User1 has not paired with anyone or not with User at pointer index or we are in testing mode.
                        user2 = users[pointer];
                    }
                    else
                    {
                        // this keeps track of how may repeated meetings we avoided.
                        repeatedMatch++;
                    }

                    pointer++;
                }

                // Pair these two and remove them from the people to pair.
                // If we didn't find someone to pair user1 with, give up on them for this week.
                if (user2 != null)
                {
                    Trace.TraceInformation($"Paired [{user1.Name}] with [{user2.Name}]");
                    pairs.Add(new Tuple<TeamsChannelAccount, TeamsChannelAccount>(user1, user2));
                    users.Remove(user2);
                }
                else
                {
                    Trace.TraceInformation($"Did not find anyone to pair with User1: [{user1.Name}]. Return");

                    if (canTryAgain)
                    {
                        // try to create pairs again
                        return null;
                    }
                }

                users.Remove(user1);
            }

            Trace.TraceInformation($"Total Pairs created: [{pairs.Count}]. Repititions avoided = [{repeatedMatch}].");
            return pairs;
        }

        public static void Randomize<T>(IList<T> items)
        {
            Random rand = new Random(Guid.NewGuid().GetHashCode());

            // For each spot in the array, pick
            // a random item to swap into that spot.
            for (int i = 0; i < items.Count - 1; i++)
            {
                int j = rand.Next(i, items.Count);
                T temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }
    }

}