// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using Android.App;
using Android.OS;
using Android.Widget;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ArcGISRuntime.Samples.GenerateOfflineMap
{
    [Activity(ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Generate offline map",
        category: "Map",
        description: "Take a web map offline.",
        instructions: "When the app starts, you will be prompted to sign in using a free ArcGIS Online account. Once the map loads, zoom to the extent you want to take offline. The red border shows the extent that will be downloaded. Tap the \"Take Map Offline\" button to start the offline map job. The progress bar will show the job's progress. When complete, the offline map will replace the online map in the map view.",
        tags: new[] { "download", "offline", "save", "web map" })]
    public class GenerateOfflineMap : Activity
    {
        // Mapview.
        private MapView _mapView;

        // Generate Button.
        private Button _takeMapOfflineButton;

        // Progress indicator.
        private AlertDialog _alertDialog;
        private ProgressBar _progressIndicator;

        // The job to generate an offline map.
        private GenerateOfflineMapJob _generateOfflineMapJob;

        // The extent of the data to take offline.
        private Envelope _areaOfInterest = new Envelope(-88.1541, 41.7690, -88.1471, 41.7720, SpatialReferences.Wgs84);

        // The ID for a web map item hosted on the server (water network map of Naperville IL).
        private string WebMapId = "acc027394bc84c2fb04d1ed317aac674";

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            Title = "Generate offline map";

            // Create the UI, setup the control references and execute initialization.
            CreateLayout();
            Initialize();
        }

        private void CreateLayout()
        {
            // Create the layout.
            LinearLayout layout = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };

            // Add the generate button.
            _takeMapOfflineButton = new Button(this)
            {
                Text = "Take map offline"
            };
            _takeMapOfflineButton.Click += TakeMapOfflineButton_Click;
            layout.AddView(_takeMapOfflineButton);

            // Add the mapview.
            _mapView = new MapView(this);
            layout.AddView(_mapView);

            // Add the layout to the view.
            SetContentView(layout);

            // Create the progress dialog display.
            _progressIndicator = new ProgressBar(this);
            _progressIndicator.SetProgress(40, true);
            AlertDialog.Builder builder = new AlertDialog.Builder(this).SetView(_progressIndicator);
            builder.SetCancelable(true);
            builder.SetMessage("Generating offline map ...");
            _alertDialog = builder.Create();
            _alertDialog.SetButton("Cancel", (s, e) =>
            {
                _generateOfflineMapJob.Cancel();
            });
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
                _mapView.Map = onlineMap;

                // Disable user interactions on the map (no panning or zooming from the initial extent).
                _mapView.InteractionOptions = new MapViewInteractionOptions
                {
                    IsEnabled = false
                };

                // Create a graphics overlay for the extent graphic and apply a renderer.
                SimpleLineSymbol aoiOutlineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Red, 3);
                GraphicsOverlay extentOverlay = new GraphicsOverlay
                {
                    Renderer = new SimpleRenderer(aoiOutlineSymbol)
                };
                _mapView.GraphicsOverlays.Add(extentOverlay);

                // Add a graphic to show the area of interest (extent) that will be taken offline.
                Graphic aoiGraphic = new Graphic(_areaOfInterest);
                extentOverlay.Graphics.Add(aoiGraphic);
            }
            catch (Exception ex)
            {
                // Show the exception message to the user.
                ShowStatusMessage(ex.Message);
            }
        }

        private async void TakeMapOfflineButton_Click(object sender, EventArgs e)
        {
            // Create a path for the output mobile map.
            string tempPath = $"{Path.GetTempPath()}";
            string[] outputFolders = Directory.GetDirectories(tempPath, "NapervilleWaterNetwork*");

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

            // Create a new folder for the output mobile map.
            string packagePath = Path.Combine(tempPath, @"NapervilleWaterNetwork");
            int num = 1;
            while (Directory.Exists(packagePath))
            {
                packagePath = Path.Combine(tempPath, @"NapervilleWaterNetwork" + num.ToString());
                num++;
            }

            // Create the output directory.
            Directory.CreateDirectory(packagePath);

            try
            {
                // Show the progress dialog while the job is running.
                _alertDialog.Show();

                // Create an offline map task with the current (online) map.
                OfflineMapTask takeMapOfflineTask = await OfflineMapTask.CreateAsync(_mapView.Map);

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
                    // Report failure to the user.
                    ShowStatusMessage("Failed to take the map offline.");
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
                    ShowStatusMessage(errorBuilder.ToString());
                }

                // Display the offline map.
                _mapView.Map = results.OfflineMap;

                // Apply the original viewpoint for the offline map.
                _mapView.SetViewpoint(new Viewpoint(_areaOfInterest));

                // Enable map interaction so the user can explore the offline data.
                _mapView.InteractionOptions.IsEnabled = true;

                // Change the title and disable the "Take map offline" button.
                _takeMapOfflineButton.Text = "Map is offline";
                _takeMapOfflineButton.Enabled = false;
            }
            catch (TaskCanceledException)
            {
                // Generate offline map task was canceled.
                ShowStatusMessage("Taking map offline was canceled");
            }
            catch (Exception ex)
            {
                // Exception while taking the map offline.
                ShowStatusMessage(ex.Message);
            }
            finally
            {
                // Hide the loading overlay when the job is done.
                _alertDialog.Dismiss();
            }
        }

        private void ShowStatusMessage(string message)
        {
            // Display the message to the user.
            AlertDialog.Builder builder = new AlertDialog.Builder(this);
            builder.SetMessage(message).SetTitle("Alert").Show();
        }

        // Show changes in job progress.
        private void OfflineMapJob_ProgressChanged(object sender, EventArgs e)
        {
            // Get the job.
            GenerateOfflineMapJob job = sender as GenerateOfflineMapJob;

            // Dispatch to the UI thread.
            RunOnUiThread(() =>
            {
                // Show the percent complete and update the progress bar.
                string percentText = job.Progress > 0 ? job.Progress.ToString() + " %" : string.Empty;
                _progressIndicator.Progress = job.Progress;
                _alertDialog.SetMessage($"Taking map offline ({percentText}) ...");
            });
        }
    }
}