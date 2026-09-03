using System;
using GTA;
using VehicleTweaks.Core;

// Both namespaces have a Control and only one of them is a game control.
using Control = GTA.Control;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Second gear stays second for a while, rather than being somewhere the box passes through.
    ///
    /// SECOND IS THE USEFUL ONE and GTA barely lets you have it. It is the gear for coming out of
    /// a junction, holding a slide, or getting the back out on purpose -- and the box treats it
    /// as a step on the way to third, or drops back to first the moment the speed falls. Either
    /// way the thing you were doing ends because the transmission had an opinion about it.
    ///
    /// BOTH DIRECTIONS, which is the point. Holding against the upshift alone would still let it
    /// fall to first every time you scrubbed off speed; holding against the downshift alone
    /// leaves it running away into third. Second is held, and second means second.
    ///
    /// ON A TIMER, AND ONLY UNDER POWER. It lets go when the window runs out, so the box is never
    /// permanently one gear, and it lets go the moment the throttle does, so coasting to a stop
    /// behaves exactly as it always has. Holding a gear against somebody who is trying to slow
    /// down would be the transmission having an opinion again, in the other direction.
    ///
    /// THIS REPLACES HOLDING WHATEVER GEAR A WHEELSPIN STARTED IN, which was asked for, built,
    /// and then not wanted. Worth saying that the mechanism underneath is the same and its
    /// effectiveness is still unproven: the version that capped HighGear was deployed but never
    /// once ran, because the script was not reloaded before it was judged. So if second still
    /// slips away, the honest next question is whether these fields move the gearbox at all --
    /// and the log below is what answers it.
    /// </summary>
    internal sealed class Gearing
    {
        /// <summary>The gear this is about. Second, and only second.</summary>
        private const int Second = 2;

        private readonly Settings _cfg;

        private int _car;

        /// <summary>When the hold started, or zero when nothing is being held.</summary>
        private int _since;

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
                if (!_cfg.HoldSecond)
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

                var gear = Gear(car);

                // NOTHING HELD YET: second has to arrive on its own before it can be kept. This
                // does not put the car into second, it stops the box leaving one it chose.
                if (_since == 0)
                {
                    if (gear != Second || !OnPower()) return;

                    _since = Game.GameTime;
                    _top = Top(car);

                    Log.Debug("Second gear: holding, top gear " + _top + " capped to " + Second + ".");
                    Hold(car);
                    return;
                }

                // Off the throttle, or the window is up. Either way the box is the game's again.
                if (!OnPower())
                {
                    Restore(car, "throttle released");
                    return;
                }

                if (Game.GameTime - _since >= (int)(_cfg.HoldSecondSeconds * 1000f))
                {
                    Restore(car, "held its " + _cfg.HoldSecondSeconds.ToString("0.0") + "s");
                    return;
                }

                Hold(car);
            }
            catch (Exception ex)
            {
                // A throw in here must not leave a gearbox capped at second.
                Release();
                Log.Once("gearing", "Holding second fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Keeps it in second, against the box going either way.
        ///
        /// THREE FIELDS, AND THEY DO DIFFERENT JOBS. HighGear is the top gear the transmission
        /// has, so capping it removes third as an option rather than arguing against it.
        /// CurrentGear is what stops the drop to first, which no cap can do -- there is no floor
        /// to set, so the gear itself is written. NextGear closes the gap between the two.
        /// </summary>
        private void Hold(Vehicle car)
        {
            try
            {
                car.HighGear = Second;
                car.NextGear = Second;
                car.CurrentGear = Second;
            }
            catch
            {
                // The next frame will try again.
            }
        }

        private void Restore(Vehicle car, string why)
        {
            var top = _top;

            _since = 0;
            _top = 0;

            if (top > 0)
            {
                try { car.HighGear = top; }
                catch { /* the release by handle is the other chance */ }
            }

            Log.Debug("Second gear: let go, " + why + ".");
        }

        /// <summary>
        /// Uncaps the box wherever it was capped, and forgets the car.
        ///
        /// BY HANDLE, like every other override in here. Stepping straight out of one car into
        /// another has to give the first its gears back, and that car is no longer anybody's
        /// CurrentVehicle -- a car left capped at second is a car that will not do more than
        /// forty, with nothing on screen to say why.
        /// </summary>
        public void Release()
        {
            var handle = _car;
            var top = _top;

            _car = 0;
            _since = 0;
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

        /// <summary>
        /// Whether he is asking for drive.
        ///
        /// Coasting is not holding a gear, it is slowing down, and a box that refused to drop
        /// while somebody was trying to lose speed would be exactly the interference this
        /// feature exists to remove.
        /// </summary>
        private static bool OnPower()
        {
            try { return Game.IsControlPressed(Control.VehicleAccelerate); }
            catch { return false; }
        }

        private static int Gear(Vehicle car)
        {
            try { return car.CurrentGear; }
            catch { return 0; }
        }

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
