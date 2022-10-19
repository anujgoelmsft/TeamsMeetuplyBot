using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Http;
using global::MeetupBot.Helpers;

namespace MeetupBot.Controllers
{
    public class NotificationsController : ApiController
    {
        // POST api/<welcome-users>/<key>
        [Route("api/welcome-users/{teamId}")]
        public void PostWelcomeUsers([FromUri] string teamId)
        {
            if (string.IsNullOrEmpty(teamId))
            {
                System.Diagnostics.Trace.TraceError($"Received Invalid TeamId. Do not do anything.");
            }
            else
            {
                HostingEnvironment.QueueBackgroundWorkItem(ct => WelcomeUsersAsync(teamId));
                return;
            }
        }

        private static async Task<int> WelcomeUsersAsync(string teamId)
        {
            return await MeetupBot.WelcomeAllUsersAsync(teamId);
        }

    }
}