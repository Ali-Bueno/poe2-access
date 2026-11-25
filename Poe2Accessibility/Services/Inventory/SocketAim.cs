namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>Which socket of a piece of gear the game's cursor frame is on, by where both are drawn. Pure.</summary>
    public static class SocketAim
    {
        /// <summary>The folder of every rune, soul core and talisman in the game's files (all 305 of them, 2026-11-16).</summary>
        private const string SocketableFolder = "Metadata/Items/SoulCores/";

        /// <summary>The frame's centre was within half a unit of the socket's; a quarter of the socket keeps two neighbours apart.</summary>
        private const float Reach = 0.25f;

        /// <summary>The frame is a cell, or the socket it shrinks to; a larger layer (a whole panel) is none.</summary>
        private const float MaxFrame = 2f;

        public static bool IsSocketable(string basePath) => basePath.StartsWith(SocketableFolder, StringComparison.Ordinal);

        /// <summary>The socket whose centre the frame is centred on, or -1.</summary>
        public static int Socket(Vector2 frameCentre, Vector2 frameSize, IReadOnlyList<Vector2> centres, Vector2 socketSize)
        {
            if (!(socketSize.X > 0f && socketSize.Y > 0f && frameSize.X > 0f && frameSize.Y > 0f) ||
                frameSize.X > socketSize.X * MaxFrame || frameSize.Y > socketSize.Y * MaxFrame)
            {
                return -1;
            }

            for (var index = 0; index < centres.Count; index++)
            {
                var apart = Vector2.Abs(frameCentre - centres[index]);
                if (apart.X <= socketSize.X * Reach && apart.Y <= socketSize.Y * Reach)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
