internal sealed class HoldAcquisition
{
    internal int Intent { get; private set; } = -1;
    internal bool Acquired { get; private set; }
    long downSequence, baseline;
    internal void Begin(int action) { Intent=action; Acquired=false; baseline=downSequence; }
    internal void ObserveDown() => downSequence++;
    internal bool TryAcquire(bool capture,bool raw,int mouseAction)
        => Acquired = Intent>=0 && downSequence>baseline && capture && raw && mouseAction==Intent;
    internal void Release() { Intent=-1; Acquired=false; }
    internal bool SuppressA(bool capture,bool[] raw,int mouseAction,bool[] supplied)
    {
        if(Intent!=4||!TryAcquire(capture,raw[4],mouseAction)||!supplied[4])return false;
        supplied[4]=false;return true;
    }
    internal static void VerifyContract()
    {
        var gate=new HoldAcquisition();gate.ObserveDown();gate.Begin(4);
        if(gate.TryAcquire(true,true,4))throw new InvalidOperationException("Prior mouse-down acquired new hold");
        gate.ObserveDown();
        if(gate.TryAcquire(false,true,4)||gate.TryAcquire(true,false,4)||gate.TryAcquire(true,true,5))throw new InvalidOperationException("Incomplete acquisition accepted");
        if(!gate.TryAcquire(true,true,4))throw new InvalidOperationException("Real current acquisition rejected");
        gate.Release();gate.Begin(5);
        if(gate.TryAcquire(true,true,5))throw new InvalidOperationException("Previous hold acquired next hold");
        // Real order: MouseDown event, Changed/VirtualChanged callback, then the
        // awaiting Hold continuation. Suppression must precede a bridge poll.
        gate.Release();gate.Begin(4);var raw=new bool[12];var supplied=new bool[12];
        raw[4]=supplied[4]=true;
        if(gate.SuppressA(true,raw,4,supplied)||!supplied[4])throw new InvalidOperationException("Unobserved down suppressed input");
        gate.ObserveDown();
        if(!gate.SuppressA(true,raw,4,supplied)||supplied[4]||!raw[4])throw new InvalidOperationException("Changed callback did not suppress before poll or changed raw evidence");
        if(!gate.TryAcquire(true,raw[4],4))throw new InvalidOperationException("Hold continuation lost real acquisition");
    }
}
