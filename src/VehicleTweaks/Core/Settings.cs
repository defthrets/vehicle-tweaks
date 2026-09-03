using System;
using System.Globalization;
using System.Windows.Forms;

namespace VehicleTweaks.Core
{
    /// <summary>
    /// How the chauffeur drives.
    ///
    /// FOUR WORDS A PERSON WOULD USE, mapped in the Driving layer onto the game's own flags --
    /// whose list is six names of uneven usefulness, one of them called SometimesOvertakeTraffic.
    /// These are the ones worth choosing between.
    /// </summary>
    internal enum AutoStyle
    {
        Cautious,
        Normal,
        Brisk,
        Reckless
    }

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

        // ---- coming back to it ------------------------------------------------

        /// <summary>
        /// Your car is still there when you come back.
        ///
        /// GTA throws away vehicles nobody is looking at, which is the most everyday annoyance
        /// in the game and makes nonsense of everything else in this mod: a car left running,
        /// lit, locked and handbraked is not much use if it is deleted while your back is turned.
        ///
        /// EXACTLY ONE CAR is held, and taking a different one hands the previous back.
        /// Persistence tells the game a vehicle may never be cleaned up, and handing that out
        /// freely fills the world's budget with cars nobody is returning for.
        /// </summary>
        public bool KeepParked = true;

        /// <summary>A blip on it, because a car that is still there is no good if you cannot find it.</summary>
        public bool ParkedBlip = true;

        /// <summary>
        /// Each car remembers what it was playing.
        ///
        /// One of those things nobody notices until it is missing -- which in GTA it is, so the
        /// first ten seconds of every journey go on cycling back to the station you were on.
        /// This session only: remembering across sessions means a file, and this mod writes
        /// nothing but its log.
        /// </summary>
        public bool RememberStations = true;

        // ---- the autopilot ----------------------------------------------------

        /// <summary>
        /// Cruise control: holds the speed you set it at.
        ///
        /// A CAP RATHER THAN A THROTTLE. MaxSpeed tells the game this car may not exceed a
        /// speed, which is a far smaller instrument than driving the accelerator ourselves --
        /// steering and braking stay entirely yours, and the worst a bug here can do is limit a
        /// car rather than drive one.
        /// </summary>
        public bool Cruise = true;

        public Keys CruiseKey = Keys.U;
        public string PadCruise = "Off";

        /// <summary>
        /// Self driving, using the task every ambient driver in the city is already running.
        ///
        /// Nothing here steers. It obeys lights, overtakes and gives way exactly as traffic
        /// does, because it IS traffic's own task.
        ///
        /// Off by default: it is the most control this mod ever takes, and taking the wheel out
        /// of somebody's hands is a thing to be asked for rather than switched on by an update.
        /// </summary>
        public bool AutoDrive = false;

        public Keys AutoDriveKey = Keys.O;
        public string PadAutoDrive = "Off";

        /// <summary>How fast it drives, in kilometres an hour.</summary>
        public float AutoDriveSpeed = 60f;

        public AutoStyle AutoDriveStyle = AutoStyle.Normal;

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
        /// Where the warning lamps sit, which is NOT wherever the speedo is.
        ///
        /// They started life bolted to the end of the speed readout and that was wrong: a
        /// readout is one object you glance at continuously, and a warning lamp is meant to
        /// catch your eye precisely by not being part of what you were already looking at.
        /// Above and to the left of the speedo by default, close enough to find and plainly its
        /// own thing.
        /// </summary>
        public float SpeedoLampsX = 0.268f;
        public float SpeedoLampsY = 0.886f;

        /// <summary>
        /// Drift tyres, the ones GTA Online actually has.
        ///
        /// A SLIDER, 0 TO 1, AND NOT A LIST OF FOUR WORDS. Off, Light, Medium and Heavy were
        /// four points on something that is plainly continuous, and picking between them is not
        /// the same as finding the one that feels right. 0.20 is a light default: enough to feel
        /// on a roundabout, not enough to be a nuisance on a motorway.
        ///
        /// TWO OF ROCKSTAR'S OWN UNDERNEATH IT, because one of them will not go on everything.
        /// SET_DRIFT_TYRES is the Drift Races tuning and it is gated to the cars that were given
        /// it, which is most of why "drift mode on any car" needed a second answer. The friction
        /// override is the second answer: it takes a float, it goes on anything, and it is what
        /// makes the slider a slider rather than three steps wearing a decimal point.
        ///
        /// OFF BY DEFAULT, because it is the only setting in this mod that changes how a car
        /// goes round a corner. Everything else adds something the game was missing; this one
        /// replaces something it already had, and that should be asked for rather than arrive.
        ///
        /// A car that already had drift tuning when we found it keeps it and is left alone --
        /// somebody paid for that, and it is not ours to take off when this is switched back off.
        /// </summary>
        public float DriftAmount = 0.20f;

