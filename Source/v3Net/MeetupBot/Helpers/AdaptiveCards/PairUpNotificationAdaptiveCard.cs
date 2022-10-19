namespace MeetupBot.Helpers.AdaptiveCards
{
    using Microsoft.Azure;
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Hosting;

    public static class PairUpNotificationAdaptiveCard
    {
        public static string GetCard(bool isPerson1, string teamName, string matchedPersonName, string matchedPersonFirstName, string receiverName, string personUpn)
        {
            var variablesToValues = new Dictionary<string, string>()
            {
                { "team", teamName },
                { "matchedPerson", matchedPersonName },
                { "matchedPersonFirstName", matchedPersonFirstName},
                { "receiverName", receiverName },
                { "personUpn", personUpn }
            };

            var pairUpNotificationPerson1 = CloudConfigurationManager.GetSetting("CardFilename_PairUpPerson1") ?? "PairUpNotificationAdaptiveCardPerson1.json";
            var pairUpNotificationPerson2 = CloudConfigurationManager.GetSetting("CardFilename_PairUpPerson2") ?? "PairUpNotificationAdaptiveCardPerson2.json";

            var card = (isPerson1 == true) ? pairUpNotificationPerson1 : pairUpNotificationPerson2;
            var cardJsonFilePath = HostingEnvironment.MapPath($"~/Helpers/AdaptiveCards/{card}");
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