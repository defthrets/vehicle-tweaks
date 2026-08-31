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
    /// TWO OF ROCKSTAR'S OWN, AND NEITHER OF THEM INVENTED HERE. Nothing in this file models
    /// grip or fakes a slide.
    ///
    /// SET_DRIFT_TYRES is the Drift Races tuning, and it is the better of the two: the same
    /// handling Rockstar wrote, engaging on the same terms. But it is GATED to the cars that
    /// update gave it to, and on anything else it does not fail -- it is simply ignored, which
    /// is the worst way for a feature to not work. Asked for drift mode on ANY car, that alone
    /// was never going to be the answer.
    ///
    /// So the second one: low grip tyres, the other thing the same update shipped, which go on
    /// anything. The real tuning is asked for first, GET_DRIFT_TYRES_SET says whether it took,
    /// and low grip is what a car gets when it would not have it.
    ///
    /// The log says which one each car got. "It feels different in this car" should be something
    /// you can look up rather than something you wonder about.
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

        private sealed class Held
        {
            public Vehicle Car;

            /// <summary>Which of the two it actually got, so the right one is taken off again.</summary>
            public bool Tuned;
            public bool Loosened;
        }

        private readonly Settings _cfg;
        private readonly List<Held> _ours = new List<Held>();

        public DriftTyres(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (_cfg.DriftTyres == DriftMode.Off)
                {
                    // Switched off in the panel: the cars we did it to get their grip back on the
                    // same frame, rather than the next time they happen to be looked at.
                    ReleaseAll();
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;
                if (car == null || !car.Exists()) return;

                foreach (var held in _ours)
                {
                    if (held.Car != null && held.Car.Handle == car.Handle) return;
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

                _ours.Add(Apply(car, _cfg.DriftTyres));
            }
            catch (Exception ex)
            {
                Log.Once("drift", "The drift tyres fell over: " + ex.Message);
            }
        }

        /// <summary>Takes it off every car we put it on. Safe to call at any time.</summary>
        public void ReleaseAll()
        {
            if (_ours.Count == 0) return;

            try
            {
                foreach (var held in _ours) Release(held);
            }
            catch
            {
                // Whatever could be handed back has been.
            }

            _ours.Clear();
        }

        /// <summary>
        /// The real tuning if the car will take it, low grip if it will not.
        ///
        /// ASKED, THEN CHECKED. SET_DRIFT_TYRES is gated to the vehicles the Drift Races update
        /// gave it to, and on anything else it is simply ignored -- no error, no complaint, just
        /// a car that handles exactly as it did. GET_DRIFT_TYRES_SET is how you find out, and
        /// without asking it this feature would have been silently doing nothing on most of the
        /// cars in the game while its setting sat there saying it was on.
        /// </summary>
        private static Held Apply(Vehicle car, DriftMode mode)
        {
            var held = new Held { Car = car };

            try
            {
                Function.Call(Hash.SET_DRIFT_TYRES, car.Handle, true);
                held.Tuned = Function.Call<bool>(Hash.GET_DRIFT_TYRES_SET, car.Handle);
            }
            catch
            {
                held.Tuned = false;
            }

            if (held.Tuned)
            {
                Log.Debug("Drift mode: " + Name(car) + " took the real drift tuning.");
                return held;
            }

            // It would not have it. Low grip tyres are the other half of the same update and
            // they go on anything -- the boolean does the work and the level modulates it.
            try
            {
                Function.Call(Hash.SET_VEHICLE_REDUCE_GRIP, car.Handle, true);
                Function.Call(Hash.SET_VEHICLE_REDUCE_GRIP_LEVEL, car.Handle, Level(mode));

                held.Loosened = true;

                Log.Debug("Drift mode: " + Name(car) + " will not take drift tuning, so it has " +
                          "low grip tyres at level " + Level(mode) + " (" + mode + ").");
            }
            catch (Exception ex)
            {
                Log.Once("drift-grip", "Could not fit low grip tyres: " + ex.Message);
            }

            return held;
        }

        /// <summary>
        /// The level the native is given.
        ///
        /// The scale is the game's and it is not written down anywhere I can check, so the
        /// number is logged next to the word that produced it. If Heavy turns out to be no
        /// different from Light, the log is what says whether the level was ignored or whether
        /// the whole call was.
        /// </summary>
        private static int Level(DriftMode mode)
        {
            switch (mode)
            {
                case DriftMode.Light: return 0;
                case DriftMode.Heavy: return 2;
                default: return 1;
            }
        }

        private static void Release(Held held)
        {
            try
            {
                var car = held.Car;
                if (car == null || !car.Exists()) return;

                // Only the one it was actually given. Switching off a thing that was never on
                // is usually harmless and occasionally is not, and there is no reason to guess
                // when the record is right here.
                if (held.Tuned) Function.Call(Hash.SET_DRIFT_TYRES, car.Handle, false);
                if (held.Loosened) Function.Call(Hash.SET_VEHICLE_REDUCE_GRIP, car.Handle, false);
            }
            catch
            {
                // The car is gone and the flag went with it.
            }
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
