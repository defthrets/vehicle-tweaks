using System;
using GTA;
using VehicleTweaks.Core;
using VehicleTweaks.Driving;

namespace VehicleTweaks
{
    /// <summary>
    /// Script entry point and the only owner of the update loop.
    ///
    /// ONE Script subclass, deliberately. SHVDN instantiates every Script it finds and ticks
    /// them in an order it does not define; a single entry point means the order our own
    /// subsystems run in is ours to decide, and there is exactly one place that has to be
    /// exception-safe.
    ///
    /// NOTHING HERE IS TIMED IN FRAMES. Both features measure in milliseconds off Game.GameTime
    /// -- how long a key has been held, how long the wheel has been over -- so there is no
    /// delta to clamp and no way for a hitch or an alt-tab to advance either of them by a
    /// second in one frame. Fumes needed that clamp because it was pouring fuel out of a tank
    /// at so many litres per second; there is nothing accumulating here.
    /// </summary>
    public sealed class Main : Script
    {
        /// <summary>Consecutive tick failures before the script parks itself rather than spamming.</summary>
        private const int MaxConsecutiveFailures = 10;

        /// <summary>
        /// Core.Settings, spelt out in full every time.
        ///
        /// Script -- the SHVDN base class -- has its own inherited Settings property, and it
        /// shadows our type in expression position. Written bare, Settings.Load() does not
        /// compile and the error points at the wrong thing entirely.
        /// </summary>
        private readonly Core.Settings _cfg;

        private readonly Ignition _ignition;
        private readonly Blinkers _blinkers;

        private int _failures;
        private bool _parked;

        public Main()
        {
            _cfg = Core.Settings.Load();

            _ignition = new Ignition(_cfg);
            _blinkers = new Blinkers(_cfg);

            // Every frame. Both features read controls, and a control read on a slower interval
            // is a key press that lands between two ticks and never happened.
            Interval = 0;
            Tick += OnTick;
            Aborted += OnAborted;

            Log.Info(Build.Name + " " + Build.Version + " loaded. Ignition " +
                     (_cfg.ManualIgnition ? "on" : "off") + ", indicators " +
                     (_cfg.Blinkers ? "on" : "off") + ".");

            if (!_cfg.Enabled)
            {
                Log.Warn("[General] Enabled is false - nothing will run until it is turned on.");
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_parked || !_cfg.Enabled) return;

            try
            {
                var me = Game.Player.Character;
                if (me == null || !me.Exists()) return;

                Greet();

                _ignition.Update(me);
                Indicate(me);

                _failures = 0;
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        /// <summary>
        /// The indicators, for the car he is actually driving.
        ///
        /// The driving test is done here rather than inside Blinkers because Blinkers has no
        /// business deciding it -- and it is by HANDLE, because every one of these properties
        /// hands back a fresh wrapper and reference equality between two of them is never true.
        /// Ignition asks the same question for itself, since its answer also has to take in
        /// whether the thing is an aircraft and what it did with the last car it was in.
        /// </summary>
        private void Indicate(Ped me)
        {
            try
            {
                var car = me.CurrentVehicle;

                var driving = car != null && car.Exists() && !car.IsDead &&
                              car.Driver != null && car.Driver.Exists() &&
                              car.Driver.Handle == me.Handle;

                _blinkers.Update(me, car, driving);
            }
            catch (Exception ex)
            {
                Log.Once("indicate", "Could not work the indicators: " + ex.Message);
            }
        }

        /// <summary>Says hello once, after the game is actually running rather than in the constructor.</summary>
        private bool _greeted;

        private void Greet()
        {
            if (_greeted) return;
            _greeted = true;

            if (!_cfg.AnnounceOnLoad) return;

            try
            {
                GTA.UI.Notification.PostTicker(
                    "~b~" + Build.Name + "~s~ " + Build.Version + " by " + Build.By, false, false);
            }
            catch
            {
                // Not being able to say hello is not a reason to stop.
            }
        }

        /// <summary>
        /// Ten failed ticks and it stops, loudly.
        ///
        /// THE ONE THING THIS MOD IS ALLOWED TO SAY ON SCREEN, and the exception proves the
        /// rule. Everything else is silent because a running engine and a working indicator are
        /// not events. A mod that has switched itself off IS an event: the exit key is about to
        /// start behaving differently and there is no other way to find that out except by
        /// wondering why the car will not do what it did five minutes ago.
        /// </summary>
        private void Fail(Exception ex)
        {
            _failures++;
            Log.Error("Tick failed (" + _failures + "/" + MaxConsecutiveFailures + ")", ex);

            if (_failures < MaxConsecutiveFailures) return;

            _parked = true;
            Log.Error("Ten ticks in a row have failed. " + Build.Name +
                      " has stopped itself rather than keep throwing. See above for the cause.");

            try
            {
                GTA.UI.Notification.PostTicker(
                    "~r~" + Build.Name + " stopped~s~ - see VehicleTweaks.log.", false, false);
            }
            catch
            {
                // Nothing further to try.
            }
        }

        /// <summary>
        /// Leaves nothing behind, because nothing was left lying about.
        ///
        /// Worth saying explicitly, since Fumes needed a real teardown here and the absence of
        /// one can read as an omission. Neither feature holds a resource: no props, no ropes,
        /// no blips, no file to flush. The only thing either of them does to the world is per
        /// frame -- DisableControlThisFrame lasts one frame by definition, and the engine calls
        /// stop mattering the moment nothing is repeating them. Stop ticking and the game has
        /// its exit key and its ignition back on the next frame, which is exactly what should
        /// happen when the script is reloaded on a keypress.
        ///
        /// The one deliberate exception is a car you have already walked away from:
        /// SET_VEHICLE_KEEP_ENGINE_ON_WHEN_ABANDONED stays set on it, and it should. That is a
        /// decision the player made about that car, not state this script is holding.
        /// </summary>
        private void OnAborted(object sender, EventArgs e)
        {
            Log.Info(Build.Name + " stopped cleanly.");
        }
    }
}
