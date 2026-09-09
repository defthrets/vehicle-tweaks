using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;

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
    /// rotation at 0x008 with the negation of it at 0x010, and its X offset at 0x030. Those are
    /// the constants FiveM's own implementations of SET_VEHICLE_WHEEL_Y_ROTATION and
    /// SET_VEHICLE_WHEEL_X_OFFSET use, hardcoded there rather than pattern-scanned. A wheel dump
    /// on Enhanced settles it: -0.7180, 0.7180, -0.7180, 0.7180 -- four numbers mirrored across
    /// the car, each of them half a track. SHVDN finds the wheel, which is the half that moves
    /// between builds; VehicleWheel.MemoryAddress is somebody else's problem to keep right.
    ///
    /// THE GAME PUTS THE WHEELS BACK, AND THAT IS WHY THIS HOLDS ON TO CARS IT IS NOT DRIVING.
    /// Writing the stance once is not enough and neither is writing it while you are sat in the
    /// car: something in the game restores those fields, so a car straightened up the moment you
    /// walked away from it. So the stance is not an event, it is a lease -- every car this has
    /// stanced is written again every frame for as long as it exists, whether anybody is in it or
    /// not. That is the same thing VStancer does by patching the game's reset code, done the way
    /// a script is allowed to do it.
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
    /// HEIGHT IS THE ONE THAT IS NOT MEMORY. It goes through the hydraulic suspension raise, per
    /// wheel, which SHVDN exposes properly -- the safest of the three and the least certain,
    /// because a car with no hydraulics may simply ignore it.
    /// </summary>
    internal sealed class Stance
    {
        /// <summary>Where a wheel keeps its lean, the negation of its lean, and its sideways offset.</summary>
        private const int Camber = 0x008;
        private const int CamberBack = 0x010;
        private const int Track = 0x030;

        /// <summary>
        /// And how big the wheel actually is: the tyre, the rim inside it, and how wide it is.
        ///
        /// THE SAME PROVENANCE AS THE OTHER THREE, from the same file: these are what FiveM's
        /// SET_VEHICLE_WHEEL_TIRE_COLLIDER_SIZE, _RIM_COLLIDER_SIZE and _TIRE_COLLIDER_WIDTH
        /// write, hardcoded rather than scanned for. They are the wheel the CAR uses -- what it
        /// rolls on and what it stands at -- which is why a bigger number lifts the car as well
        /// as filling the arch.
        /// </summary>
        private const int Tyre = 0x110;
        private const int Rim = 0x114;
        private const int Width = 0x118;

        /// <summary>Near enough to nothing that writing it would be writing nothing.</summary>
        private const float Nothing = 0.0005f;

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
            public Dictionary<int, float[]> Stock;
        }

        private readonly Settings _cfg;
        private readonly Stances _store;

        /// <summary>Every car this is holding, by handle. Usually one, sometimes a garage full.</summary>
        private readonly Dictionary<int, Held> _held = new Dictionary<int, Held>();

        private int _car;
        private string _model;
        private float _saidCamber = float.NaN;
        private int _saidAt;

        private float[] _pending;
        private int _changedAt;
        private int _sweptAt;

        /// <summary>Every model by its hash, worked out once for the session.</summary>
        private static Dictionary<int, string> _names;

        public Stance(Settings cfg)
        {
            _cfg = cfg;

            Register();

            try { _store = new Stances(Paths.StanceFile); }
            catch (Exception ex) { Log.Warn("Stances cannot be remembered: " + ex.Message); }
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
                        _car = car.Handle;
                        _model = Name(car);
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
                    _car = 0;
                    _model = null;
                    _pending = null;
                    _changedAt = 0;
                }

                Sweep(me, now);
                Keep();
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
                _cfg.WheelSize, _cfg.RimSize, _cfg.WheelWidth,
            };
        }

        private static bool Flat(float[] values)
        {
            return Stances.Flat(values);
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

            return held;
        }

        /// <summary>
        /// Writes every held car again, and lets go of the ones that are gone or back to stock.
        /// </summary>
        private void Keep()
        {
            if (_held.Count == 0) return;

            List<int> drop = null;

            foreach (var pair in _held)
            {
                var held = pair.Value;

                if (held.Car == null || !held.Car.Exists())
                {
                    (drop ?? (drop = new List<int>())).Add(pair.Key);
                    continue;
                }

                Apply(held);

                // A CAR PUT BACK TO STOCK HAS JUST HAD ITS STOCK VALUES WRITTEN, so this is the
                // frame to stop caring about it. Anything else is a lease that never ends.
                if (Flat(held.Values)) (drop ?? (drop = new List<int>())).Add(pair.Key);
            }

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
        /// Loads what this car was last given, its model's if it has never had one of its own.
        ///
        /// A CAR WITH NEITHER KEEPS WHAT IS ALREADY ON THE SLIDERS rather than snapping to nought.
        /// Zeroing would make switching this on look like the feature breaking.
        /// </summary>
        private void Recall(Vehicle car)
        {
            _pending = null;
            _changedAt = 0;

            if (!_cfg.StanceRemember) return;

            var kept = FromCar(car);
            var whose = "this one";

            if (kept == null && _store != null && _model != null)
            {
                kept = _store.Get(_model);
                whose = "any " + _model;
            }

            if (kept == null) return;

            _cfg.CamberFront = kept[0];
            _cfg.CamberRear = kept[1];
            _cfg.TrackFront = kept[2];
            _cfg.TrackRear = kept[3];
            _cfg.HeightFront = kept[4];
            _cfg.HeightRear = kept[5];
            _cfg.WheelSize = kept[6];
            _cfg.RimSize = kept[7];
            _cfg.WheelWidth = kept[8];

            Log.Info("Stance: gave back the stance remembered for " + whose + " -- camber " +
                     kept[0].ToString("0.0") + " / " + kept[1].ToString("0.0") + " deg, track " +
                     kept[2].ToString("0.00") + " / " + kept[3].ToString("0.00") + " m, height " +
                     kept[4].ToString("0.00") + " / " + kept[5].ToString("0.00") +
                     ", wheels x" + kept[6].ToString("0.00") + ".");
        }

        /// <summary>
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

            // ONTO THE CAR, which is what makes it this car's, and into the file, which is what
            // makes it outlive the car.
            ToCar(car, live);

            if (_store != null && _model != null) _store.Put(_model, live);
        }

        private static bool Moved(float[] a, float[] b)
        {
            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                if (Math.Abs(a[i] - b[i]) >= Nothing) return true;
            }

            return false;
        }

        /// <summary>Reads what a car came with, refusing any wheel that does not read sanely.</summary>
        private Dictionary<int, float[]> Capture(Vehicle car)
        {
            var stock = new Dictionary<int, float[]>();

            try
            {
                foreach (var wheel in car.Wheels)
                {
                    var at = wheel.MemoryAddress;

                    if (at == IntPtr.Zero) continue;

                    var camber = Read(at, Camber);
                    var track = Read(at, Track);
                    var tyre = Read(at, Tyre);
                    var rim = Read(at, Rim);
                    var wide = Read(at, Width);

                    // THE GEOMETRY DECIDES WHETHER THE WHEEL IS USABLE AT ALL, and the sizes
                    // decide only for themselves. They were one test, which meant a tyre radius
                    // that would not read took camber and track down with it -- three fields
                    // hostage to the newest and least proven of the six.
                    if (!Sound(camber) || !Sound(track))
                    {
                        Log.Warn("Stance: wheel " + wheel.BoneId + " does not read like a wheel (" +
                                 camber + ", " + track + "), so it is left alone.");
                        continue;
                    }

                    if (!Sound(tyre) || !Sound(rim) || !Sound(wide))
                    {
                        Log.Once("stance-size", "Wheel sizes do not read on this build (" + tyre +
                                                ", " + rim + ", " + wide + "), so they are left " +
                                                "alone. Camber and track are unaffected.");
                        tyre = rim = wide = 0f;
                    }

                    var raise = 0f;

                    try { raise = wheel.GetHydraulicSuspensionRaiseFactor(); }
                    catch { /* nought is the right assumption and the right thing to put back */ }

                    stock[(int)wheel.BoneId] = new[] { camber, track, raise, tyre, rim, wide };
                }
            }
            catch (Exception ex)
            {
                Log.Once("stance-capture", "Could not read a car's wheels: " + ex.Message);
            }

            return stock;
        }

        /// <summary>Writes one held car's wheels, this frame, whether anybody is in it or not.</summary>
        private void Apply(Held held)
        {
            try
            {
                foreach (var wheel in held.Car.Wheels)
                {
                    float[] stock;

                    if (!held.Stock.TryGetValue((int)wheel.BoneId, out stock)) continue;

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

                    var camber = stock[0] + side * lean;

                    Write(at, Camber, camber);
                    Write(at, CamberBack, -camber);
                    Write(at, Track, stock[1] - side * wide);

                    // MULTIPLIED, NOT ADDED, WHICH IS THE ONE PLACE THIS FEATURE CHANGES ITS MIND.
                    // Five centimetres of camber means the same thing on a Panto and on a
                    // Barracks; five centimetres of tyre does not. A size is a proportion of what
                    // was there, so it is written as one.
                    // Nought means the field did not read as a size when this car was captured.
                    if (stock[3] > 0f) Write(at, Tyre, stock[3] * held.Values[6]);
                    if (stock[4] > 0f) Write(at, Rim, stock[4] * held.Values[7]);
                    if (stock[5] > 0f) Write(at, Width, stock[5] * held.Values[8]);

                    try { wheel.SetHydraulicSuspensionRaiseFactor(stock[2] + up); }
                    catch { /* the one part of this the car is allowed to refuse */ }

                    Say(held, wheel, stock, camber);
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
        private void Say(Held held, VehicleWheel wheel, float[] stock, float camber)
        {
            if (held.Car.Handle != _car || wheel.BoneId != VehicleWheelBoneId.WheelLeftFront) return;

            // SAID WHEN IT CHANGES, NOT ONCE PER CAR. Once per car answered "did anything happen
            // when I got in", which was the question at the time; it cannot answer "is the slider
            // I am moving reaching the wheel", which is the question now, because the one line it
            // writes is always from before the first nudge. Throttled, or a held D-pad writes
            // sixty lines a second.
            var now = Game.GameTime;

            if (Math.Abs(camber - _saidCamber) < 0.0005f || now - _saidAt < 400) return;

            _saidCamber = camber;
            _saidAt = now;

            Log.Info("Stance: front left camber " + stock[0].ToString("0.0000") + " to " +
                     camber.ToString("0.0000") + " rad, track " + stock[1].ToString("0.0000") +
                     " to " + (stock[1] - held.Values[2]).ToString("0.0000") + " m, raise " +
                     (stock[2] + held.Values[4]).ToString("0.00") + ", tyre x" +
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
            _held.Clear();
            _car = 0;
            _model = null;
            _pending = null;
            _changedAt = 0;
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
            "vt_size", "vt_rim", "vt_width",
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
                var kept = (float[])Stances.Stock.Clone();

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
