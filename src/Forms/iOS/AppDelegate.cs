// Copyright 2016 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an 
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific 
// language governing permissions and limitations under the License.

using Foundation;
using UIKit;

namespace ArcGISRuntime.iOS
{
	// The UIApplicationDelegate for the application. This class is responsible for launching the 
	// User Interface of the application, as well as listening (and optionally responding) to 
	// application events from iOS.
	[Register("AppDelegate")]
	public partial class AppDelegate : Xamarin.Forms.Platform.iOS.FormsApplicationDelegate
	{
		//
		// This method is invoked when the application has loaded and is ready to run. In this 
		// method you should instantiate the window, load the UI into it and then make the window
		// visible.
		//
		// You have 17 seconds to return from this method, or iOS will terminate your application.
		//
		public override bool FinishedLaunching(UIApplication app, NSDictionary options)
		{
			Xamarin.Forms.Forms.Init ();
			LoadApplication(new App());

			return base.FinishedLaunching (app, options);
		}

		// https://docs.microsoft.com/en-us/xamarin/essentials/web-authenticator?tabs=ios
		public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
		{
			if (Xamarin.Essentials.Platform.OpenUrl(app, url, options))
				return true;

			return base.OpenUrl(app, url, options);
		}

		public override bool ContinueUserActivity(UIApplication application, NSUserActivity userActivity, UIApplicationRestorationHandler completionHandler)
		{
			if (Xamarin.Essentials.Platform.ContinueUserActivity(application, userActivity, completionHandler))
				return true;
			return base.ContinueUserActivity(application, userActivity, completionHandler);
		}
	}
}
