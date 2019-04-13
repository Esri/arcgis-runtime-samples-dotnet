using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace ArcGISRuntime.WPF.Viewer.Telemetry
{
    public static class DiagnosticsClient
    {
        private static bool _initialized;
        private static TelemetryClient _client;

        public static void Initialize()
        {
            var apiKey = System.Configuration.ConfigurationManager.AppSettings["InstrumentationKey"];

            if (!string.IsNullOrEmpty(apiKey) && apiKey != "__AppInsightsKey__")
            {
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    TelemetryConfiguration.Active.InstrumentationKey = apiKey;
                    TelemetryConfiguration.Active.TelemetryChannel.DeveloperMode = Debugger.IsAttached;
                    _initialized = true;
                    _client = new TelemetryClient();
                    System.Windows.Application.Current.Startup += Application_Startup;
                    System.Windows.Application.Current.Exit += Application_Exit;
                }
            }
        }

        private static void Application_Exit(object sender, System.Windows.ExitEventArgs e)
        {
            TrackEvent("AppExit");
            _client.Flush();
            // Allow time for flushing:
            System.Threading.Thread.Sleep(1000);
        }

        private static void Application_Startup(object sender, System.Windows.StartupEventArgs e)
        {
            TrackEvent("AppStart", new Dictionary<string, string> { { "launchType", e.Args.Length > 0 ? "fileAssociation" : "shortcut" } });
        }

        public static void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            if (!_initialized) return;
            _client.TrackEvent(eventName, properties, metrics);
        }

        public static void TrackTrace(string evt)
        {
            if (!_initialized) return;
            _client.TrackTrace(evt);
        }

        public static void Notify(Exception exception, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            if (!_initialized) return;
            _client.TrackException(exception, properties, metrics);
        }

        public static void TrackPageView(string pageName)
        {
            if (!_initialized) return;
            _client.TrackPageView(pageName);
        }
    }
}