using System;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// How much the tyres smoke when they are spinning.
    ///
    /// THE SMOKE IS THE TEMPERATURE. GTA has no "amount of smoke" to set; what it has is a
    /// temperature per wheel, which rises as a tyre is scrubbed and which the smoke effect is
    /// drawn from. Hot tyre, thick smoke. So this does not draw anything and does not add an
    /// effect of its own -- it leans on the number the game is already using, which is why the
    /// smoke it produces is the game's own smoke, in the game's own colour, from the right place
    /// on the right wheel.
    ///
    /// ONLY WHILE THEY ARE ACTUALLY SPINNING, and that is two questions rather than one. The game
    /// will say whether the car is in a burnout, which is the handbrake-and-throttle case and the
    /// one people mean by the word; it will not say anything about a wheel spinning up out of a
    /// corner or a slide held on the throttle. Both are the same thing to a tyre, so the second
    /// is worked out the way a person would: the wheels are turning faster than the car is
    /// travelling. Neither of them is true while you are simply driving, which is the point --
    /// a tyre that smokes at a set of lights is a bug, not a feature.
    ///
    /// MORE IS A FLOOR, LESS IS A SCALE, and they are different on purpose. To ADD smoke the
    /// temperature is held at or above a figure, so the game's own heat still runs the show and
    /// this only stops it falling below what you asked for -- multiplying instead would compound
    /// every frame and saturate on the first wheelspin, which is a switch pretending to be a
    /// slider. To TAKE smoke away there is nothing to hold up, so the temperature is scaled down
    /// and allowed to compound, which is exactly what "much less" should do.
    ///
    /// NOT HANDED BACK, BECAUSE THERE IS NOTHING TO HAND BACK. A tyre temperature is not a
    /// setting, it is a reading -- the game recomputes it from the road every frame, so anything
    /// written here is gone within a second of the wheels gripping again.
    /// </summary>
    internal sealed class Smoke
    {
        /// <summary>
        /// The temperature a wheel is held at when the setting is turned all the way up.
        ///
        /// MEASURED BY WHAT IT LOOKS LIKE RATHER THAN BY WHAT IT MEANS. The field has no unit
        /// anybody has written down; what is known is that a tyre scrubbed hard sits somewhere in
        /// the tens and that the smoke thickens with it. A hundred and eighty at full is well
        /// past anything the game does on its own, so the slider spends its range on the part
        /// that is visible.
        /// </summary>
        private const float Hottest = 180f;

        /// <summary>How much faster the wheels have to turn than the car is going, in m/s.</summary>
        private const float Spinning = 3f;

        /// <summary>Near enough to one that the setting is doing nothing.</summary>
        private const float Nothing = 0.02f;

        private readonly Settings _cfg;

        private bool _said;

        public Smoke(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (Math.Abs(_cfg.TyreSmoke - 1f) < Nothing) return;

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists() || me.SeatIndex != VehicleSeat.Driver) return;

                if (!Spun(car)) return;

                foreach (var wheel in car.Wheels)
                {
                    // THE DRIVEN WHEELS ONLY. A front-drive car does not smoke its rears and a
                    // rear-drive car does not smoke its fronts, and the game already knows which
                    // is which -- so this asks rather than assuming the usual answer.
                    if (!wheel.IsDrivingWheel || !wheel.IsTouchingSurface) continue;

                    var was = wheel.Temperature;

                    if (_cfg.TyreSmoke > 1f)
                    {
                        var floor = (_cfg.TyreSmoke - 1f) / 4f * Hottest;

                        if (was < floor) wheel.Temperature = floor;
                    }
                    else
                    {
                        wheel.Temperature = was * _cfg.TyreSmoke;
                    }

                    Say(was, wheel.Temperature);
                }
            }
            catch (Exception ex)
            {
                Log.Once("smoke", "The tyre smoke fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Whether the wheels are turning faster than the car is going anywhere.
        ///
        /// THE GAME ANSWERS HALF OF THIS AND NOT THE OTHER HALF. IS_VEHICLE_IN_BURNOUT is true
        /// for the handbrake-and-throttle stand-still and nothing else, so a wheel spun up out of
        /// a junction or held sideways on the throttle would smoke exactly as much as it always
        /// did. WheelSpeed against Speed catches those: it is the definition of wheelspin.
        /// </summary>
        private static bool Spun(Vehicle car)
        {
            try
            {
                if (Function.Call<bool>(Hash.IS_VEHICLE_IN_BURNOUT, car)) return true;

                return Math.Abs(car.WheelSpeed) - Math.Abs(car.Speed) > Spinning;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Says once what it read and what it wrote.
        ///
        /// BECAUSE THE UNIT IS A GUESS AND THIS IS HOW IT STOPS BEING ONE. If the game's own
        /// figure during a burnout turns out to be two hundred rather than twenty, Hottest is
        /// wrong by an order of magnitude and the slider does nothing over half its travel. One
        /// line the first time a wheel is spun says which.
        /// </summary>
        private void Say(float was, float now)
        {
            if (_said) return;

            _said = true;

            Log.Info("Tyre smoke: wheel was at " + was.ToString("0.0") + ", set to " +
                     now.ToString("0.0") + " at x" + _cfg.TyreSmoke.ToString("0.00") + ".");
        }
    }
}
