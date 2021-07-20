// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Samples.Managers;
using ArcGISRuntime.Samples.Shared.Models;
using Forms.Helpers;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Forms;

#if WINDOWS_UWP
using System.Threading.Tasks;
#endif

namespace ArcGISRuntime
{
    public partial class SampleListPage
    {
        private readonly string _categoryName;
        private List<SampleInfo> _listSampleItems;

        public SampleListPage(string name)
        {
            _categoryName = name;

            Initialize();

            InitializeComponent();

            Title = _categoryName;
        }

        private void Initialize()
        {
            // Get the list of sample categories.
            List<object> sampleCategories = SampleManager.Current.FullTree.Items;

            // Get the tree node for this category.
            var category = sampleCategories.FirstOrDefault(x => ((SearchableTreeNode)x).Name == _categoryName) as SearchableTreeNode;

            // Get the samples from the category.
            _listSampleItems = category?.Items.OfType<SampleInfo>().ToList();

            // Update the binding to show the samples.
            BindingContext = _listSampleItems;
        }

        public void OnItemTapped(object sender, ItemTappedEventArgs e)
        {
            var sampleInfo = e.Item as SampleInfo;
            _ = SampleLoader.LoadSample(sampleInfo, this);
        }
    }
}