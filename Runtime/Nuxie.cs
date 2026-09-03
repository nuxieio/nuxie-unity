using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nuxie.Unity.Internal;

namespace Nuxie.Unity;

public sealed class Nuxie
{
  private const string WrapperVersionValue = "0.1.0";
  private static readonly TimeSpan PurchaseTimeout = TimeSpan.FromSeconds(60);
  private static readonly object InstanceGate = new();
  private static Nuxie? _instance;
  private static Func<INuxieNativeBridge> _bridgeFactory = static () => new UnityNativeBridge();

  private readonly INuxieNativeBridge _bridge;
  private bool _isConfigured;
  private INuxiePurchaseController? _purchaseController;

  private Nuxie(INuxieNativeBridge bridge)
  {
    _bridge = bridge;
    _bridge.EventReceived += OnNativeEventReceived;
  }

  public static Nuxie Instance
  {
    get
    {
      var instance = _instance;
      if (instance is null || !instance._isConfigured)
      {
        throw new NuxieException("NOT_CONFIGURED", "Nuxie.ConfigureAsync must complete before Nuxie.Instance is used.");
      }

      return instance;
    }
  }

  public bool IsConfigured => _isConfigured;
  public string WrapperVersion => WrapperVersionValue;

  public event Action<FeatureAccessChangedEvent>? OnFeatureAccessChanged;
  public event Action<NuxieActivityInfo>? OnActivity;
  public event Action<AppAction>? OnAppAction;
  public event Action<PurchaseRequest>? OnPurchaseRequest;
  public event Action<RestoreRequest>? OnRestoreRequest;

  public static async Task<Nuxie> ConfigureAsync(
    NuxieConfig config,
    INuxiePurchaseController? purchaseController = null
  )
  {
    if (config is null)
    {
      throw new ArgumentNullException(nameof(config));
    }

    if (string.IsNullOrWhiteSpace(config.ApiKey))
    {
      throw new NuxieException("MISSING_API_KEY", "Nuxie API key is required.");
    }

    Nuxie instance;
    lock (InstanceGate)
    {
      instance = _instance ??= new Nuxie(_bridgeFactory());
    }

    if (instance._isConfigured)
    {
      throw new NuxieException("ALREADY_CONFIGURED", "Nuxie is already configured.");
    }

    instance._purchaseController = purchaseController;

    try
    {
      await instance._bridge.ConfigureAsync(
        config.ApiKey,
        config.ToBridgeOptions(),
        purchaseController is not null,
        WrapperVersionValue,
        CancellationToken.None
      );
      instance._isConfigured = true;
      return instance;
    }
    catch (NuxieException)
    {
      throw;
    }
    catch (Exception error)
    {
      throw new NuxieException("INVALID_CONFIGURATION", error.Message, inner: error);
    }
  }

  public async Task ShutdownAsync()
  {
    EnsureConfigured();
    await _bridge.ShutdownAsync(CancellationToken.None);
    _isConfigured = false;
    _purchaseController = null;
    lock (InstanceGate)
    {
      if (ReferenceEquals(_instance, this))
      {
        _instance = null;
      }
    }
  }

  public Task IdentifyAsync(
    string distinctId,
    IReadOnlyDictionary<string, object?>? userProperties = null,
    IReadOnlyDictionary<string, object?>? userPropertiesSetOnce = null
  )
  {
    EnsureConfigured();
    return _bridge.IdentifyAsync(distinctId, userProperties, userPropertiesSetOnce, CancellationToken.None);
  }

  public Task ResetAsync(bool keepAnonymousId = false)
  {
    EnsureConfigured();
    return _bridge.ResetAsync(keepAnonymousId, CancellationToken.None);
  }

  public Task<string> GetDistinctIdAsync()
  {
    EnsureConfigured();
    return _bridge.GetDistinctIdAsync(CancellationToken.None);
  }

  public Task<string> GetAnonymousIdAsync()
  {
    EnsureConfigured();
    return _bridge.GetAnonymousIdAsync(CancellationToken.None);
  }

  public Task<bool> GetIsIdentifiedAsync()
  {
    EnsureConfigured();
    return _bridge.GetIsIdentifiedAsync(CancellationToken.None);
  }

