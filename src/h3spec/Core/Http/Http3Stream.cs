using System.Buffers;
using System.IO.Pipelines;
using System.Net.Http.QPack;
using System.Text;
using H3Spec.DotNet;
using H3Spec.DotNet.Frames;
using H3Spec.DotNet.Http;

namespace H3Spec.Core.Http;

internal class Http3Stream : IThreadPoolWorkItem, IHttpStreamHeadersHandler
{
    private readonly QuicStreamContext _context;
    private volatile int _isClosed;
    private readonly Http3RawFrame _incomingFrame = new();
    private readonly Http3FrameWriter _framewriter;

    private List<byte[]> _responseData = new();
    private Dictionary<string, string> _responseHeaders = new();
    private Dictionary<long, long> _serverSettings = new();

    public long StreamId { get; private set; }
    public PipeReader Input => _context.Transport.Input;
    public PipeWriter Output => _context.Transport.Output;
    public QPackDecoder QPackDecoder { get; private set; } = default!;
    public Exception Exception => _context.Exception;
    public IReadOnlyList<byte[]> ResponseData => _responseData;
    public IReadOnlyDictionary<string, string> Headers => _responseHeaders;
    public IReadOnlyDictionary<long, long> ServerSettings => _serverSettings;

    public Http3Stream(QuicStreamContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _framewriter = new Http3FrameWriter();
        _framewriter.Reset(_context.Transport.Output);
        StreamId = context.StreamId;
        QPackDecoder = new QPackDecoder(16384); // 2^14
    }

    #region Process Read

    public void Execute() => _ = ProcessRequestAsync();

    public async Task ProcessRequestAsync()
    {
        if (!_context.CanRead)
        {
            return;
        }
        // Console.WriteLine($"##### Processing the {_context.StreamType} stream {_context.StreamId}. #####");
        if (_context.StreamType == System.Net.Quic.QuicStreamType.Unidirectional)
        {
            var streamType = await TryReadStreamHeaderAsync();
            // Console.WriteLine($"Start processing stream with type {(Http3StreamType)streamType}");
            await HandleControlStreamAsync();
        }

        if (_context.StreamType == System.Net.Quic.QuicStreamType.Bidirectional)
        {
            await HandleRequestStreamAsync();
        }
    }

    private async ValueTask<long> TryReadStreamHeaderAsync()
    {
        while (_isClosed == 0)
        {
            var result = await Input.ReadAsync();
            var readableBuffer = result.Buffer;
            var consumed = readableBuffer.Start;
            var examined = readableBuffer.End;

            try
            {
                if (!readableBuffer.IsEmpty)
                {
                    var id = VariableLengthIntegerHelper.GetInteger(readableBuffer, out consumed, out examined);
                    if (id != -1)
                    {
                        return id;
                    }
                }

                if (result.IsCompleted)
                {
                    return -1;
                }
            }
            finally
            {
                Input.AdvanceTo(consumed, examined);
            }
        }

        return -1;
    }

    private async Task HandleControlStreamAsync()
    {
        while (_isClosed == 0)
        {
            var result = await Input.ReadAsync();
            var readableBuffer = result.Buffer;
            var consumed = readableBuffer.Start;
            var examined = readableBuffer.End;

            try
            {
                if (!readableBuffer.IsEmpty)
                {
                    while (Http3FrameReader.TryReadFrame(ref readableBuffer, _incomingFrame, out var framePayload))
                    {
                        consumed = examined = framePayload.End;
                        await ProcessHttp3ControlStream(framePayload);
                    }
                }

                if (result.IsCompleted)
                {
                    return;
                }
            }
            finally
            {
                Input.AdvanceTo(consumed, examined);
            }
        }
    }

    private async Task HandleRequestStreamAsync()
    {
        while (_isClosed == 0)
        {
            var result = await Input.ReadAsync();
            var readableBuffer = result.Buffer;
            var consumed = readableBuffer.Start;
            var examined = readableBuffer.End;

            try
            {
                if (!readableBuffer.IsEmpty)
                {
                    while (Http3FrameReader.TryReadFrame(ref readableBuffer, _incomingFrame, out var framePayload))
                    {
                        consumed = examined = framePayload.End;
                        await ProcessHttp3RequestStream(framePayload);
                    }
                }

                if (result.IsCompleted)
                {
                    return;
                }
            }
            finally
            {
                Input.AdvanceTo(consumed, examined);
            }
        }
    }

