using System;

/// <summary>
/// The test runner.
///
/// WHAT CAN BE TESTED IS WHAT DOES NOT NEED THE GAME. Core references no SHVDN type, so it
/// compiles into a plain console exe and can actually be executed here -- which is the whole
/// reason the ini writer and the indicator rules live in Core rather than beside the code that
/// uses them.
///
/// Everything else needs a car and a road and is checked that way, which is slow, manual, and
/// has already let two bugs through. Anything that can be moved across this line should be.
///
/// Run with:  .\build.ps1 -Test
/// </summary>
internal static class Harness
{
    private static int _failed;
    private static int _passed;

    public static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what);

        if (ok) _passed++;
        else _failed++;
    }

    public static void Section(string name)
    {
        Console.WriteLine();
        Console.WriteLine("-- " + name + " --");
    }

    private static int Main(string[] args)
    {
        if (args.Length < 2 || args.Length % 2 != 0)
        {
            Console.WriteLine("usage: tests (<working-copy.ini> <pristine-copy.ini>)...");
            return 2;
        }

        // The ini suite runs once per copy, because the copies differ in the one thing that
        // suite is about: a file written with bare newlines and a file written with the Windows
        // pair. Everything else runs once, because running it twice would say nothing twice.
        for (var i = 0; i < args.Length; i += 2) IniTests.Run(args[i], args[i + 1]);

        BlinkerTests.Run();

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "ALL PASSED (" + _passed + ")"
            : _failed + " FAILED of " + (_passed + _failed));

        return _failed;
    }
}
