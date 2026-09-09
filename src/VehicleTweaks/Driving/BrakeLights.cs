using System;
using GTA;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The brake lights flash when you stand on the pedal, the way a modern car's do.
    ///
    /// THIS IS THE EMERGENCY STOP SIGNAL, and it is a real thing rather than a game effect. UN
    /// regulation ECE R48 lets a car flash its stop lamps under heavy braking at four hertz, give
    /// or take one, and most of what has come out of Europe since about 2005 does exactly that.
    /// The point of it is the driver behind: a steady red lamp says "slowing", and by the time
    /// they have worked out how fast, the gap is gone. A flashing one says "NOW".
    ///
    /// TRIGGERED BY THE CAR SLOWING DOWN, NOT BY A BUTTON. The obvious way to build this is a
    /// key combination -- hold brake and handbrake, flash the lights -- and that is a different
    /// feature wearing this one's name, because it fires when you ASK rather than when you brake
    /// hard, which is the one moment your hands are busy. This mod already measures the speed
    /// every frame for the speedometer, so the deceleration is free: the difference between two
    /// samples over the time between them, in metres per second per second. Standing on the
    /// brakes in a road car is somewhere around eight of those. Lifting off is one or two.
    ///
    /// IN MILLISECONDS, NOT IN FRAMES. The flash phase comes off the game clock, so it blinks at
    /// the rate the setting says at thirty frames a second and at a hundred and forty-four. A
    /// tick counter would tie the whole effect to the frame rate, which is the bug every version
    /// of this in every game has had at least once.
    ///
    /// A CRASH IS NOT BRAKING. A wall stops a car an order of magnitude harder than a tyre can,
    /// so a sample over the ceiling is thrown away rather than clamped: an impact reads as no
    /// deceleration at all instead of as the hardest braking ever recorded. What is left is
    /// smoothed, because one frame of suspension noise is not a stop either.
    ///
    /// THE LIGHTS ARE HANDED BACK THE MOMENT IT ENDS. Nothing is forced between flashes; the
    /// game goes back to deciding, which it does correctly. There is nothing to restore on
    /// shutdown for the same reason -- the override lasts one frame and is asked for again.
    /// </summary>
    internal sealed class BrakeLights
    {
        /// <summary>
        /// Above this, in metres per second per second, it was not the brakes.
        ///
        /// A car braking hard on dry tarmac manages about nine or ten; a very good one on very
        /// good tyres, twelve. Thirty metres a second into a wall is hundreds. Nothing between
        /// the two is worth arguing about, so the line is drawn well clear of both.
        /// </summary>
        private const float Crash = 25f;

        /// <summary>
        /// The shortest a flash lasts, whatever the car does next.
        ///
        /// A stab on the pedal that only just clears the threshold would otherwise show one dark
        /// half of one blink, which reads as the lights glitching rather than as a warning.
        /// </summary>
        private const int LeastMs = 350;

        /// <summary>How much of each new reading to believe, per frame. See the class note.</summary>
        private const float Smooth = 0.35f;

        private readonly Settings _cfg;

        /// <summary>The car this is about, by handle, so changing car starts the samples again.</summary>
        private int _car;

        private int _at;
        private float _was;
        private float _decel;

        /// <summary>When the current flash began, or nought when nothing is flashing.</summary>
        private int _from;

        private bool _said;

        public BrakeLights(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.BrakeLights)
                {
                    Forget();
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists() || me.SeatIndex != VehicleSeat.Driver || !Lit(car))
                {
                    Forget();
                    return;
                }

                var now = Game.GameTime;
                var speed = car.Speed;

                if (car.Handle != _car)
                {
                    _car = car.Handle;
                    _at = now;
                    _was = speed;
                    _decel = 0f;
                    _from = 0;
                    return;
                }

                var dt = (now - _at) * 0.001f;

                _at = now;

                // A PAUSE, A LOADING SCREEN OR A TELEPORT IS NOT BRAKING. Two samples a long way
                // apart in time say nothing about how the car was driven between them, and the
                // arithmetic on them says the car stopped instantly.
                if (dt <= 0f || dt > 0.25f)
                {
                    _was = speed;
                    _decel = 0f;
                    return;
                }

                var raw = (_was - speed) / dt;

                _was = speed;

                if (raw > Crash || raw < 0f) raw = 0f;

                _decel += (raw - _decel) * Smooth;

                Flash(car, now, speed);
            }
            catch (Exception ex)
            {
                Log.Once("brake-lights", "The brake lights fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Starts, holds and ends a flash, and works the lamps while one is running.
        ///
        /// IT LETS GO AT HALF THE FORCE IT TAKES HOLD AT. One threshold read both ways would
        /// stutter on and off through the whole stop as the deceleration wobbled either side of
        /// it; a stop that is still a hard stop should keep flashing until it is plainly over.
        /// </summary>
        private void Flash(Vehicle car, int now, float speed)
        {
            if (_from == 0)
            {
                if (_decel < _cfg.BrakeFlashForce || speed < _cfg.BrakeFlashSpeed) return;

                _from = now;
                Say(car, speed);
            }
            else if (now - _from > LeastMs &&
                     (_decel < _cfg.BrakeFlashForce * 0.5f || speed < 1.5f))
            {
                // HANDED BACK RATHER THAN SWITCHED OFF. Not setting them is what gives the game
                // its lamps again; setting them false here would hold them off for a frame at
                // the exact moment you are still on the brakes.
                _from = 0;
                return;
            }

            // OFF THE CLOCK. Each half-cycle is half of one over the rate, in milliseconds, and
            // which half we are in is the count of them so far.
            var half = 500f / _cfg.BrakeFlashRate;
            var phase = (int)((now - _from) / half);

            car.AreBrakeLightsOn = (phase & 1) == 0;
        }

        /// <summary>
        /// Whether this thing has brake lights to flash.
        ///
        /// A bicycle has none, and a boat, a plane and a helicopter have nothing the native would
        /// do anything useful to. A motorbike and a quad both do.
        /// </summary>
        private static bool Lit(Vehicle car)
        {
            try
            {
                var model = car.Model;

                return model.IsCar || model.IsBike || model.IsQuadBike || model.IsAmphibiousCar ||
                       model.IsAmphibiousQuadBike;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Says the first one out loud and the rest at debug.
        ///
        /// THE FIRST ONE CARRIES THE NUMBERS because they are the whole setting: if the flash
        /// never happens, what is wanted is what the deceleration actually reached, and if it
        /// happens too readily, the same. The brake pedal reading goes with it -- it is not used
        /// to decide anything, and this line is how we find out whether it could be.
        /// </summary>
        private void Say(Vehicle car, float speed)
        {
            var brake = 0f;

            try { brake = car.BrakePower; }
            catch { /* it is only here to be reported */ }

            var line = "Brake lights: flashing at " + (speed * 3.6f).ToString("0") + " km/h, " +
                       _decel.ToString("0.0") + " m/s2 (pedal " + brake.ToString("0.00") + ").";

            if (_said)
            {
                Log.Debug(line);
                return;
            }

            _said = true;
            Log.Info(line);
        }

        private void Forget()
        {
            _car = 0;
            _from = 0;
            _decel = 0f;
        }
    }
}
