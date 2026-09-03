import Foundation

#if canImport(Nuxie)
import Nuxie
#endif

@_cdecl("NuxieUnity_Invoke")
public func NuxieUnity_Invoke(
  _ methodPointer: UnsafePointer<CChar>?,
  _ argumentsPointer: UnsafePointer<CChar>?,
  _ callbackObjectPointer: UnsafePointer<CChar>?,
  _ callbackMethodPointer: UnsafePointer<CChar>?
) -> UnsafeMutablePointer<CChar>? {
  let method = methodPointer.map(String.init(cString:)) ?? ""
  let arguments = argumentsPointer.map(String.init(cString:)) ?? "{}"
  let callbackObject = callbackObjectPointer.map(String.init(cString:)) ?? "__NuxieBridgeHost"
  let callbackMethod = callbackMethodPointer.map(String.init(cString:)) ?? "OnNuxieNativeEvent"

  return strdup(
    UnityNuxieBridge.shared.invoke(
      method: method,
      argumentsJSON: arguments,
      callbackObject: callbackObject,
      callbackMethod: callbackMethod
    )
  )
}

@_cdecl("NuxieUnity_FreeCString")
public func NuxieUnity_FreeCString(_ pointer: UnsafeMutablePointer<CChar>?) {
  free(pointer)
}

private final class UnityNuxieBridge: @unchecked Sendable {
  static let shared = UnityNuxieBridge()

  private let targetLock = NSLock()
  private var callbackObject = "__NuxieBridgeHost"
  private var callbackMethod = "OnNuxieNativeEvent"

  #if canImport(Nuxie)
  private lazy var purchaseDelegate = UnityPurchaseDelegateBridge(emit: emit)
  @MainActor private lazy var delegate = UnityDelegateBridge(emit: emit)
  #endif

