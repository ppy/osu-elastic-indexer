// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.ElasticIndexer
{
    public static class Console
    {
        public static void WriteLine(string? value = null) =>
            System.Console.WriteLine(value);

        /// <summary>
        /// General guidelines for the colours used:
        /// Cyan - Typically informational.
        /// Green - Confirmations, notable operating messages.
        /// Red - Critical failure, destructive operation.
        /// Yellow - Warnings, important messages, etc.
        ///
        /// Consecutive messages can and should use different colours for contrast.
        /// </summary>
        /// <param name="foregroundColor">The colour of the text to write.</param>
        /// <param name="value">The value to write.</param>
        public static void WriteLine(ConsoleColor foregroundColor, string? value)
        {
            System.Console.ForegroundColor = foregroundColor;
            System.Console.WriteLine(value);
            System.Console.ResetColor();
        }
    }
}
