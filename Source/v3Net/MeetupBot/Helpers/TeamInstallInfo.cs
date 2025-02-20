﻿namespace MeetupBot.Helpers
{
	using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    public class TeamInstallInfo : Document
    {
        [JsonProperty("teamId")]
        public string TeamId { get; set; }
        
        [JsonProperty("teamName")]
        public string Teamname { get; set; }

        [JsonProperty("tenantId")]
        public string TenantId { get; set; }

        [JsonProperty("serviceUrl")]
        public string ServiceUrl { get; set; }

        [JsonProperty("pairingStatus")]
        public string PairingStatus { get; set; }

        [JsonProperty("lastPairedAtUTC")]
        public string LastPairedAtUTC { get; set; }

        [JsonProperty("optMode")]
        public string OptMode { get; set; } = OptInMode;

        public const string OptInMode = "optin";

        public const string OptOutMode = "optout";

		public override string ToString()
		{
			return $"Name = {this.Teamname}, TeamId = {this.TeamId}, Id = {this.Id}, PairingStatus = {this.PairingStatus}, LastPairedOn = {this.LastPairedAtUTC}, OptMode = {OptMode}";
		}
	}

	public enum PairingStatus
	{
		New,
        Pairing,
		Paired
	}
}