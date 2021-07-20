// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using ArcGISRuntime.Helpers;
using ArcGISRuntime.Samples.Shared.Managers;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContextThemeWrapper = AndroidX.AppCompat.View.ContextThemeWrapper;

namespace ArcGISRuntime.Samples.AuthorMap
{
    [Activity(ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Create and save map",
        category: "Map",
        description: "Create and save a map as an ArcGIS `PortalItem` (i.e. web map).",
        instructions: "1. Select the basemap and layers you'd like to add to your map.",
        tags: new[] { "ArcGIS Online", "OAuth", "portal", "publish", "share", "web map" })]
    [ArcGISRuntime.Samples.Shared.Attributes.ClassFile("Helpers\\ArcGISLoginPrompt.cs")]
    public class AuthorMap : Activity
    {
        // Hold a reference to the map view
        private MapView _myMapView;

        // Progress bar to show when the app is working
        private ProgressBar _progressBar;

        // String array to store basemap constructor types
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

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Title = "Author and save a map";

            // Create the UI, setup the control references and execute initialization
            CreateLayout();

            _ = Initialize();
        }

        private async Task Initialize()
        {
            // Remove API key.
            ApiKeyManager.DisableKey();

            ArcGISLoginPrompt.SetChallengeHandler();

            bool loggedIn = await ArcGISLoginPrompt.EnsureAGOLCredentialAsync(this);

            // Show a plain gray map in the map view
            if (loggedIn)
            {
                _myMapView.Map = new Map(BasemapStyle.ArcGISLightGray);
            }
            else _myMapView.Map = new Map();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Restore API key if leaving sample.
            ApiKeyManager.EnableKey();
        }

        private void CreateLayout()
        {
            // Create a horizontal layout for the buttons at the top
            LinearLayout buttonLayout = new LinearLayout(this) { Orientation = Orientation.Horizontal };

            // Create a progress bar (circle) to show when the app is working
            _progressBar = new ProgressBar(this)
            {
                Indeterminate = true,
                Visibility = ViewStates.Invisible
            };

            // Create button to clear the map from the map view (start over)
            Button newMapButton = new Button(this)
            {
                Text = "New"
            };
            newMapButton.Click += OnNewMapClicked;

            // Create button to show available basemap
            Button basemapButton = new Button(this)
            {
                Text = "Basemap"
            };
            basemapButton.Click += OnBasemapsClicked;

            // Create a button to show operational layers
            Button layersButton = new Button(this)
            {
                Text = "Layers"
            };
            layersButton.Click += OnLayersClicked;

            // Create a button to save the map
            Button saveMapButton = new Button(this)
            {
                Text = "Save ..."
            };
            saveMapButton.Click += OnSaveMapClicked;

            // Add progress bar, new map, basemap, layers, and save buttons to the layout
            buttonLayout.AddView(newMapButton);
            buttonLayout.AddView(basemapButton);
            buttonLayout.AddView(layersButton);
            buttonLayout.AddView(saveMapButton);
            buttonLayout.AddView(_progressBar);

            // Create a new vertical layout for the app (buttons followed by map view)
            LinearLayout mainLayout = new LinearLayout(this) { Orientation = Orientation.Vertical };

            // Add the button layout
            mainLayout.AddView(buttonLayout);

            // Add the map view to the layout
            _myMapView = new MapView(this);
            mainLayout.AddView(_myMapView);

            // Show the layout in the app
            SetContentView(mainLayout);
        }

        private void OnNewMapClicked(object sender, EventArgs e)
        {
            // Create a new map for the map view (can make changes and save as a new portal item)
            _myMapView.Map = new Map(BasemapStyle.ArcGISLightGray);
        }

        private void OnSaveMapClicked(object sender, EventArgs e)
        {
            // Create a dialog to show save options (title, description, and tags)
            SaveDialogFragment saveMapDialog = new SaveDialogFragment(_myMapView.Map.Item as PortalItem);
            saveMapDialog.OnSaveClicked += SaveMapAsync;

            // Begin a transaction to show a UI fragment (the save dialog)
            FragmentTransaction trans = FragmentManager.BeginTransaction();
            saveMapDialog.Show(trans, "save map");
        }

