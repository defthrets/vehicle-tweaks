using System;
using System.Windows.Forms;
using GTA;
using VehicleTweaks.Core;
using VehicleTweaks.Input;

// Both namespaces have a Control and only one of them is a game control.
using Control = GTA.Control;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Cruise control: holds the speed you set it at.
    ///
    /// A CAP, NOT A THROTTLE. Vehicle.MaxSpeed tells the game this car may not exceed a speed,
    /// which is a smaller and far safer instrument than driving the accelerator ourselves --
    /// steering, braking and everything else stay entirely the player's, and the worst a bug in
    /// here can do is limit a car rather than drive one. You still hold the throttle; it simply
    /// stops climbing past where you set it.
    ///
    /// THIS IS WHY IT EXISTS NOW AND DID NOT BEFORE. Cruise control has to say somewhere that it
    /// is on -- a car quietly refusing to go faster with nothing to explain why is a bug, not a
    /// feature -- and until the cluster existed there was nowhere to say it. Now the speed reads
    /// in a different colour while it is holding.
    /// </summary>
    internal sealed class Cruise
    {
        /// <summary>
        /// What the cap goes back to. Faster than anything in the game, which is the point.
        ///
        /// There is no "no limit" to set, so releasing means setting one nothing can reach. Five
        /// hundred metres a second is eighteen hundred kilometres an hour.
        /// </summary>
        private const float NoLimit = 500f;

        /// <summary>Below this it will not engage. Cruise control at walking pace is a mistake.</summary>
        private const float Least = 5f;

        private readonly Settings _cfg;
        private readonly Chord _chord;

        private bool _keyDown;

        private int _car;
        private bool _on;
        private float _held;

        public Cruise(Settings cfg)
        {
            _cfg = cfg;
            _chord = new Chord(cfg.PadModifier, cfg.PadCruise, "cruise");
        }

        /// <summary>The speed being held, or zero. The cluster reads this and colours the digits.</summary>
        public float Holding => _on ? _held : 0f;

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.Cruise)
                {
                    Release();
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists() || !Driving(car, me))
                {
                    Release();
                    return;
                }

                if (car.Handle != _car)
                {
                    Release();
                    _car = car.Handle;
                }

                if (Pressed()) Flip(car);

                if (!_on) return;

                // BRAKING TURNS IT OFF, which is the one thing every car with cruise control
                // agrees on. Reaching for the brake means you want the speed to come down, and a
                // system that argued with that would be one nobody trusts.
                if (Game.IsControlPressed(Control.VehicleBrake))
                {
                    Release();
                    Log.Debug("Cruise: off, braked.");
                    return;
                }

                // Re-asserted rather than set once. It is a state, it costs nothing to set to
                // what it already is, and the game hands the cap back on its own in places --
                // going through water, being repaired, a cutscene.
                car.MaxSpeed = _held;
            }
            catch (Exception ex)
            {
                Release();
                Log.Once("cruise", "Cruise control fell over: " + ex.Message);
            }
        }

        private void Flip(Vehicle car)
        {
            if (_on)
            {
                Release();
                Log.Debug("Cruise: off.");
                return;
            }

            var speed = Speed(car);

            if (speed < Least)
            {
                Log.Debug("Cruise: not engaging at " + (speed * 3.6f).ToString("0") + " kph.");
                return;
            }

            _on = true;
            _held = speed;

            try { car.MaxSpeed = _held; }
            catch { _on = false; }

            if (_on) Log.Debug("Cruise: holding " + (_held * 3.6f).ToString("0") + " kph.");
        }

        /// <summary>
        /// Takes the cap off, wherever it was put.
        ///
        /// BY HANDLE, because the car being released is not always the one in front of us --
        /// stepping straight out of one and into another has to uncap the one left behind, and
        /// a car left capped is one that will not go above whatever speed you were doing when
        /// you got out of it.
        /// </summary>
        public void Release()
        {
            if (!_on)
            {
                _car = 0;
                return;
            }

            var handle = _car;

            _on = false;
            _held = 0f;
            _car = 0;

            try
            {
                var car = (Vehicle)Entity.FromHandle(handle);
                if (car != null && car.Exists()) car.MaxSpeed = NoLimit;
            }
            catch
            {
                // The car is gone, and the cap went with it.
            }
        }

        private bool Pressed()
        {
            var key = false;

            try
            {
                var down = Game.IsKeyPressed(_cfg.CruiseKey);
                key = down && !_keyDown;
                _keyDown = down;
            }
            catch
            {
                _keyDown = false;
            }

            var chord = _chord.Fired();

            return key || chord;
        }

        private static bool Driving(Vehicle car, Ped me)
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

        private static float Speed(Vehicle car)
        {
            try { return car.Speed; }
            catch { return 0f; }
        }
    }
}