    private ValueTask ProcessHttp3ControlStream(in ReadOnlySequence<byte> payload)
    {
        switch (_incomingFrame.Type)
        {
            case Http3FrameType.Data:
            case Http3FrameType.Headers:
            case Http3FrameType.PushPromise:
                break;
            case Http3FrameType.Settings:
                return ProcessSettingsFrameAsync(payload);
            case Http3FrameType.GoAway:
                break;
            //case Http3FrameType.CancelPush:
            //    return ProcessCancelPushFrameAsync();
            //case Http3FrameType.MaxPushId:
            //    return ProcessMaxPushIdFrameAsync();
            //default:
            //    return ProcessUnknownFrameAsync(_incomingFrame.Type);
            default:
                throw new NotImplementedException();
        }

        return default;
    }

    private ValueTask ProcessHttp3RequestStream(in ReadOnlySequence<byte> payload)
    {
        switch (_incomingFrame.Type)
        {
            case Http3FrameType.Headers:
                return ProcessHeaderFrameAsync(payload);
            case Http3FrameType.Data:
                return ProcessDataFrameAsync(payload);
            case Http3FrameType.PushPromise:
                break;
            case Http3FrameType.Settings:
                break;
            default:
                throw new NotImplementedException();
        }

        return default;
    }

    private ValueTask ProcessHeaderFrameAsync(ReadOnlySequence<byte> payload)
    {
        try
        {
            QPackDecoder.Decode(payload, endHeaders: true, handler: this);
            QPackDecoder.Reset();
        }
        catch (QPackDecodingException ex)
        {
            throw new Exception(ex.Message);
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask ProcessDataFrameAsync(ReadOnlySequence<byte> payload)
    {
        _responseData.Add(payload.ToArray());
        return ValueTask.CompletedTask;
    }

    private ValueTask ProcessSettingsFrameAsync(ReadOnlySequence<byte> payload)
    {
        while (true)
        {
            var id = VariableLengthIntegerHelper.GetInteger(payload, out var consumed, out _);
            if (id == -1)
            {
                break;
            }

            payload = payload.Slice(consumed);

            var value = VariableLengthIntegerHelper.GetInteger(payload, out consumed, out _);
            if (value == -1)
            {
                break;
            }

            payload = payload.Slice(consumed);
            ProcessSetting(id, value);
        }

        return default;
    }

    private void ProcessSetting(long id, long value) => _serverSettings[id] = value;

    #endregion


    #region Process Write

    public async Task WriteStreamTypeId(long type) => await _framewriter.WriteStreamTypeIdAsync(type);// Console.WriteLine($"Writing Stream Type Id");
    public async Task WriteSettingsFrameAsync(List<Http3PeerSetting> settings) => await _framewriter.WriteSettingsAsync(settings);// Console.WriteLine($"Writing Settings");

    public async Task WriteGoAway(long id) => await _framewriter.WriteGoAwayAsync(id);// Console.WriteLine($"Writing GoAway");

    public async Task WriteRequestHeader(IDictionary<string, string> headers) => await _framewriter.WriteRequestHeaders(headers);// Console.WriteLine($"Writing Request Headers");

    public async Task WriteEndStream() => await _framewriter.WriteEndStream();// Console.WriteLine($"Writing End Stream");

    public async Task WriteData(byte[] data) => await _framewriter.WriteDataAsync(new ReadOnlySequence<byte>(data));

    #endregion


    #region IHttpStreamHeadersHandler

    public void OnStaticIndexedHeader(int index)
    {
        ref readonly var entry = ref H3StaticTable.Get(index);
        OnHeaderCore("Static Index", index, entry.Name, entry.Value);
    }

    public void OnStaticIndexedHeader(int index, ReadOnlySpan<byte> value)
    {
        var name = H3StaticTable.Get(index).Name;
        OnHeaderCore("Static Index", index, name, value);
    }

    public void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value) => OnHeaderCore("Normal", null, name, value);

    public void OnHeadersComplete(bool endStream)
    {
        // Console.WriteLine("On Headers Complete.");
    }

    public void OnDynamicIndexedHeader(int? index, ReadOnlySpan<byte> name, ReadOnlySpan<byte> value) => OnHeaderCore("DynamicIndex", index, name, value);

    private void OnHeaderCore(string type, int? index, ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
    {
        var encoding = Encoding.UTF8;
        _responseHeaders.Add(encoding.GetString(name), encoding.GetString(value));
    }

    #endregion
}
