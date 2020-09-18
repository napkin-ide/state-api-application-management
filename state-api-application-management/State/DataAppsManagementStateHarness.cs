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
using Newtonsoft.Json.Linq;

namespace LCU.State.API.NapkinIDE.ApplicationManagement.State
{
    public class DataAppsManagementStateHarness : LCUStateHarness<DataAppsManagementState>
    {
        #region Fields 
        #endregion

        #region Properties 
        #endregion

        #region Constructors
        public DataAppsManagementStateHarness(DataAppsManagementState state)
            : base(state ?? new DataAppsManagementState())
        { }
        #endregion

        #region API Methods
        public virtual async Task Ensure(ApplicationManagerClient appMgr, IdentityManagerClient idMgr, string entLookup)
        {
            await LoadAccessRightOptions(idMgr, entLookup);

            await LoadApplications(appMgr, entLookup);
        }

        public virtual async Task LoadAccessRightOptions(IdentityManagerClient idMgr, string entLookup)
        {
            var accessRightsResp = await idMgr.ListAccessRights(entLookup);

            State.AccessRightOptions = accessRightsResp?.Model?.Select(ar => ar.Lookup).ToList() ?? new List<string>();
        }

        public virtual async Task LoadApplications(ApplicationManagerClient appMgr, string entLookup)
        {
            var appsResult = await appMgr.ListApplications(entLookup);

            //  TODO:  Renable where filter for filtering support.  Each app type will have a different container
            var apps = appsResult.Model;//.Where(app => app.Container == "lcu-data-apps").ToList();

            State.Applications = apps.GroupBy(app =>
            {
                var pathParts = app.PathRegex.Replace("*", "").Split('/');

                var pathRoot = pathParts.FirstOrDefault(pathPart => !pathPart.IsNullOrEmpty() || pathParts.All(pp => pp.IsNullOrEmpty()));

                return $"/{pathRoot}";
            }).Select(appGroup =>
            {
                return new DataAppDetails()
                {
                    AppIDs = appGroup.ToDictionary(ag => ag.ID, ag => ag.PathRegex.Replace("*", "")),
                    PathGroup = appGroup.Key,
                    AppStati = calculateAppStati(appGroup.Key, appGroup.ToList()).Result
                };
            }).ToList();

            await SetActiveApp(appMgr, entLookup,
                State.Applications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup)?.PathGroup);
        }

