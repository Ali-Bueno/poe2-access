namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;

    /// <summary>The assertions the test classes share: a failure throws, and the runner prints its message.</summary>
    internal static class Check
    {
        public static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{what}: expected {expected}, got {actual}");
            }
        }

        public static void Expect<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"{what}: expected '{expected}', got '{actual}'");
            }
        }

        public static void Near(float expected, float? actual, string what, float tolerance = 0.01f)
        {
            if (actual is not { } value || MathF.Abs(value - expected) > tolerance)
            {
                throw new Exception($"{what}: expected {expected}, got {actual}");
            }
        }
    }
}
