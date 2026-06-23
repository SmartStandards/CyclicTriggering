using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;
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

namespace CyclicTriggering {

  public partial class AspWorkerBasedCyclicTriggeringService : CyclicTriggeringService, IAspTriggerTargetRegistrar {

    public AspWorkerBasedCyclicTriggeringService() {
    }

    private Task _DelayedHardShutdownTask = null;

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
    public void AddTriggerTarget(
      Action<CancellationToken> target, AspSpecialTrigger aspSpecialTrigger, int minWaitSeconds = 0
    ) {

      this.AddTriggerTarget(
        (ct)=> {
          target(ct);
          return null;//a little hack to get things executed synchronously...
        }, minWaitSeconds, false, null, (int)aspSpecialTrigger
      );

      if(aspSpecialTrigger == AspSpecialTrigger.OnEachRequest) {
        _AsyncRequstFilterShouldBeEnabled = true;
      }

    }

    //'classitis' -> an IHostedService can only be registered in a way that it is started
    //and managed as a completely unreachable instance, therefore we need this small proxy,
    //so that the DI instance of 'AspWorkerBasedCommandExecutor' is still available as a service -
    //otherwise you could not have it injected into a UJMW controller...
    internal class HostedServiceProxyForExternalTriggerReceiver : IHostedService {

      IServiceProvider _Services;

      public HostedServiceProxyForExternalTriggerReceiver(IServiceProvider services) {
        _Services = services;
      }

      private AspWorkerBasedCyclicTriggeringService FindConcreteInstance() {
        ICyclicTriggerReceiver executor = _Services.GetRequiredService<ICyclicTriggerReceiver>();
        return (AspWorkerBasedCyclicTriggeringService)executor;
      }

      public Task StartAsync(CancellationToken cancellationToken) {
        return this.FindConcreteInstance().StartAsync(cancellationToken);
      }

      public Task StopAsync(CancellationToken cancellationToken) {
        return this.FindConcreteInstance().StopAsync(cancellationToken);
      }

    }

    internal void HandleSpecialTrigger(AspSpecialTrigger specialTrigger) {

      if(specialTrigger == AspSpecialTrigger.OnApplicationReady) {
        _ApplicationIsReady = true;
      }

      base.Go((int)specialTrigger);
    }

    protected override string GetRetriggerUrl() {


      //gets the current asp.net core request:


      //System.Web.HttpRequest req = System.Web.HttpContext.Current?.Request;
      //if (req != null) {
      //  if (req.Url.AbsoluteUri.EndsWith("/go")) {
      //    return req.Url.AbsoluteUri;
      //  }
      //  return null; //wrong endpoint!
      //}
      fehlt!!!

    }

    internal bool _ApplicationIsReady = false;
    protected override bool IsApplicationReadyForLoopbackSelftrigger() {
      return _ApplicationIsReady;
    }

    // Wird vom Host beim Start aufgerufen
    private Task StartAsync(CancellationToken cancellationToken) {

      this.GlobalRuntimeCancellationToken = cancellationToken;

      //only the "Start"-Operation has completed ;-)
      return Task.CompletedTask;
    }

    // Wird vom Host beim Shutdown aufgerufen
    private async Task StopAsync(CancellationToken cancellationToken) {

      this.Shutdown();

      //TODO: wait???

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
    public void EnableTriggeringEndpoint(string endpointRoute = ".well-known/cyclic-trigger") {
      _TriggeringEndpointShouldBeRegisteredOn = endpointRoute;
    }

    internal string _TriggeringEndpointShouldBeRegisteredOn = null;

    /// <summary>
    /// Will register a async-filter (asp-hook) to trigger the cyclic operations on every incomming request!
    /// (sounds like a performace-overkill but isn't - there are some semaphores)
    /// </summary>
    /// 
    public void EnableTriggerOnAnyEnpoint() {
      _AsyncRequstFilterShouldBeEnabled = true;
      _GoShouldBeCalledForEachRequest = true;
    }

    internal bool _GoShouldBeCalledForEachRequest = false;

    //expose that we have the wish to be wired up via  async-filter!
    internal bool _AsyncRequstFilterShouldBeEnabled = false;

  }

}
