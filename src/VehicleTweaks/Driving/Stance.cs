using System;
using GTA;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Camber, track width and ride height, per axle -- what VStancer does, done a different way.
    ///
    /// NOT BY WRITING MEMORY, WHICH IS THE WHOLE POINT. VStancer finds each wheel's structure in
    /// the game's memory and writes floats at fixed byte offsets. That works, and it is why the
    /// mod has to be rebuilt for every game update and why Enhanced needed its own version: an
    /// offset is only correct for the executable it was measured against, and a wrong one writes
    /// a float into whatever else happens to be at that address. I have no way to measure offsets
    /// for this build, and guessing at them would be the one change in this mod whose failure is
    /// a crash rather than a setting that does nothing.
    ///
    /// SO IT GOES THROUGH THE BONES INSTEAD. Camber is the Y rotation of a wheel bone in the
    /// car's local space and track width is its X offset -- that is what those numbers ARE, not
    /// an approximation of them -- and EntityBone.PoseRotation and EntityBone.Pose are both
    /// settable through the ordinary API. Checked by reflection like everything else here, and
    /// version-independent by construction, because a property is not an address.
    ///
    /// READ, CHANGE ONE AXIS, WRITE BACK. The bone's pose is also where the wheel's SPIN lives.
    /// Writing a whole pose matrix built from camber alone would be a car whose wheels no longer
    /// turn -- so the rotation is read first, only Y is replaced, and the rest goes back
    /// untouched. Same for the translation: X and Z are ours, Y is left alone.
    ///
    /// EVERY FRAME, because the game poses the skeleton every frame and would undo it otherwise.
    /// That is the same problem VStancer solves by patching the game's height-reset code, which
    /// is not a thing a script should be doing; writing it again is.
    ///
    /// PER AXLE RATHER THAN PER WHEEL, which is both what stance actually is and what VStancer's
    /// own menu offers. The two sides of an axle are mirrored here rather than set separately --
    /// a car with one wheel cambered is not a stance, it is an accident.
    /// </summary>
    internal sealed class Stance
    {
        /// <summary>The wheel bones, by the names the game gives them.</summary>
        private const string FrontLeft = "wheel_lf";
        private const string FrontRight = "wheel_rf";
        private const string RearLeft = "wheel_lr";
        private const string RearRight = "wheel_rr";

        /// <summary>Near enough to nothing that writing it would be writing nothing.</summary>
        private const float Nothing = 0.0005f;

        private readonly Settings _cfg;

        private int _car;
        private bool _applied;

        /// <summary>Said once per car, so the log can show whether the bones moved at all.</summary>
        private bool _said;

        public Stance(Settings cfg)
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
                    _said = false;
                }

                if (Flat())
                {
                    if (_applied) Restore(car);
                    return;
                }

                // MIRRORED, NOT COPIED. The two sides of an axle lean and move in opposite
                // directions in the car's own coordinates, so one slider has to become two
                // opposite numbers or a "wider track" is one wheel out and one wheel in.
                Bone(car, FrontLeft, _cfg.CamberFront, _cfg.TrackFront, _cfg.HeightFront, 1f);
                Bone(car, FrontRight, _cfg.CamberFront, _cfg.TrackFront, _cfg.HeightFront, -1f);
                Bone(car, RearLeft, _cfg.CamberRear, _cfg.TrackRear, _cfg.HeightRear, 1f);
                Bone(car, RearRight, _cfg.CamberRear, _cfg.TrackRear, _cfg.HeightRear, -1f);

                _applied = true;
            }
            catch (Exception ex)
            {
                Release();
                Log.Once("stance", "The stance fell over: " + ex.Message);
            }
        }

        /// <summary>Whether every one of the six is at nothing, in which case there is nothing to do.</summary>
        private bool Flat()
        {
            return Small(_cfg.CamberFront) && Small(_cfg.CamberRear) &&
                   Small(_cfg.TrackFront) && Small(_cfg.TrackRear) &&
                   Small(_cfg.HeightFront) && Small(_cfg.HeightRear);
        }

        private static bool Small(float value)
        {
            return value > -Nothing && value < Nothing;
        }

        /// <summary>
        /// One wheel bone, leaned and moved.
        ///
        /// THE READ IS NOT A FORMALITY. A wheel bone's pose carries its spin, and this is called
        /// sixty times a second -- so a pose written from scratch is a wheel that never turns
        /// again. Only the one number each is replaced.
        /// </summary>
        private void Bone(Vehicle car, string name, float camber, float track, float height, float side)
        {
            try
            {
                var bone = car.Bones[name];

                if (bone == null || !bone.IsValid)
                {
                    // A vehicle without that wheel. Nothing to lean.
                    return;
                }

                var before = bone.PoseRotation;

                var rotation = before;
                rotation.Y = camber * side;
                bone.PoseRotation = rotation;

                var pose = bone.Pose;
                pose.X = track * side;
                pose.Z = height;
                bone.Pose = pose;

                if (_said) return;

                _said = true;

                // WHETHER THE BONE ACTUALLY MOVED, once per car. There is no other way to tell a
                // pose the game accepted from one it overwrote a frame later, and the whole
                // question of whether this approach works at all comes down to that.
                var after = bone.PoseRotation;

                Log.Debug("Stance: " + name + " pose rotation Y " +
                          before.Y.ToString("0.000") + " -> " + after.Y.ToString("0.000") +
                          " (asked for " + (camber * side).ToString("0.000") + ").");
            }
            catch
            {
                // The next frame will try again.
            }
        }

        /// <summary>
        /// Puts the four wheels back where the car had them.
        ///
        /// ZERO ON OUR THREE NUMBERS ONLY, for the same reason as above: the spin lives in the
        /// same pose, and a wheel reset to nothing is a wheel that stops turning until something
        /// else poses it again.
        /// </summary>
        private void Restore(Vehicle car)
        {
            _applied = false;

            foreach (var name in new[] { FrontLeft, FrontRight, RearLeft, RearRight })
            {
                try
                {
                    var bone = car.Bones[name];

                    if (bone == null || !bone.IsValid) continue;

                    var rotation = bone.PoseRotation;
                    rotation.Y = 0f;
                    bone.PoseRotation = rotation;

                    var pose = bone.Pose;
                    pose.X = 0f;
                    pose.Z = 0f;
                    bone.Pose = pose;
                }
                catch
                {
                    // The release by handle is the other chance.
                }
            }
        }

        /// <summary>
        /// Straightens the wheels of whatever car this was working on.
        ///
        /// BY HANDLE, like every other override in here. Stepping out of one car and into another
        /// has to put the first one's wheels back, and by then it is nobody's CurrentVehicle --
        /// and a parked car sitting on four cambered wheels is a car nothing on screen explains.
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
                // The car is gone, and its wheels went with it.
            }
        }
    }
}
