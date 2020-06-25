using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Runtime.Serialization;
using LCU.Graphs.Registry.Enterprises.Apps;
using Fathym;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.WindowsAzure.Storage.Blob;
using LCU.StateAPI.Utilities;
using LCU.Personas.Client.Applications;
using LCU.State.API.NapkinIDE.ApplicationManagement.State;

namespace LCU.State.API.NapkinIDE.ApplicationManagement.Applications
{
	[Serializable]
	[DataContract]
	public class ToggleAppAsDefaultRequest
	{
		[DataMember]
		public virtual Guid AppID { get; set; }
	}

	public class ToggleAppAsDefault
    {
        protected ApplicationDeveloperClient appDev;

        protected ApplicationManagerClient appMgr;

        public ToggleAppAsDefault(ApplicationManagerClient appMgr, ApplicationDeveloperClient appDev)
        {
            this.appDev = appDev;
			
            this.appMgr = appMgr;
        }

        [FunctionName("ToggleAppAsDefault")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = ApplicationManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<ApplicationManagementState, ToggleAppAsDefaultRequest, ApplicationManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData, actReq) =>
            {
				log.LogInformation($"Toggling Apps As Default: {reqData.AppID}");

                var stateDetails = StateUtils.LoadStateDetails(req);

                await harness.ToggleAppAsDefault(appMgr, appDev, stateDetails.EnterpriseAPIKey, reqData.AppID);

                return Status.Success;
            });
        }
    }
}
