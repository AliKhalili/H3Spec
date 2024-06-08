namespace H3Spec.Core
{
    internal class Http3ServerOptions
    {
        public string Host { get; set; }
        public int Port { get; set; } = 443;
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
