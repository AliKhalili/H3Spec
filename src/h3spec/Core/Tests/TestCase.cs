using System.Net.Quic;
using H3Spec.Core.Http;

namespace H3Spec.Core
{
    internal abstract class TestCase
    {
        public readonly string Description;
        public readonly string Requirement;
        public readonly string Section;

        protected Exception? Exception;

        protected TestCase(string description, string requirement, string section)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            ArgumentException.ThrowIfNullOrWhiteSpace(requirement);
            ArgumentException.ThrowIfNullOrWhiteSpace(section);

            Description = description;
            Requirement = requirement;
            Section = section;
        }

        public async Task<TestResult> Run(Http3Connection connection)
        {
            await ExecuteAsync(connection);

            return Verify();
        }

        public abstract Task ExecuteAsync(Http3Connection connection);
        public abstract TestResult Verify();

        public TestResult VerifyHttp3Error(Http3ErrorCode expected)
        {
            var exception = Exception;
            string expectedString = $"Http3ErrorCode.{expected}({expected:x}) is expected.";
            if (exception == null)
            {
                return new TestResult(false, expectedString, "No exception is thrown by the connection.");
            }
            if (exception is QuicException quicException)
            {
                if (quicException.ApplicationErrorCode == null)
                {
                    return new TestResult(false, expectedString, "An exception is thrown by the connection, but it application protocol error code is null.");
                }
                var errorCode = (Http3ErrorCode)quicException.ApplicationErrorCode;
                if (errorCode == expected)
                {
                    return new TestResult(true, expectedString, $"Http3ErrorCode.{expected}({expected:x}) is thrown by the connection.");
                }
                else
                {
                    return new TestResult(false, expectedString, $"An exception is thrown by the connection, but it application protocol error code is Http3ErrorCode.{errorCode}({errorCode:x}).");
                }
            }
            else
            {
                return new TestResult(false, expectedString, $"An exception is thrown by the connection, but it is not a QuicException. {exception.Message}");
            }

        }

        protected async Task WaitForInboundStreamTask(Task inboundStreamTask) => await Task.WhenAny(inboundStreamTask, Task.Delay(TestRunnerOptions.WaitForInboundControlStream));
        protected async Task WaitForOutboundStreamTask(Task? outboundStreamTask)
        {
            if (outboundStreamTask == null)
            {
                await Task.Delay(TestRunnerOptions.WaitForOutboundControlStream);
                return;
            }
            await Task.WhenAny(outboundStreamTask, Task.Delay(TestRunnerOptions.WaitForOutboundControlStream));
        }
    }
}
