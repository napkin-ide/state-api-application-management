using Fathym.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace state_api_application_management_tests
{
    [TestClass]
    public class ToggleAppAsDefaultTests : AzFunctionTestBase
    {
        
        public ToggleAppAsDefaultTests() : base()
        {
            APIRoute = "api/ToggleAppAsDefault";                
        }

        [TestMethod]
        public async Task TestToggleAppAsDefault()
        {
            LcuentLookup = "";            
            PrincipalId = "";

            addRequestHeaders();

            var url = $"{HostURL}/{APIRoute}";            

            var response = await httpGet(url); 

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            var model = getContent<dynamic>(response);

            dynamic result = model.Result;            

            throw new NotImplementedException("Implement me!");                  
        }
    }
}