  func invoke(
    method: String,
    argumentsJSON: String,
    callbackObject: String,
    callbackMethod: String
  ) -> String {
    setCallbackTarget(object: callbackObject, method: callbackMethod)
    guard let arguments = parseObject(argumentsJSON) else {
      return errorResponse(code: "INVALID_ARGUMENTS", message: "Arguments must be a JSON object.")
    }

    #if canImport(Nuxie)
    switch method {
    case "configure":
      guard let apiKey = arguments["apiKey"] as? String,
            !apiKey.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
      else {
        return errorResponse(code: "MISSING_API_KEY", message: "Nuxie API key is required.")
      }
      let options = UnsafeSendable(arguments["options"] as? [String: Any])
      let usePurchaseController = arguments["usingPurchaseController"] as? Bool ?? false
      return mapResult(blocking {
        try await MainActor.run {
          let configuration = self.makeConfiguration(
            apiKey: apiKey,
            options: options.value,
            usePurchaseController: usePurchaseController
          )
          NuxieSDK.shared.delegate = self.delegate
          do {
            try NuxieSDK.shared.setup(with: configuration)
            return true
          } catch {
            NuxieSDK.shared.delegate = nil
            throw error
          }
        }
      }) { _ in nil }

    case "shutdown":
      purchaseDelegate.cancelPending(reason: "sdk_shutdown")
      return mapResult(blocking {
        await NuxieSDK.shared.shutdown()
        await MainActor.run {
          NuxieSDK.shared.delegate = nil
        }
        return true
      }) { _ in nil }

    case "identify":
      guard let distinctId = arguments["distinctId"] as? String else {
        return errorResponse(code: "INVALID_ARGUMENTS", message: "identify requires distinctId.")
      }
      NuxieSDK.shared.identify(
        distinctId,
        userProperties: arguments["userProperties"] as? [String: Any],
        userPropertiesSetOnce: arguments["userPropertiesSetOnce"] as? [String: Any]
      )
      return okResponse(nil)

    case "reset":
      NuxieSDK.shared.reset(keepAnonymousId: arguments["keepAnonymousId"] as? Bool ?? false)
      return okResponse(nil)

    case "getDistinctId":
      return okResponse(NuxieSDK.shared.getDistinctId())

    case "getAnonymousId":
      return okResponse(NuxieSDK.shared.getAnonymousId())

    case "getIsIdentified":
      return okResponse(NuxieSDK.shared.isIdentified)

    case "trigger":
      guard let eventName = arguments["eventName"] as? String else {
        return errorResponse(code: "INVALID_ARGUMENTS", message: "trigger requires eventName.")
      }
      NuxieSDK.shared.trigger(
        eventName,
        properties: arguments["properties"] as? [String: Any]
      )
      return okResponse(nil)

    case "dismiss":
      return mapResult(blocking {
        await NuxieSDK.shared.dismiss()
        return true
      }) { _ in nil }

    case "setLocaleIdentifier":
      let localeIdentifier = arguments["localeIdentifier"] as? String
      return mapResult(blocking {
        try await NuxieSDK.shared.setLocaleIdentifier(localeIdentifier)
        return true
      }) { _ in nil }

    case "hasFeature":
      guard let featureId = arguments["featureId"] as? String else {
        return errorResponse(code: "INVALID_ARGUMENTS", message: "hasFeature requires featureId.")
      }
      let requiredBalance = number(arguments["requiredBalance"]) ?? 1
      let entityId = arguments["entityId"] as? String
      let useRemotePolicy = arguments["policy"] as? String == "remote"
      return mapResult(blocking {
        let access = try await NuxieSDK.shared.hasFeature(
          featureId,
          requiredBalance: requiredBalance,
          entityId: entityId,
          policy: useRemotePolicy ? .remote : .cacheFirst
        )
        return self.featureAccessDictionary(access)
      })

    case "useFeature":
      guard let featureId = arguments["featureId"] as? String else {
        return errorResponse(code: "INVALID_ARGUMENTS", message: "useFeature requires featureId.")
      }
      NuxieSDK.shared.useFeature(
        featureId,
        amount: number(arguments["amount"]) ?? 1,
        entityId: arguments["entityId"] as? String,
        metadata: arguments["metadata"] as? [String: Any]
      )
      return okResponse(nil)

    case "useFeatureAndWait":
      guard let featureId = arguments["featureId"] as? String else {
        return errorResponse(code: "INVALID_ARGUMENTS", message: "useFeatureAndWait requires featureId.")
      }
      let amount = number(arguments["amount"]) ?? 1
      let entityId = arguments["entityId"] as? String
      let setUsage = arguments["setUsage"] as? Bool ?? false
      let metadata = UnsafeSendable(arguments["metadata"] as? [String: Any])
      return mapResult(blocking {
        let result = try await NuxieSDK.shared.useFeatureAndWait(
          featureId,
          amount: amount,
          entityId: entityId,
          setUsage: setUsage,
          metadata: metadata.value
        )
        return self.featureUsageResultDictionary(result)
      })

    case "completePurchase":
      guard let requestId = arguments["requestId"] as? String,
            let payload = arguments["result"] as? [String: Any]
      else {
        return errorResponse(
          code: "INVALID_ARGUMENTS",
          message: "completePurchase requires requestId and result."
        )
      }
      purchaseDelegate.completePurchase(requestId: requestId, payload: payload)
      return okResponse(nil)

    case "completeRestore":
      guard let requestId = arguments["requestId"] as? String,
            let payload = arguments["result"] as? [String: Any]
      else {
        return errorResponse(
          code: "INVALID_ARGUMENTS",
          message: "completeRestore requires requestId and result."
        )
      }
      purchaseDelegate.completeRestore(requestId: requestId, payload: payload)
      return okResponse(nil)

    default:
      return errorResponse(code: "NATIVE_ERROR", message: "Unsupported method '\(method)'.")
    }
    #else
    return errorResponse(code: "NATIVE_UNAVAILABLE", message: "The Nuxie iOS SDK is not linked.")
    #endif
  }

  private func setCallbackTarget(object: String, method: String) {
    targetLock.withLock {
      callbackObject = object
      callbackMethod = method
    }
  }

  private func callbackTarget() -> (String, String) {
    targetLock.withLock { (callbackObject, callbackMethod) }
  }

  private func parseObject(_ raw: String) -> [String: Any]? {
    guard let data = raw.data(using: .utf8),
          let value = try? JSONSerialization.jsonObject(with: data)
    else {
      return nil
    }
    return value as? [String: Any]
  }

