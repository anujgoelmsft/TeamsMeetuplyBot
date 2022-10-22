namespace MeetupBot.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Bot.Connector.Teams.Models;
    using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;
    using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

    public static class MeetupBotDataProvider
    {
        private static DocumentClient documentClient;
        private static bool isTesting;

        public static async Task InitDatabaseAsync()
        {
            if (documentClient == null)
            {
                var endpointUrl = CloudConfigurationManager.GetSetting("CosmosDBEndpointUrl");
                var secretName = CloudConfigurationManager.GetSetting("CosmosDBKeySecretName");
                var primaryKey = await SecretsHelper.GetSecretAsync(secretName).ConfigureAwait(false);

                documentClient = new DocumentClient(new Uri(endpointUrl), primaryKey);

                isTesting = Boolean.Parse(CloudConfigurationManager.GetSetting("Testing"));
            }
        }

        public static async Task<TeamInstallInfo> SaveTeamStatusAsync(TeamInstallInfo team, TeamUpdateType status)
        {
            System.Diagnostics.Trace.TraceInformation($"Update info for team: [{team}] in DB");

            if (isTesting)
            {
                System.Diagnostics.Trace.TraceInformation($"Skip updating DB in testing mode");
                return team;
            }

            await InitDatabaseAsync().ConfigureAwait(false);

            var databaseName = CloudConfigurationManager.GetSetting("CosmosDBDatabaseName");
            var collectionName = CloudConfigurationManager.GetSetting("CosmosCollectionTeams");

            switch (status)
            {
                case TeamUpdateType.Add:
                    {
                        var response = await documentClient.UpsertDocumentAsync(
                                UriFactory.CreateDocumentCollectionUri(databaseName, collectionName),
                                team);
                        System.Diagnostics.Trace.TraceInformation($"Updated info: [{response.Resource}]");
                        break;
                    }

                case TeamUpdateType.Remove:
                    {
                        // query first
                        // Set some common query options
                        FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };

                        var lookupQuery = documentClient.CreateDocumentQuery<TeamInstallInfo>(
                             UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), queryOptions)
                             .Where(t => t.TeamId == team.TeamId);

                        var match = lookupQuery.ToList();

                        if (match.Count > 0)
                        {
                            System.Diagnostics.Trace.TraceInformation($"Found the team to be deleted. Deleting from DB now.");
                            var options = new RequestOptions { PartitionKey = new PartitionKey(team.TeamId) };
                            var response = await documentClient.DeleteDocumentAsync(match.First().SelfLink, options).ConfigureAwait(false);
                            System.Diagnostics.Trace.TraceInformation($"Deleted team [{team.Teamname}] from DB");
                        }
                        else
                        {
                            System.Diagnostics.Trace.TraceInformation($"Did not find team to delete. Skip");
                        }

                        break;
                    }

                case TeamUpdateType.PairingInfo:
                    {
                        var response = await documentClient.UpsertDocumentAsync(
                           UriFactory.CreateDocumentCollectionUri(databaseName, collectionName),
                           team);

                        System.Diagnostics.Trace.TraceInformation($"Updated team info: [{response.Resource}].");

                        break;
                    }

                default:
                    break;
            }

            return team;
        }

        public static async Task<TeamInstallInfo> GetTeamInfoAsync(string teamId)
        {
            System.Diagnostics.Trace.TraceInformation($"Retrieving info for Team: [{teamId}] from DB.");
            await InitDatabaseAsync().ConfigureAwait(false);

            var databaseName = CloudConfigurationManager.GetSetting("CosmosDBDatabaseName");
            var collectionName = CloudConfigurationManager.GetSetting("CosmosCollectionTeams");

            // Set some common query options
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };

            // Find matching activities
            var lookupQuery = documentClient.CreateDocumentQuery<TeamInstallInfo>(
                UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), queryOptions)
                 .Where(t => t.Id == teamId);

            var match = lookupQuery.ToList();
            System.Diagnostics.Trace.TraceInformation($"Found [{match.Count}] matches");
            return match.FirstOrDefault();
        }

        public static async Task<List<TeamInstallInfo>> GetAllTeamsInfoAsync()
        {
            await InitDatabaseAsync().ConfigureAwait(false);

            var databaseName = CloudConfigurationManager.GetSetting("CosmosDBDatabaseName");
            var collectionName = CloudConfigurationManager.GetSetting("CosmosCollectionTeams");

            // Set some common query options
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };

            // Find matching activities
            var lookupQuery = documentClient.CreateDocumentQuery<TeamInstallInfo>(
                UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), queryOptions);

            var match = lookupQuery.ToList();

            return match;
        }

        public static async Task<UserInfo> GetUserInfoAsync(string tenantId, string userId)
        {
            await InitDatabaseAsync().ConfigureAwait(false);

            var databaseName = CloudConfigurationManager.GetSetting("CosmosDBDatabaseName");
            var collectionName = CloudConfigurationManager.GetSetting("CosmosCollectionUsers");

            // Set some common query options
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };

            // Find matching activities
            var lookupQuery = documentClient.CreateDocumentQuery<UserInfo>(
                UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), queryOptions)
                .Where(f => f.TenantId == tenantId && f.UserId == userId);

            var match = lookupQuery.ToList();
            return match.FirstOrDefault();
        }

        public static async Task<Dictionary<string, UserInfo>> GetUserOptInStatusesAsync(string tenantId)
        {
            await InitDatabaseAsync().ConfigureAwait(false);

            var databaseName = CloudConfigurationManager.GetSetting("CosmosDBDatabaseName");
            var collectionName = CloudConfigurationManager.GetSetting("CosmosCollectionUsers");

            // Set some common query options
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };

            // Find matching activities
            var lookupQuery = documentClient.CreateDocumentQuery<UserInfo>(
                UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), queryOptions)
                .Where(f => f.TenantId == tenantId);

            var result = new Dictionary<string, UserInfo>();
            foreach (var status in lookupQuery)
            {
                result.Add(status.UserId, status);
            }

            return result;
        }

        public static async Task<UserInfo> SetUserAsWelcomed(string tenantId, TeamsChannelAccount teamsChannelAccount)
        {
            var user = await GetExistingOrNewUserInfoAsync(tenantId, teamsChannelAccount);

            user.HasBeenWelcomed = true;

            await InitDatabaseAsync().ConfigureAwait(false);

            return await CreateUpdateUserInfo(user);
        }

        private static async Task<UserInfo> GetExistingOrNewUserInfoAsync(string tenantId, TeamsChannelAccount teamsChannelAccount)
        {
            // get existing document to not override optin or other values
            var user = await GetUserInfoAsync(tenantId, teamsChannelAccount.ObjectId);

            if (user == null)
            {
                user = new UserInfo()
                {
                    TenantId = tenantId,
                    UserId = teamsChannelAccount.ObjectId,
                    Email = teamsChannelAccount.Email,
                    RecentPairUps = new List<string>()
                };
            }

            // supporting legacy records
            if (teamsChannelAccount.Email != null)
            {
                user.Email = teamsChannelAccount.Email;
            }

            if (teamsChannelAccount.Name != null)
            {
                user.UserFullName = teamsChannelAccount.Name;
            }

            return user;
        }

        public static async Task<UserInfo> SetUserOptInStatus(string tenantId, TeamsChannelAccount teamsChannelAccount, bool optedIn)
        {
            var user = await GetExistingOrNewUserInfoAsync(tenantId, teamsChannelAccount);

            user.OptedIn = optedIn;

            await InitDatabaseAsync().ConfigureAwait(false);

            return await CreateUpdateUserInfo(user);
        }

        public static async Task<bool> StorePairup(string tenantId, Dictionary<string, UserInfo> userOptInInfo, TeamsChannelAccount user1, TeamsChannelAccount user2)
        {
            System.Diagnostics.Trace.TraceInformation($"Storing the pair: [{user1.ObjectId}] and [{user2.ObjectId}]");
            await InitDatabaseAsync().ConfigureAwait(false);

            var maxPairUpHistory = Convert.ToInt64(CloudConfigurationManager.GetSetting("MaxPairUpHistory"));

            var user1Info = await GetExistingOrNewUserInfoAsync(tenantId, user1);
            var user2Info = await GetExistingOrNewUserInfoAsync(tenantId, user2);

            user1Info.RecentPairUps.Add(user2Info.UserId);

            if (user1Info.RecentPairUps.Count > maxPairUpHistory)
            {
                user1Info.RecentPairUps.RemoveAt(0);
            }

            user2Info.RecentPairUps.Add(user1Info.UserId);

            if (user2Info.RecentPairUps.Count > maxPairUpHistory)
            {
                user2Info.RecentPairUps.RemoveAt(0);
            }

            System.Diagnostics.Trace.TraceInformation($"Updating the pair: [{user1.ObjectId}] and [{user2.ObjectId}] in DB");

            if (isTesting)
            {
                System.Diagnostics.Trace.TraceInformation($"Skip storing pair to DB in Testing mode");
            }
            else
            {
                await CreateUpdateUserInfo(user1Info).ConfigureAwait(false);
                await CreateUpdateUserInfo(user2Info).ConfigureAwait(false);
            }

            return true;
        }

        private static async Task<UserInfo> CreateUpdateUserInfo(UserInfo userInfo)
        {
            await InitDatabaseAsync().ConfigureAwait(false);

            System.Diagnostics.Trace.TraceInformation($"Updating document for [{userInfo.UserId}] to [{System.Text.Json.JsonSerializer.Serialize(userInfo)}]");

            if (isTesting)
            {
                System.Diagnostics.Trace.TraceInformation($"Skip updating DB in testing mode");
                return userInfo;
            }

            var databaseName = CloudConfigurationManager.GetSetting("CosmosDBDatabaseName");
            var collectionName = CloudConfigurationManager.GetSetting("CosmosCollectionUsers");

            var response = await documentClient.UpsertDocumentAsync(
                UriFactory.CreateDocumentCollectionUri(databaseName, collectionName),
                userInfo);

            System.Diagnostics.Trace.TraceInformation($"Updated User: [{userInfo.UserId}] in DB. Status: {response.StatusCode}.");
            return userInfo;
        }
    }
}