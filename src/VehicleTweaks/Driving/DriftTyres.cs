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
    /// So the second one: the friction override, which goes on anything and TAKES A FLOAT. That
    /// last part is what makes the setting a slider rather than a short list -- REDUCE_GRIP_LEVEL
    /// was the obvious partner to the drift tyres and it takes a whole number on a scale nobody
    /// has written down, which is three steps wearing a decimal point at best.
    ///
    /// The real tuning is asked for first, GET_DRIFT_TYRES_SET says whether it took, and the
    /// friction is scaled by the slider on every car either way -- so turning the knob does
    /// something no matter what is being driven, which is the whole point of a knob.
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
                if (_cfg.DriftAmount <= 0f && !AnySlide())
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
                    if (held.Car == null || held.Car.Handle != car.Handle) continue;

                    // ALREADY FITTED, BUT THE SLIDER MAY HAVE MOVED SINCE. Friction is a state,
                    // costs nothing to set to what it already is, and this is the row somebody
                    // will sit and nudge while driving -- so it has to follow the setting rather
                    // than be whatever it was when they got in.
                    Friction(car, Amount(car));
                    return;
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

                _ours.Add(Apply(car, Amount(car)));
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
        private static Held Apply(Vehicle car, float amount)
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

            Friction(car, amount);
            held.Loosened = true;

            Log.Debug("Drift mode: " + Name(car) +
                      (held.Tuned ? " took the real drift tuning, " : " would not take drift tuning, ") +
                      "friction " + Grip(amount).ToString("0.00") +
                      " at " + amount.ToString("0.00") + ".");

            return held;
        }

        /// <summary>
        /// The slider, as a friction figure.
        ///
        /// One is normal grip and the slider takes half of it away at full tilt. HALF, not all:
        /// a car with no friction at all does not drift, it simply stops being connected to the
        /// road, and there is no interesting driving anywhere in the last stretch of that range.
        /// Spending the whole slider on the half that is usable is worth more than a top end
        /// nobody would choose.
        /// </summary>
        /// <summary>
        /// The drift amount for the gear the car is in: the slider, plus that gear's bar.
        ///
        /// Summed and clamped rather than multiplied, because the bars are "extra slide" and
        /// extra is what a person means by it -- a slider at nought with second set to a half is
        /// a car that is planted everywhere except second, which is exactly the picture drawn.
        /// </summary>
        private float Amount(Vehicle car)
        {
            var amount = _cfg.DriftAmount;

            try
            {
                var gear = car.CurrentGear;

                if (gear >= 1)
                {
                    var bars = _cfg.GearSlide;
                    amount += bars[Math.Min(gear, bars.Length) - 1];
                }
            }
            catch
            {
                // The slider alone, then.
            }

            if (amount < 0f) amount = 0f;
            return amount > 1f ? 1f : amount;
        }

        /// <summary>Whether any gear asks for slide on its own, with the slider at nought.</summary>
        private bool AnySlide()
        {
            foreach (var bar in _cfg.GearSlide)
            {
                if (bar > 0f) return true;
            }

            return false;
        }

        private static float Grip(float amount)
        {
            if (amount < 0f) amount = 0f;
            if (amount > 1f) amount = 1f;

            return 1f - amount * 0.5f;
        }

        private static void Friction(Vehicle car, float amount)
        {
            try { Function.Call(Hash.SET_VEHICLE_FRICTION_OVERRIDE, car.Handle, Grip(amount)); }
            catch { /* the next pass will try again */ }
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

                // Back to normal grip, which is one. There is no "stop overriding" to call, so
                // the override stays -- set to the value that means it is not doing anything.
                if (held.Loosened) Function.Call(Hash.SET_VEHICLE_FRICTION_OVERRIDE, car.Handle, 1f);
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
