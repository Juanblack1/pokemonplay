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
    internal static void VerifyContract()
    {
        var gate=new HoldAcquisition();gate.ObserveDown();gate.Begin(4);
        if(gate.TryAcquire(true,true,4))throw new InvalidOperationException("Prior mouse-down acquired new hold");
        gate.ObserveDown();
        if(gate.TryAcquire(false,true,4)||gate.TryAcquire(true,false,4)||gate.TryAcquire(true,true,5))throw new InvalidOperationException("Incomplete acquisition accepted");
        if(!gate.TryAcquire(true,true,4))throw new InvalidOperationException("Real current acquisition rejected");
        gate.Release();gate.Begin(5);
        if(gate.TryAcquire(true,true,5))throw new InvalidOperationException("Previous hold acquired next hold");
    }
}
