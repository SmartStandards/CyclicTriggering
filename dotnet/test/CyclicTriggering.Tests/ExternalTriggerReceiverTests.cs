using CyclicTriggering;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Web.UJMW;

namespace CyclicTriggering {

  [TestClass()]
  public class ExternalTriggerReceiverTests {

    [TestMethod()]
    public void Test1() {

      Dictionary<DateTime, string> timeLog = new Dictionary<DateTime, string>();

      CyclicTriggeringService triggerReceiver = new CyclicTriggeringService();

      Action target1 = () => {
        Thread.Sleep(10);
        timeLog.Add (DateTime.Now, $"Target 1 started");
        Thread.Sleep(290);
        timeLog.Add(DateTime.Now, $"Target 1 ended");
      };

      Action target2 = () => {
        Thread.Sleep(30);
        timeLog.Add(DateTime.Now, $"Target 2 started");
        Thread.Sleep(999);
        timeLog.Add(DateTime.Now, $"Target 2 ended");
      };

      Action target3 = () => {
        Thread.Sleep(60);
        timeLog.Add(DateTime.Now, $"Target 3 started");
        Thread.Sleep(999);
        timeLog.Add(DateTime.Now, $"Target 3 ended");
      };

      triggerReceiver.AddTriggerTarget((c)=> new Task(target1), minWaitSeconds: 0, rescheduleWhileExecting: false); // X X
      triggerReceiver.AddTriggerTarget((c) => new Task(target2), minWaitSeconds: 0, rescheduleWhileExecting: false); // X _
      triggerReceiver.AddTriggerTarget((c) => new Task(target3), minWaitSeconds: 0, rescheduleWhileExecting: true);  // X R

      DateTime trigger1Time = DateTime.Now;
      DateTime trigger1ShouldBeProcessed = trigger1Time.AddMilliseconds(100);
      triggerReceiver.Go();

      Thread.Sleep(500);

      DateTime trigger2Time = DateTime.Now;
      DateTime trigger2ShouldBeProcessed = trigger2Time.AddMilliseconds(100);
      triggerReceiver.Go();

      Thread.Sleep(1700);

      KeyValuePair<DateTime,string>[] logEntries = timeLog.ToArray();

      Assert.AreEqual(10, logEntries.Length);

      Assert.AreEqual($"Target 1 started", logEntries[0].Value);
      Assert.IsInRange(trigger1Time, trigger1ShouldBeProcessed, logEntries[0].Key);
      Assert.AreEqual($"Target 2 started", logEntries[1].Value);
      Assert.IsInRange(trigger1Time, trigger1ShouldBeProcessed, logEntries[1].Key);
      Assert.AreEqual($"Target 3 started", logEntries[2].Value);
      Assert.IsInRange(trigger1Time, trigger1ShouldBeProcessed, logEntries[2].Key);
      // ~ 300
      Assert.AreEqual($"Target 1 ended", logEntries[3].Value);
      Assert.IsInRange(trigger1Time.AddMilliseconds(300), trigger1ShouldBeProcessed.AddMilliseconds(300), logEntries[3].Key);
      // ~ 200  @500
      Assert.AreEqual($"Target 1 started", logEntries[4].Value);
      Assert.IsInRange(trigger2Time, trigger2ShouldBeProcessed, logEntries[4].Key);
      // ~ 300  @800
      Assert.AreEqual($"Target 1 ended", logEntries[5].Value);
      Assert.IsInRange(trigger2Time.AddMilliseconds(300), trigger2ShouldBeProcessed.AddMilliseconds(300), logEntries[5].Key);
      // ~ 200 @1000
      Assert.AreEqual($"Target 2 ended", logEntries[6].Value);
      Assert.IsInRange(trigger1Time.AddMilliseconds(1000), trigger1ShouldBeProcessed.AddMilliseconds(1000), logEntries[6].Key);
      Assert.AreEqual($"Target 3 ended", logEntries[7].Value);
      Assert.IsInRange(trigger1Time.AddMilliseconds(1000), trigger1ShouldBeProcessed.AddMilliseconds(1000), logEntries[7].Key);
      Assert.AreEqual($"Target 3 started", logEntries[8].Value); //RESCHEDULE
      Assert.IsInRange(trigger1Time.AddMilliseconds(1000), trigger1ShouldBeProcessed.AddMilliseconds(1200), logEntries[8].Key);
      // ~ 1000 @2000
      Assert.AreEqual($"Target 3 ended", logEntries[9].Value);
      Assert.IsInRange(trigger1Time.AddMilliseconds(2000), trigger1ShouldBeProcessed.AddMilliseconds(2200), logEntries[9].Key);

    }

  }

}
