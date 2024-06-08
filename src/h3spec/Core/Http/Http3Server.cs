namespace H3Spec.Core.Http
{
    internal class Http3Server
    {
        public readonly string Host;
        public readonly int Port;
        public readonly string Name;
        public readonly string Description;

        public Http3Server(string host, int port, string name, string description)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

            Host = host;
            Port = port;
            Name = name;
            Description = description;
        }

        public override string ToString() => $"{Name}";
    }
}
