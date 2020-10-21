using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Fathym;
using Microsoft.Azure.Storage.Blob;
using System.Runtime.Serialization;
using Fathym.API;
using System.Collections.Generic;
using System.Linq;
using LCU.Personas.Client.Applications;
using LCU.StateAPI.Utilities;
using System.Security.Claims;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.Identity;
using LCU.State.API.NapkinIDE.ApplicationManagement.State;

namespace LCU.State.API.NapkinIDE.ApplicationManagement.Host
{
    [Serializable]
    [DataContract]
    public class RefreshRequest : BaseRequest
    { }

    public class Refresh
    {
        protected ApplicationManagerClient appMgr;

        protected EnterpriseManagerClient entMgr;

        protected IdentityManagerClient idMgr;

        public Refresh(ApplicationManagerClient appMgr, EnterpriseManagerClient entMgr, IdentityManagerClient idMgr)
        {
            this.appMgr = appMgr;

            this.entMgr = entMgr;

            this.idMgr = idMgr;
        }

        [FunctionName("Refresh")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = ApplicationManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            if (stateDetails.StateKey == "data-apps")
            {
                log.LogInformation($"Refreshing Data Applications Management State.");

                return await stateBlob.WithStateHarness<DataAppsManagementState, RefreshRequest, DataAppsManagementStateHarness>(req, signalRMessages, log,
                    async (harness, refreshReq, actReq) =>
                {
                    await harness.Refresh(appMgr, entMgr, idMgr, stateDetails.EnterpriseLookup);

                    return Status.Success;
                });
            }
            else
            {
                log.LogInformation($"Refreshing Application Management State.");

                return await stateBlob.WithStateHarness<ApplicationManagementState, RefreshRequest, ApplicationManagementStateHarness>(req, signalRMessages, log,
                    async (harness, refreshReq, actReq) =>
                {
                    await harness.Ensure(appMgr, stateDetails.EnterpriseLookup);

                    await harness.LoadAccessRightOptions(idMgr, stateDetails.EnterpriseLookup);

                    await harness.LoadApplications(appMgr, stateDetails.EnterpriseLookup);

                    await harness.LoadDefaultApps(appMgr, stateDetails.EnterpriseLookup);

                    return Status.Success;
                });
            }
        }
    }
}