        private async void SaveMapAsync(object sender, OnSaveMapEventArgs e)
        {
            AlertDialog.Builder alertBuilder = new AlertDialog.Builder(this);

            // Get the current map
            Map myMap = _myMapView.Map;

            try
            {
                // Show the progress bar so the user knows work is happening
                _progressBar.Visibility = ViewStates.Visible;

                // Get information entered by the user for the new portal item properties
                string title = e.MapTitle;
                string description = e.MapDescription;
                string[] tags = e.Tags;

                // Apply the current extent as the map's initial extent
                myMap.InitialViewpoint = _myMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

                // Export the current map view for the item's thumbnail
                RuntimeImage thumbnailImg = await _myMapView.ExportImageAsync();

                // See if the map has already been saved (has an associated portal item)
                if (myMap.Item == null)
                {
                    // Call a function to save the map as a new portal item
                    await SaveNewMapAsync(myMap, title, description, tags, thumbnailImg);

                    // Report a successful save
                    alertBuilder.SetTitle("Map saved");
                    alertBuilder.SetMessage("Saved '" + title + "' to ArcGIS Online!");
                    alertBuilder.Show();
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
                    alertBuilder.SetTitle("Updates saved");
                    alertBuilder.SetMessage("Saved changes to '" + myMap.Item.Title + "'");
                    alertBuilder.Show();
                }
            }
            catch (Exception ex)
            {
                // Show the exception message
                alertBuilder.SetTitle("Unable to save map");
                alertBuilder.SetMessage(ex.Message);
                alertBuilder.Show();
            }
            finally
            {
                // Hide the progress bar
                _progressBar.Visibility = ViewStates.Invisible;
            }
        }

        private async Task SaveNewMapAsync(Map myMap, string title, string description, string[] tags, RuntimeImage img)
        {
            await ArcGISLoginPrompt.EnsureAGOLCredentialAsync(this);

            // Get the ArcGIS Online portal (will use credential from login above)
            ArcGISPortal agsOnline = await ArcGISPortal.CreateAsync();

            // Save the current state of the map as a portal item in the user's default folder
            await myMap.SaveAsAsync(agsOnline, null, title, description, tags, img);
        }

        #region Basemap Button

        private void OnBasemapsClicked(object sender, EventArgs e)
        {
            Button mapsButton = (Button)sender;

            // Create a menu to show basemaps
            PopupMenu mapsMenu = new PopupMenu(mapsButton.Context, mapsButton);
            mapsMenu.MenuItemClick += OnBasemapsMenuItemClicked;

            // Create a menu option for each basemap type
            foreach (string basemapType in _basemapNames)
            {
                mapsMenu.Menu.Add(basemapType);
            }

            // Show menu in the view
            mapsMenu.Show();
        }

        private void OnBasemapsMenuItemClicked(object sender, PopupMenu.MenuItemClickEventArgs e) => ApplyBasemap(e.Item.TitleCondensedFormatted.ToString());

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

        #endregion Basemap Button

        #region Layers Button

        private void OnLayersClicked(object sender, EventArgs e)
        {
            Button layerButton = (Button)sender;

            // Create menu to show layers
            PopupMenu layerMenu = new PopupMenu(layerButton.Context, layerButton);
            layerMenu.MenuItemClick += OnLayerMenuItemClicked;

            // Create menu options
            foreach (string layerInfo in _operationalLayerUrls.Keys)
            {
                layerMenu.Menu.Add(layerInfo);
            }

            // Show menu in the view
            layerMenu.Show();
        }

        private void OnLayerMenuItemClicked(object sender, PopupMenu.MenuItemClickEventArgs e)
        {
            // Get the title of the selected item
            string selectedLayerName = e.Item.TitleCondensedFormatted.ToString();

            // See if the layer already exists
            ArcGISMapImageLayer layer = _myMapView.Map.OperationalLayers.FirstOrDefault(l => l.Name == selectedLayerName) as ArcGISMapImageLayer;

            // If the layer is in the map, remove it
            if (layer != null)
            {
                _myMapView.Map.OperationalLayers.Remove(layer);
            }
            else
            {
                // Get the URL for this layer
                string layerUrl = _operationalLayerUrls[selectedLayerName];
                Uri layerUri = new Uri(layerUrl);

                // Create a new map image layer
                layer = new ArcGISMapImageLayer(layerUri)
                {
                    Name = selectedLayerName,

                    // Set it 50% opaque, and add it to the map
                    Opacity = 0.5
                };
                _myMapView.Map.OperationalLayers.Add(layer);
            }
        }

