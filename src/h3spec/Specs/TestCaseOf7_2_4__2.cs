using H3Spec.Core;
using H3Spec.Core.Http;
using System.Net.Quic;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-7.2.4-2"/>
    /// Description:  A SETTINGS MUST NOT be sent subsequently. If an endpoint receives a second SETTINGS frame on the control stream, the endpoint MUST respond with a connection error of type H3_FRAME_UNEXPECTED.
    /// </summary>
    internal class TestCaseOf7_2_4__2 : TestCase
    {
        public TestCaseOf7_2_4__2() : base(
            description: "A SETTINGS MUST NOT be sent subsequently.",
            requirement: "A SETTINGS MUST NOT be sent subsequently. If an endpoint receives a second SETTINGS frame on the control stream, the endpoint MUST respond with a connection error of type H3_FRAME_UNEXPECTED.",
            section: "7.2.4-2")
        {
        }

        public async override Task ExecuteAsync(Http3Connection connection)
        {
            try
            {
                var outboundControlStream = await connection.OpenStreamAsync(QuicStreamType.Unidirectional);
                await outboundControlStream.WriteStreamTypeId((long)Http3StreamType.Control);

                // first
                await outboundControlStream.WriteSettingsFrameAsync([new(Http3SettingType.QPackBlockedStreams, 1)]);
                // second
                await outboundControlStream.WriteSettingsFrameAsync([new(Http3SettingType.QPackMaxTableCapacity, 1)]);

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
    }
}
