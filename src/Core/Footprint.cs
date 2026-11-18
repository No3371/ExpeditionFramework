using System;

namespace ExpeditionFramework.Core
{
    /// <summary>Measures the filled part of an item's grid shape.</summary>
    internal static class Footprint
    {
        /// <summary>
        /// The longer and shorter side of the smallest box around the filled cells, or (0, 0) when none is filled. It measures
        /// the box, not the grid: a sprite's grid can carry empty rows or columns, and counting them would make an item longer
        /// than it looks. A diagonal item fills a square box, so it reads its length in grid steps as reach and its breadth
        /// shows it isn't straight. Packs that want the diagonal longer can combine the two; a single rounded length would
        /// hide which it was.
        /// </summary>
        public static (int Reach, int Breadth) Measure(int width, int height, Func<int, int, bool> filled)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!filled(x, y)) continue;
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }
            if (maxX < 0) return (0, 0);
            int across = maxX - minX + 1, down = maxY - minY + 1;
            return (Math.Max(across, down), Math.Min(across, down));
        }
    }
}
