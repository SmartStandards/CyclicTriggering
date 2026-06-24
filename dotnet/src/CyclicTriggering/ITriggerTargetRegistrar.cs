using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CyclicTriggering {

  /// <summary> 
  ///  A callback which will be called when an exception occours during the execution of the target.
  ///  This will give you the opportunity to implement a custom retry logic by modifying the suspendUntilUtc
  ///  parameter based on the exception and the number of times this exception has already occoured.
  /// </summary>
  /// <param name="ex">The Exception which occoured during the exectution</param>
  /// <param name="exCounter">The Exception-Counter (how many times this occoured)</param>
  /// <param name="suspendUntilUtc">A pre-calculated DateTime(utc), which is a recommendation based on the Exception-Counter</param>
  public delegate void OnExceptionOccoured(Exception ex, int exCounter, ref DateTime suspendUntilUtc);

  public interface ITriggerTargetRegistrar {

    /// <summary>
    /// Registers a trigger target to be executed when a trigger is received.
    /// </summary>
    /// <param name="target">
    ///  A really (short running) method that will be invoked each time.
    ///  NOTE: the given method MUST NOT be long running, because the cyclic triggering
    ///  service will reschedule it again and again - AVOID any LONG RUNNING LOOP implementations
    ///  inside of that returned task - it's the job of the framework!
    /// </param>
    /// <param name="minWaitSeconds">
    ///  During this period any re-triggering will be ignored, 
    ///  even if the previous execution is already finished.
    ///  The framework recommends a best practive interval of 10-20s.
    /// </param>
    /// <param name="rescheduleWhileExecuting">
    ///  Controls the behavior if a trigger is received while the previous execution is still running.
    ///  If set to true, the trigger will be rescheduled to execute immediately after the current execution finishes. 
    ///  If set to false, any trigger received during an ongoing execution will be ignored and not rescheduled.
    /// </param>
    /// <param name="onExceptionOccoured">
    ///  A callback which will be called when an exception occours during the execution of the target.
    ///  This will give you the opportunity to implement a custom retry logic by modifying the suspendUntilUtc
    ///  parameter based on the exception and the number of times this exception has already occoured.
    /// </param>
    void AddTriggerTarget(
      Action<CancellationToken> target, int minWaitSeconds = 5, bool rescheduleWhileExecuting = true,
      OnExceptionOccoured onExceptionOccoured = null
    );

    /// <summary>
    /// Registers a trigger target to be executed when a trigger is received.
    /// </summary>
    /// <param name="taskFactory">
    ///  A really (short running) factory that creates the task to be executed cyclically.
    ///  NOTE: the returned task itself MUST NOT be long running, because the cyclic triggering
    ///  service will reschedule it again and again - AVOID any LONG RUNNING LOOP implementations
    ///  inside of that returned task - it's the job of the framework!
    /// </param>
    /// <param name="minWaitSeconds">
    ///  During this period any re-triggering will be ignored, 
    ///  even if the previous execution is already finished.
    ///  The framework recommends a best practive interval of 10-20s.
    /// </param>
    /// <param name="rescheduleWhileExecting">
    ///  Controls the behavior if a trigger is received while the previous execution is still running.
    ///  If set to true, the trigger will be rescheduled to execute immediately after the current execution finishes. 
    ///  If set to false, any trigger received during an ongoing execution will be ignored and not rescheduled.
    /// </param>
    /// <param name="onExceptionOccoured">
    ///  A callback which will be called when an exception occours during the execution of the target.
    ///  This will give you the opportunity to implement a custom retry logic by modifying the suspendUntilUtc
    ///  parameter based on the exception and the number of times this exception has already occoured.
    /// </param>
    void AddTriggerTarget(
      Func<CancellationToken, Task> taskFactory, int minWaitSeconds = 5, bool rescheduleWhileExecting = true,
      OnExceptionOccoured onExceptionOccoured = null
    );

    /// <summary>
    /// Activates an internal self-triggering mechanism that will periodically send an http-request to trigger the registered targets
    /// over a 'ICyclicTriggerReceiver'-endpoint which MUST BE HOSTED BY THE SAME APPLICATION!
    /// This high-layer loopback is useful to keep webservices alive and preserve the availabliy of request-context relased information.
    /// (if this is not needed, the 'EnableInternalSelftrigger' method can be used instead)
    /// </summary>
    /// <param name="retriggerIntervalSec">
    ///  After the first trigger was received, this delay will be used to send the next trigger-request...
    /// </param>
    /// <param name="startSelfInitiatedOverThisUrl">
    ///  If this parameter is set, the first http-trigger call will be sent to the given url
    ///  3sec after this method is called (if the application ist ready at this time), 
    ///  instead of waiting for the first incomming real request from a external participant. 
    ///  This is useful to ensure a direct start of the self-triggering mechanism,
    ///  but you'll need to know your own public url of the service to be able to set this parameter correctly (not easy under webservers like IIS). 
    /// </param>
    void EnableLoopbackSelftrigger(
      int retriggerIntervalSec = 10,
      string startSelfInitiatedOverThisUrl = null
    );

    /// <summary>
    /// Activates an internal self-triggering mechanism that will periodically invoke the registered targets.
    /// IMPORTANT: The initial tigger will fire asynchonously, 3sec after this method is called, and will
    /// not be running under any request context (if this is needed, the 'EnableLoopbackSelftrigger' method can be used instead)!
    /// </summary>
    /// <param name="retriggerIntervalSec">
    ///  After the first trigger was received, this delay will be used to invoke the next trigger...
    /// </param>
    void EnableInternalSelftrigger(
      int retriggerIntervalSec = 10
    );

  }

}