        /// <summary>
        /// The engine keeps pulling while the car is sideways.
        ///
        /// GTA bogs a car down the moment it stops pointing where it is going, which is what
        /// makes long drifts collapse: the back comes out, the power falls away underneath you,
        /// and the slide dies of its own accord rather than because you ended it.
        ///
        /// Done with EnginePowerMultiplier, which is per CAR and per moment -- not handling
        /// data, which is per model and would change every other example of that car in the
        /// world for the session.
        /// </summary>
        public bool DriftPower = true;

        /// <summary>
        /// How much power it finds at full slide. 1.0 is none at all.
        ///
        /// Reached gradually as the angle opens up rather than switched on at a line: a car that
        /// suddenly found more power at twelve degrees would be harder to hold than one that
        /// never found any.
        /// </summary>
        public float DriftPowerBoost = 1.4f;

        // ---- the drivetrain ---------------------------------------------------

        /// <summary>
        /// You change gear. Nothing else does.
        ///
        /// OFF, AND IT IS THE ONLY THING HERE THAT HAS TO BE. Everything else in this mod is a
        /// small correction to a car that still drives the way you expect; this changes what the
        /// car IS. Get in with it on and you will pull away in whatever gear you left it in.
        /// That is a choice, not an improvement.
        ///
        /// Below walking pace it writes nothing at all, so reverse still works exactly as it
        /// did -- and the gear goes quietly back to first, so pulling away is never in fifth
        /// because that is where you happened to stop.
        /// </summary>
        public bool ManualBox = false;

        /// <summary>
        /// The pad buttons, up and down, by GTA control name.
        ///
        /// A AND X, WHICH ARE NOT FREE BUTTONS. There is no spare button on a pad -- every face
        /// button, shoulder and stick is already a gameplay action -- so these share with
        /// whatever the game does with them in a car, and a shift will do both things. They are
        /// settings for exactly that reason, and paddles on the bumpers (FrontendLb, FrontendRb)
        /// are what a real sequential box uses anyway.
        ///
        /// The log names which physical button each vehicle action sits on, once, out of the
        /// game's own glyph table, so the collision is something you can read rather than
        /// something to discover at a junction.
        /// </summary>
        public string ManualUpPad = "FrontendAccept";
        public string ManualDownPad = "FrontendX";

        /// <summary>
        /// And the same two on a keyboard.
        ///
        /// SHIFT AND CONTROL, the pair every driving game has used for this since before they
        /// had pads. Left Control is the game's duck-in-a-vehicle, which is a collision worth
        /// naming and not worth avoiding; both are settings.
        /// </summary>
        public Keys ManualUpKey = Keys.LShiftKey;
        public Keys ManualDownKey = Keys.LControlKey;

        /// <summary>
        /// First and second are held very slightly longer under power.
        ///
        /// GTA'S LOW GEARS ARE SHORT AND IT LEAVES THEM EARLY, so pulling away is two flat
        /// little shifts and then you are in third at walking pace with nothing to hear. Half a
        /// second more in each lets the engine actually come up before it changes.
        ///
        /// AND THAT IS ALL IT IS. The shifting is otherwise entirely the game's -- nothing is
        /// blocked going down, nothing is held through a slide, nothing is held through a
        /// wheelspin. A gearbox you are aware of is the thing nobody wants, and the version
        /// before this one was one.
        /// </summary>
        public bool HoldLowGears = true;

        /// <summary>
        /// How much longer, in seconds, measured from the moment the throttle goes down.
        ///
        /// FROM THE THROTTLE RATHER THAN FROM THE GEAR, so pulling away from a standstill gets
        /// the whole window instead of having quietly spent it sitting at the lights in first.
        ///
        /// Half a second is meant to be felt and not noticed. Past about one it stops being a
        /// longer pull and starts being a gearbox with an opinion.
        /// </summary>
        public float HoldLowGearsSeconds = 0.5f;

        /// <summary>
        /// The rev counter reads the wheels, so a lit tyre shows on it and can be heard.
        ///
        /// GTA'S REVS FOLLOW ROAD SPEED, so the one moment the engine is working hardest --
        /// tyres spinning, car going nowhere -- is the one moment the needle says nothing. The
        /// engine note comes off the same value, so this is as much about what a burnout SOUNDS
        /// like as what the tacho shows.
        ///
        /// Only ever upwards, and never held: the instant the tyres grip, this stops writing and
        /// the engine is the game's again on the next frame.
        /// </summary>
        public bool RevsFollowWheels = true;

