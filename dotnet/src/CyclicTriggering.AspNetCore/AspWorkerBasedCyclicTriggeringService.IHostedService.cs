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

    // Wird vom Host beim Start aufgerufen
    private Task StartAsync(CancellationToken cancellationToken) {
      this.GlobalRuntimeCancellationToken = cancellationToken;
      //only the "Start"-Operation has completed ;-)
      return Task.CompletedTask;
    }

    // Wird vom Host beim Shutdown aufgerufen
    private async Task StopAsync(CancellationToken cancellationToken) {
      this.Shutdown();
      //TODO: wait for all tasks to complete, or cancel them after a timeout
      await Task.CompletedTask;
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

  }

}
