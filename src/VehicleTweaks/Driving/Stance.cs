using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GTA;
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
    /// always correct and always thrown away before anything was drawn. There is no ordering
    /// that fixes it, because the numbers the poser uses do not live in the bone.
    ///
    /// THEY LIVE IN THE WHEEL, AND THE OFFSETS ARE NOT A GUESS. Each CWheel carries its own Y
    /// rotation at 0x008 with the negation of it at 0x010, and its X offset at 0x030. Those three
    /// are not measured here: they are the constants FiveM's own implementations of
    /// SET_VEHICLE_WHEEL_Y_ROTATION and SET_VEHICLE_WHEEL_X_OFFSET use, hardcoded in that project
    /// rather than pattern-scanned -- which is the interesting part, because everything in that
    /// file which HAS moved between game builds is scanned for and these are not. They are the
    /// same numbers on Legacy and on Enhanced, and they are exercised by every FiveM server
    /// running a stance script.
    ///
    /// SHVDN FINDS THE WHEEL, WHICH IS THE HALF THAT DOES MOVE. VehicleWheel.MemoryAddress is a
    /// maintained API: the walk from a vehicle to its wheel array is somebody else's problem, and
    /// theirs to keep right. Nothing here scans for anything.
    ///
    /// AND IT LOOKS BEFORE IT WRITES. Every wheel's own values are read once when you get in, and
    /// a reading that is not a finite number in a sane range means the address is not what this
    /// thinks it is -- so that wheel is skipped and said out loud, rather than written to. What is
    /// written is that stock value plus the setting, so nought is genuinely the car as it came and
    /// a car with camber from the factory keeps it.
    ///
    /// MIRRORED ACROSS THE AXLE. The two sides move in opposite directions in the car's own
    /// coordinates, so one slider becomes two opposite numbers, or a wider track is one wheel out
    /// and one wheel in. Odd bone ids are the left of each axle. Widening wants a NEGATIVE offset
    /// on the left, which is a quirk of the wheel models being rotated to face outwards, and is
    /// why VStancer's own readme tells you to type a negative number for a wider track. This
    /// takes the sign out of the setting: positive is wider, here as anywhere else.
    ///
    /// HEIGHT IS THE ONE THAT IS NOT MEMORY. It goes through the hydraulic suspension raise, per
    /// wheel, which SHVDN exposes properly -- so it is the safest of the three and the least
    /// certain, because a car with no hydraulics may simply ignore it. Said in the log either way.
    ///
    /// AND IT IS NOT HANDED BACK, WHICH IS THIS FILE'S ONE EXCEPTION TO THE HOUSE RULE. Every
    /// other override here is given back by handle when you get out, because the others are how a
    /// car BEHAVES while you are in it -- power, torque, grip -- and none of them should outlive
    /// the drive. A stance is what the car LOOKS like. It is meant to outlive the drive, and a car
    /// that straightens up the moment you step out of it is not stanced, it is borrowed. Restore
    /// still exists and still runs when the sliders go back to nought, which is how one is undone.
    ///
    /// REMEMBERED PER MODEL, in a file of its own beside the log -- see Core\Stances.cs for why a
    /// model and not a car, which is the honest limit of it.
    /// </summary>
    internal sealed class Stance
    {
        /// <summary>Where a wheel keeps its lean, the negation of its lean, and its sideways offset.</summary>
        private const int Camber = 0x008;
        private const int CamberBack = 0x010;
        private const int Track = 0x030;

        /// <summary>Near enough to nothing that writing it would be writing nothing.</summary>
        private const float Nothing = 0.0005f;

        /// <summary>
        /// A wheel does not lean by five radians and does not sit five metres out.
        ///
        /// This is not a limit on the setting, it is a test of the ADDRESS: if what is already
        /// there is not a small number, the pointer is not a wheel and nothing gets written.
        /// </summary>
        private const float Sane = 5f;

        private readonly Settings _cfg;

        /// <summary>The car this is about, and a way back to it when the player has got out.</summary>
        private int _car;
        private Vehicle _last;

        private bool _applied;
        private bool _said;

        /// <summary>How long the sliders have to sit still before the stance is written down.</summary>
        private const int SettleMs = 900;

        /// <summary>Where remembered stances live, and what this car is called in there.</summary>
        private readonly Stances _store;
        private string _model;

        private float[] _pending;
        private int _changedAt;

        /// <summary>Every model by its hash, worked out once for the session.</summary>
        private static Dictionary<int, string> _names;

        /// <summary>Camber, track and raise as the car came, per wheel, by bone id.</summary>
        private readonly Dictionary<int, float[]> _stock = new Dictionary<int, float[]>();

        public Stance(Settings cfg)
        {
            _cfg = cfg;

            try { _store = new Stances(Paths.StanceFile); }
            catch (Exception ex) { Log.Warn("Stances cannot be remembered: " + ex.Message); }
        }

        public void Update(Ped me)
        {
            try
            {
                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists())
                {
                    Release();
                    return;
                }

                if (car.Handle != _car)
                {
                    // NOT Release(). Getting out of a car used to put its wheels back, on the
                    // house rule that an override is handed back by handle -- and that rule is
                    // right for power, torque and grip, which are how a car BEHAVES while you
                    // are in it. A stance is not that. It is what the car looks like, it is
                    // meant to outlive the drive, and a car that straightens up the moment you
                    // step out of it is the fault this feature was reported for.
                    Forget();

                    _car = car.Handle;
                    _last = car;
                    _said = false;
                    _model = Name(car);

                    Capture(car);
                    Recall();
                }

                if (Flat())
                {
                    if (_applied) Restore();

                    Remember(Game.GameTime);
                    return;
                }

                Apply(car);
                Remember(Game.GameTime);

                _applied = true;
            }
            catch (Exception ex)
            {
                Forget();
                Log.Once("stance", "The stance fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Loads what this model was last given, if anything, into the sliders.
        ///
        /// A CAR NOBODY HAS STANCED KEEPS WHAT IS ALREADY ON THEM rather than snapping to nought.
        /// Zeroing would make switching this on look like the feature breaking: every car you got
        /// into would wipe the six numbers you had set, and the one you were sitting in when you
        /// turned it on would be the first.
        /// </summary>
        private void Recall()
        {
            _pending = null;
            _changedAt = 0;

            if (!_cfg.StanceRemember || _store == null || _model == null) return;

            var kept = _store.Get(_model);

            if (kept == null) return;

            _cfg.CamberFront = kept[0];
            _cfg.CamberRear = kept[1];
            _cfg.TrackFront = kept[2];
            _cfg.TrackRear = kept[3];
            _cfg.HeightFront = kept[4];
            _cfg.HeightRear = kept[5];

            Log.Info("Stance: gave " + _model + " back the one it had (" +
                     kept[0].ToString("0.0") + " deg front, " + kept[2].ToString("0.00") + " m).");
        }

        /// <summary>
        /// Writes the stance down for this model, a moment after you stop changing it.
        ///
        /// NOT ON EVERY NUDGE. A slider held down on a D-pad moves twenty times a second and each
        /// one of those would rewrite the file; waiting for the numbers to sit still turns a
        /// held button into one write.
        /// </summary>
        private void Remember(int now)
        {
            if (!_cfg.StanceRemember || _store == null || _model == null) return;

            var live = new[]
            {
                _cfg.CamberFront, _cfg.CamberRear, _cfg.TrackFront,
                _cfg.TrackRear, _cfg.HeightFront, _cfg.HeightRear,
            };

            if (_pending == null || Moved(live, _pending))
            {
                _pending = live;
                _changedAt = now;
                return;
            }

            if (_changedAt == 0 || now - _changedAt < SettleMs) return;

            _changedAt = 0;
            _store.Put(_model, live);
        }

        private static bool Moved(float[] a, float[] b)
        {
            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                if (Math.Abs(a[i] - b[i]) >= 0.0005f) return true;
            }

            return false;
        }

        /// <summary>
        /// The model's own name, which is the only thing about a car that survives a save.
        ///
        /// The catalogue is 921 names and the hash of each is worked out once, on the first car
        /// of the session, rather than 921 times per car.
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

        /// <summary>Whether every slider is at nought, in which case there is nothing to do.</summary>
        private bool Flat()
        {
            return Math.Abs(_cfg.CamberFront) < Nothing && Math.Abs(_cfg.CamberRear) < Nothing &&
                   Math.Abs(_cfg.TrackFront) < Nothing && Math.Abs(_cfg.TrackRear) < Nothing &&
                   Math.Abs(_cfg.HeightFront) < Nothing && Math.Abs(_cfg.HeightRear) < Nothing;
        }

        /// <summary>
        /// Reads what this car came with, once, and refuses any wheel that does not read sanely.
        /// </summary>
        private void Capture(Vehicle car)
        {
            _stock.Clear();

            // EVERY WHEEL, ONCE, WITH ITS ADDRESS. Four numbers that mirror across the car --
            // one side negative, the other positive, both about half a track apart -- are proof
            // that 0x030 is the X offset on THIS build. Four numbers that do not are proof that
            // it is not, which is the other thing this could be and cannot be told apart from a
            // write that is simply ignored.
            var say = new System.Text.StringBuilder("Stance: ");

            foreach (var wheel in car.Wheels)
            {
                var at = wheel.MemoryAddress;

                if (at == IntPtr.Zero) continue;

                var camber = Read(at, Camber);
                var track = Read(at, Track);

                say.Append(wheel.BoneId).Append(" @").Append(at.ToString("X")).Append(" camber ")
                   .Append(camber.ToString("0.0000")).Append(" track ")
                   .Append(track.ToString("0.0000")).Append("; ");

                if (!Sound(camber) || !Sound(track))
                {
                    Log.Once("stance-address", "Wheel " + wheel.BoneId + " does not read like a " +
                                               "wheel (" + camber + ", " + track + "), so the " +
                                               "stance leaves it alone.");
                    continue;
                }

                var raise = 0f;

                try { raise = wheel.GetHydraulicSuspensionRaiseFactor(); }
                catch { /* nought is the right assumption and the right thing to put back */ }

                _stock[(int)wheel.BoneId] = new[] { camber, track, raise };
            }

            Log.Debug(say.ToString());
        }

        /// <summary>
        /// Writes every wheel this frame, because the game is writing over it every frame.
        /// </summary>
        private void Apply(Vehicle car)
        {
            foreach (var wheel in car.Wheels)
            {
                float[] stock;

                if (!_stock.TryGetValue((int)wheel.BoneId, out stock)) continue;

                var at = wheel.MemoryAddress;

                if (at == IntPtr.Zero) continue;

                var id = (int)wheel.BoneId;

                // FRONT IS THE FIRST AXLE AND EVERYTHING ELSE FOLLOWS THE REAR. A six-wheeler's
                // middle axle has no slider of its own, and the rear is the one it looks like.
                var front = id == (int)VehicleWheelBoneId.WheelLeftFront ||
                            id == (int)VehicleWheelBoneId.WheelRightFront;

                // Left is odd: 11 is the left front, 12 the right front, 13 the left rear.
                var side = (id & 1) == 1 ? 1f : -1f;

                var lean = (front ? _cfg.CamberFront : _cfg.CamberRear) * (float)(Math.PI / 180.0);
                var out_ = front ? _cfg.TrackFront : _cfg.TrackRear;
                var up = front ? _cfg.HeightFront : _cfg.HeightRear;

                var camber = stock[0] + side * lean;

                Write(at, Camber, camber);
                Write(at, CamberBack, -camber);
                Write(at, Track, stock[1] - side * out_);

                try { wheel.SetHydraulicSuspensionRaiseFactor(stock[2] + up); }
                catch { /* the one part of this the car is allowed to refuse */ }

                Say(wheel, stock, camber);
            }
        }

        /// <summary>Puts every wheel back the way it was read.</summary>
        private void Restore()
        {
            _applied = false;

            var car = _last;

            if (car == null) return;

            try
            {
                if (!car.Exists()) return;

                foreach (var wheel in car.Wheels)
                {
                    float[] stock;

                    if (!_stock.TryGetValue((int)wheel.BoneId, out stock)) continue;

                    var at = wheel.MemoryAddress;

                    if (at == IntPtr.Zero) continue;

                    Write(at, Camber, stock[0]);
                    Write(at, CamberBack, -stock[0]);
                    Write(at, Track, stock[1]);

                    try { wheel.SetHydraulicSuspensionRaiseFactor(stock[2]); }
                    catch { /* it was never taken, so there is nothing to give back */ }
                }
            }
            catch (Exception ex)
            {
                Log.Once("stance-restore", "Could not put the stance back: " + ex.Message);
            }
        }

        /// <summary>
        /// Lets go of the car without touching it.
        ///
        /// THE STANCE STAYS ON THE CAR. Restore is still there and still called when the sliders
        /// go back to nought, which is how you undo one -- but getting out, changing car, or the
        /// mod being reloaded no longer straightens anything up. That is what "permanently" asks
        /// for, and it is a deliberate exception to the rule the rest of this mod follows.
        /// </summary>
        private void Forget()
        {
            _applied = false;
            _car = 0;
            _last = null;
            _model = null;
            _pending = null;
            _changedAt = 0;
            _stock.Clear();
        }

        public void Release()
        {
            Forget();
        }

        /// <summary>
        /// Says once per car what the front left wheel was and what it became.
        ///
        /// THE SIGNS ARE THE ONE THING THAT CANNOT BE CHECKED FROM OUTSIDE THE GAME. If the
        /// camber leans the wrong way or the track pulls in instead of out, this line is what
        /// says so -- and the fix is a minus sign in the ini rather than a rebuild.
        /// </summary>
        private void Say(VehicleWheel wheel, float[] stock, float camber)
        {
            if (_said || wheel.BoneId != VehicleWheelBoneId.WheelLeftFront) return;

            _said = true;

            Log.Info("Stance: front left camber " + stock[0].ToString("0.0000") + " to " +
                     camber.ToString("0.0000") + " rad, track " + stock[1].ToString("0.0000") +
                     " to " + (stock[1] - _cfg.TrackFront).ToString("0.0000") + " m, raise " +
                     (stock[2] + _cfg.HeightFront).ToString("0.00") + ".");
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
