// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Helpers;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ArcGISRuntime.WPF.Samples.AuthorMap
{
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Create and save map",
        category: "Map",
        description: "Create and save a map as an ArcGIS `PortalItem` (i.e. web map).",
        instructions: "1. Select the basemap and layers you'd like to add to your map.",
        tags: new[] { "ArcGIS Online", "OAuth", "portal", "publish", "share", "web map" })]
    [ArcGISRuntime.Samples.Shared.Attributes.ClassFile("Helpers\\ArcGISLoginPrompt.cs")]
    public partial class AuthorMap
    {
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

            // Create the UI, setup the control references and execute initialization
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

            // Fill the basemap combo box with basemap names
            BasemapListBox.ItemsSource = _basemapNames;

            // Add a listener for changes in the selected basemap.
            BasemapListBox.SelectionChanged += BasemapSelectionChanged;

            // Fill the operational layers list box with layer names
            OperationalLayerListBox.ItemsSource = _operationalLayerUrls;

            // Update the extent labels whenever the view point (extent) changes
            MyMapView.ViewpointChanged += (s, evt) => UpdateViewExtentLabels();
        }

        #region UI event handlers

        private void BasemapSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Call a function to set the basemap to the one selected
            ApplyBasemap(e.AddedItems[0].ToString());
        }

        private void OperationalLayerSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Call a function to add the chosen layers to the map
            AddOperationalLayers();
        }

        private void ClearMapClicked(object sender, RoutedEventArgs e)
        {
            // Create a new map (will not have an associated PortalItem)
            MyMapView.Map = new Map(BasemapStyle.ArcGISLightGray);

            // Reset UI to be consistent with map
            BasemapListBox.SelectedIndex = 0;
            OperationalLayerListBox.SelectedIndex = -1;
        }

        private async void SaveMapClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show the progress bar so the user knows work is happening
                SaveProgressBar.Visibility = Visibility.Visible;

                // Get the current map
                Map myMap = MyMapView.Map;

                // Load the current map before saving to portal.
                await myMap.LoadAsync();

                // Apply the current extent as the map's initial extent
                myMap.InitialViewpoint = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

                // Get the current map view for the item thumbnail
                RuntimeImage thumbnailImg = await MyMapView.ExportImageAsync();

                // See if the map has already been saved (has an associated portal item)
                if (myMap.Item == null)
                {
                    // Get information for the new portal item
                    string title = TitleTextBox.Text;
                    string description = DescriptionTextBox.Text;
                    string[] tags = TagsTextBox.Text.Split(',');

                    // Make sure all required info was entered
                    if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description) || tags.Length == 0)
                    {
                        throw new Exception("Please enter a title, description, and some tags to describe the map.");
                    }

                    // Call a function to save the map as a new portal item
                    await SaveNewMapAsync(MyMapView.Map, title, description, tags, thumbnailImg);

                    // Report a successful save
                    MessageBox.Show("Saved '" + title + "' to ArcGIS Online!", "Map Saved");
                }
                else
                {
                    // This is not the initial save, call SaveAsync to save changes to the existing portal item
                    await myMap.SaveAsync();

                    // Get the file stream from the new thumbnail image
                    Stream imageStream = await thumbnailImg.GetEncodedBufferAsync();

                    // Update the item thumbnail
                    ((PortalItem)myMap.Item).SetThumbnail(imageStream);
                    await myMap.SaveAsync();

                    // Report update was successful
                    MessageBox.Show("Saved changes to '" + myMap.Item.Title + "'", "Updates Saved");
                }
            }
            catch (Exception ex)
            {
                // Report error message
                MessageBox.Show("Error saving map to ArcGIS Online: " + ex.Message);
            }
            finally
            {
                // Hide the progress bar
                SaveProgressBar.Visibility = Visibility.Hidden;
            }
        }

        #endregion UI event handlers

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

        private void AddOperationalLayers()
        {
            // Clear all operational layers from the map
            Map myMap = MyMapView.Map;
            myMap.OperationalLayers.Clear();

            // Loop through the selected items in the operational layers list box
            foreach (KeyValuePair<string, string> item in OperationalLayerListBox.SelectedItems)
            {
                // Get the service uri for each selected item
                KeyValuePair<string, string> layerInfo = item;
                Uri layerUri = new Uri(layerInfo.Value);

                // Create a new map image layer, set it 50% opaque, and add it to the map
                ArcGISMapImageLayer layer = new ArcGISMapImageLayer(layerUri)
                {
                    Opacity = 0.5
                };
                myMap.OperationalLayers.Add(layer);
            }
        }

        private async Task SaveNewMapAsync(Map myMap, string title, string description, string[] tags, RuntimeImage thumb)
        {
            await ArcGISLoginPrompt.EnsureAGOLCredentialAsync();

            // Get the ArcGIS Online portal (will use credential from login above)
            ArcGISPortal agsOnline = await ArcGISPortal.CreateAsync();

            // Save the current state of the map as a portal item in the user's default folder
            await myMap.SaveAsAsync(agsOnline, null, title, description, tags, thumb);
        }

        private void UpdateViewExtentLabels()
        {
            // Get the current view point for the map view
            Viewpoint currentViewpoint = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);
            if (currentViewpoint == null) { return; }

            // Get the current map extent (envelope) from the view point
            Envelope currentExtent = currentViewpoint.TargetGeometry as Envelope;

            // Project the current extent to geographic coordinates (longitude / latitude)
            Envelope currentGeoExtent = (Envelope)GeometryEngine.Project(currentExtent, SpatialReferences.Wgs84);

            // Fill the app text boxes with min / max longitude (x) and latitude (y) to four decimal places
            XMinTextBox.Text = currentGeoExtent.XMin.ToString("0.####");
            YMinTextBox.Text = currentGeoExtent.YMin.ToString("0.####");
            XMaxTextBox.Text = currentGeoExtent.XMax.ToString("0.####");
            YMaxTextBox.Text = currentGeoExtent.YMax.ToString("0.####");
        }
    }
}