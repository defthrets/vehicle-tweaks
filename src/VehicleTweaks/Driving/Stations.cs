using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Each car remembers what it was playing.
    ///
    /// Get back into your car and it is on the station you left it on, rather than whatever the
    /// game feels like. Cars have had this since radios were fitted to them, and it is one of
    /// those things nobody notices until it is missing -- which in GTA it is, so you spend the
    /// first ten seconds of every journey cycling back to the station you were already on.
    ///
    /// READ WHILE HE IS IN IT, SET ONCE WHEN HE RETURNS. The native answers "what is the PLAYER
    /// listening to", so it only has an answer while he is in the seat -- the same reason the
    /// ignition reads it before the exit animation rather than after. And setting a station is a
    /// CHANGE, not a state: called every frame it restarts the track every frame, which is the
    /// mistake that made a car left running stutter the first half second of a song forever.
    ///
    /// IN THIS SESSION ONLY. Remembering across sessions means a file to write, and this mod
    /// writes nothing but its log -- deliberately, because a save file is a thing that can be
    /// corrupted, out of date, or full of vehicles that no longer exist.
    /// </summary>
    internal sealed class Stations
    {
        /// <summary>Bounded, like every other list of cars in here.</summary>
        private const int Most = 24;

        private readonly Settings _cfg;

        private readonly Dictionary<int, string> _known = new Dictionary<int, string>();
        private readonly List<int> _order = new List<int>();

        private int _car;

        public Stations(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            if (!_cfg.RememberStations) return;

            try
            {
                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists())
                {
                    _car = 0;
                    return;
                }

                if (car.Handle != _car)
                {
                    _car = car.Handle;
                    Restore(car);
                    return;
                }

                // Still in the same car: keep note of what he has tuned it to, so that changing
                // station and getting out remembers the new one rather than the old.
                Remember(car);
            }
            catch (Exception ex)
            {
                Log.Once("stations", "Could not remember the radio: " + ex.Message);
            }
        }

        private void Remember(Vehicle car)
        {
            string station;

            try { station = Function.Call<string>(Hash.GET_PLAYER_RADIO_STATION_NAME); }
            catch { return; }

            if (string.IsNullOrEmpty(station)) return;

            if (_known.TryGetValue(car.Handle, out var had) && had == station) return;

            if (!_known.ContainsKey(car.Handle))
            {
                if (_order.Count >= Most)
                {
                    _known.Remove(_order[0]);
                    _order.RemoveAt(0);
                }

                _order.Add(car.Handle);
            }

            _known[car.Handle] = station;
        }

        private void Restore(Vehicle car)
        {
            if (!_known.TryGetValue(car.Handle, out var station)) return;
            if (string.IsNullOrEmpty(station)) return;

            try
            {
                Function.Call(Hash.SET_VEH_RADIO_STATION, car.Handle, station);
                Log.Debug("Stations: " + Name(car) + " back on " + station + ".");
            }
            catch (Exception ex)
            {
                Log.Once("station-set", "Could not put the station back: " + ex.Message);
            }
        }

        private static string Name(Vehicle v)
        {
            try { return v.LocalizedName; }
            catch { return "the car"; }
        }
    }
}
