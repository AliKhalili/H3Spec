using H3Spec.DotNet.Frames;
using System.Buffers;

namespace H3Spec.DotNet
{
    internal sealed class Http3FrameReader
    {
        /* https://quicwg.org/base-drafts/draft-ietf-quic-http.html#frame-layout
             0                   1                   2                   3
             0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                           Type (i)                          ...
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                          Length (i)                         ...
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                       Frame Payload (*)                     ...
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        */
        internal static bool TryReadFrame(ref ReadOnlySequence<byte> readableBuffer, Http3RawFrame frame, out ReadOnlySequence<byte> framePayload)
        {
            framePayload = ReadOnlySequence<byte>.Empty;
            SequencePosition consumed;

            var type = VariableLengthIntegerHelper.GetInteger(readableBuffer, out consumed, out _);
            if (type == -1)
            {
                return false;
            }

            var firstLengthBuffer = readableBuffer.Slice(consumed);

            var length = VariableLengthIntegerHelper.GetInteger(firstLengthBuffer, out consumed, out _);

            // Make sure the whole frame is buffered
            if (length == -1)
            {
                return false;
            }

            var startOfFramePayload = readableBuffer.Slice(consumed);
            if (startOfFramePayload.Length < length)
            {
                return false;
            }

            frame.Length = length;
            frame.Type = (Http3FrameType)type;

            // The remaining payload minus the extra fields
            framePayload = startOfFramePayload.Slice(0, length);
            readableBuffer = readableBuffer.Slice(framePayload.End);

            return true;
        }
    }

}
