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
    /// THE STRENGTH IS AN ACCELERATION, in metres per second squared, so the setting means
    /// something rather than being a figure tuned by feel on one car.
    ///
    /// IT DOES NOT GET MULTIPLIED BY THE CAR'S MASS, and the first version of this did. The
    /// reasoning was that SHVDN calls this force type an IMPULSE, an impulse is mass times a
    /// change in velocity, so the number to send is mass times the wanted acceleration times the
    /// frame. Every step of that is correct physics and the conclusion was wrong, because the
    /// native does not take newton-seconds: it takes the velocity change directly and does the
    /// dividing itself. Multiplying by fifteen hundred kilograms therefore asked for fifteen
    /// hundred times the pull -- about two hundred and thirty g -- and the car left like a
    /// rocket.
    ///
    /// The lesson is not about physics. It is that a name in somebody else's API is not a
    /// specification of its units, and reasoning confidently from one produces an answer that
    /// looks principled and is out by three orders of magnitude. The clamp below exists because
    /// this is the second force in this file to be wrong and the failure is always the same
    /// shape: too much, instantly, in one frame.
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
        /// The most speed one frame of this is ever allowed to add, in metres a second.
        ///
        /// A QUARTER OF A METRE A SECOND is fifteen metres a second squared at sixty frames --
        /// already brisker than most cars accelerate, and nothing this feature does should ever
        /// need it. It is not a tuning value, it is a ceiling: the previous version of this
        /// asked for thirty-seven metres a second in a single frame and the car left like a
        /// rocket, and no arithmetic mistake in here should be able to do that again.
        /// </summary>
        private const float MostPerFrame = 0.25f;

        /// <summary>
        /// Forward, in the car's own axes, as a change in velocity.
        ///
        /// THE NATIVE TAKES THE VELOCITY CHANGE, not a force in newtons and not an impulse in
        /// newton-seconds -- it does the dividing by mass itself. So one frame of pulling at a
        /// given acceleration is simply that acceleration times the length of the frame, and
        /// applying it every frame accumulates to exactly that acceleration at any frame rate.
        /// Which also means the setting is still honestly in metres per second squared, and
        /// still means the same thing in a hatchback and a van -- that part was never the
        /// problem, the mass factor in front of it was.
        /// </summary>
        private static void Pull(Vehicle car, float accel)
        {
            try
            {
                var dt = Game.LastFrameTime;

                // A hitch or an alt-tab hands back a nonsense delta, and one frame of that would
                // be a shove rather than a pull.
                if (dt <= 0f || dt > 0.1f) dt = 1f / 60f;

                var delta = accel * dt;
                if (delta > MostPerFrame) delta = MostPerFrame;

                // Through the middle, not at the front axle. The turn a front-driver makes on the
                // handbrake comes from the rear being locked while the front is not, which the
                // game's own physics already produces -- adding a lever arm of our own would be
                // inventing a second source of the same rotation and then fighting the first.
                //
                // InternalImpulse because SHVDN marks the obvious one obsolete and says outright
                // that it is incorrect. Internal is the right word for it too: this is the car
                // driving itself, not something happening to it from outside.
                car.ApplyForceRelative(new Vector3(0f, delta, 0f), Vector3.Zero,
                                       ForceType.InternalImpulse);
            }
            catch
            {
                // The next frame will try again, or it will not.
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
