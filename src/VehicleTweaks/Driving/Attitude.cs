using System;
using GTA;
using GTA.Math;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// What the car is actually doing: how far sideways it is, and how far the tyres are
    /// outrunning the road.
    ///
    /// HERE RATHER THAN IN EACH FEATURE, because three of them now ask these two questions and
    /// they must not be able to disagree. The slide angle governs the power, the steering lock
    /// AND the gearbox; if the gearbox worked it out with its own slightly different maths, then
    /// somewhere in the middle there is an angle at which the engine has found more power and
    /// the box has decided the car is straight. That is the kind of bug that is felt rather than
    /// seen, and never gets found.
    ///
    /// BOTH ARE MEASURED, NEITHER IS INFERRED FROM THE CONTROLS. A throttle position says
    /// nothing about whether the tyres have let go, and a steering angle says nothing about
    /// which way the car is travelling. These read the car.
    /// </summary>
    internal static class Attitude
    {
        /// <summary>Below this it is parking, not drifting, and the angle means nothing.</summary>
        private const float Least = 6f;

        /// <summary>Past this it is not a slide, it is reversing.</summary>
        private const float Backwards = 90f;

        /// <summary>
        /// How far sideways it is, in degrees, or nought when the question does not apply.
        ///
        /// The angle between the way it points and the way it is actually moving, which is what
        /// a drift IS. Flattened, because a car going down a hill is not drifting and the height
        /// difference would say otherwise.
        /// </summary>
        public static float Sideways(Vehicle car)
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
        /// How much faster the tyres are turning than the road is going by, in metres a second.
        /// Nought when they are gripping.
        ///
        /// THIS IS WHAT WHEELSPIN IS, stated directly rather than guessed at from the throttle.
        /// Both are taken as magnitudes because reverse makes one or both negative and the
        /// difference between two signed numbers pointing backwards is not a slip.
        /// </summary>
        public static float Wheelspin(Vehicle car)
        {
            try
            {
                var wheels = Math.Abs(car.WheelSpeed);
                var road = Math.Abs(car.Speed);

                var slip = wheels - road;
                return slip > 0f ? slip : 0f;
            }
            catch
            {
                return 0f;
            }
        }
    }
}
