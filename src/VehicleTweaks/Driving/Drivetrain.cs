using System;
using GTA;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The gearbox and the rev counter, rewritten around what the car is doing rather than how
    /// fast it happens to be going.
    ///
    /// THE ROOT CAUSE OF EVERY DRIVETRAIN COMPLAINT IN THIS GAME IS ONE FACT: GTA's automatic
    /// box shifts on ROAD SPEED. Every symptom follows from it and they are all the same bug --
    ///
    ///   Sideways, the road speed still climbs, so it upshifts. The torque at the wheels drops
    ///   with the shift, the tyres hook up, and the slide ends -- at the exact moment it was
    ///   working, because of a decision that had nothing to do with the engine.
    ///
    ///   Scrubbing speed sideways, the road speed falls, so it drops to first. That is a torque
    ///   spike into a car that is already loose, which either snaps it round or bogs it flat.
    ///
    ///   Spinning the tyres, the road speed does not rise at all, so the revs do not either.
    ///   The one moment the engine is doing the most work is the one moment nothing on the
    ///   dashboard or in the exhaust note says so.
    ///
    /// A REAL BOX SHIFTS ON ENGINE REVS, and a real rev counter reads the ENGINE. So both are
    /// moved off road speed and onto what the wheels are actually doing.
    ///
    /// AND THAT IS WHY THIS IS NOT A DRIFT MODE. Wheel speed and road speed are the same number
    /// when the tyres are gripping, and the slide angle is nought when the car is pointing where
    /// it is going -- so on a normal drive every condition in here reads false and the gearbox
    /// is the game's, untouched, with not one field written. There is nothing to switch on
    /// before a corner and nothing to switch off afterwards. It is the same rule; ordinary
    /// driving simply never meets it.
    ///
    /// IT BLOCKS SHIFTS, IT DOES NOT SCHEDULE THEM. Refusing the two shifts that ruin things is
    /// a small claim that can be made safely -- the box re-decides every frame and gets its own
    /// answer back the moment the condition clears. Owning the whole shift schedule would mean
    /// inventing a torque curve per model for six hundred vehicles and getting it wrong for most
    /// of them. This does the half that is worth having.
    ///
    /// WHAT IT REPLACES was a hold on second gear against a stopwatch: the right instinct
    /// pointed at the wrong trigger. Second was never the thing that mattered -- being sideways
    /// was -- and a stopwatch cannot tell the difference between a drift and a traffic light.
    /// </summary>
    internal sealed class Drivetrain
    {
        /// <summary>
        /// Below this, in metres a second, the gear is left entirely alone.
        ///
        /// A HELD GEAR THROUGH A STOP IS A CAR THAT WILL NOT PULL AWAY. Coming to rest is the
        /// one time the box genuinely has to be allowed down to first, and a burnout from a
        /// standstill -- which is stationary and lit at the same time -- would otherwise hold
        /// whatever gear it started in for as long as the tyres were spinning.
        /// </summary>
        private const float Crawl = 2.0f;

        /// <summary>Below this much slip the tyres are gripping and the engine is the game's.</summary>
        private const float Gripping = 0.25f;

        /// <summary>How long the hold has to survive before it reports whether it took.</summary>
        private const int Proof = 8;

        private readonly Settings _cfg;

        private int _car;

        /// <summary>The gear being held, or nought when the box is the game's.</summary>
        private int _gear;

        /// <summary>The top gear the box had before it was capped, so it can be given back.</summary>
        private int _top;

        /// <summary>How many frames the hold has been asked for, to report whether it took.</summary>
        private int _frames;

        public Drivetrain(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.SmartGearbox)
                {
                    Release();
                    return;
                }

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

                var sideways = Attitude.Sideways(car);
                var spin = Attitude.Wheelspin(car);

                if (_cfg.RevsFollowWheels) Rev(car, spin);

                Box(car, sideways, spin);
            }
            catch (Exception ex)
            {
                // A throw in here must not leave a gearbox capped in second.
                Release();
                Log.Once("drivetrain", "The gearbox fell over: " + ex.Message);
            }
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
        /// same value, so a lit tyre should now be something you HEAR rather than something you
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
        // The gearbox
        // ==================================================================

        /// <summary>
        /// The gear it is in is the gear it keeps, while the car is sideways or lit up.
        ///
        /// TAKEN AT THE START AND HELD THERE, not read fresh every frame. A shift that slipped
        /// through between two frames would otherwise become the new held gear, and the box
        /// walks up or down through the range one escape at a time -- which looks exactly like
        /// the hold not working at all.
        ///
        /// THREE FIELDS, THREE JOBS. HighGear is the top gear the transmission HAS, so capping
        /// it removes everything above as an option rather than arguing with each shift as it
        /// comes. CurrentGear is the only thing that stops a drop, because there is no floor to
        /// set -- the gear itself has to be written. NextGear closes the gap between them.
        /// </summary>
        private void Box(Vehicle car, float sideways, float spin)
        {
            var drifting = _cfg.HoldGearSideways && sideways >= _cfg.DriftAngle;
            var lit = _cfg.HoldGearWheelspin && spin >= _cfg.WheelspinSlip;

            var gear = Read(car);
            var moving = Math.Abs(Speed(car)) >= Crawl;

            // Reverse and neutral are not gears worth arguing about, and a car at walking pace
            // has to be allowed down to first or it will not pull away from the stop it is
            // in the middle of making.
            if ((!drifting && !lit) || gear < 1 || !moving)
            {
                Handback(car);
                return;
            }

            if (_gear == 0)
            {
                _gear = gear;
                _top = Top(car);
                _frames = 0;

                Log.Debug("Gearbox: holding " + _gear + " (" +
                          (drifting ? "sideways " + sideways.ToString("0") + " deg" : "tyres lit") +
                          "), top gear was " + _top + ".");
            }

            // THE PROOF, and the reason this counts frames at all. Whether writing these fields
            // moves the real gearbox has never actually been established: the version that
            // capped HighGear was deployed but never once ran, because the script was not
            // reloaded before it was judged. So the hold reports whether it took, once, from the
            // car's own read-back -- and the question gets answered by a drive rather than by an
            // opinion.
            if (_frames == Proof)
            {
                Log.Once("gearbox-proof", gear == _gear
                             ? "Gearbox: the hold TAKES - asked for " + _gear +
                               " and the car is still in " + _gear + " eight frames later."
                             : "Gearbox: the hold DOES NOT TAKE - asked for " + _gear +
                               " and the car is in " + gear + " eight frames later. Writing " +
                               "these fields does not move this gearbox, and no amount of " +
                               "tuning the numbers will change that.");
            }

            _frames++;

            try
            {
                car.HighGear = _gear;
                if (gear != _gear) car.NextGear = _gear;
                car.CurrentGear = _gear;
            }
            catch
            {
                // The next frame will try again.
            }
        }

        /// <summary>Gives the box back, wherever it was capped, and stops holding a gear.</summary>
        private void Handback(Vehicle car)
        {
            if (_gear == 0) return;

            var top = _top;

            _gear = 0;
            _top = 0;
            _frames = 0;

            if (top > 0)
            {
                try { car.HighGear = top; }
                catch { /* the release by handle is the other chance */ }
            }

            Log.Debug("Gearbox: let go.");
        }

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
        /// Whether this thing has a gearbox worth arguing with.
        ///
        /// Aircraft and boats are excluded because they have nothing of the sort. Electric cars
        /// are excluded because they genuinely have one gear, and holding a car in the only gear
        /// it has is a no-op that would still be writing three fields a frame to achieve it.
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

        private static float Speed(Vehicle car)
        {
            try { return car.Speed; }
            catch { return 0f; }
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
