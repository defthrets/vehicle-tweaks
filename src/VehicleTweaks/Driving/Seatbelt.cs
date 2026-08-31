using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;
using VehicleTweaks.Input;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The seatbelt, which you are wearing unless you took it off.
    ///
    /// BELTED BY DEFAULT, AND A KEY TO UNDO IT. A belt you have to fasten needs somewhere to
    /// tell you whether you did: the whole benefit is invisible right up until the one crash
    /// where you do not go through the windscreen, so "am I belted?" is a question that wants a
    /// permanent HUD, and this mod deliberately has none while you are driving.
    ///
    /// Turned round, the question stops existing. You are always wearing it, in the same way the
    /// ignition is always yours, and the only thing that needs feedback is the deliberate act of
    /// taking it off -- which gets a sound, because it is a thing you just did rather than a
    /// thing that is quietly true.
    ///
    /// The flag is CanFlyThruWindscreen, and belted means FALSE. Worth saying out loud, because
    /// a boolean whose name is the opposite of the thing being set is exactly the sort of thing
    /// that gets inverted six months later by somebody tidying up.
    /// </summary>
    internal sealed class Seatbelt
    {
        /// <summary>CPED_CONFIG_FLAG_CanFlyThruWindscreen.</summary>
        private const int CanFlyThroughWindscreen = 32;

        private readonly Settings _cfg;
        private readonly Chord _chord;

        private bool _keyDown;

        /// <summary>The car he is in, so getting into a different one buckles again.</summary>
        private int _car;

        /// <summary>When he got in, for the pause before it goes on.</summary>
        private int _satAt;

        /// <summary>Whether the belt is on, and whether he has taken it off in THIS car.</summary>
        private bool _on;
        private bool _refused;

        public Seatbelt(Settings cfg)
        {
            _cfg = cfg;
            _chord = new Chord(cfg.PadModifier, cfg.PadSeatbelt, "seatbelt");

            Log.Info("Seatbelt off with " + cfg.SeatbeltKey + ", or on a pad " + _chord.Describe() + ".");
        }

        public void Update(Ped me)
        {
            if (!_cfg.Seatbelt) return;

            try
            {
                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists())
                {
                    Out(me);
                    return;
                }

                if (car.Handle != _car)
                {
                    _car = car.Handle;
                    _satAt = Game.GameTime;
                    _refused = false;
                    _on = false;
                }

                if (Pressed()) Flip(me);

                // The pause before it goes on. Not instant, because instant is something that
                // happens TO you -- and it is long enough that hopping in and straight back out
                // never involves a belt at all.
                if (_on || _refused) return;
                if (Game.GameTime - _satAt < (int)(_cfg.SeatbeltSeconds * 1000f)) return;

                // SET BEFORE IT IS ACTED ON, because this flag is the only thing that stops the
                // block above running again next frame. It was missing, and the belt was
                // therefore refastened sixty times a second for as long as anybody sat in a car
                // -- fifty-nine thousand log lines, which rotated the log past its own size
                // limit and took the history of every other feature with it.
                //
                // The louder failure was the quieter one: Out() only unfastens a belt it thinks
                // is on, so it never unfastened anything, and the ped kept the flag after
                // getting out. And Flip() reads it, so the unbuckle key was toggling false to
                // true -- fastening a belt that was already fastened rather than taking it off.
                _on = true;

                Wear(me, true);
                Log.Debug("Seatbelt on.");
            }
            catch (Exception ex)
            {
                Log.Once("seatbelt", "The seatbelt fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// On foot: the belt comes off and the flag goes back to the game's own default.
        ///
        /// LEFT AS WE FOUND IT rather than left as we set it. The flag is on the PED, not on the
        /// car, so a ped walking around with it cleared is this script still holding something
        /// after it has stopped having an opinion -- the same mistake the light override made,
        /// in a place where nothing would ever have shown it.
        /// </summary>
        private void Out(Ped me)
        {
            if (_car == 0) return;

            _car = 0;
            _refused = false;

            if (!_on) return;

            _on = false;
            Wear(me, false);
        }

        private void Flip(Ped me)
        {
            _on = !_on;

            // Taking it off is a decision about THIS car, and it sticks until he gets out of it.
            // Without that, the belt would simply go back on a second later and the key would
            // look broken.
            _refused = !_on;

            Wear(me, _on);
            Chirp();

            Log.Debug("Seatbelt " + (_on ? "on." : "off, and staying off in this car."));
        }

        private static void Wear(Ped me, bool on)
        {
            try
            {
                if (me == null) return;

                // Belted is FALSE: the flag is permission to fly through the windscreen.
                Function.Call(Hash.SET_PED_CONFIG_FLAG, me.Handle, CanFlyThroughWindscreen, !on);
            }
            catch (Exception ex)
            {
                Log.Once("belt-set", "Could not work the seatbelt: " + ex.Message);
            }
        }

        /// <summary>
        /// The click.
        ///
        /// The one sound this mod makes, and only ever in answer to a key the player pressed.
        /// Without it, unbuckling is a keystroke with no observable result until a crash that
        /// may be twenty minutes away.
        /// </summary>
        private static void Chirp()
        {
            try
            {
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "SELECT",
                              "HUD_FRONTEND_DEFAULT_SOUNDSET", true);
            }
            catch { /* the log line is the fallback */ }
        }

        private bool Pressed()
        {
            var key = false;

            try
            {
                var down = Game.IsKeyPressed(_cfg.SeatbeltKey);
                key = down && !_keyDown;
                _keyDown = down;
            }
            catch
            {
                _keyDown = false;
            }

            // Not short-circuited: Fired() is what advances the chord's own memory.
            var chord = _chord.Fired();

            return key || chord;
        }
    }
}
