/// <summary>Where the extractor's progress lines go: the console, or the writer the mod's executable gives it.</summary>
public static class Say
{
    private static volatile TextWriter? redirected;

    public static TextWriter Out => redirected ?? Console.Out;

    /// <summary>Sends the output to <paramref name="writer"/>; null goes back to the console.</summary>
    public static void Redirect(TextWriter? writer) => redirected = writer;
}
