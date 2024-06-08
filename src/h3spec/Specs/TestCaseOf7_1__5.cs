using H3Spec.Core;
using H3Spec.Core.Http;
using H3Spec.DotNet;
using System.IO.Pipelines;
using System.Net.Quic;

namespace H3Spec.Specs
{
    /// <summary>
    /// Link: <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-7.1-5"/>
    /// Description: Each frame's payload MUST contain exactly the fields identified in its description. A frame payload that contains additional bytes after the identified fields or a frame payload that terminates before the end of the identified fields MUST be treated as a connection error of type H3_FRAME_ERROR.
    /// Also <see cref="https://www.rfc-editor.org/rfc/rfc9114.html#section-10.8">
    /// An implementation MUST ensure that the length of a frame exactly matches the length of the fields it contains.
    /// </summary>
    internal class TestCaseOf7_1__5 : TestCase
    {

        public TestCaseOf7_1__5() : base(
            description: "Frame payload contains additional bytes after the identified fields.",
            requirement: "Each frame's payload MUST contain exactly the fields identified in its description. A frame payload that contains additional bytes after the identified fields or a frame payload that terminates before the end of the identified fields MUST be treated as a connection error of type H3_FRAME_ERROR.",
            section: "7.1-5")
        {
        }

        public override async Task ExecuteAsync(Http3Connection connection)
        {
            try
            {
                var outboundControlStream = await connection.OpenStreamAsync(QuicStreamType.Unidirectional);
                await outboundControlStream.WriteStreamTypeId((long)Http3StreamType.Control);
                await WriteFrameWithAdditionalByteAsync(outboundControlStream.Output);

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

        public override TestResult Verify() => VerifyHttp3Error(Http3ErrorCode.FrameError);

        private Task WriteFrameWithAdditionalByteAsync(PipeWriter output)
        {
            List<Http3PeerSetting> settings = [new(Http3SettingType.QPackBlockedStreams, 100000)];
            var settingsLength = Http3FrameWriter.CalculateSettingsSize(settings);
            // Call GetSpan with enough room for
            // - One encoded length int for setting size
            // - 1 byte for setting type
            // - settings length
            var buffer = output.GetSpan(settingsLength + VariableLengthIntegerHelper.MaximumEncodedLength + 1);

            buffer[0] = (byte)Http3FrameType.Settings;
            buffer = buffer[1..];

            var settingsBytesWritten = VariableLengthIntegerHelper.WriteInteger(buffer, settingsLength);
            buffer = buffer.Slice(settingsBytesWritten);

            // tyoe + length + payload
            var totalLength = 1 + settingsLength + settingsBytesWritten;

            Http3FrameWriter.WriteSettings(settings, buffer);
            output.Advance(totalLength);

            // write additional byte after payload without modifying frame length 
            buffer = output.GetSpan(1);
            buffer[0] = 0x1; // additional byte after payload
            output.Advance(1);

            return output.FlushAsync().AsTask();
        }
    }
}
