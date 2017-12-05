using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using Newtonsoft.Json;
using System.Data;

namespace RQDataIntegration
{
    internal class Authentication
    {
        public string AuthenticationToken { get; set; }
        public string Division { get; set; }
        public int DivisionId { get; set; }
        public int CompanyId { get; set; }
        public string PreferredEndpoint { get; set; }
        private Dictionary<string, string> PreferredEndpoints { get; set; }
        private Dictionary<string, int> CompanyIDs { get; set; }

        internal void SetToken(int divisionId)
        {
            if (DivisionId == divisionId) return;

            var userName = ConfigurationManager.AppSettings["RqUserName"];
            var password = ConfigurationManager.AppSettings["RqPassword"];

            switch (divisionId)
            {
                case 1: // Spring
                    Division = Divisions.SpringCommunications.ToString();
                    break;
                case 2: // Cricket      
                    Division = Divisions.AioWireless2.ToString();
                    break;
                case 3: // SimplyMac
                    Division = Divisions.SimplyMac.ToString();
                    break;
                default:
                    Division = null;
                    break;
            }

            var authenticationPair = userName + ":" + password;
            var authenticationAscii = Encoding.ASCII.GetBytes(authenticationPair);
            var token = Convert.ToBase64String(authenticationAscii);

            AuthenticationToken = token;
            DivisionId = divisionId;

            if (PreferredEndpoints == null)
            {
                GetCompanyInfo();
            }

            if (PreferredEndpoints != null) if (Division != null) PreferredEndpoint = PreferredEndpoints[Division];
            if (CompanyIDs != null) if (Division != null) CompanyId = CompanyIDs[Division];
        }

        internal void GetCompanyInfo()
        {
            PreferredEndpoints = new Dictionary<string, string>();
            CompanyIDs = new Dictionary<string, int>();
            var companyInfo = HttpRestClient.RequestReport("master/relationships", "", "");
            var divisionList = Enum.GetNames(typeof(Divisions));

            if (PreferredEndpoints.Count != 0) return;

            var test = (DataTable)JsonConvert.DeserializeObject(companyInfo, typeof(DataTable));

            // TODO: beter way to do this???
            foreach (var division in divisionList)
            {
                var expression = "CompanyName = \'" + division + "\'";
                var t = test.Select(expression);
                PreferredEndpoints.Add(t[0]["CompanyName"].ToString(), t[0]["PreferredEndpoint"].ToString());
                CompanyIDs.Add(t[0]["CompanyName"].ToString(), Convert.ToInt32(t[0]["CompanyID"]));
            }
        }

        internal void AbandonSession()
        {
            HttpRestClient.RequestReport("session/abandon", "", "");
        }
    }
}
