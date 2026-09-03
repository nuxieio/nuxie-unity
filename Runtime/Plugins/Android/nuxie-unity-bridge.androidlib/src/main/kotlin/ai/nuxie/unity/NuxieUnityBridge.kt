package ai.nuxie.unity

import ai.nuxie.sdk.AppAction
import ai.nuxie.sdk.AppActionValue
import ai.nuxie.sdk.LogLevel
import ai.nuxie.sdk.Nuxie
import ai.nuxie.sdk.NuxieActivityInfo
import ai.nuxie.sdk.NuxieActivityValue
import ai.nuxie.sdk.NuxieConfiguration
import ai.nuxie.sdk.NuxieEnvironment
import ai.nuxie.sdk.NuxieListener
import ai.nuxie.sdk.billing.NuxiePurchaseDelegate
import ai.nuxie.sdk.billing.PurchaseHandlingMode
import ai.nuxie.sdk.billing.PurchaseResult
import ai.nuxie.sdk.billing.RestoreResult
import ai.nuxie.sdk.billing.StoreProduct
import ai.nuxie.sdk.features.FeatureAccess
import ai.nuxie.sdk.features.FeatureCheckPolicy
import ai.nuxie.sdk.features.FeatureType
import ai.nuxie.sdk.features.FeatureUsageResult
import com.unity3d.player.UnityPlayer
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeoutOrNull
import org.json.JSONArray
import org.json.JSONObject

object NuxieUnityBridge {
  private val purchaseDelegate = UnityPurchaseDelegate(::emit)
  private val listener = object : NuxieListener {
    override fun featureAccessDidChange(
      featureId: String,
      oldAccess: FeatureAccess?,
      newAccess: FeatureAccess,
    ) {
      emit(
        "feature_access_changed",
        mapOf(
          "featureId" to featureId,
          "from" to oldAccess?.toMap(),
          "to" to newAccess.toMap(),
        ),
      )
    }

    override fun onActivityEmitted(sdk: Nuxie, info: NuxieActivityInfo) {
      emit("activity", info.toMap())
    }

    override fun onAppActionRequested(sdk: Nuxie, action: AppAction) {
      emit("app_action", action.toMap())
    }
  }

  @Volatile
  private var callbackObject = "__NuxieBridgeHost"

  @Volatile
  private var callbackMethod = "OnNuxieNativeEvent"

