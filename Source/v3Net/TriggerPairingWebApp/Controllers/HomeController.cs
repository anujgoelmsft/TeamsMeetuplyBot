using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Microsoft.Azure;
using Newtonsoft.Json;
using TriggerPairingWebApp.Models;

namespace TriggerPairingWebApp.Controllers
{
	public class HomeController : Controller
	{
		// GET: Home
		public ActionResult Index()
		{
			List<TeamInfo> teamsInfoList;

            teamsInfoList = TeamsDataProvider.GetAllTeams();

            System.Diagnostics.Trace.TraceInformation($"{teamsInfoList.Count} teams retrieved.");

            var teamsSelectList = teamsInfoList.Select(t => new SelectListItem
			{
				Text = t.Teamname,
				Value = t.Id
			});

			var viewModel = new TeamViewModel
			{
				AllTeams = new SelectList(teamsSelectList, "Value", "Text"),
				AllTeamsInfo = teamsInfoList.Select(t => new TeamInfo
				{
					Teamname = t.Teamname,
					PairingStatus = t.PairingStatus,
					LastPairedAtUTC = t.LastPairedAtUTC
				})
			};

            System.Diagnostics.Trace.TraceInformation($"Rendering view model");

            return View(viewModel);
		}


        [HttpPost]
		public ActionResult Pair(TeamViewModel model)
		{
			var baseUri = CloudConfigurationManager.GetSetting("MeetupBotAppUri") ?? "https://meetupbotappservice.azurewebsites.net";

            // Send Web Request to trigger pairing
            var selectedTeamId = model.SelectedTeamId;

            if (selectedTeamId == null)
            {
                throw new ArgumentNullException("selectedTeamId");
            }

            System.Diagnostics.Trace.TraceInformation($"Creating a request for {baseUri}/api/processnow/{selectedTeamId}");
            
			WebRequest webRequest = WebRequest.Create($"{baseUri}/api/processnow/{selectedTeamId}");
			webRequest.Method = "POST";
			webRequest.ContentLength = 0;
			webRequest.GetResponse();

			// go back to home page
			return Redirect("~/");
		}

        [HttpPost]
        public ActionResult Welcome(TeamViewModel model)
        {
            var baseUri = CloudConfigurationManager.GetSetting("MeetupBotAppUri") ?? "https://meetupbotappservice.azurewebsites.net";

            // Send Web Request to trigger pairing
            var selectedTeamId = model.SelectedTeamId;

            System.Diagnostics.Trace.TraceInformation($"Creating a request for {baseUri}/api/welcome-users/{selectedTeamId}");

			if (selectedTeamId == null)
			{
				throw new ArgumentNullException("selectedTeamId");
			}

            WebRequest webRequest = WebRequest.Create($"{baseUri}/api/welcome-users/{selectedTeamId}");
            webRequest.Method = "POST";
            webRequest.ContentLength = 0;
            webRequest.GetResponse();

            // go back to home page
            return Redirect("~/");
        }

        public ActionResult About()
		{
			ViewBag.Message = "Your application description page.";

			return View();
		}

		public ActionResult Contact()
		{
			ViewBag.Message = "Your contact page.";

			return View();
		}
	}
}