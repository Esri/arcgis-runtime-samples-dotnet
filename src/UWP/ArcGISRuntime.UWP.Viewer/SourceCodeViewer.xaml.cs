// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Samples.Managers;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArcGISRuntime.UWP.Viewer
{
    public partial class SourceCode
    {
        // List of all of the tabs that will load.
        private List<TabViewItem> tabs;

        public SourceCode()
        {
            InitializeComponent();
        }

        public void LoadSourceCode()
        {
            // Create a new list of TabViewItems.
            tabs = new List<TabViewItem>();

            // Create a list of source files for the sample.
            var sourcePaths = new List<string>();

            foreach (string filepath in Directory.GetFiles(SampleManager.Current.SelectedSample.Path)
                .Where(candidate => candidate.EndsWith(".cs") || candidate.EndsWith(".xaml")))
            {
                sourcePaths.Add(filepath);
            }

            // Add additional class files from the sample.
            if (SampleManager.Current.SelectedSample.ClassFiles != null)
            {
                foreach (string additionalPath in SampleManager.Current.SelectedSample.ClassFiles)
                {
                    sourcePaths.Add(additionalPath);
                }
            }

            // Loop through all .cs and .xaml files.
            foreach (string filepath in sourcePaths)
            {
                // Get the source text from the file.
                string source = File.ReadAllText(filepath);

                // Create a new tab.
                TabViewItem newTab = new TabViewItem();

                // Set the tab text to the file name.
                newTab.Header = Path.GetFileName(filepath);

                // Create the code viewer.
                Monaco.CodeEditor viewer = new Monaco.CodeEditor();
                viewer.Options.ReadOnly = true;
                viewer.Text = source;

                // Change Monaco language for C# files.
                if (filepath.EndsWith(".cs")) { viewer.CodeLanguage = "csharp"; }

                // Adjust tabs for dark mode.
                if (Windows.UI.Xaml.Application.Current.RequestedTheme == Windows.UI.Xaml.ApplicationTheme.Dark)
                {
                    Tabs.RequestedTheme = Windows.UI.Xaml.ElementTheme.Dark;
                    newTab.RequestedTheme = Windows.UI.Xaml.ElementTheme.Dark;
                }

                // Set the tabs content to the code viewer.
                newTab.Content = viewer;

                // Add the tab to the beginning of the list.
                tabs.Insert(0, newTab);
            }

            // Set the Tab source to the list of tabs.
            Tabs.ItemsSource = tabs;
        }
    }
}