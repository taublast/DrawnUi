namespace DrawnUi.Draw;

public partial class SkiaLabel
{
    public static class EmojiData
    {

        /// <summary>
        /// Returns the length of EmojiModifierSequence if found at index ins
        /// </summary>
        /// <param name="text"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static int IsEmojiModifierSequence(string text, int index)
        {
            var sequenceLength = 0;

            if (index < text.Length && char.IsHighSurrogate(text[index]))
            {
                sequenceLength += 2; // Surrogate pair length
                int nextIndex = index + 2;

                // Loop to handle potential modifiers or additional emojis
                while (nextIndex < text.Length)
                {
                    if (IsModifier(text, nextIndex))
                    {
                        sequenceLength += 2; // Modifier length (surrogate pair)
                        nextIndex += 2;
                    }
                    else if (IsZeroWidthJoiner(text, nextIndex) && nextIndex + 1 < text.Length
                                                                && char.IsHighSurrogate(text[nextIndex + 1]))
                    {
                        sequenceLength += 3; // ZWJ and the next surrogate pair length
                        nextIndex += 3;
                    }
                    else
                    {
                        break;
                    }
                }

            }
            return sequenceLength;
        }


        public static bool IsModifier(string text, int index)
        {
            if (index < text.Length)
            {
                int codePoint;

                // Check if the character is part of a surrogate pair
                if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                {
                    // Combine the high and low surrogate to get the code point
                    codePoint = char.ConvertToUtf32(text, index);
                }
                else
                {
                    // Not a surrogate pair, use the character's code point directly
                    codePoint = text[index];
                }

                // Check if the code point is in the EmojiModifiers set
                return EmojiData.EmojiModifiers.Contains(codePoint);
            }

            return false;
        }

        public static bool IsZeroWidthJoiner(string text, int index)
        {
            if (index < text.Length)
            {
                return text[index] == '\u200D';
            }
            return false;
        }

        public static HashSet<int> EmojiModifiers = new HashSet<int>
        {
            0x1F3FB, // EMOJI MODIFIER FITZPATRICK TYPE-1-2
            0x1F3FC, // EMOJI MODIFIER FITZPATRICK TYPE-3
            0x1F3FD, // EMOJI MODIFIER FITZPATRICK TYPE-4
            0x1F3FE, // EMOJI MODIFIER FITZPATRICK TYPE-5
            0x1F3FF, // EMOJI MODIFIER FITZPATRICK TYPE-6
        };

        private static readonly List<(int Start, int End)> EmojiModifierBaseRanges = new List<(int Start, int End)>
        {
            (0x261d, 0x261d),
            (0x26f9, 0x26f9),
            (0x270a, 0x270b),
            (0x270c, 0x270d),
            (0x1f385, 0x1f385),
            (0x1f3c2, 0x1f3c4),
            (0x1f3c7, 0x1f3c7),
            (0x1f3ca, 0x1f3ca),
            (0x1f3cb, 0x1f3cc),
            (0x1f442, 0x1f443),
            (0x1f446, 0x1f450),
            (0x1f466, 0x1f469),
            (0x1f46e, 0x1f46e),
            (0x1f470, 0x1f478),
            (0x1f47c, 0x1f47c),
            (0x1f481, 0x1f483),
            (0x1f485, 0x1f487),
            (0x1f4aa, 0x1f4aa),
            (0x1f574, 0x1f575),
            (0x1f57a, 0x1f57a),
            (0x1f590, 0x1f590),
            (0x1f595, 0x1f596),
            (0x1f645, 0x1f647),
            (0x1f64b, 0x1f64f),
            (0x1f6a3, 0x1f6a3),
            (0x1f6b4, 0x1f6b6),
            (0x1f6c0, 0x1f6c0),
            (0x1f6cc, 0x1f6cc),
            (0x1f918, 0x1f918),
            (0x1f919, 0x1f91c),
            (0x1f91e, 0x1f91e),
            (0x1f91f, 0x1f91f),
            (0x1f926, 0x1f926),
            (0x1f930, 0x1f930),
            (0x1f931, 0x1f932),
            (0x1f933, 0x1f939),
            (0x1f93d, 0x1f93e),
            (0x1f9d1, 0x1f9dd)
        };

        public static bool IsEmoji(int codePoint)
        {
            switch (codePoint)
            {
            case >= 0x1F600 and <= 0x1F64F:   // Emoticons
            case >= 0x1F300 and <= 0x1F5FF:   // Misc Symbols and Pictographs
            case >= 0x1F680 and <= 0x1F6FF:   // Transport and Map
            case >= 0x2600 and <= 0x26FF:     // Misc symbols
            case >= 0x2700 and <= 0x27BF:     // Dingbats
            case >= 0xFE00 and <= 0xFE0F:     // Variation Selectors
            case >= 0x1F900 and <= 0x1F9FF:   // Supplemental Symbols and Pictographs
            case >= 0x1F1E6 and <= 0x1F1FF:   // Flags
            return true;
            default:
            return false;
            }
        }

    }
}