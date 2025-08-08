using Bam.Logging;

namespace Bam.Application.Unit;

public class TestLoggable : Loggable
{
    public TestLoggable()
    {
    }

    public event EventHandler<TestEventArgs> TestEvent;

    public void TestFire(TestEventArgs e)
    {
        FireEvent(TestEvent, e);
    }
}