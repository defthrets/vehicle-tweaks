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
        /// A CHORD, because there is no spare pad button -- and SHARED BY ALL SEVEN of the mod's
        /// pad chords, so one button held is the way into the panel, the hazards, the locks, the
        /// seatbelt, cruise, the autopilot and the cabin light. One thing to learn, one place to
        /// change it.
        ///
        /// R3, BECAUSE NOTHING IS FREE AND THIS COSTS LEAST. There is no unbound button on a pad
        /// in this game. LB is the weapon wheel, RB is the handbrake, L3 is the horn, VIEW changes
        /// the camera, and every D-pad direction is the phone, the character wheel, the radio
        /// wheel or a detonator. So the modifier is not chosen by being free, it is chosen by what
        /// it costs to HOLD:
        ///
        ///   LB   summons the weapon wheel, which is then worked with the D-pad -- the chord and
        ///        the wheel are the same gesture, and the chord fires while you pick a gun.
        ///   RB   pulls the handbrake at speed. Not a menu button.
        ///   L3   sounds the horn in a car and toggles stealth on foot, which STAYS toggled.
        ///   VIEW cycles the camera on every open, and a menu that moves the camera under itself
        ///        is worse than one that is awkward to reach.
        ///   R3   looks behind. The camera swings while it is held and comes back when it is let
        ///        go: no wheel, no HUD, no sound, nothing left changed, nothing taken away.
        ///
        /// It is also the only one your other thumb can hold while the D-pad is being pressed.
        ///
        /// "Off" on PadOpen means the pad cannot open the panel. "None" on PadModifier means the
        /// single button does it on its own.
        /// </summary>
        public string PadOpen = "PhoneLeft";
        public string PadModifier = "ScriptRS";

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
        /// Killing the ignition takes the lights with it, and they stay off when you get out.
        ///
        /// BECAUSE THAT IS WHAT TURNING A CAR OFF IS. Nobody has ever switched off an engine and
        /// left the headlights burning -- the key going round is one gesture that stops the
        /// whole car, and a car sitting there dead with its lights still on is not a car
        /// somebody has parked, it is a car somebody has abandoned mid-thought.
        ///
        /// It only applies to stopping the engine DELIBERATELY, by holding the exit key. A tap
        /// leaves the car exactly as it stands, lights included, which is the other half of the
        /// same idea: what you did not switch off stays on.
        ///
        /// The lights come back the moment the engine does. It is a scripted override while it
        /// lasts -- the one kind of state that outlives us if it is left -- so it is handed to
        /// the same bookkeeping that already looks after abandoned cars, and lifted when you get
        /// back in.
        /// </summary>
        public bool LightsOffWithEngine = true;

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

        // ---- tuning -----------------------------------------------------------

        /// <summary>
        /// The top end, as a multiplier on what the car came with.
        ///
        /// PER CAR AND PER MOMENT, not handling data. HandlingData is per MODEL and permanent for
        /// the session: write to it and every other example of that car in the world changes and
        /// stays changed until the game restarts. EnginePowerMultiplier is a small safe
        /// instrument aimed at the car you are in, and it is given back when you get out.
        ///
        /// POWER IS THE TOP END. It is what the engine can do once it is already spinning, so
        /// this is the number that moves the far end of the speedo rather than what happens when
        /// the lights go green.
        ///
        /// The slide compensation on the GRIP page MULTIPLIES this rather than replacing it: 1.5
        /// here and another 1.4 while sideways is 2.1, which is what both settings said.
        /// </summary>
        public float PowerMultiplier = 1.0f;

        /// <summary>
        /// The low end, as a multiplier on what the car came with.
        ///
        /// TORQUE IS WHAT YOU FEEL, and it is the half most people mean when they say a car needs
        /// more power. It is what is available down low -- pulling away, coming out of a corner,
        /// getting the back out on purpose -- where the engine is not spinning fast enough for
        /// power to be the answer.
        ///
        /// Separate from the one above because the game keeps them separate, and because they do
        /// genuinely different things: torque without power is a car that leaps off the line and
        /// runs out of legs, and power without torque is one that does nothing until it is
        /// already moving.
        /// </summary>
        public float TorqueMultiplier = 1.0f;

        /// <summary>
        /// How far the front wheels turn, as a multiplier on what the car came with.
        ///
        /// THE BASELINE, WHICH THE COUNTER-STEER SETTING MULTIPLIES. That one gives extra lock as
        /// the car goes sideways and takes it away again as it straightens; this one is there all
        /// the time, in the car park and on the motorway alike. A drift car is built with both:
        /// more lock at rest, and it does not run out when the angle opens up.
        ///
        /// It asks the game which wheels steer rather than assuming the front two.
        /// </summary>
        public float SteeringLock = 1.0f;

        /// <summary>
        /// The tyres cannot be burst, and the wheels cannot be knocked off.
        ///
        /// FLAGS, NOT MULTIPLIERS, and that difference matters when they are given back. A
        /// multiplier of one IS the standard value, so writing it costs nothing. These are
        /// normally TRUE, so anything that set them back to true on every car it touched would be
        /// re-arming tyres some other mod had deliberately disarmed. Only what this turned off
        /// gets turned back on.
        /// </summary>
        public bool TyresNeverBurst = false;
        public bool WheelsNeverBreak = false;

        /// <summary>
        /// Softer springs, using the game's own reduced suspension force.
        ///
        /// THE GAME'S SWITCH, NOT A NUMBER OF MINE. There is no suspension stiffness to turn
        /// down through any interface a script can reach; there is one flag that says "use the
        /// reduced force", and this is it. It is what the game itself puts on a car it wants
        /// sitting closer to the road.
        /// </summary>
        public bool SoftSuspension = false;

        /// <summary>
        /// The wheels keep their shape, however hard they are hit.
        ///
        /// A BENT WHEEL IS PERMANENT AND IT STEERS. Once a kerb has folded one over, the car
        /// pulls for the rest of its life and there is no repair short of a full one -- which
        /// is a lot of consequence for a mistake that lasted a tenth of a second. Separate from
        /// the wheel breaking off, which is the same event several stages further on.
        /// </summary>
        public bool WheelsNeverDeform = false;

        /// <summary>
        /// How high the hydraulics sit, on the cars that have them.
        ///
        /// NOUGHT MEANS LEAVE IT ALONE, which is why the range starts there rather than at a
        /// height. Anything above it is a height being held, and the value the car already had
        /// is read once and put back afterwards -- a car whose hydraulics were parked high and
        /// came back sitting on the floor would be this mod deciding something nobody asked it
        /// to.
        ///
        /// Applied to everything, and it only does anything on a car with hydraulics fitted.
        /// Filtering for Benny's cars would mean keeping a list of them, and the game already
        /// knows which is which.
        /// </summary>
        public float HydraulicRaise = 0f;

        // ---- stance -----------------------------------------------------------

        /// <summary>
        /// How far the tops of the wheels lean, in degrees, front and rear.
        ///
        /// THROUGH THE BONES, NOT THROUGH MEMORY, which is the whole reason this exists here at
        /// all. Camber IS the Y rotation of a wheel bone in the car's local space -- not an
        /// approximation of it -- and that rotation is settable through the ordinary API. The
        /// other way to do it is what VStancer does: find each wheel's structure in memory and
        /// write floats at fixed byte offsets, which is why that mod needs rebuilding for every
        /// game update and needed its own Enhanced version. An offset is only correct for the
        /// executable it was measured against; a property is not an address.
        ///
        /// NOUGHT IS THE CAR AS IT CAME. Which direction counts as leaning IN depends on a sign
        /// convention I have not been able to check from outside the game -- if it leans the
        /// wrong way, use the other sign. It is a slider.
        /// </summary>
        public float CamberFront = 0f;
        public float CamberRear = 0f;

        /// <summary>
        /// How much further apart the wheels sit, in metres, front and rear.
        ///
        /// The X offset of the wheel bones, mirrored across the axle -- one slider has to become
        /// two opposite numbers, or a wider track is one wheel out and one wheel in.
        /// </summary>
        public float TrackFront = 0f;
        public float TrackRear = 0f;

        /// <summary>
        /// How far the wheels move up or down in the arches, in metres, front and rear.
        ///
        /// Not the same thing as softer springs further up: that changes how the car behaves over
        /// a bump, and this changes where the wheel sits. A car can want either, or both.
        /// </summary>
        public float HeightFront = 0f;
        public float HeightRear = 0f;

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

        /// <summary>
        /// And how much torque it finds, at full slide.
        ///
        /// THE HALF THAT ACTUALLY HOLDS A SLIDE. Power is the top end -- what the engine does once
        /// it is already spinning -- and a car that has just been thrown sideways is not there:
        /// the revs have dropped into the middle of the range and what it needs is pull, not
        /// speed. Boosting power alone was asking the engine for help in the one place a drifting
        /// car never is.
        ///
        /// ON THE SAME RAMP AS THE POWER, deliberately. They are not two questions -- the game
        /// bogs a sideways car and takes both away at once -- so giving them separate arrival
        /// angles would invent a distinction the problem does not have.
        ///
        /// It multiplies the TorqueMultiplier baseline rather than replacing it, the same way the
        /// power boost does: 1.2 set on the TUNING page and another 1.4 found while sideways is
        /// 1.68, which is what both settings said.
        /// </summary>
        public float DriftTorqueBoost = 1.4f;

        // ---- per gear -----------------------------------------------------------

        /// <summary>
        /// Torque, gear by gear, as a multiplier on top of everything else.
        ///
        /// A CHART RATHER THAN A NUMBER, because one number is not what anybody means when they
        /// say a car needs more torque. They mean it needs more in SECOND -- the gear a slide is
        /// held in, the gear you leave a junction in -- and no more in fifth, where extra torque
        /// is a car that will not settle at speed. Six bars, one per gear; seventh and up use the
        /// sixth, because six is what nearly everything you would drift has and a chart of eight
        /// would be two bars of nothing.
        ///
        /// MULTIPLIES, LIKE EVERYTHING ELSE THAT LANDS ON THIS FIELD. The TUNING torque is the
        /// baseline, this is per gear on top, and the slide compensation is on top of that. One
        /// place adds it all up so that none of them can fight.
        /// </summary>
        public float[] GearTorque = { 1f, 1f, 1f, 1f, 1f, 1f };

        /// <summary>
        /// How much slidier the tyres are, gear by gear. Nought is no change.
        ///
        /// ADDED TO THE DRIFT SLIDER, NOT REPLACING IT. The GRIP page's drift amount is the
        /// baseline for every gear; this is extra on top, per gear, so a car can be planted in
        /// fourth and loose in second without either setting knowing about the other. The sum is
        /// clamped to the same nought-to-one the slider uses.
        /// </summary>
        public float[] GearSlide = { 0f, 0f, 0f, 0f, 0f, 0f };

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
        /// How far sideways you have to be, in degrees, before all of that lock has arrived.
        ///
        /// THE HALF THAT USED TO BE HARD-CODED. How MUCH extra lock was a setting from the
        /// start; where it turns up was three times the slide angle and nothing else, which is
        /// the part you actually feel. Arriving too early is a car that goes vague as soon as it
        /// steps out; too late is running out of steering in the only moment it was needed.
        ///
        /// Thirty degrees is a proper slide, held. Anything under about fifteen means the extra
        /// lock is already all there by the time you have noticed the back move.
        ///
        /// SEPARATE FROM THE POWER'S RAMP, deliberately. The power is undoing something the game
        /// does to a sideways car, so it follows the game's problem; the lock is giving you
        /// something, so where it arrives is taste.
        /// </summary>
        public float CounterSteerFull = 30f;

        /// <summary>
        /// How far sideways counts as a slide, in degrees.
        ///
        /// The angle between where the car points and where it is actually travelling, which is
        /// what a drift IS. Twelve is past what a fast corner asks for and short of what a
        /// deliberate slide gives you.
        /// </summary>
        public float DriftAngle = 12f;

        /// <summary>
        /// The cabin light, on a switch of its own.
        ///
        /// IT USED TO FOLLOW THE HEADLIGHTS, on the argument that a real dashboard lights with
        /// the side lights. That is true of the instruments and it is not true of the cabin
        /// light, which is the one thing in a car that is explicitly NOT automatic -- every car
        /// ever built has it on its own switch, because the point of it is that you decide when
        /// the inside of the car is lit. Following the headlights meant it came on for every
        /// tunnel and every dusk, which is exactly when you can see fine and do not want the
        /// glare on the glass.
        ///
        /// This is the feature, not the state: off here means the key does nothing.
        /// </summary>
        public bool DashLight = true;

        /// <summary>
        /// He drives everything the way he drives a lowrider: sat back, arm through the window.
        ///
        /// THE GAME ALREADY HAS THIS POSE and only ever gives it to you in the cars Benny built.
        /// It is a seat context, chosen per vehicle in the game's own layout data, and a script
        /// can ask for a different one -- so this is not a new animation bolted on, it is the one
        /// that already exists applied to the car you are actually in.
        ///
        /// EVERY VEHICLE. No lowrider check, no convertible check, no cars-only filter -- one
        /// whose seat layout has no such clipset simply ignores the context and sits him
        /// normally, so a filter would not be preventing a broken pose, it would be preventing
        /// an attempt.
        ///
        /// OFF, because it changes how the character looks in every car rather than how any car
        /// behaves. That is a taste, and tastes get asked for.
        /// </summary>
        public bool LowriderPose = false;

        /// <summary>
        /// The seat context to ask for, by name.
        ///
        /// A SETTING RATHER THAN A CONSTANT, AND DELIBERATELY SO. Everything else in this mod was
        /// checked against SHVDN by reflection before it was relied on. A context cannot be: it is
        /// a NAME that gets hashed at runtime, and there is no list to check it against from
        /// outside the running game. Baking a guess into the build would mean a rebuild to try the
        /// next candidate; here it is one line and a reload.
        ///
        /// MINI_LOWRIDER is the first candidate. If the pose does not change, the others worth
        /// trying are LOWRIDER, MINI_LOWRIDER_ARM and MINI. The log prints the name AND the hash
        /// it generated, because a context the game does not have looks exactly like one that was
        /// never applied.
        /// </summary>
        /// <summary>
        /// The animation played over his top half, by dictionary and clip.
        ///
        /// PLAYED, NOT SELECTED, and that is the second attempt at this. The first asked the game
        /// to change his SEAT CONTEXT, which is how the game itself picks a sitting animation --
        /// and the log proved it does nothing: the seat clipset came back unchanged on every car,
        /// every time. The documented contexts are mission-specific things like
        /// MISSFBI5_TREVOR_DRIVING, and there is no lowrider among them.
        ///
        /// NAMES THAT CAN BE CHECKED, which is why this route is better than the last one.
        /// DOES_ANIM_DICT_EXIST answers for a dictionary and GET_ANIM_DURATION answers for a clip,
        /// so a wrong name here is a line in the log rather than a feature that quietly does
        /// nothing. Turn LowriderProbe on and the log lists what this build actually has.
        /// </summary>
        public string LowriderAnimDict = "veh@low@front_ds@base";
        public string LowriderAnimClip = "sit";

        /// <summary>
        /// Ask the game which animation dictionaries and clips it has, and write them to the log.
        ///
        /// OFF, BECAUSE IT HAS ALREADY ANSWERED. It was on while the names above were a guess, and
        /// one drive turned them into a fact: 32 dictionaries exist on this build and
        /// veh@low@front_ds@base has a "sit" clip of 6.33 seconds. There is nothing left to ask.
        ///
        /// It stays because the question comes back. A game update, a different build, or a name
        /// that stops working is answered by turning this on and driving once -- and the seat line
        /// it prints names the clipset of whatever car you are in, which is how a real lowrider
        /// would tell us what a real lowrider uses.
        /// </summary>
        public bool LowriderProbe = false;

        /// <summary>
        /// The driver's window goes down with the pose.
        ///
        /// AN ARM HANGING THROUGH GLASS IS WORSE THAN NO ARM. It is most of why the pose looks
        /// right in a lowrider and wrong everywhere else -- those cars are driven with the window
        /// down. Only our own window is wound back up when the pose comes off.
        /// </summary>
        public bool LowriderWindow = true;

        /// <summary>
        /// The switch, on a keyboard and on a pad.
        ///
        /// I FOR INTERIOR, AFTER K TURNED OUT TO BE THE SEATBELT'S. This shipped on K without
        /// anybody checking what else was on K, and the seatbelt had been there for weeks -- so
        /// one press did both, which is the kind of collision that reads as one of the two
        /// features being broken. L is the headlights, which is the thing this was separated
        /// from; H is the horn and R is the radio.
        ///
        /// AND NOTHING ON THE PAD, which is an honest shortage rather than an oversight. There
        /// are four directions on a D-pad and five things that want one: the panel, the hazards,
        /// the seatbelt, the lock and this. This is the newest of them and the one that matters
        /// least, so it is the one that goes without. Name any control here to give it one back
        /// -- something else will have to lose it.
        /// </summary>
        public Keys DashLightKey = Keys.I;
        public string PadDashLight = "Off";

        // ---- crashes ----------------------------------------------------------

        /// <summary>
        /// A moment of slow motion when you hit something hard enough.
        ///
        /// OFF, AND IT IS THE ONE SETTING HERE THAT REACHES OUTSIDE YOUR CAR. Time scale is
        /// global and it is persistent -- it is the speed of the whole world, and nothing puts
        /// it back on its own. See Driving.Crashes for how many different ways it is put back
        /// and why there are that many.
        ///
        /// The rest of this mod is corrections to a car. This one takes the game away from you
        /// for six tenths of a second at the moment you were most likely trying to catch
        /// something, which is a taste, not a fix -- and a taste that reaches the whole world
        /// should be asked for rather than assumed.
        /// </summary>
        // ---- the spawner -------------------------------------------------------

        /// <summary>
        /// Every vehicle in the game, browsable, with the highlighted one stood in front of you.
        ///
        /// THE LIST IS THE GAME'S OWN -- 843 entries covering the base game and every DLC and
        /// multiplayer pack, each checked against the model actually being installed here. There
        /// is no list to maintain and nothing to go stale.
        /// </summary>
        public bool Spawner = true;

        /// <summary>
        /// The key it opens on. There is a row on the panel too, which is how a pad reaches it.
        ///
        /// F7 BECAUSE F8 IS THE PANEL, and the two belong beside each other. No pad binding of its
        /// own: the D-pad has four directions and they are all spoken for, so the pad route is the
        /// panel row rather than a chord nobody could guess.
        /// </summary>
        public Keys SpawnerKey = Keys.F7;

        public bool CrashSlowMo = false;

        // ---- repairs -----------------------------------------------------------

        /// <summary>
        /// The car you are driving puts itself back together every few minutes.
        ///
        /// OFF, LIKE EVERYTHING HERE THAT REMOVES A CONSEQUENCE rather than adding one. The rest
        /// of this mod corrects a car that still behaves the way you expect; this undoes the
        /// last few minutes of your driving. That is a choice about how the game should treat
        /// you, and it should be made deliberately rather than found.
        ///
        /// A wreck is left as a wreck: repairing a dead car does not repair it, it resurrects
        /// it, and nobody asking for their scratches buffed out is asking for that.
        /// </summary>
        public bool Repairs = false;

        /// <summary>
        /// How often, in minutes.
        ///
        /// THE CLOCK RESTARTS WHETHER OR NOT ANYTHING WAS REPAIRED, which is the whole difference
        /// between this and invincibility. Left to run on, an undamaged car would sit with its
        /// timer already expired and fix the next scrape a frame after it happened. Damage is
        /// meant to last up to the interval -- that IS the interval.
        /// </summary>
        public float RepairMinutes = 5f;

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
        public string PadSeatbelt = "PhoneRight";

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
        public string PadLock = "PhoneUp";

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
                s.LightsOffWithEngine = ini.GetBool("Driving", "LightsOffWithEngine", s.LightsOffWithEngine);
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

                s.PowerMultiplier = ini.GetFloat("Driving", "PowerMultiplier", s.PowerMultiplier, 0.25f, 3f);
                s.TorqueMultiplier = ini.GetFloat("Driving", "TorqueMultiplier", s.TorqueMultiplier, 0.25f, 3f);
                s.SteeringLock = ini.GetFloat("Driving", "SteeringLock", s.SteeringLock, 1f, 2.5f);
                s.TyresNeverBurst = ini.GetBool("Driving", "TyresNeverBurst", s.TyresNeverBurst);
                s.WheelsNeverBreak = ini.GetBool("Driving", "WheelsNeverBreak", s.WheelsNeverBreak);
                s.SoftSuspension = ini.GetBool("Driving", "SoftSuspension", s.SoftSuspension);
                s.WheelsNeverDeform = ini.GetBool("Driving", "WheelsNeverDeform", s.WheelsNeverDeform);
                s.HydraulicRaise = ini.GetFloat("Driving", "HydraulicRaise", s.HydraulicRaise, 0f, 1f);

                s.CamberFront = ini.GetFloat("Driving", "CamberFront", s.CamberFront, -20f, 20f);
                s.CamberRear = ini.GetFloat("Driving", "CamberRear", s.CamberRear, -20f, 20f);
                s.TrackFront = ini.GetFloat("Driving", "TrackFront", s.TrackFront, -0.3f, 0.3f);
                s.TrackRear = ini.GetFloat("Driving", "TrackRear", s.TrackRear, -0.3f, 0.3f);
                s.HeightFront = ini.GetFloat("Driving", "HeightFront", s.HeightFront, -0.3f, 0.3f);
                s.HeightRear = ini.GetFloat("Driving", "HeightRear", s.HeightRear, -0.3f, 0.3f);

                s.DriftPower = ini.GetBool("Driving", "DriftPower", s.DriftPower);
                s.DriftPowerBoost = ini.GetFloat("Driving", "DriftPowerBoost", s.DriftPowerBoost, 1f, 3f);
                s.DriftTorqueBoost = ini.GetFloat("Driving", "DriftTorqueBoost", s.DriftTorqueBoost, 1f, 3f);

                for (var g = 0; g < s.GearTorque.Length; g++)
                {
                    s.GearTorque[g] = ini.GetFloat("Driving", "GearTorque" + (g + 1), s.GearTorque[g], 0.25f, 3f);
                    s.GearSlide[g] = ini.GetFloat("Driving", "GearSlide" + (g + 1), s.GearSlide[g], 0f, 1f);
                }
                s.DriftAngle = ini.GetFloat("Driving", "DriftAngle", s.DriftAngle, 3f, 60f);
                s.CounterSteer = ini.GetBool("Driving", "CounterSteer", s.CounterSteer);
                s.CounterSteerLock = ini.GetFloat("Driving", "CounterSteerLock", s.CounterSteerLock, 1f, 3f);
                s.CounterSteerFull = ini.GetFloat("Driving", "CounterSteerFull", s.CounterSteerFull, 5f, 90f);
                s.DashLight = ini.GetBool("Driving", "DashLight", s.DashLight);

                s.LowriderPose = ini.GetBool("Driving", "LowriderPose", s.LowriderPose);
                s.LowriderAnimDict = ini.GetString("Driving", "LowriderAnimDict", s.LowriderAnimDict);
                s.LowriderAnimClip = ini.GetString("Driving", "LowriderAnimClip", s.LowriderAnimClip);
                s.LowriderProbe = ini.GetBool("Driving", "LowriderProbe", s.LowriderProbe);
                s.LowriderWindow = ini.GetBool("Driving", "LowriderWindow", s.LowriderWindow);
                s.DashLightKey = ini.GetKey("Driving", "DashLightKey", s.DashLightKey);
                s.PadDashLight = ini.GetString("Driving", "PadDashLight", s.PadDashLight);

                s.Spawner = ini.GetBool("General", "Spawner", s.Spawner);
                s.SpawnerKey = ini.GetKey("General", "SpawnerKey", s.SpawnerKey);

                s.CrashSlowMo = ini.GetBool("General", "CrashSlowMo", s.CrashSlowMo);

                s.Repairs = ini.GetBool("General", "Repairs", s.Repairs);
                s.RepairMinutes = ini.GetFloat("General", "RepairMinutes", s.RepairMinutes, 0.5f, 60f);
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
