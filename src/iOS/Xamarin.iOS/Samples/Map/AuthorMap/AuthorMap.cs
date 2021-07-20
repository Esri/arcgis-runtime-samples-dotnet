// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Helpers;
using ArcGISRuntime.Samples.Shared.Managers;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Foundation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UIKit;

namespace ArcGISRuntime.Samples.AuthorMap
{
    [Register("AuthorMap")]
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Create and save map",
        category: "Map",
        description: "Create and save a map as an ArcGIS `PortalItem` (i.e. web map).",
        instructions: "1. Select the basemap and layers you'd like to add to your map.",
        tags: new[] { "ArcGIS Online", "OAuth", "portal", "publish", "share", "web map" })]
    [ArcGISRuntime.Samples.Shared.Attributes.ClassFile("Helpers/ArcGISLoginPrompt.cs")]
    public class AuthorMap : UIViewController
    {
        // Hold references to UI controls.
        private MapView _myMapView;
        private UIBarButtonItem _newMapButton;
        private UIBarButtonItem _basemapButton;
        private UIBarButtonItem _layersButton;
        private UIBarButtonItem _saveButton;

        // Map metadata.
        private string _title = "My new map";
        private string _description = "Created with ArcGIS Runtime";
        private string _tags = "ArcGIS Runtime";

        // String array to store names of the available basemaps
        private readonly string[] _basemapNames =
        {
            "Light Gray",
            "Topographic",
            "Streets",
            "Imagery",
            "Ocean"
        };

        // Dictionary of operational layer names and URLs
        private Dictionary<string, string> _operationalLayerUrls = new Dictionary<string, string>
        {
            {"World Elevations", "https://sampleserver6.arcgisonline.com/arcgis/rest/services/Elevation/WorldElevations/MapServer"},
            {"World Cities", "https://sampleserver6.arcgisonline.com/arcgis/rest/services/SampleWorldCities/MapServer/" },
            {"US Census Data", "https://sampleserver6.arcgisonline.com/arcgis/rest/services/Census/MapServer"}
        };

        // Progress bar to show that the app is working.
        private UIActivityIndicatorView _activityIndicator;

        // Constants for OAuth-related values ...
        // URL of the server to authenticate with.
        private const string ServerUrl = "https://www.arcgis.com/sharing/rest";

        public AuthorMap()
        {
            Title = "Author and save a map";
        }

        private async Task Initialize()
        {
            // Remove the API key.
            ApiKeyManager.DisableKey();

            // Prompt the user for a login. (Needed for loading basemaps without an API key.)
            ArcGISLoginPrompt.SetChallengeHandler(this);
            bool loggedIn = await ArcGISLoginPrompt.EnsureAGOLCredentialAsync();

            // Show a plain gray map in the map view
            if (loggedIn)
            {
                _myMapView.Map = new Map(BasemapStyle.ArcGISLightGray);
            }
            else _myMapView.Map = new Map();
        }

        private void ShowBasemapClicked(object sender, EventArgs e)
        {
            // Create a new Alert Controller.
            UIAlertController basemapsActionSheet =
                UIAlertController.Create("Basemaps", "Choose a basemap", UIAlertControllerStyle.ActionSheet);

            // Add actions to apply each basemap type.
            foreach (string name in _basemapNames)
            {
                basemapsActionSheet.AddAction(UIAlertAction.Create(name, UIAlertActionStyle.Default,
                action => ApplyBasemap(name)));
            }
            basemapsActionSheet.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel,
                action => Console.WriteLine("Canceled")));

            // Required for iPad - You must specify a source for the Action Sheet since it is displayed as a popover.
            UIPopoverPresentationController presentationPopover = basemapsActionSheet.PopoverPresentationController;
            if (presentationPopover != null)
            {
                presentationPopover.BarButtonItem = (UIBarButtonItem)sender;
                presentationPopover.PermittedArrowDirections = UIPopoverArrowDirection.Down;
            }

            // Display the list of basemaps.
            PresentViewController(basemapsActionSheet, true, null);
        }

        private void ApplyBasemap(string basemapName)
        {
            // Set the basemap for the map according to the user's choice in the list box.
            switch (basemapName)
            {
                case "Light Gray":
                    _myMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISLightGray);
                    break;

                case "Topographic":
                    _myMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISTopographic);
                    break;

                case "Streets":
                    _myMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISStreets);
                    break;

                case "Imagery":
                    _myMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISImagery);
                    break;

                case "Ocean":
                    _myMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISOceans);
                    break;
            }
        }

        private void ShowLayerListClicked(object sender, EventArgs e)
        {
            // Create a new Alert Controller.
            UIAlertController layersActionSheet =
                UIAlertController.Create("Layers", "Choose layers", UIAlertControllerStyle.ActionSheet);

            // Add actions to add or remove each of the available layers.
            foreach (string key in _operationalLayerUrls.Keys)
            {
                layersActionSheet.AddAction(UIAlertAction.Create(key, UIAlertActionStyle.Default,
                    action => AddOrRemoveLayer(key)));
            }

            // Add a choice to cancel.
            layersActionSheet.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel,
                action => Console.WriteLine("Canceled")));

            // Required for iPad - You must specify a source for the Action Sheet since it is displayed as a popover.
            UIPopoverPresentationController presentationPopover = layersActionSheet.PopoverPresentationController;
            if (presentationPopover != null)
            {
                presentationPopover.BarButtonItem = (UIBarButtonItem)sender;
                presentationPopover.PermittedArrowDirections = UIPopoverArrowDirection.Down;
            }

            // Display the list of layers to add/remove.
            PresentViewController(layersActionSheet, true, null);
        }

        private async void AddOrRemoveLayer(string layerName)
        {
            // See if the layer already exists.
            ArcGISMapImageLayer layer =
                _myMapView.Map.OperationalLayers.FirstOrDefault(l => l.Name == layerName) as ArcGISMapImageLayer;

            // If the layer is in the map, remove it.
            if (layer != null)
            {
                _myMapView.Map.OperationalLayers.Remove(layer);
            }
            else
            {
                // Get the URL for this layer.
                string layerUrl = _operationalLayerUrls[layerName];
                Uri layerUri = new Uri(layerUrl);

                // Create a new map image layer.
                layer = new ArcGISMapImageLayer(layerUri)
                {
                    Name = layerName
                };
                await layer.LoadAsync();

                // Set it 50% opaque, and add it to the map.
                layer.Opacity = 0.5;
                _myMapView.Map.OperationalLayers.Add(layer);
            }
        }

        private void SaveMapClicked(object sender, EventArgs e)
        {
            UIAlertController alertController = UIAlertController.Create("Map properties", "Set the title, description, and tags",
                UIAlertControllerStyle.Alert);

            alertController.AddTextField(field => { field.Text = _title; });
            alertController.AddTextField(field => { field.Text = _description; });
            alertController.AddTextField(field => { field.Text = _tags; });

            alertController.AddAction(UIAlertAction.Create("Save", UIAlertActionStyle.Default, uiAction =>
            {
                _title = alertController.TextFields[0].Text.Trim();
                _description = alertController.TextFields[1].Text.Trim();
                _tags = alertController.TextFields[2].Text.Trim();
                MapItemInfoEntered();
            }));

            alertController.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));

            PresentViewController(alertController, true, null);
        }

        // Handle the OnMapInfoEntered event from the item input UI.
        // MapSavedEventArgs contains the title, description, and tags that were entered.
        private async void MapItemInfoEntered()
        {
            // Get the current map.
            Map myMap = _myMapView.Map;

            try
            {
                // Show the activity indicator so the user knows work is happening.
                _activityIndicator.StartAnimating();

                // Get information entered by the user for the new portal item properties.
                string[] tags = _tags.Split(',');

                // Apply the current extent as the map's initial extent.
                myMap.InitialViewpoint = _myMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

                // Export the current map view for the item's thumbnail.
                RuntimeImage thumbnailImg = await _myMapView.ExportImageAsync();

                // See if the map has already been saved (has an associated portal item).
                if (myMap.Item == null)
                {
                    // Call a function to save the map as a new portal item.
                    await SaveNewMapAsync(myMap, _title, _description, tags, thumbnailImg);

                    // Report a successful save.
                    UIAlertController alert = UIAlertController.Create("Saved map",
                        "Saved " + _title + " to ArcGIS Online", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    PresentViewController(alert, true, null);
                }
                else
                {
                    // This is not the initial save, call SaveAsync to save changes to the existing portal item.
                    await myMap.SaveAsync();

                    // Get the file stream from the new thumbnail image.
                    Stream imageStream = await thumbnailImg.GetEncodedBufferAsync();

                    // Update the item thumbnail.
                    ((PortalItem)myMap.Item).SetThumbnail(imageStream);
                    await myMap.SaveAsync();

                    // Report update was successful.
                    UIAlertController alert = UIAlertController.Create("Updated map", "Saved changes to " + _title,
                        UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    PresentViewController(alert, true, null);
                }
            }
            catch (Exception)
            {
                // Report save error.
                UIAlertController alert = UIAlertController.Create("Error", "Unable to save " + _title,
                    UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                PresentViewController(alert, true, null);
            }
            finally
            {
                // Hide the progress bar.
                _activityIndicator.StopAnimating();
            }
        }

        private async Task SaveNewMapAsync(Map myMap, string title, string description, string[] tags, RuntimeImage img)
        {
            await ArcGISLoginPrompt.EnsureAGOLCredentialAsync();

            // Get the ArcGIS Online portal (will use credential from login above).
            ArcGISPortal agsOnline = await ArcGISPortal.CreateAsync(new Uri(ServerUrl));

            // Save the current state of the map as a portal item in the user's default folder.
            await myMap.SaveAsAsync(agsOnline, null, title, description, tags, img);
        }

        private void NewMapClicked(object sender, EventArgs e)
        {
            // Clear the map from the map view (allow the user to start over and save as a new portal item).
            _myMapView.Map = new Map(BasemapStyle.ArcGISLightGray);
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            _ = Initialize();
        }

        public override void LoadView()
        {
            // Create the views.
            View = new UIView { BackgroundColor = ApplicationTheme.BackgroundColor };

            _myMapView = new MapView();
            _myMapView.TranslatesAutoresizingMaskIntoConstraints = false;

            _newMapButton = new UIBarButtonItem();
            _newMapButton.Title = "New map";

            _basemapButton = new UIBarButtonItem();
            _basemapButton.Title = "Basemap";

            _layersButton = new UIBarButtonItem();
            _layersButton.Title = "Layers";

            _saveButton = new UIBarButtonItem();
            _saveButton.Title = "Save";

            UIToolbar toolbar = new UIToolbar();
            toolbar.TranslatesAutoresizingMaskIntoConstraints = false;
            toolbar.Items = new[]
            {
                _newMapButton,
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                _basemapButton,
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                _layersButton,
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                _saveButton
            };

            _activityIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.WhiteLarge)
            {
                TranslatesAutoresizingMaskIntoConstraints = false,
                BackgroundColor = UIColor.FromWhiteAlpha(0f, .8f),
                HidesWhenStopped = true
            };

            // Add the views.
            View.AddSubviews(_myMapView, toolbar, _activityIndicator);

            // Lay out the views.
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _myMapView.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
                _myMapView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _myMapView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                _myMapView.BottomAnchor.ConstraintEqualTo(toolbar.TopAnchor),

                toolbar.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor),
                toolbar.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                toolbar.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),

                _activityIndicator.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
                _activityIndicator.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _activityIndicator.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                _activityIndicator.BottomAnchor.ConstraintEqualTo(View.BottomAnchor)
            });
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);

            // Subscribe to events.
            _newMapButton.Clicked += NewMapClicked;
            _basemapButton.Clicked += ShowBasemapClicked;
            _layersButton.Clicked += ShowLayerListClicked;
            _saveButton.Clicked += SaveMapClicked;
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);

            // Unsubscribe from events, per best practice.
            _newMapButton.Clicked -= NewMapClicked;
            _basemapButton.Clicked -= ShowBasemapClicked;
            _layersButton.Clicked -= ShowLayerListClicked;
            _saveButton.Clicked -= SaveMapClicked;

            // Restore API key if leaving Create and save map sample.
            ApiKeyManager.EnableKey();
        }
    }
}