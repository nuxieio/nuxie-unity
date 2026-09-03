using System;
using System.Collections.Generic;

namespace Nuxie.Unity;

public enum NuxieEnvironment
{
  Production,
  Development,
}

public enum NuxieLogLevel
{
  Verbose,
  Debug,
  Info,
  Warning,
  Error,
  None,
}

public enum PurchaseHandlingMode
{
  Full,
  Observer,
}

/// <summary>Customer-owned values used to configure the native SDK.</summary>
public sealed class NuxieConfig
{
  public NuxieConfig(string apiKey)
  {
    ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
  }

  public string ApiKey { get; }
  public NuxieEnvironment? Environment { get; init; }
  public NuxieLogLevel? LogLevel { get; init; }
  public bool? EnableConsoleLogging { get; init; }
  public bool? RedactSensitiveData { get; init; }
  public string? LocaleIdentifier { get; init; }
  public PurchaseHandlingMode? PurchaseHandlingMode { get; init; }

  /// <summary>Enables the iOS Test Store in development builds. Android ignores this value.</summary>
  public bool? TestStoreEnabled { get; init; }

  internal Dictionary<string, object?> ToBridgeOptions()
  {
    var options = new Dictionary<string, object?>(StringComparer.Ordinal);

    if (Environment.HasValue)
    {
      options["environment"] = Environment.Value == NuxieEnvironment.Development
        ? "development"
        : "production";
    }

    if (LogLevel.HasValue)
    {
      options["logLevel"] = LogLevel.Value switch
      {
        NuxieLogLevel.Verbose => "verbose",
        NuxieLogLevel.Debug => "debug",
        NuxieLogLevel.Info => "info",
        NuxieLogLevel.Warning => "warning",
        NuxieLogLevel.Error => "error",
        NuxieLogLevel.None => "none",
        _ => "warning",
      };
    }

    AddIfNotNull(options, "enableConsoleLogging", EnableConsoleLogging);
    AddIfNotNull(options, "redactSensitiveData", RedactSensitiveData);
    AddIfNotNull(options, "localeIdentifier", LocaleIdentifier);
    if (PurchaseHandlingMode.HasValue)
    {
      options["purchaseHandlingMode"] = PurchaseHandlingMode.Value == global::Nuxie.Unity.PurchaseHandlingMode.Observer
        ? "observer"
        : "full";
    }
    AddIfNotNull(options, "testStoreEnabled", TestStoreEnabled);

    return options;
  }

  private static void AddIfNotNull(Dictionary<string, object?> options, string key, object? value)
  {
    if (value is not null)
    {
      options[key] = value;
    }
  }
}
