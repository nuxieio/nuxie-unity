namespace Nuxie.Unity;

/// <summary>Portable checkout request. Native bridges encode these fields as snake_case.</summary>
public sealed class PurchaseRequest
{
  public string RequestId { get; init; } = "";
  public string Platform { get; init; } = "";
  public string ProductId { get; init; } = "";
  public string StoreProductId { get; init; } = "";
  public string? BasePlanId { get; init; }
  public string? PurchaseOptionId { get; init; }
  public string? OfferId { get; init; }
  public string? PlacementId { get; init; }
  public string? DisplayName { get; init; }
  public string? DisplayPrice { get; init; }
  public long TimestampMs { get; init; }
}

public sealed class RestoreRequest
{
  public string RequestId { get; init; } = "";
  public string Platform { get; init; } = "";
  public long TimestampMs { get; init; }
}

public enum PurchaseResultType
{
  Purchased,
  Cancelled,
  Pending,
  Failed,
}

public sealed class PurchaseResult
{
  public PurchaseResultType Type { get; init; }
  public string? Message { get; init; }

  public static PurchaseResult Purchased() => new() { Type = PurchaseResultType.Purchased };
  public static PurchaseResult Cancelled() => new() { Type = PurchaseResultType.Cancelled };
  public static PurchaseResult Pending() => new() { Type = PurchaseResultType.Pending };
  public static PurchaseResult Failed(string message) => new() { Type = PurchaseResultType.Failed, Message = message };
}

public enum RestoreResultType
{
  Restored,
  NoPurchases,
  Failed,
}

public sealed class RestoreResult
{
  public RestoreResultType Type { get; init; }
  public string? Message { get; init; }

  public static RestoreResult Restored() => new() { Type = RestoreResultType.Restored };
  public static RestoreResult NoPurchases() => new() { Type = RestoreResultType.NoPurchases };
  public static RestoreResult Failed(string message) => new() { Type = RestoreResultType.Failed, Message = message };
}

public interface INuxiePurchaseController
{
  System.Threading.Tasks.Task<PurchaseResult> OnPurchaseAsync(PurchaseRequest request);
  System.Threading.Tasks.Task<RestoreResult> OnRestoreAsync(RestoreRequest request);
}
