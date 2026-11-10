namespace Poe2Accessibility.Services
{
    /// <summary>A recorded cue's two channels, of equal length, at the mixer rate.</summary>
    public sealed record StereoSound(float[] Left, float[] Right);
}
