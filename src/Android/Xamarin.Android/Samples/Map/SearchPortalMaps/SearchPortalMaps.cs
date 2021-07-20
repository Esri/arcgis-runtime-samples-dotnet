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
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContextThemeWrapper = AndroidX.AppCompat.View.ContextThemeWrapper;

namespace ArcGISRuntime.Samples.SearchPortalMaps
{
    [Activity(ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Search for webmap",
        category: "Map",
        description: "Find webmap portal items by using a search term.",
        instructions: "Enter search terms into the search bar. Once the search is complete, a list is populated with the resultant webmaps. Tap on a webmap to set it to the map view. Scrolling to the bottom of the webmap recycler view will get more results.",
        tags: new[] { "keyword", "query", "search", "webmap" })]
    [ArcGISRuntime.Samples.Shared.Attributes.ClassFile("Helpers\\ArcGISLoginPrompt.cs")]
    public class SearchPortalMaps : Activity
    {
        // Hold a reference to the map view
        private MapView _myMapView;

        // Dictionary to hold URIs to web maps
        private Dictionary<string, Uri> _webMapUris;

        // URL of the server.
        private const string ServerUrl = "https://www.arcgis.com/sharing/rest";

        // Button layout at the top of the page
        private LinearLayout _buttonPanel;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Title = "Search a portal for maps";

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

            // Display a default map
            if (loggedIn) DisplayDefaultMap();
        }

