using System;
using GTA;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The car you are driving puts itself back together every few minutes.
    ///
    /// OFF, LIKE EVERYTHING HERE THAT REMOVES A CONSEQUENCE RATHER THAN ADDING ONE. The rest of
    /// this mod corrects a car that still behaves the way you expect; this one undoes the last
    /// few minutes of your driving. That is a choice about how the game should treat you, and it
    /// should be made deliberately rather than found.
    ///
    /// THE CLOCK RESTARTS WHETHER OR NOT ANYTHING WAS REPAIRED, and that is the whole difference
    /// between a repair every few minutes and invincibility. Left to run on, an undamaged car
    /// would sit there with its timer already expired and fix the very next scrape a frame after
    /// it happened -- the same code, and a completely different game. Damage is meant to last up
    /// to the interval; that IS the interval.
    ///
    /// IT LOOKS BEFORE IT ACTS. SET_VEHICLE_FIXED snaps the bodywork straight in one frame, which
    /// on an undamaged car is a small visible jolt in return for nothing, so it is only called
    /// when there is something to undo. That also means the log line is true when it appears.
    ///
    /// A WRECK IS LEFT AS A WRECK. Repairing a dead car does not repair it, it resurrects it --
    /// engine running, flames out, upright again -- and nobody asking for their scratches buffed
    /// out is asking for that. If it burned, it stays burned.
    ///
    /// The car being DRIVEN, and only that one. Everything else in the world is somebody else's,
    /// and a mod that quietly restored every vehicle near the player would be a different and
    /// much stranger feature.
    /// </summary>
    internal sealed class Repairs
    {
        /// <summary>What the game counts as undamaged, on all three of its healths.</summary>
        private const float Whole = 1000f;

        private readonly Settings _cfg;

        private int _car;
        private int _since;

        public Repairs(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.Repairs)
                {
                    _car = 0;
                    _since = 0;
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists() || !AtTheWheel(car, me))
                {
                    // THE CLOCK IS PER CAR AND PER SITTING. Getting out and back in starts the
                    // interval again rather than banking the time spent standing next to it,
                    // which would repair the car a moment after getting in and make the setting
                    // read as "on".
                    _car = 0;
                    _since = 0;
                    return;
                }

                if (car.Handle != _car)
                {
                    _car = car.Handle;
                    _since = Game.GameTime;
                    return;
                }

                if (Game.GameTime - _since < (int)(_cfg.RepairMinutes * 60000f)) return;

                _since = Game.GameTime;

                if (Dead(car) || !Damaged(car)) return;

                car.Repair();

                Log.Debug("Repairs: " + Name(car) + " put back together.");
            }
            catch (Exception ex)
            {
                Log.Once("repairs", "The repairs fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Whether there is anything to put right.
        ///
        /// THREE QUESTIONS, BECAUSE THE GAME ANSWERS THEM SEPARATELY. Decals are the bullet
        /// holes and scrapes, and say nothing about a dead engine; the healths cover the dents
        /// and the mechanicals, and say nothing about a flat tyre. A car can be mechanically
        /// perfect and shot to pieces, or straight and running on a burst rear.
        ///
        /// IsDamaged IS NOT A FOURTH. It was in here as one until SHVDN pointed out it is
        /// obsolete and means HasDamageDecals -- the same question, asked twice, dressed as the
        /// general one. That is worth a note rather than a silent deletion: the name promises a
        /// summary of the car's condition and it is nothing of the sort.
        /// </summary>
        private static bool Damaged(Vehicle car)
        {
            try
            {
                if (car.HasDamageDecals) return true;

                if (car.BodyHealth < Whole || car.EngineHealth < Whole ||
                    car.PetrolTankHealth < Whole)
                {
                    return true;
                }

                foreach (var wheel in car.Wheels.GetAllWheels())
                {
                    if (wheel != null && (wheel.IsPunctured || wheel.IsBursted)) return true;
                }

                return false;
            }
            catch
            {
                // Something without wheels, or without those properties. Nothing provable, so
                // nothing done: an unnecessary repair is a jolt, and a missed one is a car that
                // is already fine.
                return false;
            }
        }

        private static bool Dead(Vehicle car)
        {
            try { return car.IsDead; }
            catch { return true; }
        }

        private static bool AtTheWheel(Vehicle car, Ped me)
        {
            try
            {
                var driver = car.Driver;
                return driver != null && driver.Exists() && driver.Handle == me.Handle;
            }
            catch
            {
                return false;
            }
        }

        private static string Name(Vehicle car)
        {
            try { return car.DisplayName + " (" + car.Handle + ")"; }
            catch { return "the car"; }
        }
    }
}
