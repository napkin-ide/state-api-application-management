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
        public virtual async Task Ensure(ApplicationManagerClient appMgr, IdentityManagerClient idMgr, string entApiKey)
        {
            await LoadAccessRightOptions(idMgr, entApiKey);

            await LoadApplications(appMgr, entApiKey);
        }

        public virtual async Task LoadAccessRightOptions(IdentityManagerClient idMgr, string entApiKey)
        {
            var accessRightsResp = await idMgr.ListAccessRights(entApiKey);

            State.AccessRightOptions = accessRightsResp?.Model.Select(ar => ar.Lookup).ToList() ?? new List<string>();
        }

        public virtual async Task LoadApplications(ApplicationManagerClient appMgr, string entApiKey)
        {
            var appsResult = await appMgr.ListApplications(entApiKey);

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

            await SetActiveApp(appMgr, entApiKey,
                State.Applications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup)?.PathGroup);
        }

        public virtual async Task LoadAppView(ApplicationManagerClient appMgr, string entApiKey)
        {
            if (State.ActiveAppPathGroup != null)
            {
                var appDetails = State.Applications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup);

                var dafApps = State.DAFApplications;

                if (!dafApps.IsNullOrEmpty())
                {
                    // if (dafApps.Count < 2)
                    // {
                    //     State.ActiveDAFApp = dafApps?.Model?.FirstOrDefault()?.JSONConvert<DAFApplicationConfiguration>();

                    //     State.ActiveDAFAPIs = null;
                    // }
                    // else
                    // {
                    //     State.ActiveDAFAPIs = dafApps?.Model?.Select(da => da?.JSONConvert<DAFAPIConfiguration>())?.ToList();

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

        public virtual async Task LoadDAFApplications(ApplicationManagerClient appMgr, string entApiKey)
        {
            State.DAFApplications = new List<DataDAFAppDetails>();

            if (!State.Applications.IsNullOrEmpty() && !State.ActiveAppPathGroup.IsNullOrEmpty())
            {
                var activeApp = State.Applications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup);

                await activeApp.AppIDs.Each(async appId =>
                {
                    var dafApps = await appMgr.ListDAFApplications(entApiKey, appId.Key);

                    var appResp = await appMgr.GetApplication(entApiKey, appId.Key);

                    var app = appResp.Model;

                    lock (activeApp)
                        State.DAFApplications.AddRange((dafApps.Model ?? new List<DAFApplicationConfiguration>()).Select(dafApp =>
                        {
                            var lookup = dafApp.Lookup.IsNullOrEmpty() ? "" : $" {dafApp.Lookup}";

                            return new DataDAFAppDetails()
                            {
                                Description = app.Description,
                                ID = dafApp.ID,
                                Name = $"{app.Name}{lookup}",
                                Priority = app.Priority,
                                Path = app.PathRegex.Replace("*", "")
                            };
                        }));
                });
            }
        }

        public virtual async Task LoadDAFAppOptions(ApplicationManagerClient appMgr, string entApiKey)
        {
            State.DAFAppOptions = new Dictionary<string, string>();

            // if (!State.Applications.IsNullOrEmpty() && !State.DAFApplications.IsNullOrEmpty())
            // {
            //     await State.Applications.Each(async app =>
            //     {
            //         await app.AppIDs.Each(async appId =>
            //         {
            //             var dafApps = State.DAFApplications[appId.Key];

            //             dafApps.Each(dafApp => State.DAFAppOptions[dafApp.ID.ToString()] = $"{app.Name} {appId.Value} {dafApp.Lookup}");
            //         });
            //     });
            // }
        }

        public virtual async Task SetActiveApp(ApplicationManagerClient appMgr, string entApiKey, string appPathGroup)
        {
            State.ActiveAppPathGroup = appPathGroup;

            await LoadDAFApplications(appMgr, entApiKey);

            // await LoadDAFAppOptions(appMgr, entApiKey);

            await LoadAppView(appMgr, entApiKey);
        }

        public virtual async Task SetActiveDAFApp(string dafAppId)
        {
            State.ActiveDAFApp = dafAppId;
        }

        public virtual async Task SetApplicationTab(int appTab)
        {
            State.CurrentApplicationTab = appTab;
        }
        #endregion

        #region Helpers
        protected virtual async Task<List<DAFAppStatus>> calculateAppStati(string name, List<Application> apps)
        {
            var appStati = new List<DAFAppStatus>();

            appStati.Add(new DAFAppStatus()
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

            appStati.Add(new DAFAppStatus()
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
        #endregion
    }
}
