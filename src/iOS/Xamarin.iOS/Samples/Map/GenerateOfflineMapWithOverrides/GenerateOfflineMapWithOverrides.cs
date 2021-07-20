// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime;
using CoreGraphics;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Foundation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UIKit;
using Xamarin.Auth;

namespace ArcGISRuntimeXamarin.Samples.GenerateOfflineMapWithOverrides
{
    [Register("GenerateOfflineMapWithOverrides")]
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Generate offline map (overrides)",
        category: "Map",
        description: "Take a web map offline with additional options for each layer.",
        instructions: "Modify the overrides parameters:",
        tags: new[] { "LOD", "adjust", "download", "extent", "filter", "offline", "override", "parameters", "reduce", "scale range", "setting" })]
    public class GenerateOfflineMapWithOverrides : UIViewController
    {
        // Hold references to UI controls.
        private MapView _myMapView;
        private UIActivityIndicatorView _loadingIndicator;
        private UIBarButtonItem _takeMapOfflineButton;
        private UILabel _statusLabel;
        private ConfigureOverridesViewController _overridesVC;
        private OAuth2Authenticator _auth;

        // Class-scope variables needed because job continues after configuration by separate class.
        private OfflineMapTask _takeMapOfflineTask;
        private GenerateOfflineMapParameters _parameters;
        private GenerateOfflineMapParameterOverrides _overrides;
        private string _packagePath;

        // The job to generate an offline map.
        private GenerateOfflineMapJob _generateOfflineMapJob;

        // The extent of the data to take offline.
        private readonly Envelope _areaOfInterest = new Envelope(-88.1541, 41.7690, -88.1471, 41.7720, SpatialReferences.Wgs84);

        // The ID for a web map item hosted on the server (water network map of Naperville IL).
        private const string WebMapId = "acc027394bc84c2fb04d1ed317aac674";

        public GenerateOfflineMapWithOverrides()
        {
            Title = "Generate offline map (overrides)";
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

        private void ShowConfigurationWindow(GenerateOfflineMapParameterOverrides overrides)
        {
            if (_overridesVC == null)
            {
                _overridesVC = new ConfigureOverridesViewController(overrides, _myMapView.Map);
            }

            // Show the layer list popover. Note: most behavior is managed by the table view & its source. See MapViewModel.
            var controller = new UINavigationController(_overridesVC);
            controller.Title = "Override parameters";
            // Show a close button in the top right.
            var closeButton = new UIBarButtonItem("Close", UIBarButtonItemStyle.Plain, (o, ea) => controller.DismissViewController(true, null));
            controller.NavigationBar.Items[0].SetRightBarButtonItem(closeButton, false);
            // Show the table view in a popover.
            controller.ModalPresentationStyle = UIModalPresentationStyle.Popover;
            controller.PreferredContentSize = new CGSize(300, 250);
            UIPopoverPresentationController pc = controller.PopoverPresentationController;
            if (pc != null)
            {
                pc.BarButtonItem = (UIBarButtonItem)_takeMapOfflineButton;
                pc.PermittedArrowDirections = UIPopoverArrowDirection.Down;
                pc.Delegate = new ppDelegate();
            }

            PresentViewController(controller, true, null);
        }

        private async void TakeMapOfflineButton_Click(object sender, EventArgs e)
        {
            // Disable the button to prevent errors.
            _takeMapOfflineButton.Enabled = false;

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
                catch (Exception ex)
                {
                    // Ignore exceptions (files might be locked, for example).
                    Debug.WriteLine(ex);
                }
            }

            // Create a new folder for the output mobile map.
            _packagePath = Path.Combine(tempPath, @"NapervilleWaterNetwork");
            int num = 1;
            while (Directory.Exists(_packagePath))
            {
                _packagePath = Path.Combine(tempPath, @"NapervilleWaterNetwork" + num);
                num++;
            }

            // Create the output directory.
            Directory.CreateDirectory(_packagePath);

            try
            {
                // Show the loading overlay while the job is running.
                _statusLabel.Text = "Taking map offline...";

                // Create an offline map task with the current (online) map.
                _takeMapOfflineTask = await OfflineMapTask.CreateAsync(_myMapView.Map);

                // Create the default parameters for the task, pass in the area of interest.
                _parameters = await _takeMapOfflineTask.CreateDefaultGenerateOfflineMapParametersAsync(_areaOfInterest);

                // Generate parameter overrides for more in-depth control of the job.
                _overrides = await _takeMapOfflineTask.CreateGenerateOfflineMapParameterOverridesAsync(_parameters);

                // Show the configuration window.
                ShowConfigurationWindow(_overrides);

                // Finish work once the user has configured the override.
                _overridesVC.FinishedConfiguring += ConfigurationContinuation;
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

        private async void ConfigurationContinuation()
        {
            // Hide the configuration UI.
            _overridesVC.DismissViewController(true, null);

            // Create the job with the parameters and output location.
            _generateOfflineMapJob = _takeMapOfflineTask.GenerateOfflineMap(_parameters, _packagePath, _overrides);

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
                    errorBuilder.AppendLine($"{layerError.Key.Id} : {layerError.Value.Message}");
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
            View = new UIView { BackgroundColor = ApplicationTheme.BackgroundColor };

            _myMapView = new MapView();
            _myMapView.TranslatesAutoresizingMaskIntoConstraints = false;

            _takeMapOfflineButton = new UIBarButtonItem();
            _takeMapOfflineButton.Title = "Generate offline map";

            UIToolbar toolbar = new UIToolbar();
            toolbar.TranslatesAutoresizingMaskIntoConstraints = false;
            toolbar.Items = new[]
            {
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                _takeMapOfflineButton
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
            _takeMapOfflineButton.Clicked += TakeMapOfflineButton_Click;

            if (_overridesVC != null) _overridesVC.FinishedConfiguring += ConfigurationContinuation;
            if (_generateOfflineMapJob != null) _generateOfflineMapJob.ProgressChanged += OfflineMapJob_ProgressChanged;
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);

            // Unsubscribe from events, per best practice.
            if (_generateOfflineMapJob != null) _generateOfflineMapJob.ProgressChanged -= OfflineMapJob_ProgressChanged;
            if (_overridesVC != null) _overridesVC.FinishedConfiguring -= ConfigurationContinuation;
            _takeMapOfflineButton.Clicked -= TakeMapOfflineButton_Click;
        }

        // Force popover to display on iPhone.
        private class ppDelegate : UIPopoverPresentationControllerDelegate
        {
            public override UIModalPresentationStyle GetAdaptivePresentationStyle(
                UIPresentationController forPresentationController) => UIModalPresentationStyle.None;

            public override UIModalPresentationStyle GetAdaptivePresentationStyle(UIPresentationController controller,
                UITraitCollection traitCollection) => UIModalPresentationStyle.None;
        }
    }

    public class ConfigureOverridesViewController : UIViewController
    {
        // Hold references to the overrides and the map.
        private GenerateOfflineMapParameterOverrides _overrides;
        private Map _map;
        private UIButton _takeOfflineButton;
        private readonly Envelope _areaOfInterest = new Envelope(-88.1541, 41.7690, -88.1471, 41.7720, SpatialReferences.Wgs84);

        // Hold state from UI selections.
        private int _minScale = 0;
        private int _maxScale = 23;
        private int _bufferExtent = 0;
        private int _flowRate = 500;
        private bool _includeServiceConn;
        private bool _includeSystemValues;
        private bool _cropWaterPipes;

        public ConfigureOverridesViewController(GenerateOfflineMapParameterOverrides overrides, Map map)
        {
            _overrides = overrides;
            _map = map;
            Title = "Parameter overrides";
        }

        private void ConfigureOverrides()
        {
            ConfigureTileLayerOverrides();
            ConfigureLayerExclusion();
            CropWaterPipes();
            ApplyFeatureFilter();
        }

        #region overrides

        private void ConfigureTileLayerOverrides()
        {
            // Create a parameter key for the first basemap layer.
            OfflineMapParametersKey basemapKey = new OfflineMapParametersKey(_map.Basemap.BaseLayers.First());

            // Get the export tile cache parameters for the layer key.
            ExportTileCacheParameters basemapParams = _overrides.ExportTileCacheParameters[basemapKey];

            // Clear the existing level IDs.
            basemapParams.LevelIds.Clear();

            // Re-add selected scales.
            for (int i = _minScale; i < _maxScale; i++)
            {
                basemapParams.LevelIds.Add(i);
            }

            // Expand the area of interest based on the specified buffer distance.
            basemapParams.AreaOfInterest = GeometryEngine.BufferGeodetic(_areaOfInterest, _bufferExtent, LinearUnits.Meters);
        }

        private void ConfigureLayerExclusion()
        {
            // Apply layer exclusions as specified in the UI.
            if (!_includeServiceConn)
            {
                ExcludeLayerByName("Service Connection");
            }

            if (!_includeSystemValues)
            {
                ExcludeLayerByName("System Valve");
            }
        }

        private void CropWaterPipes()
        {
            if (_cropWaterPipes)
            {
                // Get the ID of the water pipes layer.
                long targetLayerId = GetServiceLayerId(GetLayerByName("Main"));

                // For each layer option.
                foreach (GenerateLayerOption layerOption in GetAllLayerOptions())
                {
                    // If the option's LayerId matches the selected layer's ID.
                    if (layerOption.LayerId == targetLayerId)
                    {
                        layerOption.UseGeometry = true;
                    }
                }
            }
        }

        private void ApplyFeatureFilter()
        {
            // For each layer option.
            foreach (GenerateLayerOption option in GetAllLayerOptions())
            {
                // If the option's LayerId matches the selected layer's ID.
                if (option.LayerId == GetServiceLayerId(GetLayerByName("Hydrant")))
                {
                    // Apply the where clause.
                    option.WhereClause = $"FLOW >= {_flowRate}";

                    // Configure the option to use the where clause.
                    option.QueryOption = GenerateLayerQueryOption.UseFilter;
                }
            }
        }

        private IList<GenerateLayerOption> GetAllLayerOptions()
        {
            // Find the first feature layer.
            FeatureLayer targetLayer = _map.OperationalLayers.OfType<FeatureLayer>().First();

            // Get the key for the layer.
            OfflineMapParametersKey layerKey = new OfflineMapParametersKey(targetLayer);

            // Use that key to get the generate options for the layer.
            GenerateGeodatabaseParameters generateParams = _overrides.GenerateGeodatabaseParameters[layerKey];

            // Return the layer options.
            return generateParams.LayerOptions;
        }

        private void ExcludeLayerByName(string layerName)
        {
            // Get the feature layer with the specified name.
            FeatureLayer targetLayer = GetLayerByName(layerName);

            // Get the layer's ID.
            long targetLayerId = GetServiceLayerId(targetLayer);

            // Create a layer key for the selected layer.
            OfflineMapParametersKey layerKey = new OfflineMapParametersKey(targetLayer);

            // Get the parameters for the layer.
            GenerateGeodatabaseParameters generateParams = _overrides.GenerateGeodatabaseParameters[layerKey];

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
            return _map.OperationalLayers.OfType<FeatureLayer>().First(layer => layer.Name == layerName);
        }

        private long GetServiceLayerId(FeatureLayer layer)
        {
            // Find the service feature table for the layer; this assumes the layer is backed by a service feature table.
            ServiceFeatureTable serviceTable = (ServiceFeatureTable)layer.FeatureTable;

            // Return the layer ID.
            return serviceTable.LayerInfo.ServiceLayerId;
        }

        #endregion overrides

        public override void ViewWillDisappear(bool animated)
        {
            // This is called when the popover closes for any reason.
            ConfigureOverrides();
            FinishedConfiguring?.Invoke();
            base.ViewWillDisappear(animated);
        }

        public override void LoadView()
        {
            // Create the views.
            View = new UIView { BackgroundColor = ApplicationTheme.BackgroundColor };

            UIScrollView outerScroller = new UIScrollView();
            outerScroller.TranslatesAutoresizingMaskIntoConstraints = false;

            UIStackView outerStackView = new UIStackView();
            outerStackView.TranslatesAutoresizingMaskIntoConstraints = false;
            outerStackView.Axis = UILayoutConstraintAxis.Horizontal;
            outerStackView.Alignment = UIStackViewAlignment.Center;
            outerStackView.Distribution = UIStackViewDistribution.Fill;

            UIStackView innerStackView = new UIStackView();
            innerStackView.TranslatesAutoresizingMaskIntoConstraints = false;
            innerStackView.Axis = UILayoutConstraintAxis.Vertical;
            innerStackView.Alignment = UIStackViewAlignment.Fill;
            innerStackView.Spacing = 5;

            outerStackView.AddArrangedSubview(innerStackView);

            innerStackView.AddArrangedSubview(getLabel("Configure basemap"));

            innerStackView.AddArrangedSubview(getSliderRow("Min scale: ", 0, 23, _minScale, "", (sender, args) => { _minScale = (int)((UISlider)sender).Value; }));

            innerStackView.AddArrangedSubview(getSliderRow("Max scale: ", 0, 23, _maxScale, "", (sender, args) => { _maxScale = (int)((UISlider)sender).Value; }));

            innerStackView.AddArrangedSubview(getSliderRow("Buffer dist.: ", 0, 500, _bufferExtent, "m", (sender, args) => { _bufferExtent = (int)((UISlider)sender).Value; }));

            innerStackView.AddArrangedSubview(getLabel("Include layers"));

            innerStackView.AddArrangedSubview(getCheckRow("System valves: ", (sender, args) => { _includeSystemValues = !_includeSystemValues; }));

            innerStackView.AddArrangedSubview(getCheckRow("Service connections: ", (sender, args) => { _includeServiceConn = !_includeServiceConn; }));

            innerStackView.AddArrangedSubview(getLabel("Filter feature layer"));

            innerStackView.AddArrangedSubview(getSliderRow("Min. flow: ", 0, 1000, _flowRate, "", (sender, args) => { _flowRate = (int)((UISlider)sender).Value; }));

            innerStackView.AddArrangedSubview(getLabel("Crop layer to extent"));

            innerStackView.AddArrangedSubview(getCheckRow("Water pipes: ", (sender, args) => _cropWaterPipes = !_cropWaterPipes));

            _takeOfflineButton = new UIButton();
            _takeOfflineButton.TranslatesAutoresizingMaskIntoConstraints = false;
            _takeOfflineButton.SetTitle("Take map offline", UIControlState.Normal);
            _takeOfflineButton.SetTitleColor(View.TintColor, UIControlState.Normal);
            innerStackView.AddArrangedSubview(_takeOfflineButton);

            // Add the views.
            View.AddSubview(outerScroller);
            outerScroller.AddSubview(outerStackView);

            // Lay out the views.
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                outerScroller.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
                outerScroller.LeadingAnchor.ConstraintEqualTo(View.LayoutMarginsGuide.LeadingAnchor),
                outerScroller.TrailingAnchor.ConstraintEqualTo(View.LayoutMarginsGuide.TrailingAnchor),
                outerScroller.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor),
                outerStackView.LeadingAnchor.ConstraintEqualTo(outerScroller.LeadingAnchor),
                outerStackView.TrailingAnchor.ConstraintEqualTo(outerScroller.TrailingAnchor),
                outerStackView.TopAnchor.ConstraintEqualTo(outerScroller.TopAnchor),
                outerStackView.BottomAnchor.ConstraintEqualTo(outerScroller.BottomAnchor),
                outerStackView.WidthAnchor.ConstraintEqualTo(outerScroller.WidthAnchor)
            });
        }

        private void TakeOffline_Click(Object sender, EventArgs e) => DismissViewController(true, null);

        private UILabel getLabel(string text)
        {
            UILabel label = new UILabel();
            label.TranslatesAutoresizingMaskIntoConstraints = false;
            label.Text = text;
            label.Font = UIFont.BoldSystemFontOfSize(16);

            return label;
        }

        private UIStackView getSliderRow(string label, int min, int max, int startingValue, string units, EventHandler sliderChangeAction)
        {
            UIStackView rowView = new UIStackView();
            rowView.TranslatesAutoresizingMaskIntoConstraints = false;
            rowView.Axis = UILayoutConstraintAxis.Horizontal;
            rowView.Alignment = UIStackViewAlignment.Center;
            rowView.Distribution = UIStackViewDistribution.Fill;
            rowView.Spacing = 5;

            UILabel descriptionLabel = new UILabel();
            descriptionLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            descriptionLabel.Text = label;
            descriptionLabel.WidthAnchor.ConstraintGreaterThanOrEqualTo(140).Active = true;
            descriptionLabel.SetContentCompressionResistancePriority((float)UILayoutPriority.Required, UILayoutConstraintAxis.Horizontal);
            rowView.AddArrangedSubview(descriptionLabel);

            UILabel valueLabel = new UILabel();
            valueLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            valueLabel.Text = $"{startingValue}{units}";
            valueLabel.WidthAnchor.ConstraintEqualTo(60).Active = true;

            UISlider sliderView = new UISlider();
            sliderView.TranslatesAutoresizingMaskIntoConstraints = false;
            sliderView.MinValue = min;
            sliderView.MaxValue = max;
            sliderView.Value = startingValue;
            sliderView.WidthAnchor.ConstraintGreaterThanOrEqualTo(100).Active = true;
            sliderView.SetContentCompressionResistancePriority((float)UILayoutPriority.DefaultLow, UILayoutConstraintAxis.Horizontal);
            sliderView.ValueChanged += sliderChangeAction;
            sliderView.ValueChanged += (sender, args) => { valueLabel.Text = $"{(int)sliderView.Value}{units}"; };
            rowView.AddArrangedSubview(sliderView);

            rowView.AddArrangedSubview(valueLabel);

            return rowView;
        }

        private UIStackView getCheckRow(string label, EventHandler checkboxChecked)
        {
            UIStackView rowView = new UIStackView();
            rowView.TranslatesAutoresizingMaskIntoConstraints = false;
            rowView.Axis = UILayoutConstraintAxis.Horizontal;
            rowView.Alignment = UIStackViewAlignment.Center;
            rowView.Distribution = UIStackViewDistribution.Fill;
            rowView.Spacing = 5;
            rowView.LayoutMarginsRelativeArrangement = true;
            rowView.LayoutMargins = new UIEdgeInsets(0, 0, 0, 5);

            UILabel descriptionLabel = new UILabel();
            descriptionLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            descriptionLabel.Text = label;
            rowView.AddArrangedSubview(descriptionLabel);

            UISwitch valueSwitch = new UISwitch();
            valueSwitch.TranslatesAutoresizingMaskIntoConstraints = false;
            valueSwitch.ValueChanged += checkboxChecked;
            rowView.AddArrangedSubview(valueSwitch);

            return rowView;
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);

            // Subscribe to events.
            _takeOfflineButton.TouchUpInside += TakeOffline_Click;
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);

            // Unsubscribe from events, per best practice.
            _takeOfflineButton.TouchUpInside -= TakeOffline_Click;
        }

        public delegate void CompletionEventHandler();

        public event CompletionEventHandler FinishedConfiguring;
    }
}