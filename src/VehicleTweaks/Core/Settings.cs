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
        /// A car left running keeps its radio on, loud enough to hear from outside.
        ///
        /// Off leaves the radio to the game, which stops it the moment you are not in the seat.
        /// </summary>
        public bool RadioKeepsPlaying = true;

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
                s.RadioKeepsPlaying = ini.GetBool("Ignition", "RadioKeepsPlaying", s.RadioKeepsPlaying);

                s.Blinkers = ini.GetBool("Blinkers", "Blinkers", s.Blinkers);
                s.BlinkerArmSeconds = ini.GetFloat("Blinkers", "BlinkerArmSeconds", s.BlinkerArmSeconds, 0.1f, 5f);
                s.BlinkerCancelSeconds = ini.GetFloat("Blinkers", "BlinkerCancelSeconds", s.BlinkerCancelSeconds, 0.1f, 10f);
                s.BlinkerOppositeSeconds = ini.GetFloat("Blinkers", "BlinkerOppositeSeconds", s.BlinkerOppositeSeconds, 0f, 5f);
                s.BlinkerDeadzone = ini.GetFloat("Blinkers", "BlinkerDeadzone", s.BlinkerDeadzone, 0.05f, 0.95f);
                s.BlinkerMinSpeed = ini.GetFloat("Blinkers", "BlinkerMinSpeed", s.BlinkerMinSpeed, 0f, 20f);
                s.BlinkerInvert = ini.GetBool("Blinkers", "BlinkerInvert", s.BlinkerInvert);
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
