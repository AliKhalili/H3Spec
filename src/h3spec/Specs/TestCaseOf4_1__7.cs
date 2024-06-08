using H3Spec.Core;
using H3Spec.Core.Http;
using System.Net.Quic;
using System.Text;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-4.1-7"/>
    /// Description: Receipt of an invalid sequence of frames MUST be treated as a connection error of type H3_FRAME_UNEXPECTED. 
    /// </summary>
    internal class TestCaseOf4_1__7 : TestCase
    {
        private string _request = "{ 'data': 'ping!' }";
        private int? _statusCode = null;
        public TestCaseOf4_1__7() : base(
            description: "Receipt invalid sequence of frames.",
            requirement: "Receipt of an invalid sequence of frames MUST be treated as a connection error of type H3_FRAME_UNEXPECTED.",
            section: "4.1-7")
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

                // send request
                var headers = new Dictionary<string, string>()
                {
                    {":authority", connection.QuicConnection.TargetHostName },
                    {":method", "POST" },
                    {":path", "/" },
                    {":scheme", "https" },
                    {"Content-Type", "application/x-www-form-urlencoded" }
                };
                var body = Encoding.UTF8.GetBytes(_request);

                await requestStream.WriteData(body); // bodey before header
                await requestStream.WriteRequestHeader(headers);
                await requestStream.WriteEndStream();

                // receive response
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

        public override TestResult Verify() => VerifyHttp3Error(Http3ErrorCode.UnexpectedFrame);
    }
}
