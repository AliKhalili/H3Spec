using H3Spec.Core;
using H3Spec.Core.Http;
using System.Net.Quic;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-4.3.1-2.10.1"/>
    /// Description:  The authority MUST NOT include the deprecated userinfo subcomponent for URIs of scheme "http" or "https". 
    /// </summary>
    internal class TestCaseOf4_3_1__2_10_1 : TestCase
    {
        public TestCaseOf4_3_1__2_10_1() : base(
            description: "The authority MUST NOT include the deprecated userinfo subcomponent.",
            requirement: "The authority MUST NOT include the deprecated userinfo subcomponent for URIs of scheme \"http\" or \"https\".",
            section: "4.3.1-2.10.1")
        {
        }

        public async override Task ExecuteAsync(Http3Connection connection)
        {
            try
            {
                var outboundControlStream = await connection.OpenStreamAsync(QuicStreamType.Unidirectional);
                await outboundControlStream.WriteStreamTypeId((long)Http3StreamType.Control);

                await outboundControlStream.WriteSettingsFrameAsync([
                    new(Http3SettingType.QPackMaxTableCapacity, 1)
                ]);


                var requestStream = await connection.OpenStreamAsync(QuicStreamType.Bidirectional);
                await requestStream.WriteRequestHeader(new Dictionary<string, string>()
                {
                    {":authority", $"http://{connection.QuicConnection.TargetHostName}" },
                    {":method", "GET" },
                    {":path", "/" },
                    {":scheme", "https" },
                });
                await requestStream.WriteEndStream();
                var requestTask = requestStream.ProcessRequestAsync();
                await WaitForOutboundStreamTask(requestTask);

                if (requestStream.Exception != null)
                {
                    Exception = requestStream.Exception;
                }
            }
            catch (Exception ex)
            {
                Exception = ex;
            }
        }

        public override TestResult Verify() => VerifyHttp3Error(Http3ErrorCode.MessageError);
    }
}
