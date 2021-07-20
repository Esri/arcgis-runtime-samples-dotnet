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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace ArcGISRuntime.Helpers
{
    internal static class ArcGISLoginPrompt
    {
        private const string ArcGISOnlineUrl = "https://www.arcgis.com/sharing/rest";

        // - The Client ID for an app registered with the server (the ID below is for a public app created by the ArcGIS Runtime team).
        private const string AppClientId = "lgAdHkYZYlwwfAhC";

        // - An optional client secret for the app (only needed for the OAuthClientCredentials authorization type).
        private const string ClientSecret = "";

        // - A URL for redirecting after a successful authorization (this must be a URL configured with the app).
        private const string OAuthRedirectUrl = "my-ags-app://auth";

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
                // Login failure
                MessageBox.Show("Login failed: " + ex.Message);
            }

            return loggedIn;
        }

        // ChallengeHandler function that will be called whenever access to a secured resource is attempted
        public static async Task<Credential> PromptCredentialAsync(CredentialRequestInfo info)
        {
            Credential credential = null;

            try
            {
                // IOAuthAuthorizeHandler will challenge the user for OAuth credentials
                credential = await AuthenticationManager.Current.GenerateCredentialAsync(info.ServiceUri);
            }
            catch (OperationCanceledException)
            {
                // OAuth login was canceled, no need to display error to user.
            }

            return credential;
        }

        public static void SetChallengeHandler()
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

            // Register the ArcGIS Online server information with the AuthenticationManager
            AuthenticationManager.Current.RegisterServer(portalServerInfo);

            // Use the OAuthAuthorize class in this project to create a new web view to show the login UI
            AuthenticationManager.Current.OAuthAuthorizeHandler = new OAuthAuthorize();

            // Create a new ChallengeHandler that uses a method in this class to challenge for credentials
            AuthenticationManager.Current.ChallengeHandler = new ChallengeHandler(PromptCredentialAsync);
        }

        #region OAuth handler

        // In a desktop (WPF) app, an IOAuthAuthorizeHandler component is used to handle some of the OAuth details. Specifically, it
        //     implements AuthorizeAsync to show the login UI (generated by the server that hosts secure content) in a web control.
        //     When the user logs in successfully, cancels the login, or closes the window without continuing, the IOAuthAuthorizeHandler
        //     is responsible for obtaining the authorization from the server or raising an OperationCanceledException.
        public class OAuthAuthorize : IOAuthAuthorizeHandler
        {
            // Window to contain the OAuth UI.
            private Window _authWindow;

            // Use a TaskCompletionSource to track the completion of the authorization.
            private TaskCompletionSource<IDictionary<string, string>> _tcs;

            // URL for the authorization callback result (the redirect URI configured for your application).
            private string _callbackUrl;

            // URL that handles the OAuth request.
            private string _authorizeUrl;

            // Function to handle authorization requests, takes the URIs for the secured service, the authorization endpoint, and the redirect URI.
            public Task<IDictionary<string, string>> AuthorizeAsync(Uri serviceUri, Uri authorizeUri, Uri callbackUri)
            {
                if (_tcs != null && !_tcs.Task.IsCompleted)
                    throw new Exception("Task in progress");

                _tcs = new TaskCompletionSource<IDictionary<string, string>>();

                // Store the authorization and redirect URLs.
                _authorizeUrl = authorizeUri.AbsoluteUri;
                _callbackUrl = callbackUri.AbsoluteUri;

                // Call a function to show the login controls, make sure it runs on the UI thread for this app.
                Dispatcher dispatcher = Application.Current.Dispatcher;
                if (dispatcher == null || dispatcher.CheckAccess())
                {
                    AuthorizeOnUIThread(_authorizeUrl);
                }
                else
                {
                    Action authorizeOnUIAction = () => AuthorizeOnUIThread(_authorizeUrl);
                    dispatcher.BeginInvoke(authorizeOnUIAction);
                }

                // Return the task associated with the TaskCompletionSource.
                return _tcs.Task;
            }

            // Challenge for OAuth credentials on the UI thread.
            private void AuthorizeOnUIThread(string authorizeUri)
            {
                // Create a WebBrowser control to display the authorize page.
                WebBrowser webBrowser = new WebBrowser();

                // Handle the navigation event for the browser to check for a response to the redirect URL.
                webBrowser.Navigating += WebBrowserOnNavigating;

                // Display the web browser in a new window.
                _authWindow = new Window
                {
                    Content = webBrowser,
                    Width = 430,
                    Height = 395,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                // Set the app's window as the owner of the browser window (if main window closes, so will the browser).
                if (Application.Current != null && Application.Current.MainWindow != null)
                {
                    _authWindow.Owner = Application.Current.MainWindow;
                }

                // Handle the window closed event then navigate to the authorize url.
                _authWindow.Closed += OnWindowClosed;
                webBrowser.Navigate(authorizeUri);

                // Display the window.
                _authWindow.ShowDialog();
            }

            // Handle the browser window closing.
            private void OnWindowClosed(object sender, EventArgs e)
            {
                // If the browser window closes, return the focus to the main window.
                if (_authWindow != null && _authWindow.Owner != null)
                {
                    _authWindow.Owner.Focus();
                }

                // If the task wasn't completed, the user must have closed the window without logging in.
                if (!_tcs.Task.IsCompleted)
                {
                    // Set the task completion source exception to indicate a canceled operation.
                    _tcs.SetCanceled();
                }

                _authWindow = null;
            }

            // Handle browser navigation (content changing).
            private void WebBrowserOnNavigating(object sender, NavigatingCancelEventArgs e)
            {
                // Check for a response to the callback url.
                const string portalApprovalMarker = "/oauth2/approval";
                WebBrowser webBrowser = sender as WebBrowser;

                Uri uri = e.Uri;

                // If no browser, uri, or an empty url, return.
                if (webBrowser == null || uri == null || string.IsNullOrEmpty(uri.AbsoluteUri))
                    return;

                // Check for redirect.
                bool isRedirected = uri.AbsoluteUri.StartsWith(_callbackUrl) ||
                    _callbackUrl.Contains(portalApprovalMarker) && uri.AbsoluteUri.Contains(portalApprovalMarker);

                // Check if browser was redirected to the callback URL. (This indicates succesful authentication.)
                if (isRedirected)
                {
                    e.Cancel = true;

                    // Call a helper function to decode the response parameters.
                    IDictionary<string, string> authResponse = DecodeParameters(uri);

                    // Set the result for the task completion source.
                    _tcs.SetResult(authResponse);

                    // Close the window.
                    if (_authWindow != null)
                    {
                        _authWindow.Close();
                    }
                }
            }

            private static IDictionary<string, string> DecodeParameters(Uri uri)
            {
                // Create a dictionary of key value pairs returned in an OAuth authorization response URI query string.
                string answer = "";

                // Get the values from the URI fragment or query string.
                if (!string.IsNullOrEmpty(uri.Fragment))
                {
                    answer = uri.Fragment.Substring(1);
                }
                else
                {
                    if (!string.IsNullOrEmpty(uri.Query))
                    {
                        answer = uri.Query.Substring(1);
                    }
                }

                // Parse parameters into key / value pairs.
                Dictionary<string, string> keyValueDictionary = new Dictionary<string, string>();
                string[] keysAndValues = answer.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string kvString in keysAndValues)
                {
                    string[] pair = kvString.Split('=');
                    string key = pair[0];
                    string value = string.Empty;
                    if (key.Length > 1)
                    {
                        value = Uri.UnescapeDataString(pair[1]);
                    }

                    keyValueDictionary.Add(key, value);
                }

                // Return the dictionary of string keys/values.
                return keyValueDictionary;
            }
        }

        #endregion OAuth handler
    }
}