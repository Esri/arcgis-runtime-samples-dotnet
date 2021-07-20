// Copyright 2021 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Samples.Shared.Models;
using Foundation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UIKit;
using WebKit;

namespace ArcGISRuntime
{
    public class SampleInfoViewController : UIViewController
    {
        // SampleInfo for the sample provided by the constructor.
        private SampleInfo _info;

        // Control for switching between readme and source code viewing.
        private UISegmentedControl _switcherControl;

        // Web view for viewing readme markdown.
        private WKWebView _readmeView;

        // Controls for the source code viewer.
        private WKWebView _codeWebView;
        private UIBarButtonItem _codeButton;
        private UIToolbar _codeToolbar;

        // Dictionary where keys are filenames and values are HTML of source code.
        private Dictionary<string, string> _sourceCodeFiles;

        // Directory for loading HTML locally.
        private string _contentDirectoryPath = Path.Combine(NSBundle.MainBundle.BundlePath, "Content/");

        private bool _darkMode = false;

        private const string _lightMarkdownFile = "github-markdown.css";
        private const string _darkMarkdownFile = "github-markdown-dark.css";

        private const string _lightSyntaxFile = "highlight.css";
        private const string _darkSyntaxFile = "highlight-dark.css";

        public SampleInfoViewController(SampleInfo info, UISegmentedControl switcher)
        {
            _info = info;
            _switcherControl = switcher;
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            Initialize();
        }

        private void Initialize()
        {
            CheckDarkMode();

            // Build out readme html.
            try
            {
                string markdownFile = _darkMode ? _darkMarkdownFile : _lightMarkdownFile;

                string readmePath = Path.Combine(NSBundle.MainBundle.BundlePath, "Samples", _info.Category, _info.FormalName, "readme.md");
                string readmeCSSPath = Path.Combine(NSBundle.MainBundle.BundlePath, $"SyntaxHighlighting/{markdownFile}");
                string readmeContent = new MarkedNet.Marked().Parse(File.ReadAllText(readmePath));

                string readmeHTML = "<!doctype html><head><base href=\"" +
                    readmePath +
                    "\"><link rel=\"stylesheet\" href=\"" +
                    readmeCSSPath +
                    "\" />" +
                    "<meta name=\"viewport\" content=\"width=" +
                    UIScreen.MainScreen.Bounds.Width.ToString() +
                    ", shrink-to-fit=YES\">" +
                    "</head>" +
                    "<body class=\"markdown-body\">" +
                    readmeContent +
                    "</body>";

                // Load the readme into the webview.
                _readmeView.LoadHtmlString(readmeHTML, new NSUrl(_contentDirectoryPath, true));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                _readmeView.LoadHtmlString("Could not load information.", new NSUrl(_contentDirectoryPath, true));
            }

            // Build out source code html.
            try
            {
                string syntaxFile = _darkMode ? _darkSyntaxFile : _lightSyntaxFile;

                // Get the paths for the .css and .js files for syntax highlighting.
                string syntaxHighlightCSS = Path.Combine(NSBundle.MainBundle.BundlePath, $"SyntaxHighlighting/{syntaxFile}");
                string jsPath = Path.Combine(NSBundle.MainBundle.BundlePath, "SyntaxHighlighting/highlight.pack.js");

                // Start and end HTML tags.
                const string _sourceHTMLStart =
                            "<html>" +
                            "<head>" +
                            "<link rel=\"stylesheet\" href=\"{csspath}\">" +
                            "<script type=\"text/javascript\" src=\"{jspath}\"></script>" +
                            "<script>hljs.initHighlightingOnLoad();</script>" +
                            "</head>" +
                            "<body>" +
                            "<pre>";

                const string _sourceHTMLEnd =
                           "</pre>" +
                           "</body>" +
                           "</html>";

                string sourceFilesPath = Path.Combine(NSBundle.MainBundle.BundlePath, "Samples", _info.Category, _info.FormalName);

                // Create a dictionary of the files.
                _sourceCodeFiles = new Dictionary<string, string>();

                List<string> sourceCodePaths = new List<string>(Directory.GetFiles(sourceFilesPath, "*.cs"));

                // Add additional class files from the sample.
                if (_info.ClassFiles != null)
                {
                    foreach (string additionalPath in _info.ClassFiles)
                    {
                        string path = Path.Combine(NSBundle.MainBundle.BundlePath, additionalPath).Replace('\\', '/');
                        sourceCodePaths.Add(path);
                    }
                }

                // Loop over every source code file in the sample directory.
                foreach (string sourceCodePath in sourceCodePaths)
                {
                    // Get the code as a string.
                    string baseContent = File.ReadAllText(sourceCodePath);

                    // Build the html.
                    string sourceCodeHTML =
                        _sourceHTMLStart.Replace("{csspath}", syntaxHighlightCSS).Replace("{jspath}", jsPath) +
                        "<code class=\"csharp\">" +
                        baseContent +
                        "</code>" +
                        _sourceHTMLEnd;

                    // Get the filename without the full path.
                    string shortPath = sourceCodePath.Split('/').Last();

                    // Add the html to the source code files dictionary.
                    _sourceCodeFiles.Add(shortPath, sourceCodeHTML);
                }

                // Load the first source code file into the web view.
                string firstKey = _sourceCodeFiles.Keys.First();
                _codeWebView.LoadHtmlString(_sourceCodeFiles[firstKey], new NSUrl(_contentDirectoryPath, true));
                _codeButton.Title = firstKey;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                _codeWebView.LoadHtmlString("Could not load source code.", new NSUrl(_contentDirectoryPath, true));
                _codeButton.Enabled = false;
            }
        }

