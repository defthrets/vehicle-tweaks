using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Cars you walked away from, kept the way you left them for as long as they are there --
    /// and handed back properly when you return.
    ///
    /// WHY THIS IS NOT JUST PART OF THE HAND-OUT. Ignition sets a car up once, the moment the
    /// player is clear of the seat, and holds it that way for a few seconds while the game
    /// finishes tidying up after a driver who has left. That window exists to win an argument
    /// with the game, and it is measured in seconds because that is how long the argument lasts.
    /// It is not how long the player is away from the car.
    ///
    /// THE HANDING BACK IS THE HALF THAT CAN HURT. Everything applied here is an override that
    /// outlives us: a forced handbrake, a forced light setting, a door propped open. Any one of
    /// them left on a car after we have stopped caring about it is a car that behaves wrongly
    /// forever with nothing to say why -- and the handbrake one is a car that simply will not
    /// pull away. So every path out of this list releases, including the one where the list is
    /// full and the oldest entry is dropped.
    ///
    /// STATES ONLY while it is held, never a change. Radio enabled, radio loud and lights on can
    /// all be set to what they already are for nothing. The radio STATION is a change, and
    /// re-asserting a change restarts the track; Ignition sets it once and this never touches it.
    /// </summary>
    internal sealed class LeftRunning
    {
        /// <summary>
        /// How many cars are kept at once.
        ///
        /// More than one because you can leave more than one running, and a list holding a single
        /// car would silently stop keeping the first one the moment you left a second. Bounded
        /// because this list otherwise only grows.
        /// </summary>
        private const int Most = 12;

        /// <summary>Re-asserted a few times a second, not sixty. These are states, not events.</summary>
        private const int EveryMs = 500;

        private sealed class Held
        {
            public Vehicle Car;
            public bool Radio;
            public bool Lights;
            public bool Handbrake;
            public bool Door;

            /// <summary>
            /// Its lights were put out with its engine, and that is an OVERRIDE rather than a
            /// switch -- so somebody has to lift it, or the headlight key is dead on this car
            /// for the rest of the session with nothing anywhere to say why.
            /// </summary>
            public bool Dark;

            /// <summary>Whether its engine stopping has already been dealt with. See Update.</summary>
            public bool Quiet;
        }

        private readonly List<Held> _cars = new List<Held>();
        private int _next;

        /// <summary>
        /// Takes on a car the player has walked away from.
        ///
        /// CALLED FOR EVERY EXIT, whatever is or is not being kept. It used to be called from
        /// inside the radio handling, which returned early when RadioKeepsPlaying was off -- so
        /// with the radio feature disabled the handbrake was applied by the exit and then never
        /// registered here, which meant it was never released either. The car was locked in
        /// place permanently, and turning off an unrelated setting about music is what did it.
        /// </summary>
        public void Keep(Vehicle car, bool radio, bool lights, bool handbrake, bool door, bool dark)
        {
            if (car == null) return;
            if (!radio && !lights && !handbrake && !door && !dark) return;

            try
            {
                if (!car.Exists()) return;

                foreach (var held in _cars)
                {
                    if (held.Car == null || held.Car.Handle != car.Handle) continue;

                    // Already held. Widen what is being kept rather than adding it twice.
                    held.Radio |= radio;
                    held.Lights |= lights;
                    held.Handbrake |= handbrake;
                    held.Door |= door;
                    held.Dark |= dark;
                    return;
                }

                // The oldest goes, not the newest: the car you just walked away from is the one
                // you are standing next to. It is RELEASED on the way out rather than simply
                // dropped -- a forgotten entry is a car still wearing a forced handbrake that
                // nothing is ever going to take off again.
                if (_cars.Count >= Most)
                {
                    var oldest = _cars[0];
                    _cars.RemoveAt(0);

                    if (oldest.Car != null && oldest.Car.Exists()) Release(oldest.Car, oldest, true);

                    Log.Debug("Left running: full, so " + Name(oldest.Car) + " was let go.");
                }

                _cars.Add(new Held
                {
                    Car = car, Radio = radio, Lights = lights, Handbrake = handbrake, Door = door,
                    Dark = dark,
                });

                Log.Debug("Left running: keeping " + Name(car) +
                          (radio ? " playing" : "") + (lights ? " lit" : "") +
                          (dark ? " dark" : "") +
                          (handbrake ? " braked" : "") + (door ? " open" : "") +
                          " (" + _cars.Count + " car(s) held).");
            }
            catch (Exception ex)
            {
                Log.Once("keep", "Could not keep a car as it was left: " + ex.Message);
            }
        }

        /// <summary>
        /// Stops keeping a car and hands everything back.
        ///
        /// <paramref name="returning"/> is the difference between "he is getting back in" and
        /// "this car has stopped being interesting". Coming back releases the handbrake and
        /// shuts the door, because he is about to drive it; anything else leaves both where they
        /// are, because a parked car with its engine stopped still wants its handbrake on.
        /// </summary>
        public void Forget(Vehicle car, bool returning)
        {
            if (car == null) return;

            try
            {
                for (var i = _cars.Count - 1; i >= 0; i--)
                {
                    if (_cars[i].Car == null || _cars[i].Car.Handle != car.Handle) continue;

                    var held = _cars[i];
                    _cars.RemoveAt(i);

                    Release(car, held, returning);

                    Log.Debug("Left running: released " + Name(car) + ".");
                }
            }
            catch (Exception ex)
            {
                Log.Once("forget", "Could not release a car: " + ex.Message);
            }
        }

        public void Update(Ped me)
        {
            if (_cars.Count == 0) return;

            if (Game.GameTime < _next) return;
            _next = Game.GameTime + EveryMs;

            try
            {
                var mine = Seated(me);

                for (var i = _cars.Count - 1; i >= 0; i--)
                {
                    var held = _cars[i];
                    var car = held.Car;

                    // Gone, or streamed out from under us. Nothing to assert and nothing to tidy:
                    // whatever was set on it went with it.
                    if (car == null || !car.Exists() || car.IsDead)
                    {
                        _cars.RemoveAt(i);
                        continue;
                    }

                    // He is back in it. It is his again -- including the station, and his own
                    // headlight key, which we would otherwise be overruling every half second.
                    if (mine != 0 && car.Handle == mine)
                    {
                        _cars.RemoveAt(i);
                        Release(car, held, true);
                        Log.Debug("Left running: " + Name(car) + " has its driver back.");
                        continue;
                    }

                    // The engine stopped -- run dry, shot, or switched off by somebody. A dead
                    // car with its stereo on and its lights blazing is a flat battery, so those
                    // two go with the engine.
                    //
                    // THE HANDBRAKE AND THE DOOR DO NOT, and the entry survives for them alone.
                    // A parked car still wants its handbrake on when its engine stops, and both
                    // of them are things only a returning driver should undo -- so dropping the
                    // entry here would throw away the only record that we are the ones who put
                    // them there.
                    if (!Running(car))
                    {
                        // ONCE, NOT TWICE A SECOND FOREVER. An entry that survives its engine
                        // stopping -- because the handbrake or the door still has to be given
                        // back -- came round here on every pass, quietening a radio that was
                        // already quiet and writing a line about it. Two hundred lines about one
                        // Glendale, and a megabyte of log that pushed everything else out.
                        if (held.Quiet) continue;

                        held.Quiet = true;

                        Quieten(car, held);

                        held.Radio = false;
                        held.Lights = false;

                        if (!held.Handbrake && !held.Door && !held.Dark) _cars.RemoveAt(i);

                        Log.Debug("Left running: " + Name(car) + " stopped; radio and lights off" +
                                  (held.Handbrake || held.Door ? ", still braked or open." : "."));
                        continue;
                    }

                    // Running again -- restarted, or it was only ever a moment of the game
                    // deciding otherwise. Whatever it was, the next stop is a fresh one.
                    held.Quiet = false;

                    if (held.Radio)
                    {
                        Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, car.Handle, true);
                        Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, car.Handle, true);
                    }

                    if (held.Lights) Override(car, ScriptedVehicleLightSetting.ForceVehicleLightsOn);
                }
            }
            catch (Exception ex)
            {
                Log.Once("left-running", "Could not keep a car as it was left: " + ex.Message);
            }
        }

        /// <summary>
        /// The handle of the vehicle the player is actually SITTING IN, or 0.
        ///
        /// Sitting, not merely associated with. CurrentVehicle answers with the car through the
        /// whole climb-in, exactly as it does through the whole climb-out -- which is the bug
        /// that stopped the radio ever being set, met from the other side. Releasing on it would
        /// shut the door while he is still half way through it.
        /// </summary>
        private static int Seated(Ped me)
        {
            try
            {
                if (me == null) return 0;

                var v = me.CurrentVehicle;
                if (v == null || !v.Exists()) return 0;

                return me.IsSittingInVehicle(v) ? v.Handle : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>Radio and lights out, for a car whose engine has stopped.</summary>
        private static void Quieten(Vehicle car, Held held)
        {
            try
            {
                if (held.Radio)
                {
                    Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, car.Handle, false);
                    Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, car.Handle, false);
                }

                if (held.Lights) Override(car, ScriptedVehicleLightSetting.SetVehicleLightsOff);
            }
            catch { /* it is going quiet either way */ }
        }

        /// <summary>
        /// Hands a car back to the game, and to whoever is now in the driver's seat.
        ///
        /// THE LIGHT OVERRIDE ALWAYS GOES, whichever way this is being released. Forcing the
        /// lights on outlives us: left in place after we have stopped caring about the car, it
        /// sits there overruling the player's own headlight key for the rest of the session --
        /// they press it, nothing happens, and nothing anywhere says why.
        /// </summary>
        private static void Release(Vehicle car, Held held, bool returning)
        {
            try
            {
                if (held.Lights)
                {
                    Override(car, returning
                                      ? ScriptedVehicleLightSetting.NoVehicleLightOverride
                                      : ScriptedVehicleLightSetting.SetVehicleLightsOff);
                }
                else if (held.Dark && returning)
                {
                    // ONLY ON THE WAY BACK IN. The car was deliberately put out, so it stays put
                    // out for as long as it is parked -- but a driver in the seat has to be able
                    // to work his own headlights, and this override is the thing that would stop
                    // him. Lifting it does not turn them on; it hands the switch back.
                    Override(car, ScriptedVehicleLightSetting.NoVehicleLightOverride);
                }

                if (!returning) return;

                // Only for the driver coming back: he is about to drive it away, and neither of
                // these should still be true when he does.
                if (held.Handbrake) car.IsHandbrakeForcedOn = false;
                if (held.Door) Shut(car);
            }
            catch { /* the next entry will not be this one */ }
        }

        /// <summary>
        /// The driver's door, shut.
        ///
        /// BY NAME, NOT BY INDEX, the same way it was opened -- and by us, rather than by the
        /// game. Leaving it to the entry animation was an assumption written into three
        /// different comments and never checked; it does not close it, so the door stayed
        /// hanging open with the player sat behind it.
        /// </summary>
        public static void Shut(Vehicle car)
        {
            try { car.Doors[VehicleDoorIndex.FrontLeftDoor].Close(false); }
            catch { /* it is a door */ }
        }

        private static void Override(Vehicle car, ScriptedVehicleLightSetting setting)
        {
            try { car.SetScriptedLightSetting(setting); }
            catch { /* the next pass will try again */ }
        }

        private static bool Running(Vehicle v)
        {
            try { return v.IsEngineRunning; }
            catch { return false; }
        }

        private static string Name(Vehicle v)
        {
            try { return v == null ? "the car" : v.LocalizedName; }
            catch { return "the car"; }
        }
    }
}
