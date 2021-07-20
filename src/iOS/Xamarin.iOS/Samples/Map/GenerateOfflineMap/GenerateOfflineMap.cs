// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an 
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific 
// language governing permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Foundation;
using UIKit;
using Xamarin.Auth;

namespace ArcGISRuntime.Samples.GenerateOfflineMap
{
    [Register("GenerateOfflineMap")]
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Generate offline map",
        category: "Map",
        description: "Take a web map offline.",
        instructions: "When the app starts, you will be prompted to sign in using a free ArcGIS Online account. Once the map loads, zoom to the extent you want to take offline. The red border shows the extent that will be downloaded. Tap the \"Take Map Offline\" button to start the offline map job. The progress bar will show the job's progress. When complete, the offline map will replace the online map in the map view.",
        tags: new[] { "download", "offline", "save", "web map" })]
    public class GenerateOfflineMap : UIViewController
    {
        // Hold references to UI controls.
        private MapView _myMapView;
        private UIActivityIndicatorView _loadingIndicator;
        private UIBarButtonItem _takeMapOfflineButton;
        private UILabel _statusLabel;
        private OAuth2Authenticator _auth;

        // The job to generate an offline map.
        private GenerateOfflineMapJob _generateOfflineMapJob;

        // The extent of the data to take offline.
        private readonly Envelope _areaOfInterest = new Envelope(-88.1541, 41.7690, -88.1471, 41.7720, SpatialReferences.Wgs84);

        // The ID for a web map item hosted on the server (water network map of Naperville IL).
        private const string WebMapId = "acc027394bc84c2fb04d1ed317aac674";

        public GenerateOfflineMap()
        {
            Title = "Generate offline map";
        }

        private async void Initialize()
        {
            try
            {
                // Start the loading indicator.
                _loadingIndicator.StartAnimating();

                // Create the ArcGIS Online portal.
                ArcGISPortal portal = await ArcGISPortal.CreateAsync();

                // Get the Naperville water web map item using its ID.
                PortalItem webmapItem = await PortalItem.CreateAsync(portal, WebMapId);

                // Create a map from the web map item.
                Map onlineMap = new Map(webmapItem);

                // Display the map in the MapView.
                _myMapView.Map = onlineMap;

                // Disable user interactions on the map (no panning or zooming from the initial extent).
                _myMapView.InteractionOptions = new MapViewInteractionOptions
                {
                    IsEnabled = false
                };

                // Create a graphics overlay for the extent graphic and apply a renderer.
                SimpleLineSymbol aoiOutlineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Red, 3);
                GraphicsOverlay extentOverlay = new GraphicsOverlay
                {
                    Renderer = new SimpleRenderer(aoiOutlineSymbol)
                };
                _myMapView.GraphicsOverlays.Add(extentOverlay);

                // Add a graphic to show the area of interest (extent) that will be taken offline.
                Graphic aoiGraphic = new Graphic(_areaOfInterest);
                extentOverlay.Graphics.Add(aoiGraphic);

                // Hide the map loading progress indicator.
                _loadingIndicator.StopAnimating();
            }
            catch (Exception ex)
            {
                // Show the exception message to the user.
                UIAlertController messageAlert = UIAlertController.Create("Error", ex.Message, UIAlertControllerStyle.Alert);
                messageAlert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                PresentViewController(messageAlert, true, null);
            }
        }

        private async void TakeMapOfflineButton_Click(object sender, EventArgs e)
        {
            // Show the loading indicator.
            _loadingIndicator.StartAnimating();

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
                packagePath = Path.Combine(tempPath, @"NapervilleWaterNetwork" + num);
                num++;
            }

            // Create the output directory.
            Directory.CreateDirectory(packagePath);

            try
            {
                // Show the loading overlay while the job is running.
                _statusLabel.Text = "Taking map offline...";

                // Create an offline map task with the current (online) map.
                OfflineMapTask takeMapOfflineTask = await OfflineMapTask.CreateAsync(_myMapView.Map);

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
                    UIAlertController messageAlert = UIAlertController.Create("Error", "Failed to take the map offline.", UIAlertControllerStyle.Alert);
                    messageAlert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    PresentViewController(messageAlert, true, null);
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
                    UIAlertController messageAlert = UIAlertController.Create("Error", errorBuilder.ToString(), UIAlertControllerStyle.Alert);
                    messageAlert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    PresentViewController(messageAlert, true, null);
                }

                // Display the offline map.
                _myMapView.Map = results.OfflineMap;

                // Apply the original viewpoint for the offline map.
                _myMapView.SetViewpoint(new Viewpoint(_areaOfInterest));

                // Enable map interaction so the user can explore the offline data.
                _myMapView.InteractionOptions.IsEnabled = true;

                // Change the title and disable the "Take map offline" button.
                _statusLabel.Text = "Map is offline";
                _takeMapOfflineButton.Enabled = false;
            }
            catch (TaskCanceledException)
            {
                // Generate offline map task was canceled.
                UIAlertController messageAlert = UIAlertController.Create("Canceled", "Taking map offline was canceled", UIAlertControllerStyle.Alert);
                messageAlert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                PresentViewController(messageAlert, true, null);
            }
            catch (Exception ex)
            {
                // Exception while taking the map offline.
                UIAlertController messageAlert = UIAlertController.Create("Error", ex.Message, UIAlertControllerStyle.Alert);
                messageAlert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                PresentViewController(messageAlert, true, null);
            }
            finally
            {
                // Hide the loading overlay when the job is done.
                _loadingIndicator.StopAnimating();
            }
        }

        // Show changes in job progress.
        private void OfflineMapJob_ProgressChanged(object sender, EventArgs e)
        {
            // Get the job.
            GenerateOfflineMapJob job = sender as GenerateOfflineMapJob;

            // Dispatch to the UI thread.
            InvokeOnMainThread(() =>
            {
                // Show the percent complete and update the progress bar.
                _statusLabel.Text = $"Taking map offline ({job.Progress}%) ...";
            });
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            Initialize();
        }

        public override void LoadView()
        {
            // Create the views.
            View = new UIView {BackgroundColor = ApplicationTheme.BackgroundColor};

            _myMapView = new MapView();
            _myMapView.TranslatesAutoresizingMaskIntoConstraints = false;

            _takeMapOfflineButton = new UIBarButtonItem();
            _takeMapOfflineButton.Title = "Generate offline map";

            UIToolbar toolbar = new UIToolbar();
            toolbar.TranslatesAutoresizingMaskIntoConstraints = false;
            toolbar.Items = new[]
            {
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                _takeMapOfflineButton,
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace)
            };

            _statusLabel = new UILabel
            {
                Text = "Use the button to take the map offline.",
                AdjustsFontSizeToFitWidth = true,
                TextAlignment = UITextAlignment.Center,
                BackgroundColor = UIColor.FromWhiteAlpha(0, .6f),
                TextColor = UIColor.White,
                Lines = 1,
                TranslatesAutoresizingMaskIntoConstraints = false
            };

            _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.WhiteLarge);
            _loadingIndicator.TranslatesAutoresizingMaskIntoConstraints = false;
            _loadingIndicator.BackgroundColor = UIColor.FromWhiteAlpha(0, .6f);

            // Add the views.
            View.AddSubviews(_myMapView, toolbar, _loadingIndicator, _statusLabel);

            // Lay out the views.
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _myMapView.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
                _myMapView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _myMapView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                _myMapView.BottomAnchor.ConstraintEqualTo(toolbar.TopAnchor),

                toolbar.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                toolbar.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                toolbar.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor),

                _statusLabel.TopAnchor.ConstraintEqualTo(_myMapView.TopAnchor),
                _statusLabel.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _statusLabel.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                _statusLabel.HeightAnchor.ConstraintEqualTo(40),

                _loadingIndicator.TopAnchor.ConstraintEqualTo(_statusLabel.BottomAnchor),
                _loadingIndicator.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
                _loadingIndicator.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _loadingIndicator.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor)
            });
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);

            // Subscribe to events.
            if (_generateOfflineMapJob != null) _generateOfflineMapJob.ProgressChanged += OfflineMapJob_ProgressChanged;
            _takeMapOfflineButton.Clicked += TakeMapOfflineButton_Click;
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);

            // Unsubscribe from events, per best practice.
            _generateOfflineMapJob.ProgressChanged -= OfflineMapJob_ProgressChanged;
            _takeMapOfflineButton.Clicked -= TakeMapOfflineButton_Click;
        }
    }
}