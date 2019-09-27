# Explore scenes in flyover AR

Use augmented reality (AR) to quickly explore a scene more naturally than you could with a touch or mouse interface.

![Scene shown in an app](ExploreScenesInFlyoverAR.jpg)

## Use case

You can use AR to drop into an area and visualize information, like a proposed development or a historical model of a city. You could use flyover AR to explore a city by walking through it virtually.

## How to use the sample

When you open the sample, you'll be viewing the scene from above. You can walk around, using your device as a window into the scene. Try moving vertically to get closer to the ground. Touch gestures which 

## How it works

1. Create the `ARSceneView` and add it to the view.
2. Create the scene, add content, then display it.
3. When the content you want to view loads, get its center point and use that to create the origin camera for the AR view. Note that the altitude should be set so that all scene content is visible. For a city, a good value might be a bit higher than the tallest building. The sample uses 250 meters in the absence of tall buildings in the sample data.
4. Set the translation factor so that you can move through the scene easily. With a translation factor of 800, you will move 800 feet in the scene for every foot you move the physical device.
5. Set the space effect to `Stars` and atmosphere effect to `Realistic` to create an immersive experience.

## Relevant API

* ARSceneView

## About the data

This sample uses a sample [integrated mesh layer](https://www.arcgis.com/home/item.html?id=d4fb271d1cb747e696bb80adca8487fa) provided by [Vricon](https://www.vricon.com/). The integrated mesh layer shows an area around the US-Mexico border.

## Additional information

**Flyover AR** is one of three main patterns for working with geographic information in augmented reality. See the [guide doc]() for more information.

This sample requires a device that is compatible with ARCore 1.8 on Android. See Google's list of [ARCore supported devices](https://developers.google.com/ar/discover/supported-devices).

This sample uses the ArcGIS Runtime Toolkit. See [Agumented reality]() in the guide to learn about the toolkit and how to add it to your app.

Note the following steps which must be taken to enable your app for AR:

* Add the `CAMERA` permission to **AndroidManifest.xml**: `<uses-permission android:name="android.permission.CAMERA" />`
  * Android won't display the camera permission request if you skip this step.
* Add the AR Core metadata attribute to the application definition in **AndroidManifest.xml**: `<meta-data android:name="com.google.ar.core" android:value="optional" />`
  * Specify `optional` or `required` depending on if your app should be installable on devices without AR Core.
  * If you leave this out, AR Core tracking will fail to start.

## Tags

augmented reality, bird's eye, birds-eye-view, fly over, flyover, mixed reality, translation factor