using System;
using System.Windows.Forms;

namespace VehicleTweaks.Core
{
    /// <summary>What the speed is read in.</summary>
    internal enum SpeedoUnits
    {
        Kph,
        Mph
    }

    /// <summary>
    /// What colour the display is lit.
    ///
    /// A SHORT LIST RATHER THAN THREE SLIDERS. Red, green and blue as separate numbers is three
    /// settings whose interesting combinations are a handful and whose uninteresting ones are
    /// sixteen million -- most of them colours no dashboard has ever been made in. These are the
    /// ones real displays actually use.
    /// </summary>
    internal enum SpeedoColour
    {
        Amber,
        Red,
        Green,
        Cyan,
        Blue,
        White
    }

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

        // ---- the speedo -------------------------------------------------------

        /// <summary>
        /// A speed readout: three seven-segment digits, a unit, a rev strip and the gear.
        ///
        /// THE ONE THING THIS MOD LEAVES ON SCREEN. Everything else here is silent on purpose,
        /// and that stays true: silence was always about not INTERRUPTING -- no prompts, no
        /// notifications, nothing to dismiss. A dial you glance at is what a car already has,
        /// and it is the one thing the game itself does not give you.
        /// </summary>
        public bool Speedo = true;

        public SpeedoUnits SpeedoUnits = SpeedoUnits.Kph;
        public SpeedoColour SpeedoColour = SpeedoColour.Amber;

        /// <summary>
        /// Where it sits, as fractions of the screen from the top left.
        ///
        /// Immediately right of the minimap, level with its bottom edge, because that is where
        /// it was asked for -- and it is the right answer: it puts the speed next to the map you
        /// are already looking at, in the one strip along the bottom that GTA leaves empty.
        ///
        /// THESE ARE AN ESTIMATE OFF A SCREENSHOT and they cannot be anything better. Where the
        /// minimap actually ends depends on the safe-zone slider and the aspect ratio, and there
        /// is no way for a script to ask. So the panel draws the speedo while it is open and
        /// these two rows move it live, which is the only honest way to place something on
        /// somebody else's screen.
        /// </summary>
        public float SpeedoX = 0.268f;
        public float SpeedoY = 0.928f;

        public float SpeedoScale = 1.0f;
        public float SpeedoOpacity = 0.90f;

        /// <summary>A dark panel behind it, so it reads against a white car in daylight.</summary>
        public bool SpeedoBackground = true;

        /// <summary>
        /// Unlit segments drawn faintly, the way a real display shows them.
        ///
        /// The detail that makes it a panel rather than a number: an LCD reading 42 also faintly
        /// shows the 8 it is not lighting, and without that the digits appear to jump around as
        /// the speed changes width.
        /// </summary>
        public bool SpeedoGhost = true;

        /// <summary>The rev strip under the digits, and the gear beside them.</summary>
        public bool SpeedoRevs = true;
        public bool SpeedoGear = true;

        public bool SpeedoOnlyInVehicle = true;

        /// <summary>
        /// The engine lamp, coloured by how well the engine is.
        ///
        /// GTA already tracks this and has never once shown it to you. Green, amber, red -- and
        /// those colours rather than the display's, because a warning lamp is a judgement and
        /// everybody already knows what a red one means.
        /// </summary>
        public bool SpeedoEngineIcon = true;

        /// <summary>
        /// The oil lamp, lit only when the oil is low.
        ///
        /// Also already tracked and never shown. Only when it is low, because a warning light
        /// that is on all the time is decoration -- the whole meaning of it is that seeing it is
        /// unusual. Measured against the engine's own capacity, which the game knows, rather
        /// than against a number in litres that would be wrong for half the cars in the game.
        /// </summary>
        public bool SpeedoOilLight = true;

        /// <summary>
        /// Drift tyres, the ones GTA Online actually has.
        ///
        /// SET_DRIFT_TYRES is the flag the Los Santos Tuners update added for the drift tuning
        /// you buy at a garage, so this switches on the handling Rockstar wrote rather than an
        /// impression of it. Nothing here models grip or fakes a slide.
        ///
        /// OFF BY DEFAULT, because it is the only setting in this mod that changes how a car
        /// goes round a corner. Everything else adds something the game was missing; this one
        /// replaces something it already had, and that should be asked for rather than arrive.
        ///
        /// A car that already had drift tuning when we found it keeps it and is left alone --
        /// somebody paid for that, and it is not ours to take off when this is switched back off.
        /// </summary>
        public bool DriftTyres = false;

        /// <summary>
        /// The cabin lights up when the headlights are on.
        ///
        /// Tied to the headlights rather than to the clock, which is both simpler and more
        /// faithful: a real dashboard lights with the side lights, which is why a tunnel at noon
        /// lights your instruments. The game already turns a player's headlights on when it gets
        /// dark, so this follows a decision that has already been made properly.
        /// </summary>
        public bool DashLight = true;

