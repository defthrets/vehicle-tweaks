using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace VehicleTweaks.Core
{
    /// <summary>
    /// Small INI reader. Tolerates ';', '#' and '//' comments, trailing inline comments, and
    /// keys outside any section. Reading a missing file yields an empty instance rather than
    /// throwing, so a deleted ini falls back to the code defaults instead of killing the mod.
    ///
    /// TAKEN FROM FUMES, MINUS TWO METHODS. The original also carried a surgical single-value
    /// WRITER -- it edits one line of the file and leaves every comment around it alone -- and
    /// a key-name parser. Both existed for a settings menu, and this mod has no menu and no key
    /// settings: every control it reads is one the game already owns. They are in the Fumes
    /// history if a menu is ever wanted here.
    /// </summary>
    internal sealed class IniFile
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static IniFile Load(string path)
        {
            var ini = new IniFile();
            try
            {
                if (!File.Exists(path))
                {
                    Log.Warn("No ini at " + path + " - using built-in defaults.");
                    return ini;
                }

                var current = ini.SectionFor("");
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line[0] == ';' || line[0] == '#') continue;
                    if (line.StartsWith("//", StringComparison.Ordinal)) continue;

                    if (line[0] == '[')
                    {
                        var close = line.IndexOf(']');
                        if (close > 1)
                        {
                            current = ini.SectionFor(line.Substring(1, close - 1).Trim());
                            continue;
                        }
                    }

                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    var key = line.Substring(0, eq).Trim();
                    var value = StripInlineComment(line.Substring(eq + 1)).Trim();
                    current[key] = value;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed reading ini " + path, ex);
            }

            return ini;
        }

        /// <summary>
        /// Strips a trailing '//' or ';' comment, but only when whitespace precedes it, so a
        /// value that legitimately contains those characters survives.
        /// </summary>
        private static string StripInlineComment(string value)
        {
            for (var i = 1; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i - 1])) continue;
                if (value[i] == ';') return value.Substring(0, i);
                if (value[i] == '/' && i + 1 < value.Length && value[i + 1] == '/') return value.Substring(0, i);
            }
            return value;
        }

        private Dictionary<string, string> SectionFor(string name)
        {
            if (!_sections.TryGetValue(name, out var s))
            {
                s = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _sections[name] = s;
            }
            return s;
        }

        private bool TryGet(string section, string key, out string value)
        {
            value = null;
            return _sections.TryGetValue(section, out var s) && s.TryGetValue(key, out value);
        }

        public string GetString(string section, string key, string fallback)
        {
            return TryGet(section, key, out var v) && v.Length > 0 ? v : fallback;
        }

        public bool GetBool(string section, string key, bool fallback)
        {
            if (!TryGet(section, key, out var v)) return fallback;

            switch (v.Trim().ToLowerInvariant())
            {
                case "1": case "true": case "yes": case "on": return true;
                case "0": case "false": case "no": case "off": return false;
                default:
                    Log.Warn("[" + section + "] " + key + " = '" + v + "' is not a yes/no - using " + fallback + ".");
                    return fallback;
            }
        }

        public float GetFloat(string section, string key, float fallback, float min = float.MinValue, float max = float.MaxValue)
        {
            if (!TryGet(section, key, out var v)) return fallback;

            if (!float.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            {
                Log.Warn("[" + section + "] " + key + " = '" + v + "' is not a number - using " + fallback + ".");
                return fallback;
            }

            if (n < min || n > max)
            {
                Log.Warn("[" + section + "] " + key + " = " + n + " is outside " + min + ".." + max + " - clamped.");
                return n < min ? min : max;
            }

            return n;
        }
    }
}
