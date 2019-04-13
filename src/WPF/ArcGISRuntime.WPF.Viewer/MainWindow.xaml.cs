// Copyright 2018 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Samples.Managers;
using ArcGISRuntime.Samples.Shared.Models;
using Esri.ArcGISRuntime.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ArcGISRuntime.Samples.Desktop
{
    public partial class MainWindow
    {
        private bool _waitFlag;
        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                SampleManager.Current.Initialize();

                // Set category data context
                var samples = WPF.Viewer.Helpers.ToTreeViewItem(SampleManager.Current.FullTree);
                Categories.DataContext = samples;

                // Select the first item
                samples.First().IsSelected = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Initialization exception occurred.");
            }
        }

        private async void categoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var sample = e.AddedItems[0] as SampleInfo;
                ((ListView)sender).SelectedItem = null;
                DetailsRegion.Visibility = Visibility.Visible;
                CategoriesRegion.Visibility = Visibility.Collapsed;
                await SelectSample(sample);

                // Deselect all categories
                ((List<TreeViewItem>)Categories.DataContext).ForEach(item => item.IsSelected = false);
            }
        }

        private async void categories_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var context = e.NewValue as TreeViewItem;
            if (context == null) {return;}
            var sample = context.DataContext as SampleInfo;
            var category = context.DataContext as SearchableTreeNode;
            if (category != null)
            {
                CategoriesList.ItemsSource = category.Items;
                DetailsRegion.Visibility = Visibility.Collapsed;
                CategoriesRegion.Visibility = Visibility.Visible;
            }
            else if (sample != null)
            {
                DetailsRegion.Visibility = Visibility.Visible;
                CategoriesRegion.Visibility = Visibility.Collapsed;
                await SelectSample(sample);
            }
        }

        private async Task SelectSample(SampleInfo selectedSample)
        {
            if (selectedSample == null) return;
#if TELEMETRY
            WPF.Viewer.Telemetry.DiagnosticsClient.TrackPageView(selectedSample.SampleType.FullName);
#endif

            SampleManager.Current.SelectedSample = selectedSample;
            DescriptionContainer.SetSample(selectedSample);

            // Call a function to clear any existing credentials from AuthenticationManager
            ClearCredentials();

            try
            {
                if (selectedSample.OfflineDataItems != null)
                {
                    // Show waiting page
                    SampleContainer.Content = new WPF.Viewer.WaitPage();

                    // Wait for offline data to complete
                    await DataManager.EnsureSampleDataPresent(selectedSample);
                }

                // Show the sample
                SampleContainer.Content = SampleManager.Current.SampleToControl(selectedSample);
#if TELEMETRY
                WPF.Viewer.Telemetry.DiagnosticsClient.TrackEvent($"{selectedSample.SampleType.FullName} loaded");
#endif
                SourceCodeContainer.LoadSourceCode();
            }
            catch (Exception exception)
            {
                // failed to create new instance of the sample
                SampleContainer.Content = new WPF.Viewer.ErrorPage(exception);
#if TELEMETRY
                WPF.Viewer.Telemetry.DiagnosticsClient.Notify(exception, new Dictionary<string, string>() { { "Sample", selectedSample.SampleType.FullName } });
#endif
            }
            CategoriesRegion.Visibility = Visibility.Collapsed;
            SampleContainer.Visibility = Visibility.Visible;
        }

        private static void ClearCredentials()
        {
            // Clear credentials (if any) from previous sample runs
            foreach (var cred in AuthenticationManager.Current.Credentials)
            {
                if (cred != null)
                {
                    AuthenticationManager.Current.RemoveCredential(cred);
                }
            }
        }

        private void OpenCategoryLeaves()
        {
            if (Categories.Items.Count > 0)
            {
                var firstTreeViewItem = Categories.Items[0] as TreeViewItem;
                if (firstTreeViewItem != null) firstTreeViewItem.IsSelected = true;

                foreach (var item in Categories.Items)
                {
                    var treeViewItem = item as TreeViewItem;
                    if (treeViewItem != null) treeViewItem.IsExpanded = true;
                }
            }
        }

        private void LiveSample_Click(object sender, RoutedEventArgs e)
        {
            SampleContainer.Visibility = Visibility.Visible;
            DescriptionContainer.Visibility = Visibility.Collapsed;
            CategoriesRegion.Visibility = Visibility.Collapsed;
            SourceCodeContainer.Visibility = Visibility.Collapsed;
        }

        private void Description_Click(object sender, RoutedEventArgs e)
        {
            SampleContainer.Visibility = Visibility.Collapsed;
            DescriptionContainer.Visibility = Visibility.Visible;
            CategoriesRegion.Visibility = Visibility.Collapsed;
            SourceCodeContainer.Visibility = Visibility.Collapsed;
        }
        private void SourceCode_Click(object sender, RoutedEventArgs e)
        {
            SampleContainer.Visibility = Visibility.Collapsed;
            DescriptionContainer.Visibility = Visibility.Collapsed;
            CategoriesRegion.Visibility = Visibility.Collapsed;
            SourceCodeContainer.Visibility = Visibility.Visible;
        }

        private async void SearchFilterBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            // Don't update results immediately; makes search-as-you-type more comfortable
            if (_waitFlag) { return; }
            _waitFlag = true;
            await Task.Delay(200);
            _waitFlag = false;

            var results =
                SampleManager.Current.FullTree.Search(SampleSearchFunc);

            // Set category data context
            Categories.DataContext = WPF.Viewer.Helpers.ToTreeViewItem(results);

            // Open all
            OpenCategoryLeaves();
        }

        private bool SampleSearchFunc(SampleInfo sample)
        {
            return SampleManager.Current.SampleSearchFunc(sample, SearchFilterBox.Text);
        }
    }
}