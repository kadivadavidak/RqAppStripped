using RestSharp;

namespace RQDataIntegration
{
    class SessionHandling
    {
        public string AccessToken { get; set; }
        public bool IsValid { get; set; }
        public int ParentEntityId { get; set; }
        public int RqEmployeeId { get; set; }

        internal static void GetSessionInfo()
        {
            var token = HttpRestClient.AuthenticationInfo.AuthenticationToken;

            var request = new RestRequest(Method.GET);

            var httpRequest = $"https://rqdataconnect.iqmetrix.net/session?Auth={token}";

            var client = new RestClient(httpRequest);

            // todo: finish implementation
            var response = client.Execute(request);
        }
        internal static void AbandonSession()
        {
            //var token = AuthenticationInfo.

            var request = new RestRequest(Method.GET);

            var httpRequest = $"https://rqdataconnect.iqmetrix.net/session/abandon";

            request.AddHeader("Authorization", $"Basic {HttpRestClient.AuthenticationInfo}");

            var client = new RestClient(httpRequest);

            // todo: finish implementation
            var response = client.Execute(request);
        }
    }
}
