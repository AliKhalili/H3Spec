namespace H3Spec.DotNet.Frames
{
    internal partial class Http3RawFrame
    {
        public void PrepareHeaders()
        {
            Length = 0;
            Type = Http3FrameType.Headers;
        }
    }
}
