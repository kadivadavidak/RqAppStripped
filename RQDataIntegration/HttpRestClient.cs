using System;
using System.Collections.Generic;
using System.Net;
using RestSharp;
using Newtonsoft.Json;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;

namespace RQDataIntegration
{
    internal class HttpRestClient
    {
        internal static Authentication AuthenticationInfo;

        internal static void GetAccessInformation(int divisionId)
        {
            if (AuthenticationInfo == null)
            {
                AuthenticationInfo = new Authentication();
            }
            AuthenticationInfo.SetToken(divisionId);
        }

        internal static String RequestReport(string reportName, string databaseTableName, string fileName, string startDate = "", string stopDate = "")  // reportName = [report or list]/reportOrListName
        {
            var count = 1;
            var division = AuthenticationInfo.Division;
            var data = new DataTable();
            var json = "";
            var dateParams = "";
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            if (reportName.Contains("?"))
            {
                reportName += "&";
            }
            else
            {
                reportName += "?";
            }

            if (startDate != "")
            {
                dateParams = $"StartDate={startDate}&StopDate={stopDate}&";
            }

            var preferredEnpoint = AuthenticationInfo.PreferredEndpoint ?? "https://rqdataconnect.iqmetrix.net";
            var companyPartial = "";
            if (AuthenticationInfo.CompanyId != 0) companyPartial = $"&CompanyID={AuthenticationInfo.CompanyId}";
            var httpRequest = $"{preferredEnpoint}/{reportName}{dateParams}Response=json&Auth={AuthenticationInfo.AuthenticationToken}{companyPartial}"; // todo: use jsondatatables
            var client = new RestClient(httpRequest);
            var request = new RestRequest(Method.GET);

            client.Timeout = 10800000; // 3 hour

            IRestResponse response = new RestResponse();

            var index = reportName.IndexOf("/", StringComparison.Ordinal);

            reportName = reportName.Substring(index + 1, reportName.IndexOf(@"?", StringComparison.Ordinal) - index - 1);

            Console.WriteLine($"Attempting to retrieve {reportName} data for {division}.");

            try
            {
                response = (RestResponse)client.Execute(request);
                Console.WriteLine($"Successfully received {reportName} response for {division}.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unable to retrieve {reportName} data for {division}.");
                SmtpHandler.SendMessage($"RQ API import error: Unable to retrieve {reportName}", $"Unable to retrieve {reportName} data for {division}.\n\n{e}");
            }

            if (response.StatusDescription != "OK" || response.Content == "" || response.Content == "<Table />")
            {
                while (count < 3 && (response.StatusDescription != "OK" || response.Content == "" || response.Content == "<Table />"))
                {
                    count++;
                    Console.WriteLine($"{response.StatusDescription}. Attempt {count} to pull {reportName} data for {division}.");
                    response = client.Execute(request);
                }
            }

            if (count == 3 && (response.StatusDescription != "OK" || response.Content == "" || response.Content == "<Table />"))
            {
                Console.WriteLine($"Could not pull {division} {reportName} data for the following reason: {response.ErrorMessage}. {response.StatusDescription}. {response.Content}.");
                SmtpHandler.SendMessage($"RQ API import could not pull {reportName}",
                    $"Could not pull {division} {reportName} data for the following reason: {response.ErrorMessage}. {response.StatusDescription}. {response.Content}.");
                return json;
            }

            Console.WriteLine($"Successfully pulled {reportName} data for {division} :D");

            json = response.Content;

            if (databaseTableName != "" || fileName != "")
            {
                data = DataHandler.JsonToDataTable(json, startDate);
            }

            if (databaseTableName != "")
            {
                DatabaseConnection.ReadDataToDatabase(data, databaseTableName);
            }
            if (fileName != "")
            {
                DataHandler.JsonToCsvFile(data, fileName);
            }

            Console.WriteLine($"Data processing for {division} {reportName} complete.\n");

            return json;
        }

