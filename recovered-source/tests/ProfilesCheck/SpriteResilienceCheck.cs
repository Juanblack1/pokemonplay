using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

internal static class SpriteResilienceCheck
{
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    internal static void Run(string root, Assembly app)
    {
        Type service = app.GetType("PokemonSpriteService");
        Task<byte[]> Fetch(HttpClient client) => (Task<byte[]>)service.GetMethod("FetchSpriteBytesAsync", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { client, 25, true, true });
        byte[] png;
        using (var bitmap = new Bitmap(8, 8))
        using (var encoded = new MemoryStream()) { bitmap.Save(encoded, ImageFormat.Png); png = encoded.ToArray(); }
        using (var handler = new FallbackHandler(png))
        using (var client = new HttpClient(handler))
        {
            byte[] result = Fetch(client).GetAwaiter().GetResult();
            Assert(result != null && result.SequenceEqual(png) && handler.Requests == 2, "sprite network failure allows the next variant fallback");
        }
        using (var oversized = new TrackingStream(new byte[2 * 1024 * 1024]))
        {
            var task = (Task<byte[]>)service.GetMethod("ReadBoundedAsync", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { oversized, CancellationToken.None });
            Assert(task.GetAwaiter().GetResult() == null && oversized.Position == 1024 * 1024 + 1, "sprite reader rejects oversized streams after exactly the size limit and sentinel");
        }
        using (var handler = new UnknownLengthHandler(png))
        using (var client = new HttpClient(handler))
        {
            byte[] result = Fetch(client).GetAwaiter().GetResult();
            Assert(result != null && result.SequenceEqual(png) && handler.Requests == 2 && handler.Body.BytesRead == 1024 * 1024 + 1, "sprite HTTP body without declared length is bounded before trying its fallback");
        }
        string cache = Path.Combine(root, "oversized-sprite-cache.png"); File.WriteAllBytes(cache, new byte[2 * 1024 * 1024]);
        var cachedTask = (Task<byte[]>)service.GetMethod("ReadCachedAsync", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { cache });
        Assert(cachedTask.GetAwaiter().GetResult() == null && !File.Exists(cache), "oversized sprite cache is rejected and removed after releasing the file handle");
        using (var handler = new CancelledHandler())
        using (var client = new HttpClient(handler))
        {
            Assert(Fetch(client).GetAwaiter().GetResult() == null && handler.Cancelled && handler.Requests == 1, "sprite total deadline cancels a stalled request without starting more fallbacks");
        }
    }

    private sealed class FallbackHandler(byte[] png) : HttpMessageHandler
    {
        internal int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (++Requests == 1) throw new HttpRequestException("simulated transient network failure");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) });
        }
    }
    private sealed class CancelledHandler : HttpMessageHandler
    {
        internal bool Cancelled;
        internal int Requests;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            try { await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
            throw new Exception("stalled handler unexpectedly completed");
        }
    }
    private sealed class UnknownLengthHandler(byte[] png) : HttpMessageHandler
    {
        internal int Requests;
        internal readonly TrackingStream Body = new(new byte[2 * 1024 * 1024]);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = ++Requests == 1 ? new UnknownLengthContent(Body) : new ByteArrayContent(png) });
    }
    private sealed class UnknownLengthContent(Stream body) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) => body.CopyToAsync(stream);
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(body);
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) => Task.FromResult(body);
    }
    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        internal int BytesRead;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            BytesRead += count;
            return count;
        }
    }
}
