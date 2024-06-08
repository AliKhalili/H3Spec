using H3Spec.Core;
using H3Spec.Core.Http;
using H3Spec.DotNet;
using System.IO.Pipelines;
using System.Net.Quic;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-7.2.8-3"/>
    /// Description: Frame types that were used in HTTP/2 where there is no corresponding HTTP/3 frame have also been reserved (Section 11.2.1). These frame types MUST NOT be sent, and their receipt MUST be treated as a connection error of type H3_FRAME_UNEXPECTED.
    /// </summary>
    internal class TestCaseOf7_2_8__3 : TestCase
    {
        public TestCaseOf7_2_8__3() : base(
            description: "HTTP/2 reserved frame types MUST be treated as a connection error.",
            requirement: "Frame types that were used in HTTP/2 where there is no corresponding HTTP/3 frame have also been reserved (Section 11.2.1). These frame types MUST NOT be sent, and their receipt MUST be treated as a connection error of type H3_FRAME_UNEXPECTED.",
            section: "7.2.8-3")
        {
        }

        public async override Task ExecuteAsync(Http3Connection connection)
        {
            try
            {
                var outboundControlStream = await connection.OpenStreamAsync(QuicStreamType.Unidirectional);
                await outboundControlStream.WriteStreamTypeId((long)Http3StreamType.Control);
                await WriteFrameReservedFrameType(outboundControlStream.Output);

                var inboundControlStream = await connection.AcceptStreamAsync();
                var inboundTask = inboundControlStream.ProcessRequestAsync();
                await WaitForInboundStreamTask(inboundTask);

                if (inboundControlStream.Exception != null)
                {
                    Exception = inboundControlStream.Exception;
                }

            }
            catch (Exception ex)
            {
                Exception = ex;
            }
        }

        public override TestResult Verify() => VerifyHttp3Error(Http3ErrorCode.UnexpectedFrame);

        private Task WriteFrameReservedFrameType(PipeWriter output)
        {
            var length = VariableLengthIntegerHelper.GetByteCount(1);

            Http3FrameWriter.WriteHeader(Http3FrameType.ReservedHttp2Priority, length, output);

            var buffer = output.GetSpan(8);
            VariableLengthIntegerHelper.WriteInteger(buffer, 1);
            output.Advance(length);
            return output.FlushAsync().AsTask();
        }
    }
}
