using System;
using System.Windows.Forms;

namespace VehicleTweaks.Core
{
    /// <summary>What has to be held down with the menu key.</summary>
    internal enum MenuModifier
    {
        None,
        Shift,
        Control,
        Alt
    }

    /// <summary>
    /// Everything tunable, read once at start-up from VehicleTweaks.ini.
    ///
    /// Every value has a code default, so a missing or half-written ini degrades to something
    /// playable rather than to a nonsense timer at 60fps. Every value is also CLAMPED on the
    /// way in: these are durations and speeds that get compared against Game.GameTime and
    /// Vehicle.Speed every frame, and a negative arming time is an indicator that comes on the
    /// instant the wheel moves at all.
    ///
    /// THE NAMES ARE THE ONES FUMES USED, deliberately. Both features grew up inside that mod
    /// and anybody who tuned them there can copy their numbers straight across. The sections
    /// are new -- Fumes filed them under a catch-all [Engine] because it had a fuel system to
    /// share it with, and here they are the whole mod.
    /// </summary>
    internal sealed class Settings
    {
        // ---- general ----------------------------------------------------------

        public bool Enabled = true;
        public LogLevel LogLevel = LogLevel.Info;

        /// <summary>
        /// Says hello in the notification feed the first time it ticks.
        ///
        /// OFF, which is the opposite of what most mods do and the opposite of what Fumes does.
        /// The entire design of both features is that nothing announces itself -- an engine you
        /// left running is not an event, and an indicator is not a notification -- and a mod
        /// that opens by posting its own name has broken that before it has done anything. The
        /// log records the load for anyone who needs to know it happened.
        /// </summary>
        public bool AnnounceOnLoad = false;

        /// <summary>
        /// The key that opens the settings panel, and what has to be held with it.
        ///
        /// NO MODIFIER, BECAUSE F8 DOES NOT NEED ONE. A modifier is what a LETTER needs: a bare
        /// letter is one keystroke from whatever else the player has bound it to, this game has
        /// mods on most of the alphabet, and the vanilla letters are all spoken for -- V, the
        /// obvious first choice for a vehicle mod, is the camera key. A function key is none of
        /// those things. Vanilla binds nothing to F8, so there is no conflict for a modifier to
        /// resolve, and adding one would only make the panel harder to open than it needs to be.
        ///
        /// Both halves stay configurable, and both are rows in the panel, so a player whose
        /// other mods have claimed F8 can move it without touching this file.
        /// </summary>
        public Keys MenuKey = Keys.F8;
        public MenuModifier MenuModifier = MenuModifier.None;

        /// <summary>
        /// The pad's way into the panel: a button held, and a button pressed.
        ///
        /// STRINGS, NOT GTA.Control, AND THAT IS DELIBERATE. Core does not reference a single
        /// SHVDN type, which is the only reason the ini reader and its writer can be compiled
        /// into a console exe and actually TESTED -- see tests\IniTests.cs. Putting a GTA enum
        /// in this file to save one Enum.TryParse in the panel would trade a test suite that
        /// runs for a type that reads slightly better. The panel resolves these by name and
        /// says so in the log, including when it cannot.
        ///
        /// A CHORD, because there is no spare pad button. Every face button, shoulder and stick
        /// is a gameplay action and the D-pad changes the radio station; holding one and
        /// pressing another is not something a thumb does by accident. The default holds the
        /// View / Select / touchpad button and presses D-pad up.
        ///
        /// "Off" on PadOpen means the pad cannot open the panel. "None" on PadModifier means the
        /// single button does it on its own.
        /// </summary>
        public string PadOpen = "PhoneUp";
        public string PadModifier = "MultiplayerInfo";

        /// <summary>
        /// The combination, written the way a person would say it.
        ///
        /// HERE RATHER THAN IN THE PANEL, because two places need it and they must not be able
        /// to disagree: the panel prints it in its own footer, and the log prints it at start-up.
        /// Concatenating the two fields is what the log used to do, and with no modifier set
        /// that produced "None+F8".
        /// </summary>
        public string BindingText()
        {
            var key = MenuKey.ToString().ToUpperInvariant();

            switch (MenuModifier)
            {
                case MenuModifier.Shift: return "SHIFT+" + key;
                case MenuModifier.Control: return "CTRL+" + key;
                case MenuModifier.Alt: return "ALT+" + key;
                default: return key;
            }
        }