        public virtual async Task LoadAppView(ApplicationManagerClient appMgr, string entLookup)
        {
            if (State.ActiveAppPathGroup != null)
            {
                var appDetails = State.Applications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup);

                var dafApps = State.DAFApplications;

                if (!dafApps.IsNullOrEmpty())
                {
                    // if (dafApps.Count < 2)
                    // {
                    //     State.ActiveDAFApp = dafApps?.Model?.FirstOrDefault()?.JSONConvert<DAFApplication>();

                    //     State.ActiveDAFAPIs = null;
                    // }
                    // else
                    // {
                    //     State.ActiveDAFAPIs = dafApps?.Model?.Select(da => da?.JSONConvert<DAFAPIApplicationDetails>())?.ToList();

                    //     State.ActiveDAFApp = null;
                    // }
                }
                else
                {
                    // State.ActiveDAFApp = null;

                    // State.ActiveDAFAPIs = null;
                }

                // if (State.ActiveDAFApp != null)
                // {
                //     if (State.ActiveDAFApp.Metadata.ContainsKey("APIRoot"))
                //         await SetViewType(DAFAppTypes.API);
                //     else if (State.ActiveDAFApp.Metadata.ContainsKey("Redirect"))
                //         await SetViewType(DAFAppTypes.Redirect);
                //     else if (State.ActiveDAFApp.Metadata.ContainsKey("BaseHref"))
                //         await SetViewType(DAFAppTypes.View);
                //     else if (State.ActiveDAFApp.Metadata.ContainsKey("DAFApplicationID"))
                //         await SetViewType(DAFAppTypes.DAFApp);
                // }
                // else if (!State.ActiveDAFAPIs.IsNullOrEmpty())
                //     await SetViewType(DAFAppTypes.API);
            }
            else
            {
                // State.ActiveDAFApp = null;

                // State.ActiveDAFAPIs = null;
            }
        }

        public virtual async Task LoadDAFApplications(ApplicationManagerClient appMgr, string entLookup)
        {
            State.DAFApplications = new List<DataDAFAppDetails>();

            if (!State.Applications.IsNullOrEmpty() && !State.ActiveAppPathGroup.IsNullOrEmpty())
            {
                var activeApp = State.Applications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup);

                await activeApp.AppIDs.Each(async appId =>
                {
                    var dafApps = await appMgr.ListDAFApplications(entLookup, appId.Key);

                    if (dafApps.Status)
                    {
                        var appResp = await appMgr.GetApplication(entLookup, appId.Key);

                        var app = appResp.Model;

                        var dafAppStatus = new DataDAFAppStatus()
                        {
                            Name = "Public",
                            AppCount = dafApps.Model.Count,
                            Code = Status.Success.Code,
                            Message = "All applications are running well.",
                            Icon = new LCUIcon()
                            {
                                Icon = app.IsPrivate ? "security" : "public"
                            }
                        };

                        DataDAFAppTypes? dafAppType = null;
                        var configs = (dafApps.Model ?? new List<DAFApplication>()).ToDictionary(dafApp =>
                        {
                            return dafApp.Lookup.IsNullOrEmpty() ? "" : $" {dafApp.Lookup}";
                        }, dafApp =>
                        {
                            return loadDafConfig(dafApp, out dafAppType);
                        });

                        var dafAppDetails = new DataDAFAppDetails()
                        {
                            AppStatus = dafAppStatus,
                            Configs = configs,
                            DAFAppType = dafAppType,
                            Description = app.Description,
                            ID = app.ID,
                            Name = $"{app.Name}",
                            Priority = app.Priority,
                            Path = app.PathRegex.Replace("*", "")
                        };

                        lock (activeApp)
                            State.DAFApplications.Add(dafAppDetails);
                    }
                });
            }

            await SetActiveDAFApp(State.DAFApplications.FirstOrDefault(da => da.ID == State.ActiveDAFAppID)?.ID);
        }

        public virtual async Task LoadDAFAppOptions(ApplicationManagerClient appMgr, string entLookup)
        {
            State.DAFAppOptions = new Dictionary<string, string>();

            if (!State.Applications.IsNullOrEmpty() && !State.DAFApplications.IsNullOrEmpty() &&
                State.DAFApplications.Any(dafApp => dafApp.DAFAppType == DataDAFAppTypes.DAFAppPointer))
            {
                await State.Applications.Each(async app =>
                {
                    await app.AppIDs.Each(async appId =>
                    {
                        var appResp = await appMgr.GetApplication(entLookup, appId.Key);

                        var app = appResp.Model;

                        var dafApps = await appMgr.ListDAFApplications(entLookup, appId.Key);

                        dafApps?.Model.Each(dafApp => State.DAFAppOptions[dafApp.ID.ToString()] = $"{app.Name} {appId.Value} {dafApp.Lookup}");
                    });
                });
            }
        }

        public virtual async Task SetActiveApp(ApplicationManagerClient appMgr, string entLookup, string appPathGroup)
        {
            State.ActiveAppPathGroup = appPathGroup;

            // State.CurrentApplicationTab = 0;

            await LoadDAFApplications(appMgr, entLookup);

            await LoadDAFAppOptions(appMgr, entLookup);

            await LoadAppView(appMgr, entLookup);
        }

        public virtual async Task SetActiveDAFApp(Guid? dafAppId)
        {
            State.ActiveDAFAppID = dafAppId;
        }

        public virtual async Task SetApplicationTab(int appTab)
        {
            State.CurrentApplicationTab = appTab;
        }
        #endregion

        #region Helpers
        protected virtual async Task<List<DataDAFAppStatus>> calculateAppStati(string name, List<Application> apps)
        {
            var appStati = new List<DataDAFAppStatus>();

            appStati.Add(new DataDAFAppStatus()
            {
                Name = "Public",
                AppCount = apps.Where(app => !app.IsPrivate).Count(),
                Code = Status.Success.Code,
                Message = "All public applications are running well.",
                Icon = new LCUIcon()
                {
                    Icon = "public"
                }
            });

            appStati.Add(new DataDAFAppStatus()
            {
                Name = "Secure",
                AppCount = apps.Where(app => app.IsPrivate).Count(),
                Code = Status.Success.Code,
                Message = "All secured applications are running well.",
                Icon = new LCUIcon()
                {
                    Icon = "security"
                }
            });

            return appStati;
        }

        protected virtual MetadataModel loadDafConfig(DAFApplication dafApp, out DataDAFAppTypes? dafAppType)
        {
            if (dafApp.Details.Metadata.ContainsKey("APIRoot"))
            {
                dafAppType = DataDAFAppTypes.API;

                return new Dictionary<string, JToken>()
                {
                    { "APIRoot", dafApp.Details.Metadata["APIRoot"] },
                    { "InboundPath", dafApp.Details.Metadata["InboundPath"] },
                    { "Methods", dafApp.Details.Metadata["Methods"] },
                    { "Security", dafApp.Details.Metadata["Security"] }
                }.JSONConvert<MetadataModel>();
            }
            else if (dafApp.Details.Metadata.ContainsKey("Redirect"))
            {
                dafAppType = DataDAFAppTypes.Redirect;

                return new Dictionary<string, JToken>()
                {
                    { "Redirect", dafApp.Details.Metadata["Redirect"] }
                }.JSONConvert<MetadataModel>();
            }
            else if (dafApp.Details.Metadata.ContainsKey("BaseHref"))
            {
                dafAppType = DataDAFAppTypes.View;

                return new Dictionary<string, JToken>()
                {
                    { "BaseHref", dafApp.Details.Metadata["BaseHref"] },
                    { "NPMPackage", dafApp.Details.Metadata["NPMPackage"] },
                    { "PackageVersion", dafApp.Details.Metadata["PackageVersion"] }
                }.JSONConvert<MetadataModel>();
            }
            else if (dafApp.Details.Metadata.ContainsKey("DAFApplicationID"))
            {
                dafAppType = DataDAFAppTypes.DAFAppPointer;

                return new Dictionary<string, JToken>()
                {
                    { "DAFApplicationID", dafApp.Details.Metadata["DAFApplicationID"] },
                    { "DAFApplicationRoot", dafApp.Details.Metadata["DAFApplicationRoot"] }
                }.JSONConvert<MetadataModel>();
            }
            else
            {
                dafAppType = null;

                return null;
            }
        }
        #endregion
    }
}
