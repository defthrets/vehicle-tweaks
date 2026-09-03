using System;
using GTA;
using GTA.Math;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The engine keeps pulling while the car is sideways.
    ///
    /// GTA BOGS A CAR DOWN THE MOMENT IT STOPS POINTING WHERE IT IS GOING, which is the thing
    /// that makes long drifts collapse: you get the back out, the power falls away underneath
    /// you, and the slide dies of its own accord rather than because you ended it. Holding a
    /// drift in this game is mostly a fight against the car quietly giving up.
    ///
    /// EnginePowerMultiplier is PER CAR and per moment -- not handling data, which is per MODEL
    /// and would change every other example of that car in the world for the session. This is
    /// the same distinction the front-wheel handbrake turns on, and it is the reason this is a
    /// small safe thing rather than a large dangerous one.
    ///
    /// THE SLIDE IS MEASURED, NOT GUESSED AT. The angle between where the car is pointing and
    /// where it is actually travelling is what a drift IS -- so it is worked out from those two
    /// directly, and the compensation comes in gradually as the angle opens up rather than
    /// switching on at a line. A car that suddenly found more power at twelve degrees would be
    /// harder to hold than one that never found any.
    /// </summary>
    internal sealed class Slides
    {
        /// <summary>Below this it is parking, not drifting, and the angle means nothing.</summary>
        private const float Least = 6f;

        /// <summary>Past this it is not a slide, it is reversing.</summary>
        private const float Backwards = 90f;

        private readonly Settings _cfg;

        private int _car;
        private bool _boosted;

        public Slides(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.DriftPower)
                {
                    Release();
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists())
                {
                    Release();
                    return;
                }

                if (car.Handle != _car)
                {
                    Release();
                    _car = car.Handle;
                }

                var angle = Slip(car);

                if (angle < _cfg.DriftAngle)
                {
                    // Straight again. The multiplier is a state left on the car, so it is put
                    // back rather than left to be noticed later as a car that feels quick.
                    if (_boosted) Restore(car);
                    return;
                }

                // RAMPED, not switched. Full compensation is reached at three times the angle
                // that counts as a slide, so the power arrives as the car goes further sideways
                // -- which is when it is being taken away.
                var over = (angle - _cfg.DriftAngle) / (_cfg.DriftAngle * 2f);

                if (over < 0f) over = 0f;
                if (over > 1f) over = 1f;

                car.EnginePowerMultiplier = 1f + (_cfg.DriftPowerBoost - 1f) * over;
                _boosted = true;
            }
            catch (Exception ex)
            {
                Release();
                Log.Once("slides", "Holding the power through a slide fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// How far sideways the car is, in degrees.
        ///
        /// The angle between the way it points and the way it is actually moving. Flattened,
        /// because a car going down a hill is not drifting and the height difference would say
        /// otherwise.
        /// </summary>
        private static float Slip(Vehicle car)
        {
            try
            {
                var travel = car.Velocity;
                var facing = car.ForwardVector;

                travel.Z = 0f;
                facing.Z = 0f;

                var speed = travel.Length();
                if (speed < Least) return 0f;

                travel.Normalize();
                facing.Normalize();

                var dot = Vector3.Dot(travel, facing);

                if (dot > 1f) dot = 1f;
                if (dot < -1f) dot = -1f;

                var angle = (float)(Math.Acos(dot) * 180.0 / Math.PI);

                // Reversing is not sliding, and it reads as almost a hundred and eighty degrees.
                return angle >= Backwards ? 0f : angle;
            }
            catch
            {
                return 0f;
            }
        }

        private void Restore(Vehicle car)
        {
            _boosted = false;

            try { car.EnginePowerMultiplier = 1f; }
            catch { /* the next frame will try again */ }
        }

        /// <summary>
        /// Hands the power back, wherever it was given.
        ///
        /// BY HANDLE, like every other override in here: stepping out of one car and into
        /// another has to put the first one's engine back to normal, and that car is no longer
        /// anybody's CurrentVehicle.
        /// </summary>
        public void Release()
        {
            if (!_boosted)
            {
                _car = 0;
                return;
            }

            var handle = _car;

            _boosted = false;
            _car = 0;

            try
            {
                var car = (Vehicle)Entity.FromHandle(handle);
                if (car != null && car.Exists()) car.EnginePowerMultiplier = 1f;
            }
            catch
            {
                // The car is gone, and the multiplier went with it.
            }
        }
    }
}
