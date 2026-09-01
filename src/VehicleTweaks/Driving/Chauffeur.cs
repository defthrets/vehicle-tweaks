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
    /// Self driving: the game's own chauffeur, at a speed and a manner you choose.
    ///
    /// THE TASK IS ROCKSTAR'S. CruiseWithVehicle is what every ambient driver in the city is
    /// running, so the car obeys lights, overtakes, gives way and finds its way around exactly
    /// as traffic does. Nothing here steers. Asked for self driving, the answer is the thing the
    /// game already drives three hundred cars with.
    ///
    /// GETTING OUT OF IT IS THE PART THAT MATTERS. Handing the player's own ped to a task is the
    /// most control this mod ever takes, and a player who cannot get it back is a player who has
    /// to reload a save. So it stops on the key, on the throttle, on the brake, on the
    /// handbrake, on steering, on leaving the car, on the feature being switched off, and on the
    /// script shutting down. Touching any control at all is enough -- grab the wheel and it is
    /// yours again, which is what anybody would try first.
    /// </summary>
    internal sealed class Chauffeur
    {
        private readonly Settings _cfg;
        private readonly Chord _chord;

        private bool _keyDown;

        private bool _on;
        private int _car;

        /// <summary>When it took over, so its own first frames are not read as a player grabbing the wheel.</summary>
        private int _since;

        public Chauffeur(Settings cfg)
        {
            _cfg = cfg;
            _chord = new Chord(cfg.PadModifier, cfg.PadAutoDrive, "self driving");
        }

        public bool Driving => _on;

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.AutoDrive)
                {
                    Stop(me);
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists() || !AtTheWheel(car, me))
                {
                    Stop(me);
                    return;
                }

                if (car.Handle != _car)
                {
                    Stop(me);
                    _car = car.Handle;
                }

                if (Pressed())
                {
                    if (_on) Stop(me);
                    else Start(me, car);

                    return;
                }

                if (!_on) return;

                // A HAND ON ANY CONTROL TAKES IT BACK. Not after a moment, not above a
                // threshold: the first thing anybody does when a car is driving itself somewhere
                // they did not want is grab the wheel, and that has to be the thing that works.
                //
                // The short grace period is because the task's own first frames can read as
                // input; without it, it would switch itself off the instant it started.
                if (Game.GameTime - _since > 400 && Grabbed())
                {
                    Stop(me);
                    Log.Debug("Self driving: off, you took the wheel.");
                }
            }
            catch (Exception ex)
            {
                Stop(me);
                Log.Once("chauffeur", "Self driving fell over: " + ex.Message);
            }
        }

        private void Start(Ped me, Vehicle car)
        {
            try
            {
                me.Task.CruiseWithVehicle(car, _cfg.AutoDriveSpeed / 3.6f, Style(_cfg.AutoDriveStyle));

                _on = true;
                _since = Game.GameTime;

                Log.Debug("Self driving: on, " + _cfg.AutoDriveStyle + " at " +
                          _cfg.AutoDriveSpeed.ToString("0") + " kph.");
            }
            catch (Exception ex)
            {
                _on = false;
                Log.Once("chauffeur-start", "Could not hand over the driving: " + ex.Message);
            }
        }

        /// <summary>
        /// Gives the car back. Safe to call at any time, including when it was never taken.
        ///
        /// ClearAllImmediately rather than ClearAll, because the polite version lets the current
        /// task finish -- and the current task is driving to the other side of the city.
        /// </summary>
        public void Stop(Ped me)
        {
            _car = 0;

            if (!_on) return;

            _on = false;

            try
            {
                if (me != null && me.Exists()) me.Task.ClearAllImmediately();
            }
            catch
            {
                // Nothing further to try, and the flag is already down.
            }
        }

        /// <summary>Whether the player is asking the car to do anything at all.</summary>
        private static bool Grabbed()
        {
            try
            {
                return Game.IsControlPressed(Control.VehicleAccelerate) ||
                       Game.IsControlPressed(Control.VehicleBrake) ||
                       Game.IsControlPressed(Control.VehicleHandbrake) ||
                       Game.IsControlPressed(Control.VehicleMoveLeftOnly) ||
                       Game.IsControlPressed(Control.VehicleMoveRightOnly);
            }
            catch
            {
                // Unreadable controls are treated as a hand on the wheel, which errs towards
                // giving the car back rather than keeping it.
                return true;
            }
        }

        /// <summary>
        /// Four words a person would use, mapped onto the game's own flags.
        ///
        /// VehicleDrivingFlags, NOT DrivingStyle, which SHVDN marks obsolete and points here
        /// instead. The flags are the better thing anyway: DrivingStyle offered six names of
        /// uneven usefulness, one of them called SometimesOvertakeTraffic, while these are
        /// composed of what a driver actually does -- stop for vehicles, obey lights, swerve
        /// around rather than wait. The named combinations below are the game's own, so there
        /// is not a magic number in here.
        /// </summary>
        private static VehicleDrivingFlags Style(AutoStyle style)
        {
            switch (style)
            {
                // Stops for people as well as cars, and waits at lights.
                case AutoStyle.Cautious:
                    return VehicleDrivingFlags.DrivingModeAvoidVehiclesStopForPedsObeyLights;

                // Goes around what is in the way instead of queueing behind it, still stops at
                // red.
                case AutoStyle.Brisk:
                    return VehicleDrivingFlags.DrivingModeAvoidVehiclesObeyLights;

                // Neither stops nor waits. The hint on the settings row says so plainly, because
                // this is the one that will take you through a junction on red.
                case AutoStyle.Reckless:
                    return VehicleDrivingFlags.DrivingModeAvoidVehiclesReckless;

                default:
                    return VehicleDrivingFlags.DrivingModeStopForVehicles;
            }
        }

        private bool Pressed()
        {
            var key = false;

            try
            {
                var down = Game.IsKeyPressed(_cfg.AutoDriveKey);
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

        private static bool AtTheWheel(Vehicle car, Ped me)
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
    }
}
