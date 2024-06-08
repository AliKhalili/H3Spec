namespace H3Spec.DotNet.Frames
{
    internal partial class Http3RawFrame
    {
        public void PrepareGoAway()
        {
            Length = 0;
            Type = Http3FrameType.GoAway;
        }
    }
}
