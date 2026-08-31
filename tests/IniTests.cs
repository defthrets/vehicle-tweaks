using System;
using System.IO;
using VehicleTweaks.Core;

/// <summary>
/// The ini reader and, mostly, the ini WRITER.
///
/// THE WRITER IS WHY THIS EXISTS. It is the one piece of this mod that can quietly destroy
/// something the player owns -- their settings file, with all its comments, which the panel
/// rewrites every time it closes. It is also the piece that has already been caught doing it:
/// it used File.WriteAllLines, which writes Environment.NewLine whatever the file used, so
/// saving a single setting into an ini with bare newlines rewrote every line in it.
/// </summary>
internal static class IniTests
{
    private static void Check(string what, bool ok) => Harness.Check(what, ok);

    private static string Term(string text)
    {
        return text.Contains("\r\n") ? "CRLF" : text.Contains("\n") ? "LF" : "none";
    }

    public static void Run(string ini, string iniPristine)
    {
        Harness.Section("ini reader and writer");

        var before = File.ReadAllLines(ini);

        // 1. An existing key, changed in place.
        Check("SetValue reports success on an existing key",
              IniFile.SetValue(ini, "Blinkers", "BlinkerArmSeconds", "1.4"));

        // 2. A key that is absent from a section that exists.
        Check("SetValue reports success on a missing key",
              IniFile.SetValue(ini, "Ignition", "NotAKeyYet", "7"));

        // 3. A section that does not exist at all.
        Check("SetValue reports success on a missing section",
              IniFile.SetValue(ini, "Invented", "Something", "9"));

        var after = File.ReadAllLines(ini);

        // Every comment line that was there is still there, in the same order.
        var commentsBefore = Array.FindAll(before, l => l.TrimStart().StartsWith(";"));
        var commentsAfter = Array.FindAll(after, l => l.TrimStart().StartsWith(";"));

        Check("every comment survived (" + commentsBefore.Length + " of them)",
              commentsBefore.Length == commentsAfter.Length &&
              string.Join("\n", commentsBefore) == string.Join("\n", commentsAfter));

        // The file grew by exactly what was added: one key, one blank + header + key.
        Check("the file grew by 4 lines and no more, was " + before.Length + " now " + after.Length,
              after.Length == before.Length + 4);

        // The changed line is still directly under its own comment, rather than appended to
        // the end of the section. Measured against the line above it, because the two other
        // writes in this test legitimately shift absolute line numbers.
        var at = Array.FindIndex(after, l => l.TrimStart().StartsWith("BlinkerArmSeconds"));
        Check("the changed key is still under its own comment",
              at > 0 && after[at - 1].Contains("How long the wheel is held over"));

        // LINE ENDINGS. The whole promise of this writer is that one setting is one line.
        var rawBefore = File.ReadAllText(iniPristine);
        var rawAfter = File.ReadAllText(ini);

        Check("line endings are unchanged (was " + Term(rawBefore) + ", now " + Term(rawAfter) + ")",
              Term(rawBefore) == Term(rawAfter));

        // ON THE BYTES, not on the string. File.ReadAllText strips a BOM while decoding, so a
        // check through it passes whether or not one is there -- a test that cannot fail.
        var bytes = File.ReadAllBytes(ini);
        Check("no byte order mark was added",
              bytes.Length < 3 || !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF));

        // And it reads back as what was written, through the real parser.
        var reread = IniFile.Load(ini);

        Check("changed value reads back as 1.4",
              Math.Abs(reread.GetFloat("Blinkers", "BlinkerArmSeconds", -1f) - 1.4f) < 0.0001f);
        Check("appended key reads back from its section",
              reread.GetString("Ignition", "NotAKeyYet", null) == "7");
        Check("invented section reads back",
              reread.GetString("Invented", "Something", null) == "9");

        // Nothing else moved.
        Check("an untouched neighbour is unchanged",
              Math.Abs(reread.GetFloat("Blinkers", "BlinkerCancelSeconds", -1f) - 2.0f) < 0.0001f);
        Check("an untouched other section is unchanged",
              reread.GetString("Ignition", "ManualIgnitionMaxSpeed", null) == "2.5");
        // A FUNCTION KEY, which is the case the single-character path in GetKey does NOT
        // cover -- "F8" is two characters and has to survive Enum.TryParse on its own.
        Check("the key parser still reads MenuKey", reread.GetKey("General", "MenuKey",
              System.Windows.Forms.Keys.None) == System.Windows.Forms.Keys.F8);

        // A clamp, which is what stops a bad ini becoming a bad frame.
        Check("out-of-range value is clamped, not taken",
              Math.Abs(reread.GetFloat("Blinkers", "BlinkerArmSeconds", 1f, 0.1f, 1.0f) - 1.0f) < 0.0001f);

    }
}
