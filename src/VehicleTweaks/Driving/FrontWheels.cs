using System;
using GTA;
using GTA.Math;
using VehicleTweaks.Core;

// Both namespaces have a Control and only one of them is a game control.
using Control = GTA.Control;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Front-wheel-drive cars keep pulling when the handbrake is on.
    ///
    /// A handbrake is a REAR brake. That is not a detail, it is the whole of what one is: a cable
    /// to the back wheels, which is why it locks the rear and why a rear-drive car spins on it.
    /// In a front-driver the driven wheels are the ones it does not touch, so you can sit with it
    /// up and the engine still dragging the car forward -- and that is the thing this restores.
    /// The game brakes the car as a unit and the fronts give up with everything else.
    ///
    /// BY PUSHING, NOT BY EDITING THE HANDLING, and that distinction is the reason this is safe
    /// enough to ship. HandlingData is right there and it has a HandBrakeForce on it, and turning
    /// that down would look like the obvious fix. It is not: handling is loaded PER MODEL, not
    /// per car, so weakening it would weaken the handbrake on every other example of that model
    /// in the world -- traffic, parked cars, the one the police are chasing you in -- and it
    /// would stay weakened for the rest of the session, with nothing to put it back.
    ///
    /// A force applied while a key is held lasts exactly as long as the key is held, affects
    /// exactly the car under the player, and is gone the moment this script stops running. It is
    /// the smaller instrument and it is the one that cannot leave a mess behind.
    ///
    /// THE STRENGTH IS AN ACCELERATION, in metres per second squared, and that is not a
    /// presentational choice. SHVDN rejects the obvious force type as "incorrect" and points at
    /// an IMPULSE instead -- and an impulse is mass times a change in velocity, which means the
    /// number to apply is the car's own mass times the pull you want times the length of the
    /// frame. Do that and the setting stops being a magic figure tuned by feel: 1.5 means the
    /// fronts drag the car at one and a half metres per second squared, and it means the same
    /// thing in a hatchback and in a van.
    /// </summary>
    internal sealed class FrontWheels
    {
        private readonly Settings _cfg;

        /// <summary>Which car was last looked at, and whether it drives its front wheels only.</summary>
        private int _car;
        private bool _fwd;

        private bool _said;

        public FrontWheels(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Vehicle car, bool driving)
        {
            if (!_cfg.FwdHandbrake) return;

            try
            {
                if (!driving || car == null || !car.Exists())
                {
                    _car = 0;
                    return;
                }

                if (car.Handle != _car)
                {
                    _car = car.Handle;
                    _fwd = FrontDriven(car);
                    _said = false;
                }

                if (!_fwd) return;

                // Both, held. The handbrake on its own should still stop the car dead -- that is
                // what it is for. This is only about what happens when you ask for drive at the
                // same time, which in a front-driver is a thing the car can actually do.
                if (!Held(Control.VehicleHandbrake) || !Held(Control.VehicleAccelerate)) return;

                if (!Running(car)) return;

                // THE FRONTS HAVE TO BE ON THE GROUND TO PULL ANYTHING. A wheel in the air has no
                // grip to put the engine's torque through, and a car being shoved forward while
                // its nose is up in the air is a mod moving something that should not move.
                if (!Gripping(car)) return;

                var speed = Speed(car);
                if (speed >= _cfg.FwdHandbrakeMaxSpeed) return;

                // Eased off as it approaches the limit rather than cut at it. A force that stops
                // dead at a threshold is a lurch you can feel; the real thing simply runs out of
                // ability to drag a locked axle any faster.
                var taper = 1f - speed / _cfg.FwdHandbrakeMaxSpeed;
                if (taper < 0f) taper = 0f;

                Pull(car, _cfg.FwdHandbrakePull * taper);

                if (_said) return;

                _said = true;
                Log.Debug("Front wheels: " + Name(car) + " drives its fronts only, so they keep " +
                          "pulling against the handbrake.");
            }
            catch (Exception ex)
            {
                Log.Once("front-wheels", "The handbrake handling fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Forward, in the car's own axes, as an impulse worked out rather than guessed.
        ///
        /// IMPULSE IS MASS TIMES A CHANGE IN VELOCITY. So to pull at a given acceleration for one
        /// frame the impulse is the car's mass times that acceleration times the length of the
        /// frame -- and applying that every frame accumulates to exactly that acceleration, at
        /// any frame rate and in any car. A number picked by feel would have been right for one
        /// hatchback on one machine.
        ///
        /// The mass is READ from the handling and never written. Reading is per car and harmless;
        /// writing is per model and lasts the session, which is the whole reason this feature
        /// pushes the car rather than turning its handbrake down.
        /// </summary>
        private static void Pull(Vehicle car, float accel)
        {
            try
            {
                var dt = Game.LastFrameTime;

                // A hitch or an alt-tab hands back a nonsense delta, and one frame of that would
                // be a shove rather than a pull.
                if (dt <= 0f || dt > 0.1f) dt = 1f / 60f;

                var impulse = Mass(car) * accel * dt;

                // Through the middle, not at the front axle. The turn a front-driver makes on the
                // handbrake comes from the rear being locked while the front is not, which the
                // game's own physics already produces -- adding a lever arm of our own would be
                // inventing a second source of the same rotation and then fighting the first.
                //
                // InternalImpulse because SHVDN marks the obvious one obsolete and says outright
                // that it is incorrect. Internal is the right word for it too: this is the car
                // driving itself, not something happening to it from outside.
                car.ApplyForceRelative(new Vector3(0f, impulse, 0f), Vector3.Zero,
                                       ForceType.InternalImpulse);
            }
            catch
            {
                // The next frame will try again, or it will not.
            }
        }

        /// <summary>The car's own mass, or an ordinary one if it will not say.</summary>
        private static float Mass(Vehicle car)
        {
            try
            {
                var m = car.HandlingData.Mass;
                return m > 1f ? m : 1500f;
            }
            catch
            {
                return 1500f;
            }
        }

        /// <summary>
        /// Whether this car drives its front wheels and only its front wheels.
        ///
        /// ASKED OF THE WHEELS, not worked out from the handling data. DriveBiasFront is right
        /// there and it is a number whose meaning has to be assumed: the value in handling.meta
        /// is not the value the game keeps in memory, and getting that mapping backwards would
        /// mean this feature applied to every rear-driver and to nothing else. IsDrivingWheel is
        /// the game's own answer to the actual question, per wheel, per car.
        ///
        /// Four-wheel drive is not this. Its rear wheels ARE driven, so a handbrake locking them
        /// is a different situation with a different answer, and guessing at that answer is not
        /// what was asked for.
        /// </summary>
        private static bool FrontDriven(Vehicle car)
        {
            try
            {
                var wheels = car.Wheels;

                var frontLeft = wheels[VehicleWheelBoneId.WheelLeftFront];
                var frontRight = wheels[VehicleWheelBoneId.WheelRightFront];
                var rearLeft = wheels[VehicleWheelBoneId.WheelLeftRear];
                var rearRight = wheels[VehicleWheelBoneId.WheelRightRear];

                if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
                {
                    return false;
                }

                var front = frontLeft.IsDrivingWheel || frontRight.IsDrivingWheel;
                var rear = rearLeft.IsDrivingWheel || rearRight.IsDrivingWheel;

                return front && !rear;
            }
            catch
            {
                // A bike, a boat, something with no wheels where these are looked for. Not a
                // front-driver as far as this is concerned.
                return false;
            }
        }

        /// <summary>At least one front wheel on something it can push against.</summary>
        private static bool Gripping(Vehicle car)
        {
            try
            {
                var wheels = car.Wheels;

                return wheels[VehicleWheelBoneId.WheelLeftFront].IsTouchingSurface ||
                       wheels[VehicleWheelBoneId.WheelRightFront].IsTouchingSurface;
            }
            catch
            {
                return false;
            }
        }

        private static bool Held(Control control)
        {
            try { return Game.IsControlPressed(control); }
            catch { return false; }
        }

        private static bool Running(Vehicle v)
        {
            try { return v.IsEngineRunning; }
            catch { return false; }
        }

        private static float Speed(Vehicle v)
        {
            try { return v.Speed; }
            catch { return 0f; }
        }

        private static string Name(Vehicle v)
        {
            try { return v.LocalizedName; }
            catch { return "the car"; }
        }
    }
}
