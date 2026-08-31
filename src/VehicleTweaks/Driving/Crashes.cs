using System;
using GTA;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// A moment of slow motion when you hit something hard enough.
    ///
    /// THE MOST DANGEROUS THING IN THIS MOD, and it is worth saying so plainly at the top rather
    /// than burying it. Game.TimeScale is GLOBAL and it is PERSISTENT: it is not a property of a
    /// car or a ped, it is the speed of the entire world, and nothing puts it back on its own. A
    /// script that sets it to four tenths and then dies -- an exception, a reload, the player
    /// unloading the mod at the wrong moment -- leaves the game running at four tenths speed for
    /// ever, with no setting anywhere to explain it and no obvious way out short of a restart.
    ///
    /// So it is written the way a thing like that has to be written. There is one place that
    /// sets it and one that clears it, and the clearing happens when the window ends, when the
    /// player is no longer in a car, when the feature is switched off in the panel, when
    /// anything at all throws in here, and when the script shuts down. Any one of those would do
    /// in the ordinary case. They are all there for the cases that are not ordinary.
    ///
    /// A CRASH IS A SPEED THAT VANISHES, not a collision. HasCollided is true for kerbs, walls
    /// taken at a scrape and the gatepost you clipped -- it says something was touched, not that
    /// anything happened. Losing eight metres a second inside one frame is fifty g, which is not
    /// something braking can do and not something a scrape can do. Both are required: the speed
    /// says it was violent and the collision says it was an impact rather than the physics engine
    /// having an opinion.
    /// </summary>
    internal sealed class Crashes
    {
        /// <summary>
        /// A ceiling on the window, whatever the setting says.
        ///
        /// A CAP, NOT A WATCHDOG, and worth being exact about which: the ini already clamps the
        /// setting, so this only matters if that clamp is ever loosened. What actually protects
        /// against the world being left slowed is the list of places Restore is called from --
        /// the window ending, the player leaving the car, the feature being switched off, a
        /// throw anywhere in Update, and the script shutting down.
        /// </summary>
        private const int CeilingMs = 4000;

        private readonly Settings _cfg;

        private bool _active;
        private int _until;

        /// <summary>Speed on the previous frame, which is the only place a crash can be seen.</summary>
        private float _was;

        public Crashes(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (_active && (Game.GameTime >= _until || !_cfg.CrashSlowMo)) Restore();

                if (!_cfg.CrashSlowMo) return;

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists())
                {
                    // Out of the car mid-window -- thrown through the windscreen, most likely.
                    // The world goes back to full speed with him.
                    Restore();
                    _was = 0f;
                    return;
                }

                var speed = Speed(car);
                var was = _was;

                _was = speed;

                if (_active) return;

                // KILOMETRES AN HOUR IN THE SETTING, metres a second here. The setting is in the
                // unit the question was asked in; the game answers in the other one.
                if (was < _cfg.CrashSlowMoSpeed / 3.6f) return;

                if (was - speed < _cfg.CrashSlowMoDrop) return;

                if (!Hit(car)) return;

                Slow(was);
            }
            catch (Exception ex)
            {
                // A throw in here must not be able to leave the world slowed down.
                Restore();
                Log.Once("crashes", "The crash slow motion fell over: " + ex.Message);
            }
        }

        private void Slow(float from)
        {
            try
            {
                var ms = (int)(_cfg.CrashSlowMoSeconds * 1000f);
                if (ms > CeilingMs) ms = CeilingMs;

                _active = true;
                _until = Game.GameTime + ms;

                Game.TimeScale = _cfg.CrashSlowMoScale;

                Log.Debug("Crash at " + (from * 3.6f).ToString("0") + " kph; the world slowed to " +
                          _cfg.CrashSlowMoScale.ToString("0.00") + " for " + ms + "ms.");
            }
            catch (Exception ex)
            {
                Restore();
                Log.Once("crash-slow", "Could not slow the world down: " + ex.Message);
            }
        }

        /// <summary>
        /// Full speed, now.
        ///
        /// SET BACK TO ONE RATHER THAN TO WHAT WAS THERE BEFORE, deliberately. Putting back a
        /// remembered value is the more polite thing and it is the wrong trade here: if anything
        /// went wrong badly enough that this is being called from a catch, the remembered value
        /// is exactly what cannot be trusted. One is always right for a player who wants their
        /// game back, and the worst it can do is cut short somebody else's slow motion.
        ///
        /// Safe to call at any time, including when nothing is slowed.
        /// </summary>
        public void Restore()
        {
            if (!_active) return;

            _active = false;

            try { Game.TimeScale = 1f; }
            catch { /* nothing further can be done about it here */ }
        }

        private static bool Hit(Vehicle car)
        {
            try { return car.HasCollided; }
            catch { return true; }
        }

        private static float Speed(Vehicle car)
        {
            try { return car.Speed; }
            catch { return 0f; }
        }
    }
}
