// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Samples.Managers;
using ArcGISRuntime.Samples.Shared.Managers;
using ArcGISRuntime.Samples.Shared.Models;
using Esri.ArcGISRuntime.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ArcGISRuntime.Samples.Desktop
{
    public partial class MainWindow
    {
        private bool _waitFlag;

        private List<string> _namedUserSamples = new List<string> {
            "AuthorMap",
            "SearchPortalMaps",
            "OAuth" };

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

                Loaded += FirstLoaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Initialization exception occurred.");
            }
        }

        private void FirstLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= FirstLoaded;

            _ = CheckApiKey();
        }

        private async Task CheckApiKey()
        {
            // Attempt to load a locally stored API key.
            await ApiKeyManager.TrySetLocalKey();

            // Check that the current API key is valid.
            ApiKeyStatus status = await ApiKeyManager.CheckKeyValidity();
            if (status != ApiKeyStatus.Valid)
            {
                PromptForKey();
            }
        }

        private void PromptForKey()
        {
            var keyPrompt = new Window() { Width = 500, Height = 220, Title = "Edit API key" };
            keyPrompt.Content = new ApiKeyPrompt();
            keyPrompt.Show();
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
            if (context == null) { return; }
            var sample = context.DataContext as SampleInfo;
            var category = context.DataContext as SearchableTreeNode;
            if (category != null)
            {
                CategoriesList.ItemsSource = category.Items;
                DetailsRegion.Visibility = Visibility.Collapsed;
                SampleContainer.Content = null;
                CategoriesRegion.Visibility = Visibility.Visible;
                CategoriesHeader.Text = category.Name;
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

            // Restore API key if leaving named user sample.
            if (_namedUserSamples.Contains(SampleManager.Current?.SelectedSample?.FormalName))
            {
                ApiKeyManager.EnableKey();
            }

            // Remove API key if opening named user sample.
            if (_namedUserSamples.Contains(selectedSample.FormalName))
            {
                ApiKeyManager.DisableKey();
            }

            // Call a function to clear any existing credentials from AuthenticationManager
            ClearCredentials();

            SampleTitleBlock.Text = selectedSample.SampleName;
            SampleManager.Current.SelectedSample = selectedSample;
            DescriptionContainer.SetSample(selectedSample);
            ShowSampleTab();

            try
            {
                if (selectedSample.OfflineDataItems != null)
                {
                    CancellationTokenSource cancellationSource = new CancellationTokenSource();

                    // Show waiting page
                    SampleContainer.Content = new WPF.Viewer.WaitPage(cancellationSource);

                    // Wait for offline data to complete
                    await DataManager.EnsureSampleDataPresent(selectedSample, cancellationSource.Token);
                }

                // Show the sample
                SampleContainer.Content = SampleManager.Current.SampleToControl(selectedSample);
                SourceCodeContainer.LoadSourceCode();
            }
            catch (OperationCanceledException)
            {
                CategoriesRegion.Visibility = Visibility.Visible;
                SampleContainer.Visibility = Visibility.Collapsed;
                return;
            }
            catch (Exception exception)
            {
                // failed to create new instance of the sample
                SampleContainer.Content = new WPF.Viewer.ErrorPage(exception);
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

            // Clear the challenge handler.
            AuthenticationManager.Current.ChallengeHandler = null;
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

        private void CloseCategoryLeaves()
        {
            if (Categories.Items.Count > 0)
            {
                var firstTreeViewItem = Categories.Items[0] as TreeViewItem;
                if (firstTreeViewItem != null) firstTreeViewItem.IsSelected = true;

                foreach (var item in Categories.Items)
                {
                    var treeViewItem = item as TreeViewItem;
                    if (treeViewItem != null) treeViewItem.IsExpanded = false;
                }
            }
        }

        private void ShowSampleTab()
        {
            SampleRadTab.IsChecked = true;
            SampleContainer.Visibility = Visibility.Visible;
            DescriptionContainer.Visibility = Visibility.Collapsed;
            CategoriesRegion.Visibility = Visibility.Collapsed;
            SourceCodeContainer.Visibility = Visibility.Collapsed;
        }

        private void ShowDescriptionTab()
        {
            DetailsRadTab.IsChecked = true;
            SampleContainer.Visibility = Visibility.Collapsed;
            DescriptionContainer.Visibility = Visibility.Visible;
            CategoriesRegion.Visibility = Visibility.Collapsed;
            SourceCodeContainer.Visibility = Visibility.Collapsed;
        }

        private void ShowSourceTab()
        {
            SourceRadTab.IsChecked = true;
            SampleContainer.Visibility = Visibility.Collapsed;
            DescriptionContainer.Visibility = Visibility.Collapsed;
            CategoriesRegion.Visibility = Visibility.Collapsed;
            SourceCodeContainer.Visibility = Visibility.Visible;
        }

        private void LiveSample_Click(object sender, RoutedEventArgs e) => ShowSampleTab();

        private void Description_Click(object sender, RoutedEventArgs e) => ShowDescriptionTab();

        private void SourceCode_Click(object sender, RoutedEventArgs e) => ShowSourceTab();

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

            // Open all if query isn't empty
            if (!string.IsNullOrWhiteSpace(SearchFilterBox.SearchText))
            {
                OpenCategoryLeaves();
            }
            else
            {
                CloseCategoryLeaves();
            }
        }

        private bool SampleSearchFunc(SampleInfo sample)
        {
            return SampleManager.Current.SampleSearchFunc(sample, SearchFilterBox.SearchText);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.Show();
        }
    }
}