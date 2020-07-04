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

namespace LCU.State.API.NapkinIDE.ApplicationManagement.State
{
    [Serializable]
    [DataContract]
    public class DataAppsManagementState
    {
        [DataMember]
        public virtual List<string> AccessRightOptions { get; set; }

        [DataMember]
        public virtual string ActiveAppPathGroup { get; set; }
        
        [DataMember]
        public virtual string ActiveDAFApp { get; set; }
        
        [DataMember]
        public virtual List<DataAppDetails> Applications { get; set; }
        
        [DataMember]
        public virtual int CurrentApplicationTab { get; set; }
        
        [DataMember]
        public virtual List<DataDAFAppDetails> DAFApplications { get; set; }
        
        [DataMember]
        public Dictionary<string, string> DAFAppOptions { get; set; }

        [DataMember]
        public virtual bool Loading { get; set; }
    }
    
    [Serializable]
    [DataContract]
    public class DataAppDetails
    {
        [DataMember]
        public virtual Dictionary<Guid, string> AppIDs { get; set; }
        
        [DataMember]
        public virtual List<DAFAppStatus> AppStati { get; set; }
        
        [DataMember]
        public virtual string PathGroup { get; set; }
    }
    
    [Serializable]
    [DataContract]
    public class DataDAFAppDetails
    {
        [DataMember]
        public virtual string Description { get; set; }
        
        [DataMember]
        public virtual Guid ID { get; set; }
        
        [DataMember]
        public virtual string Name { get; set; }
        
        [DataMember]
        public virtual string Path { get; set; }
        
        [DataMember]
        public virtual int Priority { get; set; }
    }
    
    [Serializable]
    [DataContract]
    public class DAFAppStatus : Status
    {
        [DataMember]
        public virtual int AppCount { get; set; }
        
        [DataMember]
        public virtual LCUIcon Icon { get; set; }
        
        [DataMember]
        public virtual string Name { get; set; }
    }
}