        /// <summary>
        /// How far up the range a fully lit tyre pushes the revs, as a fraction of the whole.
        ///
        /// 0.5 is half the rev range added on top of wherever the engine already was, which is
        /// enough to reach the limiter from the middle without pinning it there from idle.
        /// </summary>
        public float RevsWheelspinGain = 0.5f;

        /// <summary>
        /// How much faster the wheels have to turn than the road goes by, in metres a second,
        /// before it counts as spinning.
        ///
        /// MEASURED RATHER THAN ASSUMED FROM THE THROTTLE. Wheel speed against road speed is the
        /// tyres turning faster than the ground is passing, which is what wheelspin IS; a
        /// throttle position says nothing about whether they have actually let go.
        ///
        /// ONE NUMBER, TWO USES, deliberately: it is where the gear starts being held and it is
        /// where the revs reach their full extra. Two settings for the same physical fact would
        /// be two numbers that can disagree about whether a tyre is spinning.
        /// </summary>
        public float WheelspinSlip = 3.0f;

        /// <summary>
        /// More steering lock while the car is sideways, so a slide can be caught.
        ///
        /// THIS IS HOW A REAL DRIFT CAR IS BUILT. The single most common modification on one is
        /// extended steering angle -- knuckles, spacers, whatever it takes -- for exactly this
        /// reason: catching a slide means winding on more opposite lock than the car came with,
        /// and once you run out of lock the car is going wherever it was already going.
        ///
        /// The stock limit is what makes GTA drifts feel like they end by themselves. It is per
        /// WHEEL and per car, not handling data, so it is the same small safe instrument as the
        /// power above.
        /// </summary>
        public bool CounterSteer = true;

        /// <summary>
        /// How much more lock, at full slide. 1.0 is the car as it came.
        ///
        /// Reached on the same ramp as the power, so both arrive as the angle opens rather than
        /// switching on at a line -- a car whose steering suddenly got quicker mid-corner would
        /// be harder to hold, not easier.
        /// </summary>
        public float CounterSteerLock = 1.6f;

        /// <summary>
        /// How far sideways counts as a slide, in degrees.
        ///
        /// The angle between where the car points and where it is actually travelling, which is
        /// what a drift IS. Twelve is past what a fast corner asks for and short of what a
        /// deliberate slide gives you.
        /// </summary>
        public float DriftAngle = 12f;

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
        /// <remarks>
        /// EIGHT, AFTER TWO GOES AT IT. One and a half was picked while the version before it
        /// was still fifteen hundred times too strong -- a timid number chosen in the shadow of
        /// a rocket -- and four was still not enough to see. The handbrake is braking the car
        /// the whole time this is pushing it, so the number has to be a real one before anything
        /// visible happens. The point is a car that plainly strains against the brake.
        /// </remarks>
        public float FwdHandbrakePull = 8.0f;

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
                s.KeepParked = ini.GetBool("Leaving", "KeepParked", s.KeepParked);
                s.ParkedBlip = ini.GetBool("Leaving", "ParkedBlip", s.ParkedBlip);
                s.RememberStations = ini.GetBool("Leaving", "RememberStations", s.RememberStations);

                s.Cruise = ini.GetBool("Autopilot", "Cruise", s.Cruise);
                s.CruiseKey = ini.GetKey("Autopilot", "CruiseKey", s.CruiseKey);
                s.PadCruise = ini.GetString("Autopilot", "PadCruise", s.PadCruise);
                s.AutoDrive = ini.GetBool("Autopilot", "AutoDrive", s.AutoDrive);
                s.AutoDriveKey = ini.GetKey("Autopilot", "AutoDriveKey", s.AutoDriveKey);
                s.PadAutoDrive = ini.GetString("Autopilot", "PadAutoDrive", s.PadAutoDrive);
                s.AutoDriveSpeed = ini.GetFloat("Autopilot", "AutoDriveSpeed", s.AutoDriveSpeed, 5f, 200f);
                s.AutoDriveStyle = ParseEnum(ini.GetString("Autopilot", "AutoDriveStyle", "Normal"), s.AutoDriveStyle);

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
                s.SpeedoLampsX = ini.GetFloat("Speedo", "SpeedoLampsX", s.SpeedoLampsX, 0f, 1f);
                s.SpeedoLampsY = ini.GetFloat("Speedo", "SpeedoLampsY", s.SpeedoLampsY, 0f, 1f);