        // ---- ignition ---------------------------------------------------------

        /// <summary>
        /// The ignition is the player's, not the game's.
        ///
        /// Hold the exit key to stop the engine without getting out; tap it to get out and
        /// leave the car exactly as it stands; get in and nothing starts until the throttle is
        /// touched.
        /// </summary>
        public bool ManualIgnition = true;

        /// <summary>How long the exit key counts as held rather than tapped.</summary>
        public float ExitHoldSeconds = 0.30f;

        /// <summary>
        /// How fast the car can be moving, in metres a second, before the exit key goes back to
        /// the game untouched.
        ///
        /// 2.5 is a brisk walk. Above it the game handles the key exactly as it always did --
        /// hold to bail out -- because a tap that ejects you at sixty is not what a tap should
        /// do, and refusing the tap on its own would have left no way out of a moving car.
        /// </summary>
        public float ManualIgnitionMaxSpeed = 2.5f;

        /// <summary>
        /// Whether aircraft get it too. They do not, by default.
        ///
        /// The gesture that parks a car is the one that kills you in a helicopter, and it is
        /// the same key you use to climb out on the ground.
        /// </summary>
        public bool ManualIgnitionAircraft = false;

        /// <summary>
        /// The starter turns over before the engine catches.
        ///
        /// NOT A TIMER AND NOT A SOUND WE PLAY. SET_VEHICLE_ENGINE_ON's third argument is
        /// "instantly", and passing false hands the whole thing to the game: its own cranking,
        /// its own starter sound, its own length, correct for the engine in that particular car.
        /// A half-second we invented would be the same half-second in a moped and a tanker.
        ///
        /// This is what makes the ignition feel like a key rather than a switch, and it is the
        /// one part of the feature that was still instant.
        /// </summary>
        public bool StarterCranks = true;

        /// <summary>
        /// A car left running keeps its radio on, loud enough to hear from outside.
        ///
        /// Off leaves the radio to the game, which stops it the moment you are not in the seat.
        /// </summary>
        public bool RadioKeepsPlaying = true;

        /// <summary>
        /// Headlights are left the way you left them, like the engine and the radio.
        ///
        /// The third thing you leave on. The game switches them off as the driver gets out,
        /// which is the same argument it has about the engine and the radio and is answered the
        /// same way. A car left running with its lights on is a different thing to walk away
        /// from at night.
        /// </summary>
        public bool LightsStayAsLeft = true;

        /// <summary>
        /// The driver's door is left open when you step out.
        ///
        /// Getting back in shuts it, which is the game's own behaviour and needs nothing from
        /// us. Off if you would rather not have it torn off by passing traffic, which is a
        /// realistic outcome and not everybody's idea of a good time.
        /// </summary>
        public bool LeaveDoorOpen = true;

        // ---- safety -----------------------------------------------------------

        /// <summary>
        /// The seatbelt, which you are wearing unless you took it off.
        ///
        /// THE WRONG WAY ROUND ON PURPOSE. A belt you have to buckle needs somewhere to tell you
        /// whether you did -- the benefit is invisible until the one crash where you do not go
        /// through the windscreen, so "am I belted?" is a question that wants a HUD, and this mod
        /// does not have one while you are driving.
        ///
        /// Belted by default and a key to UNDO it means the common case has no question in it.
        /// You are always wearing it, exactly as the ignition is always yours, and taking it off
        /// is the deliberate act that a deliberate act's feedback can be attached to.
        /// </summary>
        public bool Seatbelt = true;

        public Keys SeatbeltKey = Keys.K;
        public string PadSeatbelt = "PhoneLeft";

        /// <summary>
        /// How long after getting in before it goes on.
        ///
        /// Not instant, because instant is a thing that happens TO you. A second is about how
        /// long reaching over your shoulder takes, and it is long enough that hopping in and
        /// straight out again never involves a belt at all.
        /// </summary>
        public float SeatbeltSeconds = 1.0f;

