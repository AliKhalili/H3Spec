using H3Spec.Core;
using H3Spec.Core.Http;
using H3Spec.DotNet;
using System.IO.Pipelines;
using System.Net.Quic;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-6.2-7"/>
    /// Description: The recipient MUST NOT consider unknown stream types to be a connection error of any kind.
    /// </summary>
    internal class TestCaseOf6_2__7 : TestCase
    {
        public TestCaseOf6_2__7() : base(
            description: "Unknown stream types MUST NOT be considered a connection error.",
            requirement: "The recipient MUST NOT consider unknown stream types to be a connection error of any kind.",
            section: "6.2-7")
        {
        }

        public async override Task ExecuteAsync(Http3Connection connection)
        {
            try
            {
                long unknownStreamType = 0x256;
                var outboundControlStream = await connection.OpenStreamAsync(QuicStreamType.Unidirectional);
                await outboundControlStream.WriteStreamTypeId(unknownStreamType);
                await WriteDummyFrameAsync(outboundControlStream.Output);
                await outboundControlStream.WriteEndStream();

                await WaitForOutboundStreamTask(null);

                if (outboundControlStream.Exception != null)
                {
                    Exception = outboundControlStream.Exception;
                }

            }
            catch (Exception ex)
            {
                Exception = ex;
            }
        }

        public override TestResult Verify()
        {
            var expected = "The recipient MUST NOT consider unknown stream types to be a connection error of any kind.";
            if (Exception != null)
            {
                return new TestResult(
                    IsPassed: false,
                    Expected: expected,
                    Actual: Exception.Message
                    );
            }
            return new TestResult(IsPassed: true, Expected: expected, Actual: expected);
        }

        private Task WriteDummyFrameAsync(PipeWriter output)
        {
            var length = VariableLengthIntegerHelper.GetByteCount(1);

            Http3FrameWriter.WriteHeader(Http3FrameType.Settings, length, output);

            var buffer = output.GetSpan(8);
            VariableLengthIntegerHelper.WriteInteger(buffer, 1);
            output.Advance(length);
            return output.FlushAsync().AsTask();
        }
    }
}
