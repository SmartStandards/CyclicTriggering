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
using System.Web.UJMW;
using CyclicTriggering;

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


            DevLogger.LogTrace("Trigger received - executing target 1");

          },
           AspSpecialTrigger.OnEachRequest
        );

        r.EnableTriggeringEndpoint();
        r.EnableInternalSelftrigger(20);
        r.EnableLoopbackSelftrigger(20)

        r.AddTriggerTarget()

      });

      //////////////////////////////////////////////////////////////////////////////////////////

      services.AddSwaggerGenSmartStandardsFlavored();
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

    }

  }

}