                // THIS SETTING HAS BEEN THREE THINGS NOW: a yes/no, then four words, and now a
                // number. Anybody's ini is written in whichever of those their last build used,
                // and each of them would fail to parse as the next -- warning once and quietly
                // resetting a setting somebody had chosen. So every spelling it has ever had is
                // still understood, and the new name is looked for first.
                s.DriftAmount = Drift(ini, s.DriftAmount);

                s.DriftPower = ini.GetBool("Driving", "DriftPower", s.DriftPower);
                s.DriftPowerBoost = ini.GetFloat("Driving", "DriftPowerBoost", s.DriftPowerBoost, 1f, 3f);
                s.DriftAngle = ini.GetFloat("Driving", "DriftAngle", s.DriftAngle, 3f, 60f);
                s.ManualBox = ini.GetBool("Driving", "ManualBox", s.ManualBox);
                s.ManualUpPad = ini.GetString("Driving", "ManualUpPad", s.ManualUpPad);
                s.ManualDownPad = ini.GetString("Driving", "ManualDownPad", s.ManualDownPad);
                s.ManualUpKey = ini.GetKey("Driving", "ManualUpKey", s.ManualUpKey);
                s.ManualDownKey = ini.GetKey("Driving", "ManualDownKey", s.ManualDownKey);

                s.HoldLowGears = ini.GetBool("Driving", "HoldLowGears", s.HoldLowGears);
                s.HoldLowGearsSeconds = ini.GetFloat("Driving", "HoldLowGearsSeconds", s.HoldLowGearsSeconds, 0.1f, 2f);
                s.RevsFollowWheels = ini.GetBool("Driving", "RevsFollowWheels", s.RevsFollowWheels);
                s.RevsWheelspinGain = ini.GetFloat("Driving", "RevsWheelspinGain", s.RevsWheelspinGain, 0f, 1f);
                s.WheelspinSlip = ini.GetFloat("Driving", "WheelspinSlip", s.WheelspinSlip, 0.5f, 20f);
                s.CounterSteer = ini.GetBool("Driving", "CounterSteer", s.CounterSteer);
                s.CounterSteerLock = ini.GetFloat("Driving", "CounterSteerLock", s.CounterSteerLock, 1f, 3f);
                s.DashLight = ini.GetBool("Driving", "DashLight", s.DashLight);

                s.CrashSlowMo = ini.GetBool("General", "CrashSlowMo", s.CrashSlowMo);
                s.CrashSlowMoSpeed = ini.GetFloat("General", "CrashSlowMoSpeed", s.CrashSlowMoSpeed, 10f, 400f);
                s.CrashSlowMoDrop = ini.GetFloat("General", "CrashSlowMoDrop", s.CrashSlowMoDrop, 1f, 40f);
                s.CrashSlowMoScale = ini.GetFloat("General", "CrashSlowMoScale", s.CrashSlowMoScale, 0.05f, 1f);
                s.CrashSlowMoSeconds = ini.GetFloat("General", "CrashSlowMoSeconds", s.CrashSlowMoSeconds, 0.1f, 4f);

                s.FwdHandbrake = ini.GetBool("Driving", "FwdHandbrake", s.FwdHandbrake);
                s.FwdHandbrakePull = ini.GetFloat("Driving", "FwdHandbrakePull", s.FwdHandbrakePull, 0f, 15f);
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

        /// <summary>
        /// The drift amount, in whichever of its three spellings the file happens to use.
        /// </summary>
        private static float Drift(IniFile ini, float fallback)
        {
            // The name it has now.
            var raw = ini.GetString("Driving", "DriftAmount", null);

            // The name it had when it was a yes/no and when it was four words.
            if (string.IsNullOrEmpty(raw)) raw = ini.GetString("Driving", "DriftTyres", null);

            if (string.IsNullOrEmpty(raw)) return fallback;

            raw = raw.Trim();

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            {
                if (n < 0f) return 0f;
                return n > 1f ? 1f : n;
            }

            switch (raw.ToLowerInvariant())
            {
                case "off":
                case "false":
                case "no": return 0f;

                case "light": return 0.20f;

                case "true":
                case "medium": return 0.50f;

                case "heavy": return 0.85f;
            }

            Log.Warn("[Driving] the drift amount reads '" + raw + "', which is neither a number " +
                     "between 0 and 1 nor a word this understands - using " + fallback + ".");

            return fallback;
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
