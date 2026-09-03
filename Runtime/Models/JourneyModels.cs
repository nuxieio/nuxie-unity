using System.Collections.Generic;

namespace Nuxie.Unity;

public sealed class ExperienceRef
{
  public string ExperienceId { get; init; } = "";
  public string? ExperienceVersion { get; init; }
  public string? JourneyId { get; init; }
}

public sealed class AppAction
{
  public string Name { get; init; } = "";
  public IReadOnlyDictionary<string, object>? Payload { get; init; }
  public ExperienceRef Experience { get; init; } = new();
}

public sealed class NuxieActivityInfo
{
  public const int CurrentSchemaVersion = 1;

  public int SchemaVersion { get; init; } = CurrentSchemaVersion;
  public string Id { get; init; } = "";
  public long TimestampMs { get; init; }
  public long ReceivedAtMs { get; init; }
  public string Name { get; init; } = "";
  public IReadOnlyDictionary<string, object> Properties { get; init; } =
    new Dictionary<string, object>();
}
