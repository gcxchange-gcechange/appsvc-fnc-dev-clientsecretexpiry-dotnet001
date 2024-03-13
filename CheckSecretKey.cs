using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace appsvc_fnc_dev_clientsecretexpiry_dotnet001
{
    public class CheckSecretKey
    {
        // Runs at 07:00 on Sunday
        [FunctionName("Function1")]
        public void Run([TimerTrigger("0 0 7 * * 0")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}