using System;
using GTA;
using VehicleTweaks.Core;
using VehicleTweaks.Input;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The cabin light, on a switch of its own.
    ///
    /// IT USED TO FOLLOW THE HEADLIGHTS, on the argument that a real dashboard lights with the
    /// side lights. That is true of the instruments and it is not true of the cabin light, which
    /// is the one thing in a car that is explicitly NOT automatic -- every car ever built has it
    /// on its own switch, because the whole point of it is that you decide when the inside of
    /// the car is lit. Tying it to the headlights meant it came on every tunnel and every dusk,
    /// which is precisely when you can see fine and do not want the glare on the glass.
    ///
    /// A PREFERENCE, NOT A PROPERTY OF ONE CAR. The switch is yours, so it follows you: leave it
    /// on, get into something else, and that one is lit too. What is not carried is the state on
    /// the car itself.
    ///
    /// PUT BACK WHEN YOU GET OUT. It is a state set on somebody else's car, the same shape as
    /// every override in this mod, and the same rule applies: whatever we switch on, we switch
    /// off again when we stop having an opinion about it. A parked car glowing from the inside
    /// forever would be this script's litter.
    /// </summary>
    internal sealed class DashLight
    {
        private readonly Settings _cfg;
        private readonly Chord _chord;

        /// <summary>The car it was set on, so it can be put back on that one.</summary>
        private Vehicle _car;

        /// <summary>What the driver has asked for, which outlives any particular car.</summary>
        private bool _want;

        /// <summary>What is actually set on the car in front of us.</summary>
        private bool _on;

        private bool _keyDown;

        public DashLight(Settings cfg)
        {
            _cfg = cfg;
            _chord = new Chord(cfg.PadModifier, cfg.PadDashLight, "the cabin light");

            Log.Info("Cabin light on " + cfg.DashLightKey + ", or on a pad " +
                     _chord.Describe() + ".");
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.DashLight)
                {
                    // Turned off in the panel with it currently lit: that is still ours to undo.
                    _want = false;
                    _keyDown = false;
                    _chord.Deafen();
                    Release();
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;

                // THE SWITCH IS READ ONLY FROM INSIDE A CAR. On foot the key belongs to whatever
                // else the player has bound it to, and a cabin light toggled while walking down
                // the street is a keystroke going somewhere nobody can see.
                if (car == null || !car.Exists())
                {
                    _keyDown = false;
                    _chord.Deafen();
                    Release();
                    return;
                }

                if (Flicked()) _want = !_want;

                if (_car != null && _car.Handle != car.Handle) Release();

                _car = car;

                if (_want == _on) return;

                _on = _want;
                car.IsInteriorLightOn = _want;

                Log.Debug("Cabin light " + (_want ? "on." : "off."));
            }
            catch (Exception ex)
            {
                Log.Once("dash-light", "The cabin light fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Whether the switch was just thrown, by either hand.
        ///
        /// NOT SHORT-CIRCUITED. Fired() is what advances the chord's own memory of whether the
        /// buttons were down last frame, so skipping it because the key already answered would
        /// leave that memory stale and the next pad press would be read as a hold.
        /// </summary>
        private bool Flicked()
        {
            var down = false;

            try { down = Game.IsKeyPressed(_cfg.DashLightKey); }
            catch { /* no keyboard is not an error */ }

            var key = down && !_keyDown;
            _keyDown = down;

            var chord = _chord.Fired();

            return key || chord;
        }

        /// <summary>Hands the cabin light back, if we are the ones holding it.</summary>
        public void Release()
        {
            if (_car == null) return;

            var car = _car;

            _car = null;

            if (!_on) return;

            _on = false;

            try
            {
                if (car.Exists()) car.IsInteriorLightOn = false;
            }
            catch
            {
                // The car is gone, and the light went with it.
            }
        }
    }
}
