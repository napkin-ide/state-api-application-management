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
using LCU.Personas.Client.Enterprises;

namespace LCU.State.API.NapkinIDE.ApplicationManagement.DataApps
{
    [Serializable]
    [DataContract]
    public class UploadZipsRequest
    {
        [DataMember]
        public virtual List<ZipAppOption> ZipApps { get; set; }
    }

    public class UploadZips
    {
        protected ApplicationManagerClient appMgr;

        protected EnterpriseManagerClient entMgr;

        public UploadZips(EnterpriseManagerClient entMgr, ApplicationManagerClient appMgr)
        {
            this.appMgr = appMgr;
            
            this.entMgr = entMgr;
        }

        [FunctionName("UploadZips")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = ApplicationManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var actionReq = requestBody?.FromJSON<MetadataModel>();

            req.Body.Seek(0, SeekOrigin.Begin);

            requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            actionReq = requestBody?.FromJSON<MetadataModel>();

            req.Body.Seek(0, SeekOrigin.Begin);

            await stateBlob.WithStateHarness<DataAppsManagementState, UploadZipsRequest, DataAppsManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData, actReq) =>
            {
                harness.State.ZipLoading = true;

                return Status.Success;
            });

            req.Body.Seek(0, SeekOrigin.Begin);

            return await stateBlob.WithStateHarness<DataAppsManagementState, UploadZipsRequest, DataAppsManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData, actReq) =>
            {
                var stateDetails = StateUtils.LoadStateDetails(req);

                log.LogInformation($"Uploading Zips: {reqData.ZipApps.Count}");

                await harness.UploadZips(appMgr, entMgr, stateDetails.EnterpriseLookup, reqData.ZipApps);

                return Status.Success;
            });
        }
    }
}
