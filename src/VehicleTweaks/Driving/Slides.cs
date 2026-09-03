using System;
using GTA;

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
    /// THE SLIDE IS MEASURED, NOT GUESSED AT -- and measured in ONE place, Attitude, which the
    /// gearbox reads too. The angle between where the car is pointing and where it is actually
    /// travelling is what a drift IS, and if these two features worked it out separately there
    /// would be an angle somewhere in the middle at which the engine has found more power and
    /// the gearbox has decided the car is straight.
    ///
    /// The compensation comes in gradually as the angle opens rather than switching on at a
    /// line. A car that suddenly found more power at twelve degrees would be harder to hold
    /// than one that never found any.
    /// </summary>
    internal sealed class Slides
    {
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

                var angle = Attitude.Sideways(car);

                if (angle < _cfg.DriftAngle)
                {
                    // Straight again. Both of these are states left on the car, so they are put
                    // back rather than left to be noticed later as a car that feels quick and
                    // steers oddly.
                    if (_applied) Restore(car);
                    return;
                }

                // RAMPED, not switched. Full effect is reached at three times the angle that
                // counts as a slide, so both arrive as the car goes further sideways -- which is
                // when the power is being taken away and when the lock is running out.
                var over = (angle - _cfg.DriftAngle) / (_cfg.DriftAngle * 2f);

                if (over < 0f) over = 0f;
                if (over > 1f) over = 1f;

                if (_cfg.DriftPower)
                {
                    // BOTH MULTIPLIERS, because they are different halves of the engine and a
                    // slide needs the other one. Power is the top end; TORQUE is what is
                    // available down low, which is what actually keeps the back out when the
                    // revs have fallen into the middle of the range. Boosting power alone was
                    // asking the engine for help in the one place a sideways car never is.
                    var found = 1f + (_cfg.DriftPowerBoost - 1f) * over;

                    car.EnginePowerMultiplier = found;
                    car.EngineTorqueMultiplier = found;
                }

                if (_cfg.CounterSteer)
                {
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

            try
            {
                car.EnginePowerMultiplier = 1f;
                car.EngineTorqueMultiplier = 1f;
            }
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
                car.EngineTorqueMultiplier = 1f;
                Lock(car, 1f);
            }
            catch
            {
                // The car is gone, and both went with it.
            }
        }
    }
}
