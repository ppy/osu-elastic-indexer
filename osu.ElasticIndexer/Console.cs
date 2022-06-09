// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.ElasticIndexer
{
    public static class Console
    {
        public static void WriteLine(string? value = null) =>
            System.Console.WriteLine(value);

        public static void WriteLine(ConsoleColor foregroundColor, string? value)
        {
            System.Console.ForegroundColor = foregroundColor;
            System.Console.WriteLine(value);
            System.Console.ResetColor();
        }
    }
}
