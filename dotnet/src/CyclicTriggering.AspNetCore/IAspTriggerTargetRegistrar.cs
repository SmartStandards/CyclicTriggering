using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

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
    void EnableTriggeringEndpoint(
      string endpointRoute = ".well-known/cyclic-trigger"
    );

    /// <summary>
    /// Will register a async-filter (asp-hook) to trigger the cyclic operations on every incomming request!
    /// (sounds like a performace-overkill but isn't - there are some semaphores)
    /// </summary>
    void EnableTriggerOnAnyEnpoint();

    /// <summary>
    /// Registers a trigger target to be executed when a special trigger-event occurs.
    /// </summary>
    /// <param name="target">
    ///  A really (short running) method that will be invoked each time.
    ///  NOTE: the given method will be invoked SYNCHRONOUSLY, so it MUST NOT be long running!
    /// </param>
    /// <param name="aspSpecialTrigger">
    ///   A well-known asp event to be used to trigger a given target.
    /// </param>
    /// <param name="minWaitSeconds">
    ///  During this period any re-triggering will be ignored
    /// </param> 
    void AddTriggerTarget(
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
