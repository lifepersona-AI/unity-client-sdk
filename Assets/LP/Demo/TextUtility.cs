using System.Text.RegularExpressions;

namespace LP
{
    /// <summary>
    /// Utility class for text processing operations
    /// </summary>
    public static class TextUtility
    {
        // Regex pattern to match emoji characters
        // Covers most common emoji ranges:
        // - Basic Emoticons (U+2600-U+26FF, U+2700-U+27BF)
        // - Miscellaneous Symbols (U+2300-U+23FF, U+2B50-U+2BFF)
        // - Supplementary Private Use Area (U+1F000-U+1F9FF)
        // - Enclosed characters (U+3030, U+303D, etc.)
        private static readonly Regex EmojiPattern = new Regex(
            @"[\u2600-\u27BF]|[\uE000-\uF8FF]|\uD83C[\uDC00-\uDFFF]|\uD83D[\uDC00-\uDFFF]|[\u2011-\u26FF]|\uD83E[\uDD10-\uDDFF]|[\uD800-\uDBFF][\uDC00-\uDFFF]",
            RegexOptions.Compiled
        );

        /// <summary>
        /// Removes all emoji characters from the input string
        /// </summary>
        /// <param name="text">Input text that may contain emojis</param>
        /// <returns>Text with all emojis removed</returns>
        public static string RemoveEmojis(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove emojis and trim any extra whitespace
            return EmojiPattern.Replace(text, "").Trim();
        }
    }
}
