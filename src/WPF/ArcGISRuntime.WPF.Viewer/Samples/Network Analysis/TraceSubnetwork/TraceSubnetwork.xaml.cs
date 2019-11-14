﻿// Copyright 2019 Esri.
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
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Esri.ArcGISRuntime.UtilityNetworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SelectionMode = Esri.ArcGISRuntime.Mapping.SelectionMode;

namespace ArcGISRuntime.WPF.Samples.TraceSubnetwork
{
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        "Trace a subnetwork",
        "Network Analysis",
        "Discover all the features participating in a subnetwork with subnetwork, upstream, and downstream trace types.",
        "")]
    [ArcGISRuntime.Samples.Shared.Attributes.OfflineData()]
    public partial class TraceSubnetwork
    {
        private const string FeatureServiceUrl = "https://sampleserver7.arcgisonline.com/arcgis/rest/services/UtilityNetwork/NapervilleElectric/FeatureServer";
        private Viewpoint _startingViewpoint = new Viewpoint(new Envelope(-9812980.8041217551, 5128523.87694709, -9812798.4363710005, 5128627.6261982173, SpatialReferences.WebMercator));

        private UtilityNetwork _utilityNetwork;
        private SimpleMarkerSymbol _startingPointSymbol;
        private SimpleMarkerSymbol _barrierPointSymbol;

        private List<UtilityElement> _startingLocations = new List<UtilityElement>();
        private List<UtilityElement> _barriers = new List<UtilityElement>();
        private TaskCompletionSource<UtilityTerminal> _terminalCompletionSource = null;

        public TraceSubnetwork()
        {
            InitializeComponent();
            Initialize();
        }

        private async void Initialize()
        {
            try
            {
                IsBusy.Visibility = Visibility.Visible;
                Status.Text = "Loading Utility Network...";

                // Setup Map with Feature Layer(s) that contain Utility Network.
                MyMapView.Map = new Map(Basemap.CreateStreetsNightVector())
                {
                    InitialViewpoint = _startingViewpoint
                };
                FeatureLayer lineLayer = new FeatureLayer(new Uri($"{FeatureServiceUrl}/115"));
                lineLayer.Renderer = new SimpleRenderer(new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.DarkCyan, 3));
                MyMapView.Map.OperationalLayers.Add(lineLayer);
                FeatureLayer electricDevicelayer = new FeatureLayer(new Uri($"{FeatureServiceUrl}/100"));
                MyMapView.Map.OperationalLayers.Add(electricDevicelayer);

                MyMapView.SelectionProperties = new SelectionProperties(System.Drawing.Color.Yellow);

                // Create and load the utility network.
                _utilityNetwork = await UtilityNetwork.CreateAsync(new Uri(FeatureServiceUrl), MyMapView.Map);

                // Update Configuration UI.
                TraceTypes.ItemsSource = new[] { UtilityTraceType.Subnetwork, UtilityTraceType.Upstream, UtilityTraceType.Downstream };
                TraceTypes.SelectedIndex = 0;
                IEnumerable<UtilityTier> tiers = (from d in _utilityNetwork.Definition.DomainNetworks
                                                  select d.Tiers).SelectMany(t => t);
                SourceTiers.ItemsSource = tiers;
                if (tiers.Any())
                {
                    SourceTiers.SelectedIndex = 0;
                }

                Status.Text = "Click on the network lines or points to add a utility element.";

                // Create symbols for starting locations and barriers.
                _startingPointSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Cross, System.Drawing.Color.Green, 20d);
                _barrierPointSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.X, System.Drawing.Color.Red, 20d);

                // Create a graphics overlay.
                GraphicsOverlay graphicsOverlay = new GraphicsOverlay();
                MyMapView.GraphicsOverlays.Add(graphicsOverlay);
            }
            catch (Exception ex)
            {
                Status.Text = "Loading Utility Network failed...";
                MessageBox.Show(ex.Message, ex.Message.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                MainUI.IsEnabled = true;
                IsBusy.Visibility = Visibility.Hidden;
            }
        }

        private async void OnGeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            try
            {
                IsBusy.Visibility = Visibility.Visible;
                Status.Text = "Identifying trace locations...";

                // Identify the feature to be used.
                IEnumerable<IdentifyLayerResult> identifyResult = await MyMapView.IdentifyLayersAsync(e.Position, 10.0, false);
                ArcGISFeature feature = identifyResult?.FirstOrDefault()?.GeoElements?.FirstOrDefault() as ArcGISFeature;
                if (feature == null) { return; }

                // Create element from the identified feature.
                UtilityElement element = _utilityNetwork.CreateElement(feature);

                if (element.NetworkSource.SourceType == UtilityNetworkSourceType.Junction)
                {
                    // Select terminal for junction feature.
                    IEnumerable<UtilityTerminal> terminals = element.AssetType.TerminalConfiguration?.Terminals;
                    if (terminals?.Count() > 1)
                    {
                        element.Terminal = await GetTerminalAsync(terminals);
                    }
                    Status.Text = $"Terminal: {element.Terminal?.Name ?? "default"}";
                }
                else if (element.NetworkSource.SourceType == UtilityNetworkSourceType.Edge)
                {
                    // Compute how far tapped location is along the edge feature.
                    if (feature.Geometry is Polyline line)
                    {
                        line = GeometryEngine.RemoveZ(line) as Polyline;
                        element.FractionAlongEdge = GeometryEngine.FractionAlong(line, e.Location, -1);
                        Status.Text = $"Fraction along edge: {element.FractionAlongEdge}";
                    }
                }

                // Check whether starting location or barrier is added to update the right collection and symbology.
                Symbol symbol = null;
                if (IsAddingStartingLocations.IsChecked.Value == true)
                {
                    _startingLocations.Add(element);
                    symbol = _startingPointSymbol;
                }
                else
                {
                    _barriers.Add(element);
                    symbol = _barrierPointSymbol;
                }

                // Add a graphic for the new utility element.
                Graphic traceLocationGraphic = new Graphic(feature.Geometry as MapPoint ?? e.Location, symbol);
                MyMapView.GraphicsOverlays.FirstOrDefault()?.Graphics.Add(traceLocationGraphic);
            }
            catch (Exception ex)
            {
                Status.Text = "Could not identify location.";
                MessageBox.Show(ex.Message, ex.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy.Visibility = Visibility.Hidden;
            }
        }

        private async Task<UtilityTerminal> GetTerminalAsync(IEnumerable<UtilityTerminal> terminals)
        {
            try
            {
                UpdateBorderVisibility(TerminalPicker);
                Picker.ItemsSource = terminals;
                Picker.SelectedIndex = 1;

                // Waits for user to select a terminal.
                _terminalCompletionSource = new TaskCompletionSource<UtilityTerminal>();
                return await _terminalCompletionSource.Task;
            }
            finally
            {
                UpdateBorderVisibility(MainUI);
            }
        }

        private void UpdateBorderVisibility(Border borderToShow)
        {
            if (borderToShow == MainUI)
            {
                MyMapView.GeoViewTapped += OnGeoViewTapped;
            }
            else
            {
                MyMapView.GeoViewTapped -= OnGeoViewTapped;
            }
            IEnumerable<Border> borders = new[] { MainUI, Configuration, TerminalPicker };
            foreach (Border border in borders)
            {
                if (border == borderToShow)
                {
                    border.Visibility = Visibility.Visible;
                }
                else
                {
                    border.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OnTerminalSelected(object sender, RoutedEventArgs e)
        {
            _terminalCompletionSource.TrySetResult(Picker.SelectedItem as UtilityTerminal);
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            // Reset the UI.
            Status.Text = "Click on the network lines or points to add a utility element.";
            IsBusy.Visibility = Visibility.Hidden;
            UpdateBorderVisibility(MainUI);
            TraceTypes.SelectedIndex = 0;
            if (SourceTiers.ItemsSource is IEnumerable<UtilityTier> tiers && tiers.Any())
            {
                SourceTiers.SelectedIndex = 0;
            }
            // Clear collections of starting locations and barriers.
            _startingLocations.Clear();
            _barriers.Clear();

            // Clear the map of any locations, barriers and trace result.
            MyMapView.GraphicsOverlays.FirstOrDefault()?.Graphics.Clear();
            MyMapView.Map.OperationalLayers.OfType<FeatureLayer>().ToList().ForEach(layer => layer.ClearSelection());
        }

        private void OnConfigureTrace(object sender, RoutedEventArgs e)
        {
            UpdateBorderVisibility(Configuration);
        }

        private void OnConfigureClosed(object sender, RoutedEventArgs e)
        {
            UpdateBorderVisibility(MainUI);
        }

        private async void OnTrace(object sender, RoutedEventArgs e)
        {
            try
            {
                MainUI.IsEnabled = false;
                UpdateBorderVisibility(MainUI);
                var traceType = (UtilityTraceType)TraceTypes.SelectedItem;
                IsBusy.Visibility = Visibility.Visible;
                Status.Text = $"Running `{traceType}` trace...";

                // Clear previous selection from the layers.
                MyMapView.Map.OperationalLayers.OfType<FeatureLayer>().ToList().ForEach(layer => layer.ClearSelection());

                // Build trace parameters.
                var parameters = new UtilityTraceParameters(traceType, _startingLocations);
                foreach (UtilityElement barrier in _barriers)
                {
                    parameters.Barriers.Add(barrier);
                }
                if (SourceTiers.SelectedItem is UtilityTier tier)
                {
                    // Domain Network, Source/Target Tier, and Traversability will all be set as configured on server.
                    parameters.TraceConfiguration = tier.TraceConfiguration;
                }

                //  Get the trace result from the utility network.
                IEnumerable<UtilityTraceResult> traceResult = await _utilityNetwork.TraceAsync(parameters);
                UtilityElementTraceResult elementTraceResult = traceResult?.FirstOrDefault() as UtilityElementTraceResult;

                if (elementTraceResult?.Elements?.Count > 0)
                {
                    foreach (FeatureLayer layer in MyMapView.Map.OperationalLayers.OfType<FeatureLayer>())
                    {
                        QueryParameters query = new QueryParameters();
                        foreach (long id in from element in elementTraceResult.Elements
                                            where element.NetworkSource.Name == layer.FeatureTable.TableName
                                            select element.ObjectId)
                            query.ObjectIds.Add(id);
                        await layer.SelectFeaturesAsync(query, SelectionMode.New);
                    }
                }
                Status.Text = "Trace completed.";
            }
            catch (Exception ex)
            {
                Status.Text = "Trace failed...";
                MessageBox.Show(ex.Message, ex.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                MainUI.IsEnabled = true;
                IsBusy.Visibility = Visibility.Hidden;
            }
        }
    }
}