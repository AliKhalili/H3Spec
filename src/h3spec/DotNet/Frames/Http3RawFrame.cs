namespace H3Spec.DotNet.Frames
{
#pragma warning disable CA1852 // Seal internal types
    internal partial class Http3RawFrame
#pragma warning restore CA1852 // Seal internal types
    {
        public long Length { get; set; }

        public Http3FrameType Type { get; internal set; }

        public string FormattedType => Http3Formatting.ToFormattedType(Type);

        public override string ToString() => $"{FormattedType} Length: {Length}";
    }
}
