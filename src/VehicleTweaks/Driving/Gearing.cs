using System;
using GTA;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// While the tyres are spinning, the gear it is in is the gear it stays in.
    ///
    /// AN UPSHIFT ENDS A WHEELSPIN, which is the whole problem. The revs climb, the box takes
    /// the next gear, the torque at the wheels drops with it and the tyres hook up -- so the
    /// game puts a stop to the thing you were deliberately doing, at the exact moment it was
    /// working. Anybody holding a burnout or feeding power into a slide is fighting the gearbox
    /// as much as the car.
    ///
    /// THE SPIN IS MEASURED, not assumed from the throttle. WheelSpeed is how fast the wheels
    /// are turning and Speed is how fast the car is actually going: when the first is well past
    /// the second, the tyres are turning faster than the road is going by, and that is what
    /// wheelspin IS. A throttle position would have said nothing about whether the tyres had
    /// actually let go.
    ///
    /// THE GEAR IS TAKEN AT THE START OF THE SPIN and held there, rather than whatever gear is
    /// current each frame. Holding "the current one" would let a shift that happened between two
    /// frames become the new floor, and the box would walk up through the gears one escape at a
    /// time -- slower than doing nothing, and much harder to explain.
    /// </summary>
    internal sealed class Gearing
    {
        /// <summary>Below this, wheel speed and road speed disagree for uninteresting reasons.</summary>
        private const float Least = 1.5f;

        private readonly Settings _cfg;

        private int _car;
        private int _held;

        public Gearing(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.HoldGear)
                {
                    _car = 0;
                    _held = 0;
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists() || !AtTheWheel(car, me))
                {
                    Forget();
                    return;
                }

                if (car.Handle != _car)
                {
                    Forget();
                    _car = car.Handle;
                }

                if (!Spinning(car))
                {
                    if (_held != 0) Log.Debug("Gear hold: released, tyres hooked up.");
                    _held = 0;
                    return;
                }

                if (_held == 0)
                {
                    _held = Gear(car);
                    Log.Debug("Gear hold: holding " + _held + " while the tyres spin.");
                }

                Hold(car);
            }
            catch (Exception ex)
            {
                Forget();
                Log.Once("gearing", "Holding the gear fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Whether the tyres are turning faster than the road is going past.
        ///
        /// Both figures unsigned, because reversing is still wheelspin and the two would
        /// otherwise cancel into nonsense. IsInBurnout is taken as well: it is the game's own
        /// answer to the same question, and it catches the case where the car is not moving at
        /// all and the difference alone is small.
        /// </summary>
        private bool Spinning(Vehicle car)
        {
            try
            {
                if (car.IsInBurnout) return true;

                var wheels = Math.Abs(car.WheelSpeed);
                var road = Math.Abs(car.Speed);

                if (wheels < Least) return false;

                return wheels - road >= _cfg.HoldGearSlip;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Keeps the box where it was.
        ///
        /// NextGear is what it is shifting TOWARDS, so setting it back is the instruction not to
        /// go up. CurrentGear is only touched when the box has already climbed past the held
        /// one -- pushing it back down is the heavier hand of the two and it is not needed
        /// unless a shift has already got through.
        /// </summary>
        private void Hold(Vehicle car)
        {
            try
            {
                car.NextGear = _held;

                if (car.CurrentGear > _held) car.CurrentGear = _held;
            }
            catch
            {
                // The next frame will try again.
            }
        }

        private void Forget()
        {
            // Nothing to hand back: this only ever asks for a gear while the tyres are spinning,
            // and the moment it stops asking the box is the game's again. That is the whole
            // reason this one has no Release for the shutdown handler to call.
            _car = 0;
            _held = 0;
        }

        private static int Gear(Vehicle car)
        {
            try
            {
                var g = car.CurrentGear;
                return g < 1 ? 1 : g;
            }
            catch
            {
                return 1;
            }
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
