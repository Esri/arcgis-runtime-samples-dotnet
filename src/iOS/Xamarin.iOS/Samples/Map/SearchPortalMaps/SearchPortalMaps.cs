// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Helpers;
using ArcGISRuntime.Samples.Shared.Managers;
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.UI.Controls;
using Foundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UIKit;

namespace ArcGISRuntime.Samples.SearchPortalMaps
{
    [Register("SearchPortalMaps")]
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Search for webmap",
        category: "Map",
        description: "Find webmap portal items by using a search term.",
        instructions: "Enter search terms into the search bar. Once the search is complete, a list is populated with the resultant webmaps. Tap on a webmap to set it to the map view. Scrolling to the bottom of the webmap recycler view will get more results.",
        tags: new[] { "keyword", "query", "search", "webmap" })]
    [ArcGISRuntime.Samples.Shared.Attributes.ClassFile("Helpers/ArcGISLoginPrompt.cs")]
    public class SearchPortalMaps : UIViewController
    {
        // Hold references to UI controls.
        private MapView _myMapView;
        private UIBarButtonItem _searchButton;
        private UIBarButtonItem _myMapsButton;
        private bool _myMapsLastClicked;

        // Variables for OAuth configuration and default values.
        // URL of the server to authenticate with.
        private const string ServerUrl = "https://www.arcgis.com/sharing/rest";

        public SearchPortalMaps()
        {
            Title = "Search a portal for maps";
        }

        private async Task Initialize()
        {
            // Remove the API key.
            ApiKeyManager.DisableKey();

            ArcGISLoginPrompt.SetChallengeHandler(this);

            bool loggedIn = await ArcGISLoginPrompt.EnsureAGOLCredentialAsync();

            // Display a default map
            if (loggedIn) _myMapView.Map = new Map(BasemapStyle.ArcGISLightGray);
        }

        private async void GetMyMaps_Clicked(object sender, EventArgs e)
        {
            try
            {
                // For UI popup.
                _myMapsLastClicked = true;

                await GetMyMaps();
            }
            catch (Exception ex)
            {
                UIAlertController alertController = UIAlertController.Create("There was a problem.", ex.ToString(),
                    UIAlertControllerStyle.Alert);
                alertController.AddAction(UIAlertAction.Create("Ok", UIAlertActionStyle.Cancel, null));
                PresentViewController(alertController, true, null);
            }
        }

        private void SearchMaps_Clicked(object sender, EventArgs e)
        {
            // For UI popup.
            _myMapsLastClicked = false;

            // Prompt for the query.
            UIAlertController unionAlert = UIAlertController.Create("Search for maps", "Enter a search term.",
                UIAlertControllerStyle.Alert);
            unionAlert.AddTextField(field => { });
            unionAlert.AddAction(UIAlertAction.Create("Search", UIAlertActionStyle.Default,
                action => SearchTextEntered(unionAlert.TextFields[0].Text)));
            unionAlert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));

            // Show the alert.
            PresentViewController(unionAlert, true, null);
        }

        private async Task GetMyMaps()
        {
            // Call a sub that will force the user to log in to ArcGIS Online (if they haven't already).
            bool loggedIn = await ArcGISLoginPrompt.EnsureAGOLCredentialAsync();
            if (!loggedIn) { return; }

            // Connect to the portal (will connect using the provided credentials).
            ArcGISPortal portal = await ArcGISPortal.CreateAsync(new Uri(ServerUrl));

            // Get the user's content (items in the root folder and a collection of sub-folders).
            PortalUserContent myContent = await portal.User.GetContentAsync();

            // Get the web map items in the root folder.
            IEnumerable<PortalItem> mapItems =
                from item in myContent.Items where item.Type == PortalItemType.WebMap select item;

            // Loop through all sub-folders and get web map items, add them to the mapItems collection.
            foreach (PortalFolder folder in myContent.Folders)
            {
                IEnumerable<PortalItem> folderItems = await portal.User.GetContentAsync(folder.FolderId);
                mapItems = mapItems.Concat(
                    from item in folderItems where item.Type == PortalItemType.WebMap select item);
            }

            // Show the map results.
            ShowMapList(mapItems);
        }

        // Handle the SearchTextEntered event from the search input UI.
        // SearchMapsEventArgs contains the search text that was entered.
        private async void SearchTextEntered(string searchText)
        {
            try
            {
                // Connect to the portal (anonymously).
                ArcGISPortal portal = await ArcGISPortal.CreateAsync(new Uri(ServerUrl));

                // Create a query expression that will get public items of type 'web map' with the keyword(s) in the items tags.
                string queryExpression =
                    $"tags:\"{searchText}\" access:public type: (\"web map\" NOT \"web mapping application\")";

                // Create a query parameters object with the expression and a limit of 10 results.
                PortalQueryParameters queryParams = new PortalQueryParameters(queryExpression, 10);

                // Search the portal using the query parameters and await the results.
                PortalQueryResultSet<PortalItem> findResult = await portal.FindItemsAsync(queryParams);

                // Get the items from the query results.
                IEnumerable<PortalItem> mapItems = findResult.Results;

                // Show the map results.
                ShowMapList(mapItems);
            }
            catch (Exception ex)
            {
                // Report search error.
                UIAlertController alert = UIAlertController.Create("Error", ex.Message, UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                PresentViewController(alert, true, null);
            }
        }

        private void ShowMapList(IEnumerable<PortalItem> webmapItems)
        {
            // Create a new Alert Controller.
            UIAlertController mapListActionSheet =
                UIAlertController.Create("Web maps", "Choose a map", UIAlertControllerStyle.ActionSheet);

            // Add actions to load the available web maps.
            foreach (PortalItem item in webmapItems)
            {
                mapListActionSheet.AddAction(UIAlertAction.Create(item.Title, UIAlertActionStyle.Default,
                    async action => await DisplayMap(item.Url)));
            }

            // Add a choice to cancel.
            mapListActionSheet.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel,
                action => Console.WriteLine("Canceled")));

            // Required for iPad - You must specify a source for the Action Sheet since it is displayed as a popover.
            UIPopoverPresentationController presentationPopover = mapListActionSheet.PopoverPresentationController;
            if (presentationPopover != null)
            {
                presentationPopover.BarButtonItem = _myMapsLastClicked ? _myMapsButton : _searchButton;
                presentationPopover.PermittedArrowDirections = UIPopoverArrowDirection.Down;
            }

            // Display the list of maps.
            PresentViewController(mapListActionSheet, true, null);
        }

        private async Task DisplayMap(Uri webMapUri)
        {
            Map webMap = new Map(webMapUri);
            try
            {
                await webMap.LoadAsync();
            }
            catch (ArcGISRuntimeException e)
            {
                UIAlertView alert =
                    new UIAlertView("Map Load Error", e.Message, (IUIAlertViewDelegate)null, "OK", null);
                alert.Show();
            }

            // Handle change in the load status (to report load errors).
            webMap.LoadStatusChanged += WebMapLoadStatusChanged;

            _myMapView.Map = webMap;
        }

        private void WebMapLoadStatusChanged(object sender, Esri.ArcGISRuntime.LoadStatusEventArgs e)
        {
            Map map = (Map)sender;

            // Report errors if map failed to load.
            if (e.Status == LoadStatus.Loaded)
            {
                // Unsubscribe from event.
                map.LoadStatusChanged -= WebMapLoadStatusChanged;
            }
            else if (e.Status == LoadStatus.FailedToLoad)
            {
                // Unsubscribe from event.
                map.LoadStatusChanged -= WebMapLoadStatusChanged;

                // Show the error
                Exception err = map.LoadError;
                if (err != null)
                {
                    UIAlertView alert = new UIAlertView("Map Load Error", err.Message, (IUIAlertViewDelegate)null,
                        "OK", null);
                    alert.Show();
                }
            }
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            _ = Initialize();
        }

        public override void LoadView()
        {
            // Create the views.
            View = new UIView { BackgroundColor = ApplicationTheme.BackgroundColor };

            _myMapView = new MapView();
            _myMapView.TranslatesAutoresizingMaskIntoConstraints = false;

            _searchButton = new UIBarButtonItem();
            _searchButton.Title = "Search maps";

            _myMapsButton = new UIBarButtonItem();
            _myMapsButton.Title = "Get my maps";

            UIToolbar toolbar = new UIToolbar();
            toolbar.TranslatesAutoresizingMaskIntoConstraints = false;
            toolbar.Items = new[]
            {
                _searchButton,
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                _myMapsButton
            };

            // Add the views.
            View.AddSubviews(_myMapView, toolbar);

            // Lay out the views.
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _myMapView.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
                _myMapView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _myMapView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                _myMapView.BottomAnchor.ConstraintEqualTo(toolbar.TopAnchor),
                toolbar.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                toolbar.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                toolbar.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor)
            });
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);

            // Subscribe to events.
            _searchButton.Clicked += SearchMaps_Clicked;
            _myMapsButton.Clicked += GetMyMaps_Clicked;
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);

            // Unsubscribe from events, per best practice.
            _searchButton.Clicked -= SearchMaps_Clicked;
            _myMapsButton.Clicked -= GetMyMaps_Clicked;

            // Restore API key if leaving sample.
            ApiKeyManager.EnableKey();
        }
    }
}