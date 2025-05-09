using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Newtonsoft.Json;
using static appsvc_fnc_dev_clientsecretexpiry_dotnet001.Auth;
using Microsoft.Graph.Models;
using System.Text;
using Microsoft.Kiota.Abstractions;
using Microsoft.Azure.Functions.Worker;

namespace appsvc_fnc_dev_clientsecretexpiry_dotnet001
{
    public class PasswordCredential
    {
        public string DisplayName;
        public DateTime EndDateTime;
    }

    public class Application
    {
        public string Id;
        public string DisplayName;
        public List<PasswordCredential> PasswordCredentials = new List<PasswordCredential>();

        public Application(string id, string displayName, PasswordCredential passwordCredential)
        {
            Id = id;
            DisplayName = displayName;
            PasswordCredentials.Add(passwordCredential);
        }
    }

    public enum RiskLevel
    {
        Critical = 0,
        Expired = 1,
        Warning = 2
    }

    public class CheckSecretKey
    {
        private readonly ILogger<CheckSecretKey> log;

        public CheckSecretKey(ILogger<CheckSecretKey> logger)
        {
            log = logger;
        }
        
        // Runs at 07:00 on Sunday
        [Function("CheckSecretKey")]
        public async Task Run([TimerTrigger("0 0 7 * * 0")]TimerInfo myTimer)
        {
            log.LogInformation($"C# Timer trigger function CheckSecretKey began execution at: {DateTime.Now}");

            IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();

            List<Application> applications = new List<Application>();
            List<Application> applicationsExpired = new List<Application>();
            List<Application> applicationsCritical = new List<Application>();
            List<Application> applicationsWarning = new List<Application>();

            DateTime currentDateTime = DateTime.Now;

            var graphClient = graphAuth(log);

            try
            {
                var apps = await graphClient.Applications.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "Id", "displayName", "PasswordCredentials" };
                });

                foreach (var app in apps.Value)
                {
                    applications.Add(JsonConvert.DeserializeObject<Application>(JsonConvert.SerializeObject(app)));
                }

                // check nextlink for more data from the collection
                while (apps.OdataNextLink != null)
                {
                    var nextPageRequestInformation = new RequestInformation
                    {
                        HttpMethod = Method.GET,
                        UrlTemplate = apps.OdataNextLink
                    };

                    apps = await graphClient.RequestAdapter.SendAsync(nextPageRequestInformation, (parseNode) => new ApplicationCollectionResponse());

                    foreach (var app in apps.Value)
                    {
                        applications.Add(JsonConvert.DeserializeObject<Application>(JsonConvert.SerializeObject(app)));
                    }
                }

                log.LogInformation($"applications.Count: {applications.Count}");

                applications = applications.OrderBy(o => o.DisplayName).ToList();

