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
using Microsoft.Azure.Storage.Blob;
using LCU.StateAPI.Utilities;
using LCU.State.API.NapkinIDE.ApplicationManagement.State;
using LCU.Personas.Client.Security;

namespace LCU.State.API.NapkinIDE.ApplicationManagement.DataApps
{
    [Serializable]
    [DataContract]
    public class SaveGlobalAppSettingsRequest
    {
        [DataMember]
        public virtual GlobalApplicationSettings Settings { get; set; }
    }

    public class SaveGlobalAppSettings
    {
        protected SecurityManagerClient secMgr;

        public SaveGlobalAppSettings(SecurityManagerClient secMgr)
        {
            this.secMgr = secMgr;
        }

        [FunctionName("SaveGlobalAppSettings")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = ApplicationManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<DataAppsManagementState, SaveGlobalAppSettingsRequest, DataAppsManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData, actReq) =>
            {
                var stateDetails = StateUtils.LoadStateDetails(req);

                log.LogInformation($"Saving Global App Settings: {reqData.Settings.ToJSON()}");

                await harness.SaveGlobalAppSettings(secMgr, stateDetails.EnterpriseLookup, reqData.Settings);

                return Status.Success;
            });
        }
    }
}
