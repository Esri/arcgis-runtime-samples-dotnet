// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime;
using ArcGISRuntime.Samples.Managers;
using ArcGISRuntime.Samples.Shared.Managers;
using ArcGISRuntime.Samples.Shared.Models;
using Esri.ArcGISRuntime.Security;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Forms.Helpers
{
    internal static class SampleLoader
    {
        private static List<string> _namedUserSamples = new List<string> {
            "AuthorMap",
            "SearchPortalMaps",
            "OAuth" };

        // Used to load a sample from the search or list in a category.
        public static async Task LoadSample(SampleInfo sampleInfo, NavigableElement nav)
        {
            try
            {
                // Restore API key if leaving named user sample.
                if (_namedUserSamples.Contains(SampleManager.Current?.SelectedSample?.FormalName))
                {
                    ApiKeyManager.EnableKey();
                }

                // Remove API key if opening named user sample.
                if (_namedUserSamples.Contains(sampleInfo.FormalName))
                {
                    ApiKeyManager.DisableKey();
                }

                // Clear any existing credentials from AuthenticationManager.
                foreach (Credential cred in AuthenticationManager.Current.Credentials)
                {
                    AuthenticationManager.Current.RemoveCredential(cred);
                }

                // Clear the challenge handler.
                AuthenticationManager.Current.ChallengeHandler = null;

                // Load offline data before showing the sample.
                if (sampleInfo.OfflineDataItems != null)
                {
                    CancellationTokenSource cancellationSource = new CancellationTokenSource();

                    // Show the wait page.
                    await nav.Navigation.PushModalAsync(new WaitPage(cancellationSource) { Title = sampleInfo.SampleName }, false);

#if WINDOWS_UWP
                    // Workaround for bug with Xamarin Forms UWP.
                    await Task.WhenAll(
                        Task.Delay(100),
                        DataManager.EnsureSampleDataPresent(sampleInfo, cancellationSource.Token)
                        );
#else
                    // Wait for the sample data download.
                    await DataManager.EnsureSampleDataPresent(sampleInfo, cancellationSource.Token);
#endif

                    // Remove the waiting page.
                    await nav.Navigation.PopModalAsync(false);
                }

                // Get the sample control from the selected sample.
                ContentPage sampleControl = (ContentPage)SampleManager.Current.SampleToControl(sampleInfo);

                // Create the sample display page to show the sample and the metadata.
                SamplePage page = new SamplePage(sampleControl, sampleInfo);

                // Show the sample.
                await nav.Navigation.PushAsync(page, true);
            }
            catch (OperationCanceledException)
            {
                // Remove the waiting page.
                await nav.Navigation.PopModalAsync(false);

                await Application.Current.MainPage.DisplayAlert("", "Download cancelled", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading sample: {ex.Message}");
            }
        }
    }
}