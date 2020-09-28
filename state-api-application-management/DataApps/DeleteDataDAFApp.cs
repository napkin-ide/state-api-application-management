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
using System.Collections.Generic;

namespace LCU.State.API.NapkinIDE.ApplicationManagement.DataApps
{
    [Serializable]
    [DataContract]
    public class DeleteDataDAFAppRequest
    {
        [DataMember]
        public virtual Guid ApplicationID { get; set; }
        
        [DataMember]
        public virtual List<string> Lookups { get; set; }
    }

    public class DeleteDataDAFApp
    {
        protected ApplicationDeveloperClient appDev;

        protected ApplicationManagerClient appMgr;

        public DeleteDataDAFApp(ApplicationDeveloperClient appDev, ApplicationManagerClient appMgr)
        {
            this.appDev = appDev;

            this.appMgr = appMgr;
        }

        [FunctionName("DeleteDataDAFApp")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = ApplicationManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<DataAppsManagementState, DeleteDataDAFAppRequest, DataAppsManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData, actReq) =>
            {
                var stateDetails = StateUtils.LoadStateDetails(req);

                log.LogInformation($"Deleting Data DAF App: {reqData.ToJSON()}");

                await harness.DeleteDAFApp(appDev, appMgr, stateDetails.EnterpriseLookup, reqData.ApplicationID, reqData.Lookups);

                return Status.Success;
            });
        }
    }
}
