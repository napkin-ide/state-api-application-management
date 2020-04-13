using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using LCU.Graphs.Registry.Enterprises.Apps;
using LCU.Personas.Client.Applications;
using Fathym;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.WindowsAzure.Storage.Blob;
using LCU.StateAPI.Utilities;

namespace LCU.State.API.NapkinIDE.ApplicationManagement
{
    [Serializable]
    [DataContract]
    public class SetActiveDAFAPIAppRequest
    {
        [DataMember]
        public virtual Guid DAFAPIAppID { get; set; }
    }

    public class SetActiveDAFAPIApp
    {
        protected ApplicationManagerClient appMgr;

        public SetActiveDAFAPIApp(ApplicationManagerClient appMgr)
        {
            this.appMgr = appMgr;
        }

        [FunctionName("SetActiveDAFAPIApp")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = ApplicationManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<ApplicationManagementState, SetActiveDAFAPIAppRequest, ApplicationManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData, actReq) =>
            {
                var stateDetails = StateUtils.LoadStateDetails(req);

                log.LogInformation($"Setting Active DAF API App: {reqData.DAFAPIAppID}");

                await harness.SetActiveDAFAPIApp(reqData.DAFAPIAppID);

                return Status.Success;
            });
        }
    }
}
