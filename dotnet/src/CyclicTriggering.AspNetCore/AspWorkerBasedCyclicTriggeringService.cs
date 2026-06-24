using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UJMW;

namespace CyclicTriggering {

  public partial class AspWorkerBasedCyclicTriggeringService : CyclicTriggeringService, IAspTriggerTargetRegistrar {

    public AspWorkerBasedCyclicTriggeringService() {
    }

    private string _TriggeringEndpointShouldBeRegisteredOn = null;
    private bool _TriggeringEndpointShouldAllowAnonymous = false;

    private bool _GoShouldBeCalledForEachRequest = false;
    private bool _SpecialEventShouldBeCalledForEachRequest = false;
    private bool _ApplicationIsReady = false;
    private Task _DelayedHardShutdownTask = null;
    internal bool _AmbientHttpInfoProviderShouldBeRegistered;

    protected override bool IsApplicationReadyForLoopbackSelftrigger() {
      return _ApplicationIsReady;
    }

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
    public void AddTriggerTargetForAspEvent(
      Action<CancellationToken> target, AspSpecialTrigger aspSpecialTrigger, int minWaitSeconds = 0
    ) {

      this.AddTriggerTarget(
        (ct)=> {
          target(ct);
          return null;//a little hack to get things executed synchronously...
        }, minWaitSeconds, false, null, (int)aspSpecialTrigger
      );

      if(aspSpecialTrigger == AspSpecialTrigger.OnEachRequest) {
        _SpecialEventShouldBeCalledForEachRequest = true;
      }

    }

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
    public void EnableTriggeringEndpoint(
      string endpointRoute = ".well-known/cyclic-trigger", bool anonymousAccessAllowed = true
    ) {
      _TriggeringEndpointShouldBeRegisteredOn = endpointRoute;
      _TriggeringEndpointShouldAllowAnonymous = anonymousAccessAllowed;
    }

    /// <summary>
    /// Will register a async-filter (asp-hook) to trigger the cyclic operations on every incomming request!
    /// (sounds like a performace-overkill but isn't - there are some semaphores)
    /// </summary>
    /// 
    public void EnableTriggerOnAnyEnpoint() {
      _GoShouldBeCalledForEachRequest = true;
    }

    public void EnableAmbientHttpInfoProvider() {
      _AmbientHttpInfoProviderShouldBeRegistered = true;
    }

    internal void HandleSpecialTrigger(AspSpecialTrigger specialTrigger) {
      if(specialTrigger == AspSpecialTrigger.OnApplicationReady) {
        _ApplicationIsReady = true;
      }
      base.Go((int)specialTrigger);
    }

    protected override string TryGetCurrentRequestUrl() {
      return GetCurrentRequestUrl(); 
    }

    internal void RegisterUjmwEndpointIfRequired(
      IServiceCollection services
    ) {

      if (string.IsNullOrWhiteSpace(_TriggeringEndpointShouldBeRegisteredOn)) {
        return;
      }

      //register the executor itself as ujmw-endpoint...
      services.AddDynamicUjmwControllers(
        (DynamicUjmwControllerRegistrar ujmw) => {
          ujmw.AddControllerFor<ICyclicTriggerReceiver>((opt) => {
            opt.ControllerRoute = _TriggeringEndpointShouldBeRegisteredOn;
            opt.EnableRequestSidechannel = false;
            opt.EnableResponseSidechannel = false;
            if (_TriggeringEndpointShouldAllowAnonymous) {
              opt.AuthAttribute = typeof(AllowAnonymousAttribute);
              opt.AuthAttributeConstructorParams = new object[] { };
            }
          });      
        }
      );
     
    }
  }

}
