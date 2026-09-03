using System.Text.Json;
using Nuxie.Unity.Internal;

namespace Nuxie.Unity.Core.Tests;

public sealed class NativePayloadMapperTests
{
  [Fact]
  public void ParseFeatureUsageResult_PreservesAuthoritativeFractionalAccess()
  {
    using var document = JsonDocument.Parse(
      """
      {
        "success": true,
        "featureId": "credits",
        "amountUsed": 1.25,
        "message": null,
        "usage": {
          "current": 4.5,
          "limit": 10.5,
          "remaining": 6
        },
        "authoritativeAccess": {
          "allowed": true,
          "unlimited": false,
          "balance": 6.25,
          "type": "creditSystem"
        }
      }
      """
    );

    var result = NativePayloadMapper.ParseFeatureUsageResult(document.RootElement);

    Assert.Equal(1.25, result.AmountUsed);
    Assert.Equal(4.5, result.Usage?.Current);
    Assert.Equal(6.25, result.AuthoritativeAccess?.Balance);
    Assert.Equal(FeatureType.CreditSystem, result.AuthoritativeAccess?.Type);
  }

  [Fact]
  public void CompletionPayloads_UseCanonicalResultValuesOnly()
  {
    Assert.Equal(
      new Dictionary<string, object?> { ["type"] = "purchased" },
      NativePayloadMapper.PurchaseResultToDictionary(PurchaseResult.Purchased())
    );
    Assert.Equal(
      new Dictionary<string, object?> { ["type"] = "restored" },
      NativePayloadMapper.RestoreResultToDictionary(RestoreResult.Restored())
    );
  }
}
