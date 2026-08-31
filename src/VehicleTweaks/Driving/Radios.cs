using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Cars left running with the radio on, kept audible for as long as they are there.
    ///
    /// WHY THIS IS NOT JUST PART OF THE HAND-OUT. Ignition sets the station once, the moment the
    /// player is clear of the seat, and then holds the radio on for a few seconds while the game
    /// finishes tidying up after a driver who has left. That window exists to win an argument
    /// with the game, and it is measured in seconds because that is how long the argument lasts.
    ///
    /// It is not how long the player is away from the car. Walk into a shop for half a minute
    /// and the window closed twenty seconds ago -- so anything that switches the radio off after
    /// it closes goes unanswered, and the thing the feature promised is quietly not true any
    /// more. The promise is "the car you left running is still playing", and the honest way to
    /// keep it is to keep asserting it for as long as the car is still there and still running.
    ///
    /// ONLY THE TWO BOOLEANS, never the station. Enabled and loud are states -- setting them to
    /// what they already are costs nothing. The station is a CHANGE, and re-asserting a change
    /// restarts the track, which is how you get a car that stutters the first half-second of a
    /// song forever.
    /// </summary>
    internal sealed class Radios
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

        private readonly List<Vehicle> _cars = new List<Vehicle>();
        private int _next;

        /// <summary>Takes on a car whose station has already been set, and keeps it playing.</summary>
        public void Keep(Vehicle car)
        {
            if (car == null) return;

            try
            {
                if (!car.Exists()) return;

                foreach (var v in _cars)
                {
                    if (v != null && v.Handle == car.Handle) return;
                }

                // The oldest goes, not the newest. The car you just walked away from is the one
                // you are standing next to.
                if (_cars.Count >= Most) _cars.RemoveAt(0);

                _cars.Add(car);
                Log.Debug("Radio: keeping " + Name(car) + " playing (" + _cars.Count + " car(s) held).");
            }
            catch (Exception ex)
            {
                Log.Once("radio-keep", "Could not keep a radio playing: " + ex.Message);
            }
        }

        /// <summary>Stops keeping a car, and takes the radio off it if it is still there.</summary>
        public void Forget(Vehicle car, bool silence)
        {
            if (car == null) return;

            try
            {
                for (var i = _cars.Count - 1; i >= 0; i--)
                {
                    if (_cars[i] == null || _cars[i].Handle != car.Handle) continue;

                    _cars.RemoveAt(i);
                    if (silence) Off(car);

                    Log.Debug("Radio: released " + Name(car) + ".");
                }
            }
            catch (Exception ex)
            {
                Log.Once("radio-forget", "Could not release a radio: " + ex.Message);
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
                    var car = _cars[i];

                    // Gone, or streamed out from under us. Nothing to assert and nothing to
                    // tidy: the radio went with it.
                    if (car == null || !car.Exists() || car.IsDead)
                    {
                        _cars.RemoveAt(i);
                        continue;
                    }

                    // The player is back in it. The radio is theirs again, and the game will do
                    // the right thing with it -- including letting them change station, which
                    // we would otherwise be fighting.
                    if (mine != 0 && car.Handle == mine)
                    {
                        _cars.RemoveAt(i);
                        Log.Debug("Radio: " + Name(car) + " has its driver back.");
                        continue;
                    }

                    // The engine stopped -- run dry, shot, or switched off by somebody. A dead
                    // car with a stereo on is a flat battery, so it goes quiet with the engine.
                    if (!Running(car))
                    {
                        _cars.RemoveAt(i);
                        Off(car);
                        Log.Debug("Radio: " + Name(car) + " stopped running; radio off.");
                        continue;
                    }

                    Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, car.Handle, true);
                    Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, car.Handle, true);
                }
            }
            catch (Exception ex)
            {
                Log.Once("radio-update", "Could not keep the radios playing: " + ex.Message);
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

        private static void Off(Vehicle car)
        {
            try
            {
                Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, car.Handle, false);
                Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, car.Handle, false);
            }
            catch { /* it is going quiet either way */ }
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
