using H3Spec.Core;
using H3Spec.Core.Http;
using System.Net.Quic;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-7.2.4.1-5"/>
    /// Description: Setting identifiers that were defined in [HTTP/2] where there is no corresponding HTTP/3 setting have also been reserved (Section 11.2.2). These reserved settings MUST NOT be sent, and their receipt MUST be treated as a connection error of type H3_SETTINGS_ERROR.
    /// </summary>
    internal class TestCaseOf7_2_4_1__5 : TestCase
    {
        public TestCaseOf7_2_4_1__5() : base(
            description: "Reserved settings MUST NOT be sent.",
            requirement: "Setting identifiers that were defined in [HTTP/2] where there is no corresponding HTTP/3 setting have also been reserved (Section 11.2.2). These reserved settings MUST NOT be sent, and their receipt MUST be treated as a connection error of type H3_SETTINGS_ERROR.",
            section: "7.2.4.1-5")
        {
        }

        public async override Task ExecuteAsync(Http3Connection connection)
        {
            try
            {
                var outboundControlStream = await connection.OpenStreamAsync(QuicStreamType.Unidirectional);
                await outboundControlStream.WriteStreamTypeId((long)Http3StreamType.Control);

                await outboundControlStream.WriteSettingsFrameAsync([
                    new(Http3SettingType.ReservedHttp2EnablePush, 1),
                    new(Http3SettingType.ReservedHttp2MaxConcurrentStreams, 1),
                    new(Http3SettingType.ReservedHttp2InitialWindowSize, 1),
                    new(Http3SettingType.ReservedHttp2MaxFrameSize, 1),
                ]);

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

        public override TestResult Verify() => VerifyHttp3Error(Http3ErrorCode.SettingsError);
    }
}