  @JvmStatic
  fun invoke(
    method: String,
    argumentsJson: String,
    callbackObject: String,
    callbackMethod: String,
  ): String {
    this.callbackObject = callbackObject
    this.callbackMethod = callbackMethod

    return runCatching {
      val arguments = jsonObjectToMap(JSONObject(argumentsJson))
      when (method) {
        "configure" -> configure(arguments)
        "shutdown" -> {
          purchaseDelegate.cancelPending("sdk_shutdown")
          runBlocking { Nuxie.shutdown() }
          if (Nuxie.listener === listener) {
            Nuxie.listener = null
          }
          okResponse(null)
        }

        "identify" -> {
          val distinctId = arguments["distinctId"] as? String
            ?: return@runCatching errorResponse(
              "INVALID_ARGUMENTS",
              "identify requires distinctId.",
            )
          Nuxie.identify(
            distinctId = distinctId,
            userProperties = arguments["userProperties"].asStringAnyMap(),
            userPropertiesSetOnce = arguments["userPropertiesSetOnce"].asStringAnyMap(),
          )
          okResponse(null)
        }

        "reset" -> {
          Nuxie.reset(keepAnonymousId = arguments["keepAnonymousId"] as? Boolean ?: false)
          okResponse(null)
        }

        "getDistinctId" -> okResponse(Nuxie.distinctId)
        "getAnonymousId" -> okResponse(Nuxie.anonymousId)
        "getIsIdentified" -> okResponse(Nuxie.isIdentified)

        "trigger" -> {
          val eventName = arguments["eventName"] as? String
            ?: return@runCatching errorResponse(
              "INVALID_ARGUMENTS",
              "trigger requires eventName.",
            )
          Nuxie.trigger(eventName, arguments["properties"].asStringAnyMap())
          okResponse(null)
        }

        "dismiss" -> {
          runBlocking { Nuxie.dismiss() }
          okResponse(null)
        }

        "setLocaleIdentifier" -> {
          runBlocking {
            Nuxie.setLocaleIdentifier(arguments["localeIdentifier"] as? String)
          }
          okResponse(null)
        }

        "hasFeature" -> {
          val featureId = arguments["featureId"] as? String
            ?: return@runCatching errorResponse(
              "INVALID_ARGUMENTS",
              "hasFeature requires featureId.",
            )
          val access = runBlocking {
            Nuxie.hasFeature(
              featureId = featureId,
              requiredBalance = (arguments["requiredBalance"] as? Number)?.toDouble() ?: 1.0,
              entityId = arguments["entityId"] as? String,
              policy = if (arguments["policy"] == "remote") {
                FeatureCheckPolicy.REMOTE
              } else {
                FeatureCheckPolicy.CACHE_FIRST
              },
            )
          }
          okResponse(access.toMap())
        }

        "useFeature" -> {
          val featureId = arguments["featureId"] as? String
            ?: return@runCatching errorResponse(
              "INVALID_ARGUMENTS",
              "useFeature requires featureId.",
            )
          Nuxie.useFeature(
            featureId = featureId,
            amount = (arguments["amount"] as? Number)?.toDouble() ?: 1.0,
            entityId = arguments["entityId"] as? String,
            metadata = arguments["metadata"].asStringAnyMap(),
          )
          okResponse(null)
        }

        "useFeatureAndWait" -> {
          val featureId = arguments["featureId"] as? String
            ?: return@runCatching errorResponse(
              "INVALID_ARGUMENTS",
              "useFeatureAndWait requires featureId.",
            )
          val result = runBlocking {
            Nuxie.useFeatureAndWait(
              featureId = featureId,
              amount = (arguments["amount"] as? Number)?.toDouble() ?: 1.0,
              entityId = arguments["entityId"] as? String,
              setUsage = arguments["setUsage"] as? Boolean ?: false,
              metadata = arguments["metadata"].asStringAnyMap(),
            )
          }
          okResponse(result.toMap())
        }

        "completePurchase" -> {
          val requestId = arguments["requestId"] as? String
            ?: return@runCatching errorResponse(
              "INVALID_ARGUMENTS",
              "completePurchase requires requestId.",
            )
          val result = arguments["result"].asStringAnyMap()
            ?: return@runCatching errorResponse(
              "INVALID_ARGUMENTS",
              "completePurchase requires result.",
            )
          purchaseDelegate.completePurchase(requestId, result)
          okResponse(null)
        }

        "completeRestore" -> {
          val requestId = arguments["requestId"] as? String
            ?: return@runCatching errorResponse(
              "INVALID_ARGUMENTS",
              "completeRestore requires requestId.",
            )
          val result = arguments["result"].asStringAnyMap()
            ?: return@runCatching errorResponse(
              "INVALID_ARGUMENTS",
              "completeRestore requires result.",
            )
          purchaseDelegate.completeRestore(requestId, result)
          okResponse(null)
        }

        else -> errorResponse("NATIVE_ERROR", "Unsupported method '$method'.")
      }
    }.getOrElse { error ->
      errorResponse(
        "NATIVE_ERROR",
        error.message ?: "Native bridge failure.",
        error.stackTraceToString(),
      )
    }
  }

