using System;
using System.Collections.Generic;

namespace Avalonia86.Tools
{
    /// <summary>
    /// A self-contained, zero-allocation natural sort engine.
    /// </summary>
    /// <remarks>
    /// To speed things up, we could have each item being sorted pretokenized.
    ///
    /// However, it is less cache-friendly as it requires storing additional tokenized data.
    /// For our use case, it might actually be slower.
    /// </remarks>
    internal class NaturalComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            ReadOnlySpan<char> xSpan = x.AsSpan();
            ReadOnlySpan<char> ySpan = y.AsSpan();

            int xIndex = 0;
            int yIndex = 0;

            bool hasNaturalCompared = false;

            while (true)
            {
                bool hasX = xIndex < xSpan.Length;
                bool hasY = yIndex < ySpan.Length;

                // If one or both streams ended
                if (!hasX || !hasY)
                {
                    // If both ended at the same time, they are equal.
                    // If X ended first, it's shorter (returns -1). If Y ended first, it's longer (returns 1).
                    if (hasX) return 1;
                    if (hasY) return -1;
                    return 0;
                }

                // Get the next numeric or non-numeric tokens.
                ReadOnlySpan<char> xTokValue = Tokenize(xSpan, ref xIndex, out bool xIsNumeric);
                ReadOnlySpan<char> yTokValue = Tokenize(ySpan, ref yIndex, out bool yIsNumeric);

                if (xTokValue.SequenceEqual(yTokValue)) continue;

                // Only attempt natural sort if both are numeric and we haven't done it yet
                if (!hasNaturalCompared && xIsNumeric && yIsNumeric)
                {
                    // 1. Find the start of significant digits by skipping leading zeros
                    int xStart = 0;
                    while (xStart < xTokValue.Length && xTokValue[xStart] == '0') xStart++;

                    int yStart = 0;
                    while (yStart < yTokValue.Length && yTokValue[yStart] == '0') yStart++;

                    // 2. Calculate the length of the actual numerical value 
                    int xSigLength = xTokValue.Length - xStart;
                    int ySigLength = yTokValue.Length - yStart;

                    // 3. More significant digits means a larger numeric value.
                    if (xSigLength != ySigLength)
                    {
                        hasNaturalCompared = true;
                        return xSigLength.CompareTo(ySigLength);
                    }

                    // 4. If the significant lengths are equal, compare the remaining digits
                    // lexicographically to determine the larger numeric value (e.g. "123" vs "125").
                    int numCompare = xTokValue.Slice(xStart, xSigLength).SequenceCompareTo(yTokValue.Slice(yStart, ySigLength));
                    if (numCompare != 0)
                    {
                        hasNaturalCompared = true;
                        return numCompare;
                    }

                    // 5. If the values are numerically equal, the one with fewer leading zeros sorts first.
                    int zeroCompare = xStart.CompareTo(yStart);
                    if (zeroCompare != 0)
                    {
                        hasNaturalCompared = true;
                        return zeroCompare;
                    }

                    // If they reached here, the numeric chunks are 100% identical (e.g. "001" vs "001").
                }

                // Fallback to an ordinal, case-insensitive string comparison (handles identical length differences like "07" vs "13")
                int stringCompare = MemoryExtensions.CompareTo(xTokValue, yTokValue, StringComparison.OrdinalIgnoreCase);
                if (stringCompare != 0) return stringCompare;
            }
        }

        private static ReadOnlySpan<char> Tokenize(ReadOnlySpan<char> src, ref int index, out bool isNumeric)
        {
            int start = index;
            isNumeric = char.IsDigit(src[index]);
            index++;

            while (index < src.Length)
            {
                if (char.IsDigit(src[index]) != isNumeric)
                {
                    break; // Token boundary found
                }
                index++;
            }

            return src.Slice(start, index - start);
        }
    }
}