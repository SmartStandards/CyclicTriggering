using System.ComponentModel;
using System.Diagnostics.Contracts;
#if NET_FX
using System.ServiceModel;
using System.ServiceModel.Web;
#endif

namespace CyclicTriggering {

#if NET_FX 
  [ServiceContract()]
#endif
  public interface ICyclicTriggerReceiver {

#if NET_FX
  [OperationContract()]
  [WebInvoke(Method = "POST", UriTemplate = "go")]
#endif
    [DisplayName("go")] //UJMW will use this for the URL!
    void Go();

  }

}
