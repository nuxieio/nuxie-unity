using System.Text.Json;
using Nuxie.Unity.Internal;

namespace Nuxie.Unity.Core.Tests;

internal sealed class FakeNativeBridge : INuxieNativeBridge
{
  public event Action<NativeEventEnvelope>? EventReceived;

  public Dictionary<string, object?>? ConfigurationOptions { get; private set; }
  public bool? LastResetKeepAnonymousId { get; private set; }
  public (string EventName, IReadOnlyDictionary<string, object?>? Properties)? TriggerCall { get; private set; }
  public (string FeatureId, double RequiredBalance, string? EntityId, FeatureCheckPolicy Policy)? FeatureCall { get; private set; }
  public (string FeatureId, double Amount, string? EntityId)? UseFeatureCall { get; private set; }
  public int ShutdownCalls { get; private set; }

  public TaskCompletionSource<(string RequestId, PurchaseResult Result)> PurchaseCompletions { get; } =
    new(TaskCreationOptions.RunContinuationsAsynchronously);

  public TaskCompletionSource<(string RequestId, RestoreResult Result)> RestoreCompletions { get; } =
    new(TaskCreationOptions.RunContinuationsAsynchronously);

  public Task ConfigureAsync(
    string apiKey,
    Dictionary<string, object?> options,
    bool usingPurchaseController,
    string wrapperVersion,
    CancellationToken cancellationToken
  )
  {
    ConfigurationOptions = options;
    return Task.CompletedTask;
  }

  public Task ShutdownAsync(CancellationToken cancellationToken)
  {
    ShutdownCalls += 1;
    return Task.CompletedTask;
  }

  public Task IdentifyAsync(
    string distinctId,
    IReadOnlyDictionary<string, object?>? userProperties,
    IReadOnlyDictionary<string, object?>? userPropertiesSetOnce,
    CancellationToken cancellationToken
  ) => Task.CompletedTask;

  public Task ResetAsync(bool keepAnonymousId, CancellationToken cancellationToken)
  {
    LastResetKeepAnonymousId = keepAnonymousId;
    return Task.CompletedTask;
  }

  public Task<string> GetDistinctIdAsync(CancellationToken cancellationToken) =>
    Task.FromResult("distinct-1");

  public Task<string> GetAnonymousIdAsync(CancellationToken cancellationToken) =>
    Task.FromResult("anonymous-1");

  public Task<bool> GetIsIdentifiedAsync(CancellationToken cancellationToken) =>
    Task.FromResult(true);

  public void Trigger(string eventName, IReadOnlyDictionary<string, object?>? properties)
  {
    TriggerCall = (eventName, properties);
  }

  public Task DismissAsync(CancellationToken cancellationToken) => Task.CompletedTask;

  public Task SetLocaleIdentifierAsync(string? localeIdentifier, CancellationToken cancellationToken) =>
    Task.CompletedTask;

  public Task<FeatureAccess> HasFeatureAsync(
    string featureId,
    double requiredBalance,
    string? entityId,
    FeatureCheckPolicy policy,
    CancellationToken cancellationToken
  )
  {
    FeatureCall = (featureId, requiredBalance, entityId, policy);
    return Task.FromResult(new FeatureAccess
    {
      Allowed = true,
      Unlimited = false,
      Balance = 4.5,
      Type = FeatureType.Metered,
    });
  }

  public void UseFeature(
    string featureId,
    double amount,
    string? entityId,
    IReadOnlyDictionary<string, object?>? metadata
  )
  {
    UseFeatureCall = (featureId, amount, entityId);
  }

  public Task<FeatureUsageResult> UseFeatureAndWaitAsync(
    string featureId,
    double amount,
    string? entityId,
    bool setUsage,
    IReadOnlyDictionary<string, object?>? metadata,
    CancellationToken cancellationToken
  )
  {
    return Task.FromResult(new FeatureUsageResult
    {
      Success = true,
      FeatureId = featureId,
      AmountUsed = amount,
      AuthoritativeAccess = new FeatureAccess
      {
        Allowed = true,
        Balance = 3.5,
        Type = FeatureType.Metered,
      },
    });
  }

  public Task CompletePurchaseAsync(
    string requestId,
    PurchaseResult result,
    CancellationToken cancellationToken
  )
  {
    PurchaseCompletions.TrySetResult((requestId, result));
    return Task.CompletedTask;
  }

  public Task CompleteRestoreAsync(
    string requestId,
    RestoreResult result,
    CancellationToken cancellationToken
  )
  {
    RestoreCompletions.TrySetResult((requestId, result));
    return Task.CompletedTask;
  }

  public void Emit(NativeEventType type, object payload, long timestampMs = 1_739_246_400_000)
  {
    var typeName = type switch
    {
      NativeEventType.FeatureAccessChanged => "feature_access_changed",
      NativeEventType.Activity => "activity",
      NativeEventType.AppAction => "app_action",
      NativeEventType.PurchaseRequest => "purchase_request",
      NativeEventType.RestoreRequest => "restore_request",
      _ => "unknown",
    };

    var json = JsonSerializer.Serialize(new { type = typeName, timestampMs, payload });
    if (!NativeEventEnvelope.TryParse(json, out var envelope, out var error))
    {
      throw new InvalidOperationException(error);
    }
    EventReceived?.Invoke(envelope!);
  }
}
