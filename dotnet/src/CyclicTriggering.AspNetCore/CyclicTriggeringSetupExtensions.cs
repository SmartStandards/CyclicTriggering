using Microsoft.Extensions.DependencyInjection;
using System.Web.UJMW;

namespace CyclicTriggering {

  public static partial class CyclicTriggeringSetupExtensions {

    public static void AddCyclicTriggering(
      this IServiceCollection services, Action<IAspTriggerTargetRegistrar> onRegisteringTargets
    ) {

      AspWorkerBasedCyclicTriggeringService service = new AspWorkerBasedCyclicTriggeringService();
      onRegisteringTargets.Invoke(service);

      //register the executor into the asp DI framework:
      services.AddSingleton<ICyclicTriggerReceiver>(service);
      services.AddSingleton<ITriggerTargetRegistrar>(service);

      if (service._AmbientHttpInfoProviderShouldBeRegistered) {
        services.AddSingleton<IAmbientHttpContextProvider>(service);
      }

      //asp-special type of service-registration for background-workers
      services.AddHostedService<AspWorkerBasedCyclicTriggeringService.HostedServiceProxyForExternalTriggerReceiver>();

      //external trigger endpoint
      service.RegisterUjmwEndpointIfRequired(services);

      //request-interceptor to grap the current HttpContext + trigger special events
      service.RegisterAsyncRequstFilterIfRequired(services);

    }

  }

}
