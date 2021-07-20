// Copyright 2020 Esri.

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UIKit;

namespace ArcGISRuntime
{
    /// <summary>
    /// Defines common styling parameters; modify this class to change colors, materials, spacing, and corner rounding
    /// </summary>
    public static class ApplicationTheme
    {

        public static UIColor BackgroundColor;
        public static UIColor ForegroundColor;

        static ApplicationTheme()
        {
            // Check if the device is running iOS 13 or higher. iOS 13 is required to use these UIColor values at runtime in code.
            if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
            {
                BackgroundColor = UIColor.SystemBackgroundColor;
                ForegroundColor = UIColor.LabelColor;
            }
            else
            {
                BackgroundColor = UIColor.White;
                ForegroundColor = UIColor.Black;
            }
        }
    }
}