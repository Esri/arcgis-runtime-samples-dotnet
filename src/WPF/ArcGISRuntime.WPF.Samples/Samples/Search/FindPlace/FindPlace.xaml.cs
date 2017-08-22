﻿// Copyright 2017 Esri.
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
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ArcGISRuntime.WPF.Samples.FindPlace
{
    public partial class FindPlace
    {
        // The LocatorTask provides geocoding services
        private LocatorTask _geocoder;

        // Service Uri to be provided to the LocatorTask (geocoder)
        private Uri _serviceUri = new Uri("https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer");

        public FindPlace()
        {
            InitializeComponent();

            // Setup the control references and execute initialization
            Initialize();
        }

        private async void Initialize()
        {
            // Create new Map with basemap
            Map myMap = new Map(Basemap.CreateStreets());

            // Provide used Map to the MapView
            MyMapView.Map = myMap;

            // Enable location display
            MyMapView.LocationDisplay.IsEnabled = true;

            // Enable tap-for-info pattern on results
            MyMapView.GeoViewTapped += MyMapView_GeoViewTapped;

            // Initialize the LocatorTask with the provided service Uri
            _geocoder = await LocatorTask.CreateAsync(_serviceUri);

            // Enable all controls now that the locator task is ready
            MySearchBox.IsEnabled = true;
            MyLocationBox.IsEnabled = true;
            MySearchButton.IsEnabled = true;
            MySearchRestrictedButton.IsEnabled = true;
        }

        private async Task<MapPoint> GetSearchMapPoint(string locationText)
        {
            // Get the map point for the search text
            if (locationText != "Current Location")
            {
                // Geocode the location
                IReadOnlyList<GeocodeResult> locations = await _geocoder.GeocodeAsync(locationText);

                // return if there are no results
                if (locations.Count() < 1) { return null; }

                // Get the first result
                GeocodeResult result = locations.First();

                // Return the map point
                return result.DisplayLocation;
            }
            else
            {
                // Get the current device location
                return MyMapView.LocationDisplay.Location.Position;
            }
        }

        private async void UpdateSearch(string enteredText, string locationText, bool restrictToExtent = false)
        {
            // Clear any existing markers
            MyMapView.GraphicsOverlays.Clear();

            // Return gracefully if the textbox is empty or the geocoder isn't ready
            if (string.IsNullOrWhiteSpace(enteredText) || _geocoder == null) { return; }

            // Create the geocode parameters
            GeocodeParameters parameters = new GeocodeParameters();

            // Get the MapPoint for the current search location
            MapPoint searchLocation = await GetSearchMapPoint(locationText);

            // Update the geocode parameters if the map point is not null
            if (searchLocation != null)
            {
                parameters.PreferredSearchLocation = searchLocation;
            }

            // Update the search area if desired
            if (restrictToExtent)
            {
                // Get the current map extent
                Geometry extent = MyMapView.VisibleArea;

                // Update the search parameters
                parameters.SearchArea = extent;
            }

            // Get the location information
            IReadOnlyList<GeocodeResult> locations = await _geocoder.GeocodeAsync(enteredText, parameters);

            // Stop gracefully and show a message if the geocoder does not return a result
            if (locations.Count < 1) { ShowStatusMessage("No results found"); return; }

            // Create the GraphicsOverlay so that results can be drawn on the map
            GraphicsOverlay resultOverlay = new GraphicsOverlay();

            // Add each address to the map
            foreach (GeocodeResult location in locations)
            {
                // Get the Graphic to display
                Graphic point = await GraphicForPoint(location.DisplayLocation);

                // Add the specific result data to the point
                point.Attributes["Match_Title"] = location.Label;

                // Get the address for the point
                IReadOnlyList<GeocodeResult> addresses = await _geocoder.ReverseGeocodeAsync(location.DisplayLocation);

                // Add the first suitable address if possible
                if (addresses.Count() > 0)
                {
                    point.Attributes["Match_Address"] = addresses.First().Label;
                }

                // Add the Graphic to the GraphicsOverlay
                resultOverlay.Graphics.Add(point);
            }

            // Add the GraphicsOverlay to the MapView
            MyMapView.GraphicsOverlays.Add(resultOverlay);

            // Create a viewpoint for the extent containing all graphics
            Viewpoint viewExtent = new Viewpoint(resultOverlay.Extent);

            // Update the map viewpoint
            MyMapView.SetViewpoint(viewExtent);
        }

        /// <summary>
        /// Creates and returns a Graphic associated with the given MapPoint
        /// </summary>
        private async Task<Graphic> GraphicForPoint(MapPoint point)
        {
            // Get current assembly that contains the image
            var currentAssembly = Assembly.GetExecutingAssembly();

            // Get image as a stream from the resources
            // Picture is defined as EmbeddedResource and DoNotCopy
            var resourceStream = currentAssembly.GetManifestResourceStream(
                "ArcGISRuntime.WPF.Resources.PictureMarkerSymbols.pin_star_blue.png");

            // Create new symbol using asynchronous factory method from stream
            PictureMarkerSymbol pinSymbol = await PictureMarkerSymbol.CreateAsync(resourceStream);
            pinSymbol.Width = 60;
            pinSymbol.Height = 60;
            // The image is a pin; offset the image so that the pinpoint
            //     is on the point rather than the image's true center
            pinSymbol.OffsetX = pinSymbol.Width / 2;
            pinSymbol.OffsetY = pinSymbol.Height / 2;
            return new Graphic(point, pinSymbol);
        }

        private async void MyMapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            // Search for the graphics underneath the user's tap
            IReadOnlyList<IdentifyGraphicsOverlayResult> results = await MyMapView.IdentifyGraphicsOverlaysAsync(e.Position, 12, false);

            // Clear callouts and return if there was no result
            if (results.Count < 1 || results.First().Graphics.Count < 1) { MyMapView.DismissCallout(); return; }

            // Get the first graphic from the first result
            Graphic matchingGraphic = results.First().Graphics.First();

            // Get the title; manually added to the point's attributes in UpdateSearch
            String title = matchingGraphic.Attributes["Match_Title"] as String;

            // Get the address; manually added to the point's attributes in UpdateSearch
            String address = matchingGraphic.Attributes["Match_Address"] as String;

            // Define the callout
            CalloutDefinition calloutBody = new CalloutDefinition(title, address);

            // Show the callout on the map at the tapped location
            MyMapView.ShowCalloutAt(e.Location, calloutBody);
        }

        private async Task<IEnumerable<String>> GetSuggestResults(string searchText, string location = "", bool poiOnly = false)
        {
            // Quit if string is null, empty, or whitespace
            if (String.IsNullOrWhiteSpace(searchText)) { return null; }

            // Quit if the geocoder isn't ready
            if (_geocoder == null) { return null; }

            // Create geocode parameters
            SuggestParameters parameters = new SuggestParameters();

            // Restrict suggestions to points of interest if desired
            if (poiOnly) { parameters.Categories.Add("POI"); }

            // Set the location for the suggest parameters
            if (!String.IsNullOrWhiteSpace(location))
            {
                // Get the MapPoint for the current search location
                MapPoint searchLocation = await GetSearchMapPoint(location);

                // Update the geocode parameters if the map point is not null
                if (searchLocation != null)
                {
                    parameters.PreferredSearchLocation = searchLocation;
                }
            }

            // Get the updated results from the query so far
            IReadOnlyList<SuggestResult> results = await _geocoder.SuggestAsync(searchText, parameters);

            // Convert the list into a list of strings (corresponding to the label property on each result)
            IEnumerable<String> formattedResults = results.Select(result => result.Label);

            // Return the list
            return formattedResults;
        }

        private void ShowStatusMessage(string message)
        {
            // Display the message to the user
            MessageBox.Show(message);
        }

        private async void MySearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Get the current text
            string searchText = MySearchBox.Text;

            // Get the current search location
            string locationText = MyLocationBox.Text;

            // Convert the list into a usable format for the suggest box
            IEnumerable<String> results = await GetSuggestResults(searchText, locationText, true);

            // Quit if there are no results
            if (results == null || results.Count() == 0) { return; }

            // Update the list of options
            MySearchBox.ItemsSource = results;
        }

        private async void MyLocationBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Get the current text
            string searchText = MyLocationBox.Text;

            // Get the results
            IEnumerable<String> results = await GetSuggestResults(searchText);

            // Quit if there are no results
            if (results == null || results.Count() == 0) { return; }

            // Get a modifiable list from the results
            List<String> mutableResults = results.ToList();

            // Add a 'current location' option to the list
            mutableResults.Insert(0, "Current Location");

            // Update the list of options
            MyLocationBox.ItemsSource = mutableResults;
        }

        private void MySearchRestrictedButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the search text
            string searchText = MySearchBox.Text;

            // Get the location text
            string locationText = MyLocationBox.Text;

            // Run the search
            UpdateSearch(searchText, locationText, true);
        }

        private void MySearchButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the search text
            string searchText = MySearchBox.Text;

            // Get the location text
            string locationText = MyLocationBox.Text;

            // Run the search
            UpdateSearch(searchText, locationText, false);
        }
    }
}