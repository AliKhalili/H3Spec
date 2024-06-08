using H3Spec.Core;
using H3Spec.Core.Http;
using System.Net.Quic;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-6.2.1-2"/>
    /// Description: If the first frame of the control stream is any other frame type, this MUST be treated as a connection error of type H3_MISSING_SETTINGS.
    /// </summary>
    internal class TestCaseOf6_2_1__2 : TestCase
    {
        public TestCaseOf6_2_1__2() : base(
            description: "Missing SETTINGS frame in the first frame of the control stream.",
            requirement: "If the first frame of the control stream is any other frame type, this MUST be treated as a connection error of type H3_MISSING_SETTINGS.",
            section: "6.2.1-2")
        {
        }

        public override async Task ExecuteAsync(Http3Connection connection)
        {
            try
            {
                var outboundControlStream = await connection.OpenStreamAsync(QuicStreamType.Unidirectional);
                await outboundControlStream.WriteStreamTypeId((long)Http3StreamType.Control);
                await outboundControlStream.WriteGoAway(0);

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

        public override TestResult Verify() => VerifyHttp3Error(Http3ErrorCode.MissingSettings);
    }
}
