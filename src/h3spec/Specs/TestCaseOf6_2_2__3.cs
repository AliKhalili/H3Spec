using H3Spec.Core;
using H3Spec.Core.Http;
using System.Net.Quic;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-6.2.2-3"/>
    /// Description: Only servers can push; if a server receives a client-initiated push stream, this MUST be treated as a connection error of type H3_STREAM_CREATION_ERROR.
    /// </summary>
    internal class TestCaseOf6_2_2__3 : TestCase
    {
        public TestCaseOf6_2_2__3() : base(
            description: "Client-initiated push stream received by the server.",
            requirement: "Only servers can push; if a server receives a client-initiated push stream, this MUST be treated as a connection error of type H3_STREAM_CREATION_ERROR.",
            section: "6.2.2-3")
        {
        }

        public override async Task ExecuteAsync(Http3Connection connection)
        {
            try
            {
                var outboundControlStream = await connection.OpenStreamAsync(QuicStreamType.Unidirectional);
                await outboundControlStream.WriteStreamTypeId((long)Http3StreamType.Push);

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

        public override TestResult Verify() => VerifyHttp3Error(Http3ErrorCode.StreamCreationError);
    }
}
