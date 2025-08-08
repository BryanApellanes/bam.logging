namespace Bam.Application.Unit;

public class TestEventArgs : EventArgs
{
    public TestEventArgs()
    {
    }
    
    public string Value { get; set; }
}