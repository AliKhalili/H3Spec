using System.Buffers;
using System.IO.Pipelines;
using System.Net.Quic;
using H3Spec.DotNet;

namespace H3Spec.Core.Http;

internal sealed class QuicStreamContext
{
    private const int MinAllocBufferSize = 4096;

    private readonly QuicStream _stream;
    private CancellationTokenSource? _streamClosedTokenSource;

    public MemoryPool<byte> MemoryPool { get; } = default!;
    public IDuplexPipe Transport { get; private set; }
    public IDuplexPipe Application { get; private set; }
    public QuicStreamType StreamType { get; private set; }
    public long StreamId { get; private set; }
    public bool CanRead { get; private set; }
    public bool CanWrite { get; private set; }
    public Exception Exception { get; private set; }

    public QuicStreamContext(QuicStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _stream = stream;
        StreamType = stream.Type;
        StreamId = stream.Id;

        _streamClosedTokenSource = null;

        var maxReadBufferSize = MinAllocBufferSize * 4;
        var maxWriteBufferSize = MinAllocBufferSize * 4;
        var inputOptions = new PipeOptions(MemoryPool, PipeScheduler.ThreadPool, PipeScheduler.Inline, maxReadBufferSize, maxReadBufferSize / 2, useSynchronizationContext: false);
        var outputOptions = new PipeOptions(MemoryPool, PipeScheduler.Inline, PipeScheduler.ThreadPool, maxWriteBufferSize, maxWriteBufferSize / 2, useSynchronizationContext: false);

        var inputPipe = new Pipe(inputOptions);
        var outputPipe = new Pipe(outputOptions);

        Transport = new DuplexPipe(inputPipe.Reader, outputPipe.Writer);
        Application = new DuplexPipe(outputPipe.Reader, inputPipe.Writer);

        CanRead = stream.CanRead;
        CanWrite = stream.CanWrite;
    }

    public CancellationToken ConnectionClosed
    {
        get
        {
            // Allocate CTS only if requested.
            if (_streamClosedTokenSource == null)
            {
                _streamClosedTokenSource = new CancellationTokenSource();
            }
            return _streamClosedTokenSource.Token;
        }
        set => throw new NotSupportedException();
    }

    public async void Start()
    {
        try
        {
            var receiveTask = Task.CompletedTask;
            var sendTask = Task.CompletedTask;
            if (_stream.CanRead)
            {
                receiveTask = DoReceiveAsync();
            }
            if (_stream.CanWrite)
            {
                sendTask = DoSendAsync();
            }
            await sendTask;
            await receiveTask;
            FireStreamClosed();
        }
        catch (Exception ex)
        {
            Exception = ex;
        }

    }

    private async Task DoReceiveAsync()
    {
        try
        {
            var input = Application.Output;
            while (true)
            {
                var buffer = input.GetMemory(MinAllocBufferSize);
                var bytesReceived = await _stream.ReadAsync(buffer);

                if (bytesReceived == 0)
                {
                    // Read completed.
                    break;
                }

                input.Advance(bytesReceived);

                if (_stream.ReadsClosed.IsCompletedSuccessfully)
                {
                    await input.CompleteAsync();
                    break;
                }
                else
                {
                    await input.FlushAsync();
                }
            }
        }
        catch (QuicException ex)
        {
            Exception = ex;
        }
        catch (Exception ex)
        {
            Exception = ex;
        }
        finally
        {
            Application.Output.Complete();
        }
    }

    private async Task DoSendAsync()
    {
        try
        {
            // Resolve `output` PipeReader via the IDuplexPipe interface prior to loop start for performance.
            var output = Application.Input;
            while (true)
            {
                var result = await output.ReadAsync();

                if (result.IsCanceled)
                {
                    break;
                }

                var buffer = result.Buffer;

                var end = buffer.End;
                var isCompleted = result.IsCompleted;
                if (!buffer.IsEmpty)
                {
                    if (buffer.IsSingleSegment)
                    {
                        // Fast path when the buffer is a single segment.
                        await _stream.WriteAsync(buffer.First, completeWrites: isCompleted);
                    }
                    else
                    {
                        // When then buffer has multiple segments then write them in a loop.
                        // We're not using a standard foreach here because we want to detect
                        // the final write and pass end stream flag with that write.
                        var enumerator = buffer.GetEnumerator();
                        var isLastSegment = !enumerator.MoveNext();

                        while (!isLastSegment)
                        {
                            var currentSegment = enumerator.Current;
                            isLastSegment = !enumerator.MoveNext();
                            await _stream.WriteAsync(currentSegment, completeWrites: isLastSegment && isCompleted);
                        }
                    }
                }

                output.AdvanceTo(end);

                if (isCompleted)
                {
                    // Once the stream pipe is closed, shutdown the stream.
                    break;
                }
            }
        }
        catch (QuicException ex)
        {
            Exception = ex;
        }
        catch (Exception ex)
        {
            Exception = ex;
        }
        finally
        {
            Application.Input.Complete();
        }
    }

    private void FireStreamClosed()
    {
        if (_streamClosedTokenSource != null)
        {
            _streamClosedTokenSource.Cancel();
        }
    }
}
