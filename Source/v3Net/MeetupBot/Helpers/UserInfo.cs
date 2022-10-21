namespace MeetupBot.Helpers
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using System.Collections.Generic;

    public class UserInfo
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty("tenantId")]
        public string TenantId { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("optedIn")]
        public bool OptedIn { get; set; }

        [JsonProperty("hasBeenWelcomed")]
        public bool HasBeenWelcomed { get; set; } = false;

        [JsonProperty("recentPairups")]
        public List<string> RecentPairUps { get; set; }

        [JsonProperty("userFullName")]
        public string UserFullName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }
    }
}