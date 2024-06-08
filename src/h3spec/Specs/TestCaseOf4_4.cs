using H3Spec.Core;
using H3Spec.Core.Http;
using System.Net.Quic;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-4.4"/>
    /// Description: The CONNECT method is used to establish a tunnel over a single stream.
    /// </summary>
    internal class TestCaseOf4_4 : TestCase
    {
        public TestCaseOf4_4() : base(
            description: "The CONNECT Method.",
            requirement: "The CONNECT method is used to establish a tunnel over a single stream.",
            section: "4.4")
        {
        }

        private string _httpStatusCode = string.Empty;
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
                    {":authority", $"{connection.QuicConnection.TargetHostName}:443" }, // The :authority pseudo-header field contains the host and port to connect to
                    {":method", "CONNECT" }, // The :method pseudo-header field is set to "CONNECT"
                    // The :scheme and :path pseudo-header fields are omitted
                });
                await requestStream.WriteEndStream();
                var requestTask = requestStream.ProcessRequestAsync();

                //  Once this connection is successfully established, the proxy sends a HEADERS frame containing a 2xx series status code to the client, as defined in Section 15.3 of [HTTP].
                await WaitForOutboundStreamTask(requestTask);

                requestStream.Headers.TryGetValue(":status", out _httpStatusCode);

            }
            catch (Exception ex)
            {
                Exception = ex;
            }
        }

        public override TestResult Verify()
        {
            if (string.IsNullOrWhiteSpace(_httpStatusCode))
            {
                return new TestResult(false, "A 2xx series status code is expected.", "The status code is empty.");
            }
            if (_httpStatusCode.StartsWith("2"))
            {
                return new TestResult(true, $"A 2xx series status code is expected.", $"The status code is {_httpStatusCode}.");
            }
            else
            {
                return new TestResult(false, $"A 2xx series status code is expected.", $"The status code is {_httpStatusCode}.");
            }
        }
    }
}
