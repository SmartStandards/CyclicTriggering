using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CyclicTriggering {

  public interface IAmbientHttpContextProvider {

    //HttpContext GetCurrentContext();

    /// <summary> Full url without query string </summary>
    string GetCurrentRequestUrl();

    /// <summary> The application base url </summary>
    string GetCurrentBaseUrl();
  }

}