  private func number(_ value: Any?) -> Double? {
    (value as? NSNumber)?.doubleValue
  }

  #if canImport(Nuxie)
  @MainActor
  private func makeConfiguration(
    apiKey: String,
    options: [String: Any]?,
    usePurchaseController: Bool
  ) -> NuxieConfiguration {
    let configuration = NuxieConfiguration(apiKey: apiKey)

    if options?["environment"] as? String == "development" {
      configuration.environment = .development
    } else if options?["environment"] != nil {
      configuration.environment = .production
    }

    if let logLevel = options?["logLevel"] as? String {
      switch logLevel {
      case "verbose": configuration.logLevel = .verbose
      case "debug": configuration.logLevel = .debug
      case "info": configuration.logLevel = .info
      case "error": configuration.logLevel = .error
      case "none": configuration.logLevel = .none
      default: configuration.logLevel = .warning
      }
    }

    if let value = options?["enableConsoleLogging"] as? Bool {
      configuration.enableConsoleLogging = value
    }
    if let value = options?["redactSensitiveData"] as? Bool {
      configuration.redactSensitiveData = value
    }
    if options?.keys.contains("localeIdentifier") == true {
      configuration.localeIdentifier = options?["localeIdentifier"] as? String
    }
    if let value = options?["purchaseHandlingMode"] as? String {
      configuration.purchaseHandlingMode = value == "observer" ? .observer : .full
    }
    if let value = options?["testStoreEnabled"] as? Bool {
      configuration.testStoreEnabled = value
    }
    if usePurchaseController {
      configuration.purchaseDelegate = purchaseDelegate
    }

    return configuration
  }

  private func featureAccessDictionary(_ access: FeatureAccess) -> [String: Any] {
    [
      "allowed": access.allowed,
      "unlimited": access.unlimited,
      "balance": nullable(access.balance),
      "type": access.type.rawValue,
    ]
  }

  private func featureUsageResultDictionary(_ result: FeatureUsageResult) -> [String: Any] {
    let usage: Any
    if let value = result.usage {
      usage = [
        "current": value.current,
        "limit": nullable(value.limit),
        "remaining": nullable(value.remaining),
      ]
    } else {
      usage = NSNull()
    }

    return [
      "success": result.success,
      "featureId": result.featureId,
      "amountUsed": result.amountUsed,
      "message": nullable(result.message),
      "usage": usage,
      "authoritativeAccess": nullable(result.authoritativeAccess.map(featureAccessDictionary)),
    ]
  }
  #endif

  private func blocking<T>(
    _ operation: @escaping @Sendable () async throws -> T
  ) -> Result<T, Error> {
    let semaphore = DispatchSemaphore(value: 0)
    let box = BlockingResultBox<T>()
    Task.detached {
      do {
        box.store(.success(try await operation()))
      } catch {
        box.store(.failure(error))
      }
      semaphore.signal()
    }
    semaphore.wait()
    return box.load() ?? .failure(
      NSError(
        domain: "ai.nuxie.unity",
        code: -1,
        userInfo: [NSLocalizedDescriptionKey: "Native operation did not produce a result."]
      )
    )
  }

  private func mapResult<T>(
    _ result: Result<T, Error>,
    transform: (T) -> Any? = { $0 }
  ) -> String {
    switch result {
    case .success(let value):
      return okResponse(transform(value))
    case .failure(let error):
      return errorResponse(
        code: "NATIVE_ERROR",
        message: error.localizedDescription,
        nativeStack: String(describing: error)
      )
    }
  }

  private func okResponse(_ value: Any?) -> String {
    serialize(["ok": true, "value": nullable(value)])
  }

  private func errorResponse(
    code: String,
    message: String,
    nativeStack: String? = nil
  ) -> String {
    var error: [String: Any] = ["code": code, "message": message]
    if let nativeStack {
      error["nativeStack"] = nativeStack
    }
    return serialize(["ok": false, "error": error])
  }

