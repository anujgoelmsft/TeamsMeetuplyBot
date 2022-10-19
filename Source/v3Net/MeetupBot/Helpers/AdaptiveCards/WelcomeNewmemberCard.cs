namespace MeetupBot.Helpers.AdaptiveCards
{
    using Microsoft.Azure;
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Hosting;

    public static class WelcomeNewMemberCard
    {
        public static string GetCard(string teamName, string personFirstName)
        {
            var welcomeMemberCardFileName = CloudConfigurationManager.GetSetting("CardFilename_WelcomeNewMember") ?? "WelcomeNewMemberCard.json";

            var variablesToValues = new Dictionary<string, string>()
            {
                { "team", teamName },
                { "personFirstName", personFirstName }
            };

            var cardJsonFilePath = HostingEnvironment.MapPath($"~/Helpers/AdaptiveCards/{welcomeMemberCardFileName}");
            var cardTemplate = File.ReadAllText(cardJsonFilePath);

            var cardBody = cardTemplate;

            foreach (var kvp in variablesToValues)
            {
                cardBody = cardBody.Replace($"%{kvp.Key}%", kvp.Value);
            }

            return cardBody;
        }
    }
}