                foreach (Application application in applications)
                {
                    foreach (var credential in application.PasswordCredentials)
                    {
                        int dateDifference = (credential.EndDateTime - currentDateTime).Days; // (cred.EndDateTime - currentDateTime).TotalDays; // DateTime.Compare(cred.EndDateTime, currentDateTime);

                        if (dateDifference < 0)
                        {
                            Application app = applicationsExpired.Find(Application => Application.Id == application.Id);
                            if (app == null)
                                applicationsExpired.Add(new Application(application.Id, application.DisplayName, credential));
                            else
                                app.PasswordCredentials.Add(credential);
                        }
                        else if (dateDifference < 14)
                        {
                            Application app = applicationsCritical.Find(Application => Application.Id == application.Id);
                            if (app == null)
                                applicationsCritical.Add(new Application(application.Id, application.DisplayName, credential));
                            else
                                app.PasswordCredentials.Add(credential);
                        }
                        else if (dateDifference >= 14 && dateDifference < 30)
                        {
                            Application app = applicationsCritical.Find(Application => Application.Id == application.Id);
                            if (app == null)
                                applicationsWarning.Add(new Application(application.Id, application.DisplayName, credential));
                            else
                                app.PasswordCredentials.Add(credential);
                        }
                        else if (dateDifference > 30)
                        {
                            // do nothing
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.LogError($"Message: {e.Message}");
                if (e.InnerException is not null) log.LogError($"InnerException: {e.InnerException.Message}");
                log.LogError($"StackTrace: {e.StackTrace}");
            }

            await SendEmailNotification(applicationsExpired, applicationsCritical, applicationsWarning, log);

            log.LogInformation($"C# Timer trigger function CheckSecretKey finished execution at: {DateTime.Now}");
        }

        public static async Task SendEmailNotification(List<Application> applicationsExpired, List<Application> applicationsCritical, List<Application> applicationsWarning, ILogger log)
        {
            IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();
            string emailUserId = config["emailUserId"];
            string emailUserName = config["emailUserName"];
            string emailUserSecret = config["emailUserSecret"];
            string recipientAddress = config["recipientAddress"];

            StringBuilder content = new StringBuilder();

            ROPCConfidentialTokenCredential auth = new ROPCConfidentialTokenCredential(emailUserName, emailUserSecret, log);
            var graphClient = new GraphServiceClient(auth);

            content.AppendLine(GetFormattedContent(applicationsExpired, "secrets are expired", RiskLevel.Expired));
            content.AppendLine(GetFormattedContent(applicationsCritical, "secrets are set to expire in less than 14 days", RiskLevel.Critical));
            content.AppendLine(GetFormattedContent(applicationsWarning, "secrets are set to expire between 14 and 30 days", RiskLevel.Warning));

            var msg = new Message
            {
                Subject = "Client secret expiry notification report",
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = content.ToString()
                },
                ToRecipients = new List<Recipient>()
            };

            foreach (string address in recipientAddress.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries))
            {
                msg.ToRecipients.Add(new Recipient { EmailAddress = new EmailAddress { Address = address.Trim() } });
            }

            Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody body = new()
            {
                Message = msg,
                SaveToSentItems = false
            };

            try
            {
                await graphClient.Users[emailUserId].SendMail.PostAsync(body);
            }
            catch (Exception e)
            {
                log.LogError($"Message: {e.Message}");
                if (e.InnerException is not null) log.LogError($"InnerException: {e.InnerException.Message}");
                log.LogError($"StackTrace: {e.StackTrace}");
            }
        }

        private static string GetFormattedContent(List<Application> applications, string contentTitle, RiskLevel riskLevel)
        {
            StringBuilder content = new StringBuilder();
            int secretCount;

            secretCount = 0;
            foreach (Application application in applications)
            {
                application.PasswordCredentials = application.PasswordCredentials.OrderBy(o => o.DisplayName).ToList();

                for (int i = 0; i < application.PasswordCredentials.Count; i++)
                {
                    PasswordCredential credential = application.PasswordCredentials[i];

                    secretCount = secretCount + 1;

                    if (i == 0)
                    {
                        content.AppendFormat("<strong>{0}</strong><br />", application.DisplayName);
                        content.AppendLine("<table style=\"border: 1px solid black; width: 100%\">");
                        content.AppendLine("<tr><td style=\"width: 50%\"><strong>Secret Name</strong></td><td style=\"width: 50%\"><strong>Expiry Date</strong></td></tr>");
                    }

                    content.AppendLine("<tr>");
                    content.AppendFormat("<td>{0}</td>", credential.DisplayName);
                    content.AppendFormat("<td>{0}</td>", credential.EndDateTime);
                    content.AppendLine("</tr>");
                    if (i == (application.PasswordCredentials.Count - 1))
                    {
                        content.AppendLine("</table><br />");
                    }
                }
            }

            return string.Concat($"<p style=\"color: {((riskLevel == RiskLevel.Warning) ? "#000000" : "#ff0000")};\"><strong>{secretCount} {contentTitle}.</strong></p>", content.ToString());
        }
    }
}