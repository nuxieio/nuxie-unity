using Nuxie.Unity.Internal;

namespace Nuxie.Unity.Core.Tests;

public sealed class NuxieTests : IDisposable
{
  public NuxieTests()
  {
    Nuxie.ResetForTests();
  }

  public void Dispose()
  {
    Nuxie.ResetForTests();
  }

  [Fact]
  public async Task ConfigureAsync_RequiresApiKey()
  {
    var error = await Assert.ThrowsAsync<NuxieException>(
      () => Nuxie.ConfigureAsync(new NuxieConfig(""))
    );
    Assert.Equal("MISSING_API_KEY", error.Code);
  }

  [Fact]
  public async Task ConfigureAsync_ForwardsOnlyCompactCustomerOptions()
  {
    var bridge = new FakeNativeBridge();
    Nuxie.SetBridgeFactoryForTests(() => bridge);

    await Nuxie.ConfigureAsync(new NuxieConfig("NX_TEST")
    {
      Environment = NuxieEnvironment.Development,
      LogLevel = NuxieLogLevel.Info,
      RedactSensitiveData = true,
      LocaleIdentifier = "fr-CA",
      PurchaseHandlingMode = PurchaseHandlingMode.Observer,
      TestStoreEnabled = true,
    });

    Assert.Equal(
      new[] {
        "environment",
        "localeIdentifier",
        "logLevel",
        "purchaseHandlingMode",
        "redactSensitiveData",
        "testStoreEnabled",
      },
      bridge.ConfigurationOptions!.Keys.Order()
    );
  }

  [Fact]
  public async Task Trigger_IsEventOnlyAndFireAndForget()
  {
    var bridge = new FakeNativeBridge();
    Nuxie.SetBridgeFactoryForTests(() => bridge);
    var sdk = await Nuxie.ConfigureAsync(new NuxieConfig("NX_TEST"));
    var properties = new Dictionary<string, object?> { ["screen"] = "upgrade" };

    sdk.Trigger("upgrade_tapped", properties);

    Assert.Equal("upgrade_tapped", bridge.TriggerCall?.EventName);
    Assert.Same(properties, bridge.TriggerCall?.Properties);
  }

  [Fact]
  public async Task ResetAsync_DefaultsToNewAnonymousIdentity()
  {
    var bridge = new FakeNativeBridge();
    Nuxie.SetBridgeFactoryForTests(() => bridge);
    var sdk = await Nuxie.ConfigureAsync(new NuxieConfig("NX_TEST"));

    await sdk.ResetAsync();

    Assert.False(bridge.LastResetKeepAnonymousId);
  }

  [Fact]
  public async Task FeatureMethods_PreserveFractionsPolicyAndAuthoritativeAccess()
  {
    var bridge = new FakeNativeBridge();
    Nuxie.SetBridgeFactoryForTests(() => bridge);
    var sdk = await Nuxie.ConfigureAsync(new NuxieConfig("NX_TEST"));

    var access = await sdk.HasFeatureAsync(
      "credits",
      requiredBalance: 1.5,
      entityId: "workspace-1",
      policy: FeatureCheckPolicy.Remote
    );
    sdk.UseFeature("credits", amount: 0.5, entityId: "workspace-1");
    var usage = await sdk.UseFeatureAndWaitAsync("credits", amount: 1.25);

    Assert.Equal(4.5, access.Balance);
    Assert.Equal(1.5, bridge.FeatureCall?.RequiredBalance);
    Assert.Equal(FeatureCheckPolicy.Remote, bridge.FeatureCall?.Policy);
    Assert.Equal(0.5, bridge.UseFeatureCall?.Amount);
    Assert.Equal(3.5, usage.AuthoritativeAccess?.Balance);
  }

  [Fact]
  public async Task TypedJourneyEvents_AreForwardedWithoutTriggerState()
  {
    var bridge = new FakeNativeBridge();
    Nuxie.SetBridgeFactoryForTests(() => bridge);
    var sdk = await Nuxie.ConfigureAsync(new NuxieConfig("NX_TEST"));
    NuxieActivityInfo? activity = null;
    AppAction? action = null;
    sdk.OnActivity += value => activity = value;
    sdk.OnAppAction += value => action = value;

    bridge.Emit(NativeEventType.Activity, new
    {
      schemaVersion = 1,
      id = "activity-1",
      timestampMs = 100L,
      receivedAtMs = 110L,
      name = "experience_shown",
      properties = new { journey_id = "journey-1", count = 2 },
    });
    bridge.Emit(NativeEventType.AppAction, new
    {
      name = "open_settings",
      payload = new { source = "journey" },
      experience = new
      {
        experienceId = "experience-1",
        experienceVersion = "v3",
        journeyId = "journey-1",
      },
    });

    Assert.Equal("experience_shown", activity?.Name);
    Assert.Equal("journey-1", activity?.Properties["journey_id"]);
    Assert.Equal("open_settings", action?.Name);
    Assert.Equal("journey-1", action?.Experience.JourneyId);
  }

  [Fact]
  public async Task PurchaseRequest_UsesCanonicalPortablePayload()
  {
    var bridge = new FakeNativeBridge();
    var controller = new TestPurchaseController();
    Nuxie.SetBridgeFactoryForTests(() => bridge);
    var sdk = await Nuxie.ConfigureAsync(new NuxieConfig("NX_TEST"), controller);
    PurchaseRequest? observed = null;
    sdk.OnPurchaseRequest += request => observed = request;

    bridge.Emit(NativeEventType.PurchaseRequest, new Dictionary<string, object?>
    {
      ["request_id"] = "purchase-1",
      ["platform"] = "android",
      ["product_id"] = "pro",
      ["store_product_id"] = "pro.monthly",
      ["base_plan_id"] = "monthly",
      ["purchase_option_id"] = "trial",
      ["offer_id"] = "launch",
      ["placement_id"] = "upgrade",
      ["display_name"] = "Pro",
      ["display_price"] = "$9.99",
      ["timestamp_ms"] = 123L,
    });

    var completion = await bridge.PurchaseCompletions.Task.WaitAsync(TimeSpan.FromSeconds(1));
    Assert.Equal("pro.monthly", observed?.StoreProductId);
    Assert.Equal("trial", observed?.PurchaseOptionId);
    Assert.Equal("purchase-1", completion.RequestId);
    Assert.Equal(PurchaseResultType.Purchased, completion.Result.Type);
  }

  [Fact]
  public async Task Shutdown_RemovesConfiguredSingleton()
  {
    var bridge = new FakeNativeBridge();
    Nuxie.SetBridgeFactoryForTests(() => bridge);
    var sdk = await Nuxie.ConfigureAsync(new NuxieConfig("NX_TEST"));

    await sdk.ShutdownAsync();

    Assert.Equal(1, bridge.ShutdownCalls);
    Assert.Throws<NuxieException>(() => _ = Nuxie.Instance);
  }

  private sealed class TestPurchaseController : INuxiePurchaseController
  {
    public Task<PurchaseResult> OnPurchaseAsync(PurchaseRequest request) =>
      Task.FromResult(PurchaseResult.Purchased());

    public Task<RestoreResult> OnRestoreAsync(RestoreRequest request) =>
      Task.FromResult(RestoreResult.NoPurchases());
  }
}
