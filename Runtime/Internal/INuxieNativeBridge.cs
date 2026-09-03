using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nuxie.Unity.Internal;

internal interface INuxieNativeBridge
{
  event Action<NativeEventEnvelope>? EventReceived;

  Task ConfigureAsync(
    string apiKey,
    Dictionary<string, object?> options,
    bool usingPurchaseController,
    string wrapperVersion,
    CancellationToken cancellationToken
  );

  Task ShutdownAsync(CancellationToken cancellationToken);

  Task IdentifyAsync(
    string distinctId,
    IReadOnlyDictionary<string, object?>? userProperties,
    IReadOnlyDictionary<string, object?>? userPropertiesSetOnce,
    CancellationToken cancellationToken
  );

  Task ResetAsync(bool keepAnonymousId, CancellationToken cancellationToken);
  Task<string> GetDistinctIdAsync(CancellationToken cancellationToken);
  Task<string> GetAnonymousIdAsync(CancellationToken cancellationToken);
  Task<bool> GetIsIdentifiedAsync(CancellationToken cancellationToken);

  void Trigger(string eventName, IReadOnlyDictionary<string, object?>? properties);
  Task DismissAsync(CancellationToken cancellationToken);
  Task SetLocaleIdentifierAsync(string? localeIdentifier, CancellationToken cancellationToken);

  Task<FeatureAccess> HasFeatureAsync(
    string featureId,
    double requiredBalance,
    string? entityId,
    FeatureCheckPolicy policy,
    CancellationToken cancellationToken
  );

  void UseFeature(
    string featureId,
    double amount,
    string? entityId,
    IReadOnlyDictionary<string, object?>? metadata
  );

  Task<FeatureUsageResult> UseFeatureAndWaitAsync(
    string featureId,
    double amount,
    string? entityId,
    bool setUsage,
    IReadOnlyDictionary<string, object?>? metadata,
    CancellationToken cancellationToken
  );

  Task CompletePurchaseAsync(string requestId, PurchaseResult result, CancellationToken cancellationToken);
  Task CompleteRestoreAsync(string requestId, RestoreResult result, CancellationToken cancellationToken);
}
