internal readonly record struct DesktopFrameSample(long Time,GuestFrame Frame,Bitmap Image);

internal static class ComposedFramePairing
{
    internal static bool Matches(DesktopFrameSample sample,long phaseStarted,long arrival,GuestFrame internalFrame)
        =>sample.Time>=phaseStarted&&Math.Abs(arrival-sample.Time)<=500&&
          GuestDecoder.Distance(internalFrame.Counter,sample.Frame.Counter)<=12&&
          internalFrame.Mask==sample.Frame.Mask;

    internal static async Task<DesktopFrameSample?> WaitForMatch(
        Func<IReadOnlyList<DesktopFrameSample>> snapshot,long phaseStarted,long arrival,
        GuestFrame internalFrame,long deadline,Func<long> now,Action checkFailures,
        Func<int,Task> pause=null)
    {
        while(true){
            var matches=snapshot().Where(sample=>Matches(sample,phaseStarted,arrival,internalFrame))
                .OrderBy(sample=>GuestDecoder.Distance(internalFrame.Counter,sample.Frame.Counter))
                .ToArray();
            if(matches.Length>0)return matches[0];
            if(now()>=deadline)return null;
            checkFailures();
            await (pause?.Invoke(20)??Task.Delay(20));
        }
    }

    internal static void VerifyContract()
    {
        long now=0;var samples=new List<DesktopFrameSample>();
        var expected=new GuestFrame(0,10,Rectangle.Empty);
        samples.Add(new(999,new GuestFrame(0,10,Rectangle.Empty),null));
        samples.Add(new(1120,new GuestFrame(1,11,Rectangle.Empty),null));
        samples.Add(new(1120,new GuestFrame(0,90,Rectangle.Empty),null));
        int waits=0;
        Task Pause(int milliseconds){now+=milliseconds;if(++waits==2)samples.Add(new(1200,new GuestFrame(0,15,Rectangle.Empty),null));return Task.CompletedTask;}
        var selected=WaitForMatch(()=>samples,1000,1100,expected,200,()=>now,()=>{},Pause).GetAwaiter().GetResult();
        if(!selected.HasValue||selected.Value.Time!=1200||waits!=2)
            throw new InvalidOperationException("Fresh matching desktop frame was not awaited; stale, masked or distant counters must remain ineligible.");
        if(WaitForMatch(()=>samples,1300,1100,expected,now,()=>now,()=>{},_=>Task.CompletedTask).GetAwaiter().GetResult().HasValue)
            throw new InvalidOperationException("Desktop frame before the current phase was accepted.");
    }
}
