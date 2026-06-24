using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CyclicTriggering {

  public partial class CyclicTriggeringService : ITriggerTargetRegistrar {

    protected bool _InternalSelftriggerEnabled = false;
    protected bool _LoopbackSelftriggerEnabled = false;

    private string _StartedImmediatelyOverThisUrl = null;

    #region " Convenience overload "

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
    public void AddTriggerTarget(
      Action<CancellationToken> target, int minWaitSeconds = 5, bool rescheduleWhileExecting = true,
      OnExceptionOccoured onExceptionOccoured = null
    ) {
      this.AddTriggerTarget(
        (ct) => new Task(() => target(ct), ct),
        minWaitSeconds, rescheduleWhileExecting, onExceptionOccoured, 0
      );
    }

    #endregion

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
    public void AddTriggerTarget(
      Func<CancellationToken, Task> taskFactory, int minWaitSeconds = 5, bool rescheduleWhileExecting = true,
      OnExceptionOccoured onExceptionOccoured = null
    ) {
      this.AddTriggerTarget(taskFactory, minWaitSeconds, rescheduleWhileExecting, onExceptionOccoured, 0);
    }

    protected void AddTriggerTarget(
     Func<CancellationToken, Task> taskFactory, int minWaitSeconds, bool rescheduleWhileExecting,
     OnExceptionOccoured onExceptionOccoured, int eventScope
    ) {
      lock (_RegisteredTriggerTargets) {
        _RegisteredTriggerTargets.Add(
          new TriggerTargetInfo() {
            Target = taskFactory,
            MinWaitSeconds = minWaitSeconds,
            ShouldRescheduleWhileExecuting = rescheduleWhileExecting,
            OnExceptionOccoured = onExceptionOccoured,
            EventScope = eventScope
          }
        );
      }
    }

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
    public void EnableLoopbackSelftrigger(int retriggerIntervalSec = 10, string startSelfInitiatedOverThisUrl = null) {
      if (_LoopbackSelftriggerEnabled) {
        throw new InvalidOperationException(nameof(EnableLoopbackSelftrigger) + " was already called!");
      }
      _LoopbackSelftriggerEnabled = true;
      _StartedImmediatelyOverThisUrl = startSelfInitiatedOverThisUrl;
      if (retriggerIntervalSec < 2) {
        retriggerIntervalSec = 1;
      }
      else {
        //we need a gap that we can-reschedule our self "on-time"
        retriggerIntervalSec = retriggerIntervalSec - 1;
      }

      this.AddTriggerTarget(
        (ct) => { //the factory

          //IMPORTANT: this construction (reason, why were using a factory here) is have a chance to
          //do some preparation inside of the http-request-lifecycle, so that we can get the correct
          //base-url for the retriggering-url (because it is not known at startup-time)
          string retriggerUrl = this.GetRetriggerUrl();

          //were sill not part of a regular incomming 'go'-request!!!
          if (retriggerUrl == null) {
            return null;
          }

          return new Task(() => { //the task itself

            Task.Delay((retriggerIntervalSec * 1000), ct).Wait(ct);

            if (!ct.IsCancellationRequested && !_CancellationToken.IsCancellationRequested) {

              Task.Run(() => {
                //very important: we need to run this in a separate thread, because the current thread
                //needs to have ended before the retriggering http-call is sent, otherwise the
                //retriggering will not start ourself again!
                Thread.Sleep(500);

                if (!ct.IsCancellationRequested && !_CancellationToken.IsCancellationRequested) { 
                  this.SendSelftriggerHttpCall(retriggerUrl);
                }

              });

            }
          }, ct);

        },
        minWaitSeconds: retriggerIntervalSec,
        rescheduleWhileExecting: false //we alyways wand to be executed within the request-lifecycle!
      );

      if (!string.IsNullOrWhiteSpace(startSelfInitiatedOverThisUrl)) {
        Task.Run(() => {
          Task.Delay(3000, this.GlobalRuntimeCancellationToken).Wait(this.GlobalRuntimeCancellationToken);
          while (!this.IsApplicationReadyForLoopbackSelftrigger() && !this.GlobalRuntimeCancellationToken.IsCancellationRequested) {
            Thread.Sleep(500);
          }
          this.SendSelftriggerHttpCall(startSelfInitiatedOverThisUrl);
        }, _CancellationToken);
      }

    }

    /// <summary>
    /// Activates an internal self-triggering mechanism that will periodically invoke the registered targets.
    /// IMPORTANT: The initial tigger will fire asynchonously, 3sec after this method is called, and will
    /// not be running under any request context (if this is needed, the 'EnableLoopbackSelftrigger' method can be used instead)!
    /// </summary>
    /// <param name="retriggerIntervalSec">
    ///  After the first trigger was received, this delay will be used to send the next trigger...
    /// </param>
    public void EnableInternalSelftrigger(int retriggerIntervalSec = 10) {
      if (_InternalSelftriggerEnabled) {
        throw new InvalidOperationException(nameof(EnableInternalSelftrigger) + " was already called!");
      }
      _InternalSelftriggerEnabled = true;
      if (retriggerIntervalSec < 2) {
        retriggerIntervalSec = 1;
      }
      else {
        //we need a gap that we can-reschedule our self "on-time"
        retriggerIntervalSec = retriggerIntervalSec - 1;
      }
      this.AddTriggerTarget(
        (ct) => { //the factory

          return new Task(() => { //the task itself

            Task.Delay((retriggerIntervalSec * 1000), ct).Wait(ct);

            if (!ct.IsCancellationRequested && !_CancellationToken.IsCancellationRequested) {

              Task.Run(() => {
                //very important: we need to run this in a separate thread, because the current thread
                //needs to have ended before the retriggering http-call is sent, otherwise the
                //retriggering will not start ourself again!
                Thread.Sleep(500);

                if (!ct.IsCancellationRequested && !_CancellationToken.IsCancellationRequested) {
                  this.Go();
                }

              });

            }
          }, ct);

        },
        minWaitSeconds: retriggerIntervalSec,
        rescheduleWhileExecting: false
      );

      Task.Run(() => {
        Task.Delay(3000, _CancellationToken).Wait(_CancellationToken);
        this.Go();
      }, _CancellationToken);
    }

    [DebuggerDisplay("{ExecState} ({DisplayName})")]
    private class TriggerTargetInfo {

      public Func<CancellationToken, Task> Target { get; set; }
      public int MinWaitSeconds { get; set; }
      public bool ShouldRescheduleWhileExecuting { get; set; }
      public DateTime NextExecutionNbf { get; set; } = DateTime.MinValue;
      public Task CurrentExecutionTask { get; set; } = null;
      public bool RescheduleRequested { get; set; } = false;
      public int ExCount { get; set; } = 0;
      public Exception LastEx { get; set; } = null;

      #region " Debugger "

      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      public string DisplayName {
        get {
          if (Target == null) {
            return "NULL-Target";
          }
          MethodInfo mi = this.Target.GetMethodInfo();
          return $"{mi.DeclaringType.FullName}.{mi.Name}";
        }
      }

      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      public string ExecState {
        get {
          if (this.CurrentExecutionTask == null) {
            return "never executed";
          }
          else if (this.CurrentExecutionTask.IsCompleted) {
            if (this.LastEx != null) {
              return $"FAILED: '{this.LastEx.Message}'";
            }
            else {
              return "idle";
            }
          }
          else if (this.RescheduleRequested) {
            return "running (+rescheduled)";
          }
          else {
            return "running";
          }
        }
      }

      public OnExceptionOccoured OnExceptionOccoured { get; internal set; }
      public int EventScope { get; internal set; }

      #endregion

    }

  }

}
