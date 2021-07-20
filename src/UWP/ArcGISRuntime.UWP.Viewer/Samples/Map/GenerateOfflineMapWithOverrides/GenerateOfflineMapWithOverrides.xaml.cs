// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Data;
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
using Windows.UI.Xaml.Controls;

namespace ArcGISRuntime.UWP.Samples.GenerateOfflineMapWithOverrides
{
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Generate offline map (overrides)",
        category: "Map",
        description: "Take a web map offline with additional options for each layer.",
        instructions: "Modify the overrides parameters:",
        tags: new[] { "LOD", "adjust", "download", "extent", "filter", "offline", "override", "parameters", "reduce", "scale range", "setting" })]
    public partial class GenerateOfflineMapWithOverrides
    {
        // The job to generate an offline map.
        private GenerateOfflineMapJob _generateOfflineMapJob;

        // The extent of the data to take offline.
        private Envelope _areaOfInterest = new Envelope(-88.1541, 41.7690, -88.1471, 41.7720, SpatialReferences.Wgs84);

        // The ID for a web map item hosted on the server (water network map of Naperville IL).
        private const string WebMapId = "acc027394bc84c2fb04d1ed317aac674";

        public GenerateOfflineMapWithOverrides()
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
                LoadingIndicator.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                // Clean up any existing output data folders that might exist from running this sample previously.
                // The output data folder is where the results of the taking the web map offline get stored on the device.
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
                BusyIndicator.Visibility = Windows.UI.Xaml.Visibility.Visible;

                // Create an offline map task with the current (online) map.
                OfflineMapTask takeMapOfflineTask = await OfflineMapTask.CreateAsync(MyMapView.Map);

                // Create the default parameters for the task, pass in the area of interest.
                GenerateOfflineMapParameters parameters = await takeMapOfflineTask.CreateDefaultGenerateOfflineMapParametersAsync(_areaOfInterest);

                #region overrides

                // Generate parameter overrides for more in-depth control of the job.
                GenerateOfflineMapParameterOverrides overrides = await takeMapOfflineTask.CreateGenerateOfflineMapParameterOverridesAsync(parameters);

                // Configure the overrides using helper methods.
                ConfigureTileLayerOverrides(overrides);
                ConfigureLayerExclusion(overrides);
                CropWaterPipes(overrides);
                ApplyFeatureFilter(overrides);

                // Create the job with the parameters and output location.
                _generateOfflineMapJob = takeMapOfflineTask.GenerateOfflineMap(parameters, packagePath, overrides);

                #endregion overrides

                // Handle the progress changed event for the job.
                _generateOfflineMapJob.ProgressChanged += OfflineMapJob_ProgressChanged;

                // Await the job to generate geodatabases, export tile packages, and create the mobile map package.
                GenerateOfflineMapResult results = await _generateOfflineMapJob.GetResultAsync();

                // Check for job failure (writing the output was denied, e.g.).
                if (_generateOfflineMapJob.Status != JobStatus.Succeeded)
                {
                    MessageDialog dialog = new MessageDialog("Generate offline map package failed.", "Job status");
                    await dialog.ShowAsync();
                    BusyIndicator.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
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
                TakeOfflineArea.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                // Show a message that the map is offline.
                MessageArea.Visibility = Windows.UI.Xaml.Visibility.Visible;
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
                BusyIndicator.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        #region overrides

        private void ConfigureTileLayerOverrides(GenerateOfflineMapParameterOverrides overrides)
        {
            // Create a parameter key for the first basemap layer.
            OfflineMapParametersKey basemapKey = new OfflineMapParametersKey(MyMapView.Map.Basemap.BaseLayers.First());

            // Get the export tile cache parameters for the layer key.
            ExportTileCacheParameters basemapParams = overrides.ExportTileCacheParameters[basemapKey];

            // Clear the existing level IDs.
            basemapParams.LevelIds.Clear();

            // Get the min and max scale from the UI.
            int minScale = (int)MinScaleEntry.Value;
            int maxScale = (int)MaxScaleEntry.Value;

            // Re-add selected scales.
            for (int i = minScale; i < maxScale; i++)
            {
                basemapParams.LevelIds.Add(i);
            }

            // Expand the area of interest based on the specified buffer distance.
            int bufferDistance = (int)ExtentBufferEntry.Value;
            basemapParams.AreaOfInterest = GeometryEngine.BufferGeodetic(_areaOfInterest, bufferDistance, LinearUnits.Meters);
        }

        private void ConfigureLayerExclusion(GenerateOfflineMapParameterOverrides overrides)
        {
            // Apply layer exclusions as specified in the UI.
            if (ServiceConnCheckbox.IsChecked == false)
            {
                ExcludeLayerByName("Service Connection", overrides);
            }

            if (SysValvesLayerCheckbox.IsChecked == false)
            {
                ExcludeLayerByName("System Valve", overrides);
            }
        }

        private void CropWaterPipes(GenerateOfflineMapParameterOverrides overrides)
        {
            if (CropLayerCheckbox.IsChecked == true)
            {
                // Get the ID of the water pipes layer.
                long targetLayerId = GetServiceLayerId(GetLayerByName("Main"));

                // For each layer option.
                foreach (GenerateLayerOption layerOption in GetAllLayerOptions(overrides))
                {
                    // If the option's LayerId matches the selected layer's ID.
                    if (layerOption.LayerId == targetLayerId)
                    {
                        layerOption.UseGeometry = true;
                    }
                }
            }
        }

        private void ApplyFeatureFilter(GenerateOfflineMapParameterOverrides overrides)
        {
            // For each layer option.
            foreach (GenerateLayerOption option in GetAllLayerOptions(overrides))
            {
                // If the option's LayerId matches the selected layer's ID.
                if (option.LayerId == GetServiceLayerId(GetLayerByName("Hydrant")))
                {
                    // Apply the where clause.
                    option.WhereClause = "FLOW >= " + (int)FlowRateFilterEntry.Value;

                    // Configure the option to use the where clause.
                    option.QueryOption = GenerateLayerQueryOption.UseFilter;
                }
            }
        }

        private IList<GenerateLayerOption> GetAllLayerOptions(GenerateOfflineMapParameterOverrides overrides)
        {
            // Find the first feature layer.
            FeatureLayer targetLayer = MyMapView.Map.OperationalLayers.OfType<FeatureLayer>().First();

            // Get the key for the layer.
            OfflineMapParametersKey layerKey = new OfflineMapParametersKey(targetLayer);

            // Use that key to get the generate options for the layer.
            GenerateGeodatabaseParameters generateParams = overrides.GenerateGeodatabaseParameters[layerKey];

            // Return the layer options.
            return generateParams.LayerOptions;
        }

        private void ExcludeLayerByName(string layerName, GenerateOfflineMapParameterOverrides overrides)
        {
            // Get the feature layer with the specified name.
            FeatureLayer targetLayer = GetLayerByName(layerName);

            // Get the layer's ID.
            long targetLayerId = GetServiceLayerId(targetLayer);

            // Create a layer key for the selected layer.
            OfflineMapParametersKey layerKey = new OfflineMapParametersKey(targetLayer);

            // Get the parameters for the layer.
            GenerateGeodatabaseParameters generateParams = overrides.GenerateGeodatabaseParameters[layerKey];

            // Get the layer options for the layer.
            IList<GenerateLayerOption> layerOptions = generateParams.LayerOptions;

            // Find the layer option matching the ID.
            GenerateLayerOption targetLayerOption = layerOptions.First(layer => layer.LayerId == targetLayerId);

            // Remove the layer option.
            layerOptions.Remove(targetLayerOption);
        }

        private FeatureLayer GetLayerByName(string layerName)
        {
            // Get the first map in the operational layers collection that is a feature layer with name matching layerName
            return MyMapView.Map.OperationalLayers.OfType<FeatureLayer>().First(layer => layer.Name == layerName);
        }

        private long GetServiceLayerId(FeatureLayer layer)
        {
            // Find the service feature table for the layer; this assumes the layer is backed by a service feature table.
            ServiceFeatureTable serviceTable = (ServiceFeatureTable)layer.FeatureTable;

            // Return the layer ID.
            return serviceTable.LayerInfo.ServiceLayerId;
        }

        #endregion overrides

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
               ProgressBar.Value = job.Progress;
           });
        }

        private void CancelJobButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // The user canceled the job.
            _generateOfflineMapJob.Cancel();
        }
    }
}