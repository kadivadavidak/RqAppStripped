using System.Net;
using System.Net.Mail;

namespace RQDataIntegration
{
    class SmtpHandler
    {
        internal static void SendMessage(string subject, string body)
        {
            var emailAddress = "";

#if DEBUG
            emailAddress = "david.kirschman@springmobile.com";
#else
            emailAddress = "tbsc.reportingteam@springmobile.com";
#endif

            var email = new MailMessage("noreply@springmobile.com", emailAddress)
            {
                Subject = subject,
                Body = body
            };

            using(var client = new SmtpClient())
            {
                client.Port = 25;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = false;
                client.Host = "";
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential("",
                    "");

                client.Send(email);
            }
        }
    }
}
