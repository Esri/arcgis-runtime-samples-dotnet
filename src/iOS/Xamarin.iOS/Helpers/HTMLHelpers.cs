using Foundation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UIKit;

namespace ArcGISRuntime
{
    public class HTMLHelpers
    {
        private const string _lightMarkdownFile = "github-markdown.css";
        private const string _darkMarkdownFile = "github-markdown-dark.css";

        public static bool CheckDarkMode(UITraitCollection collection)
        {
            return UIDevice.CurrentDevice.CheckSystemVersion(12, 0) && collection.UserInterfaceStyle == UIUserInterfaceStyle.Dark;
        }

        public static string MarkdownToHTML(string rawMarkdown, UITraitCollection collection)
        {
            bool darkMode = CheckDarkMode(collection);

            string markdownFile = darkMode ? _darkMarkdownFile : _lightMarkdownFile;

            string markdownCSSPath = Path.Combine(NSBundle.MainBundle.BundlePath, $"SyntaxHighlighting/{markdownFile}");
            string parsedMarkdown = new MarkedNet.Marked().Parse(rawMarkdown);

            string markdowntHTML = "<!doctype html>" +
                "<head>" +
                "<link rel=\"stylesheet\" href=\"" +
                markdownCSSPath +
                "\" />" +
                "<meta name=\"viewport\" content=\"width=" +
                UIScreen.MainScreen.Bounds.Width.ToString() +
                ", shrink-to-fit=YES\">" +
                "</head>" +
                "<body class=\"markdown-body\">" +
                parsedMarkdown +
                "</body>";

            return markdowntHTML;
        }
    }
}