        private void DisplayDefaultMap() => _myMapView.Map = new Map(BasemapStyle.ArcGISLightGray);

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Restore API key if leaving sample.
            ApiKeyManager.EnableKey();
        }

        private void SearchMapsClicked(object sender, EventArgs e)
        {
            // Create a dialog to show save options (title, description, and tags)
            SearchMapsDialogFragment searchMapsDialog = new SearchMapsDialogFragment();
            searchMapsDialog.OnSearchClicked += OnSearchMapsClicked;

            // Begin a transaction to show a UI fragment (the search dialog)
            FragmentTransaction trans = FragmentManager.BeginTransaction();
            searchMapsDialog.Show(trans, "search maps");
        }

        private async void MyMapsClicked(object sender, EventArgs e)
        {
            try
            {
                // Get web map portal items in the current user's folder
                IEnumerable<PortalItem> mapItems = null;

                // Call a sub that will force the user to log in to ArcGIS Online (if they haven't already)
                bool loggedIn = await ArcGISLoginPrompt.EnsureAGOLCredentialAsync(this);
                if (!loggedIn) { return; }

                // Connect to the portal (will connect using the provided credentials)
                ArcGISPortal portal = await ArcGISPortal.CreateAsync(new Uri(ServerUrl));

                // Get the user's content (items in the root folder and a collection of sub-folders)
                PortalUserContent myContent = await portal.User.GetContentAsync();

                // Get the web map items in the root folder
                mapItems = from item in myContent.Items where item.Type == PortalItemType.WebMap select item;

                // Loop through all sub-folders and get web map items, add them to the mapItems collection
                foreach (PortalFolder folder in myContent.Folders)
                {
                    IEnumerable<PortalItem> folderItems = await portal.User.GetContentAsync(folder.FolderId);
                    mapItems = mapItems.Concat(from item in folderItems where item.Type == PortalItemType.WebMap select item);
                }

                // Show the map results
                ShowMapList(mapItems);
            }
            catch (Exception ex)
            {
                new AlertDialog.Builder(this).SetMessage(ex.ToString()).SetTitle("Error").Show();
            }
        }

        private async void OnSearchMapsClicked(object sender, OnSearchMapEventArgs e)
        {
            try
            {
                // Get web map portal items from a keyword search
                IEnumerable<PortalItem> mapItems = null;

                // Connect to the portal (anonymously)
                ArcGISPortal portal = await ArcGISPortal.CreateAsync(new Uri(ServerUrl));

                // Create a query expression that will get public items of type 'web map' with the keyword(s) in the items tags
                string queryExpression = $"tags:\"{e.SearchText}\" access:public type: (\"web map\" NOT \"web mapping application\")";

                // Create a query parameters object with the expression and a limit of 10 results
                PortalQueryParameters queryParams = new PortalQueryParameters(queryExpression, 10);

                // Search the portal using the query parameters and await the results
                PortalQueryResultSet<PortalItem> findResult = await portal.FindItemsAsync(queryParams);

                // Get the items from the query results
                mapItems = findResult.Results;

                // Show the map results
                ShowMapList(mapItems);
            }
            catch (Exception ex)
            {
                new AlertDialog.Builder(this).SetMessage(ex.ToString()).SetTitle("Error").Show();
            }
        }

        private void ShowMapList(IEnumerable<PortalItem> webmapItems)
        {
            // Create menu to show map results
            PopupMenu mapsMenu = new PopupMenu(this, _buttonPanel);
            mapsMenu.MenuItemClick += OnMapsMenuItemClicked;

            // Create a dictionary of web maps and show the titles in the menu
            _webMapUris = new Dictionary<string, Uri>();
            foreach (PortalItem item in webmapItems)
            {
                if (!_webMapUris.ContainsKey(item.Title))
                {
                    _webMapUris.Add(item.Title, item.Url);
                    mapsMenu.Menu.Add(item.Title);
                }
            }

            //Show menu in the view
            mapsMenu.Show();
        }

        private void OnMapsMenuItemClicked(object sender, PopupMenu.MenuItemClickEventArgs e)
        {
            // Get the selected web map item URI from the dictionary
            string mapTitle = e.Item.TitleCondensedFormatted.ToString();
            Uri selectedMapUri = _webMapUris[mapTitle];

            if (selectedMapUri == null) { return; }

            // Create a new map, pass the web map portal item to the constructor
            Map webMap = new Map(selectedMapUri);

            // Handle change in the load status (to report load errors)
            webMap.LoadStatusChanged += WebMapLoadStatusChanged;

            // Show the web map in the map view
            _myMapView.Map = webMap;
        }

        private void WebMapLoadStatusChanged(object sender, Esri.ArcGISRuntime.LoadStatusEventArgs e)
        {
            // Report errors if map failed to load
            if (e.Status == LoadStatus.FailedToLoad)
            {
                Map map = (Map)sender;
                Exception err = map.LoadError;
                if (err != null)
                {
                    AlertDialog.Builder alertBuilder = new AlertDialog.Builder(this);
                    alertBuilder.SetTitle("Map Load Error");
                    alertBuilder.SetMessage(err.Message);
                    alertBuilder.Show();
                }
            }
        }

        private void CreateLayout()
        {
            // Create a new vertical layout for the app
            LinearLayout mainLayout = new LinearLayout(this) { Orientation = Orientation.Vertical };

            // Create a layout for app buttons
            _buttonPanel = new LinearLayout(this) { Orientation = Orientation.Horizontal };

            // Create button to show search UI
            Button searchMapsButton = new Button(this)
            {
                Text = "Search Maps"
            };
            searchMapsButton.Click += SearchMapsClicked;

            // Create another button to show maps from user's ArcGIS Online account
            Button myMapsButton = new Button(this)
            {
                Text = "My Maps"
            };
            myMapsButton.Click += MyMapsClicked;

            // Add buttons to the horizontal layout panel
            _buttonPanel.AddView(searchMapsButton);
            _buttonPanel.AddView(myMapsButton);

            // Add button panel to the main layout
            mainLayout.AddView(_buttonPanel);

            // Add the map view to the layout
            _myMapView = new MapView(this);
            mainLayout.AddView(_myMapView);

            // Show the layout in the app
            SetContentView(mainLayout);
        }
    }

    #region UI for entering web map search text

    // A custom DialogFragment class to show input controls for searching for maps
    public class SearchMapsDialogFragment : DialogFragment
    {
        // Inputs for portal item search text
        private EditText _mapSearchTextbox;

        // Raise an event so the listener can access the input search text value when the form has been completed
        public event EventHandler<OnSearchMapEventArgs> OnSearchClicked;

        public SearchMapsDialogFragment()
        {
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Dialog to display
            LinearLayout dialogView = null;

            // Get the context for creating the dialog controls
            Android.Content.Context ctx = Activity.ApplicationContext;
            ContextThemeWrapper ctxWrapper = new ContextThemeWrapper(ctx, Android.Resource.Style.ThemeMaterialLight);

            // Set a dialog title
            Dialog.SetTitle("Search Portal");

            try
            {
                base.OnCreateView(inflater, container, savedInstanceState);

                // The container for the dialog is a vertical linear layout
                dialogView = new LinearLayout(ctxWrapper)
                {
                    Orientation = Orientation.Vertical
                };

                // Add a text box for entering web map search text
                _mapSearchTextbox = new EditText(ctxWrapper)
                {
                    Hint = "Search text"
                };
                dialogView.AddView(_mapSearchTextbox);

                // Add a button to complete search
                Button searchMapsButton = new Button(ctxWrapper)
                {
                    Text = "Search"
                };
                searchMapsButton.Click += SearchMapsButtonClick;
                dialogView.AddView(searchMapsButton);
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

        // A click handler for the search button
        private void SearchMapsButtonClick(object sender, EventArgs e)
        {
            try
            {
                // Get information for the new portal item
                string search = _mapSearchTextbox.Text;

                // Create a new OnSaveMapEventArgs object to store the information entered by the user
                OnSearchMapEventArgs mapSearchArgs = new OnSearchMapEventArgs(search);

                // Raise the OnSaveClicked event so the main activity can handle the event and save the map
                OnSearchClicked?.Invoke(this, mapSearchArgs);

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

    // Custom EventArgs class for containing map search expression text
    public class OnSearchMapEventArgs : EventArgs
    {
        // Search text
        public string SearchText { get; set; }

        public OnSearchMapEventArgs(string searchText) : base()
        {
            // Store the web map search text
            SearchText = searchText;
        }
    }

    #endregion UI for entering web map search text
}