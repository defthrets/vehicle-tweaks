using System;
using GTA;
using VehicleTweaks.Core;

// Both namespaces have a Control and only one of them is a game control.
using Control = GTA.Control;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The gearbox, which is the game's, and the rev counter, which is not quite.
    ///
    /// STOCK SHIFTING. The version before this one held the gear through a slide and through a
    /// wheelspin, in both directions, on the argument that GTA shifts on road speed and a real
    /// box shifts on revs. The argument was sound and the result was still wrong: a gearbox that
    /// refuses shifts is a gearbox you are aware of, and being aware of the gearbox is the thing
    /// nobody wants. It is gone. Up, down, sideways, lit up -- the box decides, exactly as it
    /// always did.
    ///
    /// WHAT IS LEFT IS ONE SMALL THING: first and second are held very slightly longer under
    /// power. GTA's low gears are short and it leaves them early, so pulling away is two flat
    /// little shifts and then you are in third at walking pace with nothing to hear. Half a
    /// second more in each is enough to let the engine actually come up before it changes, and
    /// not enough to be a feature you would name if asked what the car was doing.
    ///
    /// ONLY FIRST AND SECOND, AND ONLY UNDER POWER. Third and above are the ones you spend real
    /// time in, where a held gear is a car that will not get out of its own way. Off the
    /// throttle the box is not upshifting anyway, so there is nothing there to hold and the
    /// clock simply resets -- which also means coming to a stop, sitting at a light and pulling
    /// away gets the hold from the start rather than having quietly spent it while stationary.
    ///
    /// THE REV COUNTER IS THE OTHER HALF and it stays. It is not a shift, it is a readout: GTA's
    /// revs follow road speed, so the one moment the engine is working hardest -- tyres
    /// spinning, car going nowhere -- is the one moment the needle says nothing at all.
    /// </summary>
    internal sealed class Drivetrain
    {
        /// <summary>The highest gear worth holding. Above this you are driving, not pulling away.</summary>
        private const int Low = 2;

        /// <summary>Below this much slip the tyres are gripping and the engine is the game's.</summary>
        private const float Gripping = 0.25f;

        /// <summary>How long the hold has to survive before it reports whether it took.</summary>
        private const int Proof = 8;

        private readonly Settings _cfg;

        private int _car;

        /// <summary>The gear being watched, so a change of gear restarts the clock.</summary>
        private int _gear;

        /// <summary>When the throttle went down in this gear, or nought for not yet.</summary>
        private int _since;

        /// <summary>The top gear the box had before it was capped, or nought for not capped.</summary>
        private int _top;

        /// <summary>How many frames the cap has been asked for, to report whether it took.</summary>
        private int _frames;

        public Drivetrain(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists() || !AtTheWheel(car, me) || !Geared(car))
                {
                    Release();
                    return;
                }

                if (car.Handle != _car)
                {
                    Release();
                    _car = car.Handle;
                }

                if (_cfg.RevsFollowWheels) Rev(car, Attitude.Wheelspin(car));

                if (_cfg.HoldLowGears) Hold(car);
                else Uncap(car);
            }
            catch (Exception ex)
            {
                // A throw in here must not leave a gearbox capped in second.
                Release();
                Log.Once("drivetrain", "The gearbox fell over: " + ex.Message);
            }
        }

        // ==================================================================
        // The low gears
        // ==================================================================

        /// <summary>
        /// First and second, kept a moment longer than the game would.
        ///
        /// CAPPING HighGear RATHER THAN REFUSING EACH SHIFT. HighGear is the top gear the
        /// transmission HAS, so setting it to the gear you are in removes everything above as an
        /// option -- the box never decides to shift and then gets overruled, it simply has
        /// nowhere to go. Nothing is written to CurrentGear at all, which is the difference
        /// between this and the version it replaces: a downshift is never blocked, so the car
        /// still drops a gear whenever it wants one.
        ///
        /// THE CLOCK RESETS OFF THE THROTTLE, which matters more than it sounds. It means the
        /// window is measured from the moment you actually ask for drive, so pulling away from a
        /// standstill gets the whole of it -- rather than having spent it idling at the lights in
        /// first with your foot on the brake.
        /// </summary>
        private void Hold(Vehicle car)
        {
            var gear = Read(car);

            // Reverse, neutral and everything from third up are the game's, untouched.
            if (gear < 1 || gear > Low)
            {
                Uncap(car);
                _gear = gear;
                _since = 0;
                return;
            }

            if (gear != _gear)
            {
                Uncap(car);
                _gear = gear;
                _since = 0;
            }

            if (!OnPower())
            {
                Uncap(car);
                _since = 0;
                return;
            }

            if (_since == 0) _since = Game.GameTime;

            if (Game.GameTime - _since >= (int)(_cfg.HoldLowGearsSeconds * 1000f))
            {
                Uncap(car);
                return;
            }

            if (_top == 0)
            {
                _top = Top(car);
                _frames = 0;
                Log.Debug("Gearbox: holding " + gear + " a moment, top gear was " + _top + ".");
            }

            // THE PROOF, and the reason this counts frames. Whether writing this field moves the
            // real gearbox has never been established -- the first attempt at it was deployed but
            // never once ran, because the script was not reloaded before it was judged. So the
            // cap reads the gear back and says, once, whether the car did what it was asked.
            if (_frames == Proof)
            {
                Log.Once("gearbox-proof", gear == _gear
                             ? "Gearbox: the cap TAKES - asked to stay in " + _gear +
                               " and the car is still in " + _gear + " eight frames later."
                             : "Gearbox: the cap DOES NOT TAKE - asked to stay in " + _gear +
                               " and the car is in " + gear + " eight frames later. Writing " +
                               "HighGear does not move this gearbox, and no amount of tuning " +
                               "the numbers will change that.");
            }

            _frames++;

            try { car.HighGear = _gear; }
            catch { /* the next frame will try again */ }
        }

        /// <summary>Gives the top gear back, if it was ever taken.</summary>
        private void Uncap(Vehicle car)
        {
            if (_top == 0) return;

            var top = _top;

            _top = 0;
            _frames = 0;

            try { car.HighGear = top; }
            catch { /* the release by handle is the other chance */ }
        }

        // ==================================================================
        // The revs
        // ==================================================================

        /// <summary>
        /// The rev counter, told about the tyres.
        ///
        /// CurrentRPM runs nought to one, which is not an assumption -- the speedo has been
        /// clamping it to that range and drawing the result since the tacho was built, and the
        /// cells fill and empty exactly as an engine does.
        ///
        /// ONLY EVER UPWARDS, AND NEVER HELD. Writing a number lower than the game's own would
        /// be a flat spot nobody asked for, and the instant the tyres hook up this stops writing
        /// entirely and the engine is the game's again on the very next frame. There is nothing
        /// to restore because nothing is being held, which is the whole reason it is safe to
        /// write a field this central.
        ///
        /// THE POINT IS THE NOISE AS MUCH AS THE NEEDLE. The engine note is generated from this
        /// same value, so a lit tyre should be something you HEAR rather than something you
        /// deduce from the car not going anywhere.
        /// </summary>
        private void Rev(Vehicle car, float spin)
        {
            if (spin <= Gripping) return;

            var full = _cfg.WheelspinSlip;
            if (full <= 0f) return;

            var over = spin / full;
            if (over > 1f) over = 1f;

            try
            {
                var rpm = car.CurrentRPM;
                var want = rpm + over * _cfg.RevsWheelspinGain;

                if (want > 1f) want = 1f;
                if (want > rpm) car.CurrentRPM = want;
            }
            catch
            {
                // The next frame will try again.
            }
        }

        // ==================================================================
        // Letting go
        // ==================================================================

        /// <summary>
        /// Uncaps the box wherever it was capped, and forgets the car.
        ///
        /// BY HANDLE, like every other override in here. Stepping straight out of one car into
        /// another has to give the first its gears back, and that car is no longer anybody's
        /// CurrentVehicle -- a car left capped in second is a car that will not do more than
        /// forty, with nothing on screen to say why.
        /// </summary>
        public void Release()
        {
            var handle = _car;
            var top = _top;

            _car = 0;
            _gear = 0;
            _since = 0;
            _top = 0;
            _frames = 0;

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

        // ==================================================================
        // Asking the car things
        // ==================================================================

        /// <summary>
        /// Whether this thing has a gearbox worth holding.
        ///
        /// Aircraft and boats are excluded because they have nothing of the sort. Electric cars
        /// are excluded because they genuinely have one gear, and holding a car in the only gear
        /// it has is a no-op that would still be writing a field every frame to achieve it.
        /// </summary>
        private static bool Geared(Vehicle car)
        {
            try
            {
                var m = car.Model;
                return (m.IsCar || m.IsBike || m.IsQuadBike) && !m.IsBicycle && !m.IsElectricVehicle;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Whether he is asking for drive. Coasting has no shift to hold.</summary>
        private static bool OnPower()
        {
            try { return Game.IsControlPressed(Control.VehicleAccelerate); }
            catch { return false; }
        }

        private static int Read(Vehicle car)
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
