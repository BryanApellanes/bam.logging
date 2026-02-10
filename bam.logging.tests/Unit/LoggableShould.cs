using Bam.Logging;
using Bam.Test;

namespace Bam.Application.Unit;


[UnitTestMenu("Loggable Should", Selector = "ls")]
public class LoggableShould : UnitTestMenuContainer
{
    [UnitTest]
    public void ShouldFireGenericEventHandler()
    {
        string expected = 16.RandomLetters();
        bool? called = false; 
        string received = null;
        When.A<Loggable>("Uses Fire() to fire a generic event", () => new TestLoggable(), (testLoggable) =>
        {
            TestLoggable loggable = (TestLoggable)testLoggable;
            loggable.TestEvent += (o, args) =>
            {
                called = true;
                received = args.Value;
            };
            loggable.TestFire(new TestEventArgs(){Value = expected});
            return new object[] { testLoggable is TestLoggable, received };
        })
        .It
        .ShouldPass(because =>
        {
            object[] r = (object[])because.Result;
            because.ItsTrue("object under test is TestLoggable", (bool)r[0]);
            because.ItsTrue("The event was called", called.Value);
            because.ItsTrue($"Received value was {expected} as expected", expected.Equals(r[1]));
        })
        .SoBeHappy()
        .UnlessItFailed();
    }
    
}