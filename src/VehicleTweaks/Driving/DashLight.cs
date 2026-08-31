using System;
using GTA;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The cabin lights up when the headlights are on.
    ///
    /// TIED TO THE HEADLIGHTS, NOT TO THE CLOCK, and that is the more faithful rule as well as
    /// the simpler one. A real dashboard lights with the side lights, which is why going into a
    /// tunnel at noon lights your instruments up -- asking the game what time it is would have
    /// got the tunnel wrong and needed a threshold nobody can name for dusk. The game already
    /// turns a player's headlights on when it gets dark, so this follows the same decision.
    ///
    /// PUT BACK WHEN YOU GET OUT. It is a state set on somebody else's car, which is the same
    /// shape as every override in this mod, and the same rule applies: whatever we switch on, we
    /// switch off again when we stop having an opinion about it. A parked car glowing from the
    /// inside forever would be this script's litter.
    /// </summary>
    internal sealed class DashLight
    {
        private readonly Settings _cfg;

        /// <summary>The car it was set on, so it can be put back on that one.</summary>
        private Vehicle _car;
        private bool _on;

        public DashLight(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.DashLight)
                {
                    // Turned off in the panel with it currently lit: that is still ours to undo.
                    Release();
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists())
                {
                    Release();
                    return;
                }

                if (_car != null && _car.Handle != car.Handle) Release();

                _car = car;

                var want = Lit(car);
                if (want == _on) return;

                _on = want;
                car.IsInteriorLightOn = want;

                Log.Debug("Dash light " + (want ? "on." : "off."));
            }
            catch (Exception ex)
            {
                Log.Once("dash-light", "The dash light fell over: " + ex.Message);
            }
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

        private static bool Lit(Vehicle car)
        {
            try { return car.AreLightsOn; }
            catch { return false; }
        }
    }
}
