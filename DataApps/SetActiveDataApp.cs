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
using LCU.State.API.NapkinIDE.ApplicationManagement.State;

namespace LCU.State.API.NapkinIDE.ApplicationManagement.DataApps
{
    [Serializable]
    [DataContract]
    public class SetActiveDataAppRequest
    {
        [DataMember]
        public virtual string AppPathGroup { get; set; }
    }

    public class SetActiveDataApp
    {
        protected ApplicationManagerClient appMgr;

        public SetActiveDataApp(ApplicationManagerClient appMgr)
        {
            this.appMgr = appMgr;
        }

        [FunctionName("SetActiveDataApp")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = ApplicationManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<DataAppsManagementState, SetActiveDataAppRequest, DataAppsManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData, actReq) =>
            {
                var stateDetails = StateUtils.LoadStateDetails(req);

                log.LogInformation($"Setting Active App: {reqData.AppPathGroup}");

                await harness.SetActiveApp(appMgr, stateDetails.EnterpriseAPIKey, reqData.AppPathGroup);

                return Status.Success;
            });
        }
    }
}
