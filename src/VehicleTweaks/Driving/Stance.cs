using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using GTA;
using GTA.Native;
using System.Drawing;
using VehicleTweaks.Core;
using VehicleTweaks.UI;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Camber, track width and ride height, per axle -- what VStancer does, the way it does it.
    ///
    /// THE BONES WERE THE RIGHT IDEA AND THEY DO NOT WORK. Camber IS the Y rotation of a wheel
    /// bone and track width IS its X offset, both settable through EntityBone, and the first
    /// version of this set them there on the reasoning that a property is not an address and so
    /// cannot go stale with a game update. What it missed is WHEN: the game poses the vehicle
    /// skeleton from the wheel physics every frame, after scripts have run, so the write was
    /// always correct and always thrown away before anything was drawn.
    ///
    /// THEY LIVE IN THE WHEEL, AND THE OFFSETS ARE NOT A GUESS. Each CWheel carries its own Y
    /// rotation at 0x008 with the negation of it at 0x010, its place in the car at 0x020 and
    /// again at 0x030, and its radii at 0x110. The first three and the last are the constants
    /// FiveM's own implementations of SET_VEHICLE_WHEEL_Y_ROTATION and _X_OFFSET use, hardcoded
    /// there rather than pattern-scanned; the sweep below found the rest on this build. SHVDN
    /// finds the wheel, which is the half that moves between builds.
    ///
    /// TWO KINDS OF FIELD, AND THE SWEEP TOLD THEM APART. Write the top of the suspension line
    /// at 0x020, or the diagonal of the lean at 0x000, and it stays. Write the lean at 0x008, or
    /// the bottom of the line at 0x030, or a radius at 0x110, and a frame later the car's own
    /// number is back: the game rewrites those every frame, after this script has had its turn
    /// and before the wheel is drawn, so a write from the tick is undone before anyone sees it
    /// -- and the wheel is drawn from the ones that are put back. FiveM writes the same fields and works because
    /// it ticks its scripts at a different point in the frame; VStancer works because it patches
    /// the game's code. This does the third thing, in Publish: a thread of its own writes them
    /// faster than the game can put them back.
    ///
    /// AND IT HOLDS ON TO CARS IT IS NOT DRIVING. A stance is not an event, it is a lease: every
    /// car this has stanced is written again every frame for as long as it exists, whether
    /// anybody is in it or not, so a car does not straighten up the moment you walk away from
    /// it.
    ///
    /// AND CARS IT HAS NEVER SEEN ARE PICKED UP BY THEIR OWN DECORATOR. A stance is written onto
    /// the vehicle as well as into a file, so a car found nearby carrying one is adopted and held
    /// like any other -- which is what makes it survive a save, a reload of the mod, and driving
    /// something else for an hour.
    ///
    /// AND IT LOOKS BEFORE IT WRITES. Every wheel's own values are read once, and a reading that
    /// is not a finite number in a sane range means the address is not what this thinks it is --
    /// so that wheel is skipped and said out loud. What is written is that stock value plus the
    /// setting, so nought is genuinely the car as it came, and setting a stanced car back to
    /// nought writes the stock values once more and then lets go of it.
    ///
    /// MIRRORED ACROSS THE AXLE. The two sides move in opposite directions in the car's own
    /// coordinates, so one slider becomes two opposite numbers. Odd bone ids are the left of each
    /// axle. Widening wants a NEGATIVE offset on the left -- a quirk of the wheel models being
    /// rotated to face outwards, and why VStancer's readme tells you to type a minus sign for a
    /// wider track. This takes the sign out of the setting: positive is wider.
    ///
    /// HEIGHT IS AN OFFSET ON A MOVING NUMBER. The bottom of the suspension line is where the
    /// wheel is this frame and the game moves it every frame as the suspension works, so it
    /// cannot be pinned the way the track is -- pinned where it sat at rest, every wheel hung at
    /// full droop and the car floated. The race reads what the game just put there and takes
    /// the offset off it; see Lower. A wheel moved up into its arch is a body sat lower over
    /// it, which is why negative drops the car.
    /// </summary>
    internal sealed class Stance
    {
        /// <summary>
        /// The wheel's lean, as the two off-diagonal terms of a rotation about the car's long
        /// axis, and the two diagonal ones.
        ///
        /// A ROTATION, NOT AN ANGLE, WHICH IS WHAT LETS IT GO TO NINETY. FiveM writes an angle
        /// into 0x008 and its negation into 0x010 and leaves 0x000 and 0x018 at one, which is a
        /// rotation only while the angle is small: at ninety degrees it is a wheel stretched to
        /// nearly twice its width and leaned about sixty. The dump reads (1, 0, 0) at 0x000 and
        /// (0, 0, 1) at 0x010 -- the X and Z rows of a matrix -- so this writes the sine into the
        /// off-diagonal pair and the cosine into the diagonal pair, and the wheel turns rather
        /// than shears. The diagonal pair holds; the off-diagonal pair is raced.
        ///
        /// AND THE LENGTH OF THE FIRST ROW IS THE WIDTH THE WHEEL IS DRAWN AT. A rotation's rows
        /// are unit long, and a row that is longer than one scales whatever is drawn through it
        /// along that axis. The first row is the wheel's own axle, so the width slider goes here:
        /// the row is written as width times (cos, 0, sin) rather than (cos, 0, sin). The
        /// number the game keeps for the Arena wheel sizes, on the car, could not be found on
        /// this build -- none of FiveM's three patterns for it exist in this code -- and this is
        /// the one place a wheel is guaranteed to be drawn through.
        /// </summary>
        private const int CosX = 0x000;
        private const int Sin = 0x008;
        private const int SinBack = 0x010;
        private const int CosZ = 0x018;

        /// <summary>
        /// Where the wheel sits: the top of its suspension line at 0x020 and the bottom at 0x030,
        /// a vector each, X across the car and Z up it.
        ///
        /// THE TOP HOLDS, THE BOTTOM IS PUT BACK, AND THE WHEEL IS PLACED FROM THE BOTTOM. The
        /// sweep found 0x020 keeps what it is given and 0x030 does not, and a track written to
        /// the top alone moved nothing anyone could see. So both ends are written -- the top from
        /// the tick, because it holds, and the bottom from the race, because it does not -- and
        /// the whole line moves sideways for track and up for height.
        /// </summary>
        private const int TopX = 0x020;
        private const int TopZ = 0x028;
        private const int BottomX = 0x030;
        private const int BottomZ = 0x038;

        /// <summary>
        /// How big the wheel is: the tyre's radius, the collider's width, and the width it is
        /// drawn at.
        ///
        /// 0x110 IS THE TYRE AND IT IS THE ONE THAT SHOWS: the wheel is drawn to it. 0x114, the
        /// rim inside it, is the collider the car rolls on with the tyre gone and changes nothing
        /// you can see, so it is not offered any more. 0x118 is the collider's width; 0x11C is a
        /// fourth number the sweep found sitting beside them, the size of a tyre width and, unlike
        /// the three before it, left alone by the game -- so width is written to both.
        /// </summary>
        private const int Tyre = 0x110;
        private const int Width = 0x118;
        private const int Tread = 0x11C;

        /// <summary>Near enough to nothing that writing it would be writing nothing.</summary>
        private const float Nothing = 0.0005f;

        /// <summary>The eight, in the order they are written onto a car.</summary>
        private const int Values = 8;

        /// <summary>
        /// What each of the nine means "as the car came".
        ///
        /// NOT ALL NOUGHTS. The first six are amounts ADDED to what the car has, so nothing is
        /// nought; the last three are what a wheel is MULTIPLIED by, so nothing is one. A car
        /// stanced before the sizes existed carries six decorators, and reading the seventh as
        /// the nought the game hands back for a missing one would shrink its wheels to nothing.
        /// </summary>
        private static readonly float[] Stock = { 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f };

        /// <summary>
        /// A wheel does not lean by five radians and does not sit five metres out.
        ///
        /// Not a limit on the setting -- a test of the ADDRESS. If what is already there is not a
        /// small number, the pointer is not a wheel and nothing gets written.
        /// </summary>
        private const float Sane = 5f;

        /// <summary>How long the sliders have to sit still before the stance is written down.</summary>
        private const int SettleMs = 900;

        /// <summary>How often to look around for a car carrying a stance of its own.</summary>
        private const int SweepMs = 900;

        /// <summary>How far to look. Beyond this a car is not drawn, so its wheels do not matter.</summary>
        private const float SweepRange = 120f;

        /// <summary>One car being held at a stance, and what it looked like before.</summary>
        private sealed class Held
        {
            public Vehicle Car;
            public float[] Values;
            public Dictionary<int, Came> Stock;
        }

        private readonly Settings _cfg;

        /// <summary>The size a wheel is drawn at, which is the car's to say and not the wheel's.</summary>
        private readonly Drawn _drawn = new Drawn();

        /// <summary>Every car this is holding, by handle. Usually one, sometimes a garage full.</summary>
        private readonly Dictionary<int, Held> _held = new Dictionary<int, Held>();

        private int _car;
        private string _model;
        private bool _probed;
        private float _saidCamber = float.NaN;
        private int _saidAt;

        private float[] _pending;

        /// <summary>The car being driven, kept so a change can be written onto it on the way out.</summary>
        private Vehicle _driving;
        private int _changedAt;
        private int _sweptAt;

        /// <summary>Every model by its hash, worked out once for the session.</summary>
        private static Dictionary<int, string> _names;

        public Stance(Settings cfg)
        {
            _cfg = cfg;

            Register();
        }

        public void Update(Ped me)
        {
            try
            {
                var now = Game.GameTime;
                var car = me == null ? null : me.CurrentVehicle;

                if (car != null && car.Exists())
                {
                    if (car.Handle != _car)
                    {
                        // Straight from one car into another: what the last one was owed, it gets.
                        Flush();

                        _car = car.Handle;
                        _model = Name(car);
                        _driving = car;
                        _saidCamber = float.NaN;

                        Recall(car);
                    }

                    // THE CAR YOU ARE IN FOLLOWS THE SLIDERS, every frame, which is what makes the
                    // panel worth having open while you drive.
                    var live = Live();

                    if (!Flat(live) || _held.ContainsKey(_car)) Hold(car).Values = live;

                    Remember(car, now);
                }
                else
                {
                    // ON THE WAY OUT, WHATEVER IS STILL OWED IS WRITTEN. A change waits nine
                    // tenths of a second for the sliders to sit still before it goes onto the
                    // car, and getting out inside that window used to throw it away -- which is
                    // a height set and then lost the moment you climbed back in. Nothing is
                    // owed for long; it is only ever owed at all when you leave quickly.
                    Flush();

                    _car = 0;
                    _model = null;
                    _driving = null;
                }

                Sweep(me, now);
                Keep();
                Trial(car, now);

                if (_cfg.StanceProbe)
                {
                    _drawn.Trial(car, now, Math.Abs(_cfg.WheelWidth - 1f) >= Nothing);
                }
            }
            catch (Exception ex)
            {
                Log.Once("stance", "The stance fell over: " + ex.Message);
            }
        }

        /// <summary>The six as they stand on the panel.</summary>
        private float[] Live()
        {
            return new[]
            {
                _cfg.CamberFront, _cfg.CamberRear, _cfg.TrackFront,
                _cfg.TrackRear, _cfg.HeightFront, _cfg.HeightRear,
                _cfg.WheelSize, _cfg.WheelWidth,
            };
        }

        /// <summary>Whether these are the values that change nothing.</summary>
        private static bool Flat(float[] values)
        {
            if (values == null) return true;

            for (var i = 0; i < Values && i < values.Length; i++)
            {
                if (Math.Abs(values[i] - Stock[i]) >= Nothing) return false;
            }

            return true;
        }

        /// <summary>Starts holding a car, reading what it came with the first time.</summary>
        private Held Hold(Vehicle car)
        {
            Held held;

            if (_held.TryGetValue(car.Handle, out held) && held.Car != null && held.Car.Exists())
            {
                return held;
            }

            held = new Held { Car = car, Values = Live(), Stock = Capture(car) };
            _held[car.Handle] = held;

            _drawn.Find(car);

            // AND IT STOPS BEING TRAFFIC. A car the game owns is cleaned up the moment you are
            // far enough away and looking elsewhere -- which for an ordinary car is exactly
            // right and for one you have just spent five minutes setting up is the whole work
            // thrown away. Made persistent, it stays where you left it until you say otherwise.
            try { car.IsPersistent = true; }
            catch { /* it will still be stanced for as long as it lasts */ }

            return held;
        }

        /// <summary>
        /// Writes every held car again, and lets go of the ones that are gone or back to stock.
        /// </summary>
        private void Keep()
        {
            if (_held.Count == 0)
            {
                Publish(null);
                return;
            }

            List<int> drop = null;
            var shots = new List<Shot>();

            foreach (var pair in _held)
            {
                var held = pair.Value;

                if (held.Car == null || !held.Car.Exists())
                {
                    (drop ?? (drop = new List<int>())).Add(pair.Key);
                    continue;
                }

                Apply(held, shots);

                // A CAR PUT BACK TO STOCK HAS JUST HAD ITS STOCK VALUES WRITTEN, so this is the
                // frame to stop caring about it. Anything else is a lease that never ends.
                if (!Flat(held.Values)) continue;

                (drop ?? (drop = new List<int>())).Add(pair.Key);

                // AND IT GOES BACK TO BEING TRAFFIC. Kept alive because it was stanced; stanced
                // no longer, so there is nothing left to keep it for.
                try { held.Car.MarkAsNoLongerNeeded(); }
                catch { /* the game will have it back either way */ }
            }

            Publish(shots);

            if (drop == null) return;

            foreach (var handle in drop) _held.Remove(handle);
        }

        /// <summary>
        /// Looks around for a car carrying a stance of its own and takes it on.
        ///
        /// NOT EVERY FRAME. The list of vehicles near the player is a fresh array every time it is
        /// asked for, and a car that has just appeared can wait a moment for its wheels.
        /// </summary>
        private void Sweep(Ped me, int now)
        {
            if (!_cfg.StanceRemember || me == null || now - _sweptAt < SweepMs) return;

            _sweptAt = now;

            try
            {
                foreach (var near in World.GetNearbyVehicles(me, SweepRange))
                {
                    if (near == null || !near.Exists() || _held.ContainsKey(near.Handle)) continue;

                    var kept = FromCar(near);

                    if (kept == null || Flat(kept)) continue;

                    _held[near.Handle] = new Held
                    {
                        Car = near,
                        Values = kept,
                        Stock = Capture(near),
                    };

                    Log.Debug("Stance: picked up " + (Name(near) ?? "a car") + " still carrying " +
                              "its own stance.");
                }
            }
            catch (Exception ex)
            {
                Log.Once("stance-sweep", "Could not look for stanced cars: " + ex.Message);
            }
        }

        /// <summary>
        /// Loads what THIS car is carrying, and nothing else.
        ///
        /// A CAR YOU HAVE NOT STANCED IS A STOCK CAR. It used to fall back to whatever that MODEL
        /// was last given, and then to whatever happened to be on the sliders -- so stancing one
        /// Baller stanced every Baller, and getting into a taxi you had never touched put the
        /// last car's camber on it. That is not what a stance is. It belongs to the car it was
        /// done to and to nothing else, so a car with no stance of its own puts the sliders back
        /// to stock and is left completely alone.
        /// </summary>
        private void Recall(Vehicle car)
        {
            _pending = null;
            _changedAt = 0;

            if (!_cfg.StanceRemember) return;

            var kept = FromCar(car) ?? Stock;

            _cfg.CamberFront = kept[0];
            _cfg.CamberRear = kept[1];
            _cfg.TrackFront = kept[2];
            _cfg.TrackRear = kept[3];
            _cfg.HeightFront = kept[4];
            _cfg.HeightRear = kept[5];
            _cfg.WheelSize = kept[6];
            _cfg.WheelWidth = kept[7];

            if (Flat(kept)) return;

            Log.Info("Stance: this car is carrying its own -- camber " + kept[0].ToString("0.0") +
                     " / " + kept[1].ToString("0.0") + " deg, track " + kept[2].ToString("0.00") +
                     " / " + kept[3].ToString("0.00") + " m, height " + kept[4].ToString("0.00") +
                     " / " + kept[5].ToString("0.00") + ", wheels x" + kept[6].ToString("0.00") + ".");
        }

        /// <summary>
        /// Writes the stance down a moment after you stop changing it.        /// <summary>
        /// Writes the stance down a moment after you stop changing it.
        ///
        /// NOT ON EVERY NUDGE. A slider held down on a D-pad moves twenty times a second and each
        /// of those would rewrite a file and a set of decorators.
        /// </summary>
        private void Remember(Vehicle car, int now)
        {
            if (!_cfg.StanceRemember) return;

            var live = Live();

            if (_pending == null || Moved(live, _pending))
            {
                _pending = live;
                _changedAt = now;
                return;
            }

            if (_changedAt == 0 || now - _changedAt < SettleMs) return;

            _changedAt = 0;

            // ONTO THE CAR, AND ONLY ONTO THE CAR. There is no file any more: a stance that was
            // also filed by model came back on every other car of that model, which is the thing
            // this was asked to stop doing.
            ToCar(car, live);
        }

        /// <summary>Writes a change that has not settled yet onto the car it belongs to, now.</summary>
        private void Flush()
        {
            try
            {
                if (_cfg.StanceRemember && _pending != null && _changedAt != 0 &&
                    _driving != null && _driving.Exists())
                {
                    ToCar(_driving, _pending);
                }
            }
            catch
            {
                // Nothing to flush onto, which is what happens when the car is already gone.
            }

            _pending = null;
            _changedAt = 0;
        }

        private static bool Moved(float[] a, float[] b)
        {
            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                if (Math.Abs(a[i] - b[i]) >= Nothing) return true;
            }

            return false;
        }


        /// <summary>
        /// Reads the front wheels byte by byte and writes down what is where.
        ///
        /// BECAUSE THE WRITES LAND AND THE WHEELS DO NOT MOVE. The log proves the slider reaches
        /// the field and the field takes the value; the car ignores it. That leaves one likely
        /// answer -- the three offsets are FiveM's, FiveM is Legacy, and this is Enhanced, which
        /// is a different executable with its own idea of where things sit in a CWheel.
        ///
        /// AND A STRUCT CAN BE FOUND WITHOUT A DEBUGGER, because two of its fields announce
        /// themselves. The X offset is HALF A TRACK, and the left wheel's is the NEGATIVE of the
        /// right wheel's -- so any offset holding a mirrored pair of plausible size is a
        /// candidate, and on Legacy the winner is 0x030. The tyre, rim and width sit beside each
        /// other as three believable radii in a row, which on Legacy is 0x110. Find those two
        /// landmarks on this build and the rest of the layout follows from them.
        ///
        /// OFF, AND IT RUNS ONCE. It is a page of numbers in a log for somebody to read, not a
        /// feature anybody wants.
        /// </summary>
        private void Probe(Vehicle car)
        {
            if (!_cfg.StanceProbe || _probed) return;

            _probed = true;

            try
            {
                var left = IntPtr.Zero;
                var right = IntPtr.Zero;

                foreach (var wheel in car.Wheels)
                {
                    if (wheel.BoneId == VehicleWheelBoneId.WheelLeftFront) left = wheel.MemoryAddress;
                    if (wheel.BoneId == VehicleWheelBoneId.WheelRightFront) right = wheel.MemoryAddress;
                }

                if (left == IntPtr.Zero || right == IntPtr.Zero)
                {
                    Log.Warn("Stance probe: could not find both front wheels.");
                    return;
                }

                string build;

                try { build = Game.Version.ToString(); }
                catch { build = "?"; }

                Log.Info("Stance probe: " + (Name(car) ?? "?") + ", front wheels at " +
                         left.ToString("X") + " and " + right.ToString("X") + ", game version " +
                         build + ".");

                var mirrored = new System.Text.StringBuilder();
                var sizes = new System.Text.StringBuilder();

                for (var off = 0; off < 0x1C0; off += 4)
                {
                    var l = Read(left, off);
                    var r = Read(right, off);

                    // A MIRRORED PAIR THE SIZE OF HALF A TRACK. That is what an X offset looks
                    // like, and very little else does.
                    if (Sound(l) && Sound(r) && Math.Abs(l + r) < 0.002f && Math.Abs(l) > 0.25f)
                    {
                        mirrored.Append("0x").Append(off.ToString("X3")).Append(" ")
                                .Append(l.ToString("0.0000")).Append("/")
                                .Append(r.ToString("0.0000")).Append("  ");
                    }

                    // Three believable radii in a row is a tyre, a rim and a width.
                    if (Believable(left, off) && Believable(left, off + 4) && Believable(left, off + 8))
                    {
                        sizes.Append("0x").Append(off.ToString("X3")).Append(" ")
                             .Append(Read(left, off).ToString("0.000")).Append("/")
                             .Append(Read(left, off + 4).ToString("0.000")).Append("/")
                             .Append(Read(left, off + 8).ToString("0.000")).Append("  ");
                    }
                }

                Log.Info("Stance probe: mirrored pairs -- " +
                         (mirrored.Length == 0 ? "none" : mirrored.ToString()));
                Log.Info("Stance probe: size triples -- " +
                         (sizes.Length == 0 ? "none" : sizes.ToString()));

                for (var line = 0; line < 0x1C0; line += 32)
                {
                    var row = new System.Text.StringBuilder("0x" + line.ToString("X3") + " ");

                    for (var off = line; off < line + 32; off += 4)
                    {
                        row.Append(Read(left, off).ToString("0.0000").PadLeft(11));
                    }

                    Log.Info("Stance probe: " + row);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Stance probe fell over: " + ex.Message);
            }
        }

        /// <summary>Whether a float looks like a wheel radius or a tyre width.</summary>
        private static bool Believable(IntPtr at, int offset)
        {
            var v = Read(at, offset);

            return !float.IsNaN(v) && v > 0.08f && v < 1.2f;
        }

        /// <summary>What one wheel came with, read once, so nought can mean the car as it was.</summary>
        private sealed class Came
        {
            /// <summary>The lean the car came with, worked back from the sine at 0x008.</summary>
            public float Angle;

            public float TopX, TopZ, BottomX, BottomZ;

            /// <summary>Nought where the field did not read as a size.</summary>
            public float Tyre, Width, Tread;
        }

        /// <summary>Reads what a car came with, refusing any wheel that does not read sanely.</summary>
        private Dictionary<int, Came> Capture(Vehicle car)
        {
            Probe(car);

            var stock = new Dictionary<int, Came>();

            try
            {
                foreach (var wheel in car.Wheels)
                {
                    var at = wheel.MemoryAddress;

                    if (at == IntPtr.Zero) continue;

                    var sin = Read(at, Sin);

                    var came = new Came
                    {
                        TopX = Read(at, TopX),
                        TopZ = Read(at, TopZ),
                        BottomX = Read(at, BottomX),
                        BottomZ = Read(at, BottomZ),
                        Tyre = Read(at, Tyre),
                        Width = Read(at, Width),
                        Tread = Read(at, Tread),
                    };

                    // THE GEOMETRY DECIDES WHETHER THE WHEEL IS USABLE AT ALL, and the sizes
                    // decide only for themselves. They were one test, which meant a tyre radius
                    // that would not read took camber and track down with it -- three fields
                    // hostage to the newest and least proven of the six.
                    if (!Sound(sin) || Math.Abs(sin) > 1f || !Sound(came.TopX) || !Sound(came.TopZ) ||
                        !Sound(came.BottomX) || !Sound(came.BottomZ))
                    {
                        Log.Warn("Stance: wheel " + wheel.BoneId + " does not read like a wheel (" +
                                 sin + ", " + came.TopX + ", " + came.BottomZ + "), so it is left alone.");
                        continue;
                    }

                    came.Angle = (float)Math.Asin(sin);

                    if (!Sound(came.Tyre) || !Sound(came.Width) || came.Tyre <= 0f || came.Width <= 0f)
                    {
                        Log.Once("stance-size", "Wheel sizes do not read on this build (" + came.Tyre +
                                                ", " + came.Width + "), so they are left alone. " +
                                                "Camber and track are unaffected.");
                        came.Tyre = came.Width = 0f;
                    }

                    // The fourth size is the sweep's find rather than FiveM's, so it answers to a
                    // tighter test than the others: a tyre width, or nothing.
                    if (!Sound(came.Tread) || came.Tread < 0.05f || came.Tread > 1.2f) came.Tread = 0f;

                    stock[(int)wheel.BoneId] = came;
                }
            }
            catch (Exception ex)
            {
                Log.Once("stance-capture", "Could not read a car's wheels: " + ex.Message);
            }

            return stock;
        }

        /// <summary>
        /// Writes one held car's wheels, this frame, whether anybody is in it or not -- and puts
        /// the fields the game would undo on the list for the other thread.
        /// </summary>
        private void Apply(Held held, List<Shot> shots)
        {
            try
            {
                foreach (var wheel in held.Car.Wheels)
                {
                    Came came;

                    if (!held.Stock.TryGetValue((int)wheel.BoneId, out came)) continue;

                    var at = wheel.MemoryAddress;

                    if (at == IntPtr.Zero) continue;

                    var id = (int)wheel.BoneId;

                    // FRONT IS THE FIRST AXLE AND EVERYTHING ELSE FOLLOWS THE REAR. A six-wheeler's
                    // middle axle has no slider of its own, and the rear is the one it looks like.
                    var front = id == (int)VehicleWheelBoneId.WheelLeftFront ||
                                id == (int)VehicleWheelBoneId.WheelRightFront;

                    // Left is odd: 11 is the left front, 12 the right front, 13 the left rear.
                    var side = (id & 1) == 1 ? 1f : -1f;

                    var lean = (front ? held.Values[0] : held.Values[1]) * (float)(Math.PI / 180.0);
                    var wide = front ? held.Values[2] : held.Values[3];
                    var up = front ? held.Values[4] : held.Values[5];

                    var angle = came.Angle + side * lean;
                    var sin = (float)Math.Sin(angle);
                    var cos = (float)Math.Cos(angle);

                    var bottomX = came.BottomX - side * wide;

                    // ONLY WHAT THE SLIDERS ASK FOR. Every field here is pinned or raced, and a
                    // field pinned to the value it had is not a no-op: the bottom of the
                    // suspension line moves with the suspension, and pinning it where it sat at
                    // rest held every wheel at full droop and floated every car that had so much
                    // as a camber. A slider on stock leaves its field entirely alone.
                    var leaning = Math.Abs(lean) >= Nothing;
                    var tracking = Math.Abs(wide) >= Nothing;
                    var lowering = Math.Abs(up) >= Nothing;
                    var widening = Math.Abs(held.Values[7] - 1f) >= Nothing;

                    // THE FIRST ROW OF THE WHEEL'S MATRIX, STRETCHED. It is the axle direction,
                    // and a row longer than one draws the wheel that much wider; see CosX.
                    var stretch = widening ? held.Values[7] : 1f;

                    // MULTIPLIED, NOT ADDED, WHICH IS THE ONE PLACE THIS FEATURE CHANGES ITS MIND.
                    // Five centimetres of camber means the same thing on a Panto and on a
                    // Barracks; five centimetres of tyre does not. A size is a proportion of what
                    // was there, so it is written as one. Nought means leave it.
                    var tyre = came.Tyre > 0f && Math.Abs(held.Values[6] - 1f) >= Nothing
                                   ? came.Tyre * held.Values[6] : 0f;
                    var width = came.Width > 0f && Math.Abs(held.Values[7] - 1f) >= Nothing
                                    ? came.Width * held.Values[7] : 0f;

                    if (Trialling(held.Car)) continue;

                    Before(held, wheel, at, sin, bottomX);

                    // THE ONES THAT HOLD, written from here.
                    if (leaning || widening)
                    {
                        Write(at, CosX, stretch * cos);
                        Write(at, CosZ, cos);
                    }

                    if (tracking) Write(at, TopX, came.TopX - side * wide);
                    if (width > 0f && came.Tread > 0f) Write(at, Tread, came.Tread * held.Values[7]);

                    // THE ONES THE GAME PUTS BACK, written here for a build where the timing
                    // happens to work, and handed to the race for the one where it does not.
                    // Height is not among them: it is an offset on a moving number, and only the
                    // race can see the number move.
                    if (leaning || widening)
                    {
                        Write(at, Sin, stretch * sin);
                        Write(at, SinBack, -sin);
                    }

                    if (tracking) Write(at, BottomX, bottomX);
                    if (tyre > 0f) Write(at, Tyre, tyre);
                    if (width > 0f) Write(at, Width, width);

                    // AND THE ONE YOU CAN SEE, which is the car's rather than the wheel's. Written
                    // once per car per frame would do; once per wheel is the same number again
                    // and costs three checked reads, which is nothing.
                    if (width > 0f) _drawn.Write(held.Car, held.Values[7]);

                    shots.Add(new Shot
                    {
                        At = at,
                        Xz = leaning || widening ? stretch * sin : float.NaN,
                        Zx = -sin,
                        BottomX = tracking ? bottomX : float.NaN,
                        Up = lowering ? up : 0f,
                        Tyre = tyre,
                        Width = width,
                    });

                    Say(held, wheel, came, angle);
                    Say(held, wheel, came, angle);
                }
            }
            catch (Exception ex)
            {
                Log.Once("stance-apply", "Could not set a car's wheels: " + ex.Message);
            }
        }

        /// <summary>
        /// Says once per car what the front left wheel was and what it became.
        ///
        /// THE SIGNS ARE THE ONE THING THAT CANNOT BE CHECKED FROM OUTSIDE THE GAME. If the camber
        /// leans the wrong way or the track pulls in instead of out, this line says so -- and the
        /// fix is a minus sign in the ini rather than a rebuild.
        /// </summary>
        private void Say(Held held, VehicleWheel wheel, Came came, float angle)
        {
            if (held.Car.Handle != _car || wheel.BoneId != VehicleWheelBoneId.WheelLeftFront) return;

            // SAID WHEN IT CHANGES, NOT ONCE PER CAR. Once per car answered "did anything happen
            // when I got in", which was the question at the time; it cannot answer "is the slider
            // I am moving reaching the wheel", which is the question now, because the one line it
            // writes is always from before the first nudge. Throttled, or a held D-pad writes
            // sixty lines a second.
            var now = Game.GameTime;

            if (Math.Abs(angle - _saidCamber) < 0.0005f || now - _saidAt < 400) return;

            _saidCamber = angle;
            _saidAt = now;

            Log.Info("Stance: front left lean " + came.Angle.ToString("0.0000") + " to " +
                     angle.ToString("0.0000") + " rad, x " + came.BottomX.ToString("0.0000") + " to " +
                     (came.BottomX - held.Values[2]).ToString("0.0000") + " m, z " +
                     came.BottomZ.ToString("0.00") + " to " +
                     (came.BottomZ - held.Values[4]).ToString("0.00") + ", tyre x" +
                     held.Values[6].ToString("0.00") + ". Holding " + _held.Count + " car(s).");
        }

        /// <summary>
        /// Lets go of everything without touching it, which is all shutdown is allowed to do.
        ///
        /// NOTHING IS PUT BACK. Every other override in this mod is handed back by handle when you
        /// get out, because the others are how a car BEHAVES while you are in it. A stance is what
        /// the car LOOKS like, it is meant to outlive the drive, and a car that straightens up
        /// when you walk away from it is not stanced, it is borrowed. Zeroing the sliders is how
        /// one is undone; see Keep.
        /// </summary>
        public void Release()
        {
            Flush();

            _stopRacing = true;
            _shots = null;
            _held.Clear();
            _car = 0;
            _model = null;
            _pending = null;
            _changedAt = 0;
        }


        // ==================================================================
        // The sweep: the one experiment that answers the question
        // ==================================================================

        /// <summary>How long each field is tried for, and by how much it is pushed.</summary>
        private const int TrialMs = 2500;
        private const float TrialNudge = 0.35f;

        private List<int> _trial;
        private int _trialIndex = -1;
        private int _trialAt;
        private int _trialCar;
        private float _trialOrig;
        private float _trialSeen;
        private bool _trialDone;

        /// <summary>What the front left wheel was last given, and when that was last said.</summary>
        private float[] _wrote;
        private int _beforeAt;

        /// <summary>Whether the sweep has this car, in which case the ordinary writes stand aside.</summary>
        private bool Trialling(Vehicle car)
        {
            return _cfg.StanceProbe && !_trialDone && _trialIndex >= 0 && car.Handle == _trialCar;
        }

        /// <summary>
        /// Says what the front left wheel reads NOW, a frame after it was last written.
        ///
        /// THE WHOLE QUESTION, ASKED OF THE GAME. The writes go in; the wheel does not move. Either
        /// the field is not the one this build draws from, in which case what was written is
        /// still there a frame later -- or it is exactly the one, and the game rewrites it every
        /// frame AFTER this script has run, in which case it reads as the car came. Those two
        /// look identical from the driver's seat and want opposite fixes.
        ///
        /// That second answer is the one everything else points at. VStancer works on this
        /// build and its author calls what it does "suspension patching" and lists "wheel
        /// deformation stops while it is active" as a known issue: it patches the game code that
        /// rebuilds the wheel from the suspension each frame, rather than writing after it.
        /// FiveM's natives write plainly and need calling every frame, and FiveM ticks its
        /// scripts at a different point in the frame from ScriptHookV -- after that rebuild
        /// rather than before it. A script here cannot choose when it runs.
        /// </summary>
        private void Before(Held held, VehicleWheel wheel, IntPtr at, float camber, float track)
        {
            if (!_cfg.StanceProbe || held.Car.Handle != _car ||
                wheel.BoneId != VehicleWheelBoneId.WheelLeftFront) return;

            var now = Game.GameTime;

            if (_wrote != null && Math.Abs(_wrote[0]) > 0.001f && now - _beforeAt >= 1000)
            {
                _beforeAt = now;

                var c = Read(at, Sin);
                var b = Read(at, SinBack);
                var t = Read(at, BottomX);
                var kept = Math.Abs(c - _wrote[0]) < 0.0005f;

                Log.Info("Stance probe: a frame on, 0x008 reads " + c.ToString("0.0000") +
                         " (wrote " + _wrote[0].ToString("0.0000") + "), 0x010 reads " +
                         b.ToString("0.0000") + " (wrote " + _wrote[1].ToString("0.0000") +
                         "), 0x030 reads " + t.ToString("0.0000") + " (wrote " +
                         _wrote[2].ToString("0.0000") + ") -- " +
                         (kept ? "the camber HELD, so the game does not touch this field and it " +
                                 "is not where this build draws from."
                               : "the camber was PUT BACK, so the game rewrites this field every " +
                                 "frame after this script has run."));
            }

            _wrote = new[] { camber, -camber, track };
        }

        /// <summary>
        /// Tries every plausible field of the front left wheel in turn, so a person can watch.
        ///
        /// A PROBE THAT NEEDS EYES, because the one thing a log cannot say is whether a wheel
        /// moved. Each float in the wheel that reads like a float and is small enough to be an
        /// angle or a distance is pushed by a third of a radian, or a third of a metre, for two
        /// and a half seconds, with the offset written across the top of the screen -- and then
        /// put back. Whoever is watching says which offset the wheel moved on, and that offset is
        /// where this build keeps the thing this mod is looking for. If it moves on none of them,
        /// the answer is the one in Before, and it is not a matter of offsets at all.
        ///
        /// STARTS WHEN THE FRONT CAMBER SLIDER LEAVES NOUGHT, which is the sign that somebody is
        /// looking. The known four go first, then everything else that is not nought. THE
        /// NOUGHTS ARE NOT TRIED, any more: a nought might be an integer, a float written into an
        /// integer is how a probe becomes a crash, and 0x128 was one. Each field is named in the
        /// log before it is touched, so a crash still says which. The struct is 0x230 bytes on
        /// this build -- that is how far apart two wheels sit -- and the sweep runs to its end,
        /// picking up from StanceProbeFrom so a sweep cut short is not begun again.
        ///
        /// WHILE IT RUNS, THE ORDINARY WRITES STAND ASIDE for this car, or every step would be
        /// two changes at once.
        /// </summary>
        private void Trial(Vehicle car, int now)
        {
            if (!_cfg.StanceProbe || _trialDone) return;

            if (car == null || !car.Exists() || car.Handle != _car)
            {
                // Between cars nothing is tried. A sweep half done starts again on the next car.
                _trial = null;
                _trialIndex = -1;
                return;
            }

            // NOUGHT MEANS NO SWEEP, which leaves the rest of the probe -- the dump and the
            // read-back -- to run on their own while the race is being judged by eye.
            if (_cfg.StanceProbeFrom < 1f) return;

            if (_trialIndex < 0 && Math.Abs(_cfg.CamberFront) < 0.5f) return;

            var fl = FrontLeft(car);

            if (fl == IntPtr.Zero) return;

            if (_trial == null || _trialCar != car.Handle)
            {
                _trial = Candidates(fl);
                _trialCar = car.Handle;

                // FROM AN OFFSET, NOT A NUMBER. Which fields are worth trying depends on what
                // they hold at the moment, so the count changes from car to car and a number
                // does not name the same field twice. The offset on the screen does.
                var from = (int)_cfg.StanceProbeFrom;

                if (from > 1) _trial.RemoveAll(o => o < from);

                _trialIndex = -1;

                Log.Info("Stance probe: sweeping " + _trial.Count + " fields of the front left " +
                         "wheel, " + (TrialMs / 1000f).ToString("0.0") + " s each, from 0x" +
                         Math.Max(0, from).ToString("X3") + ". Watch that wheel.");
            }

            if (_trialIndex < 0 || now - _trialAt >= TrialMs)
            {
                if (_trialIndex >= 0 && _trialIndex < _trial.Count)
                {
                    var was = _trial[_trialIndex];
                    var held = Math.Abs(_trialSeen - (_trialOrig + TrialNudge)) < 0.0005f;

                    Log.Info("Stance probe: 0x" + was.ToString("X3") + " read " +
                             _trialSeen.ToString("0.0000") + " a frame after being written (" +
                             (held ? "held" : "put back") + "); restored " +
                             _trialOrig.ToString("0.0000") + ".");

                    Write(fl, was, _trialOrig);
                }

                _trialIndex++;

                if (_trialIndex >= _trial.Count)
                {
                    _trialDone = true;
                    Log.Info("Stance probe: sweep done. The wheel moved on the field named at " +
                             "the time, or on none of them.");
                    return;
                }

                _trialOrig = Read(fl, _trial[_trialIndex]);
                _trialSeen = _trialOrig;
                _trialAt = now;

                Log.Info("Stance probe: trying 0x" + _trial[_trialIndex].ToString("X3") + " (" +
                         (_trialIndex + 1) + " of " + _trial.Count + "), which reads " +
                         _trialOrig.ToString("0.0000") + ".");
            }

            var off = _trial[_trialIndex];

            // READ BEFORE WRITING, on every frame but the first: that is the value the game left
            // there after it had its turn, which is the only reading that says anything.
            if (now != _trialAt) _trialSeen = Read(fl, off);

            Write(fl, off, _trialOrig + TrialNudge);

            Draw.Text("STANCE PROBE  0x" + off.ToString("X3") + "   " + (_trialIndex + 1) + " / " +
                      _trial.Count + "   watch the front left wheel", 0.5f, 0.10f, 0.55f,
                      Color.FromArgb(255, 245, 196, 60), 4, true);
        }

        private static IntPtr FrontLeft(Vehicle car)
        {
            try
            {
                foreach (var wheel in car.Wheels)
                {
                    if (wheel.BoneId == VehicleWheelBoneId.WheelLeftFront) return wheel.MemoryAddress;
                }
            }
            catch
            {
                // no wheel, no sweep
            }

            return IntPtr.Zero;
        }

        /// <summary>Every field of the wheel worth trying, in the order worth trying them.</summary>
        private static List<int> Candidates(IntPtr fl)
        {
            var known = new List<int> { Sin, SinBack, TopX, TopZ, BottomX };
            var rest = new List<int>();

            for (var off = 0; off < 0x230; off += 4)
            {
                if (known.Contains(off)) continue;

                var bits = Marshal.ReadInt32(fl, off);
                var v = Read(fl, off);

                // NOT A FLOAT: a small integer, a flag, the top half of a pointer. All of those
                // read as denormals, and none of them wants a third of a radian written in.
                if (bits != 0 && (bits & 0x7F800000) == 0) continue;
                if (float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) >= 3f) continue;

                if (bits == 0) continue;

                rest.Add(off);
            }

            known.AddRange(rest);
            return known;
        }

        // ==================================================================
        // The race: writing faster than the game can put it back
        // ==================================================================

        /// <summary>One wheel's numbers for the other thread: where, and what to keep writing.</summary>
        /// <summary>
        /// One wheel's numbers for the other thread: where, and what to keep writing.
        ///
        /// NaN means leave that field alone; nought means the same for a size or a height.
        /// </summary>
        private sealed class Shot
        {
            public IntPtr At;

            /// <summary>The first row's Z and the third row's X: the lean, and the width along with it.</summary>
            public float Xz;
            public float Zx;

            public float BottomX;
            public float Up;
            public float Tyre;
            public float Width;
        }

        private volatile Shot[] _shots;
        private volatile bool _stopRacing;
        private Thread _racer;

        /// <summary>
        /// Hands the other thread this frame's wheels, and starts it the first time.
        ///
        /// WHAT THE SWEEP FOUND, AND WHAT THIS DOES ABOUT IT. Every field a stance needs is one
        /// of two kinds. The top of the suspension line at 0x020, the diagonal of the lean at
        /// 0x000 and the fourth size at 0x11C HOLD: write them and they stay, so they are written
        /// once a frame from the ordinary tick. The lean at 0x008 and 0x010, the bottom of the
        /// line at 0x030 and the radii at 0x110 and 0x118 are PUT BACK: the game rewrites every
        /// one of them every frame, after this script has had its turn and before the wheel is
        /// drawn, so a write from the tick is gone before anyone sees it -- and the wheel is
        /// drawn from those. The Z of the bottom is put back to a DIFFERENT number each frame,
        /// which is the suspension working, and is the one field that is offset rather than
        /// overwritten.
        /// FiveM's natives write these same fields and work because FiveM ticks its scripts at
        /// a different point in the frame; VStancer works because it patches the game's code.
        /// A script under ScriptHookV can do neither.
        ///
        /// SO IT WRITES FASTER THAN THE GAME CAN UNDO IT. A thread of its own writes the lean
        /// and the radii of every held wheel again and again, thousands of times a second,
        /// whichever thread the game is on -- so that whenever the game comes to draw the wheel,
        /// what it finds there is ours. A four-byte aligned write is atomic on this
        /// architecture, so the game never reads half a number; and the addresses come from
        /// the tick, which has just confirmed every car still exists, so the worst a stale one
        /// can do is put a lean on a wheel in the pool for one frame.
        ///
        /// IT COSTS A CORE while a stanced car exists, and nothing while none does. That is the
        /// price of not being allowed to patch, and it is a setting.
        /// </summary>
        private void Publish(List<Shot> shots)
        {
            if (!_cfg.StanceRace || shots == null || shots.Count == 0)
            {
                _shots = null;
                return;
            }

            _shots = shots.ToArray();

            if (_racer != null) return;

            _racer = new Thread(Race) { IsBackground = true, Name = "Vehicle Tweaks stance" };
            _racer.Start();

            Log.Info("Stance: racing the game for the camber and the wheel sizes on a thread of " +
                     "its own, " + (_cfg.StanceRaceRest > 0f
                                        ? "resting " + _cfg.StanceRaceRest.ToString("0") + " ms between passes."
                                        : "never resting."));
        }

        private void Race()
        {
            try
            {
                while (!_stopRacing)
                {
                    var shots = _shots;

                    if (shots == null)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    for (var i = 0; i < shots.Length; i++)
                    {
                        var s = shots[i];

                        if (!float.IsNaN(s.Xz))
                        {
                            Write(s.At, Sin, s.Xz);
                            Write(s.At, SinBack, s.Zx);
                        }

                        if (!float.IsNaN(s.BottomX)) Write(s.At, BottomX, s.BottomX);
                        if (s.Tyre > 0f) Write(s.At, Tyre, s.Tyre);
                        if (s.Width > 0f) Write(s.At, Width, s.Width);
                        if (s.Up != 0f) Lower(s.At, s.Up);
                    }

                    if (_lowered.Count > 64) _lowered.Clear();

                    var rest = (int)_cfg.StanceRaceRest;

                    if (rest > 0) Thread.Sleep(rest);
                    else Thread.SpinWait(64);
                }
            }
            catch (ThreadAbortException)
            {
                // The script is being unloaded, and this thread with it.
            }
            catch (Exception ex)
            {
                Log.Once("stance-race", "The stance race fell over: " + ex.Message);
            }
        }

        /// <summary>What this thread last wrote for a wheel's height, so a fresh number can be told from its own.</summary>
        private readonly Dictionary<IntPtr, float> _lowered = new Dictionary<IntPtr, float>();

        /// <summary>
        /// Moves a wheel up or down by an offset from wherever the suspension has put it THIS pass.
        ///
        /// A PLACE CANNOT BE PINNED, AND THAT IS WHAT THE FLOATING CARS WERE. The bottom of the
        /// suspension line is where the wheel is this frame, and the game moves it every frame
        /// as the suspension works. Pinned to the number it had at rest, the wheel was held at
        /// full droop and the body floated on it. So height is not a place, it is an offset:
        /// read what the game just put there, take the offset off it, write that back.
        ///
        /// THE GAME'S NUMBER OR OURS? If the field holds what this thread last put there, the
        /// game has not been round since, and taking the offset off again would walk the wheel
        /// down the screen a few centimetres a pass. Anything else is fresh from the suspension.
        /// The comparison is exact because the read gives back the very bits that were written.
        /// </summary>
        private void Lower(IntPtr at, float up)
        {
            var now = Read(at, BottomZ);
            float mine;

            if (_lowered.TryGetValue(at, out mine) && now == mine) return;

            mine = now - up;

            Write(at, BottomZ, mine);
            _lowered[at] = mine;
        }

        // ==================================================================
        // The car's own memory
        // ==================================================================

        /// <summary>
        /// What the six numbers are called when they are written onto a car itself.
        ///
        /// A DECORATOR IS THE ONLY NAME AN INDIVIDUAL CAR HAS. Everything else about a vehicle is
        /// made up when it is created -- the handle most of all -- but a decorator is a named
        /// value the game carries on the entity and hands back later, and it is what the game
        /// itself uses to mark a car as somebody's personal vehicle. VStancer stores its stance
        /// the same way. This is the difference between "every Panto" and "this Panto".
        /// </summary>
        private static readonly string[] Decors =
        {
            "vt_camber_f", "vt_camber_r", "vt_track_f", "vt_track_r", "vt_height_f", "vt_height_r",
            "vt_size", "vt_width",
        };

        private static bool _registered;

        private static void Register()
        {
            if (_registered) return;

            _registered = true;

            try
            {
                // 1 is DECOR_TYPE_FLOAT. The enumeration is the game's, not SHVDN's.
                foreach (var name in Decors) Function.Call(Hash.DECOR_REGISTER, name, 1);
            }
            catch (Exception ex)
            {
                Log.Warn("Stances cannot be written onto cars themselves: " + ex.Message);
            }
        }

        /// <summary>The stance this particular car is carrying, or null if it has never had one.</summary>
        private static float[] FromCar(Vehicle car)
        {
            try
            {
                if (!Function.Call<bool>(Hash.DECOR_EXIST_ON, car, Decors[0])) return null;

                // EACH ONE ASKED FOR SEPARATELY, and a missing one left at what it means as
                // stock. A car stanced before the wheel sizes existed carries six decorators,
                // and reading the seventh as the nought the game hands back for an absent one
                // would shrink its wheels to nothing the moment it was picked up again.
                var kept = (float[])Stock.Clone();

                for (var i = 0; i < Decors.Length; i++)
                {
                    if (!Function.Call<bool>(Hash.DECOR_EXIST_ON, car, Decors[i])) continue;

                    kept[i] = Function.Call<float>(Hash.DECOR_GET_FLOAT, car, Decors[i]);
                }

                return kept;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Writes the stance onto the car, so the car is the thing that remembers it.</summary>
        private static void ToCar(Vehicle car, float[] values)
        {
            try
            {
                for (var i = 0; i < Decors.Length; i++)
                {
                    Function.Call(Hash.DECOR_SET_FLOAT, car, Decors[i], values[i]);
                }
            }
            catch
            {
                // The file still has it, which is the half that survives a reload anyway.
            }
        }

        /// <summary>
        /// The model's own name, which is the only thing about a car that survives a save.
        ///
        /// The catalogue is 921 names and the hash of each is worked out once, on the first car of
        /// the session, rather than 921 times per car.
        /// </summary>
        private static string Name(Vehicle car)
        {
            try
            {
                if (_names == null)
                {
                    _names = new Dictionary<int, string>();

                    foreach (var name in Models.All)
                    {
                        _names[unchecked((int)StringHash.AtStringHash(name))] = name;
                    }
                }

                string found;

                return _names.TryGetValue(car.Model.Hash, out found) ? found : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool Sound(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) < Sane;
        }

        /// <summary>
        /// A float out of the wheel, the long way round.
        ///
        /// BitConverter rather than a pointer cast: this project has no unsafe blocks in it and
        /// four bytes is four bytes. Marshal reads at an offset from a handle, which is exactly
        /// the shape of the thing being read.
        /// </summary>
        private static float Read(IntPtr at, int offset)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(at, offset)), 0);
        }

        private static void Write(IntPtr at, int offset, float value)
        {
            Marshal.WriteInt32(at, offset, BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
        }
    }
}
