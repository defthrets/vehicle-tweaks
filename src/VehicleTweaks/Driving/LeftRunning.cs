using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Cars you walked away from, kept the way you left them for as long as they are there.
    ///
    /// WHY THIS IS NOT JUST PART OF THE HAND-OUT. Ignition sets a car up once, the moment the
    /// player is clear of the seat, and then holds it that way for a few seconds while the game
    /// finishes tidying up after a driver who has left. That window exists to win an argument
    /// with the game, and it is measured in seconds because that is how long the argument lasts.
    ///
    /// It is not how long the player is away from the car. Walk into a shop for half a minute
    /// and the window closed twenty seconds ago -- so anything that switches the radio off or
    /// the lights out after it goes unanswered, and the thing the feature promised is quietly
    /// not true any more. The promise is "the car you left running is still running, still
    /// playing, still lit", and the honest way to keep it is to keep saying so.
    ///
    /// STATES ONLY, never a change. Radio enabled, radio loud and lights on are all things that
    /// can be set to what they already are for nothing. The radio STATION is a change, and
    /// re-asserting a change restarts the track -- which is how you get a car that stutters the
    /// first half-second of a song forever. Ignition sets the station once and this never
    /// touches it.
    /// </summary>
    internal sealed class LeftRunning
    {
        /// <summary>
        /// How many cars are kept at once.
        ///
        /// More than one because you can leave more than one running, and a list that held a
        /// single car would silently stop keeping the first one the moment you left a second.
        /// Bounded because this is a list that only ever grows otherwise, and a player who
        /// leaves cars idling all session should not be able to make it unbounded.
        /// </summary>
        private const int Most = 12;

        /// <summary>Re-asserted a few times a second, not sixty. These are states, not events.</summary>
        private const int EveryMs = 500;

        private sealed class Held
        {
            public Vehicle Car;
            public bool Radio;
            public bool Lights;
        }

        private readonly List<Held> _cars = new List<Held>();
        private int _next;

        /// <summary>
        /// Takes on a car the player has walked away from.
        ///
        /// The radio's station has already been set by the time this is called; all that is
        /// wanted here is for it to stay set.
        /// </summary>
        public void Keep(Vehicle car, bool radio, bool lights)
        {
            if (car == null) return;
            if (!radio && !lights) return;

            try
            {
                if (!car.Exists()) return;

                foreach (var held in _cars)
                {
                    if (held.Car == null || held.Car.Handle != car.Handle) continue;

                    // Already held. Widen what is being kept rather than adding it twice.
                    held.Radio |= radio;
                    held.Lights |= lights;
                    return;
                }

                // The oldest goes, not the newest. The car you just walked away from is the one
                // you are standing next to.
                if (_cars.Count >= Most) _cars.RemoveAt(0);

                _cars.Add(new Held { Car = car, Radio = radio, Lights = lights });

                Log.Debug("Left running: keeping " + Name(car) +
                          (radio ? " playing" : "") + (lights ? " lit" : "") +
                          " (" + _cars.Count + " car(s) held).");
            }
            catch (Exception ex)
            {
                Log.Once("keep", "Could not keep a car as it was left: " + ex.Message);
            }
        }

        /// <summary>
        /// Stops keeping a car, and optionally takes the radio and lights off it.
        ///
        /// THE LIGHT OVERRIDE IS ALWAYS LIFTED, silence or not, and that is not tidiness. Forcing
        /// the lights on is an override that outlives us: left in place after we have stopped
        /// caring about the car, it would sit there overruling the player's own headlight key
        /// for the rest of the session. They would get back into their car, press the key, and
        /// nothing would happen -- and nothing anywhere would say why.
        /// </summary>
        public void Forget(Vehicle car, bool silence)
        {
            if (car == null) return;

            try
            {
                for (var i = _cars.Count - 1; i >= 0; i--)
                {
                    if (_cars[i].Car == null || _cars[i].Car.Handle != car.Handle) continue;

                    var held = _cars[i];
                    _cars.RemoveAt(i);

                    Release(car, held, silence);

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

                    // Gone, or streamed out from under us. Nothing to assert and nothing to
                    // tidy: the radio and the lights went with it.
                    if (car == null || !car.Exists() || car.IsDead)
                    {
                        _cars.RemoveAt(i);
                        continue;
                    }

                    // The player is back in it. It is theirs again, and the game will do the
                    // right thing with it -- including letting them change station and work
                    // their own lights, which we would otherwise be fighting every half second.
                    if (mine != 0 && car.Handle == mine)
                    {
                        _cars.RemoveAt(i);
                        Release(car, held, false);
                        Log.Debug("Left running: " + Name(car) + " has its driver back.");
                        continue;
                    }

                    // The engine stopped -- run dry, shot, or switched off by somebody. A dead
                    // car with its stereo on and its lights blazing is a flat battery, so both
                    // go with the engine.
                    if (!Running(car))
                    {
                        _cars.RemoveAt(i);
                        Release(car, held, true);
                        Log.Debug("Left running: " + Name(car) + " stopped; radio and lights off.");
                        continue;
                    }

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

        /// <summary>The handle of the vehicle the player is sitting in, or 0.</summary>
        private static int Seated(Ped me)
        {
            try
            {
                if (me == null) return 0;

                var v = me.CurrentVehicle;
                return v == null ? 0 : v.Handle;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Hands a car back to the game.
        ///
        /// Silenced or not, the light OVERRIDE goes -- see Forget. What differs is where it is
        /// handed back to: "off" actually puts the lights out, because an engine that has
        /// stopped should not leave them burning, while a release just returns the decision to
        /// the game and to whoever is now sitting in the driver's seat.
        /// </summary>
        private static void Release(Vehicle car, Held held, bool silence)
        {
            try
            {
                if (held.Radio && silence)
                {
                    Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, car.Handle, false);
                    Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, car.Handle, false);
                }

                if (held.Lights)
                {
                    Override(car, silence
                                      ? ScriptedVehicleLightSetting.SetVehicleLightsOff
                                      : ScriptedVehicleLightSetting.NoVehicleLightOverride);
                }
            }
            catch { /* it is going quiet either way */ }
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
            try { return v.LocalizedName; }
            catch { return "the car"; }
        }
    }
}
