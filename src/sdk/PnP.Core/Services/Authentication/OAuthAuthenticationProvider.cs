﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace PnP.Core.Services
{
    public class OAuthAuthenticationProvider : IAuthenticationProvider
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string tokenEndpoint = "https://login.microsoftonline.com/common/oauth2/token";

        // Microsoft SharePoint Online Management Shell client id
        // private static readonly string aadAppId = "9bc3ab49-b65d-410a-85ad-de819febfddc";
        // PnP Office 365 Management Shell 
        private const string defaultAADAppId = "31359c7f-bd7e-475c-86db-fdb8c937548e";

        private readonly ILogger log;
        private IAuthenticationProviderConfiguration configuration;

        // Token cache handling
        private static readonly SemaphoreSlim semaphoreSlimTokens = new SemaphoreSlim(1);
        private readonly ConcurrentDictionary<string, string> tokenCache = new ConcurrentDictionary<string, string>();

        public OAuthAuthenticationProvider(
            ILogger<OAuthAuthenticationProvider> logger)
        {
            log = logger;
        }

        public void Configure(IAuthenticationProviderConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task AuthenticateRequestAsync(Uri resource, HttpRequestMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            var (Username, Password) = GetCredential();

            if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("bearer", await EnsureSharePointAccessTokenAsync(resource, Username, Password).ConfigureAwait(false));
            }
        }

        private async Task<string> GetMicrosoftGraphAccessTokenAsync()
        {
            var (Username, Password) = GetCredential();
            return await EnsureMicrosoftGraphAccessTokenAsync(Username, Password).ConfigureAwait(false);
        }

        private async Task<string> GetSharePointOnlineAccessTokenAsync(Uri resource)
        {
            var (Username, Password) = GetCredential();
            return await EnsureSharePointAccessTokenAsync(resource, Username, Password).ConfigureAwait(false);
        }

        public async Task<string> GetAccessTokenAsync(Uri resource, string[] scopes)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            if (resource.AbsoluteUri.Equals(PnPConstants.MicrosoftGraphBaseUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                return await GetMicrosoftGraphAccessTokenAsync().ConfigureAwait(false);
            }
            else if (resource.AbsoluteUri.ToLower(CultureInfo.InvariantCulture).Contains("sharepoint.com"))
            {
                return await GetSharePointOnlineAccessTokenAsync(resource).ConfigureAwait(false);
            }
            else
            {
                return default(string);
            }
        }

        private async Task<string> AcquireTokenAsync(Uri resourceUri, string username, string password)
        {
            string resource = $"{resourceUri.Scheme}://{resourceUri.DnsSafeHost}";

            var clientId = this.configuration.ClientId ?? defaultAADAppId;
            var body = $"resource={resource}&client_id={clientId}&grant_type=password&username={HttpUtility.UrlEncode(username)}&password={HttpUtility.UrlEncode(password)}";
            using (var stringContent = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded"))
            {

                var result = await httpClient.PostAsync(tokenEndpoint, stringContent).ContinueWith((response) =>
                {
                    return response.Result.Content.ReadAsStringAsync().Result;
                }).ConfigureAwait(false);

                var tokenResult = JsonSerializer.Deserialize<JsonElement>(result);
                var token = tokenResult.GetProperty("access_token").GetString();
                return token;
            }
        }

        private async Task<string> EnsureSharePointAccessTokenAsync(Uri resourceUri, string userPrincipalName, string userPassword)
        {
            return await EnsureAccessTokenAsync(resourceUri, userPrincipalName, userPassword).ConfigureAwait(true);
        }

        private async Task<string> EnsureMicrosoftGraphAccessTokenAsync(string userPrincipalName, string userPassword)
        {
            Uri resourceUri = PnPConstants.MicrosoftGraphBaseUri;
            return await EnsureAccessTokenAsync(resourceUri, userPrincipalName, userPassword).ConfigureAwait(true);
        }

        private async Task<string> EnsureAccessTokenAsync(Uri resourceUri, string userPrincipalName, string userPassword)
        {
            string accessTokenFromCache = TokenFromCache(resourceUri, tokenCache);
            if (accessTokenFromCache == null)
            {
                await semaphoreSlimTokens.WaitAsync().ConfigureAwait(false);
                try
                {
                    // No async methods are allowed in a lock section
                    string accessToken = await AcquireTokenAsync(resourceUri, userPrincipalName, userPassword).ConfigureAwait(false);
                    log.LogInformation($"Successfully requested new access token resource {resourceUri.DnsSafeHost} and user {userPrincipalName}");
                    AddTokenToCache(resourceUri, tokenCache, accessToken);

                    // Spin up a thread to invalidate the access token once's it's expired
                    ThreadPool.QueueUserWorkItem(async (obj) =>
                    {
                        try
                        {
                            // Wait until we're 5 minutes before the planned token expiration
                            Thread.Sleep(CalculateThreadSleep(accessToken));
                            // Take a lock to ensure no other threads are updating the SharePoint Access token at this time
                            await semaphoreSlimTokens.WaitAsync().ConfigureAwait(false);
                            RemoveTokenFromCache(resourceUri, tokenCache);
                            log.LogInformation($"Cached token for resource {resourceUri.DnsSafeHost} and user {userPrincipalName} expired");
                        }
                        catch (Exception ex)
                        {
                            log.LogError(ex, $"Something went wrong during cache token invalidation: {ex.Message}");
                            RemoveTokenFromCache(resourceUri, tokenCache);
                        }
                        finally
                        {
                            semaphoreSlimTokens.Release();
                        }
                    });

                    return accessToken;

                }
                finally
                {
                    semaphoreSlimTokens.Release();
                }
            }
            else
            {
                log.LogInformation($"Returning token from cache for resource {resourceUri.DnsSafeHost} and user {userPrincipalName}");
                return accessTokenFromCache;
            }
        }

        private (string Username, string Password) GetCredential()
        {
            var username = string.Empty;
            var password = string.Empty;

            switch (configuration)
            {
                case OAuthCredentialManagerConfiguration credentialsManager:
                    // We're using a credential manager entry instead of a username/password set in the options
                    var credentials = CredentialManager.GetCredential(credentialsManager.CredentialManagerName);
                    username = credentials.UserName;
                    password = credentials.Password;
                    break;
                case OAuthUsernamePasswordConfiguration usernamePassword:
                    username = usernamePassword.Username;
                    password = usernamePassword.Password.ToInsecureString();
                    break;
                case OAuthCertificateConfiguration certificate:
                    // TODO: To implement ...
                    break;
            }

            return (username, password);
        }

        private static string TokenFromCache(Uri web, ConcurrentDictionary<string, string> tokenCache)
        {
            if (tokenCache.TryGetValue(web.DnsSafeHost, out string accessToken))
            {
                return accessToken;
            }

            return null;
        }

        private static void AddTokenToCache(Uri web, ConcurrentDictionary<string, string> tokenCache, string newAccessToken)
        {
            if (tokenCache.TryGetValue(web.DnsSafeHost, out string currentAccessToken))
            {
                tokenCache.TryUpdate(web.DnsSafeHost, newAccessToken, currentAccessToken);
            }
            else
            {
                tokenCache.TryAdd(web.DnsSafeHost, newAccessToken);
            }
        }

        private static void RemoveTokenFromCache(Uri web, ConcurrentDictionary<string, string> tokenCache)
        {
            tokenCache.TryRemove(web.DnsSafeHost, out string currentAccessToken);
        }

        private static TimeSpan CalculateThreadSleep(string accessToken)
        {
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(accessToken);
            var lease = GetAccessTokenLease(token.ValidTo);
            lease = TimeSpan.FromSeconds(lease.TotalSeconds - TimeSpan.FromMinutes(5).TotalSeconds > 0 ? lease.TotalSeconds - TimeSpan.FromMinutes(5).TotalSeconds : lease.TotalSeconds);
            return lease;
        }

        private static TimeSpan GetAccessTokenLease(DateTime expiresOn)
        {
            DateTime now = DateTime.UtcNow;
            DateTime expires = expiresOn.Kind == DateTimeKind.Utc ? expiresOn : TimeZoneInfo.ConvertTimeToUtc(expiresOn);
            TimeSpan lease = expires - now;
            return lease;
        }
    }
}
