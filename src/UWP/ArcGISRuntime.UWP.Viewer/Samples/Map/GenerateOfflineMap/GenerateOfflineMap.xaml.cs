// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Popups;

namespace ArcGISRuntime.UWP.Samples.GenerateOfflineMap
{
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Generate offline map",
        category: "Map",
        description: "Take a web map offline.",
        instructions: "When the app starts, you will be prompted to sign in using a free ArcGIS Online account. Once the map loads, zoom to the extent you want to take offline. The red border shows the extent that will be downloaded. Click the \"Take Map Offline\" button to start the offline map job. The progress bar will show the job's progress. When complete, the offline map will replace the online map in the map view.",
        tags: new[] { "download", "offline", "save", "web map" })]
    public partial class GenerateOfflineMap
    {
        // The job to generate an offline map.
        private GenerateOfflineMapJob _generateOfflineMapJob;

        // The extent of the data to take offline.
        private Envelope _areaOfInterest = new Envelope(-88.1541, 41.7690, -88.1471, 41.7720, SpatialReferences.Wgs84);

        // The ID for a web map item hosted on the server (water network map of Naperville IL).
        private string WebMapId = "acc027394bc84c2fb04d1ed317aac674";

        public GenerateOfflineMap()
        {
            InitializeComponent();

            // Load the web map, show area of interest, restrict map interaction, and set up authorization.
            Initialize();
        }

        private async void Initialize()
        {
            try
            {
                // Create the ArcGIS Online portal.
                ArcGISPortal portal = await ArcGISPortal.CreateAsync();

                // Get the Naperville water web map item using its ID.
                PortalItem webmapItem = await PortalItem.CreateAsync(portal, WebMapId);

                // Create a map from the web map item.
                Map onlineMap = new Map(webmapItem);

                // Display the map in the MapView.
                MyMapView.Map = onlineMap;

                // Disable user interactions on the map (no panning or zooming from the initial extent).
                MyMapView.InteractionOptions = new MapViewInteractionOptions
                {
                    IsEnabled = false
                };

                // Create a graphics overlay for the extent graphic and apply a renderer.
                SimpleLineSymbol aoiOutlineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Red, 3);
                GraphicsOverlay extentOverlay = new GraphicsOverlay
                {
                    Renderer = new SimpleRenderer(aoiOutlineSymbol)
                };
                MyMapView.GraphicsOverlays.Add(extentOverlay);

                // Add a graphic to show the area of interest (extent) that will be taken offline.
                Graphic aoiGraphic = new Graphic(_areaOfInterest);
                extentOverlay.Graphics.Add(aoiGraphic);

                // Hide the map loading progress indicator.
                loadingIndicator.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                // When the map view unloads, try to clean up existing output data folders.
                MyMapView.Unloaded += (s, e) =>
                {
                    // Find output mobile map folders in the temp directory.
                    string[] outputFolders = Directory.GetDirectories(Environment.ExpandEnvironmentVariables("%TEMP%"), "NapervilleWaterNetwork*");

                    // Loop through the folder names and delete them.
                    foreach (string dir in outputFolders)
                    {
                        try
                        {
                            // Delete the folder.
                            Directory.Delete(dir, true);
                        }
                        catch (Exception)
                        {
                            // Ignore exceptions (files might be locked, for example).
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                MessageDialog dialog = new MessageDialog(ex.ToString(), "Error loading map");
                await dialog.ShowAsync();
            }
        }

        private async void TakeMapOfflineButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Create a new folder for the output mobile map.
            string packagePath = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), @"NapervilleWaterNetwork");
            int num = 1;
            while (Directory.Exists(packagePath))
            {
                packagePath = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), @"NapervilleWaterNetwork" + num.ToString());
                num++;
            }

            // Create the output directory.
            Directory.CreateDirectory(packagePath);

            try
            {
                // Show the progress indicator while the job is running.
                busyIndicator.Visibility = Windows.UI.Xaml.Visibility.Visible;

                // Create an offline map task with the current (online) map.
                OfflineMapTask takeMapOfflineTask = await OfflineMapTask.CreateAsync(MyMapView.Map);

                // Create the default parameters for the task, pass in the area of interest.
                GenerateOfflineMapParameters parameters = await takeMapOfflineTask.CreateDefaultGenerateOfflineMapParametersAsync(_areaOfInterest);

                // Create the job with the parameters and output location.
                _generateOfflineMapJob = takeMapOfflineTask.GenerateOfflineMap(parameters, packagePath);

                // Handle the progress changed event for the job.
                _generateOfflineMapJob.ProgressChanged += OfflineMapJob_ProgressChanged;

                // Await the job to generate geodatabases, export tile packages, and create the mobile map package.
                GenerateOfflineMapResult results = await _generateOfflineMapJob.GetResultAsync();

                // Check for job failure (writing the output was denied, e.g.).
                if (_generateOfflineMapJob.Status != JobStatus.Succeeded)
                {
                    MessageDialog dialog = new MessageDialog("Generate offline map package failed.", "Job status");
                    await dialog.ShowAsync();
                    busyIndicator.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }

                // Check for errors with individual layers.
                if (results.LayerErrors.Any())
                {
                    // Build a string to show all layer errors.
                    System.Text.StringBuilder errorBuilder = new System.Text.StringBuilder();
                    foreach (KeyValuePair<Layer, Exception> layerError in results.LayerErrors)
                    {
                        errorBuilder.AppendLine(string.Format("{0} : {1}", layerError.Key.Id, layerError.Value.Message));
                    }

                    // Show layer errors.
                    string errorText = errorBuilder.ToString();
                    MessageDialog dialog = new MessageDialog(errorText, "Layer errors");
                    await dialog.ShowAsync();
                }

                // Display the offline map.
                MyMapView.Map = results.OfflineMap;

                // Apply the original viewpoint for the offline map.
                MyMapView.SetViewpoint(new Viewpoint(_areaOfInterest));

                // Enable map interaction so the user can explore the offline data.
                MyMapView.InteractionOptions.IsEnabled = true;

                // Hide the "Take map offline" button.
                takeOfflineArea.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                // Show a message that the map is offline.
                messageArea.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            catch (TaskCanceledException)
            {
                // Generate offline map task was canceled.
                MessageDialog dialog = new MessageDialog("Taking map offline was canceled");
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                // Exception while taking the map offline.
                MessageDialog dialog = new MessageDialog(ex.Message, "Offline map error");
                await dialog.ShowAsync();
            }
            finally
            {
                // Hide the activity indicator when the job is done.
                busyIndicator.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        // Show changes in job progress.
        private async void OfflineMapJob_ProgressChanged(object sender, EventArgs e)
        {
            // Get the job.
            GenerateOfflineMapJob job = sender as GenerateOfflineMapJob;

            // Dispatch to the UI thread.
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
           {
               // Show the percent complete and update the progress bar.
               Percentage.Text = job.Progress > 0 ? job.Progress.ToString() + " %" : string.Empty;
               progressBar.Value = job.Progress;
           });
        }

        private void CancelJobButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // The user canceled the job.
            _generateOfflineMapJob.Cancel();
        }
    }
}