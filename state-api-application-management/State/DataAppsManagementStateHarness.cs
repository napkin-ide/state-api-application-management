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
using System.Text.RegularExpressions;
using LCU.Personas.Client.Security;

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
        public DataAppsManagementStateHarness(DataAppsManagementState state, ILogger log)
            : base(state ?? new DataAppsManagementState(), log)
        { }
        #endregion

        #region API Methods
        public virtual async Task DeleteDAFApp(ApplicationDeveloperClient appDev, ApplicationManagerClient appMgr, string entLookup,
            Guid appId, List<string> lookups)
        {
            log.LogInformation($"Deleting DAF Applications for {entLookup} from {appId} with lookups {lookups.ToJSON()}");

            var dafApps = await appMgr.ListDAFApplications(entLookup, appId);

            await lookups.Each(async lookup =>
            {
                lookup = lookup.Trim();

                var dafApp = dafApps.Model.FirstOrDefault(da => da.Lookup == lookup);

                if (dafApp != null)
                {
                    log.LogInformation($"Removing DAF Application {lookup} for {appId}");

                    await appDev.RemoveDAFApp(appId, dafApp.ID, entLookup);
                }
            });

            dafApps = await appMgr.ListDAFApplications(entLookup, appId);

            if (dafApps.Status && dafApps.Model.IsNullOrEmpty())
            {
                log.LogInformation($"Removing entire Application {appId}");

                await appDev.RemoveApp(appId, entLookup);
            }

            await LoadApplications(appMgr, entLookup);
        }

        public virtual async Task LoadAccessRightOptions(IdentityManagerClient idMgr, string entLookup)
        {
            log.LogInformation($"Loading Access right options for {entLookup}");

            var accessRightsResp = await idMgr.ListAccessRights(entLookup);

            State.AccessRightOptions = accessRightsResp?.Model?.Select(ar => ar.Lookup).ToList() ?? new List<string>();
        }

        public virtual async Task LoadApplications(ApplicationManagerClient appMgr, string entLookup)
        {
            log.LogInformation($"Loading Applications for {entLookup}");

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

            log.LogInformation($"Preparing system applications");

            var apiAppDets = State.Applications.FirstOrDefault(app => app.PathGroup == "/api") ?? new DataAppDetails()
            {
                AppIDs = new Dictionary<Guid, string>(),
                PathGroup = "/api",
                AppStati = new List<DataDAFAppStatus>()
            };

            apiAppDets.DisplayName = "APIs";

            apiAppDets.Description = "Manage API proxies to control usage and security.";

            var lcuAppDets = State.Applications.FirstOrDefault(app => app.PathGroup == "/_lcu") ?? new DataAppDetails()
            {
                AppIDs = new Dictionary<Guid, string>(),
                PathGroup = "/_lcu",
                AppStati = new List<DataDAFAppStatus>()
            };

            lcuAppDets.DisplayName = "Low Code Units";

            lcuAppDets.Description = "Manage reusable web components for your IDE and applications.";

            var homeAppDets = State.Applications.FirstOrDefault(app => app.PathGroup == "/") ?? new DataAppDetails()
            {
                AppIDs = new Dictionary<Guid, string>(),
                PathGroup = "/",
                AppStati = new List<DataDAFAppStatus>()
            };

            homeAppDets.DisplayName = "Home Pages";

            homeAppDets.Description = "Manage home page applications, redirects and more.";

            State.FixedApplications = new List<DataAppDetails>() { apiAppDets, lcuAppDets, homeAppDets };

            State.Applications = State.Applications.Where(app => !State.FixedApplications.Contains(app)).ToList();

            await SetActiveApp(appMgr, entLookup,
                AllApplications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup)?.PathGroup);
        }

        public virtual async Task LoadAppView(ApplicationManagerClient appMgr, string entLookup)
        {
            if (State.ActiveAppPathGroup != null)
            {
                log.LogInformation($"Loading Application View for {entLookup}");

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
            log.LogInformation($"Loading DAF Applications for {entLookup}");

            State.DAFApplications = new List<DataDAFAppDetails>();

            if (!AllApplications.IsNullOrEmpty() && !State.ActiveAppPathGroup.IsNullOrEmpty())
            {
                log.LogInformation($"Loading active DAF Applications for {entLookup}");

                var activeApp = AllApplications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup);

                await activeApp.AppIDs.Each(async appId =>
                {
                    log.LogInformation($"Listing active DAF Applications for {entLookup} from {appId}");

                    var dafApps = await appMgr.ListDAFApplications(entLookup, appId.Key);

                    if (dafApps.Status)
                    {
                        log.LogInformation($"Preparing DAF Application details for {entLookup} from {appId}");

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

        public virtual async Task LoadGlobalAppSettings(SecurityManagerClient secMgr, string entLookup)
        {
            log.LogInformation($"Loading global app settings {entLookup}");

            var lookups = new[] {
                $"{State.ActiveHost}:GOOGLE-ANALYTICS-MEASUREMENT-ID",
                $"{State.ActiveHost}:GOOGLE-TAG-MANAGER-ID",
                $"{State.ActiveHost}:ORIBI-ANALYTICS-TRACKING-ID"
            };

            var resp = await secMgr.RetrieveEnterpriseThirdPartyData(entLookup, lookups);

            State.GlobalAppSettings = new GlobalApplicationSettings();

            resp.Model.Each(setting =>
            {
                if (!setting.Value.IsNullOrEmpty())
                    State.GlobalAppSettings.Metadata[setting.Key.Replace($"{State.ActiveHost}:", String.Empty)] = setting.Value;
            });
        }

        public virtual async Task LoadSupportedDAFAppTypes()
        {
            State.SupportedDAFAppTypes = new List<DataDAFAppTypes>();

            var activeApp = AllApplications.FirstOrDefault(app => app.PathGroup == State.ActiveAppPathGroup);

            if (activeApp != null)
            {
                var activeAppType = State.DAFApplications.FirstOrDefault()?.DAFAppType;

                if (activeAppType == DataDAFAppTypes.API || activeAppType == DataDAFAppTypes.LCU)
                    State.SupportedDAFAppTypes.Add(activeAppType.Value);
                else if (activeAppType == DataDAFAppTypes.Redirect || activeAppType == DataDAFAppTypes.View ||
                    activeAppType == null)
                {
                    State.SupportedDAFAppTypes.Add(DataDAFAppTypes.View);

                    State.SupportedDAFAppTypes.Add(DataDAFAppTypes.Redirect);
                }
                else
                {
                    State.SupportedDAFAppTypes.Add(DataDAFAppTypes.View);

                    State.SupportedDAFAppTypes.Add(DataDAFAppTypes.Redirect);
                }
            }
            else
            {
                State.SupportedDAFAppTypes.Add(DataDAFAppTypes.View);

                State.SupportedDAFAppTypes.Add(DataDAFAppTypes.Redirect);

                State.SupportedDAFAppTypes.Add(DataDAFAppTypes.API);

                State.SupportedDAFAppTypes.Add(DataDAFAppTypes.LCU);
            }
        }

        public virtual async Task Refresh(ApplicationManagerClient appMgr, EnterpriseManagerClient entMgr, IdentityManagerClient idMgr,
            SecurityManagerClient secMgr, string entLookup, string host)
        {
            log.LogInformation($"Refreshing data apps management state for {entLookup} at {State.ActiveHost}");

            // if (State.ActiveHost.IsNullOrEmpty())
            SetActiveHost(host);

            await LoadApplications(appMgr, entLookup);

            await Task.WhenAll(
                LoadAccessRightOptions(idMgr, entLookup),
                LoadDAFAppOptions(appMgr, entLookup),
                LoadGlobalAppSettings(secMgr, entLookup),
                RefreshZipOptions(appMgr, entMgr, entLookup)
            );

            await LoadSupportedDAFAppTypes();
        }

        public virtual async Task RefreshZipOptions(ApplicationManagerClient appMgr, EnterpriseManagerClient entMgr, string entLookup)
        {
            log.LogInformation($"Refreshing Zip Options for {entLookup}");

            State.ZipAppOptions = new List<ZipAppOption>();

            var entRes = await entMgr.GetEnterprise(entLookup);

            if (entRes.Status)
            {
                // var listRes = await appMgr.Get<ListFilesResponse>($"dfs/list/{entRes.Model.ID}/app-uploads/application/zip");

                log.LogInformation($"Listing files for Zip Options");

                var listRes = await appMgr.ListFiles(entRes.Model.ID, $"app-uploads/application/zip");

                if (listRes.Status)
                {
                    State.ZipAppOptions = listRes.Files.Select(file =>
                    {
                        return new ZipAppOption()
                        {
                            DisplayName = file,
                            File = file
                        };
                    }).ToList();
                }
            }

            State.ZipLoading = false;
        }

        public virtual async Task SaveDAFApp(ApplicationDeveloperClient appDev, ApplicationManagerClient appMgr, string entLookup,
            string host, DataDAFAppDetails dafAppDetails)
        {
            log.LogInformation($"Saving DAF Application for {entLookup}");

            var saveRes = await appDev.SaveAppAndDAFApps(new SaveAppAndDAFAppsRequest()
            {
                Application = new Application()
                {
                    ID = dafAppDetails.ID,
                    Name = dafAppDetails.Name,
                    Description = dafAppDetails.Description,
                    PathRegex = $"{dafAppDetails.Path.TrimEnd('/')}*",
                    AccessRights = dafAppDetails.Security.AccessRights,
                    Licenses = dafAppDetails.Security.Licenses,
                    IsPrivate = dafAppDetails.Security.IsPrivate,
                    Priority = dafAppDetails.Priority
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

            State.ActiveDAFAppID = null;

            await LoadApplications(appMgr, entLookup);
        }

        public virtual async Task SaveGlobalAppSettings(SecurityManagerClient secMgr, string entLookup, GlobalApplicationSettings settings)
        {
            log.LogInformation($"Saving global app settings {entLookup} at {State.ActiveHost}: {settings.ToJSON()}");

            await settings.Metadata.Each(async (setting) =>
            {
                var resp = await secMgr.SetEnterpriseThirdPartyData(entLookup, new Dictionary<string, string>()
                {
                    { $"{State.ActiveHost}:{setting.Key}", setting.Value.ToString() }
                });

                return !resp.Status;
            });

            await LoadGlobalAppSettings(secMgr, entLookup);
        }

        public virtual async Task SetActiveApp(ApplicationManagerClient appMgr, string entLookup, string appPathGroup)
        {
            log.LogInformation($"Setting active application for {entLookup} to {appPathGroup}");

            State.ActiveAppPathGroup = appPathGroup;

            // State.CurrentApplicationTab = 0;

            await LoadDAFApplications(appMgr, entLookup);

            await LoadDAFAppOptions(appMgr, entLookup);

            await LoadAppView(appMgr, entLookup);

            await LoadSupportedDAFAppTypes();
        }

        public virtual async Task SetActiveDAFApp(Guid? dafAppId)
        {
            log.LogInformation($"Setting active DAF Application to {dafAppId}");

            State.ActiveDAFAppID = dafAppId;
        }

        public virtual void SetActiveHost(string host)
        {
            log.LogInformation($"Setting active host {host}");

            State.ActiveHost = host;
        }

        public virtual async Task SetApplicationTab(int appTab)
        {
            log.LogInformation($"Setting application tab to {appTab}");

            State.CurrentApplicationTab = appTab;
        }

        public virtual async Task UploadZips(ApplicationManagerClient appMgr, EnterpriseManagerClient entMgr, string entLookup,
            List<ZipAppOption> zipApps)
        {
            log.LogInformation($"Uploading zip application options for {entLookup}");

            var regex = new Regex("^data:(?<type>.*);base64,(?<data>.*)$");

            var stati = new List<Status>();

            await zipApps.Each(async zipApp =>
            {
                var match = regex.Match(zipApp.Data);

                var dataType = match.Groups["type"].Value;

                var base64Data = match.Groups["data"].Value;

                var data = Convert.FromBase64String(base64Data);

                var entRes = await entMgr.GetEnterprise(entLookup);

                if (entRes.Status)
                {
                    log.LogInformation($"Saving zip application option file for {entLookup}");

                    var saveRes = await appMgr.SaveFile(data, entRes.Model.ID, "", zipApp.File, null, $"app-uploads/{dataType}");

                    stati.Add(saveRes.Status);
                }
                else
                    stati.Add(entRes.Status);
            });

            await RefreshZipOptions(appMgr, entMgr, entLookup);
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
                return dafApp.Lookup.IsNullOrEmpty() ? "" : dafApp.Lookup.Trim();
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
                    { "Lookup", dafApp.Details.Metadata.ContainsKey("Lookup") ? dafApp.Details.Metadata["Lookup"].ToString().Trim() : "" },
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
                var view = dafApp.Details.JSONConvert<DAFLCUApplicationDetails>();

                var package = new Dictionary<string, JToken>()
                {
                    { "BaseHref", view.BaseHref },
                    { "Package", view.Package.JSONConvert<JToken>() },
                    { "PackageType", view.PackageType.ToString() },
                    { "RegScripts", view.RegScripts },
                    { "StateConfig", view.StateConfig?.JSONConvert<JToken>() }
                };

                if (dafApp.Details.Metadata["BaseHref"].ToString().StartsWith("/_lcu/"))
                {
                    dafAppType = DataDAFAppTypes.LCU;

                    package.Add("Lookup", view.Lookup);
                }
                else
                    dafAppType = DataDAFAppTypes.View;

                return package.JSONConvert<MetadataModel>();
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
