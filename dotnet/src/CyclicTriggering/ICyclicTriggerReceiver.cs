using System.ComponentModel;
using System.Diagnostics.Contracts;
#if NET_FX
using System.ServiceModel;
#endif

namespace CyclicTriggering {

#if NET_FX 
  [ServiceContract()]
#endif
  public interface ICyclicTriggerReceiver {

#if NET_FX
  [OperationContract(Action="go", Name="go")]
#endif
    [DisplayName("go")] //UJMW will use this for the URL!
    void Go();

  }

}
