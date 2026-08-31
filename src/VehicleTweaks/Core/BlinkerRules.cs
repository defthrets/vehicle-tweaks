namespace VehicleTweaks.Core
{
    /// <summary>
    /// When the indicators come on and when they go off, with nothing in it that needs a game.
    ///
    /// SPLIT OUT SO IT CAN BE RUN. This is the most intricate thing in the mod -- arm after a
    /// second of held lock, cancel on a HELD opposite lock but not a flick, cancel when straight
    /// but only above a speed -- and every one of those rules is a claim about what happens after
    /// a particular sequence of inputs over a particular stretch of time. Claims like that are
    /// exactly what a test is for, and until this class existed not one of them could be checked
    /// without a car, a road, a stopwatch and a pair of eyes.
    ///
    /// It matters more here than the usual argument for testability. Every bug this mod has had
    /// was in code that could not be run outside the game: the identity comparison that stopped
    /// the ignition ever firing, the exit-animation race that stopped the radio ever being set.
    /// Both were found by reading, late, after being shipped as working.
    ///
    /// So there is no GTA type in this file. It takes a time in milliseconds, a wheel direction
    /// as -1, 0 or +1, and a speed; it hands back which lights should be lit. The half that
    /// knows about vehicles lives in Driving.Blinkers and is thin enough to read in one go.
    /// </summary>
    internal sealed class BlinkerRules
    {
        public const int Off = 0;
        public const int LeftSide = -1;
        public const int RightSide = 1;

        private readonly Settings _cfg;

        private int _side;
        private int _steering;
        private int _steeringSince;
        private int _straightSince;
        private bool _hazard;

        public BlinkerRules(Settings cfg)
        {
            _cfg = cfg;
        }

        /// <summary>-1 left, 0 off, +1 right. Meaningless while the hazards are on.</summary>
        public int Side => _side;

        public bool Hazard => _hazard;

        public bool LeftOn => _hazard || _side == LeftSide;
        public bool RightOn => _hazard || _side == RightSide;

        /// <summary>
        /// Starts on a car, taking its current state as the truth.
        ///
        /// READ OFF THE CAR rather than carried over from the last one. Indicators and hazards
        /// belong to the vehicle, not to the driver: getting out of one that is indicating and
        /// into another should not bring the indicator with you, and coming back to the first
        /// should find it still going.
        /// </summary>
        public void Enter(int now, int side, bool hazard)
        {
            _hazard = hazard;
            _side = hazard ? Off : side;
            _steering = 0;
            _steeringSince = now;
            _straightSince = now;
        }

        /// <summary>Flips the hazards, and returns what they now are.</summary>
        public bool ToggleHazard(int now)
        {
            _hazard = !_hazard;

            // Off means off, not "back to whatever was indicating before". You put the hazards
            // on deliberately and take them off deliberately; restoring a turn signal cancelled
            // two junctions ago would be the mod remembering something the driver does not.
            if (!_hazard) Set(now, Off);

            return _hazard;
        }

        /// <summary>
        /// One frame of steering.
        /// </summary>
        /// <param name="now">Milliseconds, from the same clock every time.</param>
        /// <param name="wheel">-1 held left, 0 centred, +1 held right.</param>
        /// <param name="speed">Metres per second.</param>
        public void Step(int now, int wheel, float speed)
        {
            // HAZARDS WIN, and the steering is not even read while they are on. Both sides lit is
            // not a side, so every rule below is about a question that currently has no answer.
            // Letting them run would have the first corner after switching the hazards on quietly
            // turning them into an ordinary indicator.
            if (_hazard) return;

            if (wheel != _steering)
            {
                _steering = wheel;
                _steeringSince = now;
            }

            // ---- coming on -------------------------------------------------
            var armed = (int)(_cfg.BlinkerArmSeconds * 1000f);

            if (wheel != 0 && _side != wheel && now - _steeringSince >= armed) Set(now, wheel);

            // ---- going off -------------------------------------------------
            if (_side == Off) return;

            // Steering the other way -- BUT HELD, not merely touched.
            //
            // Straightening out of a turn IS steering the other way, which is why a real
            // indicator cancels there and why this needs no separate rule for "the turn is
            // finished". The catch is that straightening between two turns THE SAME WAY is also
            // steering the other way: left at one junction, a flick of right to line the car up,
            // left again at the next. Cancelling on the input alone killed the indicator in the
            // gap, on the one manoeuvre where you most want it to stay.
            //
            // A duration separates them, because they differ in duration and in nothing else.
            if (wheel == -_side)
            {
                if (now - _steeringSince >= (int)(_cfg.BlinkerOppositeSeconds * 1000f)) Set(now, Off);
                return;
            }

            // Still holding it that way: nothing has gone straight.
            if (wheel == _side)
            {
                _straightSince = now;
                return;
            }

            // Wheel centred. MOVING is what makes that mean anything -- at a standstill a centred
            // wheel is just a wheel nobody is holding, and cancelling there would make it
            // impossible to signal before pulling away from a red light.
            if (speed < _cfg.BlinkerMinSpeed)
            {
                _straightSince = now;
                return;
            }

            if (now - _straightSince >= (int)(_cfg.BlinkerCancelSeconds * 1000f)) Set(now, Off);
        }

        private void Set(int now, int side)
        {
            _side = side;
            _straightSince = now;
        }

        /// <summary>
        /// Which way the wheel is being held: -1 left, 0 centred, +1 right.
        ///
        /// READ FROM THE TWO ONE-SIDED CONTROLS, not from the signed axis, and that is not
        /// fussiness. The axis is one number whose sign means left or right by a convention this
        /// code would have to assume -- and getting it backwards is a mod that indicates the
        /// wrong way at every junction, which is worse than one that does nothing. The LeftOnly
        /// and RightOnly controls each report their own side, so there is no convention to be
        /// wrong about.
        ///
        /// The signed axis is still read as a fallback for setups where the one-sided controls
        /// stay at zero, and THAT is the reading BlinkerInvert exists for.
        ///
        /// Static and taking plain numbers, so the convention this whole comment is about can be
        /// pinned down by a test instead of by driving to a junction and looking.
        /// </summary>
        public static int Wheel(float left, float right, float axis, float deadzone, bool invert)
        {
            if (left > deadzone || right > deadzone) return right > left ? RightSide : LeftSide;

            if (axis <= deadzone && axis >= -deadzone) return Off;

            var side = axis > 0f ? RightSide : LeftSide;
            return invert ? -side : side;
        }
    }
}
