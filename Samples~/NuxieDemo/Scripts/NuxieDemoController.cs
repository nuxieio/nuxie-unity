using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Nuxie.Unity.Samples;

public sealed class NuxieDemoController : MonoBehaviour, INuxiePurchaseController
{
  [SerializeField] private string apiKey = "NX_REPLACE_ME";
  [SerializeField] private string distinctId = "unity-demo-user";
  [SerializeField] private string triggerEventName = "upgrade_tapped";
  [SerializeField] private string featureId = "premium";

  private Nuxie? _sdk;

  private async void Start()
  {
    await InitializeAsync();
  }

  private void OnDestroy()
  {
    if (_sdk is null)
    {
      return;
    }

    _sdk.OnFeatureAccessChanged -= OnFeatureAccessChanged;
    _sdk.OnActivity -= OnActivity;
    _sdk.OnAppAction -= OnAppAction;
  }

  [ContextMenu("Initialize Nuxie")]
  public async Task InitializeAsync()
  {
    if (_sdk is not null)
    {
      return;
    }

    try
    {
      _sdk = await Nuxie.ConfigureAsync(
        new NuxieConfig(apiKey)
        {
          Environment = NuxieEnvironment.Production,
          LogLevel = NuxieLogLevel.Info,
          PurchaseHandlingMode = PurchaseHandlingMode.Observer,
        },
        this
      );
      _sdk.OnFeatureAccessChanged += OnFeatureAccessChanged;
      _sdk.OnActivity += OnActivity;
      _sdk.OnAppAction += OnAppAction;

      await _sdk.IdentifyAsync(
        distinctId,
        userProperties: new Dictionary<string, object?>
        {
          ["platform"] = "unity",
          ["app_version"] = Application.version,
        }
      );
      Debug.Log("Nuxie initialized.");
    }
    catch (Exception error)
    {
      Debug.LogException(error);
    }
  }

  [ContextMenu("Trigger Event")]
  public void TriggerEvent()
  {
    if (_sdk is null)
    {
      Debug.LogWarning("Nuxie is not initialized.");
      return;
    }

    _sdk.Trigger(
      triggerEventName,
      new Dictionary<string, object?>
      {
        ["screen"] = "sample",
        ["timestamp_ms"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
      }
    );
  }

  [ContextMenu("Read Feature Access")]
  public async Task ReadFeatureAccessAsync()
  {
    if (_sdk is null)
    {
      return;
    }

    try
    {
      var access = await _sdk.HasFeatureAsync(
        featureId,
        requiredBalance: 1,
        policy: FeatureCheckPolicy.Remote
      );
      Debug.Log($"[Nuxie] Feature {featureId}: allowed={access.Allowed}");
    }
    catch (Exception error)
    {
      Debug.LogException(error);
    }
  }

  [ContextMenu("Dismiss Experience")]
  public async Task DismissAsync()
  {
    if (_sdk is not null)
    {
      await _sdk.DismissAsync();
    }
  }

  [ContextMenu("Shutdown Nuxie")]
  public async Task ShutdownAsync()
  {
    if (_sdk is null)
    {
      return;
    }

    await _sdk.ShutdownAsync();
    _sdk = null;
    Debug.Log("Nuxie shutdown complete.");
  }

  public Task<PurchaseResult> OnPurchaseAsync(PurchaseRequest request)
  {
    Debug.Log($"[Nuxie] Purchase request: {request.StoreProductId} ({request.Platform})");
    return Task.FromResult(PurchaseResult.Failed("purchase_not_implemented"));
  }

  public Task<RestoreResult> OnRestoreAsync(RestoreRequest request)
  {
    Debug.Log($"[Nuxie] Restore request ({request.Platform})");
    return Task.FromResult(RestoreResult.NoPurchases());
  }

  private void OnFeatureAccessChanged(FeatureAccessChangedEvent change)
  {
    Debug.Log($"[Nuxie] Feature changed: {change.FeatureId} allowed={change.To.Allowed}");
  }

  private void OnActivity(NuxieActivityInfo activity)
  {
    Debug.Log($"[Nuxie] Activity: {activity.Name}");
  }

  private void OnAppAction(AppAction action)
  {
    Debug.Log($"[Nuxie] App action: {action.Name} from {action.Experience.ExperienceId}");
  }
}
