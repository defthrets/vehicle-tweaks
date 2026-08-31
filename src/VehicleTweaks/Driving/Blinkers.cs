using System;
using System.Windows.Forms;
using GTA;
using VehicleTweaks.Core;
using VehicleTweaks.Input;

// Both namespaces have a Control and only one of them is a game control.
using Control = GTA.Control;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Indicators worked by the steering wheel, the way a real one self-cancels.
    ///
    /// Hold the wheel over for a second and that side comes on. It goes off when you steer the
    /// OTHER way -- which is what straightening out of a turn is -- or when you have been
    /// driving without steering that way for a couple of seconds.
    ///
    /// LETTING GO DOES NOT CANCEL IT, and that is the whole point of the design rather than a
    /// detail. Sitting at a red light you turn the wheel, let it centre, and the indicator
    /// stays on; that is how you signal before you have moved. The straight-ahead cancel only
    /// applies while the car is actually MOVING, so a centred wheel at a standstill means
    /// nothing at all. Without that split, signalling at the lights would be impossible: the
    /// wheel returns to centre the moment you release it, and a cancel that only asked "is the
    /// wheel straight" would put it out again a second later.
    ///
    /// No keys, no prompts, no notifications. It is a car behaving like a car.
    /// </summary>
    internal sealed class Blinkers
    {
        private readonly Settings _cfg;

        /// <summary>-1 left, 0 off, +1 right.</summary>
        private int _side;

        /// <summary>Which way the wheel is over, and since when.</summary>
        private int _steering;
        private int _steeringSince;

        /// <summary>When the car last did something that counts as "not still turning that way".</summary>
        private int _straightSince;

        /// <summary>The car this is about, by handle, so a change of car clears it.</summary>
        private int _car;

        /// <summary>Both sides at once, which is not a side and so is kept apart from _side.</summary>
        private bool _hazard;

        private bool _hazardKeyDown;
        private readonly Chord _hazardChord;

        public Blinkers(Settings cfg)
        {
            _cfg = cfg;
            _hazardChord = new Chord(cfg.PadModifier, cfg.PadHazard, "hazards");

            Log.Info("Hazards on " + cfg.HazardKey + ", or on a pad " + _hazardChord.Describe() + ".");
        }

        public void Update(Ped me, Vehicle car, bool driving)
        {
            if (!_cfg.Blinkers) return;

            try
            {
                if (!driving || car == null || !car.Exists())
                {
                    // Left exactly as they are. A car you walked away from indicating is still
                    // indicating, which is both what a real one does and what the ignition next
                    // door does with an engine -- and a car left on its hazards is the whole
                    // reason anybody puts hazards on.
                    _car = 0;
                    _steering = 0;
                    return;
                }

                if (car.Handle != _car)
                {
                    _car = car.Handle;

                    // READ OFF THE CAR, not carried over from the last one. Hazards belong to
                    // the vehicle, not to the driver: getting out of a car with them on and into
                    // another should not bring them with you, and getting back into the first
                    // one should find them still going.
                    _hazard = Both(car);
                    _side = _hazard ? 0 : Reading(car);
                    _steering = 0;
                    _steeringSince = Game.GameTime;
                    _straightSince = Game.GameTime;
                }

                if (Hazard()) Toggle(car);

                // HAZARDS WIN, and the steering is not even read while they are on. Both sides
                // lit is not a side, so every rule below it -- arm this side, cancel on the
                // opposite lock, cancel when straight -- is about a question that currently has
                // no answer. Letting them run would have the first corner after switching the
                // hazards on quietly turning them into an ordinary indicator.
                if (_hazard)
                {
                    Hold(car);
                    return;
                }

                var now = Game.GameTime;
                var wheel = Wheel();

                // ---- coming on -------------------------------------------------
                if (wheel != _steering)
                {
                    _steering = wheel;
                    _steeringSince = now;
                }

                var armed = (int)(_cfg.BlinkerArmSeconds * 1000f);

                if (wheel != 0 && _side != wheel && now - _steeringSince >= armed) Set(car, wheel);

                // ---- going off -------------------------------------------------
                if (_side == 0) return;

                // Steering the other way -- BUT HELD, not merely touched.
                //
                // Straightening out of a turn IS steering the other way, which is why a real
                // indicator cancels there and why this needs no separate rule for "the turn is
                // finished". The catch is that straightening between two turns THE SAME WAY is
                // also steering the other way: left at one junction, a flick of right to line
                // the car up, left again at the next. Cancelling on the input alone killed the
                // indicator in the gap, on the one manoeuvre where you most want it to stay.
                //
                // A duration separates them, because they differ in duration and in nothing
                // else. Coming out of a turn you hold the wheel over for the best part of a
                // second; lining the car up between two turns is a flick.
                if (wheel == -_side)
                {
                    if (now - _steeringSince >= (int)(_cfg.BlinkerOppositeSeconds * 1000f))
                        Set(car, 0);

                    return;
                }

                // Still holding it that way: nothing has gone straight.
                if (wheel == _side)
                {
                    _straightSince = now;
                    return;
                }

                // Wheel centred. MOVING is what makes that mean anything -- at a standstill a
                // centred wheel is just a wheel nobody is holding, and cancelling there would
                // make it impossible to signal before pulling away.
                float speed;
                try { speed = car.Speed; }
                catch { speed = 0f; }

                if (speed < _cfg.BlinkerMinSpeed)
                {
                    _straightSince = now;
                    return;
                }

                if (now - _straightSince >= (int)(_cfg.BlinkerCancelSeconds * 1000f)) Set(car, 0);
            }
            catch (Exception ex)
            {
                Log.Once("blinkers", "The indicators fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Which way the wheel is being held: -1 left, 0 centred, +1 right.
        ///
        /// READ FROM THE TWO ONE-SIDED CONTROLS, not from the signed axis, and that is not
        /// fussiness. The axis is one number whose sign means left or right by a convention
        /// this code would have to assume -- and getting it backwards is a mod that indicates
        /// the wrong way at every junction, which is worse than one that does nothing. The
        /// LeftOnly and RightOnly controls each report their own side, so there is no
        /// convention to be wrong about.
        ///
        /// The signed axis is still read as a fallback for setups where the one-sided controls
        /// stay at zero, and THAT is the reading BlinkerInvert exists for.
        /// </summary>
        private int Wheel()
        {
            var dead = _cfg.BlinkerDeadzone;

            try
            {
                var left = Game.GetControlValueNormalized(Control.VehicleMoveLeftOnly);
                var right = Game.GetControlValueNormalized(Control.VehicleMoveRightOnly);

                if (left > dead || right > dead) return right > left ? 1 : -1;

                var axis = Game.GetControlValueNormalized(Control.VehicleMoveLeftRight);
                if (Math.Abs(axis) <= dead) return 0;

                var side = axis > 0f ? 1 : -1;
                return _cfg.BlinkerInvert ? -side : side;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>The hazard key, or the pad's chord. Both read every frame, neither skipped.</summary>
        private bool Hazard()
        {
            var key = false;

            try
            {
                var down = Game.IsKeyPressed(_cfg.HazardKey);
                key = down && !_hazardKeyDown;
                _hazardKeyDown = down;
            }
            catch
            {
                _hazardKeyDown = false;
            }

            // NOT SHORT-CIRCUITED. Fired() is what advances the chord's own memory of whether it
            // was down; skipping it on the frames the key fires leaves it stale.
            var chord = _hazardChord.Fired();

            return key || chord;
        }

        private void Toggle(Vehicle car)
        {
            _hazard = !_hazard;

            if (_hazard)
            {
                Hold(car);
                Log.Debug("Hazards on.");
                return;
            }

            // Off means off, not "back to whatever was indicating before". You put the hazards
            // on deliberately and you take them off deliberately; restoring a turn signal you
            // had cancelled two junctions ago would be the mod remembering something you do not.
            Set(car, 0);
            Log.Debug("Hazards off.");
        }

        /// <summary>Both sides, every frame, because the game blinks them and we only own the state.</summary>
        private void Hold(Vehicle car)
        {
            try
            {
                car.IsLeftIndicatorLightOn = true;
                car.IsRightIndicatorLightOn = true;
            }
            catch (Exception ex)
            {
                Log.Once("hazard-set", "Could not work the hazards: " + ex.Message);
            }
        }

        private static bool Both(Vehicle car)
        {
            try { return car.IsLeftIndicatorLightOn && car.IsRightIndicatorLightOn; }
            catch { return false; }
        }

        private static int Reading(Vehicle car)
        {
            try
            {
                if (car.IsLeftIndicatorLightOn) return -1;
                if (car.IsRightIndicatorLightOn) return 1;
            }
            catch
            {
                // Treated as off, which the next steer will correct.
            }

            return 0;
        }

        private void Set(Vehicle car, int side)
        {
            _side = side;
            _straightSince = Game.GameTime;

            try
            {
                // THE PROPERTIES, not SET_VEHICLE_INDICATOR_LIGHTS. The native takes an index
                // whose meaning has to be looked up and remembered; these say which side they
                // are in their own names, and a mod that indicates the wrong way at every
                // junction is worse than one that does nothing at all.
                car.IsLeftIndicatorLightOn = side == -1;
                car.IsRightIndicatorLightOn = side == 1;
            }
            catch (Exception ex)
            {
                Log.Once("blinker-set", "Could not work the indicators: " + ex.Message);
            }
        }
    }
}
