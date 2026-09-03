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
    ///
    /// IT CAPS THE TOP GEAR, WHICH IS THE PART THAT ACTUALLY WORKS. The first version set
    /// NextGear -- the gear the box is shifting towards -- on the reasoning that setting it back
    /// is the instruction not to go up. It fired correctly, held for whole seconds at a time,
    /// said so in the log, and did nothing you could feel: the box simply chose again on the
    /// next frame. NextGear is a statement of intent and the gearbox is the one making it.
    ///
    /// HighGear is the top gear the box HAS. Cap it and there is nothing above to shift into,
    /// which is a fact about the transmission rather than a request to it. That makes this a
    /// real override -- a car left capped at first is a car stuck in first -- so the original is
    /// written down before it is touched and put back on every path out, including the one where
    /// the script is shutting down. This class had no Release before, and the comment saying it
    /// did not need one was right about NextGear and is wrong now.
    /// </summary>
    internal sealed class Gearing
    {
        /// <summary>Below this, wheel speed and road speed disagree for uninteresting reasons.</summary>
        private const float Least = 1.5f;

        private readonly Settings _cfg;

        private int _car;
        private int _held;

        /// <summary>The top gear the box had before it was capped, so it can be given back.</summary>
        private int _top;

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
                    Release();
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists() || !AtTheWheel(car, me))
                {
                    Release();
                    return;
                }

                if (car.Handle != _car)
                {
                    Release();
                    _car = car.Handle;
                }

                if (!Spinning(car))
                {
                    if (_held != 0)
                    {
                        Restore(car);
                        Log.Debug("Gear hold: released, tyres hooked up.");
                    }

                    return;
                }

                if (_held == 0)
                {
                    _held = Gear(car);
                    _top = Top(car);

                    Log.Debug("Gear hold: holding " + _held + " while the tyres spin, top gear " +
                              _top + " capped to " + _held + ".");
                }

                Hold(car);
            }
            catch (Exception ex)
            {
                // A throw in here must not leave a gearbox capped. Release rather than merely
                // forgetting, which is what this used to do back when there was nothing to undo.
                Release();
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
        /// Keeps the box where it was, by leaving it nowhere to go.
        ///
        /// THE CAP IS THE ONE THAT BITES. HighGear is the top gear the transmission has, so
        /// setting it to the held gear removes the option rather than arguing against it -- and
        /// arguing was what the first version did, setting NextGear every frame while the box
        /// cheerfully chose again on the next one.
        ///
        /// The other two stay as belt and braces. They cost a native call each and they close
        /// the gap between the cap being set and the box noticing.
        /// </summary>
        private void Hold(Vehicle car)
        {
            try
            {
                car.HighGear = _held;
                car.NextGear = _held;

                if (car.CurrentGear > _held) car.CurrentGear = _held;
            }
            catch
            {
                // The next frame will try again.
            }
        }

        /// <summary>Gives the box its gears back. Safe when nothing was ever taken.</summary>
        private void Restore(Vehicle car)
        {
            var top = _top;

            _held = 0;
            _top = 0;

            if (top <= 0) return;

            try { car.HighGear = top; }
            catch { /* the release by handle below is the other chance */ }
        }

        /// <summary>
        /// Uncaps the box wherever it was capped, and forgets the car.
        ///
        /// BY HANDLE, like every other override in here. Stepping straight out of one car and
        /// into another has to give the first one its gears back, and that car is no longer
        /// anybody's CurrentVehicle -- a car left capped at first is a car stuck in first, with
        /// nothing on screen to say why.
        /// </summary>
        public void Release()
        {
            var handle = _car;
            var top = _top;

            _car = 0;
            _held = 0;
            _top = 0;

            if (handle == 0 || top <= 0) return;

            try
            {
                var car = (Vehicle)Entity.FromHandle(handle);
                if (car != null && car.Exists()) car.HighGear = top;
            }
            catch
            {
                // The car is gone, and its gearbox went with it.
            }
        }

        /// <summary>The top gear the box has, or nothing if it will not say.</summary>
        private static int Top(Vehicle car)
        {
            try
            {
                var top = car.HighGear;
                return top > 0 ? top : 0;
            }
            catch
            {
                return 0;
            }
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