  private fun configure(arguments: Map<String, Any?>): String {
    val apiKey = arguments["apiKey"] as? String
      ?: return errorResponse("MISSING_API_KEY", "Nuxie API key is required.")
    if (apiKey.isBlank()) {
      return errorResponse("MISSING_API_KEY", "Nuxie API key is required.")
    }

    val context = UnityPlayer.currentActivity?.applicationContext
      ?: return errorResponse("NO_CONTEXT", "Unity activity is unavailable.")
    val options = arguments["options"].asStringAnyMap()
    val usePurchaseController = arguments["usingPurchaseController"] as? Boolean ?: false

    Nuxie.listener = listener
    try {
      Nuxie.setup(
        context,
        NuxieConfiguration(apiKey).apply {
        environment = if (options?.get("environment") == "development") {
          NuxieEnvironment.DEVELOPMENT
        } else {
          NuxieEnvironment.PRODUCTION
        }
        logLevel = when (options?.get("logLevel") as? String) {
          "verbose", "debug" -> LogLevel.DEBUG
          "info" -> LogLevel.INFO
          "error" -> LogLevel.ERROR
          "none" -> LogLevel.NONE
          else -> LogLevel.WARN
        }
        if (options?.containsKey("localeIdentifier") == true) {
          localeIdentifier = options["localeIdentifier"] as? String
        }
        purchaseHandlingMode = if (options?.get("purchaseHandlingMode") == "observer") {
          PurchaseHandlingMode.APP_MANAGED
        } else {
          PurchaseHandlingMode.NUXIE_MANAGED
        }
        if (usePurchaseController) {
          purchaseDelegate = this@NuxieUnityBridge.purchaseDelegate
        }
        },
      )
    } catch (error: Throwable) {
      if (!Nuxie.isSetup && Nuxie.listener === listener) {
        Nuxie.listener = null
      }
      throw error
    }
    return okResponse(null)
  }

  private fun emit(type: String, payload: Map<String, Any?>) {
    val message = JSONObject(
      mapOf(
        "type" to type,
        "timestampMs" to System.currentTimeMillis(),
        "payload" to payload,
      ),
    ).toString()

    val objectName = callbackObject
    val methodName = callbackMethod
    val activity = UnityPlayer.currentActivity
    if (activity == null) {
      UnityPlayer.UnitySendMessage(objectName, methodName, message)
    } else {
      activity.runOnUiThread {
        UnityPlayer.UnitySendMessage(objectName, methodName, message)
      }
    }
  }

  private fun okResponse(value: Any?): String = JSONObject()
    .put("ok", true)
    .put("value", value ?: JSONObject.NULL)
    .toString()

  private fun errorResponse(
    code: String,
    message: String,
    nativeStack: String? = null,
  ): String {
    val error = JSONObject()
      .put("code", code)
      .put("message", message)
    if (nativeStack != null) {
      error.put("nativeStack", nativeStack)
    }
    return JSONObject()
      .put("ok", false)
      .put("error", error)
      .toString()
  }
}

private class UnityPurchaseDelegate(
  private val emit: (String, Map<String, Any?>) -> Unit,
  private val timeoutMs: Long = 60_000,
) : NuxiePurchaseDelegate {
  private val purchases = ConcurrentHashMap<String, CompletableDeferred<PurchaseResult>>()
  private val restores = ConcurrentHashMap<String, CompletableDeferred<RestoreResult>>()

  override suspend fun purchase(product: StoreProduct): PurchaseResult {
    val requestId = UUID.randomUUID().toString()
    val deferred = CompletableDeferred<PurchaseResult>()
    purchases[requestId] = deferred

    emit(
      "purchase_request",
      mapOf(
        "request_id" to requestId,
        "platform" to "android",
        "product_id" to product.productId,
        "store_product_id" to product.storeProductId,
        "base_plan_id" to product.basePlanId,
        "purchase_option_id" to product.purchaseOptionId,
        "offer_id" to product.offerId,
        "placement_id" to product.placementId,
        "display_name" to product.rawProduct?.name,
        "display_price" to null,
        "timestamp_ms" to System.currentTimeMillis(),
      ),
    )

    return try {
      withTimeoutOrNull(timeoutMs) { deferred.await() }
        ?: PurchaseResult.Failed(bridgeError("purchase_timeout"))
    } finally {
      purchases.remove(requestId)
    }
  }

  override suspend fun restorePurchases(): RestoreResult {
    val requestId = UUID.randomUUID().toString()
    val deferred = CompletableDeferred<RestoreResult>()
    restores[requestId] = deferred

    emit(
      "restore_request",
      mapOf(
        "request_id" to requestId,
        "platform" to "android",
        "timestamp_ms" to System.currentTimeMillis(),
      ),
    )

    return try {
      withTimeoutOrNull(timeoutMs) { deferred.await() }
        ?: RestoreResult.Failed(bridgeError("restore_timeout"))
    } finally {
      restores.remove(requestId)
    }
  }

  fun completePurchase(requestId: String, payload: Map<String, Any?>) {
    purchases.remove(requestId)?.complete(
      when ((payload["type"] as? String)?.lowercase()) {
        "purchased" -> PurchaseResult.Purchased
        "cancelled" -> PurchaseResult.Cancelled
        "pending" -> PurchaseResult.Pending
        else -> PurchaseResult.Failed(
          bridgeError((payload["message"] as? String) ?: "purchase_failed"),
        )
      },
    )
  }

  fun completeRestore(requestId: String, payload: Map<String, Any?>) {
    restores.remove(requestId)?.complete(
      when ((payload["type"] as? String)?.lowercase()) {
        "restored" -> RestoreResult.Restored
        "no_purchases" -> RestoreResult.NoPurchases
        else -> RestoreResult.Failed(
          bridgeError((payload["message"] as? String) ?: "restore_failed"),
        )
      },
    )
  }

  fun cancelPending(reason: String) {
    purchases.values.forEach { it.complete(PurchaseResult.Failed(bridgeError(reason))) }
    restores.values.forEach { it.complete(RestoreResult.Failed(bridgeError(reason))) }
    purchases.clear()
    restores.clear()
  }

  private fun bridgeError(message: String): Throwable = IllegalStateException(message)
}

