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
        public virtual async Task Ensure(ApplicationManagerClient appMgr, string entApiKey)
        {
            State.Applications = new List<Application>();

            if (!State.AppType.HasValue)
                State.AppType = DAFAppTypes.View;

            if (State.ActiveApp != null)
                await SetActiveApp(appMgr, entApiKey, State.ActiveApp);
        }

        public virtual async Task LoadAccessRightOptions(IdentityManagerClient idMgr, string entApiKey)
        {
            var accessRightsResp = await idMgr.ListAccessRights(entApiKey);

            State.AccessRightOptions = accessRightsResp?.Model.Select(ar => ar.Lookup).ToList() ?? new List<string>();
        }

        public virtual async Task LoadApplications(ApplicationManagerClient appMgr, string entApiKey)
        {
            var apps = await appMgr.ListApplications(entApiKey);

            State.Applications = apps.Model.Where(app => app.Container == "lcu-data-apps").ToList();

            State.ActiveApp = State.Applications.FirstOrDefault(app => app.ID == State.ActiveApp?.ID);

            await LoadDAFAppOptions(appMgr, entApiKey);
        }

        public virtual async Task LoadAppView(ApplicationManagerClient appMgr, string entApiKey)
        {
            if (State.ActiveApp != null)
            {
                var dafApps = await appMgr.ListDAFApplications(entApiKey, State.ActiveApp.ID);

                if (dafApps.Status)
                {
                    if (dafApps.Model.Count < 2)
                    {
                        State.ActiveDAFApp = dafApps?.Model?.FirstOrDefault()?.JSONConvert<DAFApplicationConfiguration>();

                        State.ActiveDAFAPIs = null;
                    }
                    else
                    {
                        State.ActiveDAFAPIs = dafApps?.Model?.Select(da => da?.JSONConvert<DAFAPIConfiguration>())?.ToList();

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
                    if (State.ActiveDAFApp.Metadata.ContainsKey("APIRoot"))
                        await SetViewType(DAFAppTypes.API);
                    else if (State.ActiveDAFApp.Metadata.ContainsKey("Redirect"))
                        await SetViewType(DAFAppTypes.Redirect);
                    else if (State.ActiveDAFApp.Metadata.ContainsKey("BaseHref"))
                        await SetViewType(DAFAppTypes.View);
                }
                else if (!State.ActiveDAFAPIs.IsNullOrEmpty())
                    await SetViewType(DAFAppTypes.API);
            }
            else
                State.ActiveDAFApp = null;
        }

        public virtual async Task LoadDAFAppOptions(ApplicationManagerClient appMgr, string entApiKey)
        {
            State.DAFAppOptions = new Dictionary<string, string>();

            if (!State.Applications.IsNullOrEmpty())
            {
                await State.Applications.Each(async app =>
                {
                    var dafApps = await appMgr.ListDAFApplications(entApiKey, app.ID);

                    dafApps.Model.Each(dafApp => State.DAFAppOptions[dafApp.ID.ToString()] = $"{app.Name} {dafApp.Lookup}");
                });
            }
        }

        public virtual async Task LoadDefaultApps(ApplicationManagerClient appMgr, string entApiKey)
        {
            var apps = await appMgr.ListDefaultApplications(entApiKey);

            State.DefaultApps = apps.Model;

            var defApps = await appMgr.HasDefaultApplications(entApiKey);

            State.DefaultAppsEnabled = defApps.Status;
        }

        public virtual async Task SaveDAFApp(ApplicationDeveloperClient appDev, ApplicationManagerClient appMgr, string entApiKey,
            DAFApplicationConfiguration dafApp)
        {
            if (State.ActiveApp != null)
            {
                if (State.AppType != DAFAppTypes.API)
                {
                    if (dafApp.Metadata.ContainsKey("APIRoot"))
                        dafApp.Metadata.Remove("APIRoot");

                    if (dafApp.Metadata.ContainsKey("InboundPath"))
                        dafApp.Metadata.Remove("InboundPath");

                    if (dafApp.Metadata.ContainsKey("Methods"))
                        dafApp.Metadata.Remove("Methods");

                    if (dafApp.Metadata.ContainsKey("Security"))
                        dafApp.Metadata.Remove("Security");
                }

                if (State.AppType != DAFAppTypes.Redirect)
                {
                    if (dafApp.Metadata.ContainsKey("Redirect"))
                        dafApp.Metadata.Remove("Redirect");
                }

                if (State.AppType != DAFAppTypes.View)
                {
                    if (dafApp.Metadata.ContainsKey("BaseHref"))
                        dafApp.Metadata.Remove("BaseHref");

                    if (dafApp.Metadata.ContainsKey("NPMPackage"))
                        dafApp.Metadata.Remove("NPMPackage");

                    if (dafApp.Metadata.ContainsKey("PackageVersion"))
                        dafApp.Metadata.Remove("PackageVersion");
                }

                var dafAppResp = await appDev.SaveDAFApps(new[] { dafApp }.ToList(), State.ActiveApp.ID, entApiKey);

                State.ActiveDAFApp = dafAppResp.Model?.FirstOrDefault();
            }

            await SetActiveApp(appMgr, entApiKey, State.ActiveApp);
        }

        public virtual async Task SaveDataApp(ApplicationDeveloperClient appDev, ApplicationManagerClient appMgr, string entApiKey, string host,
            Application app)
        {
            var appResp = await appDev.SaveApp(app, host, "lcu-data-apps", entApiKey);

            await SetActiveApp(appMgr, entApiKey, appResp.Model);
        }

        public virtual async Task SetActiveApp(ApplicationManagerClient appMgr, string entApiKey, Application app)
        {
            await ToggleAddNew(AddNewTypes.None);

            State.ActiveApp = app;

            await LoadAppView(appMgr, entApiKey);

            await LoadDAFAppOptions(appMgr, entApiKey);
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

        public virtual async Task ToggleAppAsDefault(ApplicationManagerClient appMgr, ApplicationDeveloperClient appDev, string entApiKey, Guid appId)
        {
            var response = await appDev.ToggleAppAsDefault(entApiKey, appId);

            await LoadDefaultApps(appMgr, entApiKey);
        }
        #endregion
    }
}
