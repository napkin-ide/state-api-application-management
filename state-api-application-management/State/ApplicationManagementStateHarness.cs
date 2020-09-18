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
using LCU.Graphs.Registry.Enterprises.Apps;
using LCU.Personas.Client.Identity;

namespace LCU.State.API.NapkinIDE.ApplicationManagement.State
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
        public virtual async Task Ensure(ApplicationManagerClient appMgr, string entLookup)
        {
            State.Applications = new List<Application>();

            if (!State.AppType.HasValue)
                State.AppType = DAFAppTypes.View;

            if (State.ActiveApp != null)
                await SetActiveApp(appMgr, entLookup, State.ActiveApp);
        }

        public virtual async Task LoadAccessRightOptions(IdentityManagerClient idMgr, string entLookup)
        {
            var accessRightsResp = await idMgr.ListAccessRights(entLookup);

            State.AccessRightOptions = accessRightsResp?.Model.Select(ar => ar.Lookup).ToList() ?? new List<string>();
        }

        public virtual async Task LoadApplications(ApplicationManagerClient appMgr, string entLookup)
        {
            var apps = await appMgr.ListApplications(entLookup);

            State.Applications = apps.Model.Where(app => app.Container == "lcu-data-apps").ToList();

            State.ActiveApp = State.Applications.FirstOrDefault(app => app.ID == State.ActiveApp?.ID);

            await LoadDAFAppOptions(appMgr, entLookup);
        }

        public virtual async Task LoadAppView(ApplicationManagerClient appMgr, string entLookup)
        {
            if (State.ActiveApp != null)
            {
                var dafApps = await appMgr.ListDAFApplications(entLookup, State.ActiveApp.ID);

                if (dafApps.Status)
                {
                    if (dafApps.Model.Count < 2)
                    {
                        State.ActiveDAFApp = dafApps?.Model?.FirstOrDefault()?.JSONConvert<DAFApplication>();

                        State.ActiveDAFAPIs = null;
                    }
                    else
                    {
                        State.ActiveDAFAPIs = dafApps?.Model?.ToList();

                        State.ActiveDAFApp = null;
                    }
                }
                else
                {
                    State.ActiveDAFApp = null;

                    State.ActiveDAFAPIs = null;
                }

                if (State.ActiveDAFApp != null)
                {
                    if (State.ActiveDAFApp.Details.Metadata.ContainsKey("APIRoot"))
                        await SetViewType(DAFAppTypes.API);
                    else if (State.ActiveDAFApp.Details.Metadata.ContainsKey("Redirect"))
                        await SetViewType(DAFAppTypes.Redirect);
                    else if (State.ActiveDAFApp.Details.Metadata.ContainsKey("BaseHref"))
                        await SetViewType(DAFAppTypes.View);
                    else if (State.ActiveDAFApp.Details.Metadata.ContainsKey("DAFApplicationID"))
                        await SetViewType(DAFAppTypes.DAFApp);
                }
                else if (!State.ActiveDAFAPIs.IsNullOrEmpty())
                    await SetViewType(DAFAppTypes.API);
            }
            else
                State.ActiveDAFApp = null;
        }

        public virtual async Task LoadDAFAppOptions(ApplicationManagerClient appMgr, string entLookup)
        {
            State.DAFAppOptions = new Dictionary<string, string>();

            if (!State.Applications.IsNullOrEmpty())
            {
                await State.Applications.Each(async app =>
                {
                    var dafApps = await appMgr.ListDAFApplications(entLookup, app.ID);

                    dafApps.Model.Each(dafApp => State.DAFAppOptions[dafApp.ID.ToString()] = $"{app.Name} {dafApp.Lookup}");
                });
            }
        }

        public virtual async Task LoadDefaultApps(ApplicationManagerClient appMgr, string entLookup)
        {
            var apps = await appMgr.ListDefaultApplications(entLookup);

            State.DefaultApps = apps.Model;

            var defApps = await appMgr.HasDefaultApplications(entLookup);

            State.DefaultAppsEnabled = defApps.Status;
        }

        public virtual async Task SaveDAFApp(ApplicationDeveloperClient appDev, ApplicationManagerClient appMgr, string entLookup,
            DAFApplication dafApp)
        {
            if (State.ActiveApp != null)
            {
                if (State.AppType != DAFAppTypes.API)
                {
                    if (dafApp.Details.Metadata.ContainsKey("APIRoot"))
                        dafApp.Details.Metadata.Remove("APIRoot");

                    if (dafApp.Details.Metadata.ContainsKey("InboundPath"))
                        dafApp.Details.Metadata.Remove("InboundPath");

                    if (dafApp.Details.Metadata.ContainsKey("Methods"))
                        dafApp.Details.Metadata.Remove("Methods");

                    if (dafApp.Details.Metadata.ContainsKey("Security"))
                        dafApp.Details.Metadata.Remove("Security");
                }

                if (State.AppType != DAFAppTypes.Redirect)
                {
                    if (dafApp.Details.Metadata.ContainsKey("Redirect"))
                        dafApp.Details.Metadata.Remove("Redirect");
                }

                if (State.AppType != DAFAppTypes.View)
                {
                    if (dafApp.Details.Metadata.ContainsKey("BaseHref"))
                        dafApp.Details.Metadata.Remove("BaseHref");

                    if (dafApp.Details.Metadata.ContainsKey("NPMPackage"))
                        dafApp.Details.Metadata.Remove("NPMPackage");

                    if (dafApp.Details.Metadata.ContainsKey("PackageVersion"))
                        dafApp.Details.Metadata.Remove("PackageVersion");
                }

                if (State.AppType != DAFAppTypes.DAFApp)
                {
                    if (dafApp.Details.Metadata.ContainsKey("DAFApplicationID"))
                        dafApp.Details.Metadata.Remove("DAFApplicationID");

                    if (dafApp.Details.Metadata.ContainsKey("DAFApplicationRoot"))
                        dafApp.Details.Metadata.Remove("DAFApplicationRoot");
                }

                var dafAppResp = await appDev.SaveDAFApps(new[] { dafApp }.ToList(), State.ActiveApp.ID, entLookup);

                State.ActiveDAFApp = dafAppResp.Model?.FirstOrDefault();
            }

            await SetActiveApp(appMgr, entLookup, State.ActiveApp);
        }

        public virtual async Task SaveDataApp(ApplicationDeveloperClient appDev, ApplicationManagerClient appMgr, string entLookup, string host,
            Application app)
        {
            var appResp = await appDev.SaveApp(app, host, "lcu-data-apps", entLookup);

            await SetActiveApp(appMgr, entLookup, appResp.Model);
        }

        public virtual async Task SetActiveApp(ApplicationManagerClient appMgr, string entLookup, Application app)
        {
            await ToggleAddNew(AddNewTypes.None);

            State.ActiveApp = app;

            await LoadAppView(appMgr, entLookup);

            await LoadDAFAppOptions(appMgr, entLookup);
        }

        public virtual async Task SetActiveDAFAPIApp(Guid dafApiAppId)
        {
            State.ActiveDAFApp = State.ActiveDAFAPIs?.FirstOrDefault(da => da.ID == dafApiAppId);
        }

        public virtual async Task SetViewType(DAFAppTypes appType)
        {
            State.AppType = appType;
        }

        public virtual async Task ToggleAddNew(AddNewTypes type)
        {
            State.ActiveApp = null;

            switch (type)
            {
                case AddNewTypes.App:
                    State.AddingApp = !State.AddingApp;
                    break;

                case AddNewTypes.None:
                    State.AddingApp = false;
                    break;
            }
        }

        public virtual async Task ToggleAppAsDefault(ApplicationManagerClient appMgr, ApplicationDeveloperClient appDev, string entLookup, Guid appId)
        {
            var response = await appDev.ToggleAppAsDefault(entLookup, appId);

            await LoadDefaultApps(appMgr, entLookup);
        }
        #endregion
    }
}
