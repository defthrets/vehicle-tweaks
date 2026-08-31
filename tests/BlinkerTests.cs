using VehicleTweaks.Core;

/// <summary>
/// The indicator rules, driven by the clock rather than by a road.
///
/// Every one of these is a sentence from the design that was previously only checkable by
/// driving to a junction: "a flick the other way does not cancel it, a held turn does", "you can
/// signal at a red light and let go of the wheel". They are written here in the same words, so
/// that changing a rule breaks the test that says what the rule was FOR rather than a test that
/// merely says what the code did.
///
/// Time is passed in, so a two-second cancel takes no time at all to test and there is no
/// sleeping anywhere in here.
/// </summary>
internal static class BlinkerTests
{
    private const int Left = BlinkerRules.LeftSide;
    private const int Right = BlinkerRules.RightSide;
    private const int Centre = BlinkerRules.Off;

    /// <summary>Well over BlinkerMinSpeed, so a centred wheel counts as driving straight.</summary>
    private const float Moving = 10f;

    private static Settings Fresh()
    {
        // The shipped defaults, written out rather than loaded, so a change to the ini cannot
        // silently change what these tests are asserting.
        return new Settings
        {
            BlinkerArmSeconds = 1.0f,
            BlinkerCancelSeconds = 2.0f,
            BlinkerOppositeSeconds = 0.6f,
            BlinkerDeadzone = 0.35f,
            BlinkerMinSpeed = 1.5f,
            BlinkerInvert = false,
        };
    }

    /// <summary>Holds the wheel one way for a stretch of time, a frame every 16ms.</summary>
    private static int Hold(BlinkerRules rules, int from, int ms, int wheel, float speed)
    {
        var now = from;
        var until = from + ms;

        while (now <= until)
        {
            rules.Step(now, wheel, speed);
            now += 16;
        }

        return now;
    }

    public static void Run()
    {
        Harness.Section("indicators: coming on");

        var rules = new BlinkerRules(Fresh());
        rules.Enter(0, Centre, false);

        var t = Hold(rules, 0, 900, Left, Moving);
        Harness.Check("900ms of left lock is not yet an indicator", rules.Side == Centre);

        t = Hold(rules, t, 200, Left, Moving);
        Harness.Check("past a second of left lock turns the left one on", rules.Side == Left);
        Harness.Check("and only the left one", rules.LeftOn && !rules.RightOn);

        Harness.Section("indicators: the flick that must not cancel");

        rules = new BlinkerRules(Fresh());
        rules.Enter(0, Centre, false);
        t = Hold(rules, 0, 1100, Left, Moving);

        // Lining the car up between two turns the same way: a short jab of opposite lock.
        t = Hold(rules, t, 300, Right, Moving);
        Harness.Check("a 300ms flick of opposite lock does NOT cancel it", rules.Side == Left);

        // And back to left, as at the second junction.
        t = Hold(rules, t, 300, Left, Moving);
        Harness.Check("still on after coming back to the same lock", rules.Side == Left);

        Harness.Section("indicators: the held turn that must cancel");

        rules = new BlinkerRules(Fresh());
        rules.Enter(0, Centre, false);
        t = Hold(rules, 0, 1100, Left, Moving);
        Harness.Check("left is on to begin with", rules.Side == Left);

        t = Hold(rules, t, 700, Right, Moving);
        Harness.Check("700ms of held opposite lock cancels it", rules.Side == Centre);

        Harness.Section("indicators: straightening out");

        rules = new BlinkerRules(Fresh());
        rules.Enter(0, Centre, false);
        t = Hold(rules, 0, 1100, Left, Moving);

        t = Hold(rules, t, 1500, Centre, Moving);
        Harness.Check("1.5s of driving straight is not yet a cancel", rules.Side == Left);

        t = Hold(rules, t, 700, Centre, Moving);
        Harness.Check("past 2s of driving straight cancels it", rules.Side == Centre);

        Harness.Section("indicators: signalling at a red light");

        // THE ONE THE WHOLE DESIGN TURNS ON. The wheel returns to centre the moment it is
        // released, so a cancel that only asked "is the wheel straight" would put the indicator
        // out a second after it was set, and signalling before pulling away would be impossible.
        rules = new BlinkerRules(Fresh());
        rules.Enter(0, Centre, false);
        t = Hold(rules, 0, 1100, Left, 0f);
        Harness.Check("the wheel arms it while stationary", rules.Side == Left);

        t = Hold(rules, t, 10000, Centre, 0f);
        Harness.Check("ten seconds stopped with the wheel released and it is STILL on",
                      rules.Side == Left);

        t = Hold(rules, t, 2500, Centre, Moving);
        Harness.Check("and it cancels once you actually pull away", rules.Side == Centre);

        Harness.Section("hazards");

        rules = new BlinkerRules(Fresh());
        rules.Enter(0, Centre, false);

        rules.ToggleHazard(0);
        Harness.Check("hazards light both sides", rules.LeftOn && rules.RightOn);

        t = Hold(rules, 0, 3000, Left, Moving);
        Harness.Check("a full turn does not steal a side from the hazards",
                      rules.LeftOn && rules.RightOn);

        rules.ToggleHazard(t);
        Harness.Check("switching them off leaves nothing indicating",
                      !rules.LeftOn && !rules.RightOn);

        rules = new BlinkerRules(Fresh());
        rules.Enter(0, Left, true);
        Harness.Check("a car found on its hazards is taken as being on its hazards",
                      rules.Hazard && rules.LeftOn && rules.RightOn);

        Harness.Section("which way the wheel is over");

        // The convention this cannot afford to get backwards. A mod that indicates the wrong way
        // at every junction is worse than one that does nothing.
        Harness.Check("the one-sided controls each report their own side",
                      BlinkerRules.Wheel(0.9f, 0f, 0f, 0.35f, false) == Left &&
                      BlinkerRules.Wheel(0f, 0.9f, 0f, 0.35f, false) == Right);

        Harness.Check("inside the deadzone is centred",
                      BlinkerRules.Wheel(0.2f, 0.2f, 0.2f, 0.35f, false) == Centre);

        Harness.Check("the signed axis is only a fallback, and positive is right",
                      BlinkerRules.Wheel(0f, 0f, 0.9f, 0.35f, false) == Right &&
                      BlinkerRules.Wheel(0f, 0f, -0.9f, 0.35f, false) == Left);

        Harness.Check("invert swaps the fallback and nothing else",
                      BlinkerRules.Wheel(0f, 0f, 0.9f, 0.35f, true) == Left &&
                      BlinkerRules.Wheel(0.9f, 0f, 0f, 0.35f, true) == Left);
    }
}
