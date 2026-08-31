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
    /// driving without steering that way for a couple of seconds. Letting go does NOT cancel it,
    /// which is what lets you signal at a red light and then release the wheel.
    ///
    /// THE RULES THEMSELVES ARE NOT IN HERE. They are in Core.BlinkerRules, which knows about
    /// milliseconds and a wheel direction and nothing else, and which is covered by tests that
    /// run without the game. What is left here is the half that genuinely needs a car: reading
    /// the steering controls, reading the speed, and putting the lights on. If this file gets
    /// long again, something has been put in the wrong one.
    /// </summary>
    internal sealed class Blinkers
    {
        private readonly Settings _cfg;
        private readonly BlinkerRules _rules;

        /// <summary>The car this is about, by handle, so a change of car re-reads its lights.</summary>
        private int _car;

        private bool _hazardKeyDown;
        private readonly Chord _hazardChord;

        public Blinkers(Settings cfg)
        {
            _cfg = cfg;
            _rules = new BlinkerRules(cfg);
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
                    return;
                }

                if (car.Handle != _car)
                {
                    _car = car.Handle;
                    _rules.Enter(Game.GameTime, Reading(car), Both(car));
                }

                if (Pressed()) Flip(car);

                _rules.Step(Game.GameTime, Wheel(), Speed(car));

                Apply(car);
            }
            catch (Exception ex)
            {
                Log.Once("blinkers", "The indicators fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// The lights, set from whatever the rules now say.
        ///
        /// EVERY FRAME, unconditionally, rather than only when something changed. These are two
        /// booleans and setting them to what they already are costs nothing -- and the "only on
        /// change" version has to be right about what the CAR currently shows, not just about
        /// what we last set, which is a second copy of the truth waiting to drift from the first.
        /// </summary>
        private void Apply(Vehicle car)
        {
            try
            {
                // THE PROPERTIES, not SET_VEHICLE_INDICATOR_LIGHTS. The native takes an index
                // whose meaning has to be looked up and remembered; these say which side they
                // are in their own names, and a mod that indicates the wrong way at every
                // junction is worse than one that does nothing at all.
                car.IsLeftIndicatorLightOn = _rules.LeftOn;
                car.IsRightIndicatorLightOn = _rules.RightOn;
            }
            catch (Exception ex)
            {
                Log.Once("blinker-set", "Could not work the indicators: " + ex.Message);
            }
        }

        private void Flip(Vehicle car)
        {
            var on = _rules.ToggleHazard(Game.GameTime);
            Apply(car);

            Log.Debug("Hazards " + (on ? "on." : "off."));
        }

        /// <summary>The hazard key, or the pad's chord. Both read every frame, neither skipped.</summary>
        private bool Pressed()
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
            // was down; skipping it on the frames the key fires leaves that memory stale.
            var chord = _hazardChord.Fired();

            return key || chord;
        }

        /// <summary>The steering controls, handed to the rules as a direction.</summary>
        private int Wheel()
        {
            try
            {
                return BlinkerRules.Wheel(
                    Game.GetControlValueNormalized(Control.VehicleMoveLeftOnly),
                    Game.GetControlValueNormalized(Control.VehicleMoveRightOnly),
                    Game.GetControlValueNormalized(Control.VehicleMoveLeftRight),
                    _cfg.BlinkerDeadzone,
                    _cfg.BlinkerInvert);
            }
            catch
            {
                return BlinkerRules.Off;
            }
        }

        private static float Speed(Vehicle car)
        {
            try { return car.Speed; }
            catch { return 0f; }
        }

        private static int Reading(Vehicle car)
        {
            try
            {
                if (car.IsLeftIndicatorLightOn) return BlinkerRules.LeftSide;
                if (car.IsRightIndicatorLightOn) return BlinkerRules.RightSide;
            }
            catch
            {
                // Treated as off, which the next steer will correct.
            }

            return BlinkerRules.Off;
        }

        private static bool Both(Vehicle car)
        {
            try { return car.IsLeftIndicatorLightOn && car.IsRightIndicatorLightOn; }
            catch { return false; }
        }
    }
}
