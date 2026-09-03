using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nuxie.Unity.Internal;

internal sealed class UnityNativeBridge : INuxieNativeBridge
{
  private const string CallbackObjectName = "__NuxieBridgeHost";
  private const string CallbackMethodName = "OnNuxieNativeEvent";

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
  };

  private static event Action<string>? RawNativeEventReceived;

  public event Action<NativeEventEnvelope>? EventReceived;

  public UnityNativeBridge()
  {
    RawNativeEventReceived += OnRawNativeEventReceived;
  }

  internal static void DispatchRawNativeEvent(string json)
  {
    RawNativeEventReceived?.Invoke(json);
  }

  public Task ConfigureAsync(
    string apiKey,
    Dictionary<string, object?> options,
    bool usingPurchaseController,
    string wrapperVersion,
    CancellationToken cancellationToken
  )
  {
    return InvokeVoidAsync(
      "configure",
      new Dictionary<string, object?>(StringComparer.Ordinal)
      {
        ["apiKey"] = apiKey,
        ["options"] = options,
        ["usingPurchaseController"] = usingPurchaseController,
        ["wrapperVersion"] = wrapperVersion,
      },
      cancellationToken
    );
  }

  public Task ShutdownAsync(CancellationToken cancellationToken)
  {
    return InvokeVoidAsync("shutdown", null, cancellationToken);
  }

  public Task IdentifyAsync(
    string distinctId,
    IReadOnlyDictionary<string, object?>? userProperties,
    IReadOnlyDictionary<string, object?>? userPropertiesSetOnce,
    CancellationToken cancellationToken
  )
  {
    return InvokeVoidAsync(
      "identify",
      new Dictionary<string, object?>(StringComparer.Ordinal)
      {
        ["distinctId"] = distinctId,
        ["userProperties"] = userProperties,
        ["userPropertiesSetOnce"] = userPropertiesSetOnce,
      },
      cancellationToken
    );
  }

  public Task ResetAsync(bool keepAnonymousId, CancellationToken cancellationToken)
  {
    return InvokeVoidAsync(
      "reset",
      new Dictionary<string, object?> { ["keepAnonymousId"] = keepAnonymousId },
      cancellationToken
    );
  }

  public Task<string> GetDistinctIdAsync(CancellationToken cancellationToken)
  {
    return InvokeAsync("getDistinctId", null, value => value.GetString() ?? "", cancellationToken);
  }

  public Task<string> GetAnonymousIdAsync(CancellationToken cancellationToken)
  {
    return InvokeAsync("getAnonymousId", null, value => value.GetString() ?? "", cancellationToken);
  }

  public Task<bool> GetIsIdentifiedAsync(CancellationToken cancellationToken)
  {
    return InvokeAsync("getIsIdentified", null, value => value.ValueKind == JsonValueKind.True, cancellationToken);
  }

  public void Trigger(string eventName, IReadOnlyDictionary<string, object?>? properties)
  {
    InvokeVoid(
      "trigger",
      new Dictionary<string, object?>(StringComparer.Ordinal)
      {
        ["eventName"] = eventName,
        ["properties"] = properties,
      }
    );
  }

  public Task DismissAsync(CancellationToken cancellationToken)
  {
    return InvokeVoidAsync("dismiss", null, cancellationToken);
  }

  public Task SetLocaleIdentifierAsync(string? localeIdentifier, CancellationToken cancellationToken)
  {
    return InvokeVoidAsync(
      "setLocaleIdentifier",
      new Dictionary<string, object?> { ["localeIdentifier"] = localeIdentifier },
      cancellationToken
    );
  }

  public Task<FeatureAccess> HasFeatureAsync(
    string featureId,
    double requiredBalance,
    string? entityId,
    FeatureCheckPolicy policy,
    CancellationToken cancellationToken
  )
  {
    return InvokeAsync(
      "hasFeature",
      new Dictionary<string, object?>(StringComparer.Ordinal)
      {
        ["featureId"] = featureId,
        ["requiredBalance"] = requiredBalance,
        ["entityId"] = entityId,
        ["policy"] = policy == FeatureCheckPolicy.Remote ? "remote" : "cacheFirst",
      },
      NativePayloadMapper.ParseFeatureAccess,
      cancellationToken
    );
  }

  public void UseFeature(
    string featureId,
    double amount,
    string? entityId,
    IReadOnlyDictionary<string, object?>? metadata
  )
  {
    InvokeVoid(
      "useFeature",
      new Dictionary<string, object?>(StringComparer.Ordinal)
      {
        ["featureId"] = featureId,
        ["amount"] = amount,
        ["entityId"] = entityId,
        ["metadata"] = metadata,
      }
    );
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
    return InvokeAsync(
      "useFeatureAndWait",
      new Dictionary<string, object?>(StringComparer.Ordinal)
      {
        ["featureId"] = featureId,
        ["amount"] = amount,
        ["entityId"] = entityId,
        ["setUsage"] = setUsage,
        ["metadata"] = metadata,
      },
      NativePayloadMapper.ParseFeatureUsageResult,
      cancellationToken
    );
  }

  public Task CompletePurchaseAsync(
    string requestId,
    PurchaseResult result,
    CancellationToken cancellationToken
  )
  {
    return InvokeVoidAsync(
      "completePurchase",
      new Dictionary<string, object?>(StringComparer.Ordinal)
      {
        ["requestId"] = requestId,
        ["result"] = NativePayloadMapper.PurchaseResultToDictionary(result),
      },
      cancellationToken
    );
  }

  public Task CompleteRestoreAsync(
    string requestId,
    RestoreResult result,
    CancellationToken cancellationToken
  )
  {
    return InvokeVoidAsync(
      "completeRestore",
      new Dictionary<string, object?>(StringComparer.Ordinal)
      {
        ["requestId"] = requestId,
        ["result"] = NativePayloadMapper.RestoreResultToDictionary(result),
      },
      cancellationToken
    );
  }

  private void OnRawNativeEventReceived(string json)
  {
    if (NativeEventEnvelope.TryParse(json, out var envelope, out _) && envelope is not null)
    {
      EventReceived?.Invoke(envelope);
    }
  }

  private Task InvokeVoidAsync(
    string method,
    Dictionary<string, object?>? args,
    CancellationToken cancellationToken
  )
  {
    return InvokeAsync(method, args, _ => true, cancellationToken);
  }

  private async Task<T> InvokeAsync<T>(
    string method,
    Dictionary<string, object?>? args,
    Func<JsonElement, T> mapper,
    CancellationToken cancellationToken
  )
  {
    cancellationToken.ThrowIfCancellationRequested();
    var raw = await Task.Run(() => InvokeNative(method, args), cancellationToken);
    return ParseResponse(method, raw, mapper);
  }

  private void InvokeVoid(string method, Dictionary<string, object?>? args)
  {
    ParseResponse(method, InvokeNative(method, args), _ => true);
  }

  private static T ParseResponse<T>(string method, string raw, Func<JsonElement, T> mapper)
  {
    if (string.IsNullOrWhiteSpace(raw))
    {
      throw new NuxieException("NATIVE_ERROR", $"Native bridge returned an empty response for '{method}'.");
    }

    using var document = JsonDocument.Parse(raw);
    var root = document.RootElement;
    if (!root.TryGetProperty("ok", out var okElement) || okElement.ValueKind != JsonValueKind.True)
    {
      var code = "NATIVE_ERROR";
      var message = $"Native bridge call '{method}' failed.";
      string? nativeStack = null;
      if (root.TryGetProperty("error", out var errorElement) &&
          errorElement.ValueKind == JsonValueKind.Object)
      {
        if (errorElement.TryGetProperty("code", out var codeElement))
        {
          code = codeElement.GetString() ?? code;
        }
        if (errorElement.TryGetProperty("message", out var messageElement))
        {
          message = messageElement.GetString() ?? message;
        }
        if (errorElement.TryGetProperty("nativeStack", out var stackElement))
        {
          nativeStack = stackElement.GetString();
        }
      }
      throw new NuxieException(code, message, nativeStack);
    }

    var value = root.TryGetProperty("value", out var valueElement)
      ? valueElement
      : default;
    return mapper(value);
  }

  private string InvokeNative(string method, Dictionary<string, object?>? args)
  {
    var argsJson = JsonSerializer.Serialize(args ?? new Dictionary<string, object?>(), JsonOptions);

#if UNITY_5_3_OR_NEWER
    NuxieBridgeHost.EnsureCreated();
#endif

#if UNITY_IOS && !UNITY_EDITOR
    var pointer = NuxieUnity_Invoke(method, argsJson, CallbackObjectName, CallbackMethodName);
    if (pointer == IntPtr.Zero)
    {
      return "{\"ok\":false,\"error\":{\"code\":\"NATIVE_ERROR\",\"message\":\"Native invoke returned null.\"}}";
    }

    try
    {
      return Marshal.PtrToStringAnsi(pointer) ?? "";
    }
    finally
    {
      NuxieUnity_FreeCString(pointer);
    }
#elif UNITY_ANDROID && !UNITY_EDITOR
    try
    {
      using var bridgeClass = new UnityEngine.AndroidJavaClass("ai.nuxie.unity.NuxieUnityBridge");
      return bridgeClass.CallStatic<string>(
        "invoke",
        method,
        argsJson,
        CallbackObjectName,
        CallbackMethodName
      ) ?? "{\"ok\":false,\"error\":{\"code\":\"NATIVE_ERROR\",\"message\":\"Android bridge returned null.\"}}";
    }
    catch (Exception error)
    {
      return JsonSerializer.Serialize(
        new
        {
          ok = false,
          error = new
          {
            code = "NATIVE_ERROR",
            message = error.Message,
            nativeStack = error.ToString(),
          },
        },
        JsonOptions
      );
    }
#else
    return JsonSerializer.Serialize(
      new
      {
        ok = false,
        error = new
        {
          code = "NATIVE_ERROR",
          message = "Unity native bridge is only available on iOS/Android player builds.",
        },
      },
      JsonOptions
    );
#endif
  }

#if UNITY_IOS && !UNITY_EDITOR
  [DllImport("__Internal")]
  private static extern IntPtr NuxieUnity_Invoke(
    string method,
    string argsJson,
    string callbackObjectName,
    string callbackMethodName
  );

  [DllImport("__Internal")]
  private static extern void NuxieUnity_FreeCString(IntPtr pointer);
#endif
}
