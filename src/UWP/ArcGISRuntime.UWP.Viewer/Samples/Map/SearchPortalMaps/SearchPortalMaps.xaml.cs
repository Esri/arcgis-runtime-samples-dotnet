// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Helpers;
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;

namespace ArcGISRuntime.UWP.Samples.SearchPortalMaps
{
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Search for webmap",
        category: "Map",
        description: "Find webmap portal items by using a search term.",
        instructions: "Enter search terms into the search bar. Once the search is complete, a list is populated with the resultant webmaps. Tap on a webmap to set it to the map view. Scrolling to the bottom of the webmap recycler view will get more results.",
        tags: new[] { "keyword", "query", "search", "webmap" })]
    [ArcGISRuntime.Samples.Shared.Attributes.ClassFile("Helpers\\ArcGISLoginPrompt.cs")]
    public partial class SearchPortalMaps
    {
        // URL of the server to authenticate with (ArcGIS Online)
        private const string ArcGISOnlineUrl = "https://www.arcgis.com/sharing/rest";

        // Constructor for sample class
        public SearchPortalMaps()
        {
            InitializeComponent();

            _ = Initialize();
        }

        private async Task Initialize()
        {
            ArcGISLoginPrompt.SetChallengeHandler();

            bool loggedIn = await ArcGISLoginPrompt.EnsureAGOLCredentialAsync();

            // Display a default map
            if (loggedIn) DisplayDefaultMap();
        }

        private void DisplayDefaultMap() => MyMapView.Map = new Map(BasemapStyle.ArcGISLightGray);

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get web map portal items in the current user's folder or from a keyword search
                IEnumerable<PortalItem> mapItems;
                ArcGISPortal portal;

                // See if the user wants to search public web map items
                if (SearchPublicMaps.IsChecked == true)
                {
                    // Connect to the portal (anonymously)
                    portal = await ArcGISPortal.CreateAsync();

                    // Create a query expression that will get public items of type 'web map' with the keyword(s) in the items tags
                    string queryExpression = string.Format("tags:\"{0}\" access:public type: (\"web map\" NOT \"web mapping application\")", SearchText.Text);

                    // Create a query parameters object with the expression and a limit of 10 results
                    PortalQueryParameters queryParams = new PortalQueryParameters(queryExpression, 10);

                    // Search the portal using the query parameters and await the results
                    PortalQueryResultSet<PortalItem> findResult = await portal.FindItemsAsync(queryParams);

                    // Get the items from the query results
                    mapItems = findResult.Results;
                }
                else
                {
                    // Call a sub that will force the user to log in to ArcGIS Online (if they haven't already)
                    bool loggedIn = await ArcGISLoginPrompt.EnsureAGOLCredentialAsync();
                    if (!loggedIn) { return; }

                    // Connect to the portal (will connect using the provided credentials)
                    portal = await ArcGISPortal.CreateAsync(new Uri(ArcGISOnlineUrl));

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
                }

                // Show the web map portal items in the list box
                MapListBox.ItemsSource = mapItems;
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.ToString(), "Error").ShowAsync();
            }
        }

        private void LoadMapButtonClick(object sender, RoutedEventArgs e)
        {
            // Get the selected web map item in the list box
            PortalItem selectedMap = MapListBox.SelectedItem as PortalItem;
            if (selectedMap == null)
            {
                return;
            }

            // Create a new map, pass the web map portal item to the constructor
            Map webMap = new Map(selectedMap);

            // Handle change in the load status (to report load errors)
            webMap.LoadStatusChanged += WebMapLoadStatusChanged;

            // Show the web map in the map view
            MyMapView.Map = webMap;
        }

        private async void WebMapLoadStatusChanged(object sender, LoadStatusEventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                // Report errors if map failed to load
                if (e.Status == LoadStatus.FailedToLoad)
                {
                    Map map = (Map)sender;
                    Exception err = map.LoadError;
                    if (err != null)
                    {
                        await new MessageDialog(err.Message, "Map load error").ShowAsync();
                    }
                }
            });
        }
    }
}