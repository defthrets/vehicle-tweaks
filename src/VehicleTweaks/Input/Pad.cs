using System;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;

// Both namespaces have a Control and only one of them is a game control.
using Control = GTA.Control;

namespace VehicleTweaks.Input
{
    /// <summary>
    /// The controller, in the one place that knows about it.
    ///
    /// HERE RATHER THAN IN THE PANEL, because the panel stopped being the only thing with a pad
    /// binding the moment the hazards got one. Two copies of "resolve this control name, check
    /// the player is on a pad, watch for this chord" is two places for the same subtlety to be
    /// got right, and one of them to later be got wrong.
    ///
    /// Not in Core, deliberately: Core references no SHVDN type, which is the only reason the
    /// ini reader and writer can be compiled into a console exe and tested.
    /// </summary>
    internal static class Pad
    {
        /// <summary>
        /// A GTA control by name, or null for "not bound".
        ///
        /// SAID OUT LOUD WHEN IT FAILS. These settings are names out of somebody else's
        /// enumeration, typed into a text file, and a typo is a chord that never fires -- which
        /// from the sofa is indistinguishable from a pad that is not being read at all. The log
        /// is the only place that difference can be seen.
        /// </summary>
        public static Control? Parse(string name, string what)
        {
            if (string.IsNullOrEmpty(name)) return null;

            name = name.Trim();

            if (name.Equals("Off", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (Enum.TryParse(name, true, out Control parsed) && Enum.IsDefined(typeof(Control), parsed))
            {
                return parsed;
            }

            Log.Warn("'" + name + "' is not the name of a GTA control, so " + what + " is off. " +
                     "Names come from SHVDN's GTA.Control -- PhoneUp, PhoneDown, MultiplayerInfo.");
            return null;
        }

        /// <summary>
        /// Whether the player is driving this with a pad rather than a keyboard.
        ///
        /// Group 2 is the frontend control group, which is the one that answers this question
        /// the way a menu means it. Anything that goes wrong is treated as a keyboard, because
        /// the keyboard is the half that is known to work.
        /// </summary>
        public static bool InUse()
        {
            try { return !Function.Call<bool>(Hash.IS_USING_KEYBOARD_AND_MOUSE, 2); }
            catch { return false; }
        }

        /// <summary>
        /// IsControlPressed, not IsEnabledControlPressed.
        ///
        /// Same reason the ignition reads the exit control that way: these controls are disabled
        /// every frame so the game cannot act on them, and read anyway so we can.
        /// </summary>
        public static bool Held(Control control)
        {
            try { return Game.IsControlPressed(control); }
            catch { return false; }
        }
    }

    /// <summary>
    /// A button held and a button pressed.
    ///
    /// A CHORD, because there is no spare button on a pad. Every face button, shoulder and stick
    /// is a gameplay action and the D-pad changes the radio station -- so a single button either
    /// collides with something or has to be one nobody ever presses. Holding one while pressing
    /// another is not something a thumb does by accident, which is the argument the keyboard
    /// side makes for a modifier, except that on a keyboard there were free keys and here there
    /// are none.
    ///
    /// ONLY WHILE THE PLAYER IS ACTUALLY ON A PAD. Every one of these controls has a keyboard
    /// binding too, and without that check each chord would quietly become a second keyboard
    /// shortcut that nothing documents and nobody asked for.
    /// </summary>
    internal sealed class Chord
    {
        private readonly Control? _modifier;
        private readonly Control? _button;
        private readonly string _what;

        private bool _down;

        public Chord(string modifier, string button, string what)
        {
            _modifier = Pad.Parse(modifier, what);
            _button = Pad.Parse(button, what);
            _what = what;
        }

        /// <summary>Null when nothing is bound, so callers can say so rather than guess.</summary>
        public bool Bound => _button != null;

        public string Describe()
        {
            if (_button == null) return "off";
            return (_modifier == null ? "press " : "hold " + _modifier + " and press ") + _button;
        }

        /// <summary>Both controls, so a Deafen pass can hold them off while they are being read.</summary>
        public void Deafen()
        {
            try
            {
                if (_button != null) Game.DisableControlThisFrame(_button.Value);
                if (_modifier != null) Game.DisableControlThisFrame(_modifier.Value);
            }
            catch { /* worst case the game hears what we did */ }
        }

        /// <summary>
        /// True on the frame the chord completes. Call once a frame, always.
        /// </summary>
        public bool Fired()
        {
            var fired = false;

            try
            {
                if (_button == null || !Pad.InUse())
                {
                    _down = false;
                    return false;
                }

                var held = _modifier == null || Pad.Held(_modifier.Value);
                var down = held && Pad.Held(_button.Value);

                fired = down && !_down;
                _down = down;
            }
            catch
            {
                _down = false;
                return false;
            }

            if (fired) Log.Debug("Pad: " + _what + " chord.");
            return fired;
        }
    }
}
