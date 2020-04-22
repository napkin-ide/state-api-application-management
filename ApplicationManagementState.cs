using System;
using System.IO;
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
using LCU.Graphs.Registry.Enterprises.Apps;

namespace LCU.State.API.NapkinIDE.ApplicationManagement
{
    [Serializable]
    [DataContract]
    public class ApplicationManagementState
    {
        #region Constants
        public const string HUB_NAME = "applicationmanagement";
        #endregion

        [DataMember]
        public virtual List<string> AccessRightOptions { get; set; }

        [DataMember]
        public virtual Application ActiveApp { get; set; }

        [DataMember]
        public List<DAFAPIConfiguration> ActiveDAFAPIs { get; set; }

        [DataMember]
        public virtual DAFApplicationConfiguration ActiveDAFApp { get; set; }

        [DataMember]
        public virtual bool AddingApp { get; set; }

        [DataMember]
        public virtual List<Application> Applications { get; set; }

        [DataMember]
        [JsonConverter(typeof(StringEnumConverter))]
        public virtual DAFAppTypes? AppType { get; set; }

        [DataMember]
        public virtual List<Application> DefaultApps { get; set; }

        [DataMember]
        public virtual Status DefaultAppsEnabled { get; set; }

        [DataMember]
        public virtual List<string> HostOptions { get; set; }

        [DataMember]
        public virtual bool Loading { get; set; }
    }

    [DataContract]
    public enum DAFAppTypes
    {
        [EnumMember]
        View,
        
        [EnumMember]
        API,
        
        [EnumMember]
        Redirect
    }
    
    [DataContract]
    public enum AddNewTypes
    {
        [EnumMember]
        None,
        
        [EnumMember]
        App,
    }
}
