﻿using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace appsvc_fnc_dev_clientsecretexpiry_dotnet001
{
    internal class Auth
    {
        public class ROPCConfidentialTokenCredential : Azure.Core.TokenCredential
        {
            string _clientId;
            string _clientSecret;
            string _password;
            string _tenantId;
            string _tokenEndpoint;
            string _username;
            ILogger _log;

            // TEMP
            public ROPCConfidentialTokenCredential(string userName, string userSecretName, ILogger log)
            {
                IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();

                string keyVaultUrl = config["keyVaultUrl"];
                string secretName = config["secretName"];

                _clientId = config["clientId"];
                _tenantId = config["tenantId"];
                _log = log;
                _tokenEndpoint = "https://login.microsoftonline.com/" + _tenantId + "/oauth2/v2.0/token";
                _username = userName;

                SecretClientOptions options = new SecretClientOptions()
                {
                    Retry =
                {
                    Delay= TimeSpan.FromSeconds(2),
                    MaxDelay = TimeSpan.FromSeconds(16),
                    MaxRetries = 5,
                    Mode = RetryMode.Exponential
                 }
                };
                var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential(), options);

                KeyVaultSecret secret = client.GetSecret(secretName);
                _clientSecret = secret.Value;

                KeyVaultSecret password = client.GetSecret(userSecretName);
                _password = password.Value;
            }

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                HttpClient httpClient = new HttpClient();

                var Parameters = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("scope", string.Join(" ", requestContext.Scopes)),
                    new KeyValuePair<string, string>("username", _username),
                    new KeyValuePair<string, string>("password", _password),
                    new KeyValuePair<string, string>("grant_type", "password")
                };

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(Parameters)
                };

                var response = httpClient.SendAsync(request).Result.Content.ReadAsStringAsync().Result;
                dynamic responseJson = JsonConvert.DeserializeObject(response);
                var expirationDate = DateTimeOffset.UtcNow.AddMinutes(60.0);
                return new AccessToken(responseJson.access_token.ToString(), expirationDate);
            }

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                HttpClient httpClient = new HttpClient();

                var Parameters = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("scope", string.Join(" ", requestContext.Scopes)),
                    new KeyValuePair<string, string>("username", _username),
                    new KeyValuePair<string, string>("password", _password),
                    new KeyValuePair<string, string>("grant_type", "password")
                };

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(Parameters)
                };

                var response = httpClient.SendAsync(request).Result.Content.ReadAsStringAsync().Result;
                dynamic responseJson = JsonConvert.DeserializeObject(response);
                var expirationDate = DateTimeOffset.UtcNow.AddMinutes(60.0);
                return new ValueTask<AccessToken>(new AccessToken(responseJson.access_token.ToString(), expirationDate));
            }
        }
    }
}