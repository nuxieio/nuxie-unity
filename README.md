# Nuxie Unity SDK

Native-backed Unity access to Nuxie Journeys, Experiences, Features, App Actions, activity, and commerce.

The C# facade delegates all runtime decisions to the Nuxie iOS and Android SDKs. Calling
`Trigger` captures an event and returns immediately; any matching Journey continues
asynchronously on device.

## Requirements

- Unity 2022.3 LTS or newer
- iOS 15 or newer
- Android API 23 or newer
- Nuxie iOS SDK 0.1.0
- Nuxie Android SDK 0.1.0

## Install

Add the package to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.nuxie.unity": "https://github.com/nuxieai/nuxie-unity.git#0.1.0"
  }
}
```

`Editor/NuxieDependencies.xml` pins both native SDKs to 0.1.0 through the
[External Dependency Manager for Unity](https://github.com/googlesamples/unity-jar-resolver).
Install that resolver before exporting a mobile player.

## Configure and trigger

```csharp
using System.Collections.Generic;
using Nuxie.Unity;
using UnityEngine;

public sealed class NuxieBootstrap : MonoBehaviour
{
  private async void Start()
  {
    var sdk = await Nuxie.ConfigureAsync(new NuxieConfig("NX_REPLACE_ME")
    {
      Environment = NuxieEnvironment.Production,
      LogLevel = NuxieLogLevel.Info,
    });

    await sdk.IdentifyAsync(
      "player_123",
      userProperties: new Dictionary<string, object?> { ["plan"] = "free" }
    );

    sdk.OnActivity += activity => Debug.Log(activity.Name);
    sdk.OnAppAction += action => Debug.Log(action.Name);

    sdk.Trigger(
      "upgrade_tapped",
      new Dictionary<string, object?> { ["screen"] = "settings" }
    );
  }
}
```

`Trigger` has no result, handle, cancellation, or identity mutation. Use
`OnActivity` for typed runtime telemetry and `OnAppAction` for actions delegated
to the host app.

## Features

```csharp
var access = await sdk.HasFeatureAsync(
  "credits",
  requiredBalance: 1.5,
  entityId: "workspace_123",
  policy: FeatureCheckPolicy.Remote
);

sdk.UseFeature("credits", amount: 0.5);
var usage = await sdk.UseFeatureAndWaitAsync("credits", amount: 1);
```

Feature balances remain `double` values. Atomic usage returns
`AuthoritativeAccess` when the native SDK has a current server result.

## Commerce

Implement `INuxiePurchaseController` and pass it to `ConfigureAsync` when the
host app owns checkout. `PurchaseRequest` mirrors the canonical portable
snake_case wire without receipt or transaction evidence invented by the wrapper.

## Validation

```bash
cd dotnet
dotnet test Nuxie.Unity.slnx --nologo
```

The repository also validates the Swift bridge against the real Nuxie framework
with complete concurrency checking and compiles the Kotlin bridge against the real
Android SDK with a host-only UnityPlayer stub.

See [the package documentation](Documentation~/index.md) and
[the sample](Samples~/NuxieDemo/README.md).
