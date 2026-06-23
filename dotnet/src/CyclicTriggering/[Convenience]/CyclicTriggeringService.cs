using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CyclicTriggering {

  public class CyclicTriggeringService : ICyclicTriggerReceiver, ITriggerTargetRegistrar {

    //NOTE: complete implementation is static, because some runtimes (like WCF)
    //might create multiple instances of this class, but the framework is designed
    //to control the trigger execution globally across all instances!

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static CancellationTokenSource _InternalCancellationTokenSource;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static CancellationToken _CancellationToken;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static List<TriggerTargetInfo> _RegisteredTriggerTargets = new List<TriggerTargetInfo>();

    static CyclicTriggeringService() {
      _InternalCancellationTokenSource = new CancellationTokenSource();
      _CancellationToken = _InternalCancellationTokenSource.Token;
    }

    public CyclicTriggeringService() {
    }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    private TriggerTargetInfo[] CurrentTargets { 
      get {
        lock (_RegisteredTriggerTargets) {
          return _RegisteredTriggerTargets.ToArray();
        }
      }
    }

    /// <summary>
    /// Used to bind the internal threads to a root application lifetime
    /// </summary>
    public CancellationToken GlobalRuntimeCancellationToken {
      get {
        return _CancellationToken;
      }
      set {
        _CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
          _InternalCancellationTokenSource.Token, value
        ).Token;
      }
    }

    public void Shutdown() {
      _InternalCancellationTokenSource.Cancel();
    }

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
      CyclicTriggeringService.OnExceptionOccoured onExceptionOccoured = null
    ) {
      this.AddTriggerTarget(
        (ct) => new Task(() => target(ct),ct),
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
            RescheduleWhileExecuting = rescheduleWhileExecting,
            OnExceptionOccoured = onExceptionOccoured,
            EventScope = eventScope
          }
        );
      }
    }

    /// <summary> 
    ///  A callback which will be called when an exception occours during the execution of the target.
    ///  This will give you the opportunity to implement a custom retry logic by modifying the suspendUntilUtc
    ///  parameter based on the exception and the number of times this exception has already occoured.
    /// </summary>
    /// <param name="ex">The Exception which occoured during the exectution</param>
    /// <param name="exCounter">The Exception-Counter (how many times this occoured)</param>
    /// <param name="suspendUntilUtc">A pre-calculated DateTime(utc), which is a recommendation based on the Exception-Counter</param>
    public delegate void OnExceptionOccoured(Exception ex,int exCounter, ref DateTime suspendUntilUtc);

    private static void TriggerTarget(TriggerTargetInfo targetInfo, int eventScope) {

      if (targetInfo.EventScope != eventScope || targetInfo.NextExecutionNbf < DateTime.UtcNow) {
        return;
      }
        
      if (targetInfo.CurrentExecutionTask == null || targetInfo.CurrentExecutionTask.IsCompleted) {

          // Start a new execution
          targetInfo.NextExecutionNbf = DateTime.UtcNow.AddSeconds(targetInfo.MinWaitSeconds);

          targetInfo.CurrentExecutionTask = targetInfo.Target(_CancellationToken);

          if(targetInfo.CurrentExecutionTask == null) {
            return;
          }

          if(targetInfo.CurrentExecutionTask.Status == TaskStatus.Created) {
            targetInfo.CurrentExecutionTask.Start();
          }

          //reset
          targetInfo.RescheduleRequestedWhileExecuting = false;

          targetInfo.CurrentExecutionTask.ContinueWith((t) => {
            if (!t.IsFaulted) {
              targetInfo.LastEx = null;
              targetInfo.ExCount = 0;
            }
            else { 
              Exception ex = t.Exception;

              targetInfo.LastEx = ex;
              targetInfo.ExCount++;

              DateTime nextTry = DateTime.UtcNow.AddSeconds(
                targetInfo.MinWaitSeconds + ((targetInfo.ExCount - 1) * targetInfo.ExCount)
              //  +0*1 +1*2 +2*3 +3*4 +4*5 +5*6 +6*7 +7*8 +8*9 +9*10
              //  = 0s, 7s, 22s, 52s, 102s, 202s, 382s, 722s, 1422s, 2822s
              );

              if (targetInfo.OnExceptionOccoured != null) {
                targetInfo.OnExceptionOccoured(ex, targetInfo.ExCount, ref nextTry);
              }
              targetInfo.NextExecutionNbf = nextTry;
              if (nextTry < DateTime.UtcNow) {
                targetInfo.RescheduleRequestedWhileExecuting = true;
              }
            }
          }, _CancellationToken);
      
      } else if (targetInfo.RescheduleWhileExecuting) {
        // Mark for rescheduling after current execution finishes
        targetInfo.RescheduleRequestedWhileExecuting = true;   
      }
    }

    public void Go() {
      this.Go(0); //0=regular trigger
    }

    protected void Go(int eventscope) {
      lock (_RegisteredTriggerTargets) {
        TriggerTargetInfo[] currentTargets = _RegisteredTriggerTargets.ToArray();

        foreach (TriggerTargetInfo targetInfo in currentTargets) {
          TriggerTarget(targetInfo, eventscope);
        }

      }
    }

    protected virtual string GetRetriggerUrl() {
#if NET_FX
      System.Web.HttpRequest req = System.Web.HttpContext.Current?.Request;
      if(req != null) {
        if (req.Url.AbsoluteUri.EndsWith("/go")) {
          return req.Url.AbsoluteUri;
        }
        return null; //wrong endpoint!
      }
      else if (_StartedImmediatelyOverThisUrl != null) {
        return _StartedImmediatelyOverThisUrl;
      }
      else {
        throw new InvalidOperationException("Could not determine the retrigger-url, because there is no http-request-context available. Please provide a concrete url to 'EnableLoopbackSelftrigger' or use 'EnableInternalSelftrigger' (non-http-based) instead.");
      }
#endif
      throw new NotImplementedException("Could not determine the retrigger-url, because youre using the vanilla implementation of this service. If youre running ASP.NET Core WebApi, you can use the more specialized one - otherwise youll need to provide a concrete url to 'EnableLoopbackSelftrigger' or use 'EnableInternalSelftrigger' (non-http-based) instead.");
    }

    protected void SendSelftriggerHttpCall(string url) {
      try {
        using (var client = new System.Net.Http.HttpClient()) {
          client.Timeout = TimeSpan.FromSeconds(5);
          client.PostAsync(
            url, 
            new System.Net.Http.StringContent("{ }",
            System.Text.Encoding.UTF8,
            "application/json")
          ).GetAwaiter().GetResult();
        }
      }
      catch (Exception ex) {
        //InsLogger.LogWarning(ex);
      }
    }

    protected virtual bool IsApplicationReadyForLoopbackSelftrigger() {
      return true; //just a special hook for asp.net core...
    }

    private string _StartedImmediatelyOverThisUrl = null;

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
    ///  If this parameter is set, the first http-trigger call will be sent to the given url 3sec after this method is called (if the application ist ready at this time), 
    ///  instead of waiting for the first incomming real request from a external participant. 
    ///  This is useful to ensure a direct start of the self-triggering mechanism,
    ///  but you'll need to know your own public url of the service to be able to set this parameter correctly (not easy under webservers like IIS). 
    /// </param>
    public void EnableLoopbackSelftrigger(int retriggerIntervalSec = 10, string startSelfInitiatedOverThisUrl = null) {
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
          if(retriggerUrl == null) {
            return null; 
          }

          return new Task(() => { //the task itself
            Task.Delay((retriggerIntervalSec * 1000) + 100, ct).Wait(ct);
            while (!this.IsApplicationReadyForLoopbackSelftrigger() && !ct.IsCancellationRequested && !_CancellationToken.IsCancellationRequested) {
              Thread.Sleep(500);
            }
            if (!ct.IsCancellationRequested && !_CancellationToken.IsCancellationRequested) {
              this.SendSelftriggerHttpCall(retriggerUrl);
            }
          }, ct);

        },
        minWaitSeconds: retriggerIntervalSec,
        rescheduleWhileExecting: true
      );

      if(!string.IsNullOrWhiteSpace(startSelfInitiatedOverThisUrl)) {
        Task.Run(() => {
          Task.Delay(3000, _CancellationToken).Wait(_CancellationToken);
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
            Task.Delay((retriggerIntervalSec * 1000) + 100, ct).Wait(ct);
            if (!ct.IsCancellationRequested && !_CancellationToken.IsCancellationRequested) {
              this.Go();
            }
          }, ct);

        },
        minWaitSeconds: retriggerIntervalSec,
        rescheduleWhileExecting: true
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
      public bool RescheduleWhileExecuting { get; set; }
      public DateTime NextExecutionNbf { get; set; } = DateTime.MinValue;
      public Task CurrentExecutionTask { get; set; } = null;
      public bool RescheduleRequestedWhileExecuting { get; set; } = false;
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
          else if(this.RescheduleRequestedWhileExecuting) {
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
