﻿// Copyright 2020 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an 
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific 
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Mapping;
using System;
using Xamarin.Forms;

namespace ArcGISRuntimeXamarin.Samples.ShowBookmarksWithToolkit
{
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        "Show bookmarks with toolkit",
        "Toolkit",
        "Use the toolkit's BookmarksView to allow users to navigate to a map's bookmarks.",
        "")]
    public partial class ShowBookmarksWithToolkit : ContentPage
    {
        public ShowBookmarksWithToolkit()
        {
            InitializeComponent();

            // Create and show the map.
            MyMapView.Map = new Map(new Uri("https://arcgisruntime.maps.arcgis.com/home/item.html?id=16f1b8ba37b44dc3884afc8d5f454dd2"));
        }
    }
}