  private func serialize(_ value: Any) -> String {
    guard JSONSerialization.isValidJSONObject(value),
          let data = try? JSONSerialization.data(withJSONObject: value),
          let string = String(data: data, encoding: .utf8)
    else {
      return "{\"ok\":false,\"error\":{\"code\":\"NATIVE_ERROR\",\"message\":\"JSON serialization failed.\"}}"
    }
    return string
  }

  private func emit(_ type: String, _ payload: [String: Any]) {
    let message = serialize([
      "type": type,
      "timestampMs": Int(Date().timeIntervalSince1970 * 1_000),
      "payload": payload,
    ])
    let (object, method) = callbackTarget()
    DispatchQueue.main.async {
      sendUnityMessage(object: object, method: method, payload: message)
    }
  }
}

private final class UnsafeSendable<Value>: @unchecked Sendable {
  let value: Value

  init(_ value: Value) {
    self.value = value
  }
}

private final class BlockingResultBox<Value>: @unchecked Sendable {
  private let lock = NSLock()
  private var value: Result<Value, Error>?

  func store(_ value: Result<Value, Error>) {
    lock.withLock {
      self.value = value
    }
  }

  func load() -> Result<Value, Error>? {
    lock.withLock { value }
  }
}

private func nullable(_ value: Any?) -> Any {
  value ?? NSNull()
}

#if canImport(Nuxie)
@MainActor
private final class UnityDelegateBridge: NuxieDelegate {
  private let emit: @Sendable (String, [String: Any]) -> Void

  init(emit: @escaping @Sendable (String, [String: Any]) -> Void) {
    self.emit = emit
  }

  func featureAccessDidChange(
    _ featureId: String,
    from oldValue: FeatureAccess?,
    to newValue: FeatureAccess
  ) {
    emit("feature_access_changed", [
      "featureId": featureId,
      "from": nullable(oldValue.map(featureAccessDictionary)),
      "to": featureAccessDictionary(newValue),
    ])
  }

  func nuxieDidEmit(_ info: NuxieActivityInfo) {
    emit("activity", [
      "schemaVersion": NuxieActivityInfo.schemaVersion,
      "id": info.id,
      "timestampMs": Int(info.timestamp.timeIntervalSince1970 * 1_000),
      "receivedAtMs": Int(info.receivedAt.timeIntervalSince1970 * 1_000),
      "name": info.name,
      "properties": info.properties.mapValues(activityValue),
    ])
  }

  func nuxie(_ sdk: NuxieSDK, didRequestAppAction action: AppAction) {
    emit("app_action", [
      "name": action.name,
      "payload": nullable(action.payload?.mapValues(appActionValue)),
      "experience": [
        "experienceId": action.experience.experienceId,
        "experienceVersion": nullable(action.experience.experienceVersion),
        "journeyId": nullable(action.experience.journeyId),
      ],
    ])
  }
}

private func featureAccessDictionary(_ access: FeatureAccess) -> [String: Any] {
  [
    "allowed": access.allowed,
    "unlimited": access.unlimited,
    "balance": nullable(access.balance),
    "type": access.type.rawValue,
  ]
}

private func activityValue(_ value: NuxieActivityValue) -> Any {
  switch value {
  case .string(let value): value
  case .int(let value): value
  case .double(let value): value
  case .bool(let value): value
  }
}

private func appActionValue(_ value: AppActionValue) -> Any {
  switch value {
  case .string(let value): value
  case .int(let value): value
  case .double(let value): value
  case .bool(let value): value
  }
}

private final class UnityPurchaseDelegateBridge: NuxiePurchaseDelegate, @unchecked Sendable {
  private let emit: @Sendable (String, [String: Any]) -> Void
  private let timeoutSeconds: TimeInterval
  private let lock = NSLock()
  private var purchases: [String: CheckedContinuation<PurchaseResult, Never>] = [:]
  private var restores: [String: CheckedContinuation<RestoreResult, Never>] = [:]

  init(
    timeoutSeconds: TimeInterval = 60,
    emit: @escaping @Sendable (String, [String: Any]) -> Void
  ) {
    self.timeoutSeconds = timeoutSeconds
    self.emit = emit
  }

