using System;
using System.Windows.Forms;
using GTA;
using VehicleTweaks.Core;
using VehicleTweaks.Driving;
using VehicleTweaks.UI;

// System.Windows.Forms has a Menu too, and it is not ours. Same trap as GTA.Control versus
// System.Windows.Forms.Control, one file over: importing Forms for KeyEventArgs quietly makes
// the word Menu ambiguous, and the error names a type this mod has never heard of.
using Menu = VehicleTweaks.UI.Menu;

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
        private readonly FrontWheels _frontWheels;
        private readonly Seatbelt _seatbelt;
        private readonly Locks _locks;
        private readonly Menu _menu;
        private readonly Speedo _speedo;
        private readonly DashLight _dash;
        private readonly Crashes _crashes;
        private readonly DriftTyres _drift;
        private readonly Parked _myCar;
        private readonly Stations _stations;
        private readonly Cruise _cruise;
        private readonly Chauffeur _chauffeur;
        private readonly Slides _slides;

        private int _failures;
        private bool _parked;

        public Main()
        {
            _cfg = Core.Settings.Load();

            _ignition = new Ignition(_cfg);
            _blinkers = new Blinkers(_cfg);
            _frontWheels = new FrontWheels(_cfg);
            _seatbelt = new Seatbelt(_cfg);
            _locks = new Locks(_cfg);
            _menu = new Menu(_cfg);
            _speedo = new Speedo(_cfg);
            _dash = new DashLight(_cfg);
            _crashes = new Crashes(_cfg);
            _drift = new DriftTyres(_cfg);
            _myCar = new Parked(_cfg);
            _stations = new Stations(_cfg);
            _cruise = new Cruise(_cfg);
            _chauffeur = new Chauffeur(_cfg);
            _slides = new Slides(_cfg);

            // Every frame. Both features read controls, and a control read on a slower interval
            // is a key press that lands between two ticks and never happened.
            Interval = 0;
            Tick += OnTick;
            Aborted += OnAborted;

            // ONLY the panel's rebind row listens to this. Everything else in the mod reads
            // game CONTROLS rather than keys, so that a player who has rebound their exit key
            // or their steering gets the mod they rebound. A raw key event is the one thing a
            // control cannot give you: which physical key was just pressed, when the whole
            // point is to find out.
            KeyDown += OnKeyDown;

            Log.Info(Build.Name + " " + Build.Version + " loaded. Ignition " +
                     (_cfg.ManualIgnition ? "on" : "off") + ", indicators " +
                     (_cfg.Blinkers ? "on" : "off") + ", settings on " +
                     _cfg.BindingText() + ".");

            if (!_cfg.Enabled)
            {
                Log.Warn("[General] Enabled is false - neither feature will run until it is " +
                         "turned back on, from the settings panel or from the ini.");
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_parked) return;

            try { _menu.OnKey(e.KeyCode); }
            catch (Exception ex) { Log.Once("keydown", "Key handling failed: " + ex.Message); }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_parked) return;

            try
            {
                var me = Game.Player.Character;
                if (me == null || !me.Exists()) return;

                Greet();

                // THE PANEL RUNS FIRST, AND OUTSIDE THE Enabled GATE.
                //
                // First, so a key pressed to open it is not also read by the features on the
                // same frame. Outside the gate, because "Both features on" is itself a row in
                // the panel: gating the panel on Enabled would mean that switching it off shut
                // the only door back in, and the mod could only be turned on again by finding
                // the ini and editing it by hand. A setting that can be changed one way is a
                // trap, not a setting.
                _menu.Update();

                if (!_cfg.Enabled)
                {
                    _failures = 0;
                    return;
                }

                // Not while the panel has the keyboard, or the arrow keys would be steering a
                // car nobody is looking at.
                if (!_menu.IsOpen)
                {
                    _ignition.Update(me);
                    Indicate(me);
                    _seatbelt.Update(me);
                    _locks.Update(me);
                    _dash.Update(me);
                    _crashes.Update(me);
                    _drift.Update(me);
                    _myCar.Update(me);
                    _stations.Update(me);
                    _cruise.Update(me);
                    _chauffeur.Update(me);
                    _slides.Update(me);
                }

                // LAST, AND NOT GATED ON THE PANEL BEING SHUT. It is a readout, not an input:
                // there is nothing for it to steal, and it has to keep drawing while the panel
                // is open or the four rows that position it would be moving something invisible.
                _speedo.Update(me, _menu.IsOpen, _cruise.Holding > 0f, _chauffeur.Driving);

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
        /// Also where the front wheels are told about the handbrake, because it needs the same
        /// answer to the same question and there should be one of those rather than two.
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
                _frontWheels.Update(car, driving);
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
        /// ONE OF THE TWO THINGS THIS MOD SAYS ON SCREEN UNASKED, and the exception proves the
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
        /// Leaves nothing behind, and saves the one thing that would otherwise be lost.
        ///
        /// Neither feature holds a resource: no props, no ropes, no blips. The only thing they
        /// do to the world is per frame -- DisableControlThisFrame lasts one frame by
        /// definition, and the engine calls stop mattering the moment nothing is repeating
        /// them. Stop ticking and the game has its exit key and its ignition back on the next
        /// frame, which is exactly what should happen when the script is reloaded on a keypress.
        ///
        /// The panel is the exception, and that is why this is not empty. Settings changed in
        /// it apply live and are written when it closes, so a reload with the panel still open
        /// would apply changes for one session and then throw them away. Dismiss shuts it
        /// properly, through the same path the player's own Backspace uses.
        ///
        /// The exception, and it is why this runs the time scale first: crash slow motion is not
        /// a property of a car, it is the speed of the world, and nothing puts it back on its
        /// own. Everything else here can be left safely. That cannot.
        ///
        /// The other deliberate leftover is a car you have already walked away from: the engine
        /// keeps running, on the game's own SET_VEHICLE_KEEP_ENGINE_ON_WHEN_ABANDONED, and the
        /// radio keeps playing. Both should. That is a decision the player made about that car,
        /// not state this script is holding, and switching it off on the way out would be the
        /// script tidying away somebody else's parked car because it happened to be reloaded.
        /// </summary>
        private void OnAborted(object sender, EventArgs e)
        {
            // THE TIME SCALE FIRST, before anything that could throw. It is the only thing this
            // mod can leave behind that affects the whole game rather than one car, and a
            // reload landing in the middle of a crash would otherwise leave the world running
            // at four tenths speed with nothing to explain it.
            try { _crashes.Restore(); } catch (Exception ex) { Log.Error("Time scale", ex); }
            try { _dash.Release(); } catch (Exception ex) { Log.Error("Dash light", ex); }
            try { _drift.ReleaseAll(); } catch (Exception ex) { Log.Error("Drift tyres", ex); }
            try { _frontWheels.Release(); } catch (Exception ex) { Log.Error("Front wheels", ex); }
            try { _chauffeur.Stop(Game.Player.Character); } catch (Exception ex) { Log.Error("Self driving", ex); }
            try { _cruise.Release(); } catch (Exception ex) { Log.Error("Cruise", ex); }
            try { _slides.Release(); } catch (Exception ex) { Log.Error("Slide power", ex); }
            try { _myCar.Release(); } catch (Exception ex) { Log.Error("Parked car", ex); }
            try { _menu.Dismiss(); } catch (Exception ex) { Log.Error("Panel shutdown", ex); }

            Log.Info(Build.Name + " stopped cleanly.");
        }
    }
}
