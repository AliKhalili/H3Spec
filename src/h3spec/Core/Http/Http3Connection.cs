using System.Net.Quic;

namespace H3Spec.Core.Http;

internal class Http3Connection(QuicConnection quicConnection) : IAsyncDisposable
{
    public QuicConnection QuicConnection => quicConnection;
    private readonly List<Http3Stream> _streams = new();

    public async Task<Http3Stream> OpenStreamAsync(QuicStreamType streamType)
    {
        var quicStream = await quicConnection.OpenOutboundStreamAsync(streamType);
        var streamContext = new QuicStreamContext(quicStream);
        var stream = new Http3Stream(streamContext);
        streamContext.Start();
        _streams.Add(stream);
        return stream;
    }

    public async Task<Http3Stream> AcceptStreamAsync()
    {
        var quicStream = await quicConnection.AcceptInboundStreamAsync();
        var streamContext = new QuicStreamContext(quicStream);
        var stream = new Http3Stream(streamContext);
        streamContext.Start();
        _streams.Add(stream);
        return stream;
    }

    public void Close()
    {
        if (quicConnection != null)
        {
            quicConnection.CloseAsync((long)Http3ErrorCode.NoError).GetAwaiter().GetResult();
        }
    }
}