private fun FeatureType.toBridgeValue(): String = when (this) {
  FeatureType.BOOLEAN -> "boolean"
  FeatureType.METERED -> "metered"
  FeatureType.CREDIT_SYSTEM -> "creditSystem"
}

private fun FeatureAccess.toMap(): Map<String, Any?> = mapOf(
  "allowed" to allowed,
  "unlimited" to unlimited,
  "balance" to balance,
  "type" to type.toBridgeValue(),
)

private fun FeatureUsageResult.toMap(): Map<String, Any?> = mapOf(
  "success" to success,
  "featureId" to featureId,
  "amountUsed" to amountUsed,
  "message" to message,
  "usage" to usage?.let { value ->
    mapOf(
      "current" to value.current,
      "limit" to value.limit,
      "remaining" to value.remaining,
    )
  },
  "authoritativeAccess" to authoritativeAccess?.toMap(),
)

private fun NuxieActivityInfo.toMap(): Map<String, Any?> = mapOf(
  "schemaVersion" to NuxieActivityInfo.SCHEMA_VERSION,
  "id" to id,
  "timestampMs" to timestampMillis,
  "receivedAtMs" to receivedAtMillis,
  "name" to name,
  "properties" to properties.mapValues { (_, value) -> value.toBridgeValue() },
)

private fun NuxieActivityValue.toBridgeValue(): Any = when (this) {
  is NuxieActivityValue.String -> value
  is NuxieActivityValue.Int -> value
  is NuxieActivityValue.Double -> value
  is NuxieActivityValue.Bool -> value
}

private fun AppAction.toMap(): Map<String, Any?> = mapOf(
  "name" to name,
  "payload" to payload?.mapValues { (_, value) -> value.toBridgeValue() },
  "experience" to mapOf(
    "experienceId" to experience.experienceId,
    "experienceVersion" to experience.experienceVersion,
    "journeyId" to experience.journeyId,
  ),
)

private fun AppActionValue.toBridgeValue(): Any = when (this) {
  is AppActionValue.String -> value
  is AppActionValue.Int -> value
  is AppActionValue.Double -> value
  is AppActionValue.Bool -> value
}

private fun jsonObjectToMap(value: JSONObject): Map<String, Any?> {
  val result = mutableMapOf<String, Any?>()
  val keys = value.keys()
  while (keys.hasNext()) {
    val key = keys.next()
    result[key] = jsonValue(value.get(key))
  }
  return result
}

private fun jsonArrayToList(value: JSONArray): List<Any?> =
  (0 until value.length()).map { index -> jsonValue(value.get(index)) }

private fun jsonValue(value: Any?): Any? = when (value) {
  null, JSONObject.NULL -> null
  is JSONObject -> jsonObjectToMap(value)
  is JSONArray -> jsonArrayToList(value)
  else -> value
}

@Suppress("UNCHECKED_CAST")
private fun Any?.asStringAnyMap(): Map<String, Any?>? = this as? Map<String, Any?>
