// Copyright 2019 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an 
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific 
// language governing permissions and limitations under the License.

using Android.App;
using Android.OS;
using Android.Widget;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.ArcGISServices;
using Esri.ArcGISRuntime.UI.Controls;
using ArcGISRuntime;
using System;
using Esri.ArcGISRuntime.Ogc;
using System.Collections.Generic;
using System.Linq;
using Android.Content;
using Android.Views;
using Android.Media;
using Java.Lang;
using ArcGISRuntime.Samples.Managers;
using System.IO;
using Android;
using Resource = ArcGISRuntime.Resource;
using Android.Support.V4.App;
using Android.Support.V4.Content;

namespace ArcGISRuntimeXamarin.Samples.CreateAndSaveKmlFile
{
    [Activity (ConfigurationChanges=Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        "Create and save KML file",
        "Layers",
        "Construct a KML document and save it as a KMZ file.",
        "")]
    [ArcGISRuntime.Samples.Shared.Attributes.AndroidLayout("CreateAndSaveKmlFile.axml", "icon_item.axml")]
    [ArcGISRuntime.Samples.Shared.Attributes.OfflineData()]
    public class CreateAndSaveKmlFile : Activity
    {
        // Hold references to the UI controls.
        private MapView _myMapView;
        private Button _addButton;
        private Button _saveButton;
        private Button _resetButton;
        private Button _completeButton;
        private LinearLayout _buttonLayout;
        private LinearLayout _pickerLayout;
        private TextView _status;
        private ListView _listView;

        private KmlDocument _kmlDocument;
        private KmlDataset _kmlDataset;
        private KmlLayer _kmlLayer;
        private KmlPlacemark _currentPlacemark;

        private List<string> iconLinks;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Title = "Create and save KML file";

            CreateLayout();
            Initialize();
        }

        private void Initialize()
        {
            // Create the map.
            _myMapView.Map = new Map(Basemap.CreateImagery());

            // Set the colors for the color picker.


            // Set the images for the point icon picker.
            iconLinks = new List<string>()
            {
                "https://static.arcgis.com/images/Symbols/Shapes/BlueCircleLargeB.png",
                "https://static.arcgis.com/images/Symbols/Shapes/BlueDiamondLargeB.png",
                "https://static.arcgis.com/images/Symbols/Shapes/BluePin1LargeB.png",
                "https://static.arcgis.com/images/Symbols/Shapes/BluePin2LargeB.png",
                "https://static.arcgis.com/images/Symbols/Shapes/BlueSquareLargeB.png",
                "https://static.arcgis.com/images/Symbols/Shapes/BlueStarLargeB.png"
            };

            // Set up a new kml document and kml layer.
            ResetKml();
        }

        private void ResetKml()
        {
            // Clear any existing layers from the map.
            _myMapView.Map.OperationalLayers.Clear();

            // Reset the most recently placed placemark.
            _currentPlacemark = null;

            // Create a new KmlDocument.
            _kmlDocument = new KmlDocument() { Name = "KML Sample Document" };

            // Create a Kml dataset using the Kml document.
            _kmlDataset = new KmlDataset(_kmlDocument);

            // Create the kmlLayer using the kmlDataset.
            _kmlLayer = new KmlLayer(_kmlDataset);

            // Add the Kml layer to the map.
            _myMapView.Map.OperationalLayers.Add(_kmlLayer);
        }

        private void Add_Click(object sender, EventArgs e)
        {
            string[] options = new string[] { "Point", "Polyline", "Polygon"};

            // Create UI for terminal selection.
            AlertDialog.Builder builder = new AlertDialog.Builder(this);
            builder.SetItems(options, Draw);
            builder.SetCancelable(false);
            builder.Show();
        }

