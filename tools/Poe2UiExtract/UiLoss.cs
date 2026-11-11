/// <summary>How many UI script files the control catalog may lose before its step fails (the step is required: a lost file is a lost screen).</summary>
internal static class UiLoss
{
    // A few damaged entries in hundreds of scripts do not stop a patch's catalog; a share of them means the index or the format changed.
    private const int MaxLostPerHundred = 2;

    /// <summary>Whether losing <paramref name="lost"/> of <paramref name="total"/> files leaves a catalog worth shipping.</summary>
    public static bool Tolerated(int lost, int total) => lost * 100L <= total * (long)MaxLostPerHundred;

    /// <summary>The line a failed step ends with, so the launcher's log carries the cause.</summary>
    public static string Failure(string what, int lost, int total) =>
        $"UI catalog not made: {lost} of {total} {what} were lost, over the {MaxLostPerHundred} per 100 tolerated";
}
