using CyclicTriggering;
using Logging.SmartStandards;
using Logging.SmartStandards.AspSupport;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.UJMW;

namespace Demo {

  public static partial class Program {

    static partial void OnConfigureServices(IServiceCollection services, IConfiguration config) {

      services.AddSmartStandardsLogging(config);

      DevLogger.LogCritical("neueeee");
      DevLogger.LogTrace("t1");
      DevLogger.LogTrace("t2");

      string outDir = AppDomain.CurrentDomain.BaseDirectory;

      services.AddControllers();

      UjmwHostConfiguration.EnableApiGroupNameFallback = true;

      UjmwHostConfiguration.AuthHeaderEvaluator = (
        (string rawAuthHeader, Type contractType, MethodInfo targetContractMethod, string callingMachine, ref int httpReturnCode, ref string failedReason) => {
         
          if(contractType == typeof(ICyclicTriggerReceiver)) {
            return true;
          }

          //in this demo - any auth header is ok - but there must be one ;-)
          if (string.IsNullOrWhiteSpace(rawAuthHeader)) {
            httpReturnCode = 403;
            failedReason = "This demo requires at least ANY string as authheader!";
            return false;
          }
          return true;
        }
      );

      //////////////////////////////////////////////////////////////////////////////////////////
      // CYCLIC-TRIGGERING:
      //////////////////////////////////////////////////////////////////////////////////////////

      services.AddCyclicTriggering((r) => {

        r.AddTriggerTarget(
           (CancellationToken c) => {
             int tid = Thread.CurrentThread.ManagedThreadId;
             DevLogger.LogDebug($"  [#{tid}]  TRIGGERRED 'Foo-Job' by Regular GO (async #{tid})");
             DevLogger.LogDebug($"  [#{tid}]  BU: {AspWorkerBasedCyclicTriggeringService.GetCurrentRequestUrl()}");
             Thread.Sleep(3000); //<< takes longer than the minWaitSeconds of 2 seconds ;-)
             DevLogger.LogDebug($"  [#{tid}]  'Foo-Job' ENDING...");
           },
           minWaitSeconds: 2,
           rescheduleWhileExecuting: false
        );

        //  r.AddTriggerTarget(
        //   (CancellationToken c) => {
        //     int tid = Thread.CurrentThread.ManagedThreadId;
        //     DevLogger.LogDebug($"  [#{tid}]  TRIGGERRED 'Bar-Job' by Regular GO (async #{tid})");
        //     Thread.Sleep(6000); //<< takes longer than the minWaitSeconds of 2 seconds ;-)
        //     DevLogger.LogDebug($"  [#{tid}]  'Bar-Job' ENDING...");
        //   },
        //   minWaitSeconds: 4,
        //   rescheduleWhileExecuting: true
        //);

        //register UJMW-Endpoint (POST on .well-known/cyclic-trigger/go)
        r.EnableTriggeringEndpoint();

        //use any incomming request (to any endpoint) as trigger (no UJMW-Endpoint required)
        //r.EnableTriggerOnAnyEnpoint();

        //self-keep-alive: call the UJMW-Endpoint (start now)
        //r.EnableLoopbackSelftrigger(2,  //only possible if we know our public url...
        //  startSelfInitiatedOverThisUrl: "http://localhost:55202/.well-known/cyclic-trigger/go"
        //);

        //self-keep-alive: call the UJMW-Endpoint (starts after at least one external call)
        //r.EnableLoopbackSelftrigger(2);

        //self-trigger without keep-alive (no http-layer - just a background-thread)
        //r.EnableInternalSelftrigger(2);

        r.EnableAmbientHttpInfoProvider();

        #region " playing with 'SpecialTrigger' events (only available for ASP) "

        //r.AddTriggerTargetForAspEvent(
        //  (CancellationToken c) => {
        //    int tid = Thread.CurrentThread.ManagedThreadId;
        //    DevLogger.LogDebug($"  [#{tid}]  TRIGGERRED by OnApplicationReady (synchronously)");
        //    DevLogger.LogDebug($"  [#{tid}]  BU: {AspWorkerBasedCyclicTriggeringService.GetCurrentRequestUrl()}");
        //  },
        //  AspSpecialTrigger.OnApplicationReady
        //);

        //r.AddTriggerTargetForAspEvent(
        //  (CancellationToken c) => {
        //    int tid = Thread.CurrentThread.ManagedThreadId;
        //    DevLogger.LogDebug($"  [#{tid}]  TRIGGERRED by OnEachRequest (synchronously)");
        //    DevLogger.LogDebug($"  [#{tid}]  BU: {AspWorkerBasedCyclicTriggeringService.GetCurrentRequestUrl()}");
        //  },
        //  AspSpecialTrigger.OnEachRequest
        //);

        //r.AddTriggerTargetForAspEvent(
        //  (CancellationToken c) => {
        //    int tid = Thread.CurrentThread.ManagedThreadId;
        //    DevLogger.LogDebug($"  [#{tid}]  TRIGGERRED by OnApplicationStopping (synchronously)");
        //    DevLogger.LogDebug($"  [#{tid}]  BU: {AspWorkerBasedCyclicTriggeringService.GetCurrentRequestUrl()}");
        //  },
        //  AspSpecialTrigger.OnApplicationStopping
        //);

        #endregion

      });

      //////////////////////////////////////////////////////////////////////////////////////////

      services.AddSwaggerGenSmartStandardsFlavored();

      int tid = Thread.CurrentThread.ManagedThreadId;
      DevLogger.LogDebug($"  [#{tid}]  OnConfigureServices COMPLETED!");
    }

    static partial void OnRunApplication(
      WebApplication app, IConfiguration config, IServiceProvider services,
      IWebHostEnvironment environment, IHostApplicationLifetime lifetime
    ) {

      //required for the www-root
      app.UseStaticFiles();

      app.UseAmbientFieldAdapterMiddleware();

      if (!config.GetValue<bool>("ProdMode")) {
        app.UseDeveloperExceptionPage();
      }

      string baseUrl = config.GetValue<string>("BaseUrl");

      app.UseHttpsRedirection();

      app.UseRouting();

      //CORS: muss zwischen 'UseRouting' und 'UseEndpoints' liegen!
      app.UseCors((p) => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

      app.UseAuthentication(); //<< WINDOWS-AUTH
      app.UseAuthorization();

      app.UseEndpoints((endpoints) => {
        endpoints.MapControllers();
      });

      int tid = Thread.CurrentThread.ManagedThreadId;
      DevLogger.LogDebug($"  [#{tid}]  OnRunApplication COMPLETED!");
    }

  }

}
