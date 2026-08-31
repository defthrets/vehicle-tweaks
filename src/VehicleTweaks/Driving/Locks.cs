using System;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;
using VehicleTweaks.Input;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Locking your car.
    ///
    /// WORTH MORE SINCE THE DOOR STARTED BEING LEFT OPEN, which is exactly the invitation it
    /// looks like. That is not a reason to undo the open door -- it is the choice the pair of
    /// them creates that is worth having: lock it and walk away, or leave it running with the
    /// door hanging open because you will only be a second and you can see it from the shop.
    ///
    /// THE HORN ANSWERS, and that is feedback which is also the feature. Locking is otherwise
    /// invisible: nothing about the car looks any different, so a key that locks it silently is
    /// a key you cannot tell you pressed. Every real car in forty years has answered the same
    /// way. A notification would have been this mod talking; a chirp is the car talking, which
    /// is the only voice it is meant to have.
    /// </summary>
    internal sealed class Locks
    {
        /// <summary>How far away his own car can be and still be the one he means, in metres.</summary>
        private const float Reach = 12f;

        private readonly Settings _cfg;
        private readonly Chord _chord;

        private bool _keyDown;

        public Locks(Settings cfg)
        {
            _cfg = cfg;
            _chord = new Chord(cfg.PadModifier, cfg.PadLock, "locking");

            Log.Info("Locking on " + cfg.LockKey + ", or on a pad " + _chord.Describe() + ".");
        }

        public void Update(Ped me)
        {
            if (!_cfg.Locking) return;

            try
            {
                if (!Pressed()) return;

                var car = Target(me);

                if (car == null)
                {
                    Log.Debug("Locking: nothing of yours within " + Reach + "m.");
                    return;
                }

                // CannotEnter, not Locked. SHVDN marks Locked obsolete and points here, and the
                // name is the better one anyway: it says what the state DOES rather than what it
                // is called, and the thing wanted is nobody getting in.
                var locked = car.LockStatus == VehicleLockStatus.CannotEnter;

                car.LockStatus = locked
                                     ? VehicleLockStatus.Unlocked
                                     : VehicleLockStatus.CannotEnter;

                if (_cfg.LockChirp) Chirp(car);

                Log.Debug("Locking: " + Name(car) + (locked ? " unlocked." : " locked."));
            }
            catch (Exception ex)
            {
                Log.Once("locks", "Could not work the locks: " + ex.Message);
            }
        }

        /// <summary>
        /// The car he means: the one he is in, or the last one he was in if he is stood by it.
        ///
        /// THE LAST ONE, NOT THE NEAREST ONE. Nearest would let this key lock a stranger's car
        /// that happened to be parked closer, which is not a thing a key in your pocket does.
        /// LastVehicle is the game's own memory of the car that is his.
        /// </summary>
        private static Vehicle Target(Ped me)
        {
            try
            {
                if (me == null || !me.Exists()) return null;

                var inside = me.CurrentVehicle;
                if (inside != null && inside.Exists()) return inside;

                var last = me.LastVehicle;
                if (last == null || !last.Exists() || last.IsDead) return null;

                return last.Position.DistanceTo(me.Position) <= Reach ? last : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// A blip of the horn. Eighty milliseconds, which is a chirp rather than a blast.
        ///
        /// The last argument is isCarAlarm, and it is false: this is a courtesy beep, not a
        /// vehicle alarm going off in the middle of the night with everything that follows.
        /// </summary>
        private static void Chirp(Vehicle car)
        {
            try { Function.Call(Hash.START_VEHICLE_HORN, car.Handle, 80, 0, false); }
            catch { /* the lock still happened */ }
        }

        private bool Pressed()
        {
            var key = false;

            try
            {
                var down = Game.IsKeyPressed(_cfg.LockKey);
                key = down && !_keyDown;
                _keyDown = down;
            }
            catch
            {
                _keyDown = false;
            }

            // Not short-circuited: Fired() is what advances the chord's own memory.
            var chord = _chord.Fired();

            return key || chord;
        }

        private static string Name(Vehicle v)
        {
            try { return v.LocalizedName; }
            catch { return "the car"; }
        }
    }
}
