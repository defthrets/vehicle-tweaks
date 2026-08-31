using System;
using System.Windows.Forms;
using GTA;

// Both namespaces have a Control and only one of them is a game control.
using Control = GTA.Control;
using GTA.Native;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The ignition, taken off the game and given to the player.
    ///
    /// Three rules, and they are one idea: THE ENGINE IS A THING YOU OPERATE, not a side effect
    /// of being sat in the seat.
    ///
    ///   Hold the exit key and the engine stops. You do not get out.
    ///   Tap it and you get out, and the car is left exactly as it stands -- running if it was
    ///   running, dead if you turned it off first.
    ///   Get in and nothing happens. It starts when you touch the throttle.
    ///
    /// Silent by design. No prompt, no notification, no help text: a car that keeps running
    /// when you leave it is not an event, it is how a car works, and telling somebody about it
    /// every time would make it into a mechanic.
    ///
    /// THE EXIT CONTROL IS TAKEN OVER RATHER THAN WATCHED. Both meanings live on one key and
    /// the game already has its own idea about it -- hold to exit at speed -- so leaving the
    /// control enabled would have the game acting on the same press we are interpreting, and
    /// the player would be out of the car before the hold ever registered as a hold. It is
    /// disabled every frame and read through IsControlPressed, which reads a disabled control;
    /// what happens next is entirely ours.
    /// </summary>
    internal sealed class Ignition
    {
        private readonly Settings _cfg;

        /// <summary>The car being driven, so getting into a different one is noticed.</summary>
        private Vehicle _car;

        /// <summary>Engine held off until the throttle is touched.</summary>
        private bool _waitingForThrottle;

        /// <summary>When the exit key went down, and whether this press has already stopped the engine.</summary>
        private int _downAt;
        private bool _stopped;

        /// <summary>
        /// A car just stepped out of, and the state it is to be left in.
        ///
        /// Held for a few seconds because the game turns the engine off ITSELF as the driver
        /// gets out, and it does it after the task starts rather than when it finishes. One
        /// call at the moment of leaving is overwritten a frame later and the car dies on the
        /// forecourt with no explanation.
        /// </summary>
        private Vehicle _leaving;
        private bool _leavingRunning;
        private int _leavingUntil;

        /// <summary>Set once the exit control has been seen through the disable. See ExitKey.</summary>
        private bool _controlReadable;

        /// <summary>
        /// Whether he has actually got out of the car he is leaving yet.
        ///
        /// Climbing out takes a second or two and he is still IN the vehicle for all of it, so
        /// "is he in that car" cannot tell the difference between not having left yet and having
        /// come back. This is the difference.
        /// </summary>
        private bool _leftSeat;

        /// <summary>When the exit task was given, so the log can say how long climbing out took.</summary>
        private int _leftAt;

        /// <summary>Cars left running, kept as they were left for longer than the window lasts.</summary>
        private readonly LeftRunning _kept = new LeftRunning();

        /// <summary>The headlights as they were when he stepped out.</summary>
        private bool _leavingLights;

        /// <summary>Whether the door has been pushed open since he got clear of it.</summary>
        private bool _doorOpened;

        /// <summary>Whether the handbrake has been put on since he got clear of it.</summary>
        private bool _braked;

        /// <summary>Whether this exit has been handed to LeftRunning yet.</summary>
        private bool _handed;

        /// <summary>The vehicle he is sitting in, whatever it is and whoever is driving.</summary>
        private int _inCar;

        /// <summary>The vehicle he has started climbing into, which is not the same question.</summary>
        private int _approaching;

        /// <summary>The starter is turning and the engine has not caught yet.</summary>
        private bool _cranking;
        private int _crankedAt;

        /// <summary>The station that was playing as he got out, and whether it has been put back on.</summary>
        private string _station;
        private bool _radioSet;

        public Ignition(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            if (!_cfg.ManualIgnition) return;

            try
            {
                _kept.Update(me);

                var car = me == null ? null : me.CurrentVehicle;

                Reclaim(me, car);
                Approaching(me);

                // BACK IN THE SAME CAR ENDS THE ENFORCEMENT -- BUT ONLY ONCE HE HAS ACTUALLY
                // BEEN OUT OF IT.
                //
                // The guard exists so that getting out and straight back in does not leave you
                // sitting in a car whose engine is being held off for the rest of the window,
                // with nothing on screen to say why. That part is right and stays.
                //
                // What was wrong was the test. Leaving a car is an ANIMATION, one to two seconds
                // of it, and the ped is still in the vehicle for all of it -- so CurrentVehicle
                // still answers with the car being left, this read "he is back in it", and the
                // whole hand-out was cancelled on the frame after it started. Every frame of it:
                // the engine was never held on, the radio was never set, and the log line that
                // was put in Radio() to prove it had worked never appeared even once.
                //
                // It cannot be cancelled by a car he has not left yet. _leftSeat is set the
                // first time he is seen clear of the seat, and only then does getting back in
                // mean anything.
                if (_leaving != null && _leftSeat && Same(car, _leaving))
                {
                    Log.Debug("Ignition: back in " + Name(_leaving) + "; enforcement ends.");
                    _kept.Forget(_leaving, true);
                    _leaving = null;
                }

                Settle(me);

                var driving = car != null && car.Exists() && !car.IsDead &&
                              Driving(car, me) && Covered(car);

                if (!driving)
                {
                    _car = null;
                    _downAt = 0;
                    _stopped = false;
                    return;
                }

                if (!Same(car, _car))
                {
                    _car = car;
                    _downAt = 0;
                    _stopped = false;

                    // ONLY IF IT WAS ALREADY OFF. Getting into something you left running is
                    // not an ignition problem -- it is still running, and stopping it so it can
                    // be started again would be the mod inventing work.
                    _waitingForThrottle = !Running(car);
                }

                Choke(car);
                Watch(car);
                ExitKey(me, car);
            }
            catch (Exception ex)
            {
                Log.Once("ignition", "The ignition handling fell over: " + ex.Message +
                                     " - the game's own behaviour is back.");
            }
        }

        /// <summary>
        /// Getting in undoes what leaving did: the handbrake off, the door shut.
        ///
        /// THE SAFETY NET, and it does not consult the list. LeftRunning releases the cars it
        /// remembers, and that is the ordinary path -- but every failure in this area has the
        /// same shape, which is a car left wearing an override after the bookkeeping that would
        /// have removed it went astray. The bookkeeping has already been wrong once and it cost
        /// a car that could not be driven.
        ///
        /// So the moment he is sitting in ANY vehicle, whatever we do or do not remember about
        /// it, the forced handbrake comes off. Releasing a force that was never applied does
        /// nothing at all, and it is not the same thing as the handbrake the player pulls with
        /// their own hand -- so the cost of doing this unconditionally is nil, and the thing it
        /// rules out is a car nobody can move.
        /// </summary>
        private void Reclaim(Ped me, Vehicle car)
        {
            int now;

            // SITTING IN IT, not merely getting into it. CurrentVehicle answers with the car for
            // the whole climb-in, the same way it does for the whole climb-out, and shutting the
            // door on that would shut it on him half way through.
            try
            {
                now = car != null && car.Exists() && me != null && me.IsSittingInVehicle(car)
                          ? car.Handle
                          : 0;
            }
            catch { now = 0; }

            if (now == 0 || now == _inCar)
            {
                _inCar = now;
                return;
            }

            _inCar = now;

            Free(car, "got into");
        }

        /// <summary>
        /// The car he is climbing into, freed before he is in it.
        ///
        /// THE RECOVERY USED TO NEED THE THING IT WAS RECOVERING FROM TO NOT BE IN THE WAY.
        /// Everything this mod leaves on a parked car was undone on the frame he sat down --
        /// which is fine until one of those things is what stops him sitting down. A car left
        /// with its handbrake forced on and its door propped open is a car you cannot get into
        /// and then cannot drive, and the only code that would have fixed it was waiting for
        /// you to be inside.
        ///
        /// VehicleTryingToEnter is the game saying he has started reaching for a door handle,
        /// which is early enough to be useful and specific enough not to fire on cars he is
        /// merely walking past.
        /// </summary>
        private void Approaching(Ped me)
        {
            int now;
            Vehicle target = null;

            try
            {
                target = me == null ? null : me.VehicleTryingToEnter;
                now = target != null && target.Exists() ? target.Handle : 0;
            }
            catch
            {
                now = 0;
            }

            if (now == 0 || now == _approaching)
            {
                _approaching = now;
                return;
            }

            _approaching = now;

            Free(target, "reaching for");
        }

        /// <summary>Hands a car back everything we left on it.</summary>
        private void Free(Vehicle car, string what)
        {
            try { car.IsHandbrakeForcedOn = false; }
            catch { /* nothing else to try */ }

            // The door, because the game does not shut it. That was an assumption, written into
            // three comments as though it were a fact, and it was wrong: the door stayed hanging
            // open with the player sat behind it.
            if (_cfg.LeaveDoorOpen) LeftRunning.Shut(car);

            _kept.Forget(car, true);

            Log.Debug("Ignition: " + what + " " + Name(car) + "; handbrake off, door shut.");
        }

        /// <summary>
        /// Aircraft are left alone.
        ///
        /// Not squeamishness: the same gesture that parks a car is, in a helicopter at a
        /// thousand feet, the one that kills you -- and it is the SAME KEY the player uses to
        /// get out on the ground. A feature nobody asked to be lethal should not be.
        /// </summary>
        private bool Covered(Vehicle v)
        {
            if (_cfg.ManualIgnitionAircraft) return true;

            try
            {
                var m = v.Model;
                return !m.IsPlane && !m.IsHelicopter;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Holds the engine off until the throttle is touched.
        ///
        /// In Fumes this also refused to start on an empty tank, because a starved engine and a
        /// choked one would otherwise fight over the ignition once a frame -- which reads as a
        /// car that will not catch rather than one with no fuel in it. There is no fuel system
        /// here and nothing else is holding the engine down, so the throttle is the only
        /// condition left.
        /// </summary>
        private void Choke(Vehicle car)
        {
            if (!_waitingForThrottle) return;

            if (Game.IsControlPressed(Control.VehicleAccelerate))
            {
                _waitingForThrottle = false;

                // THE GAME'S OWN CRANK. The third argument is "instantly", and false hands the
                // whole start over to it: its starter sound, its own length, correct for the
                // engine in this particular car. A delay we invented would be the same delay in
                // a moped and a tanker.
                Engine(car, true, !_cfg.StarterCranks);

                _cranking = _cfg.StarterCranks;
                _crankedAt = Game.GameTime;

                if (_cranking) Log.Debug("Ignition: cranking " + Name(car) + ".");
                return;
            }

            // Every frame, and with auto-start disabled: the game restarts an engine under a
            // seated driver on its own, and it does it more than once.
            Engine(car, false);
        }

        /// <summary>
        /// Watches a start that was handed to the game, and takes it back if it does not happen.
        ///
        /// A BELT FOR AN ARGUMENT I CANNOT TEST. Everything else here fails towards the game's
        /// own behaviour; this one would fail towards a car that never starts, which is the
        /// worst thing a mod about ignitions can do. If three seconds go by with the starter
        /// supposedly turning and the engine still not running, it is started outright and the
        /// log says the crank is not working on this build.
        /// </summary>
        private void Watch(Vehicle car)
        {
            if (!_cranking) return;

            if (Running(car))
            {
                _cranking = false;
                Log.Debug("Ignition: caught after " + (Game.GameTime - _crankedAt) + "ms.");
                return;
            }

            if (Game.GameTime - _crankedAt < 3000) return;

            _cranking = false;
            Engine(car, true, true);

            Log.Once("crank", "The engine did not catch three seconds after the starter was " +
                              "handed to the game; started it outright. Turn StarterCranks off " +
                              "if this keeps happening.");
        }

        private void ExitKey(Ped me, Vehicle car)
        {
            // ABOVE WALKING PACE THE KEY GOES BACK TO THE GAME, whole and untouched.
            //
            // A tap that ejects you at sixty is not what a tap should do, and it was only doing
            // it because tapping had been given a new meaning. But simply refusing the tap
            // would have left NO way out of a moving car -- hold means "stop the engine" here,
            // and vanilla's hold-to-bail would have been quietly deleted along with it.
            //
            // So the control is not disabled at all above the limit. Vanilla is not
            // approximated, it is handed back: hold to bail out, exactly as the game does it,
            // because at speed that is the only one of the two meanings worth having. Below the
            // limit, where you are parking rather than driving, it is ours again.
            float speed;
            try { speed = car.Speed; }
            catch { speed = 0f; }

            if (speed > _cfg.ManualIgnitionMaxSpeed)
            {
                _downAt = 0;
                _stopped = false;
                return;
            }

            Game.DisableControlThisFrame(Control.VehicleExit);

            // READ THROUGH THE DISABLE, with a way out if that turns out to be wrong.
            //
            // SHVDN's IsControlPressed reads a control whether or not it is disabled -- that is
            // what separates it from IsEnabledControlPressed, which is the pair's whole reason
            // for existing. Everything here rests on that: the control is disabled every frame
            // so the game cannot act on it, and read anyway so we can.
            //
            // If it were wrong, the failure would not be a feature that does nothing. It would
            // be a player sealed inside a car with the exit key doing nothing at all and no way
            // to find out why. That is worth a belt as well as braces: until the control has
            // been seen to read true at least once, the default exit key is watched directly as
            // well. After that it never is, so a rebound exit control behaves properly.
            bool down;

            try
            {
                var viaControl = Game.IsControlPressed(Control.VehicleExit);
                if (viaControl) _controlReadable = true;

                down = viaControl || (!_controlReadable && Game.IsKeyPressed(Keys.F));
            }
            catch
            {
                down = false;
            }

            if (down)
            {
                if (_downAt == 0)
                {
                    _downAt = Game.GameTime;
                    return;
                }

                if (_stopped) return;

                if (Game.GameTime - _downAt < (int)(_cfg.ExitHoldSeconds * 1000f)) return;

                // Held long enough. The engine stops and the player stays put -- and the wait
                // for the throttle is armed, so it does not simply start itself again while
                // they are still sitting there.
                _stopped = true;
                _waitingForThrottle = true;

                Engine(car, false);
                return;
            }

            var wasDown = _downAt != 0;
            var held = _stopped;

            _downAt = 0;
            _stopped = false;

            // Released without ever becoming a hold: a tap, which is the only thing that gets
            // anybody out of a car.
            if (wasDown && !held) Leave(me, car);
        }

        private void Leave(Ped me, Vehicle car)
        {
            try
            {
                _leaving = car;
                _leavingRunning = Running(car);
                _leavingLights = Lit(car);

                // SIX SECONDS, NOT FOUR, and the radio extends it again when it lands.
                //
                // Everything here happens after the ped is CLEAR OF THE SEAT, and climbing out
                // of a car takes one to two seconds -- longer from a low car, longer again if
                // the animation is interrupted by a kerb or a passing car. A window that only
                // just covers a clean exit is one that silently does nothing on a slow one, and
                // the failure looks exactly like the feature not existing.
                _leavingUntil = Game.GameTime + 6000;
                _leftAt = Game.GameTime;
                _leftSeat = false;
                _radioSet = false;
                _doorOpened = false;
                _braked = false;
                _handed = false;

                // THE STATION HAS TO BE READ NOW, from inside. This native answers "what is the
                // PLAYER listening to", and the player stops listening to a car radio the moment
                // they are not in the car -- ask afterwards and the answer is nothing, and the
                // car would be left running in silence.
                _station = null;

                if (_leavingRunning && _cfg.RadioKeepsPlaying)
                {
                    try { _station = Function.Call<string>(Hash.GET_PLAYER_RADIO_STATION_NAME); }
                    catch { _station = null; }
                }

                // The game's OWN way of keeping an abandoned engine running, which is a better
                // instrument than holding the engine on by force every frame -- that only lasts
                // as long as the window, and this lasts as long as the car.
                try
                {
                    Function.Call(Hash.SET_VEHICLE_KEEP_ENGINE_ON_WHEN_ABANDONED,
                                  car.Handle, _leavingRunning);
                }
                catch { /* the enforcement window still covers it */ }

                Function.Call(Hash.TASK_LEAVE_VEHICLE, me.Handle, car.Handle, 0);

                // The first of the breadcrumbs. Between this and the lines in Settle and Radio,
                // a Debug log says exactly how far the hand-out got: tapped out, clear of the
                // seat, station set -- or which of those never happened. That mattered enough to
                // be worth the lines: the bug that stopped any of it working was invisible from
                // inside the game and showed up as a radio that simply went quiet.
                Log.Debug("Ignition: tapped out of " + Name(car) + "; engine " +
                          (_leavingRunning ? "running" : "off") + ", lights " +
                          (_leavingLights ? "on" : "off") + ", station " +
                          (string.IsNullOrEmpty(_station) ? "none" : _station) + ".");
            }
            catch (Exception ex)
            {
                Log.Once("ignition-leave", "Could not get out: " + ex.Message);
                _leaving = null;
            }
        }

        /// <summary>Keeps a car just left in the state it was left in, while the game argues.</summary>
        private void Settle(Ped me)
        {
            if (_leaving == null) return;

            if (!_leftSeat && Clear(me))
            {
                _leftSeat = true;
                Log.Debug("Ignition: clear of the seat after " + (Game.GameTime - _leftAt) + "ms.");
            }

            if (Game.GameTime > _leavingUntil || !_leaving.Exists() || _leaving.IsDead)
            {
                // The window is over, not the feature. A car handed to Radios goes on being
                // kept there; this only stops the frame-by-frame argument with the game.
                Log.Debug("Ignition: hand-out window closed for " + Name(_leaving) +
                          (_radioSet ? "." : " WITHOUT the radio ever being set."));

                _leaving = null;
                return;
            }

            Engine(_leaving, _leavingRunning);
            Lights();
            Radio();
            HoldRadio();
            Door();
            Handbrake();
            Hand();
        }

        /// <summary>
        /// Hands the car to LeftRunning, which outlives this window and undoes all of it.
        ///
        /// ONCE, ON EVERY EXIT, AND NOT FROM INSIDE ANOTHER FEATURE. This used to be called from
        /// Radio(), which returns immediately when RadioKeepsPlaying is off -- so with that one
        /// setting turned off, the handbrake was put on by Handbrake() and then never registered
        /// here, which meant nothing ever took it off again. The car was locked in place for
        /// good, and the thing that did it was a switch about music.
        ///
        /// Everything applied to a car on the way out is an override that has to be handed back.
        /// The handing back lives in one place, and so does the handing over.
        /// </summary>
        private void Hand()
        {
            if (_handed || !_leftSeat) return;

            _handed = true;

            _kept.Keep(_leaving,
                       _radioSet && _leavingRunning && !string.IsNullOrEmpty(_station) && _station != "OFF",
                       _cfg.LightsStayAsLeft && _leavingLights,
                       _cfg.HandbrakeOnExit,
                       _cfg.LeaveDoorOpen);
        }

        /// <summary>
        /// The handbrake, on, once he is out.
        ///
        /// The one omission from "left as you left it" that actively costs you the car: park on
        /// any of the hills in this city and an unbraked one is at the bottom of it when you get
        /// back.
        ///
        /// ONCE, and taken off again by LeftRunning when he gets back in -- which is the half
        /// that matters, and the half that is written to be unconditional. A forced handbrake
        /// nobody releases is a car that will not pull away, with nothing on screen to explain
        /// it and no key that undoes it.
        /// </summary>
        private void Handbrake()
        {
            if (_braked || !_cfg.HandbrakeOnExit || !_leftSeat) return;

            _braked = true;

            try
            {
                _leaving.IsHandbrakeForcedOn = true;
                Log.Debug("Ignition: handbrake on for " + Name(_leaving) + ".");
            }
            catch (Exception ex)
            {
                Log.Once("handbrake", "Could not put the handbrake on: " + ex.Message);
            }
        }

        /// <summary>
        /// The headlights, left as they were.
        ///
        /// Every frame of the window, like the engine and unlike the station, because this is a
        /// STATE: setting it to what it already is costs nothing. The game switches them off as
        /// the driver climbs out and it does not do it on a single frame, so one call at the
        /// moment of leaving would simply be undone with nothing to say so -- the same argument
        /// the engine has, answered the same way.
        ///
        /// Only ever asserted ON. A car left with its lights off is a car the game is already
        /// treating correctly, and forcing them off every frame would fight anybody else's mod
        /// that had a view about it.
        /// </summary>
        private void Lights()
        {
            if (!_cfg.LightsStayAsLeft || !_leavingLights) return;

            // SetScriptedLightSetting, not the AreLightsOn setter, which SHVDN marks obsolete
            // and points here instead. It is the better instrument for the same reason
            // KEEP_ENGINE_ON_WHEN_ABANDONED beat forcing the engine on: an OVERRIDE outlasts the
            // frame it was set on, where a one-shot has to win a race against the game's own
            // tidying up. The getter is not obsolete, so reading them is still AreLightsOn.
            try { _leaving.SetScriptedLightSetting(ScriptedVehicleLightSetting.ForceVehicleLightsOn); }
            catch { /* the next frame will try again */ }
        }

        /// <summary>
        /// The driver's door, left hanging open.
        ///
        /// BY NAME, NOT BY INDEX. SET_VEHICLE_DOOR_OPEN takes a door number whose meaning has to
        /// be looked up and remembered; Doors[FrontLeftDoor] says which door it is. Exactly the
        /// lesson the indicators taught, and the failure would be the same shape: a mod that
        /// opens the boot every time you get out of a car.
        ///
        /// ONCE, once he is clear. It is an ACTION, not a state -- called every frame it would
        /// re-open a door the player is trying to shut, and re-start the swing animation while
        /// it is still swinging.
        ///
        /// Shutting it again is OURS, in Reclaim. This comment used to say the game did it on
        /// the way back in; that was assumed and it is not true, so the door hung open with the
        /// player sat behind it.
        /// </summary>
        private void Door()
        {
            if (_doorOpened || !_cfg.LeaveDoorOpen || !_leftSeat) return;

            _doorOpened = true;

            try
            {
                // LOOSE, which it was not. The first argument says whether the door is left free
                // to move: false pins it open, and a door pinned open is one the game will not
                // let a ped work around to climb in. True is what an open door actually is --
                // it swings, it can be pushed shut by a passing car, and it does not stand
                // between the player and their own seat.
                //
                // instantly: false, so it swings open rather than appearing open.
                _leaving.Doors[VehicleDoorIndex.FrontLeftDoor].Open(true, false);
                Log.Debug("Ignition: left the door of " + Name(_leaving) + " open.");
            }
            catch (Exception ex)
            {
                Log.Once("door", "Could not leave the door open: " + ex.Message);
            }
        }

        /// <summary>True once he is no longer in the car he is leaving.</summary>
        private bool Clear(Ped me)
        {
            try
            {
                if (me == null) return false;

                var still = me.CurrentVehicle;
                return still == null || still.Handle != _leaving.Handle;
            }
            catch
            {
                // Unknown is treated as still inside, which only delays the radio.
                return false;
            }
        }

        /// <summary>
        /// The radio, still playing, and audible from outside.
        ///
        /// ONCE, AND ONLY ONCE HE IS ACTUALLY OUT. Not every frame, because SET_VEH_RADIO_STATION
        /// is a station CHANGE -- called sixty times a second it restarts the track sixty times
        /// a second, and a car left running would sit there stuttering the first half-second of
        /// a song forever. And not at the moment the exit task starts either: he is still in the
        /// seat then, the game still owns the radio, and it turns it off behind us on the way
        /// out.
        ///
        /// SET_VEHICLE_RADIO_LOUD is the one that carries it past the windows. Without it the
        /// radio does play, at the volume it has for somebody sitting inside, which from the
        /// pavement is silence.
        /// </summary>
        private void Radio()
        {
            if (_radioSet || !_cfg.RadioKeepsPlaying) return;

            // Still climbing out. The radio is the game's until he is clear of the seat, and
            // Settle has already worked out whether he is.
            if (!_leftSeat) return;

            _radioSet = true;

            // Held for a few seconds past the moment it is set. The game does its own tidying
            // up as a driver leaves and it does not all happen on one frame -- a single call
            // that lands before the last of it is simply undone, with nothing to say so.
            _leavingUntil = Game.GameTime + 5000;

            if (!_leavingRunning || string.IsNullOrEmpty(_station) || _station == "OFF")
            {
                // Engine off, or nothing was playing. A dead car with a radio on is a flat
                // battery, not a feature.
                try
                {
                    Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, _leaving.Handle, false);
                    Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, _leaving.Handle, false);
                }
                catch { /* nothing worth reporting */ }

                Log.Debug("Ignition: " + Name(_leaving) + " left " +
                          (_leavingRunning ? "running but with nothing playing" : "switched off") +
                          "; radio off with it.");

                return;
            }

            try
            {
                Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, _leaving.Handle, true);
                Function.Call(Hash.SET_VEH_RADIO_STATION, _leaving.Handle, _station);
                Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, _leaving.Handle, true);

                // Says what actually happened, because the alternative is me telling you it
                // works and neither of us being able to check. If this line names a station and
                // you hear nothing, the natives are the problem; if it never appears, the exit
                // path is.
                Log.Info("Left " + Name(_leaving) + " running with the radio on " + _station +
                         ", loud enough to hear from outside.");
            }
            catch (Exception ex)
            {
                Log.Once("radio", "Could not leave the radio playing: " + ex.Message);
            }
        }

        /// <summary>
        /// Keeps the radio switched on and loud for the rest of the window.
        ///
        /// ONLY THE TWO BOOLEANS, never the station. Enabled and loud are states -- setting
        /// them to what they already are costs nothing and changes nothing, so they can be
        /// re-asserted every frame against whatever the game does on its way out. The station
        /// is a CHANGE, and re-asserting a change sixty times a second is a track that restarts
        /// sixty times a second.
        /// </summary>
        private void HoldRadio()
        {
            if (!_radioSet || !_cfg.RadioKeepsPlaying) return;

            var on = _leavingRunning && !string.IsNullOrEmpty(_station) && _station != "OFF";

            try
            {
                Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, _leaving.Handle, on);
                Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, _leaving.Handle, on);
            }
            catch { /* the next frame will try again */ }
        }

        /// <summary>
        /// Two wrappers for the same thing.
        ///
        /// NOT ReferenceEquals, which is what this was and why none of it worked. Every one of
        /// these properties BUILDS A NEW WRAPPER on each call -- Vehicle.Driver has a getter and
        /// no backing field, and so does Ped.CurrentVehicle -- so two reads a frame apart are
        /// two different objects around the same handle. ReferenceEquals was false every single
        /// frame: the driver never matched the player, so the feature never ran at all, and the
        /// car never matched the last car, so the hold timer was reset before it could count.
        ///
        /// The handle is the identity. SHVDN's own == does exactly this.
        /// </summary>
        private static bool Same(Entity a, Entity b)
        {
            if (a == null || b == null) return false;

            try { return a.Handle == b.Handle; }
            catch { return false; }
        }

        private static bool Driving(Vehicle car, Ped me)
        {
            try
            {
                var driver = car.Driver;
                return driver != null && driver.Exists() && Same(driver, me);
            }
            catch
            {
                return false;
            }
        }

        private static string Name(Vehicle v)
        {
            try { return v.LocalizedName; }
            catch { return "the car"; }
        }

        private static bool Running(Vehicle v)
        {
            try { return v.IsEngineRunning; }
            catch { return false; }
        }

        private static bool Lit(Vehicle v)
        {
            try { return v.AreLightsOn; }
            catch { return false; }
        }

        /// <summary>
        /// SET_VEHICLE_ENGINE_ON(vehicle, on, instantly, disableAutoStart).
        ///
        /// The fourth argument is the one that matters and the one that is easy to leave out:
        /// without it the game is free to start the engine again by itself the moment a driver
        /// is seated, which is exactly the behaviour being replaced.
        /// </summary>
        private static void Engine(Vehicle v, bool on, bool instantly = true)
        {
            try { Function.Call(Hash.SET_VEHICLE_ENGINE_ON, v.Handle, on, instantly, true); }
            catch { /* the next frame will try again */ }
        }
    }
}
