using H3Spec.Core.Http;
using H3Spec.Specs;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Text.RegularExpressions;

namespace H3Spec.Core.Cli
{
    internal sealed class TestCommand : Command<TestCommand.Settings>
    {
        private readonly Http3ServerOptions[] http3ServerOptions;
        private readonly TestCase[] validTestCases = [
            new TestCaseOf4_1__7(),
            new TestCaseOf4_3__4(),
            new TestCaseOf6_2_1__2(),
            new TestCaseOf6_2_2__3(),
            new TestCaseOf6_2__7(),
            new TestCaseOf7_1__5(),
            new TestCaseOf7_2_4__2(),
            new TestCaseOf7_2_4_1__5(),
            new TestCaseOf7_2_8__3(),
            new TestCaseOf4_3_1__2_10_1(),
            new TestCaseOf4_3_1__2_16_1(),
            new TestCaseOf4_4(),
        ];

        public TestCommand(Http3ServerOptions[] http3ServerOptions) => this.http3ServerOptions = http3ServerOptions;

        public override int Execute(CommandContext context, Settings settings)
        {
            var testCases = FilterTestCases(settings.SearchSectionPattern, validTestCases);
            var servers = FilterSevers(settings.Servers, http3ServerOptions);

            var serversResult = new Dictionary<string, int>();
            foreach (var server in servers)
            {
                WriteRow($"----------------   {server.Description}   ----------------", Color.SteelBlue);

                var passed = 0;
                foreach (var test in testCases.OrderBy(x => x.Section))
                {
                    try
                    {
                        var connection = CreateConnection(server).GetAwaiter().GetResult();
                        var result = test.Run(connection).GetAwaiter().GetResult();

                        if (result.IsPassed)
                        {
                            WriteRow($":check_mark_button:  {test.Section}: {test.Description}", Color.Green3);
                            passed++;
                        }
                        else
                        {
                            WriteRow($":cross_mark:  {test.Section}: {test.Description}", Color.Red3_1);
                            WriteRow($"   expected: {result.Expected}", Color.Grey);
                            WriteRow($"   actual: {result.Actual}", Color.DarkRed_1);
                        }
                        connection.Close();
                    }
                    catch (Exception exception)
                    {

                        WriteRow($":cross_mark:  {test.Section}: {test.Description}", Color.Red3_1);
                        WriteRow($"   {exception.Message}", Color.DarkRed_1);
                    }
                }

                WriteRow($"[bold] Number of {passed} tests passed from total number of {testCases.Length}.[/]", Color.GreenYellow);
                serversResult[server.Description] = passed;
            }

            WriteResultReport(serversResult);
            return 0;
        }

        public sealed class Settings : CommandSettings
        {
            [Description("Filter tests by section with pattern such as '4.*' or '7.1-*'. default: all tests")]
            [CommandOption("-p|--pattern")]
            public string? SearchSectionPattern { get; init; }

            [Description("Filter servers to run tests on based on the server name such as '.net aio'. default: all servers")]
            [CommandOption("-s|--server")]
            public string[]? Servers { get; init; }
        }

        static void WriteRow(string content, Color color) => AnsiConsole.MarkupLine($"[{color.ToString()}]{content}[/]");
        static void WriteResultReport(Dictionary<string, int> result)
        {
            WriteRow($"----------------   Resutl   ----------------", Color.OrangeRed1);
            var table = new Table();
            table.AddColumn(new TableColumn("Server").Centered());
            table.AddColumn(new TableColumn("Result").Centered());

            foreach (var item in result)
            {
                table.AddRow($"[orangered1 bold]{item.Key}[/]", $"[orangered1 bold]{item.Value}[/]");
            }
            AnsiConsole.Write(table);
        }

        private TestCase[] FilterTestCases(string? searchSectionPattern, TestCase[] validTestCases)
        {
            if (string.IsNullOrEmpty(searchSectionPattern))
            {
                return validTestCases;
            }

            var regex = new Regex(searchSectionPattern.Replace("*", ".*"));
            return validTestCases.Where(testCase => regex.IsMatch(testCase.Section)).ToArray();
        }

        private Http3ServerOptions[] FilterSevers(string[]? serverNames, Http3ServerOptions[] http3ServerOptions)
        {
            if (serverNames == null || serverNames.Length == 0)
            {
                return http3ServerOptions;
            }

            return http3ServerOptions.Where(server => serverNames.Equals(server.Name)).ToArray();
        }

        private async Task<Http3Connection> CreateConnection(Http3ServerOptions server)
        {
            // Configure connection defaults to match HttpClient defaults.
            // https://github.com/dotnet/runtime/blob/a5f3676cc71e176084f0f7f1f6beeecd86fbeafc/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/ConnectHelper.cs#L113
            var clientConnectionOptions = new QuicClientConnectionOptions
            {
                RemoteEndPoint = new DnsEndPoint(server.Host, server.Port),
                DefaultStreamErrorCode = (long)Http3ErrorCode.RequestCancelled,
                DefaultCloseErrorCode = (long)Http3ErrorCode.NoError,
                MaxInboundUnidirectionalStreams = 5, // Minimum is 3 (1x control stream + 2x QPACK).
                MaxInboundBidirectionalStreams = 0, // Client doesn't support inbound streams
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http3 },
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                },
            };
            var quicConnection = await QuicConnection.ConnectAsync(clientConnectionOptions);
            return new Http3Connection(quicConnection);
        }
    }
}