        /// <summary>
        /// The handbrake goes on when you step out.
        ///
        /// The one omission from "left as you left it" that actively costs you the car: park on
        /// any of the hills in this city and an unbraked car is at the bottom of it when you get
        /// back. It is released the moment you are in the driver's seat again -- which has to be
        /// certain, because a handbrake left on is a car that will not pull away.
        /// </summary>
        public bool HandbrakeOnExit = true;

        /// <summary>
        /// Locking your car.
        ///
        /// Worth more since the door started being left open, which is exactly the invitation it
        /// looks like. The choice it creates is the point: lock it and walk away, or leave it
        /// running with the door open because you will only be a second.
        /// </summary>
        public bool Locking = true;

        public Keys LockKey = Keys.L;
        public string PadLock = "PhoneRight";

        /// <summary>
        /// The horn blips when it locks.
        ///
        /// FEEDBACK THAT IS ALSO THE FEATURE. Locking is otherwise invisible -- nothing on the
        /// car looks different -- so it needs to say something, and every real car in the last
        /// forty years says it the same way. A notification would have been this mod talking; a
        /// chirp is the car talking, which is the only voice it is supposed to have.
        /// </summary>
        public bool LockChirp = true;

        // ---- indicators -------------------------------------------------------

        /// <summary>
        /// Indicators worked by the steering wheel, self-cancelling like a real one.
        /// </summary>
        public bool Blinkers = true;

        /// <summary>How long the wheel is held over before that side comes on.</summary>
        public float BlinkerArmSeconds = 1.0f;

        /// <summary>How long a moving car goes without steering that way before it cancels.</summary>
        public float BlinkerCancelSeconds = 2.0f;

        /// <summary>
        /// How long the wheel must be held the OTHER way before it cancels.
        ///
        /// Short, but not nothing. Coming out of a turn you hold the wheel over for the best
        /// part of a second; lining the car up between two turns the same way is a flick, and
        /// cancelling on that flick puts the indicator out exactly when you want it on.
        /// </summary>
        public float BlinkerOppositeSeconds = 0.6f;

        /// <summary>How far the wheel counts as turned at all, 0 to 1.</summary>
        public float BlinkerDeadzone = 0.35f;

        /// <summary>
        /// How fast the car must be moving, in metres a second, for a centred wheel to cancel.
        ///
        /// Below it a centred wheel means nothing, which is what lets you signal at a red light
        /// and let go of the wheel.
        /// </summary>
        public float BlinkerMinSpeed = 1.5f;

        /// <summary>
        /// Hazard lights: both indicators at once.
        ///
        /// A KEY, because there is no gesture left. The indicators are worked by the steering
        /// wheel and there is no steering input that means "both" -- you cannot hold the wheel
        /// left and right at once, which is precisely why a real car puts hazards on a separate
        /// switch rather than on the stalk.
        ///
        /// J because it is free. H is the vanilla headlight key and E is the horn; most of the
        /// rest of the row is spoken for by the game or by other mods.
        /// </summary>
        public Keys HazardKey = Keys.J;

        /// <summary>
        /// The pad's hazards: the panel's own modifier, and this button.
        ///
        /// SHARING PadModifier ON PURPOSE. One button held, and then D-pad up for the panel or
        /// D-pad down for the hazards, is a thing you can learn once. Two unrelated chords is
        /// two things to remember and twice the chance of colliding with something.
        /// </summary>
        public string PadHazard = "PhoneDown";

        /// <summary>
        /// Swap which way the SIGNED steering axis reads.
        ///
        /// Only used on setups where the one-sided steering controls report nothing; the normal
        /// path reads each side's own control and has no convention to get backwards.
        /// </summary>
        public bool BlinkerInvert = false;

