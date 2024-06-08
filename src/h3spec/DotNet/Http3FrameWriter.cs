using H3Spec.DotNet.Frames;
using H3Spec.DotNet.Http3;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Http.Headers;

namespace H3Spec.DotNet
{
    internal class Http3FrameWriter
    {
        // Size based on HTTP/2 default frame size
        private const int MaxDataFrameSize = 16 * 1024;
        private const int HeaderBufferSize = 16 * 1024;

        private PipeWriter _outputWriter = default!;
        private readonly Http3RawFrame _outgoingFrame;
        private readonly ArrayBufferWriter<byte> _headerEncodingBuffer;

        public Http3FrameWriter()
        {
            _outgoingFrame = new Http3RawFrame();
            _headerEncodingBuffer = new ArrayBufferWriter<byte>(HeaderBufferSize);
        }

        public void Reset(PipeWriter output) => _outputWriter = output;
        internal static int WriteHeader(Http3FrameType frameType, long frameLength, PipeWriter output)
        {
            // max size of the header is 16, most likely it will be smaller.
            var buffer = output.GetSpan(16);

            var typeLength = VariableLengthIntegerHelper.WriteInteger(buffer, (int)frameType);

            buffer = buffer.Slice(typeLength);

            var lengthLength = VariableLengthIntegerHelper.WriteInteger(buffer, (int)frameLength);

            var totalLength = typeLength + lengthLength;
            output.Advance(typeLength + lengthLength);

            return totalLength;
        }

        internal Task WriteSettingsAsync(List<Http3PeerSetting> settings)
        {
            _outgoingFrame.PrepareSettings();

            // Calculate how long settings are before allocating.

            var settingsLength = CalculateSettingsSize(settings);

            // Call GetSpan with enough room for
            // - One encoded length int for setting size
            // - 1 byte for setting type
            // - settings length
            var buffer = _outputWriter.GetSpan(settingsLength + VariableLengthIntegerHelper.MaximumEncodedLength + 1);

            // Length start at 1 for type
            var totalLength = 1;

            // Write setting type
            buffer[0] = (byte)_outgoingFrame.Type;
            buffer = buffer[1..];

            // Write settings length
            var settingsBytesWritten = VariableLengthIntegerHelper.WriteInteger(buffer, settingsLength);
            buffer = buffer.Slice(settingsBytesWritten);

            totalLength += settingsBytesWritten + settingsLength;

            WriteSettings(settings, buffer);

            // Advance pipe writer and flush
            _outgoingFrame.Length = totalLength;
            _outputWriter.Advance(totalLength);

            return _outputWriter.FlushAsync().AsTask();
        }

        internal Task WriteGoAwayAsync(long id)
        {
            _outgoingFrame.PrepareGoAway();

            var length = VariableLengthIntegerHelper.GetByteCount(id);

            _outgoingFrame.Length = length;

            Http3FrameWriter.WriteHeader(_outgoingFrame.Type, _outgoingFrame.Length, _outputWriter);

            var buffer = _outputWriter.GetSpan(8);
            VariableLengthIntegerHelper.WriteInteger(buffer, id);
            _outputWriter.Advance(length);
            return _outputWriter.FlushAsync().AsTask();
        }
        internal static int CalculateSettingsSize(List<Http3PeerSetting> settings)
        {
            var length = 0;
            foreach (var setting in settings)
            {
                length += VariableLengthIntegerHelper.GetByteCount((long)setting.Parameter);
                length += VariableLengthIntegerHelper.GetByteCount(setting.Value);
            }
            return length;
        }

        internal static void WriteSettings(List<Http3PeerSetting> settings, Span<byte> destination)
        {
            foreach (var setting in settings)
            {
                var parameterLength = VariableLengthIntegerHelper.WriteInteger(destination, (long)setting.Parameter);
                destination = destination.Slice(parameterLength);

                var valueLength = VariableLengthIntegerHelper.WriteInteger(destination, (long)setting.Value);
                destination = destination.Slice(valueLength);
            }
        }
        internal Task WriteStreamTypeIdAsync(long id)
        {
            var buffer = _outputWriter.GetSpan(8);
            _outputWriter.Advance(VariableLengthIntegerHelper.WriteInteger(buffer, id));
            return _outputWriter.FlushAsync().AsTask();
        }

        internal Task WriteDataAsync(in ReadOnlySequence<byte> data)
        {
            // The Length property of a ReadOnlySequence can be expensive, so we cache the value.
            var dataLength = data.Length;

            WriteDataUnsynchronized(data, dataLength);
            return Task.CompletedTask;
        }

        private void WriteDataUnsynchronized(in ReadOnlySequence<byte> data, long dataLength)
        {
            Debug.Assert(dataLength == data.Length);

            _outgoingFrame.PrepareData();

            if (dataLength > MaxDataFrameSize)
            {
                SplitAndWriteDataUnsynchronized(in data, dataLength);
                return;
            }

            _outgoingFrame.Length = (int)dataLength;

            Http3FrameWriter.WriteHeader(_outgoingFrame.Type, _outgoingFrame.Length, _outputWriter);

            foreach (var buffer in data)
            {
                _outputWriter.Write(buffer.Span);
            }

            return;

            void SplitAndWriteDataUnsynchronized(in ReadOnlySequence<byte> data, long dataLength)
            {
                Debug.Assert(dataLength == data.Length);

                var dataPayloadLength = (int)MaxDataFrameSize;

                Debug.Assert(dataLength > dataPayloadLength);

                var remainingData = data;
                do
                {
                    var currentData = remainingData.Slice(0, dataPayloadLength);
                    _outgoingFrame.Length = dataPayloadLength;

                    Http3FrameWriter.WriteHeader(_outgoingFrame.Type, _outgoingFrame.Length, _outputWriter);

                    foreach (var buffer in currentData)
                    {
                        _outputWriter.Write(buffer.Span);
                    }

                    dataLength -= dataPayloadLength;
                    remainingData = remainingData.Slice(dataPayloadLength);

                } while (dataLength > dataPayloadLength);

                _outgoingFrame.Length = (int)dataLength;

                Http3FrameWriter.WriteHeader(_outgoingFrame.Type, _outgoingFrame.Length, _outputWriter);

                foreach (var buffer in remainingData)
                {
                    _outputWriter.Write(buffer.Span);
                }
            }
        }

        internal Task WriteRequestHeaders(IDictionary<string, string> headers)
        {
            try
            {
                int headersTotalSize = 0;
                _outgoingFrame.PrepareHeaders();
                var buffer = _headerEncodingBuffer.GetSpan(HeaderBufferSize);
                var done = QPackHeaderWriter.BeginEncodeHeaders(headers, buffer, ref headersTotalSize, out var payloadLength);
                _headerEncodingBuffer.Advance(payloadLength);
                _outgoingFrame.Length = _headerEncodingBuffer.WrittenCount;
                Http3FrameWriter.WriteHeader(_outgoingFrame.Type, _outgoingFrame.Length, _outputWriter);
                _outputWriter.Write(_headerEncodingBuffer.WrittenSpan);
                return Task.CompletedTask;
            }
            // Any exception from the QPack encoder can leave the dynamic table in a corrupt state.
            // Since we allow custom header encoders we don't know what type of exceptions to expect.
            catch (Exception ex)
            {
                throw new InvalidOperationException(ex.Message, ex); // Report the error to the user if this was the first write.
            }
        }

        internal Task WriteEndStream() => _outputWriter.CompleteAsync().AsTask();
    }
}
