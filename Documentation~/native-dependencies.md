# Native dependencies

The wrapper ships Swift, Kotlin, and C# bridge source. Native runtime behavior comes from
Nuxie iOS 0.1.0 and Nuxie Android 0.1.0.

## Automatic resolution

`Editor/NuxieDependencies.xml` uses the External Dependency Manager for Unity to pin:

- CocoaPod `Nuxie` 0.1.0, minimum iOS 15
- Maven artifact `ai.nuxie:nuxie-android:0.1.0`

Resolve dependencies before exporting a player. A missing native SDK is a build error;
the wrapper has no fallback implementation.

## iOS

`Runtime/Plugins/iOS/NuxieUnityBridge.swift` imports `Nuxie` and exports:

- `NuxieUnity_Invoke`
- `NuxieUnity_FreeCString`

Link the Nuxie product to the target that compiles the Unity bridge. If an authored
Experience requests native permissions, add the corresponding usage descriptions:

- `NSUserTrackingUsageDescription`
- `NSCameraUsageDescription`
- `NSMicrophoneUsageDescription`
- `NSPhotoLibraryUsageDescription`
- `NSLocationWhenInUseUsageDescription`

## Android

The Android library uses package `ai.nuxie.unity` and imports `ai.nuxie.sdk`.
Android permission actions require the matching host manifest declarations, such as:

- `android.permission.POST_NOTIFICATIONS`
- `android.permission.CAMERA`
- `android.permission.RECORD_AUDIO`
- `android.permission.READ_MEDIA_IMAGES` on Android 13 or newer
- `android.permission.READ_EXTERNAL_STORAGE` on Android 12 or older
- coarse or fine location permission

## Callback envelope

Native delegates send one of five typed envelope kinds to `__NuxieBridgeHost`:

```json
{
  "type": "feature_access_changed|activity|app_action|purchase_request|restore_request",
  "timestampMs": 1739246400000,
  "payload": {}
}
```

Commerce payload keys are snake_case. Other payloads use the C# model field vocabulary.
