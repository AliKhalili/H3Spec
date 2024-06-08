namespace H3Spec.DotNet
{
    internal readonly struct Http3PeerSetting
    {
        public Http3PeerSetting(Http3SettingType parameter, uint value)
        {
            Parameter = parameter;
            Value = value;
        }

        public Http3SettingType Parameter { get; }

        public uint Value { get; }
    }
}