        private async void Draw(object sender, DialogClickEventArgs e)
        {
            try
            {
                // Hide the base UI and enable the complete button.
                _buttonLayout.Visibility = ViewStates.Invisible;
                _completeButton.Visibility = ViewStates.Visible;

                // Create variables for the sketch creation mode and color.
                SketchCreationMode creationMode;

                // Set the creation mode and UI based on which button called this method.
                switch (e.Which)
                {
                    case 0:
                        creationMode = SketchCreationMode.Point;
                        _status.Text = "Tap to add a point.";
                        //StyleText.Text = "Select an icon for the placemark.";
                        break;

                    case 1:
                        creationMode = SketchCreationMode.Polyline;
                        _status.Text = "Tap to add a vertex.";
                        //StyleText.Text = "Select a color for the placemark.";
                        break;

                    case 2:
                        creationMode = SketchCreationMode.Polygon;
                        _status.Text = "Tap to add a vertex.";
                        //StyleText.Text = "Select a color for the placemark.";
                        break;

                    default:
                        return;
                }

                // Get the user-drawn geometry.
                Geometry geometry = await _myMapView.SketchEditor.StartAsync(creationMode, true);

                // Project the geometry to WGS84 (WGS84 is required by the KML standard).
                Geometry projectedGeometry = GeometryEngine.Project(geometry, SpatialReferences.Wgs84);

                // Create a KmlGeometry using the new geometry.
                KmlGeometry kmlGeometry = new KmlGeometry(projectedGeometry, KmlAltitudeMode.ClampToGround);

                // Create a new placemark.
                _currentPlacemark = new KmlPlacemark(kmlGeometry);

                // Add the placemark to the KmlDocument.
                _kmlDocument.ChildNodes.Add(_currentPlacemark);

                // Enable the style editing UI.
                //StyleBorder.Visibility = Visibility.Visible;
                //MainUI.IsEnabled = false;

                // Choose whether to enable the icon picker or color picker.
                if (creationMode == SketchCreationMode.Point) { OpenIconDialog(); }
                else { OpenColorDialog(); }
            }
            finally
            {
                // Reset the UI.
                _buttonLayout.Visibility = ViewStates.Visible;
                _completeButton.Visibility = ViewStates.Invisible;
                _status.Text = "Select the type of feature you would like to add.";
            }
        }
        private void Complete_Click(object sender, EventArgs e)
        {
            try
            {
                // Finish the sketch.
                _myMapView.SketchEditor.CompleteCommand.Execute(null);
            }
            catch (ArgumentException)
            {
            }
        }
        private void OpenIconDialog()
        {
            IList<IDictionary<string, object>> data = new Android.Runtime.JavaList<IDictionary<string, object>>();
            foreach(var link in iconLinks)
            {
                var image = new ImageView(this);
                image.SetImageURI(Android.Net.Uri.Parse(link));
                var dict = new Android.Runtime.JavaDictionary<string, object>();
                dict.Add("image", image);
                dict.Add("name", link);
                data.Add(dict);
            }
            var adapter = new SimpleAdapter(this, data, Resource.Layout.icon_item, new string[] { "name", "image" }, new int[] { Resource.Id.textView, Resource.Id.imageView});
            _listView.Adapter = adapter;
            
            _pickerLayout.Visibility = ViewStates.Visible;
            _listView.ItemClick += IconSelected;
        }

        private void IconSelected(object sender, AdapterView.ItemClickEventArgs e)
        {
            _currentPlacemark.Style = new KmlStyle();
            _currentPlacemark.Style.IconStyle = new KmlIconStyle(new KmlIcon(new Uri(iconLinks[(int)e.Id])), 1.0);

            _pickerLayout.Visibility = ViewStates.Invisible;
        }

        private void OpenColorDialog()
        {

        }

        private async void Save_Click(object sender, EventArgs e)
        {
            // Determine where to save your file
            string filePath = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, Android.OS.Environment.DirectoryDownloads, "sampledata.kmz");
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.WriteExternalStorage) != Android.Content.PM.Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.WriteExternalStorage }, 1);
            }
            else
            {
                using (System.IO.Stream stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    // Write the KML document to the stream of the file.
                    await _kmlDocument.WriteToAsync(stream);
                }
                new AlertDialog.Builder(this).SetMessage("File saved to " + filePath).SetTitle("Success!").Show();
            }
        }

        private void Reset_Click(object sender, EventArgs e)
        {
            ResetKml();
        }


        private void CreateLayout()
        {
            // Create a new vertical layout for the app.
            SetContentView(Resource.Layout.CreateAndSaveKmlFile);

            _myMapView = FindViewById<MapView>(Resource.Id.MapView);

            _addButton = FindViewById<Button>(Resource.Id.addButton);
            _saveButton = FindViewById<Button>(Resource.Id.saveButton);
            _resetButton = FindViewById<Button>(Resource.Id.resetButton);
            _completeButton = FindViewById<Button>(Resource.Id.completeButton);
            _buttonLayout = FindViewById<LinearLayout>(Resource.Id.linearLayout);
            _pickerLayout = FindViewById<LinearLayout>(Resource.Id.PickerUI);
            _status = FindViewById<TextView>(Resource.Id.statusLabel);
            _listView = FindViewById<ListView>(Resource.Id.listView);

            _addButton.Click += Add_Click;
            _saveButton.Click += Save_Click;
            _resetButton.Click += Reset_Click;
            _completeButton.Click += Complete_Click;

        }
    }
}