        internal static void Execute(string objectToRun)
        {
            var divisionList = new List<int>();
            var startDate = DateTime.Today.AddDays(-1).ToUniversalTime().ToString("o"); //"2017-11-06T06:00:00.000Z"; 
            var stopDate = DateTime.Today.ToUniversalTime().ToString("o");
            //var startDate = $"{DateTime.Today.AddDays(-2):yyyy-MM-dd}T06:00:00.000Z";
            //var stopDate = $"{DateTime.Today:yyyy-MM-dd}T06:00:00.000Z";

            for (var i = 1; i <= Enum.GetValues(typeof(Divisions)).Length; i++)
            {
                divisionList.Add(i);
            }

            foreach (var division in divisionList)
            {
                GetAccessInformation(division);

                switch (objectToRun)
                {
                    case "Locations":
                        RequestReport("reports/locationmasterlistreport", "staging.RQLocations", "Location");
                        break;
                    case "Customers":
                        RequestReport(
                            $"reports/customerlistreport?DatePercision=True&TypeOfCustomer=-1&FilterBy=2&StoreIDLoggedIn=1&StartDate={startDate}&StopDate={stopDate}", "staging.RQCustomers", "Customer");
                        break;
                    case "CustomerDeltas":
                        RequestReport(
                            $"reports/customerlistreport?DatePercision=True&TypeOfCustomer=-1&FilterBy=2&StoreIDLoggedIn=1&StartDateLastModified={startDate}&StopDateLastModified={stopDate}&StartDate=1979-01-01T06:00:00.000Z&StopDate=2017-10-11T06:00:00.000Z", "staging.RQCustomers", "Customer");
                        break;
                    case "Employees":
                        RequestReport("reports/employeemasterlistreport?ForWho=1&ForWhoIDs=-1&EnabledStatus=2", "staging.RQEmployees", "Employee");
                        break;
                    case "Sales":
                        RequestReport("customizedreports/salesextendeddetailreport?DatePercision=True", "staging.RQSales", "SalesDetail", startDate, stopDate);
                        break;
                    case "PaymentIntegrationTransaction":
                        //if (new int[] { 1, 3 }.Contains(division))
                        RequestReport("reports/paymentintegrationtransactionbydatereport", "staging.RQPaymentIntegrationTransaction", "PaymentIntegrationTransaction", startDate, stopDate);
                        break;
                    case "AgedSerialized":
                        RequestReport(
                            "reports/AgedSeralizedInventoryReport?SearchMethod=1&SearchCriteria2=1&StoreIDLoggedIn=1&ForWho=1&ForWhoIDs=-1", "staging.API_RQAgedSerialized", "AgedSerialized");
                        break;
                    case "Inventory":
                        for (var i = 1; i < 4; i++) // Get each QtyStatuses at at time. Report is too big to get all at once.
                        {
                            RequestReport(
                                $"reports/inventorylistingreport?CategoryNumber=10&BinStatus=10&QtyStatus={i}&BlindInventory=0&DateAsOf={DateTime.Now.ToString("s")}Z", "staging.API_RQInventory", "Inventory");
                        }
                        break;
                    case "TaxCollected":
                        RequestReport(
                            "reports/TaxCollectedReport?ReportType=NetSales&TaxIDs=-1&ForWho=1&ForWhoIDs=-1&IncludeVendorRebates=True", "staging.RQTaxDetails", "TaxDetail", startDate, stopDate);
                        break;
                    case "ActivityTracking":
                        if (division == 1)
                            RequestReport(
                                $"reports/ActivityTrackingReport?UseDateCreatedSearchParameters=True&DateCreatedStartDate={startDate}&DateCreatedStopDate={stopDate}&UseDateClosedSearchParameters=False&DateClosedStartDate={startDate}&DateClosedStopDate={stopDate}&UseAssignedToSearchParameters=False&AssignedToType=1&AssignedToEmployeeIDs=-1&AssignedToGroupID=-1&UseCreatedBySearchParameters=False&CreatedByType=1&CreatedByEmployeeIDs=-1&CreatedByGroupID=-1&UseForWhoSearchParameters=True&ForWho=1&ForWhoIDs=-1&UseStatusTypeSearchParameters=True&StatusType=0&UseActivityTypeSearchParameters=False&ActivityTypeID=-1&UseContactNameSearchParameters=False",
                                "staging.RQActivityTracking", "ActivityTracking");
                        break;
                    case "Logging":
                        RequestReport("logging/requests", "staging.RequestLogs", "", startDate, DateTime.Now.ToString("o"));
                        break;
                    case "Types":
                        if (division == 1)
                        {
                            RequestReport("lists/cardtypes", "staging.RQCardTypes", "CardTypes");
                            RequestReport("lists/entrytypes", "staging.RQEntryTypes", "EntryTypes");
                            RequestReport("lists/requesttypes", "staging.RQRequestTypes", "RequestTypes");
                        }
                        break;
                    case "Products":
                        RequestReport("inventory/ProductMasterList?StoreIDLoggedIn=1&ProductType=0&SearchMethod=0&SearchCriteria=10&Enabled=2", "staging.rq.Sku", "SKUs");
                        break;
                    case "ProductIdentifier":
                        RequestReport("lists/productidentifier", "staging.rq.ProductIdentifier", "ProductIdentifier");
                        break;
                    case "CategoryNumber":
                        RequestReport("lists/categorynumber", "staging.rq.CategoryNumber", "CategoryNumber");
                        break;
                    case "GlAccount":
                        RequestReport("/Finance/GLAccountActivityReport/CompanyDetail?AccountID=-1&ForWho=1&ForWhoIDs=-1", "staging.rq.GlAccount", "GLAccount", startDate, stopDate);
                        break;
                    case "CashAudit":
                        RequestReport("reports/CashAuditTrailReport?TypeID=-1&ForWho=1&ForWhoID=-1", "staging.rq.CashAudit", "CashAudit", startDate, stopDate);
                        break;
                    case "Coupon":
                        RequestReport("lists/CouponID", "staging.rq.Coupon", "Coupon");
                        break;
                    case "CouponDetail":
                        RequestReport("reports/CouponSummaryReport?ForWho=1&ForWhoIDs=-1&ReportType=CouponDetail", "staging.rq.CouponDetail", "CouponCouponDetail", startDate, stopDate);
                        break;
                    default:
                        Console.WriteLine($"Unable to process request for {objectToRun}. Please select an existing API object to extract data for.");
                        break;
                }
            }
        }
    }
}