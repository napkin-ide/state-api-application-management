using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Fathym;
using LCU.Presentation.State.ReqRes;
using LCU.StateAPI.Utilities;
using LCU.StateAPI;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;
using System.Collections.Generic;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.DevOps;
using LCU.Personas.Enterprises;
using LCU.Personas.Client.Applications;

namespace LCU.State.API.NapkinIDE.ApplicationManagement
{
    public class ApplicationManagementStateHarness : LCUStateHarness<ApplicationManagementState>
    {
        #region Fields 
        #endregion

        #region Properties 
        #endregion

        #region Constructors
        public ApplicationManagementStateHarness(ApplicationManagementState state)
            : base(state ?? new ApplicationManagementState())
        { }
        #endregion

        #region API Methods
        public virtual async Task<Status> BootIaC(DevOpsArchitectClient devOpsArch, string parentEntApiKey, string username)
        {
            // if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            // {
            //     var resp = await devOpsArch.EnsureInfrastructureRepo(State.NewEnterpriseAPIKey, username, State.EnvironmentLookup, devOpsEntApiKey: parentEntApiKey);

            //     return resp.Status;
            // }
            // else
                return Status.Success;
        }
        #endregion
    }
}
