# Nuxie demo

The sample controller demonstrates:

- configuration and identity
- event-only Journey triggers
- policy-aware Feature access
- typed activity and App Actions
- host-owned purchase and restore callbacks
- Experience dismissal and shutdown

1. Import the package and resolve its native dependencies.
2. Add `NuxieDemoController` to a GameObject.
3. Set `apiKey`, `distinctId`, `triggerEventName`, and `featureId`.
4. Run on an iOS or Android device.

The controller intentionally returns a failed purchase until the host app connects its
store implementation.
