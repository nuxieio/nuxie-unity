using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Nuxie.Unity.Internal;

internal static class NativePayloadMapper
{
  internal static FeatureAccessChangedEvent ParseFeatureAccessChanged(NativeEventEnvelope envelope)
  {
    var payload = envelope.Payload;
    FeatureAccess? from = null;
    if (payload.TryGetProperty("from", out var fromElement) &&
        fromElement.ValueKind == JsonValueKind.Object)
    {
      from = ParseFeatureAccess(fromElement);
    }

    var to = payload.TryGetProperty("to", out var toElement) &&
             toElement.ValueKind == JsonValueKind.Object
      ? ParseFeatureAccess(toElement)
      : new FeatureAccess();

    return new FeatureAccessChangedEvent
    {
      FeatureId = GetString(payload, "featureId"),
      From = from,
      To = to,
      TimestampMs = envelope.TimestampMs,
    };
  }

  internal static NuxieActivityInfo ParseActivity(JsonElement payload)
  {
    var properties = payload.TryGetProperty("properties", out var propertiesElement)
      ? ParseScalarDictionary(propertiesElement)
      : new Dictionary<string, object>();

    return new NuxieActivityInfo
    {
      SchemaVersion = GetInt32(payload, "schemaVersion", NuxieActivityInfo.CurrentSchemaVersion),
      Id = GetString(payload, "id"),
      TimestampMs = GetInt64(payload, "timestampMs"),
      ReceivedAtMs = GetInt64(payload, "receivedAtMs"),
      Name = GetString(payload, "name"),
      Properties = properties,
    };
  }

  internal static AppAction ParseAppAction(JsonElement payload)
  {
    IReadOnlyDictionary<string, object>? actionPayload = null;
    if (payload.TryGetProperty("payload", out var actionPayloadElement) &&
        actionPayloadElement.ValueKind == JsonValueKind.Object)
    {
      actionPayload = ParseScalarDictionary(actionPayloadElement);
    }

    var experience = payload.TryGetProperty("experience", out var experienceElement) &&
                     experienceElement.ValueKind == JsonValueKind.Object
      ? experienceElement
      : default;

    return new AppAction
    {
      Name = GetString(payload, "name"),
      Payload = actionPayload,
      Experience = new ExperienceRef
      {
        ExperienceId = GetString(experience, "experienceId"),
        ExperienceVersion = GetNullableString(experience, "experienceVersion"),
        JourneyId = GetNullableString(experience, "journeyId"),
      },
    };
  }

  internal static PurchaseRequest ParsePurchaseRequest(JsonElement payload)
  {
    return new PurchaseRequest
    {
      RequestId = GetString(payload, "request_id"),
      Platform = GetString(payload, "platform"),
      ProductId = GetString(payload, "product_id"),
      StoreProductId = GetString(payload, "store_product_id"),
      BasePlanId = GetNullableString(payload, "base_plan_id"),
      PurchaseOptionId = GetNullableString(payload, "purchase_option_id"),
      OfferId = GetNullableString(payload, "offer_id"),
      PlacementId = GetNullableString(payload, "placement_id"),
      DisplayName = GetNullableString(payload, "display_name"),
      DisplayPrice = GetNullableString(payload, "display_price"),
      TimestampMs = GetInt64(payload, "timestamp_ms"),
    };
  }

  internal static RestoreRequest ParseRestoreRequest(JsonElement payload)
  {
    return new RestoreRequest
    {
      RequestId = GetString(payload, "request_id"),
      Platform = GetString(payload, "platform"),
      TimestampMs = GetInt64(payload, "timestamp_ms"),
    };
  }

  internal static FeatureAccess ParseFeatureAccess(JsonElement element)
  {
    var rawType = GetNullableString(element, "type");
    return new FeatureAccess
    {
      Allowed = GetBoolean(element, "allowed"),
      Unlimited = GetBoolean(element, "unlimited"),
      Balance = GetNullableDouble(element, "balance"),
      Type = rawType switch
      {
        "metered" => FeatureType.Metered,
        "creditSystem" or "credit_system" => FeatureType.CreditSystem,
        _ => FeatureType.Boolean,
      },
    };
  }

