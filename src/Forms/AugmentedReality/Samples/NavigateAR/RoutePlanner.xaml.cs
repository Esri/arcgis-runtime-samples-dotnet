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
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Xamarin.Forms;
using Esri.ArcGISRuntime.Tasks.NetworkAnalysis;
using System;
using System.Linq;
using System.Diagnostics;
using Esri.ArcGISRuntime.Xamarin.Forms;
using Forms.Helpers;
using System.Threading.Tasks;

#if __ANDROID__
using Application = Xamarin.Forms.Application;
using ArcGISRuntime.Droid;
#endif

namespace ArcGISRuntimeXamarin.Samples.NavigateAR
{
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Navigate in AR",
        category: "Augmented reality",
        description: "Use a route displayed in the real world to navigate.",
        instructions: "The sample opens with a map centered on the current location. Tap the map to add an origin and a destination; the route will be shown as a line. When ready, click 'Confirm' to start the AR navigation. Calibrate the heading before starting to navigate. When you start, route instructions will be displayed and spoken. As you proceed through the route, new directions will be provided until you arrive.",
        tags: new[] { "augmented reality", "directions", "full-scale", "guidance", "mixed reality", "navigate", "navigation", "real-scale", "route", "routing", "world-scale" })]
    [ArcGISRuntime.Samples.Shared.Attributes.ClassFile("Helpers\\ArcGISLoginPrompt.cs")]
    public partial class RoutePlanner : ContentPage
    {
        // Graphics overlays for showing stops and the calculated route.
        private GraphicsOverlay _routeOverlay;
        private GraphicsOverlay _stopsOverlay;

        // Hold the start and end point.
        private MapPoint _startPoint;
        private MapPoint _endPoint;

        // Routing.
        private RouteTask _routeTask;
        private Route _route;
        private RouteResult _routeResult;
        private RouteParameters _routeParameters;
        private Uri _routingUri = new Uri("https://route.arcgis.com/arcgis/rest/services/World/Route/NAServer/Route_World");

        public RoutePlanner()
        {
            InitializeComponent();
            _ = Initialize();
        }

        private async Task Initialize()
        {
            // Create and add the map.
            MyMapView.Map = new Map(BasemapStyle.ArcGISImageryStandard);

            MyMapView.PropertyChanged += async (o, e) =>
            {
                // Start the location display on the mapview.
                try
                {

                    // Permission request only needed on Android.
                    if (e.PropertyName == nameof(MyMapView.LocationDisplay) && MyMapView.LocationDisplay != null)
                    {
#if XAMARIN_ANDROID
                        // See implementation in MainActivity.cs in the Android platform project.
                        bool permissionGranted = await MainActivity.Instance.AskForLocationPermission();
                        if (!permissionGranted)
                        {
                            throw new Exception("Location permission not granted.");
                        }
#endif

                        MyMapView.LocationDisplay.AutoPanMode = LocationDisplayAutoPanMode.Recenter;
                        await MyMapView.LocationDisplay.DataSource.StartAsync();
                        MyMapView.LocationDisplay.IsEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    await Application.Current.MainPage.DisplayAlert("Couldn't start location", ex.Message, "OK");
                }
            };

            try
            {
                // Login is needed to use the routing service.
                ArcGISLoginPrompt.SetChallengeHandler();

                // Create the route task.
                _routeTask = await RouteTask.CreateAsync(_routingUri);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
                Debug.WriteLine(ex.Message);
            }

            // Create route display overlay and symbology.
            _routeOverlay = new GraphicsOverlay();
            SimpleLineSymbol routeSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Yellow, 1);
            _routeOverlay.Renderer = new SimpleRenderer(routeSymbol);

            // Create stop display overlay.
            _stopsOverlay = new GraphicsOverlay();

            // Add the overlays to the map.
            MyMapView.GraphicsOverlays.Add(_routeOverlay);
            MyMapView.GraphicsOverlays.Add(_stopsOverlay);

            // Wait for the user to place stops.
            MyMapView.GeoViewTapped += MapView_GeoViewTapped;

            // Update the help text.
            HelpLabel.Text = "Tap to set a start point";
        }

        private void MapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            if (_startPoint == null)
            {
                // Place the start point.
                _startPoint = e.Location;
                Graphic startGraphic = new Graphic(_startPoint,
                    new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Cross, System.Drawing.Color.Green, 25));
                _stopsOverlay.Graphics.Add(startGraphic);

                // Update help text.
                HelpLabel.Text = "Tap to set an end point";
            }
            else if (_endPoint == null)
            {
                // Place the end point.
                _endPoint = e.Location;
                Graphic endGraphic = new Graphic(_endPoint,
                    new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.X, System.Drawing.Color.Red, 25));
                _stopsOverlay.Graphics.Add(endGraphic);

                // Update help text.
                HelpLabel.Text = "Solving route";

                // Solve the route.
                SolveRoute();
            }
        }

        private async void SolveRoute()
        {
            try
            {
                // Create route parameters and configure them to enable navigation.
                _routeParameters = await _routeTask.CreateDefaultParametersAsync();
                _routeParameters.ReturnStops = true;
                _routeParameters.ReturnDirections = true;
                _routeParameters.ReturnRoutes = true;

                // Prefer walking directions if available.
                TravelMode walkingMode =
                    _routeTask.RouteTaskInfo.TravelModes.FirstOrDefault(mode => mode.Name.Contains("Walk")) ??
                    _routeTask.RouteTaskInfo.TravelModes.First();
                _routeParameters.TravelMode = walkingMode;

                // Set the stops for the route.
                Stop stop1 = new Stop(_startPoint);
                Stop stop2 = new Stop(_endPoint);
                _routeParameters.SetStops(new[] { stop1, stop2 });

                // Calculate the route.
                _routeResult = await _routeTask.SolveRouteAsync(_routeParameters);

                // Get the first result.
                _route = _routeResult.Routes.First();

                // Create and show a graphic for the route.
                Graphic routeGraphic = new Graphic(_route.RouteGeometry);
                _routeOverlay.Graphics.Add(routeGraphic);

                // Allow the user to start navigating.
                EnableNavigation();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Failed to calculate route.", "OK");
                Debug.WriteLine(ex);
            }
        }

        private void EnableNavigation()
        {
            // Pass the route result to the route viewer.
            RouteViewer.PassedRouteResult = _routeResult;

            // Configure the UI.
            StartARButton.IsEnabled = true;
            HelpLabel.Text = "You're ready to start navigating!";
        }

        private void StartARClicked(object sender, EventArgs e)
        {
            // Stop the current location source.
            MyMapView.LocationDisplay.DataSource.StopAsync();

            // Set the route for the route viewer.
            RouteViewer.PassedRouteResult = _routeResult;

            // Load the routeviewer as a new page on the navigation stack.
            Navigation.PopAsync();
            Navigation.PushAsync(new RouteViewer() { }, true);
        }
    }
}