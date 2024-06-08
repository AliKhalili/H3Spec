using H3Spec.Core;
using H3Spec.Core.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Quic;
using System.Text;
using System.Threading.Tasks;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-4.3.1-2.16.1"/>
    /// Description:  The path pseudo-header field MUST NOT be empty for "http" or "https" URIs;
    /// </summary>
    internal class TestCaseOf4_3_1__2_16_1 : TestCase
    {
        public TestCaseOf4_3_1__2_16_1() : base(
            description: "The path pseudo-header field MUST NOT be empty for \"http\" or \"https\" URIs;",
            requirement: "The path pseudo-header field MUST NOT be empty for \"http\" or \"https\" URIs;",
            section: "4.3.1-2.16.1")
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
                    {":authority", connection.QuicConnection.TargetHostName },
                    {":method", "GET" },
                    {":path", string.Empty },
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
