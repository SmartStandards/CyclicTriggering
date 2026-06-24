using Microsoft.AspNetCore.Http;

namespace CyclicTriggering {

  public interface IAspTriggerTargetRegistrar : ITriggerTargetRegistrar{

    /// <summary>
    /// Will register a trigger endpoint that can be called from outside to trigger the registered targets.
    /// NOTE: The given route is the URL path where the trigger ENDPOINT will be exposed, but the
    /// trigger METHOD itself will work by sending a POST request to "{endpointRoute}/go" and
    /// the body of the request needs to be an empty json capsule like this: { }
    /// </summary>
    /// <param name="endpointRoute">
    /// <see href="https://github.com/SmartStandards/.well-known.cyclic-trigger?utm_source=chatgpt.com">.well-known/cyclic-trigger</see>
    /// </param>
    /// <param name="anonymousAccessAllowed"></param> 
    void EnableTriggeringEndpoint(
      string endpointRoute = ".well-known/cyclic-trigger",
      bool anonymousAccessAllowed = true
    );

    /// <summary>
    /// Will register a async-filter (asp-hook) to trigger the cyclic operations on every incomming request!
    /// (sounds like a performace-overkill but isn't - there are some semaphores)
    /// </summary>
    void EnableTriggerOnAnyEnpoint();

    void EnableAmbientHttpInfoProvider();

    /// <summary>
    /// Registers a trigger target to be executed when a special trigger-event occurs.
    /// </summary>
    /// <param name="target">
    ///  A REALLY SHORT RUNNING method that will be invoked each time.
    ///  NOTE: the given method will be invoked SYNCHRONOUSLY, so it WILL BLOCK THE APPLICATION!
    /// </param>
    /// <param name="aspSpecialTrigger">
    ///   A well-known asp event to be used to trigger a given target.
    /// </param>
    /// <param name="minWaitSeconds">
    ///  During this period any re-triggering will be ignored
    /// </param> 
    void AddTriggerTargetForAspEvent(
      Action<CancellationToken> target, AspSpecialTrigger aspSpecialTrigger, int minWaitSeconds = 0
    );

  }

  /// <summary>
  ///   A well-known asp event to be used to trigger a given target.
  /// </summary>
  public enum AspSpecialTrigger { 
    //None/Regular = 0,
    OnApplicationReady = 1,
    OnApplicationStopping = 2,
    /// <summary> DANGER!!! DONT USE THIS IF YOU ARE NOT COMPLETELY AWARE ABOUT THE RISK THAT THIS CAN CAUSE HEAVY PERFORMANCE ISSUES!</summary>
    OnEachRequest = 3
  }

}
