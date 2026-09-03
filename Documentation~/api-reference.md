# API reference

## Configuration

- `Task<Nuxie> Nuxie.ConfigureAsync(NuxieConfig, INuxiePurchaseController? = null)`
- `Task ShutdownAsync()`
- `bool IsConfigured`
- `string WrapperVersion`

`NuxieConfig` supports:

- `ApiKey`
- `Environment`: `Production` or `Development`
- `LogLevel`
- `EnableConsoleLogging`
- `RedactSensitiveData`
- `LocaleIdentifier`
- `PurchaseHandlingMode`: `Full` or `Observer`
- `TestStoreEnabled` for iOS development builds

## Identity

- `Task IdentifyAsync(string distinctId, ...)`
- `Task ResetAsync(bool keepAnonymousId = false)`
- `Task<string> GetDistinctIdAsync()`
- `Task<string> GetAnonymousIdAsync()`
- `Task<bool> GetIsIdentifiedAsync()`

## Journeys and Experiences

- `void Trigger(string eventName, IReadOnlyDictionary<string, object?>? properties = null)`
- `Task DismissAsync()`
- `Task SetLocaleIdentifierAsync(string? localeIdentifier)`

`Trigger` records one event and returns immediately. Journey progress is represented by
native activity rather than a second trigger state API.

## Features

- `Task<FeatureAccess> HasFeatureAsync(string featureId, double requiredBalance = 1, string? entityId = null, FeatureCheckPolicy policy = CacheFirst)`
- `void UseFeature(string featureId, double amount = 1, string? entityId = null, ...)`
- `Task<FeatureUsageResult> UseFeatureAndWaitAsync(string featureId, double amount = 1, string? entityId = null, bool setUsage = false, ...)`

`FeatureAccess.Balance`, usage amounts, limits, and remaining values are `double`.
`FeatureUsageResult.AuthoritativeAccess` carries the native atomic result.

## Events

- `OnFeatureAccessChanged`
- `OnActivity`
- `OnAppAction`
- `OnPurchaseRequest`
- `OnRestoreRequest`

`NuxieActivityInfo` contains schema version 1, IDs, event and receipt timestamps, name,
and scalar properties. `AppAction` contains a name, optional scalar payload, and
`ExperienceRef`.

## Commerce

`INuxiePurchaseController` defines:

- `Task<PurchaseResult> OnPurchaseAsync(PurchaseRequest request)`
- `Task<RestoreResult> OnRestoreAsync(RestoreRequest request)`

Purchase results are `Purchased`, `Cancelled`, `Pending`, or `Failed`. Restore
results are `Restored`, `NoPurchases`, or `Failed`.

The portable request fields map directly to:

`request_id`, `platform`, `product_id`, `store_product_id`, `base_plan_id`,
`purchase_option_id`, `offer_id`, `placement_id`, `display_name`,
`display_price`, and `timestamp_ms`.
