using System;
using GTA;
using GTA.Math;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// What happens while the car is sideways: the engine keeps pulling, and there is more
    /// steering lock to catch it with.
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
    /// AND MORE LOCK TO CATCH IT WITH, which is the other half of the same problem. Catching a
    /// slide means winding on more opposite lock than the car came with, and once you run out
    /// there is nothing left to do but wait and see where it goes -- which is why GTA drifts
    /// feel like they end of their own accord rather than because you saved them. Extended
    /// steering angle is the single most common modification made to a real drift car, and for
    /// this exact reason.
    ///
    /// SteeringLimitMultiplier is per WHEEL and per car. Same small safe instrument, same
    /// distinction from handling data, which is per model and permanent for the session.
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

        /// <summary>Whether anything is currently being held on the car, so it can be given back.</summary>
        private bool _applied;

        public Slides(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.DriftPower && !_cfg.CounterSteer)
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
                    // Straight again. Both of these are states left on the car, so they are put
                    // back rather than left to be noticed later as a car that feels quick and
                    // steers oddly.
                    if (_applied) Restore(car);
                    return;
                }

                // RAMPED, not switched, so both arrive as the car goes further sideways --
                // which is when the power is being taken away and when the lock is running out.
                // A car that suddenly found more of either at one particular angle would be
                // harder to hold, not easier.
                //
                // A RAMP EACH, because they are not the same question. The power is trying to
                // undo something the game does to a sideways car, so it follows the game's
                // problem: full by three times the angle that counts as a slide. The lock is
                // trying to give YOU something, so where it arrives is a matter of taste, and
                // taste is what a setting is for.
                if (_cfg.DriftPower)
                {
                    var over = Ramp(angle, _cfg.DriftAngle, _cfg.DriftAngle * 3f);
                    car.EnginePowerMultiplier = 1f + (_cfg.DriftPowerBoost - 1f) * over;
                }

                if (_cfg.CounterSteer)
                {
                    var over = Ramp(angle, _cfg.DriftAngle, _cfg.CounterSteerFull);
                    Lock(car, 1f + (_cfg.CounterSteerLock - 1f) * over);
                }

                _applied = true;
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

        /// <summary>
        /// Nought at one angle, one at another, and a straight line between them.
        ///
        /// GUARDED AGAINST THE TWO ANGLES MEETING, because both are settings and nothing stops
        /// somebody dragging the full-lock angle below the one that counts as a slide. Divided
        /// out that way it is an infinity, which reaches the steering as a NaN and a car that
        /// does not steer at all.
        /// </summary>
        private static float Ramp(float angle, float from, float to)
        {
            if (to <= from) return angle >= from ? 1f : 0f;

            var over = (angle - from) / (to - from);

            if (over < 0f) return 0f;
            return over > 1f ? 1f : over;
        }

        /// <summary>
        /// The steering wheels, given more lock.
        ///
        /// ASKED WHICH WHEELS STEER rather than assuming the front two. Most things in this game
        /// steer with the front wheels and some do not, and IsSteeringWheel is the game's own
        /// answer per wheel -- the same reasoning as reading IsDrivingWheel to find a
        /// front-driver instead of working it out from the handling numbers.
        /// </summary>
        private static void Lock(Vehicle car, float multiplier)
        {
            try
            {
                foreach (var wheel in car.Wheels.GetAllWheels())
                {
                    if (wheel != null && wheel.IsSteeringWheel) wheel.SteeringLimitMultiplier = multiplier;
                }
            }
            catch
            {
                // Something without wheels, or without those wheels. Nothing to steer with.
            }
        }

        private void Restore(Vehicle car)
        {
            _applied = false;

            try { car.EnginePowerMultiplier = 1f; }
            catch { /* the next frame will try again */ }

            Lock(car, 1f);
        }

        /// <summary>
        /// Hands the power and the steering back, wherever they were given.
        ///
        /// BY HANDLE, like every other override in here: stepping out of one car and into
        /// another has to put the first one's engine back to normal, and that car is no longer
        /// anybody's CurrentVehicle.
        /// </summary>
        public void Release()
        {
            if (!_applied)
            {
                _car = 0;
                return;
            }

            var handle = _car;

            _applied = false;
            _car = 0;

            try
            {
                var car = (Vehicle)Entity.FromHandle(handle);
                if (car == null || !car.Exists()) return;

                car.EnginePowerMultiplier = 1f;
                Lock(car, 1f);
            }
            catch
            {
                // The car is gone, and both went with it.
            }
        }
    }
}
