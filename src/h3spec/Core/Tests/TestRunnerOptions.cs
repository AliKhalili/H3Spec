namespace H3Spec.Core
{
    internal static class TestRunnerOptions
    {
        public static TimeSpan WaitForInboundControlStream { get; set; } = TimeSpan.FromSeconds(1);
        public static TimeSpan WaitForOutboundControlStream { get; set; } = TimeSpan.FromSeconds(1);
    }
}