        private void CheckDarkMode()
        {
            _darkMode = UIDevice.CurrentDevice.CheckSystemVersion(12, 0) && TraitCollection.UserInterfaceStyle == UIUserInterfaceStyle.Dark;
        }

        private void SegmentChanged(object sender, EventArgs e)
        {
            if (_switcherControl.SelectedSegment == 0)
            {
                // If about section.
                _codeWebView.Hidden = _codeToolbar.Hidden = true;
                _readmeView.Hidden = false;
            }
            else
            {
                // If code section.
                _readmeView.Hidden = true;
                _codeWebView.Hidden = _codeToolbar.Hidden = false;
            }
        }

        private void SourceCodeButtonPressed(object sender, EventArgs e)
        {
            // Build a UI alert controller for picking the source code file.
            UIAlertController prompt = UIAlertController.Create(null, null, UIAlertControllerStyle.ActionSheet);
            foreach (string fileTitle in _sourceCodeFiles.Keys)
            {
                UIAlertAction action = UIAlertAction.Create(fileTitle, UIAlertActionStyle.Default, FileSelect);
                prompt.AddAction(action);
            }
            prompt.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));

            // Needed to prevent crash on iPad.
            UIPopoverPresentationController ppc = prompt.PopoverPresentationController;
            if (ppc != null)
            {
                ppc.BarButtonItem = _codeButton;
                ppc.PermittedArrowDirections = UIPopoverArrowDirection.Down;
            }

            PresentViewController(prompt, true, null);
        }

        private void FileSelect(UIAlertAction obj)
        {
            _codeWebView.LoadHtmlString(_sourceCodeFiles[obj.Title], new NSUrl(_contentDirectoryPath, true));
            _codeButton.Title = obj.Title;
        }

        public override void LoadView()
        {
            // Create and configure the views.
            View = new UIView { BackgroundColor = ApplicationTheme.BackgroundColor };

            // Web view for the readme.
            _readmeView = new WKWebView(new CoreGraphics.CGRect(), new WKWebViewConfiguration()) { BackgroundColor = UIColor.Clear, Opaque = false };
            _readmeView.TranslatesAutoresizingMaskIntoConstraints = false;

            // Add navigation delegat for opening readme links in browser.
            _readmeView.NavigationDelegate = new BrowserLinksNavigationDelegate();

            // Web view of the source code html.
            _codeWebView = new WKWebView(new CoreGraphics.CGRect(), new WKWebViewConfiguration()) { BackgroundColor = UIColor.Clear, Opaque = false, Hidden = true };
            _codeWebView.TranslatesAutoresizingMaskIntoConstraints = false;

            // Button for bringing up alertcontroller to switch between source code files.
            _codeToolbar = new UIToolbar { Hidden = true, TranslatesAutoresizingMaskIntoConstraints = false };
            _codeButton = new UIBarButtonItem("", UIBarButtonItemStyle.Plain, SourceCodeButtonPressed);
            _codeToolbar.Items = new UIBarButtonItem[] { _codeButton };

            // Add sub views to main view.
            View.AddSubviews(_readmeView, _codeWebView, _codeToolbar);

            // Lay out the views.
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                 _readmeView.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
                 _readmeView.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor),
                 _readmeView.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor),
                 _readmeView.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor),

                 _codeWebView.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
                 _codeWebView.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor),
                 _codeWebView.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor),
                 _codeWebView.BottomAnchor.ConstraintEqualTo(_codeToolbar.TopAnchor),

                 _codeToolbar.TopAnchor.ConstraintEqualTo(_codeWebView.BottomAnchor),
                 _codeToolbar.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor),
                 _codeToolbar.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor),
                 _codeToolbar.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor),
            });
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);

            // Subscribe to events.
            _switcherControl.ValueChanged += SegmentChanged;
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);

            // Unsubscribe from events, per best practice.
            _switcherControl.ValueChanged -= SegmentChanged;
        }

        public override void TraitCollectionDidChange(UITraitCollection previousTraitCollection)
        {
            // Reload the html pages when switching to and from dark mode.
            if (previousTraitCollection.UserInterfaceStyle != TraitCollection.UserInterfaceStyle) Initialize();
        }
    }

    public class BrowserLinksNavigationDelegate : WKNavigationDelegate
    {
        public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
        {
            if (navigationAction.NavigationType == WKNavigationType.LinkActivated)
            {
                // Open links in browser.
                if (UIApplication.SharedApplication.CanOpenUrl(navigationAction.Request.Url))
                {
                    UIApplication.SharedApplication.OpenUrl(navigationAction.Request.Url);
                }
                decisionHandler(WKNavigationActionPolicy.Cancel);
            }
            else
            {
                decisionHandler(WKNavigationActionPolicy.Allow);
            }
        }
    }
}