        // ---- crashes ----------------------------------------------------------

        /// <summary>
        /// A moment of slow motion when you hit something hard enough.
        ///
        /// THE ONE SETTING HERE THAT REACHES OUTSIDE YOUR CAR. Time scale is global and it is
        /// persistent -- it is the speed of the whole world and nothing puts it back on its own.
        /// See Driving.Crashes for how many different ways it is put back, and why there are
        /// that many.
        /// </summary>
        public bool CrashSlowMo = true;

        /// <summary>How fast you have to be going, in kilometres an hour, for it to count.</summary>
        public float CrashSlowMoSpeed = 100f;

        /// <summary>
        /// How much speed has to vanish in a single frame.
        ///
        /// Eight metres a second inside one frame is about fifty g, which braking cannot do and
        /// a scrape cannot do. This is what separates a crash from touching something: the game
        /// will tell you a kerb was hit, and a kerb is not what this is for.
        /// </summary>
        public float CrashSlowMoDrop = 8f;

        /// <summary>How slow, and for how long.</summary>
        public float CrashSlowMoScale = 0.40f;
        public float CrashSlowMoSeconds = 0.60f;

        /// <summary>
        /// Front-wheel-drive cars keep pulling when the handbrake is on.
        ///
        /// A handbrake is a REAR brake -- a cable to the back wheels, which is why a rear-driver
        /// spins on it. In a front-driver the driven wheels are the ones it does not touch, so
        /// the engine can still drag the car forward against a locked rear axle. The game brakes
        /// the car as a unit and the fronts give up with the rest of it.
        ///
        /// Done by pushing the car while both keys are held, NOT by editing its handling. See
        /// Driving.FrontWheels: handling is per model rather than per car, so turning the
        /// handbrake force down would weaken it on every other example of that model in the
        /// world for the rest of the session.
        /// </summary>
        public bool FwdHandbrake = true;

        /// <summary>
        /// How hard the fronts pull, in metres per second squared.
        ///
        /// A REAL UNIT RATHER THAN A MAGIC NUMBER: the game is handed this acceleration times
        /// the length of the frame, every frame, which accumulates to exactly this acceleration
        /// whatever the frame rate and whatever the car. 1.5 is a car dragging itself forward
        /// against a locked rear axle.
        ///
        /// It used to be multiplied by the car's mass as well, on the reasoning that the native
        /// takes an impulse and an impulse is mass times a velocity change. It does not -- it
        /// takes the velocity change and divides by mass itself -- so that asked for fifteen
        /// hundred times the intended pull and the car left like a rocket.
        /// </summary>
        public float FwdHandbrakePull = 1.5f;

        /// <summary>
        /// How fast it can drag the car, in metres a second, before the fronts give up.
        ///
        /// Eight is a fast jog. The point is a car that still creeps and still pivots with the
        /// handbrake up, not one that drives around on it -- a locked rear axle should always
        /// win eventually, and the force eases off as it approaches this rather than cutting.
        /// </summary>
        public float FwdHandbrakeMaxSpeed = 8.0f;

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

                s.ManualIgnition = ini.GetBool("Driving", "ManualIgnition", s.ManualIgnition);
                s.ExitHoldSeconds = ini.GetFloat("Driving", "ExitHoldSeconds", s.ExitHoldSeconds, 0.1f, 3f);
                s.ManualIgnitionMaxSpeed = ini.GetFloat("Driving", "ManualIgnitionMaxSpeed", s.ManualIgnitionMaxSpeed, 0f, 60f);
                s.ManualIgnitionAircraft = ini.GetBool("Driving", "ManualIgnitionAircraft", s.ManualIgnitionAircraft);
                s.StarterCranks = ini.GetBool("Driving", "StarterCranks", s.StarterCranks);
                s.RadioKeepsPlaying = ini.GetBool("Leaving", "RadioKeepsPlaying", s.RadioKeepsPlaying);
                s.LightsStayAsLeft = ini.GetBool("Leaving", "LightsStayAsLeft", s.LightsStayAsLeft);
                s.LeaveDoorOpen = ini.GetBool("Leaving", "LeaveDoorOpen", s.LeaveDoorOpen);