  func purchase(product: StoreProduct) async -> PurchaseResult {
    let requestId = UUID().uuidString
    return await withCheckedContinuation { continuation in
      lock.withLock {
        purchases[requestId] = continuation
      }
      emit("purchase_request", [
        "request_id": requestId,
        "platform": "ios",
        "product_id": product.productId,
        "store_product_id": product.storeProductId,
        "base_plan_id": NSNull(),
        "purchase_option_id": NSNull(),
        "offer_id": NSNull(),
        "placement_id": nullable(product.placementId),
        "display_name": product.name,
        "display_price": product.price,
        "timestamp_ms": Int(Date().timeIntervalSince1970 * 1_000),
      ])
      schedulePurchaseTimeout(requestId)
    }
  }

  func restorePurchases() async -> RestoreResult {
    let requestId = UUID().uuidString
    return await withCheckedContinuation { continuation in
      lock.withLock {
        restores[requestId] = continuation
      }
      emit("restore_request", [
        "request_id": requestId,
        "platform": "ios",
        "timestamp_ms": Int(Date().timeIntervalSince1970 * 1_000),
      ])
      scheduleRestoreTimeout(requestId)
    }
  }

  func completePurchase(requestId: String, payload: [String: Any]) {
    let continuation = lock.withLock { purchases.removeValue(forKey: requestId) }
    continuation?.resume(returning: purchaseResult(payload))
  }

  func completeRestore(requestId: String, payload: [String: Any]) {
    let continuation = lock.withLock { restores.removeValue(forKey: requestId) }
    continuation?.resume(returning: restoreResult(payload))
  }

  func cancelPending(reason: String) {
    let pending = lock.withLock {
      let pending = (Array(purchases.values), Array(restores.values))
      purchases.removeAll()
      restores.removeAll()
      return pending
    }
    pending.0.forEach { $0.resume(returning: .failed(bridgeError(reason))) }
    pending.1.forEach { $0.resume(returning: .failed(bridgeError(reason))) }
  }

  private func schedulePurchaseTimeout(_ requestId: String) {
    Task { [weak self] in
      guard let self else { return }
      try? await Task.sleep(nanoseconds: UInt64(self.timeoutSeconds * 1_000_000_000))
      let continuation = self.lock.withLock {
        self.purchases.removeValue(forKey: requestId)
      }
      continuation?.resume(returning: .failed(self.bridgeError("purchase_timeout")))
    }
  }

  private func scheduleRestoreTimeout(_ requestId: String) {
    Task { [weak self] in
      guard let self else { return }
      try? await Task.sleep(nanoseconds: UInt64(self.timeoutSeconds * 1_000_000_000))
      let continuation = self.lock.withLock {
        self.restores.removeValue(forKey: requestId)
      }
      continuation?.resume(returning: .failed(self.bridgeError("restore_timeout")))
    }
  }

  private func purchaseResult(_ payload: [String: Any]) -> PurchaseResult {
    switch (payload["type"] as? String)?.lowercased() {
    case "purchased": .purchased
    case "cancelled": .cancelled
    case "pending": .pending
    default: .failed(bridgeError((payload["message"] as? String) ?? "purchase_failed"))
    }
  }

  private func restoreResult(_ payload: [String: Any]) -> RestoreResult {
    switch (payload["type"] as? String)?.lowercased() {
    case "restored": .restored
    case "no_purchases": .noPurchases
    default: .failed(bridgeError((payload["message"] as? String) ?? "restore_failed"))
    }
  }

  private func bridgeError(_ message: String) -> Error {
    NSError(
      domain: "ai.nuxie.unity",
      code: 1,
      userInfo: [NSLocalizedDescriptionKey: message]
    )
  }
}
#endif

private func sendUnityMessage(object: String, method: String, payload: String) {
  object.withCString { objectPointer in
    method.withCString { methodPointer in
      payload.withCString { payloadPointer in
        UnitySendMessage(objectPointer, methodPointer, payloadPointer)
      }
    }
  }
}

@_silgen_name("UnitySendMessage")
private func UnitySendMessage(
  _ object: UnsafePointer<CChar>,
  _ method: UnsafePointer<CChar>,
  _ message: UnsafePointer<CChar>
)
