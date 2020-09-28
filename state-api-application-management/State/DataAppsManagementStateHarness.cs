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
using LCU.Personas.Applications;

namespace LCU.State.API.NapkinIDE.ApplicationManagement.State
{
    public class DataAppsManagementStateHarness : LCUStateHarness<DataAppsManagementState>
    {
        #region Fields 
        #endregion

        #region Properties 
        public virtual List<DataAppDetails> AllApplications
        {
            get
            {
                var apps = new List<DataAppDetails>(State.Applications);

                apps.AddRange(State.FixedApplications);

                return apps;
            }
        }
        #endregion

        #region Constructors
        public DataAppsManagementStateHarness(DataAppsManagementState state)
            : base(state ?? new DataAppsManagementState())
        { }
        #endregion

        #region API Methods
        public virtual async Task DeleteDAFApp(ApplicationDeveloperClient appDev, ApplicationManagerClient appMgr, string entLookup,
            Guid appId, List<string> lookups)
        {
            var dafApps = await appMgr.ListDAFApplications(entLookup, appId);

            await lookups.Each(async lookup =>
            {
                var dafApp = dafApps.Model.FirstOrDefault(da => da.Lookup == lookup);

                if (dafApp != null)
                    await appDev.RemoveDAFApp(appId, dafApp.ID, entLookup);
            });

            dafApps = await appMgr.ListDAFApplications(entLookup, appId);

            if (dafApps.Status && dafApps.Model.IsNullOrEmpty())
                await appDev.RemoveApp(appId, entLookup);

            await LoadApplications(appMgr, entLookup);
        }

        public virtual async Task Ensure(ApplicationManagerClient appMgr, IdentityManagerClient idMgr, string entLookup)
        {
            await LoadAccessRightOptions(idMgr, entLookup);

            await LoadApplications(appMgr, entLookup);

            await LoadDAFAppOptions(appMgr, entLookup);
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
                    AppStati = calculateAppStati(appGroup.Key, appGroup.ToList()).Result,
                    DisplayName = appGroup.Key,
                    PathGroup = appGroup.Key
                };
            }).ToList();

            var apiAppDets = State.Applications.FirstOrDefault(app => app.PathGroup == "/api") ?? new DataAppDetails()
            {
                AppIDs = new Dictionary<Guid, string>(),
                PathGroup = "/api",
                AppStati = new List<DataDAFAppStatus>()
            };

            apiAppDets.DisplayName = "API Management";

            apiAppDets.Description = "Manage your API proxies, to gain control over usage and security.";

            var lcuAppDets = State.Applications.FirstOrDefault(app => app.PathGroup == "/_lcu") ?? new DataAppDetails()
            {
                AppIDs = new Dictionary<Guid, string>(),
                PathGroup = "/_lcu",
                AppStati = new List<DataDAFAppStatus>()
            };

            lcuAppDets.DisplayName = "Low Code Units";

            lcuAppDets.Description = "Reusable components for your IDE and more...";

            var homeAppDets = State.Applications.FirstOrDefault(app => app.PathGroup == "/") ?? new DataAppDetails()
            {
                AppIDs = new Dictionary<Guid, string>(),
                PathGroup = "/",
                AppStati = new List<DataDAFAppStatus>()
            };

            homeAppDets.DisplayName = "Home Pages";

            homeAppDets.Description = "Manage home page applications, redirects and more";

            State.FixedApplications = new List<DataAppDetails>() { apiAppDets, lcuAppDets, homeAppDets };

            State.Applications = State.Applications.Where(app => !State.FixedApplications.Contains(app)).ToList();

            await SetActiveApp(appMgr, entLookup,
                AllApplications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup)?.PathGroup);
        }

        public virtual async Task LoadAppView(ApplicationManagerClient appMgr, string entLookup)
        {
            if (State.ActiveAppPathGroup != null)
            {
                var appDetails = AllApplications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup);

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

            if (!AllApplications.IsNullOrEmpty() && !State.ActiveAppPathGroup.IsNullOrEmpty())
            {
                var activeApp = AllApplications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup);

                await activeApp.AppIDs.Each(async appId =>
                {
                    var dafApps = await appMgr.ListDAFApplications(entLookup, appId.Key);

                    if (dafApps.Status)
                    {
                        var dafAppDetails = await getDetailsFromDAFApp(appMgr, entLookup, appId.Key, dafApps.Model);

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

            // var allDafApps = await appMgr.ListAllDAFApplications(entLookup);

            // if (!AllApplications.IsNullOrEmpty() && !allDafApps.Model.IsNullOrEmpty() &&
            //     allDafApps.Model.Any(dafApp => dafApp.DAFAppType == DataDAFAppTypes.DAFAppPointer))
            // {
            //     await AllApplications.Each(async app =>
            //     {
            //         await app.AppIDs.Each(async appId =>
            //         {
            //             var appResp = await appMgr.GetApplication(entLookup, appId.Key);

            //             var app = appResp.Model;

            //             var dafApps = await appMgr.ListDAFApplications(entLookup, appId.Key);

            //             dafApps?.Model.Each(dafApp => State.DAFAppOptions[dafApp.ID.ToString()] = $"{app.Name} {appId.Value} {dafApp.Lookup}");
            //         });
            //     });
            // }
        }

        public virtual async Task SaveDAFApp(ApplicationDeveloperClient appDev, ApplicationManagerClient appMgr, string entLookup,
            string host, DataDAFAppDetails dafAppDetails)
        {
            var saveRes = await appDev.SaveAppAndDAFApps(new SaveAppAndDAFAppsRequest()
            {
                Application = new Application()
                {
                    ID = dafAppDetails.ID,
                    Name = dafAppDetails.Name,
                    Description = dafAppDetails.Description,
                    PathRegex = $"{dafAppDetails.Path.TrimEnd('/')}*"
                },
                DAFApps = dafAppDetails.Configs.Select(dafAppConfig =>
                {
                    return new DAFApplication()
                    {
                        Lookup = dafAppConfig.Key,
                        Details = dafAppConfig.Value,
                        Priority = 500
                    };
                }).ToList()
            }, entLookup, host);

            await LoadApplications(appMgr, entLookup);
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

        protected virtual async Task<DataDAFAppDetails> getDetailsFromDAFApp(ApplicationManagerClient appMgr, string entLookup, Guid appId,
            List<DAFApplication> dafApps)
        {
            var appResp = await appMgr.GetApplication(entLookup, appId);

            var app = appResp.Model;

            var dafAppStatus = new DataDAFAppStatus()
            {
                Name = "Public",
                AppCount = dafApps.Count,
                Code = Status.Success.Code,
                Message = "All applications are running well.",
                Icon = new LCUIcon()
                {
                    Icon = app.IsPrivate ? "security" : "public"
                }
            };

            DataDAFAppTypes? dafAppType = null;
            var configs = (dafApps ?? new List<DAFApplication>()).ToDictionary(dafApp =>
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
                Path = app.PathRegex.Replace("*", ""),
                Security = new DataAppSecurityDetails()
                {
                    AccessRights = app.AccessRights,
                    IsPrivate = app.IsPrivate,
                    Licenses = app.Licenses
                }
            };

            return dafAppDetails;
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
