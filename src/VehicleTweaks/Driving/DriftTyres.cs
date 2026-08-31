using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Drift tyres, the ones GTA Online actually has.
    ///
    /// SET_DRIFT_TYRES IS THE REAL THING, and using it is the whole point of this file. It is the
    /// flag the Los Santos Tuners update added for the drift tuning you buy at a garage, so what
    /// this switches on is not an impression of drift tyres -- it is the same handling Rockstar
    /// wrote, engaging on the same terms, on the vehicles they allowed it on. Nothing here models
    /// grip or fakes a slide.
    ///
    /// NOT HasLowerFrictionTires, which sits next to it in SHVDN and is a different and much
    /// older thing: a blunt reduce-grip flag that makes a car slither everywhere, including in a
    /// straight line at walking pace. That is what an approximation of this would have felt like,
    /// and it is exactly what was not asked for.
    ///
    /// A FLAG SET ON A CAR IS AN OVERRIDE THAT OUTLIVES US, like every other one in this mod, so
    /// it is written down and taken off again. Switch the setting off and the cars it was put on
    /// lose it; reload the script and they lose it. What is never touched is a car that already
    /// had drift tyres when we found it -- somebody paid for those at a garage, and they are not
    /// ours to remove.
    /// </summary>
    internal sealed class DriftTyres
    {
        /// <summary>Bounded for the same reason every list of held cars in here is.</summary>
        private const int Most = 12;

        private readonly Settings _cfg;
        private readonly List<Vehicle> _ours = new List<Vehicle>();

        public DriftTyres(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.DriftTyres)
                {
                    // Switched off in the panel: the cars we did it to get their grip back on the
                    // same frame, rather than the next time they happen to be looked at.
                    ReleaseAll();
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;
                if (car == null || !car.Exists()) return;

                foreach (var v in _ours)
                {
                    if (v != null && v.Handle == car.Handle) return;
                }

                // ALREADY ON IT, AND NOT BY US. Drift tuning is something a player buys, so a car
                // that arrives wearing it keeps it and is never written down here -- otherwise
                // switching this setting off would take away a modification somebody paid for.
                if (Fitted(car)) return;

                if (_ours.Count >= Most)
                {
                    Release(_ours[0]);
                    _ours.RemoveAt(0);
                }

                Fit(car, true);
                _ours.Add(car);

                Log.Debug("Drift tyres fitted to " + Name(car) + " (" + _ours.Count + " car(s)).");
            }
            catch (Exception ex)
            {
                Log.Once("drift", "The drift tyres fell over: " + ex.Message);
            }
        }

        /// <summary>Takes them off every car we put them on. Safe to call at any time.</summary>
        public void ReleaseAll()
        {
            if (_ours.Count == 0) return;

            try
            {
                foreach (var car in _ours) Release(car);
            }
            catch
            {
                // Whatever could be handed back has been.
            }

            _ours.Clear();
        }

        private static void Release(Vehicle car)
        {
            try
            {
                if (car != null && car.Exists()) Fit(car, false);
            }
            catch
            {
                // The car is gone and the flag went with it.
            }
        }

        private static void Fit(Vehicle car, bool on)
        {
            try { Function.Call(Hash.SET_DRIFT_TYRES, car.Handle, on); }
            catch { /* nothing else to try */ }
        }

        private static bool Fitted(Vehicle car)
        {
            try { return Function.Call<bool>(Hash.GET_DRIFT_TYRES_SET, car.Handle); }
            catch { return false; }
        }

        private static string Name(Vehicle v)
        {
            try { return v.LocalizedName; }
            catch { return "the car"; }
        }
    }
}
