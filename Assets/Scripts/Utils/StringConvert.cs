using System;
using System.Collections.Generic;
using UnityEngine;

namespace MinecraftClient
{
    public static class StringConvert
    {
        /// <summary>
        /// Convert the specified string to an integer, defaulting to zero if invalid argument
        /// </summary>
        /// <param name="str">String to parse as an integer</param>
        /// <returns>Integer value</returns>
        public static int str2int(string str)
        {
            try
            {
                return Convert.ToInt32(str.Trim());
            }
            catch {
                Translations.LogError("error.setting.str2int", str);
                return 0;
            }
        }

        /// <summary>
        /// Convert the specified string to a float number, defaulting to zero if invalid argument
        /// </summary>
        /// <param name="str">String to parse as a float number</param>
        /// <returns>Float number</returns>
        public static float str2float(string str)
        {
            float f;
            if (float.TryParse(str.Trim(), out f))
                return f;
            else
            {
                Translations.LogError("error.setting.str2int", str);
                return 0;
            }
        }

        /// <summary>
        /// Convert the specified string to a boolean value, defaulting to false if invalid argument
        /// </summary>
        /// <param name="str">String to parse as a boolean</param>
        /// <returns>Boolean value</returns>
        public static bool str2bool(string str)
        {
            if (String.IsNullOrEmpty(str))
                return false;
            str = str.Trim().ToLowerInvariant();
            return str == "true" || str == "1";
        }

        private static string GetTMPCloseTags(int formatFlag)
        {
            string closeTags = string.Empty;
            if ((formatFlag & 1 << 0) != 0) // 1st bit on, obfuscated
                closeTags += "</rotate>";
            if ((formatFlag & 1 << 1) != 0) // 2nd bit on, bold
                closeTags += "</b>";
            if ((formatFlag & 1 << 2) != 0) // 3rd bit on, strikethrough
                closeTags += "</s>";
            if ((formatFlag & 1 << 3) != 0) // 4th bit on, underline
                closeTags += "</u>";
            if ((formatFlag & 1 << 4) != 0) // 5th bit on, italic
                closeTags += "</i>";
            if ((formatFlag & 1 << 5) != 0) // 6th bit on, colored
                closeTags += "</color>";
            return closeTags;
        }

        private static string GetTMPColorTag(char color)
        {
            return color switch
            {   // Color codes...
                '0' => "<color=#000000>", // black
                '1' => "<color=#0000AA>", // dark_blue
                '2' => "<color=#00AA00>", // dark_green
                '3' => "<color=#00AAAA>", // dark_aqua
                '4' => "<color=#AA0000>", // dark_red
                '5' => "<color=#AA00AA>", // dark_purple
                '6' => "<color=#FFAA00>", // gold
                '7' => "<color=#AAAAAA>", // gray
                '8' => "<color=#555555>", // dark_gray
                '9' => "<color=#5555FF>", // blue
                'a' => "<color=#55FF55>", // green
                'b' => "<color=#55FFFF>", // aqua
                'c' => "<color=#FF5555>", // red
                'd' => "<color=#FF55FF>", // light_purple
                'e' => "<color=#FFFF55>", // yellow
                'f' => "<color=#FFFFFF>", // white
                'g' => "<color=#DDD605>", // minecoin_gold (this one's for BE, though)
                _   => string.Empty
            };
        }

        // Convert Minecraft formatting codes to TextMesh Pro format
        // The result text can somehow also be used in Unity Console
        public static string MC2TMP(string original) // Example: Try "§7[§7YaeMicorn: §7Granted blah blah blah §5[TI§5§c[INSIDE]TLE] to §7§7Someone]"
        {
            var processed = string.Empty;
            Stack<char> prevColors = new Stack<char>();
            // curColor:  The color used by next character (from original text)
            // lastColor: The color used by last character (from original text)
            char curColor = ' ', lastColor = ' ';
            int formatFlag = 0;
            for (int ptr = 0;ptr < original.Length;ptr++)
            {
                if (original[ptr] == '\u00a7' && (ptr + 1) < original.Length)
                {
                    // Skip section sign and read the formatting code...
                    ptr++;
                    // Make sure this code is in lower case...
                    char code = original.ToLower()[ptr];

                    if (code >= '0' && code <= '9' || code >= 'a' && code <= 'g')
                    {
                        // Text is already in that color, ignore it
                        if (code == curColor)
                            continue;
                        
                        // Reset all formatting codes when applying color codes (like vanilla Minecraft)
                        string prefix = GetTMPCloseTags(formatFlag);
                        formatFlag = 1 << 5; // Only the 'colored' flag bit is on, other bits are all turned off
                        prefix += GetTMPColorTag(code); 
                        curColor = code;
                        processed += prefix; // No original text appended, color tags only
                    }
                    else
                    {
                        string prefix;
                        switch (code)
                        {   // Format codes...
                            case'k':  // obfuscated
                                prefix = "<rotate=45>";
                                formatFlag |= 1 << 0;
                                break;
                            case'l':  // bold
                                prefix = "<b>";
                                formatFlag |= 1 << 1;
                                break;
                            case'm':  // strikethrough
                                prefix = "<s>";
                                formatFlag |= 1 << 2;
                                break;
                            case'n':  // underline
                                prefix = "<u>";
                                formatFlag |= 1 << 3;
                                break;
                            case'o':  // italic
                                prefix = "<i>";
                                formatFlag |= 1 << 4;
                                break;
                            case 'r': // reset
                                prefix = GetTMPCloseTags(formatFlag);
                                formatFlag = 0;
                                curColor = ' ';
                                prevColors.Clear(); // TODO
                                break;
                            default:
                                prefix = string.Empty;
                                break;
                        };
                        processed += prefix; // No original text appended, format tags only
                    }

                }
                else if (original[ptr] == '[') // Left bracket, a field starts...
                {
                    // push color
                    if (lastColor != ' ')
                        prevColors.Push(lastColor);
                    //Debug.Log("Push at " + ptr + ": " + lastColor);
                    processed += original[ptr]; // Append part of original text
                    lastColor = curColor;
                }
                else if (original[ptr] == ']') // Right bracket, a field ends...
                {
                    string prefix = string.Empty, suffix = string.Empty;
                    // pop color
                    if (prevColors.Count > 0)
                    {
                        char preserved = prevColors.Pop();
                        if (curColor != preserved) // Then apply(restore to) this color...
                        {   // End prev color if present
                            //Debug.Log("Pop at " + ptr + ": " + preserved);
                            if ((formatFlag & (1 << 5)) > 0)
                                suffix = "</color>";
                            // Then apply this color
                            suffix += GetTMPColorTag(curColor = preserved);
                            formatFlag |= 1 << 5; // Turn the 'colored' flag bit to on, and leave other flags unchanged
                        }
                    }

                    processed += (original[ptr] + suffix); // Append part of original text and change color
                    lastColor = curColor;
                }
                else
                {
                    processed += original[ptr];  // Append part of original text
                    lastColor = curColor;
                }

            }

            if (formatFlag > 0) // There're still unclosed tags, close 'em...
                return processed + GetTMPCloseTags(formatFlag);

            return processed;

        }

    }

}