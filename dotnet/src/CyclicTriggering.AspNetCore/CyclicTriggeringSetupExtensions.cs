using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

//using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Web.UJMW;

namespace CyclicTriggering {

  public static class CyclicTriggeringSetupExtensions {

    public static void AddCyclicTriggering(
      this IServiceCollection services, Action<IAspTriggerTargetRegistrar> onRegisteringTargets
    ) {
      services.AddCyclicTriggering(onRegisteringTargets, null);
    }

    public static void AddCyclicTriggering(
      this IServiceCollection services, Action<IAspTriggerTargetRegistrar> onRegisteringTargets,
      Action<DynamicUjmwControllerOptions> ujmwOptionsConfigurator
    ) {

      AspWorkerBasedCyclicTriggeringService service = new AspWorkerBasedCyclicTriggeringService();
      onRegisteringTargets.Invoke(service);

      //register the executor as singleton (into the asp DI framework)
      services.AddSingleton<ICyclicTriggerReceiver>((services) => {
        return service;
      });

      services.AddHostedService<AspWorkerBasedCyclicTriggeringService.HostedServiceProxyForExternalTriggerReceiver>();

      if (!string.IsNullOrWhiteSpace(service._TriggeringEndpointShouldBeRegisteredOn)) {
          //register the executor itself as ujmw-endpoint...
          services.AddDynamicUjmwControllers(
          (DynamicUjmwControllerRegistrar ujmw) => {

            if (ujmwOptionsConfigurator == null) {
              ujmw.AddControllerFor<ICyclicTriggerReceiver>((opt) => {
                opt.ControllerRoute = service._TriggeringEndpointShouldBeRegisteredOn;
              });
            }
            else {
              ujmw.AddControllerFor<ICyclicTriggerReceiver>((opt) => {
                opt.ControllerRoute = service._TriggeringEndpointShouldBeRegisteredOn;
                ujmwOptionsConfigurator.Invoke(opt); //customizing, after setting the default
              });
            }
          }
        );
      }

      if (service._AsyncRequstFilterShouldBeEnabled) {
        //ASP-KLASSISTIS :-(
        StartupFilterForDeferredInit.AddTo(services);
      }

    }

    private sealed class StartupFilterForDeferredInit : IStartupFilter {

      public static void AddTo(IServiceCollection services) {
        services.AddTransient<IStartupFilter>(
         (p) => new StartupFilterForDeferredInit()
        );
      }

      private StartupFilterForDeferredInit() {
      }

      Action<IApplicationBuilder> IStartupFilter.Configure(Action<IApplicationBuilder> next) {
        return (app) => {

          AspWorkerBasedCyclicTriggeringService cts = (AspWorkerBasedCyclicTriggeringService)
            app.ApplicationServices.GetRequiredService<ICyclicTriggerReceiver>();

          IHostApplicationLifetime lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
          lifetime.ApplicationStarted.Register(()=> cts.HandleSpecialTrigger(AspSpecialTrigger.OnApplicationReady));
          lifetime.ApplicationStopping.Register(() => cts.HandleSpecialTrigger(AspSpecialTrigger.OnApplicationStopping));

          bool goForEachRequest = cts._GoShouldBeCalledForEachRequest;
          if (cts._AsyncRequstFilterShouldBeEnabled) {
            app.Use(async (context, next) => {
              try {

                cts.HandleSpecialTrigger(AspSpecialTrigger.OnEachRequest);

                if (goForEachRequest) {
                  //must be synchonously (new threads will be created inside of this!)
                  cts.Go();
                }

              }
              catch {
              }
              await next();
            });
          }
          next(app);
        };
      }
    }

  }
}