        public static Settings Load()
        {
            var s = new Settings();

            try
            {
                var ini = IniFile.Load(Paths.Ini);

                s.Enabled = ini.GetBool("General", "Enabled", s.Enabled);
                s.AnnounceOnLoad = ini.GetBool("General", "AnnounceOnLoad", s.AnnounceOnLoad);
                s.LogLevel = ParseEnum(ini.GetString("General", "LogLevel", "Info"), s.LogLevel);
                s.MenuKey = ini.GetKey("General", "MenuKey", s.MenuKey);
                s.MenuModifier = ParseEnum(ini.GetString("General", "MenuModifier", "None"), s.MenuModifier);
                s.PadOpen = ini.GetString("General", "PadOpen", s.PadOpen);
                s.PadModifier = ini.GetString("General", "PadModifier", s.PadModifier);

                s.ManualIgnition = ini.GetBool("Ignition", "ManualIgnition", s.ManualIgnition);
                s.ExitHoldSeconds = ini.GetFloat("Ignition", "ExitHoldSeconds", s.ExitHoldSeconds, 0.1f, 3f);
                s.ManualIgnitionMaxSpeed = ini.GetFloat("Ignition", "ManualIgnitionMaxSpeed", s.ManualIgnitionMaxSpeed, 0f, 60f);
                s.ManualIgnitionAircraft = ini.GetBool("Ignition", "ManualIgnitionAircraft", s.ManualIgnitionAircraft);
                s.StarterCranks = ini.GetBool("Ignition", "StarterCranks", s.StarterCranks);
                s.RadioKeepsPlaying = ini.GetBool("Ignition", "RadioKeepsPlaying", s.RadioKeepsPlaying);
                s.LightsStayAsLeft = ini.GetBool("Ignition", "LightsStayAsLeft", s.LightsStayAsLeft);
                s.LeaveDoorOpen = ini.GetBool("Ignition", "LeaveDoorOpen", s.LeaveDoorOpen);

                s.Blinkers = ini.GetBool("Blinkers", "Blinkers", s.Blinkers);
                s.BlinkerArmSeconds = ini.GetFloat("Blinkers", "BlinkerArmSeconds", s.BlinkerArmSeconds, 0.1f, 5f);
                s.BlinkerCancelSeconds = ini.GetFloat("Blinkers", "BlinkerCancelSeconds", s.BlinkerCancelSeconds, 0.1f, 10f);
                s.BlinkerOppositeSeconds = ini.GetFloat("Blinkers", "BlinkerOppositeSeconds", s.BlinkerOppositeSeconds, 0f, 5f);
                s.BlinkerDeadzone = ini.GetFloat("Blinkers", "BlinkerDeadzone", s.BlinkerDeadzone, 0.05f, 0.95f);
                s.BlinkerMinSpeed = ini.GetFloat("Blinkers", "BlinkerMinSpeed", s.BlinkerMinSpeed, 0f, 20f);
                s.BlinkerInvert = ini.GetBool("Blinkers", "BlinkerInvert", s.BlinkerInvert);
                s.Seatbelt = ini.GetBool("Safety", "Seatbelt", s.Seatbelt);
                s.SeatbeltKey = ini.GetKey("Safety", "SeatbeltKey", s.SeatbeltKey);
                s.PadSeatbelt = ini.GetString("Safety", "PadSeatbelt", s.PadSeatbelt);
                s.SeatbeltSeconds = ini.GetFloat("Safety", "SeatbeltSeconds", s.SeatbeltSeconds, 0f, 10f);
                s.HandbrakeOnExit = ini.GetBool("Safety", "HandbrakeOnExit", s.HandbrakeOnExit);
                s.Locking = ini.GetBool("Safety", "Locking", s.Locking);
                s.LockKey = ini.GetKey("Safety", "LockKey", s.LockKey);
                s.PadLock = ini.GetString("Safety", "PadLock", s.PadLock);
                s.LockChirp = ini.GetBool("Safety", "LockChirp", s.LockChirp);

                s.HazardKey = ini.GetKey("Blinkers", "HazardKey", s.HazardKey);
                s.PadHazard = ini.GetString("Blinkers", "PadHazard", s.PadHazard);
            }
            catch (Exception ex)
            {
                Log.Error("Settings failed to load; every default applies.", ex);
            }

            Log.Level = s.LogLevel;
            return s;
        }

        private static T ParseEnum<T>(string text, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            if (Enum.TryParse(text.Trim(), true, out T parsed) && Enum.IsDefined(typeof(T), parsed)) return parsed;

            Log.Warn("Setting value " + text + " is not a " + typeof(T).Name + " - using " + fallback + ".");
            return fallback;
        }
    }
}
