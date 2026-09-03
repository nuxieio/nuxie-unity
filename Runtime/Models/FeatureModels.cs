namespace Nuxie.Unity;

public enum FeatureCheckPolicy
{
  CacheFirst,
  Remote,
}

public enum FeatureType
{
  Boolean,
  Metered,
  CreditSystem,
}

public sealed class FeatureAccess
{
  public bool Allowed { get; init; }
  public bool Unlimited { get; init; }
  public double? Balance { get; init; }
  public FeatureType Type { get; init; } = FeatureType.Boolean;
}

public sealed class FeatureAccessChangedEvent
{
  public string FeatureId { get; init; } = "";
  public FeatureAccess? From { get; init; }
  public FeatureAccess To { get; init; } = new();
  public long TimestampMs { get; init; }
}

public sealed class FeatureUsageInfo
{
  public double Current { get; init; }
  public double? Limit { get; init; }
  public double? Remaining { get; init; }
}

public sealed class FeatureUsageResult
{
  public bool Success { get; init; }
  public string FeatureId { get; init; } = "";
  public double AmountUsed { get; init; }
  public string? Message { get; init; }
  public FeatureUsageInfo? Usage { get; init; }
  public FeatureAccess? AuthoritativeAccess { get; init; }
}