        #endregion Layers Button
    }

    // A custom DialogFragment class to show input controls for saving a web map
    public class SaveDialogFragment : DialogFragment
    {
        // Inputs for portal item title, description, and tags
        private EditText _mapTitleTextbox;
        private EditText _mapDescriptionTextbox;
        private EditText _tagsTextbox;

        // Store any existing portal item (for "update" versus "save", e.g.)
        private PortalItem _portalItem = null;

        // Raise an event so the listener can access input values when the form has been completed
        public event EventHandler<OnSaveMapEventArgs> OnSaveClicked;

        public SaveDialogFragment(PortalItem mapItem)
        {
            _portalItem = mapItem;
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Dialog to display
            LinearLayout dialogView = null;

            // Get the context for creating the dialog controls
            Android.Content.Context ctx = Activity.ApplicationContext;
            ContextThemeWrapper ctxWrapper = new ContextThemeWrapper(ctx, Android.Resource.Style.ThemeMaterialLight);

            // Set a dialog title
            Dialog.SetTitle("Save map to Portal");

            try
            {
                base.OnCreateView(inflater, container, savedInstanceState);

                // The container for the dialog is a vertical linear layout
                dialogView = new LinearLayout(ctxWrapper)
                {
                    Orientation = Orientation.Vertical
                };
                dialogView.SetPadding(10, 0, 10, 10);

                // Add a text box for entering a title for the new web map
                _mapTitleTextbox = new EditText(ctxWrapper)
                {
                    Hint = "Title"
                };
                dialogView.AddView(_mapTitleTextbox);

                // Add a text box for entering a description
                _mapDescriptionTextbox = new EditText(ctxWrapper)
                {
                    Hint = "Description"
                };
                dialogView.AddView(_mapDescriptionTextbox);

                // Add a text box for entering tags (populate with some values so the user doesn't have to fill this in)
                _tagsTextbox = new EditText(ctxWrapper)
                {
                    Text = "ArcGIS Runtime, Web Map"
                };
                dialogView.AddView(_tagsTextbox);

                // Add a button to save the map
                Button saveMapButton = new Button(ctxWrapper)
                {
                    Text = "Save"
                };
                saveMapButton.Click += SaveMapButtonClick;
                dialogView.AddView(saveMapButton);

                // If there's an existing portal item, configure the dialog for "update" (read-only entries)
                if (_portalItem != null)
                {
                    _mapTitleTextbox.Text = _portalItem.Title;
                    _mapTitleTextbox.Enabled = false;

                    _mapDescriptionTextbox.Text = _portalItem.Description;
                    _mapDescriptionTextbox.Enabled = false;

                    _tagsTextbox.Text = string.Join(",", _portalItem.Tags);
                    _tagsTextbox.Enabled = false;

                    // Change some of the control text
                    Dialog.SetTitle("Save changes to map");
                    saveMapButton.Text = "Update";
                }
            }
            catch (Exception ex)
            {
                // Show the exception message
                AlertDialog.Builder alertBuilder = new AlertDialog.Builder(Activity);
                alertBuilder.SetTitle("Error");
                alertBuilder.SetMessage(ex.Message);
                alertBuilder.Show();
            }

            // Return the new view for display
            return dialogView;
        }

        // A click handler for the save map button
        private void SaveMapButtonClick(object sender, EventArgs e)
        {
            try
            {
                // Get information for the new portal item
                string title = _mapTitleTextbox.Text;
                string description = _mapDescriptionTextbox.Text;
                string[] tags = _tagsTextbox.Text.Split(',');

                // Make sure all required info was entered
                if (String.IsNullOrEmpty(title) || String.IsNullOrEmpty(description) || tags.Length == 0)
                {
                    throw new Exception("Please enter a title, description, and some tags to describe the map.");
                }

                // Create a new OnSaveMapEventArgs object to store the information entered by the user
                OnSaveMapEventArgs mapSavedArgs = new OnSaveMapEventArgs(title, description, tags);

                // Raise the OnSaveClicked event so the main activity can handle the event and save the map
                OnSaveClicked?.Invoke(this, mapSavedArgs);

                // Close the dialog
                Dismiss();
            }
            catch (Exception ex)
            {
                // Show the exception message (dialog will stay open so user can try again)
                AlertDialog.Builder alertBuilder = new AlertDialog.Builder(Activity);
                alertBuilder.SetTitle("Error");
                alertBuilder.SetMessage(ex.Message);
                alertBuilder.Show();
            }
        }
    }

    // Custom EventArgs class for containing portal item properties when saving a map
    public class OnSaveMapEventArgs : EventArgs
    {
        // Portal item title
        public string MapTitle { get; set; }

        // Portal item description
        public string MapDescription { get; set; }

        // Portal item tags
        public string[] Tags { get; set; }

        public OnSaveMapEventArgs(string title, string description, string[] tags) : base()
        {
            MapTitle = title;
            MapDescription = description;
            Tags = tags;
        }
    }
}