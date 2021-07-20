// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Security;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UIKit;
using Xamarin.Essentials;

namespace ArcGISRuntime.Helpers
{
    internal static class ArcGISLoginPrompt
    {
        private const string ArcGISOnlineUrl = "https://www.arcgis.com/sharing/rest";

        // - The Client ID for an app registered with the server (the ID below is for a public app created by the ArcGIS Runtime team).
        public const string AppClientId = @"IBkBd7YYFHOzPIIO";

        // - An optional client secret for the app (only needed for the OAuthClientCredentials authorization type).
        private const string ClientSecret = "";

        // - A URL for redirecting after a successful authorization (this must be a URL configured with the app).
        // NOTE: to use a custom URL scheme like the one below, you need to add it to CFBundleURLSchemes in info.plist.
        // For example -
        //  <key>CFBundleURLSchemes</key>
        //  <array>
        //  	<string>my-ags-app</string>
        //  </array>
        private const string OAuthRedirectUrl = @"xamarin-ios-app://auth";

        public static async Task<bool> EnsureAGOLCredentialAsync()
        {
            bool loggedIn = false;

            try
            {
                // Create a challenge request for portal credentials (OAuth credential request for arcgis.com)
                CredentialRequestInfo challengeRequest = new CredentialRequestInfo
                {
                    // Use the OAuth authorization code workflow.
                    GenerateTokenOptions = new GenerateTokenOptions
                    {
                        TokenAuthenticationType = TokenAuthenticationType.OAuthAuthorizationCode
                    },

                    // Indicate the url (portal) to authenticate with (ArcGIS Online)
                    ServiceUri = new Uri(ArcGISOnlineUrl)
                };

                // Call GetCredentialAsync on the AuthenticationManager to invoke the challenge handler
                Credential cred = await AuthenticationManager.Current.GetCredentialAsync(challengeRequest, false);
                loggedIn = cred != null;
            }
            catch (OperationCanceledException)
            {
                // OAuth login was canceled, no need to display error to user.
            }
            catch (Exception ex)
            {
                new UIAlertView("Login failed", ex.Message, (IUIAlertViewDelegate)null, "OK", null).Show();
            }

            return loggedIn;
        }

        public static void SetChallengeHandler(UIViewController controller)
        {
            // Define the server information for ArcGIS Online
            ServerInfo portalServerInfo = new ServerInfo(new Uri(ArcGISOnlineUrl))
            {
                TokenAuthenticationType = TokenAuthenticationType.OAuthAuthorizationCode,
                OAuthClientInfo = new OAuthClientInfo(AppClientId, new Uri(OAuthRedirectUrl))
            };

            // If a client secret has been configured, set the authentication type to OAuth client credentials.
            if (!string.IsNullOrEmpty(ClientSecret))
            {
                // If a client secret is specified then use the TokenAuthenticationType.OAuthClientCredentials type.
                portalServerInfo.TokenAuthenticationType = TokenAuthenticationType.OAuthClientCredentials;
                portalServerInfo.OAuthClientInfo.ClientSecret = ClientSecret;
            }

            // Register this server with AuthenticationManager.
            AuthenticationManager.Current.RegisterServer(portalServerInfo);

            // Use a function in this class to challenge for credentials.
            AuthenticationManager.Current.ChallengeHandler = new ChallengeHandler(PromptCredentialAsync);

            // Set the OAuthAuthorizeHandler component (this class).
            AuthenticationManager.Current.OAuthAuthorizeHandler = new OAuthAuthorize(controller);
        }

        // ChallengeHandler function that will be called whenever access to a secured resource is attempted.
        public static async Task<Credential> PromptCredentialAsync(CredentialRequestInfo info)
        {
            Credential credential = null;

            try
            {
                // IOAuthAuthorizeHandler will challenge the user for OAuth credentials.
                credential = await AuthenticationManager.Current.GenerateCredentialAsync(info.ServiceUri);
            }
            // OAuth login was canceled, no need to display error to user.
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }

            return credential;
        }
    }

    public class OAuthAuthorize : IOAuthAuthorizeHandler
    {
        // Use a TaskCompletionSource to track the completion of the authorization.
        private TaskCompletionSource<IDictionary<string, string>> _taskCompletionSource;

        private UIViewController _viewController;

        public OAuthAuthorize(UIViewController activity)
        {
            _viewController = activity;
        }

        // IOAuthAuthorizeHandler.AuthorizeAsync implementation.
        public async Task<IDictionary<string, string>> AuthorizeAsync(Uri serviceUri, Uri authorizeUri, Uri callbackUri)
        {
            try
            {
                // Create a task completion source.
                _taskCompletionSource = new TaskCompletionSource<IDictionary<string, string>>();

                // Start the authentication on the UI thread.
                _viewController.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var result = await WebAuthenticator.AuthenticateAsync(authorizeUri, callbackUri);
                        _taskCompletionSource.SetResult(result.Properties);
                    }
                    catch (Exception ex)
                    {
                        _taskCompletionSource.TrySetException(ex);
                    }
                });
                return await _taskCompletionSource.Task;
            }
            catch (Exception) { }

            // Return null if anything goes wrong with authentication.
            return null;
        }
    }
}