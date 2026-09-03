# Testing and validation

## Managed contract

```bash
cd dotnet
dotnet test Nuxie.Unity.slnx --nologo
```

The test project compiles the real `Runtime/**/*.cs` sources and verifies:

- compact configuration
- event-only trigger behavior
- reset defaults
- fractional Feature access and atomic authoritative access
- typed activity and App Action mapping
- canonical commerce payloads
- shutdown lifecycle

## iOS bridge

Typecheck the bridge against a built Nuxie framework, not by itself:

```bash
swiftc -typecheck \
  -swift-version 6 \
  -strict-concurrency=complete \
  -target arm64-apple-ios15.0-simulator \
  -sdk "$(xcrun --sdk iphonesimulator --show-sdk-path)" \
  -F /path/to/DerivedData/Build/Products/Debug-iphonesimulator \
  -I /path/to/DerivedData/Build/Products/Debug-iphonesimulator \
  Runtime/Plugins/iOS/NuxieUnityBridge.swift
```

## Android bridge

Compile `NuxieUnityBridge.kt` against the real `nuxie-android` project. A host check
may provide a minimal `com.unity3d.player.UnityPlayer` stub; all Nuxie symbols must
come from the native SDK itself.

## Unity player smoke

For each release, export one iOS and one Android development player and verify:

1. setup and identity
2. event capture and Journey-driven Experience presentation
3. dismiss
4. Feature read and use
5. activity and App Action callbacks
6. observer commerce completion
7. shutdown and fresh setup