                s.Blinkers = ini.GetBool("Indicators", "Blinkers", s.Blinkers);
                s.BlinkerArmSeconds = ini.GetFloat("Indicators", "BlinkerArmSeconds", s.BlinkerArmSeconds, 0.1f, 5f);
                s.BlinkerCancelSeconds = ini.GetFloat("Indicators", "BlinkerCancelSeconds", s.BlinkerCancelSeconds, 0.1f, 10f);
                s.BlinkerOppositeSeconds = ini.GetFloat("Indicators", "BlinkerOppositeSeconds", s.BlinkerOppositeSeconds, 0f, 5f);
                s.BlinkerDeadzone = ini.GetFloat("Indicators", "BlinkerDeadzone", s.BlinkerDeadzone, 0.05f, 0.95f);
                s.BlinkerMinSpeed = ini.GetFloat("Indicators", "BlinkerMinSpeed", s.BlinkerMinSpeed, 0f, 20f);
                s.BlinkerInvert = ini.GetBool("Indicators", "BlinkerInvert", s.BlinkerInvert);
                s.Speedo = ini.GetBool("Speedo", "Speedo", s.Speedo);
                s.SpeedoUnits = ParseEnum(ini.GetString("Speedo", "SpeedoUnits", "Kph"), s.SpeedoUnits);
                s.SpeedoColour = ParseEnum(ini.GetString("Speedo", "SpeedoColour", "Amber"), s.SpeedoColour);
                s.SpeedoX = ini.GetFloat("Speedo", "SpeedoX", s.SpeedoX, 0f, 1f);
                s.SpeedoY = ini.GetFloat("Speedo", "SpeedoY", s.SpeedoY, 0f, 1f);
                s.SpeedoScale = ini.GetFloat("Speedo", "SpeedoScale", s.SpeedoScale, 0.4f, 3f);
                s.SpeedoOpacity = ini.GetFloat("Speedo", "SpeedoOpacity", s.SpeedoOpacity, 0.15f, 1f);
                s.SpeedoBackground = ini.GetBool("Speedo", "SpeedoBackground", s.SpeedoBackground);
                s.SpeedoGhost = ini.GetBool("Speedo", "SpeedoGhost", s.SpeedoGhost);
                s.SpeedoRevs = ini.GetBool("Speedo", "SpeedoRevs", s.SpeedoRevs);
                s.SpeedoGear = ini.GetBool("Speedo", "SpeedoGear", s.SpeedoGear);
                s.SpeedoOnlyInVehicle = ini.GetBool("Speedo", "SpeedoOnlyInVehicle", s.SpeedoOnlyInVehicle);
                s.SpeedoEngineIcon = ini.GetBool("Speedo", "SpeedoEngineIcon", s.SpeedoEngineIcon);
                s.SpeedoOilLight = ini.GetBool("Speedo", "SpeedoOilLight", s.SpeedoOilLight);

                s.DriftTyres = ini.GetBool("Driving", "DriftTyres", s.DriftTyres);
                s.DashLight = ini.GetBool("Driving", "DashLight", s.DashLight);

                s.CrashSlowMo = ini.GetBool("General", "CrashSlowMo", s.CrashSlowMo);
                s.CrashSlowMoSpeed = ini.GetFloat("General", "CrashSlowMoSpeed", s.CrashSlowMoSpeed, 10f, 400f);
                s.CrashSlowMoDrop = ini.GetFloat("General", "CrashSlowMoDrop", s.CrashSlowMoDrop, 1f, 40f);
                s.CrashSlowMoScale = ini.GetFloat("General", "CrashSlowMoScale", s.CrashSlowMoScale, 0.05f, 1f);
                s.CrashSlowMoSeconds = ini.GetFloat("General", "CrashSlowMoSeconds", s.CrashSlowMoSeconds, 0.1f, 4f);

                s.FwdHandbrake = ini.GetBool("Driving", "FwdHandbrake", s.FwdHandbrake);
                s.FwdHandbrakePull = ini.GetFloat("Driving", "FwdHandbrakePull", s.FwdHandbrakePull, 0f, 10f);
                s.FwdHandbrakeMaxSpeed = ini.GetFloat("Driving", "FwdHandbrakeMaxSpeed", s.FwdHandbrakeMaxSpeed, 0.5f, 30f);

                s.Seatbelt = ini.GetBool("Driving", "Seatbelt", s.Seatbelt);
                s.SeatbeltKey = ini.GetKey("Driving", "SeatbeltKey", s.SeatbeltKey);
                s.PadSeatbelt = ini.GetString("Driving", "PadSeatbelt", s.PadSeatbelt);
                s.SeatbeltSeconds = ini.GetFloat("Driving", "SeatbeltSeconds", s.SeatbeltSeconds, 0f, 10f);
                s.HandbrakeOnExit = ini.GetBool("Leaving", "HandbrakeOnExit", s.HandbrakeOnExit);
                s.Locking = ini.GetBool("Leaving", "Locking", s.Locking);
                s.LockKey = ini.GetKey("Leaving", "LockKey", s.LockKey);
                s.PadLock = ini.GetString("Leaving", "PadLock", s.PadLock);
                s.LockChirp = ini.GetBool("Leaving", "LockChirp", s.LockChirp);

                s.HazardKey = ini.GetKey("Indicators", "HazardKey", s.HazardKey);
                s.PadHazard = ini.GetString("Indicators", "PadHazard", s.PadHazard);
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
