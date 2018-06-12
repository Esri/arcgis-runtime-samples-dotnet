// Copyright 2018 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Rasters;
using Esri.ArcGISRuntime.UI.Controls;
using Foundation;
using System;
using System.Collections.Generic;
using ArcGISRuntime.Samples.Managers;
using Esri.ArcGISRuntime.Geometry;
using UIKit;

namespace ArcGISRuntime.Samples.ChangeBlendRenderer
{
    [Register("ChangeBlendRenderer")]
	[ArcGISRuntime.Samples.Shared.Attributes.OfflineData("7c4c679ab06a4df19dc497f577f111bd","caeef9aa78534760b07158bb8e068462")]
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        "Blend renderer",
        "Layers",
        "This sample demonstrates how to use blend renderer on a raster layer. You can get a hillshade blended with either a colored raster or color ramp.",
        "Tap on the 'Update Renderer' button to change the settings for the blend renderer. The sample allows you to change the Altitude, Azimuth, SlopeType and ColorRamp. If you use None as the ColorRamp, a standard hill shade raster output is displayed. For all the other ColorRamp types an elevation raster is used.",
        "Featured")]
    public class ChangeBlendRenderer : UIViewController
    {
        // Global reference to a label for Altitude
        private UILabel _Label_Altitude;

        // Global reference to the slider where the user can modify the Altitude
        private UISlider _Altitude_Slider;

        // Global reference to a label for Azimuth
        private UILabel _Label_Azimuth;

        // Global reference to the slider where the user can modify the Azimuth
        private UISlider _Azimuth_Slider;

        // Global reference to segmented control of SlopeType choices the user can choose from
        private UISegmentedControl _SlopeTypes;

        // Global reference to segmented control of ColorRamps choices the user can choose from
        private UISegmentedControl _ColorRamps;

        // Global reference to button the user clicks to change the blend renderer on the raster
        private UIButton _UpdateRenderer;

        // Toolbar to put in the background of the form
        private UIToolbar _toolbar = new UIToolbar();

        // Global reference to the MapView used in the sample
        private MapView _myMapView = new MapView();

        public ChangeBlendRenderer()
        {
            Title = "Blend renderer";
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            // Create the layout
            CreateLayout();

            // Initialize the app
            Initialize();
        }

        public override void ViewDidLayoutSubviews()
        {
            // Top margin location were the UI controls should be placed
            nfloat topStart = NavigationController.NavigationBar.Frame.Height + UIApplication.SharedApplication.StatusBarFrame.Height;
            nfloat margin = 5;
            nfloat controlHeight = 30;
            nfloat columnSplit = 100;
            nfloat formStart = View.Bounds.Height - (controlHeight * 5) - (margin * 6);

            _toolbar.Frame = new CoreGraphics.CGRect(0, formStart, View.Bounds.Width, (controlHeight * 5) + (margin * 6));

            _Label_Altitude.Frame = new CoreGraphics.CGRect(margin, formStart + margin, columnSplit - (2 * margin), controlHeight);
            _Label_Azimuth.Frame = new CoreGraphics.CGRect(margin, formStart + controlHeight + (2 * margin), columnSplit - (2 * margin), controlHeight);

            _Altitude_Slider.Frame = new CoreGraphics.CGRect(columnSplit + margin, formStart + margin, View.Bounds.Width - columnSplit - (2 * margin), controlHeight);
            _Azimuth_Slider.Frame = new CoreGraphics.CGRect(columnSplit + margin, formStart + controlHeight + (2 * margin), View.Bounds.Width - columnSplit - (2 * margin), controlHeight);

            // Setup the visual frame for the table of SlopeType choices the user can choose from
            _SlopeTypes.Frame = new CoreGraphics.CGRect(margin, formStart + (2 * controlHeight) + (3 * margin), View.Bounds.Width - (2 * margin), controlHeight);

            // Setup the visual frame for the table of ColorRamp choices the user can choose from
            _ColorRamps.Frame = new CoreGraphics.CGRect(margin, formStart + (3 * controlHeight) + (4 * margin), View.Bounds.Width - (2 * margin), controlHeight);

            // Setup the visual frame for button the users clicks to change the blend renderer on the raster
            _UpdateRenderer.Frame = new CoreGraphics.CGRect(margin, formStart + (4 * controlHeight) + (5 * margin), View.Bounds.Width - (2 * margin), controlHeight);;

            // Setup the visual frame for the MapView
            _myMapView.Frame = new CoreGraphics.CGRect(0, 0, View.Bounds.Width, View.Bounds.Height);
        }

        private async void Initialize()
        {
            // Set the altitude slider min/max and initial value
            _Altitude_Slider.MinValue = 0;
            _Altitude_Slider.MaxValue = 90;
            _Altitude_Slider.Value = 45;

            // Set the azimuth slider min/max and initial value
            _Azimuth_Slider.MinValue = 0;
            _Azimuth_Slider.MaxValue = 360;
            _Azimuth_Slider.Value = 180;

            // Load the raster file using a path on disk
            Raster myRasterImagery = new Raster(GetRasterPath_Imagery());

            // Create the raster layer from the raster
            RasterLayer myRasterLayerImagery = new RasterLayer(myRasterImagery);

            // Create a new map using the raster layer as the base map 
            Map myMap = new Map(new Basemap(myRasterLayerImagery));

            // Wait for the layer to load - this enabled being able to obtain the extent information 
            // of the raster layer
            await myRasterLayerImagery.LoadAsync();

            // Create a new EnvelopeBuilder from the full extent of the raster layer 
            EnvelopeBuilder myEnvelopBuilder = new EnvelopeBuilder(myRasterLayerImagery.FullExtent);

            // Zoom in the extent just a bit so that raster layer encompasses the entire viewable area of the map
            myEnvelopBuilder.Expand(0.75);

            // Set the viewpoint of the map to the EnvelopeBuilder's extent
            myMap.InitialViewpoint = new Viewpoint(myEnvelopBuilder.ToGeometry().Extent);

            // Add map to the map view
            _myMapView.Map = myMap;

            // Wait for the map to load
            await myMap.LoadAsync();

            // Enable the 'Update Renderer' button now that the map has loaded
            _UpdateRenderer.Enabled = true;
        }

        private void CreateLayout()
        {
            // This section creates the UI elements and adds them to the layout view of the GUI

            // Create label that displays the Altitude
            _Label_Altitude = new UILabel();
            _Label_Altitude.Text = "Altitude:";
            _Label_Altitude.AdjustsFontSizeToFitWidth = true;

            // Create slider that the user can modify Altitude 
            _Altitude_Slider = new UISlider();

            // Create label that displays the Azimuth
            _Label_Azimuth = new UILabel();
            _Label_Azimuth.Text = "Azimuth:";
            _Label_Azimuth.AdjustsFontSizeToFitWidth = true;

            // Create slider that the user can modify Azimuth 
            _Azimuth_Slider = new UISlider();

            // Get all the SlopeType names from the PresetColorRampType Enumeration and put them 
            // in an array of strings, then set the UITableView.Source to the array
            _SlopeTypes = new UISegmentedControl(Enum.GetNames(typeof(SlopeType)));
            _SlopeTypes.SelectedSegment = 0;

            // Get all the ColorRamp names from the PresetColorRampType Enumeration and put them 
            // in an array of strings, then set the UITableView.Source to the array
            _ColorRamps = new UISegmentedControl(Enum.GetNames(typeof(PresetColorRampType)));
            _ColorRamps.SelectedSegment = 0;

            // Create button to change stretch renderer of the raster
            _UpdateRenderer = new UIButton(UIButtonType.RoundedRect);
            _UpdateRenderer.SetTitle("Update renderer", UIControlState.Normal);
            _UpdateRenderer.SetTitleColor(View.TintColor, UIControlState.Normal);
            // Hook to touch/click event of the button
            _UpdateRenderer.TouchUpInside += OnUpdateRendererClicked;
            _UpdateRenderer.Enabled = false;

            // Add all of the UI controls to the page
            View.AddSubviews( _myMapView, _toolbar, _Label_Altitude, _Altitude_Slider, _Label_Azimuth, _Azimuth_Slider, _SlopeTypes, _ColorRamps, _UpdateRenderer);
        }

        private void OnUpdateRendererClicked(object sender, EventArgs e)
        {
            try
            {
                // Define the RasterLayer that will be used to display in the map
                RasterLayer rasterLayer_ForDisplayInMap;

                // Define the ColorRamp that will be used by the BlendRenderer
                ColorRamp myColorRamp;

                // Get the user choice for the ColorRamps

                string selection = Enum.GetNames(typeof(PresetColorRampType))[_ColorRamps.SelectedSegment];

                // Based on ColorRamp type chosen by the user, create a different
                // RasterLayer and define the appropriate ColorRamp option
                if (selection == "None")
                {
                    // The user chose not to use a specific ColorRamp, therefore 
                    // need to create a RasterLayer based on general imagery (ie. Shasta.tif)
                    // for display in the map and use null for the ColorRamp as one of the
                    // parameters in the BlendRenderer constructor

                    // Load the raster file using a path on disk
                    Raster raster_Imagery = new Raster(GetRasterPath_Imagery());

                    // Create the raster layer from the raster
                    rasterLayer_ForDisplayInMap = new RasterLayer(raster_Imagery);

                    // Set up the ColorRamp as being null
                    myColorRamp = null;
                }
                else
                {

                    // The user chose a specific ColorRamp (options: are Elevation, DemScreen, DemLight), 
                    // therefore create a RasterLayer based on an imagery with elevation 
                    // (ie. Shasta_Elevation.tif) for display in the map. Also create a ColorRamp 
                    // based on the user choice, translated into an Enumeration, as one of the parameters 
                    // in the BlendRenderer constructor

                    // Load the raster file using a path on disk
                    Raster raster_Elevation = new Raster(GetRasterPath_Elevation());

                    // Create the raster layer from the raster
                    rasterLayer_ForDisplayInMap = new RasterLayer(raster_Elevation);

                    // Create a ColorRamp based on the user choice, translated into an Enumeration
                    PresetColorRampType myPresetColorRampType = (PresetColorRampType)Enum.Parse(typeof(PresetColorRampType), selection);
                    myColorRamp = ColorRamp.Create(myPresetColorRampType, 256);
                }


                // Define the parameters used by the BlendRenderer constructor
                Raster raster_ForMakingBlendRenderer = new Raster(GetRasterPath_Elevation());
                IEnumerable<double> myOutputMinValues = new List<double> { 9 };
                IEnumerable<double> myOutputMaxValues = new List<double> { 255 };
                IEnumerable<double> mySourceMinValues = new List<double>();
                IEnumerable<double> mySourceMaxValues = new List<double>();
                IEnumerable<double> myNoDataValues = new List<double>();
                IEnumerable<double> myGammas = new List<double>();

                // Get the user choice for the SlopeType
                string slopeSelection = Enum.GetNames(typeof(SlopeType))[_SlopeTypes.SelectedSegment];

                SlopeType mySlopeType = (SlopeType)Enum.Parse(typeof(SlopeType), slopeSelection);

                BlendRenderer myBlendRenderer = new BlendRenderer(
                    raster_ForMakingBlendRenderer, // elevationRaster - Raster based on a elevation source
                    myOutputMinValues, // outputMinValues - Output stretch values, one for each band
                    myOutputMaxValues, // outputMaxValues - Output stretch values, one for each band
                    mySourceMinValues, // sourceMinValues - Input stretch values, one for each band
                    mySourceMaxValues, // sourceMaxValues - Input stretch values, one for each band
                    myNoDataValues, // noDataValues - NoData values, one for each band
                    myGammas, // gammas - Gamma adjustment
                    myColorRamp, // colorRamp - ColorRamp object to use, could be null
                    _Altitude_Slider.Value, // altitude - Altitude angle of the light source
                    _Azimuth_Slider.Value, // azimuth - Azimuth angle of the light source, measured clockwise from north
                    1, // zfactor - Factor to convert z unit to x,y units, default is 1
                    mySlopeType, // slopeType - Slope Type
                    1, // pixelSizeFactor - Pixel size factor, default is 1
                    1, // pixelSizePower - Pixel size power value, default is 1
                    8); // outputBitDepth - Output bit depth, default is 8-bi

                // Set the RasterLayer.Renderer to be the BlendRenderer
                rasterLayer_ForDisplayInMap.Renderer = myBlendRenderer;

                // Set the new base map to be the RasterLayer with the BlendRenderer applied
                _myMapView.Map.Basemap = new Basemap(rasterLayer_ForDisplayInMap);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static string GetRasterPath_Imagery()
        {
            return DataManager.GetDataFolder("7c4c679ab06a4df19dc497f577f111bd", "raster-file", "Shasta.tif");
        }

        private static string GetRasterPath_Elevation()
        {
            return DataManager.GetDataFolder("caeef9aa78534760b07158bb8e068462", "Shasta_Elevation.tif");
        }
    }
}