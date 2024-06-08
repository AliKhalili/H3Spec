using H3Spec.Core;
using H3Spec.Core.Http;
using System.Net.Quic;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-4.3-4"/>
    /// Description: All pseudo-header fields MUST appear in the header section before regular header fields. Any request or response that contains a pseudo-header field that appears in a header section after a regular header field MUST be treated as malformed. 
    /// </summary>
    internal class TestCaseOf4_3__4 : TestCase
    {
        public TestCaseOf4_3__4() : base(
            description: "Pseudo-header MUST appear before regular header fields",
            requirement: "All pseudo-header fields MUST appear in the header section before regular header fields. Any request or response that contains a pseudo-header field that appears in a header section after a regular header field MUST be treated as malformed.",
            section: "4.3-4")
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
                    {"Agent", "H3Spec" },
                    {":authority", connection.QuicConnection.TargetHostName },
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
