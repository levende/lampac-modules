using System.Linq;
using Unidecode.NET;

namespace SelfCDN
{
    public static class Transliterator
    {
        public static string Transliterate(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            return input.Unidecode();
        }

        public static bool HasCyrillic(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            return input.Any(c =>
                c is >= '\u0400' and <= '\u04FF'  // Cyrillic block
                or >= '\u0500' and <= '\u052F'    // Cyrillic Supplement block
                or >= '\u2DE0' and <= '\u2DFF'    // Cyrillic Extended-A block
                or >= '\uA640' and <= '\uA69F');  // Cyrillic Extended-B block
        }
    }
}