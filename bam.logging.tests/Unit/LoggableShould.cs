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
            testLoggable.ShouldBeOfType<TestLoggable>();
            TestLoggable loggable = (TestLoggable)testLoggable;
            loggable.TestEvent += (o, args) =>
            {
                called = true;
                args.Value.ShouldBe(expected);
                received = args.Value;
            };
            loggable.TestFire(new TestEventArgs(){Value = expected});
            return loggable;
        })
        .It
        .ShouldPass(because =>
        {
            TestLoggable loggable = because.ObjectUnderTest<TestLoggable>();
            because.TheObjectUnderTest.IsObjectOfType<TestLoggable>();
            because.ItsTrue("The event was called", called.Value);
            because.ItsTrue($"Received value was {expected} as expected", received.Equals(expected));
        })
        .SoBeHappy()
        .UnlessItFailed();
    }
    
}