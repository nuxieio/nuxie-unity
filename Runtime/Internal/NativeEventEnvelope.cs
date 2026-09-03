using System;
using System.Text.Json;

namespace Nuxie.Unity.Internal;

internal enum NativeEventType
{
  FeatureAccessChanged,
  Activity,
  AppAction,
  PurchaseRequest,
  RestoreRequest,
  Unknown,
}

internal sealed class NativeEventEnvelope
{
  public NativeEventType Type { get; init; }
  public long TimestampMs { get; init; }
  public JsonElement Payload { get; init; }

  public static bool TryParse(string json, out NativeEventEnvelope? envelope, out string? error)
  {
    envelope = null;
    error = null;

    if (string.IsNullOrWhiteSpace(json))
    {
      error = "Native event payload was empty.";
      return false;
    }

    try
    {
      using var document = JsonDocument.Parse(json);
      var root = document.RootElement;
      if (root.ValueKind != JsonValueKind.Object)
      {
        error = "Native event payload must be a JSON object.";
        return false;
      }

      var typeRaw = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
      var type = typeRaw switch
      {
        "feature_access_changed" => NativeEventType.FeatureAccessChanged,
        "activity" => NativeEventType.Activity,
        "app_action" => NativeEventType.AppAction,
        "purchase_request" => NativeEventType.PurchaseRequest,
        "restore_request" => NativeEventType.RestoreRequest,
        _ => NativeEventType.Unknown,
      };

      var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
      if (root.TryGetProperty("timestampMs", out var timestampElement) &&
          timestampElement.ValueKind == JsonValueKind.Number &&
          timestampElement.TryGetInt64(out var parsedTimestamp))
      {
        timestampMs = parsedTimestamp;
      }

      var payload = root.TryGetProperty("payload", out var payloadElement)
        ? payloadElement.Clone()
        : root.Clone();

      envelope = new NativeEventEnvelope
      {
        Type = type,
        TimestampMs = timestampMs,
        Payload = payload,
      };
      return true;
    }
    catch (Exception exception)
    {
      error = exception.Message;
      return false;
    }
  }
}
