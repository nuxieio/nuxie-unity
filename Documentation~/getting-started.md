# Getting started

## 1. Install dependencies

Install this Unity package and the External Dependency Manager for Unity. The checked-in
`Editor/NuxieDependencies.xml` resolves Nuxie iOS 0.1.0 and
`ai.nuxie:nuxie-android:0.1.0`.

See [native dependencies](native-dependencies.md) for export requirements and native
permission declarations.

## 2. Configure once

```csharp
var sdk = await Nuxie.ConfigureAsync(new NuxieConfig("NX_REPLACE_ME")
{
  Environment = NuxieEnvironment.Production,
  LogLevel = NuxieLogLevel.Warning,
  LocaleIdentifier = "en-US",
});
```

Configuration contains only customer-owned choices. Endpoint, retry, cache, storage,
delivery, and runtime lifecycle behavior stay inside the native SDK.

## 3. Identify

```csharp
await sdk.IdentifyAsync(
  "player_123",
  userProperties: new Dictionary<string, object?> { ["plan"] = "pro" },
  userPropertiesSetOnce: new Dictionary<string, object?> { ["source"] = "unity" }
);
```

`ResetAsync()` creates a new anonymous identity by default. Pass
`keepAnonymousId: true` only when the product explicitly needs it.

## 4. Trigger Journeys

```csharp
sdk.Trigger(
  "level_completed",
  new Dictionary<string, object?>
  {
    ["level"] = 12,
    ["score"] = 9850,
  }
);
```

`Trigger` is an event-only, fire-and-forget call. A matching Journey is selected and
run by native code. There is no wrapper trigger result or cancellation handle.

## 5. Receive typed host events

```csharp
sdk.OnActivity += activity => Debug.Log(activity.Name);
sdk.OnAppAction += action => HandleAppAction(action);
sdk.OnFeatureAccessChanged += change => RenderFeatureAccess(change.To);
```

`NuxieActivityInfo` preserves scalar activity properties. `AppAction` includes an
`ExperienceRef` with the originating Experience and optional Journey.

## 6. Read and use Features

```csharp
var access = await sdk.HasFeatureAsync(
  "credits",
  requiredBalance: 2.5,
  policy: FeatureCheckPolicy.CacheFirst
);

sdk.UseFeature("credits", amount: 1);
var result = await sdk.UseFeatureAndWaitAsync("credits", amount: 1);
```

Use `FeatureCheckPolicy.Remote` when the caller explicitly needs a current server
answer. `UseFeatureAndWaitAsync` includes the authoritative access snapshot when
available.

## 7. Dismiss and localize

```csharp
await sdk.DismissAsync();
await sdk.SetLocaleIdentifierAsync("fr-CA");
```

Passing `null` to `SetLocaleIdentifierAsync` restores native locale selection.

## 8. Shut down

```csharp
await sdk.ShutdownAsync();
```
