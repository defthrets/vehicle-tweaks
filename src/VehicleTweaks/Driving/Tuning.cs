using System;
using GTA;
using GTA.Math;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Everything written to the engine, the steering and the tyres of the car being driven.
    ///
    /// ONE OWNER FOR EACH FIELD, WHICH IS THE WHOLE REASON THIS IS ONE CLASS. It began as the
    /// slide compensation alone, and then a power multiplier was wanted as a plain setting -- on
    /// the same field. Two features writing EnginePowerMultiplier is not a merge conflict, it is
    /// a car that flickers between two numbers depending on which of them ran last this frame,
    /// and the symptom is a car that feels inconsistent rather than an error anybody can find.
    /// This mod has already paid for that lesson twice: the slide angle, and the handbrake the
    /// front wheels were reading off the wrong button.
    ///
    /// SO THEY COMPOSE INSTEAD OF COMPETING. The sliders are the baseline -- what the car is like
    /// before anything happens -- and the slide compensation MULTIPLIES it. A car set to 1.5
    /// power that finds another 1.4 while sideways is at 2.1, which is what both settings said
    /// they would do. Neither has to know about the other; there is simply one place that adds up.
    ///
    /// NONE OF IT IS HANDLING DATA. Every field here is per CAR and per moment. HandlingData is
    /// per MODEL and permanent for the session -- write to it and every other example of that car
    /// in the world changes, and stays changed until the game is restarted. That is the difference
    /// between a small safe instrument and a large dangerous one, and it is why this mod tunes
    /// with multipliers rather than by editing the numbers a car was built with.
    ///
    /// AND IT ALL GOES BACK, BY HANDLE. Stepping straight out of one car into another has to
    /// return the first one to standard, and by then it is not anybody's CurrentVehicle -- a car
    /// left on three times power with the steering of a shopping trolley is a car nothing on
    /// screen explains.
    /// </summary>
    internal sealed class Tuning
    {
        /// <summary>Below this it is parking, not drifting, and the angle means nothing.</summary>
        private const float Least = 6f;

        /// <summary>Past this it is not a slide, it is reversing.</summary>
        private const float Backwards = 90f;

        /// <summary>Near enough to standard that writing it would be writing nothing.</summary>
        private const float Nothing = 0.001f;

        private readonly Settings _cfg;

        private int _car;

        /// <summary>Whether anything is currently being held on the car, so it can be given back.</summary>
        private bool _applied;

        /// <summary>Whether WE took the burst out of the tyres, so only our own is given back.</summary>
        private bool _tyres;
        private bool _wheels;

        public Tuning(Settings cfg)
        {
            _cfg = cfg;
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
                    Release();
                    _car = car.Handle;
                }

                var angle = Slip(car);

                var power = _cfg.PowerMultiplier;
                var torque = _cfg.TorqueMultiplier;
                var steering = _cfg.SteeringLock;

                // THE SLIDE COMPENSATION, ON TOP. Ramped rather than switched, so it arrives as
                // the car goes further sideways -- which is when the power is being taken away
                // and when the lock is running out. A car that suddenly found more of either at
                // one particular angle would be harder to hold, not easier.
                //
                // A RAMP EACH, because they are not the same question. The power is undoing
                // something the game does to a sideways car, so it follows the game's problem:
                // full by three times the angle that counts as a slide. The lock is giving the
                // driver something, so where it arrives is taste, and taste is what a setting is
                // for.
                if (angle >= _cfg.DriftAngle)
                {
                    if (_cfg.DriftPower)
                    {
                        power *= 1f + (_cfg.DriftPowerBoost - 1f) *
                                 Ramp(angle, _cfg.DriftAngle, _cfg.DriftAngle * 3f);
                    }

                    if (_cfg.CounterSteer)
                    {
                        steering *= 1f + (_cfg.CounterSteerLock - 1f) *
                                    Ramp(angle, _cfg.DriftAngle, _cfg.CounterSteerFull);
                    }
                }

                // NOTHING TO SAY, SO NOTHING SAID. Standard multipliers and no tyre settings mean
                // this has no opinion about the car, and a class with no opinion should not be
                // writing five fields a frame to express it.
                if (Standard(power) && Standard(torque) && Standard(steering) &&
                    !_cfg.TyresNeverBurst && !_cfg.WheelsNeverBreak)
                {
                    if (_applied) Restore(car);
                    return;
                }

                car.EnginePowerMultiplier = power;
                car.EngineTorqueMultiplier = torque;

                Lock(car, steering);
                Tyres(car);

                _applied = true;
            }
            catch (Exception ex)
            {
                Release();
                Log.Once("tuning", "Tuning the car fell over: " + ex.Message);
            }
        }

        private static bool Standard(float value)
        {
            return value > 1f - Nothing && value < 1f + Nothing;
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
        /// The steering wheels, given their lock.
        ///
        /// ASKED WHICH WHEELS STEER rather than assuming the front two. Most things in this game
        /// steer with the front wheels and some do not, and IsSteeringWheel is the game's own
        /// answer per wheel -- the same reasoning as reading IsDrivingWheel to find a front-driver
        /// instead of working it out from the handling numbers.
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

        /// <summary>
        /// What the tyres and wheels are allowed to do to themselves.
        ///
        /// TRACKED SEPARATELY FROM THE MULTIPLIERS because they are the opposite shape: a
        /// multiplier of one IS the standard value, so writing it costs nothing and means
        /// nothing. These are flags whose standard value is TRUE, so a class that set them back
        /// to true on every car it saw would be quietly re-arming tyres that some other mod had
        /// deliberately disarmed. Only what we turned off gets turned back on.
        /// </summary>
        private void Tyres(Vehicle car)
        {
            try
            {
                if (_cfg.TyresNeverBurst)
                {
                    car.CanTiresBurst = false;
                    _tyres = true;
                }
                else if (_tyres)
                {
                    car.CanTiresBurst = true;
                    _tyres = false;
                }

                if (_cfg.WheelsNeverBreak)
                {
                    car.CanWheelsBreak = false;
                    _wheels = true;
                }
                else if (_wheels)
                {
                    car.CanWheelsBreak = true;
                    _wheels = false;
                }
            }
            catch
            {
                // The next frame will try again.
            }
        }

        private void Restore(Vehicle car)
        {
            _applied = false;

            try
            {
                car.EnginePowerMultiplier = 1f;
                car.EngineTorqueMultiplier = 1f;

                if (_tyres) { car.CanTiresBurst = true; _tyres = false; }
                if (_wheels) { car.CanWheelsBreak = true; _wheels = false; }
            }
            catch
            {
                // The release by handle is the other chance.
            }

            Lock(car, 1f);
        }

        /// <summary>
        /// Hands the engine, the steering and the tyres back, wherever they were given.
        ///
        /// BY HANDLE, like every other override in here: stepping out of one car and into another
        /// has to put the first one's engine back to standard, and that car is no longer
        /// anybody's CurrentVehicle by the time anyone thinks to.
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

                Restore(car);
            }
            catch
            {
                // The car is gone, and all of it went with it.
            }
        }
    }
}