  internal static FeatureUsageResult ParseFeatureUsageResult(JsonElement element)
  {
    FeatureUsageInfo? usage = null;
    if (element.TryGetProperty("usage", out var usageElement) &&
        usageElement.ValueKind == JsonValueKind.Object)
    {
      usage = new FeatureUsageInfo
      {
        Current = GetDouble(usageElement, "current"),
        Limit = GetNullableDouble(usageElement, "limit"),
        Remaining = GetNullableDouble(usageElement, "remaining"),
      };
    }

    FeatureAccess? authoritativeAccess = null;
    if (element.TryGetProperty("authoritativeAccess", out var accessElement) &&
        accessElement.ValueKind == JsonValueKind.Object)
    {
      authoritativeAccess = ParseFeatureAccess(accessElement);
    }

    return new FeatureUsageResult
    {
      Success = GetBoolean(element, "success"),
      FeatureId = GetString(element, "featureId"),
      AmountUsed = GetDouble(element, "amountUsed"),
      Message = GetNullableString(element, "message"),
      Usage = usage,
      AuthoritativeAccess = authoritativeAccess,
    };
  }

  internal static Dictionary<string, object?> PurchaseResultToDictionary(PurchaseResult result)
  {
    var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
      ["type"] = result.Type switch
      {
        PurchaseResultType.Purchased => "purchased",
        PurchaseResultType.Cancelled => "cancelled",
        PurchaseResultType.Pending => "pending",
        _ => "failed",
      },
    };
    if (result.Message is not null)
    {
      payload["message"] = result.Message;
    }
    return payload;
  }

  internal static Dictionary<string, object?> RestoreResultToDictionary(RestoreResult result)
  {
    var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
      ["type"] = result.Type switch
      {
        RestoreResultType.Restored => "restored",
        RestoreResultType.NoPurchases => "no_purchases",
        _ => "failed",
      },
    };
    if (result.Message is not null)
    {
      payload["message"] = result.Message;
    }
    return payload;
  }

  private static Dictionary<string, object> ParseScalarDictionary(JsonElement element)
  {
    var result = new Dictionary<string, object>(StringComparer.Ordinal);
    if (element.ValueKind != JsonValueKind.Object)
    {
      return result;
    }

    foreach (var property in element.EnumerateObject())
    {
      var value = ParseScalar(property.Value);
      if (value is not null)
      {
        result[property.Name] = value;
      }
    }
    return result;
  }

  private static object? ParseScalar(JsonElement element)
  {
    return element.ValueKind switch
    {
      JsonValueKind.String => element.GetString(),
      JsonValueKind.True => true,
      JsonValueKind.False => false,
      JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
      JsonValueKind.Number when element.TryGetDouble(out var number) => number,
      _ => null,
    };
  }

  private static string GetString(JsonElement element, string propertyName)
  {
    return GetNullableString(element, propertyName) ?? "";
  }

  private static string? GetNullableString(JsonElement element, string propertyName)
  {
    if (element.ValueKind != JsonValueKind.Object ||
        !element.TryGetProperty(propertyName, out var property) ||
        property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
    {
      return null;
    }
    return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
  }

  private static bool GetBoolean(JsonElement element, string propertyName)
  {
    return element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.True;
  }

  private static int GetInt32(JsonElement element, string propertyName, int fallback)
  {
    return element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(propertyName, out var property) &&
           property.TryGetInt32(out var value)
      ? value
      : fallback;
  }

  private static long GetInt64(JsonElement element, string propertyName)
  {
    return element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(propertyName, out var property) &&
           property.TryGetInt64(out var value)
      ? value
      : 0;
  }

  private static double GetDouble(JsonElement element, string propertyName)
  {
    return GetNullableDouble(element, propertyName) ?? 0;
  }

  private static double? GetNullableDouble(JsonElement element, string propertyName)
  {
    return element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.Number &&
           property.TryGetDouble(out var value)
      ? value
      : null;
  }
}
