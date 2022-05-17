// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.ElasticIndexer
{
    public static class ConsoleExtensions
    {
        public static void WriteLine(this ConsoleColor foregroundColor, string? value)
        {
            Console.ForegroundColor = foregroundColor;
            Console.WriteLine(value);
            Console.ResetColor();
        }
    }
}
