// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Samples.Managers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ArcGISRuntime.WPF.Viewer
{
    public partial class SourceCode : INotifyPropertyChanged
    {
        private SourceCodeFile _selectedFile;

        // Holds source files (as strings of html).
        public ObservableCollection<SourceCodeFile> SourceFiles { get; } = new ObservableCollection<SourceCodeFile>();

        public SourceCodeFile SelectedSourceFile
        {
            get => _selectedFile;
            set
            {
                if (value != _selectedFile)
                {
                    _selectedFile = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSourceFile)));
                }
            }
        }

        public SourceCode()
        {
            InitializeComponent();

            this.DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void LoadSourceCode()
        {
            SourceFiles.Clear();

            string folderPath = SampleManager.Current.SelectedSample.Path;

            // Add every .cs and .xaml file in the directory of the sample.
            foreach (string filepath in Directory.GetFiles(folderPath)
                .Where(candidate => candidate.EndsWith(".cs") || candidate.EndsWith(".xaml")))
            {
                SourceFiles.Add(new SourceCodeFile(filepath));
            }

            // Add additional class files from the sample.
            if (SampleManager.Current.SelectedSample.ClassFiles != null)
            {
                foreach (string additionalPath in SampleManager.Current.SelectedSample.ClassFiles)
                {
                    SourceFiles.Add(new SourceCodeFile(additionalPath));
                }
            }

            SelectedSourceFile = SourceFiles[0];
        }

        public class SourceCodeFile
        {
            // Start of each html string.
            private const string htmlStart =
                "<html>" +
                "<head>" +
                "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=11\">" +
                "<link rel=\"stylesheet\" href=\"{csspath}\">" +
                "<script type=\"text/javascript\" src=\"{jspath}\"></script>" +
                "<script>hljs.initHighlightingOnLoad();</script>" +
                "</head>" +
                "<body>" +
                "<pre>";

            private const string htmlEnd =
                "</pre>" +
                "</body>" +
                "</html>";

            private string _path;
            private string _fullContent;

            public string Path => _path;

            public string Name => System.IO.Path.GetFileName(_path);

            public string HtmlContent
            {
                get
                {
                    if (_fullContent == null)
                    {
                        LoadContent();
                    }

                    return _fullContent;
                }
            }

            public SourceCodeFile(string sourceFilePath)
            {
                _path = sourceFilePath;
            }

            private void LoadContent()
            {
                // Filepaths for the css and js files used for syntax highlighting.

                string cssPath = System.IO.Path.Combine(App.ResourcePath, "Resources", "SyntaxHighlighting", "highlight.css");
                string jsPath = System.IO.Path.Combine(App.ResourcePath, "Resources", "SyntaxHighlighting", "highlight.pack.js");

                // Source code of the file.
                try
                {
                    string baseContent = File.ReadAllText(_path);

                    if (_path.EndsWith(".xaml"))
                    {
                        baseContent = baseContent.Replace("<", "&lt;").Replace(">", "&gt;");
                    }

                    // Build the html.
                    _fullContent =
                        htmlStart.Replace("{csspath}", cssPath).Replace("{jspath}", jsPath) +
                        "<code class=\"csharp\">" +
                        baseContent +
                        "</code>" +
                        htmlEnd;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }
        }
    }

    // Taken from StackOverflow: https://stackoverflow.com/a/2586255/4630559
    public static class BrowserBehavior
    {
        public static readonly DependencyProperty HtmlProperty = DependencyProperty.RegisterAttached(
            "Html",
            typeof(string),
            typeof(BrowserBehavior),
            new FrameworkPropertyMetadata(OnHtmlChanged));

        [AttachedPropertyBrowsableForType(typeof(WebBrowser))]
        public static string GetHtml(WebBrowser d)
        {
            return (string)d.GetValue(HtmlProperty);
        }

        public static void SetHtml(WebBrowser d, string value)
        {
            d.SetValue(HtmlProperty, value);
        }

        private static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            WebBrowser wb = d as WebBrowser;
            if (wb != null && e.NewValue != null)
                wb.NavigateToString(e.NewValue as string);
        }
    }
}