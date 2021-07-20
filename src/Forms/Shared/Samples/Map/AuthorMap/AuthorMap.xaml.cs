// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.UI;
using Forms.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;

#if __ANDROID__
using Application = Xamarin.Forms.Application;
#endif

namespace ArcGISRuntime.Samples.AuthorMap
{
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Create and save map",
        category: "Map",
        description: "Create and save a map as an ArcGIS `PortalItem` (i.e. web map).",
        instructions: "1. Select the basemap and layers you'd like to add to your map.",
        tags: new[] { "ArcGIS Online", "OAuth", "portal", "publish", "share", "web map" })]
    [ArcGISRuntime.Samples.Shared.Attributes.ClassFile("SaveMapPage.xaml.cs", "Helpers\\ArcGISLoginPrompt.cs")]
    [ArcGISRuntime.Samples.Shared.Attributes.XamlFiles("SaveMapPage.xaml")]
    public partial class AuthorMap : ContentPage
    {
        private const string ArcGISOnlineUrl = "https://www.arcgis.com/sharing/rest";

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

        public AuthorMap()
        {
            InitializeComponent();

            _ = Initialize();
        }

        private async Task Initialize()
        {
            ArcGISLoginPrompt.SetChallengeHandler();

            bool loggedIn = await ArcGISLoginPrompt.EnsureAGOLCredentialAsync();

            // Show a plain gray map in the map view.
            if (loggedIn)
            {
                MyMapView.Map = new Map(BasemapStyle.ArcGISLightGray);
            }
            else MyMapView.Map = new Map();

            // Change the style of the layer list view for Android and UWP
            switch (Device.RuntimePlatform)
            {
                case Device.Android:
                    LayersList.BackgroundColor = Color.Black;
                    break;

                case Device.UWP:
                    LayersList.BackgroundColor = Color.FromRgba(255, 255, 255, 0.3);
                    LayersList.Margin = new Thickness(50);
                    break;
            }
        }

        private void LayerSelected(object sender, ItemTappedEventArgs e)
        {
            // return if null
            if (e.Item == null) { return; }

            // Handle the event when a layer item is selected (tapped) in the layer list
            string selectedItem = e.Item.ToString();

            // See if this is one of the layers in the operational layers list
            if (_operationalLayerUrls.ContainsKey(selectedItem))
            {
                // Get the service URL from the operational layers dictionary
                string value = _operationalLayerUrls[selectedItem];

                // Call a function to add the chosen operational layer
                AddLayer(selectedItem, value);
            }
            else
            {
                // Add the chosen basemap (replace the current one)
                ApplyBasemap(selectedItem);
            }

            // Hide the layer list
            LayersList.IsVisible = false;
        }

        private void ShowLayerList(object sender, EventArgs e)
        {
            // See which button was used to show the list and fill it accordingly
            Button button = (Button)sender;
            if (button.Text == "Basemap")
            {
                // Show the basemap list
                LayersList.ItemsSource = _basemapNames.ToList();
            }
            else if (button.Text == "Layers")
            {
                // Show the operational layers list (names)
                LayersList.ItemsSource = _operationalLayerUrls.Keys;
            }

            // Show the layer list view control
            LayersList.IsVisible = true;
        }

        private async void ShowSaveMapUI(object sender, EventArgs e)
        {
            // Create a SaveMapPage page for getting user input for the new web map item
            SaveMapPage mapInputForm = new SaveMapPage();

            // If an existing map, show the UI for updating the item
            Item mapItem = MyMapView.Map.Item;
            if (mapItem != null)
            {
                mapInputForm.ShowForUpdate(mapItem.Title, mapItem.Description, mapItem.Tags.ToArray());
            }

            // Handle the save button click event on the page
            mapInputForm.OnSaveClicked += SaveMapAsync;

            // Navigate to the SaveMapPage UI
            await Navigation.PushAsync(mapInputForm);
        }

        // Event handler to get information entered by the user and save the map
        private async void SaveMapAsync(object sender, SaveMapEventArgs e)
        {
            // Get the current map
            Map myMap = MyMapView.Map;

            try
            {
                // Make sure the user is logged in to ArcGIS Online
                bool loggedIn = await ArcGISLoginPrompt.EnsureAGOLCredentialAsync();
                if (!loggedIn) return;

                // Load the current map before saving to portal.
                await myMap.LoadAsync();

                // Show the progress bar so the user knows work is happening
                SaveMapProgressBar.IsVisible = true;

                // Get information entered by the user for the new portal item properties
                string title = e.MapTitle;
                string description = e.MapDescription;
                string[] tags = e.Tags;

                // Apply the current extent as the map's initial extent
                myMap.InitialViewpoint = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

                // Export the current map view for the item's thumbnail
                RuntimeImage thumbnailImage = await MyMapView.ExportImageAsync();

                // See if the map has already been saved (has an associated portal item)
                if (myMap.Item == null)
                {
                    // Get the ArcGIS Online portal (will use credential from login above)
                    ArcGISPortal agsOnline = await ArcGISPortal.CreateAsync(new Uri(ArcGISOnlineUrl));

                    // Save the current state of the map as a portal item in the user's default folder
                    await myMap.SaveAsAsync(agsOnline, null, title, description, tags, thumbnailImage);

                    // Report a successful save
                    await Application.Current.MainPage.DisplayAlert("Map Saved", "Saved '" + title + "' to ArcGIS Online!", "OK");
                }
                else
                {
                    // This is not the initial save, call SaveAsync to save changes to the existing portal item
                    await myMap.SaveAsync();

                    // Get the file stream from the new thumbnail image
                    Stream imageStream = await thumbnailImage.GetEncodedBufferAsync();

                    // Update the item thumbnail
                    ((PortalItem)myMap.Item).SetThumbnail(imageStream);
                    await myMap.SaveAsync();

                    // Report update was successful
                    await Application.Current.MainPage.DisplayAlert("Updates Saved", "Saved changes to '" + myMap.Item.Title + "'", "OK");
                }
            }
            catch (Exception ex)
            {
                // Show the exception message
                await Application.Current.MainPage.DisplayAlert("Unable to save map", ex.Message, "OK");
            }
            finally
            {
                // Hide the progress bar
                SaveMapProgressBar.IsVisible = false;
            }
        }

        private void NewMapButtonClick(object sender, EventArgs e)
        {
            // Create a new map (will not have an associated PortalItem)
            MyMapView.Map = new Map(BasemapStyle.ArcGISLightGray);
        }

        private void ApplyBasemap(string basemapName)
        {
            // Set the basemap for the map according to the user's choice in the list box.
            switch (basemapName)
            {
                case "Light Gray":
                    MyMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISLightGray);
                    break;

                case "Topographic":
                    MyMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISTopographic);
                    break;

                case "Streets":
                    MyMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISStreets);
                    break;

                case "Imagery":
                    MyMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISImagery);
                    break;

                case "Ocean":
                    MyMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISOceans);
                    break;
            }
        }

        private void AddLayer(string layerName, string url)
        {
            // See if the layer already exists, and remove it if it does
            if (MyMapView.Map.OperationalLayers.FirstOrDefault(l => l.Name == layerName) is ArcGISMapImageLayer layer)
            {
                MyMapView.Map.OperationalLayers.Remove(layer);
            }
            else
            {
                // Otherwise, add the layer
                Uri layerUri = new Uri(url);

                // Create and add a new map image layer
                layer = new ArcGISMapImageLayer(layerUri);
                layer.Name = layerName;
                layer.Opacity = 0.5;
                MyMapView.Map.OperationalLayers.Add(layer);
            }
        }
    }
}