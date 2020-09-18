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
using LCU.Personas.Client.Applications;
using Fathym;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.Storage.Blob;
using LCU.StateAPI.Utilities;
using LCU.State.API.NapkinIDE.ApplicationManagement.State;

namespace LCU.State.API.NapkinIDE.ApplicationManagement.Applications
{
    [Serializable]
    [DataContract]
    public class SetViewTypeRequest
    {
        [DataMember]
        public virtual DAFAppTypes AppType { get; set; }
    }

    public class SetViewType
    {
        [FunctionName("SetViewType")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = ApplicationManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<ApplicationManagementState, SetViewTypeRequest, ApplicationManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData, actReq) =>
            {
                var stateDetails = StateUtils.LoadStateDetails(req);

                log.LogInformation($"Setting View Type: {reqData.AppType}");

                await harness.SetViewType(reqData.AppType);

                return Status.Success;
            });
        }
    }
}