  /// <summary>Captures an event. Any matching Journey runs asynchronously in the native SDK.</summary>
  public void Trigger(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
  {
    EnsureConfigured();
    _bridge.Trigger(eventName, properties);
  }

  public Task DismissAsync()
  {
    EnsureConfigured();
    return _bridge.DismissAsync(CancellationToken.None);
  }

  public Task SetLocaleIdentifierAsync(string? localeIdentifier)
  {
    EnsureConfigured();
    return _bridge.SetLocaleIdentifierAsync(localeIdentifier, CancellationToken.None);
  }

  public Task<FeatureAccess> HasFeatureAsync(
    string featureId,
    double requiredBalance = 1,
    string? entityId = null,
    FeatureCheckPolicy policy = FeatureCheckPolicy.CacheFirst
  )
  {
    EnsureConfigured();
    return _bridge.HasFeatureAsync(
      featureId,
      requiredBalance,
      entityId,
      policy,
      CancellationToken.None
    );
  }

  public void UseFeature(
    string featureId,
    double amount = 1,
    string? entityId = null,
    IReadOnlyDictionary<string, object?>? metadata = null
  )
  {
    EnsureConfigured();
    _bridge.UseFeature(featureId, amount, entityId, metadata);
  }

  public Task<FeatureUsageResult> UseFeatureAndWaitAsync(
    string featureId,
    double amount = 1,
    string? entityId = null,
    bool setUsage = false,
    IReadOnlyDictionary<string, object?>? metadata = null
  )
  {
    EnsureConfigured();
    return _bridge.UseFeatureAndWaitAsync(
      featureId,
      amount,
      entityId,
      setUsage,
      metadata,
      CancellationToken.None
    );
  }

  internal static void SetBridgeFactoryForTests(Func<INuxieNativeBridge> bridgeFactory)
  {
    _bridgeFactory = bridgeFactory ?? throw new ArgumentNullException(nameof(bridgeFactory));
  }

  internal static void ResetForTests()
  {
    lock (InstanceGate)
    {
      _instance = null;
      _bridgeFactory = static () => new UnityNativeBridge();
    }
  }

  private async void OnNativeEventReceived(NativeEventEnvelope envelope)
  {
    switch (envelope.Type)
    {
      case NativeEventType.FeatureAccessChanged:
        OnFeatureAccessChanged?.Invoke(NativePayloadMapper.ParseFeatureAccessChanged(envelope));
        return;
      case NativeEventType.Activity:
        OnActivity?.Invoke(NativePayloadMapper.ParseActivity(envelope.Payload));
        return;
      case NativeEventType.AppAction:
        OnAppAction?.Invoke(NativePayloadMapper.ParseAppAction(envelope.Payload));
        return;
      case NativeEventType.PurchaseRequest:
        await HandlePurchaseRequestAsync(envelope);
        return;
      case NativeEventType.RestoreRequest:
        await HandleRestoreRequestAsync(envelope);
        return;
      case NativeEventType.Unknown:
      default:
        return;
    }
  }

  private async Task HandlePurchaseRequestAsync(NativeEventEnvelope envelope)
  {
    var request = NativePayloadMapper.ParsePurchaseRequest(envelope.Payload);
    OnPurchaseRequest?.Invoke(request);

    PurchaseResult result;
    if (_purchaseController is null)
    {
      result = PurchaseResult.Failed("purchase_delegate_not_configured");
    }
    else
    {
      result = await ResolveWithTimeoutAsync(
        _purchaseController.OnPurchaseAsync(request),
        PurchaseResult.Failed("purchase_timeout"),
        error => PurchaseResult.Failed(error.Message)
      );
    }

    await _bridge.CompletePurchaseAsync(request.RequestId, result, CancellationToken.None);
  }

  private async Task HandleRestoreRequestAsync(NativeEventEnvelope envelope)
  {
    var request = NativePayloadMapper.ParseRestoreRequest(envelope.Payload);
    OnRestoreRequest?.Invoke(request);

    RestoreResult result;
    if (_purchaseController is null)
    {
      result = RestoreResult.Failed("purchase_delegate_not_configured");
    }
    else
    {
      result = await ResolveWithTimeoutAsync(
        _purchaseController.OnRestoreAsync(request),
        RestoreResult.Failed("restore_timeout"),
        error => RestoreResult.Failed(error.Message)
      );
    }

    await _bridge.CompleteRestoreAsync(request.RequestId, result, CancellationToken.None);
  }

  private static async Task<T> ResolveWithTimeoutAsync<T>(
    Task<T> operation,
    T timeoutValue,
    Func<Exception, T> failureValue
  )
  {
    try
    {
      var winner = await Task.WhenAny(operation, Task.Delay(PurchaseTimeout));
      return winner == operation ? await operation : timeoutValue;
    }
    catch (Exception error)
    {
      return failureValue(error);
    }
  }

  private void EnsureConfigured()
  {
    if (!_isConfigured)
    {
      throw new NuxieException("NOT_CONFIGURED", "Nuxie SDK has not been configured.");
    }
  }
}
