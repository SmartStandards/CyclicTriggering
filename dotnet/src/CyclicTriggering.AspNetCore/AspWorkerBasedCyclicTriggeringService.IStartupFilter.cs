using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

  public partial class AspWorkerBasedCyclicTriggeringService : IAmbientHttpContextProvider {

    /// <summary>
    /// can be rigistered during the "ConfigureServices" phase,
    /// will be called during the "Configure" phase and
    /// 1: registers some application-lifetime handlers (for start and shutdown) +
    /// 2: registers a middleware-hook to be invoked 'on each request' 
    /// (grab the httpcontext and make it ambient + executue special triggers).
    ///   
    /// </summary>
    internal sealed class StartupFilterForDeferredInit : IStartupFilter {

      private static void OnEachRequest(HttpContext context, AspWorkerBasedCyclicTriggeringService service) {
        //_CurrentHttpContext.Value = context;

        HttpRequest currentRequest = context.Request;
        if (context.Request != null) {
          _CurrentHttpRequestUrl.Value = $"{currentRequest.Scheme}://{currentRequest.Host}{currentRequest.PathBase}{currentRequest.Path}";
          _CurrentHttpBaseUrl.Value = $"{currentRequest.Scheme}://{currentRequest.Host}{currentRequest.PathBase}";
        }
        else {
          _CurrentHttpRequestUrl.Value = null;
          _CurrentHttpBaseUrl.Value = null;
        }

        try {

          if (service._SpecialEventShouldBeCalledForEachRequest) {
            service.HandleSpecialTrigger(AspSpecialTrigger.OnEachRequest);
          }

          if (service._GoShouldBeCalledForEachRequest) {
            //must be synchonously (new threads will be created inside of this!)
            service.Go();
          }

        }
        catch {
        }
      }

      private static void OnAfterEachRequest(AspWorkerBasedCyclicTriggeringService service) {
        //_CurrentHttpContext.Value = null;
        _CurrentHttpRequestUrl.Value = null;
        _CurrentHttpBaseUrl.Value = null;
      }

      Action<IApplicationBuilder> IStartupFilter.Configure(Action<IApplicationBuilder> next) {
        return (app) => {

          AspWorkerBasedCyclicTriggeringService service = (AspWorkerBasedCyclicTriggeringService)app.ApplicationServices.GetRequiredService<ICyclicTriggerReceiver>();

          IHostApplicationLifetime lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();

          lifetime.ApplicationStarted.Register(() => service.HandleSpecialTrigger(AspSpecialTrigger.OnApplicationReady));
          lifetime.ApplicationStopping.Register(() => service.HandleSpecialTrigger(AspSpecialTrigger.OnApplicationStopping));

          if (
            service._GoShouldBeCalledForEachRequest ||
            service._SpecialEventShouldBeCalledForEachRequest ||
            service._LoopbackSelftriggerEnabled ||              //(needs grabbed httpcontext)
            service._AmbientHttpInfoProviderShouldBeRegistered  //(needs grabbed httpcontext)
          ) {
            app.Use(async (context, next) => {
              OnEachRequest(context, service);
              await next();
              OnAfterEachRequest(service);
            });
          }
          next(app);
        };
      }

    }

    #region " IAmbientHttpContextProvider "

    private static AsyncLocal<string> _CurrentHttpRequestUrl = new AsyncLocal<string>();
    private static AsyncLocal<string> _CurrentHttpBaseUrl = new AsyncLocal<string>();

    string IAmbientHttpContextProvider.GetCurrentRequestUrl() {
      return GetCurrentRequestUrl();
    }
    string IAmbientHttpContextProvider.GetCurrentBaseUrl() {
      return GetCurrentBaseUrl();
    }

    public static string GetCurrentRequestUrl() {
      return _CurrentHttpRequestUrl.Value;
    }
    public static string GetCurrentBaseUrl() {
      return _CurrentHttpBaseUrl.Value;
    }

    internal void RegisterAsyncRequstFilterIfRequired(IServiceCollection services) {
      //ASP-KLASSISTIS :-(
      services.AddTransient<IStartupFilter>(
        (p) => new StartupFilterForDeferredInit()
      );
    }

    #endregion

  }
}
