using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CyclicTriggering {

  public partial class CyclicTriggeringService : ICyclicTriggerReceiver {

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


    private static void WaitForNextAllowedExecutionTimeAndRescheduleIfRequired(TriggerTargetInfo targetInfo, int eventScope) {
      targetInfo.LastEx = null;
      targetInfo.ExCount = 0;
      while (targetInfo.NextExecutionNbf > DateTime.UtcNow && !_CancellationToken.IsCancellationRequested) {
        Thread.Sleep(1000);
      }
      targetInfo.CurrentExecutionTask = null;
      if (targetInfo.RescheduleRequested) {
        TriggerTarget(targetInfo, eventScope);
      }
    }

    private static void TriggerTarget(TriggerTargetInfo targetInfo, int eventScope) {

      if (targetInfo.EventScope != eventScope) {
        return;
      }

      if (targetInfo.NextExecutionNbf > DateTime.UtcNow) {
        if (targetInfo.ShouldRescheduleWhileExecuting) {
          targetInfo.RescheduleRequested = true;

          //we need to wait, but there is no active job which can reschedule the target,
          //so we need to start only the rescheduling portiaon
          if (targetInfo.CurrentExecutionTask == null || targetInfo.CurrentExecutionTask.IsCompleted) {
            //using (ExecutionContext.SuppressFlow()) {
              targetInfo.CurrentExecutionTask = Task.Run(
                () => WaitForNextAllowedExecutionTimeAndRescheduleIfRequired(targetInfo, eventScope)
              );
            //}
          }
        }
        return;
      }

      if (targetInfo.CurrentExecutionTask != null && !targetInfo.CurrentExecutionTask.IsCompleted) {
        return;
      }

      // Start a new execution
      targetInfo.NextExecutionNbf = DateTime.UtcNow.AddSeconds(targetInfo.MinWaitSeconds);

      targetInfo.CurrentExecutionTask = targetInfo.Target(_CancellationToken);

      if(targetInfo.CurrentExecutionTask == null) {
        return;
      }

      //reset
      targetInfo.RescheduleRequested = false;

      if (targetInfo.CurrentExecutionTask.Status == TaskStatus.Created) {
        targetInfo.CurrentExecutionTask.Start();
      }

      targetInfo.CurrentExecutionTask = targetInfo.CurrentExecutionTask.ContinueWith((t) => {
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
        }

        WaitForNextAllowedExecutionTimeAndRescheduleIfRequired(targetInfo, eventScope);
        //using (ExecutionContext.SuppressFlow()) {
        //  targetInfo.CurrentExecutionTask = Task.Run(
        //    () => WaitForNextAllowedExecutionTimeAndRescheduleIfRequired(targetInfo, eventScope)
        //  );
        //}

      }, _CancellationToken);



    }

    public void Go() {
      this.Go(0); //0=regular trigger
    }

    protected void Go(int specialEventChannel) {
      lock (_RegisteredTriggerTargets) {
        TriggerTargetInfo[] currentTargets = _RegisteredTriggerTargets.ToArray();

        foreach (TriggerTargetInfo targetInfo in currentTargets) {
          TriggerTarget(targetInfo, specialEventChannel);
        }

      }
    }
    protected virtual bool IsApplicationReadyForLoopbackSelftrigger() {
      return true; //just a special hook for asp.net core...
    }

    protected virtual string TryGetCurrentRequestUrl() {
#if NET_FX
      System.Web.HttpRequest req = System.Web.HttpContext.Current?.Request;
      if(req != null) {
        return req.Url.AbsoluteUri;
      }
#endif
      if (_StartedImmediatelyOverThisUrl != null) {
        return _StartedImmediatelyOverThisUrl;
      }
#if NET_FX
      throw new InvalidOperationException("Could not determine the retrigger-url, because there is no http-request-context available. Please provide a concrete url to 'EnableLoopbackSelftrigger' or use 'EnableInternalSelftrigger' (non-http-based) instead.");
#else
      throw new NotImplementedException("Could not determine the retrigger-url, because youre using the vanilla implementation of this service. If youre running ASP.NET Core WebApi, you can use the more specialized one - otherwise youll need to provide a concrete url to 'EnableLoopbackSelftrigger' or use 'EnableInternalSelftrigger' (non-http-based) instead.");
#endif
     }

    private string GetRetriggerUrl() {
      string currentRequestUrl = this.TryGetCurrentRequestUrl();

      if (
        !string.IsNullOrWhiteSpace(currentRequestUrl) && 
        currentRequestUrl.EndsWith("go",StringComparison.CurrentCultureIgnoreCase)
      ) {
        //only in this case the url can be used for self-triggering...
        return currentRequestUrl;
      } 
      return null; 

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

  }

}
