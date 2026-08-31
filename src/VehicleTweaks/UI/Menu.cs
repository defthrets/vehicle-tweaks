using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using GTA;
using GTA.Native;

// Both namespaces have a Control and only one of them is a game control.
using Control = GTA.Control;
using VehicleTweaks.Core;
using VehicleTweaks.Input;

namespace VehicleTweaks.UI
{
    /// <summary>
    /// The settings panel. F8.
    ///
    /// BUILT OUT OF RECTANGLES, because the alternative is a dependency. NativeUI and LemonUI
    /// both do this better and both are another dll a player has to find, put in the right
    /// folder and keep in step with the game -- in a scripts\ folder that resolves assemblies
    /// once for everybody in it. A menu is a list of strings and a highlight.
    ///
    /// WHY IT EXISTS AT ALL, given that both features are silent on purpose. Silent means they
    /// do not interrupt you while they work; it does not mean their settings should only be
    /// reachable by alt-tabbing to a text file. Every one of these is a feel setting -- how
    /// long a hold is, how far the wheel has to go over -- and a feel setting is tuned by
    /// changing it and immediately trying it, which is not a thing you can do from Notepad.
    ///
    /// Changes apply to the live settings the instant they are made, so the next corner uses
    /// the number you just set, and are written to the ini when the panel closes -- in place,
    /// keeping every comment, only the lines that actually changed.
    /// </summary>
    internal sealed class Menu
    {
        private readonly Settings _cfg;

        private readonly List<Page> _pages = new List<Page>();
        private readonly HashSet<Item> _changed = new HashSet<Item>();

        private int _page;
        private int _row;
        private int _scroll;

        private bool _open;

        /// <summary>Waiting for the player to press the key they want the panel on.</summary>
        private bool _capturing;

        /// <summary>
        /// When the wait started, so the keypress that STARTED it is not what gets bound.
        ///
        /// Enter opens the capture, and SHVDN delivers KeyDown off the tick rather than in it,
        /// so the Enter that armed this arrives immediately afterwards and binds the panel to
        /// Enter. A short deaf window is the whole fix, and it doubles as a guard against a
        /// held key auto-repeating its way in.
        /// </summary>
        private int _captureAt;

        /// <summary>True whenever the panel is taking input, so the rest of the mod can stand off.</summary>
        public bool IsOpen => _open;

        // Where it sits and how big, as fractions of the screen.
        private const float PanelX = 0.030f;
        private const float PanelW = 0.245f;
        private const float PanelTop = 0.180f;
        private const float TitleH = 0.052f;
        private const float RowH = 0.0295f;
        private const float FootH = 0.044f;

        /// <summary>
        /// How many rows fit before it scrolls.
        ///
        /// Larger than the longest page, deliberately, so nothing scrolls today. The scrolling
        /// below is not dead weight for that: it is what stops a page silently losing its last
        /// row the day somebody adds an eighth setting to it. A menu that quietly truncates is
        /// worse than one that scrolls.
        /// </summary>
        private const int Rows = 11;

        private const int Plain = 4;   // Chalet Comprime Cologne

        private static readonly Color Ink = Color.FromArgb(232, 228, 228, 231);
        private static readonly Color Dim = Color.FromArgb(165, 150, 150, 156);
        private static readonly Color Faint = Color.FromArgb(120, 150, 150, 156);

        /// <summary>Indicator amber, which is the one colour this mod has any claim on.</summary>
        private static readonly Color Amber = Color.FromArgb(255, 245, 196, 60);

        private static readonly Color Panel = Color.FromArgb(234, 15, 15, 18);
        private static readonly Color Head = Color.FromArgb(242, 26, 26, 31);

        /// <summary>The chord that opens this on a pad.</summary>
        private readonly Chord _openChord;

        public Menu(Settings cfg)
        {
            _cfg = cfg;
            _openChord = new Chord(cfg.PadModifier, cfg.PadOpen, "panel");

            Log.Info("Panel on " + cfg.BindingText() + ", or on a pad " + _openChord.Describe() + ".");

            Build();
        }

        // ==================================================================
        // The items
        // ==================================================================

        private sealed class Page
        {
            public string Title;
            public readonly List<Item> Items = new List<Item>();
        }

        private sealed class Item
        {
            public string Label;
            public string Hint;

            /// <summary>The value as it should read on screen.</summary>
            public Func<string> Show;

            /// <summary>Left or right, as -1 or +1.</summary>
            public Action<int> Nudge;

            /// <summary>Enter.</summary>
            public Action Press;

            /// <summary>Where it belongs in the ini, and what to write there.</summary>
            public string Section;
            public string Key;
            public Func<string> Written;
        }

        private Page Add(string title)
        {
            var p = new Page { Title = title };
            _pages.Add(p);
            return p;
        }

        private static Item Toggle(string label, Func<bool> get, Action<bool> set,
                                   string section, string key, string hint)
        {
            var item = new Item
            {
                Label = label,
                Hint = hint,
                Section = section,
                Key = key,

                // WORDS, NOT A SYMBOL. Everything decorative in this panel is a text symbol,
                // but a value is not decoration: if the game's font has no glyph for whatever
                // shape was chosen, a decorative one is missing and a load-bearing one is a
                // setting whose state cannot be read. ON and OFF cannot fail to render.
                Show = () => get() ? "ON" : "OFF",
                Written = () => get() ? "true" : "false",
            };

            item.Nudge = d => set(!get());
            item.Press = () => set(!get());
            return item;
        }

        private static Item Number(string label, Func<float> get, Action<float> set,
                                   float step, float min, float max, string format,
                                   string unit, string section, string key, string hint)
        {
            var item = new Item
            {
                Label = label,
                Hint = hint,
                Section = section,
                Key = key,

                // The unit is shown and NOT written. On screen "2.5 m/s" is the difference
                // between a number and a quantity; in the ini it would be a parse failure that
                // silently reverts the setting the player just made.
                Show = () => get().ToString(format, CultureInfo.InvariantCulture) +
                             (string.IsNullOrEmpty(unit) ? "" : " " + unit),
                Written = () => get().ToString(format, CultureInfo.InvariantCulture),
            };

            item.Nudge = d =>
            {
                var v = get() + step * d;

                // Rounded to the step, or a float that started at 0.1 and was nudged twenty
                // times reads as 0.30000004 and writes that into the ini.
                v = (float)Math.Round(v / step) * step;

                if (v < min) v = min;
                if (v > max) v = max;

                set(v);
            };

            return item;
        }

        private static Item Choice<T>(string label, Func<T> get, Action<T> set,
                                      string section, string key, string hint)
        {
            var values = Enum.GetValues(typeof(T));

            var item = new Item
            {
                Label = label,
                Hint = hint,
                Section = section,
                Key = key,
                Show = () => get().ToString().ToUpperInvariant(),
                Written = () => get().ToString(),
            };

            item.Nudge = d =>
            {
                var at = Array.IndexOf(values, get());
                at = ((at + d) % values.Length + values.Length) % values.Length;
                set((T)values.GetValue(at));
            };

            return item;
        }

        /// <summary>
        /// The panel's own key, rebound by pressing it.
        ///
        /// A CAPTURE RATHER THAN A LIST. The alternative is nudging left and right through the
        /// Keys enumeration, which is two hundred and some values including six kinds of
        /// modifier and a lot of things no keyboard has -- so finding K means holding right for
        /// a minute past OemBackslash and LaunchApplication2.
        /// </summary>
        private Item Bind(string label, Func<Keys> get, Action<Keys> set,
                          string section, string key, string hint)
        {
            Item item = null;

            item = new Item
            {
                Label = label,
                Hint = hint,
                Section = section,
                Key = key,

                // WHICH row is being rebound, not just whether one is. Two bindable rows now
                // exist, and a Show that only asked "_capturing" would put PRESS A KEY on both
                // of them at once.
                Show = () => _capturing && _captureItem == item
                                 ? "PRESS A KEY"
                                 : get().ToString().ToUpperInvariant(),
                Written = () => get().ToString(),
            };

            item.Press = () =>
            {
                _capturing = true;
                _captureAt = Game.GameTime;
                _captureSet = set;
                _captureItem = item;
            };

            // Deliberately no Nudge. Left and right on this row would step through the whole
            // Keys enumeration, two hundred values of it, most of which no keyboard has.
            return item;
        }

        /// <summary>Where a captured key goes, and the row to mark changed when it lands.</summary>
        private Action<Keys> _captureSet;
        private Item _captureItem;

        /// <summary>
        /// A key pressed while the panel is waiting for one. Called from the script's KeyDown.
        ///
        /// MODIFIERS ARE NOT KEYS HERE. Shift, Ctrl and Alt are the OTHER half of this binding
        /// and they are set on the row above; letting one of them land as the key itself gives
        /// SHIFT+SHIFT, which cannot be pressed and leaves the panel unreachable.
        /// </summary>
        public void OnKey(Keys key)
        {
            if (!_open || !_capturing) return;

            // The press that armed the capture, or a key still held down from before it.
            if (Game.GameTime - _captureAt < 250) return;

            if (key == Keys.Escape)
            {
                _capturing = false;
                return;
            }

            switch (key)
            {
                case Keys.ShiftKey: case Keys.LShiftKey: case Keys.RShiftKey:
                case Keys.ControlKey: case Keys.LControlKey: case Keys.RControlKey:
                case Keys.Menu: case Keys.LMenu: case Keys.RMenu:
                    return;
            }

            if (_captureSet == null || _captureItem == null)
            {
                _capturing = false;
                return;
            }

            _captureSet(key);
            _capturing = false;
            _changed.Add(_captureItem);

            Log.Info(_captureItem.Label + " rebound to " + key + ".");
        }

        private void Build()
        {
            var ign = Add("IGNITION");

            ign.Items.Add(Toggle("Manual ignition", () => _cfg.ManualIgnition,
                                 v => _cfg.ManualIgnition = v, "Ignition", "ManualIgnition",
                                 "Hold the exit key to stop the engine. Tap it to get out."));

            ign.Items.Add(Number("A hold, rather than a tap", () => _cfg.ExitHoldSeconds,
                                 v => _cfg.ExitHoldSeconds = v, 0.05f, 0.1f, 3f, "0.00", "s",
                                 "Ignition", "ExitHoldSeconds",
                                 "Held longer than this stops the engine. Shorter gets you out."));

            ign.Items.Add(Number("Give the key back above", () => _cfg.ManualIgnitionMaxSpeed,
                                 v => _cfg.ManualIgnitionMaxSpeed = v, 0.5f, 0f, 60f, "0.0", "m/s",
                                 "Ignition", "ManualIgnitionMaxSpeed",
                                 "Faster than this and the game's own hold-to-bail is back."));

            ign.Items.Add(Toggle("Aircraft too", () => _cfg.ManualIgnitionAircraft,
                                 v => _cfg.ManualIgnitionAircraft = v,
                                 "Ignition", "ManualIgnitionAircraft",
                                 "Off. The gesture that parks a car kills you in a helicopter."));

            ign.Items.Add(Toggle("Starter cranks", () => _cfg.StarterCranks,
                                 v => _cfg.StarterCranks = v, "Ignition", "StarterCranks",
                                 "The engine turns over before it catches, instead of just being on."));

            ign.Items.Add(Toggle("Radio keeps playing", () => _cfg.RadioKeepsPlaying,
                                 v => _cfg.RadioKeepsPlaying = v, "Ignition", "RadioKeepsPlaying",
                                 "A car left running keeps its station, audible from outside."));

            ign.Items.Add(Toggle("Lights stay as left", () => _cfg.LightsStayAsLeft,
                                 v => _cfg.LightsStayAsLeft = v, "Ignition", "LightsStayAsLeft",
                                 "Headlights carry through the same way the engine and radio do."));

            ign.Items.Add(Toggle("Leave the door open", () => _cfg.LeaveDoorOpen,
                                 v => _cfg.LeaveDoorOpen = v, "Ignition", "LeaveDoorOpen",
                                 "Getting back in shuts it. Traffic may shut it for you."));

            var bli = Add("BLINKERS");

            bli.Items.Add(Toggle("Steering indicators", () => _cfg.Blinkers,
                                 v => _cfg.Blinkers = v, "Blinkers", "Blinkers",
                                 "Hold the wheel over and that side comes on."));

            bli.Items.Add(Number("Wheel held before it lights", () => _cfg.BlinkerArmSeconds,
                                 v => _cfg.BlinkerArmSeconds = v, 0.1f, 0.1f, 5f, "0.0", "s",
                                 "Blinkers", "BlinkerArmSeconds",
                                 "Short enough to be deliberate, long enough not to be a wobble."));

            bli.Items.Add(Number("Straight cancels after", () => _cfg.BlinkerCancelSeconds,
                                 v => _cfg.BlinkerCancelSeconds = v, 0.1f, 0.1f, 10f, "0.0", "s",
                                 "Blinkers", "BlinkerCancelSeconds",
                                 "Only while moving, which is what lets you signal at a light."));

            bli.Items.Add(Number("Opposite lock cancels after", () => _cfg.BlinkerOppositeSeconds,
                                 v => _cfg.BlinkerOppositeSeconds = v, 0.1f, 0f, 5f, "0.0", "s",
                                 "Blinkers", "BlinkerOppositeSeconds",
                                 "A held turn the other way. A flick to line up should not count."));

            bli.Items.Add(Number("Wheel deadzone", () => _cfg.BlinkerDeadzone,
                                 v => _cfg.BlinkerDeadzone = v, 0.05f, 0.05f, 0.95f, "0.00", null,
                                 "Blinkers", "BlinkerDeadzone",
                                 "How far over the wheel counts as turned at all, 0 to 1."));

            bli.Items.Add(Number("Cancelling needs at least", () => _cfg.BlinkerMinSpeed,
                                 v => _cfg.BlinkerMinSpeed = v, 0.1f, 0f, 20f, "0.0", "m/s",
                                 "Blinkers", "BlinkerMinSpeed",
                                 "Below this a centred wheel means nothing. Zero breaks signalling at lights."));

            bli.Items.Add(Bind("Hazards key", () => _cfg.HazardKey, v => _cfg.HazardKey = v,
                               "Blinkers", "HazardKey",
                               "Both sides at once. On a pad it is the panel's modifier and D-pad down."));

            bli.Items.Add(Toggle("Invert the fallback axis", () => _cfg.BlinkerInvert,
                                 v => _cfg.BlinkerInvert = v, "Blinkers", "BlinkerInvert",
                                 "Only for setups where the one-sided steering controls read nothing."));

            var gen = Add("GENERAL");

            gen.Items.Add(Toggle("Both features on", () => _cfg.Enabled, v => _cfg.Enabled = v,
                                 "General", "Enabled",
                                 "Off leaves the game exactly as it was. This panel still opens."));

            gen.Items.Add(Toggle("Say hello on load", () => _cfg.AnnounceOnLoad,
                                 v => _cfg.AnnounceOnLoad = v, "General", "AnnounceOnLoad",
                                 "Off by default. Nothing here announces itself."));

            // THE LIVE LEVEL IS SET TOO, not just the stored one. Log.Level is what Log actually
            // reads; _cfg.LogLevel is only what gets written to the ini. Setting one without the
            // other gives a menu row that appears to work, changes nothing until a restart, and
            // says nothing about it -- on the one setting a person only ever touches because
            // they are already trying to find out why something is not working.
            gen.Items.Add(Choice("Log detail", () => _cfg.LogLevel,
                                 v => { _cfg.LogLevel = v; Log.Level = v; },
                                 "General", "LogLevel",
                                 "DEBUG is loud, and is what to send with a bug report."));

            gen.Items.Add(Choice("Panel modifier", () => _cfg.MenuModifier,
                                 v => _cfg.MenuModifier = v, "General", "MenuModifier",
                                 "NONE is the bare key. Pick one you do not drive with."));

            gen.Items.Add(Bind("Panel key", () => _cfg.MenuKey, v => _cfg.MenuKey = v,
                               "General", "MenuKey",
                               "ENTER, then press the key you want. ESC cancels."));
        }

        // ==================================================================
        // Input
        // ==================================================================

        /// <summary>
        /// One menu input, however it arrives.
        ///
        /// A KEY AND A PAD CONTROL COLLAPSED INTO ONE STREAM, rather than two things checked in
        /// a row. Two separate edge detectors on the same logical button is a bug waiting for
        /// the frame when both fire: DOWN would move two rows, and a number nudged with LEFT
        /// would jump two steps. Combining them into a single "is it down" and running ONE edge
        /// detector on that cannot do it, whether the player is on a pad, on the keyboard, or
        /// has a hand on each.
        ///
        /// It also gives repeat-on-hold, which the keyboard-only version never had. That was
        /// tolerable when every setting was a toggle; it is not, with a deadzone that steps in
        /// hundredths, and it is much less tolerable on a D-pad.
        /// </summary>
        private sealed class Button
        {
            private const int FirstRepeatMs = 350;
            private const int RepeatMs = 90;

            private readonly Keys _key;
            private readonly Control _pad;
            private readonly bool _hasPad;
            private readonly bool _repeats;

            private bool _down;
            private int _repeatAt;

            /// <summary>Whether this counted as a press on the frame Poll last ran.</summary>
            public bool Fired { get; private set; }

            public Button(Keys key, Control pad, bool repeats)
            {
                _key = key;
                _pad = pad;
                _hasPad = true;
                _repeats = repeats;
            }

            public Button(Keys key, bool repeats)
            {
                _key = key;
                _hasPad = false;
                _repeats = repeats;
            }

            public void Poll()
            {
                var down = Held(_key) || (_hasPad && Pad.Held(_pad));

                if (!down)
                {
                    _down = false;
                    Fired = false;
                    return;
                }

                if (!_down)
                {
                    _down = true;
                    _repeatAt = Game.GameTime + FirstRepeatMs;
                    Fired = true;
                    return;
                }

                if (_repeats && Game.GameTime >= _repeatAt)
                {
                    _repeatAt = Game.GameTime + RepeatMs;
                    Fired = true;
                    return;
                }

                Fired = false;
            }
        }

        // THE PAD CONTROLS ARE THE PHONE'S, not the frontend's. Both look right in the enum and
        // only one of them is reliably readable during gameplay -- the frontend group belongs to
        // the game's own menus. Phone up/down/left/right/select/cancel are what every other
        // in-game menu in this game's modding history is driven by, and they map to the D-pad,
        // A and B on a pad and to the arrows, Enter and Backspace on a keyboard. Which is
        // exactly the scheme this panel already had.
        private readonly Button _up = new Button(Keys.Up, Control.PhoneUp, true);
        private readonly Button _down = new Button(Keys.Down, Control.PhoneDown, true);
        private readonly Button _left = new Button(Keys.Left, Control.PhoneLeft, true);
        private readonly Button _right = new Button(Keys.Right, Control.PhoneRight, true);
        private readonly Button _accept = new Button(Keys.Return, Control.PhoneSelect, false);
        private readonly Button _back = new Button(Keys.Back, Control.PhoneCancel, false);

        /// <summary>Keyboard only, and it does not need to be anything else. See Navigate.</summary>
        private readonly Button _tab = new Button(Keys.Tab, false);

        private bool _openKey;

        /// <summary>Set on the frame the panel opens, so that frame's input is primed, not obeyed.</summary>
        private bool _justOpened;

        /// <summary>
        /// Polled ONCE a frame, all of them, before anything looks at the answers.
        ///
        /// Not where they are used, which is the trap this codebase has already been caught by
        /// once: Navigate returns early on a page change, and a button whose state is only read
        /// further down that method would have gone a frame without being updated. It then
        /// reports a fresh press the next time anything asks, for a button that was held the
        /// whole time.
        /// </summary>
        private void Poll()
        {
            _up.Poll();
            _down.Poll();
            _left.Poll();
            _right.Poll();
            _accept.Poll();
            _back.Poll();
            _tab.Poll();
        }

        public void Update()
        {
            // BOTH HALVES READ, and Edge FIRST, because Edge is what updates the remembered
            // key state -- short-circuiting past it leaves the key recorded as up while it is
            // held, and it registers a fresh press the next time anything looks.
            var keyEdge = Edge(_cfg.MenuKey, ref _openKey);
            var padEdge = _openChord.Fired();

            var toggled = ((keyEdge && Modifier()) || padEdge) && !_capturing;

            if (toggled)
            {
                if (_open)
                {
                    Close();
                }
                else
                {
                    _open = true;
                    _justOpened = true;
                }
            }

            // EVERY FRAME THE PANEL IS UP, and before anything reads an answer. See Poll.
            if (_open) Poll();

            if (!_open)
            {
                // DEAFENED ON THE WAY OUT AS WELL AS THE WAY IN.
                //
                // The frame the panel closes is still a frame in which the opening key was
                // pressed, and returning here before Deafen -- which is what the obvious
                // version of this method does -- lets the game act on it. F8 is bound to
                // nothing in vanilla so today that costs nothing; the row on the GENERAL page
                // that rebinds this is exactly what stops that staying true, and the failure it
                // would produce is a close that also does whatever the new key does.
                if (toggled) Deafen();
                return;
            }

            // The game keeps its own input while a panel is up otherwise -- arrow keys steer,
            // Enter answers the phone, and the player shoots whatever is in front of them.
            Deafen();

            // Nothing navigates while it is waiting for a key: the arrows and Enter are things
            // the player might want to bind, not commands.
            if (_capturing)
            {
                // EXCEPT THE WAY OUT. Escape cancels a capture, and Escape arrives through
                // KeyDown -- which a pad does not have. Without this, a pad user who opened the
                // rebind row is stuck on a panel that will not move and is waiting for a key
                // they may not have a keyboard to press. B cancels it.
                if (_back.Fired)
                {
                    _capturing = false;
                    Log.Debug("Panel: rebind cancelled from the pad.");
                }

                Render();
                return;
            }

            // THE OPENING FRAME IS POLLED BUT NOT ACTED ON.
            //
            // The pad chord presses D-pad up, and D-pad up is also this panel's UP. Without
            // this, opening the panel scrolls it by one row on the same frame -- and because
            // the buttons are not polled while it is shut, the button is seen going from
            // "not held" to "held" at exactly the moment it starts to matter.
            //
            // Polling and discarding is what makes it a non-event: the button is recorded as
            // already down, so the next frame is not an edge, and the player has to let go and
            // press again before anything moves.
            if (_justOpened)
            {
                _justOpened = false;
                Render();
                return;
            }

            Navigate();
            Render();
        }

        private bool Modifier()
        {
            try
            {
                switch (_cfg.MenuModifier)
                {
                    case MenuModifier.None: return true;
                    case MenuModifier.Shift: return Game.IsKeyPressed(Keys.ShiftKey);
                    case MenuModifier.Control: return Game.IsKeyPressed(Keys.ControlKey);
                    case MenuModifier.Alt: return Game.IsKeyPressed(Keys.Menu);
                }
            }
            catch
            {
                // Treated as not held, so the panel simply does not open.
            }

            return false;
        }

        /// <summary>
        /// Holds off every control that would otherwise hear the panel's own keys.
        ///
        /// THE VEHICLE HALF MATTERS MORE HERE THAN IT DID IN FUMES. This is a menu about
        /// driving; it will be opened in a moving car more often than not, and a menu that
        /// steers the car while you read it is a menu you cannot use where you need it.
        /// VehicleExit is in the list because the ignition is not running while this is open
        /// and nothing else would be holding it.
        /// </summary>
        private void Deafen()
        {
            try
            {
                Game.DisableControlThisFrame(Control.Attack);
                Game.DisableControlThisFrame(Control.Attack2);
                Game.DisableControlThisFrame(Control.Aim);
                Game.DisableControlThisFrame(Control.MoveLeftRight);
                Game.DisableControlThisFrame(Control.MoveUpDown);
                Game.DisableControlThisFrame(Control.Jump);
                Game.DisableControlThisFrame(Control.Enter);
                Game.DisableControlThisFrame(Control.Context);
                Game.DisableControlThisFrame(Control.Phone);
                Game.DisableControlThisFrame(Control.SelectWeapon);
                Game.DisableControlThisFrame(Control.CharacterWheel);

                // The camera. Kept in the list after the panel moved off V, because the reason
                // it belongs here was never only that V opened this: changing view from under a
                // menu you are reading is its own small nuisance, and V is one of the keys
                // somebody might rebind the panel to.
                Game.DisableControlThisFrame(Control.NextCamera);

                Game.DisableControlThisFrame(Control.VehicleExit);
                Game.DisableControlThisFrame(Control.VehicleMoveLeftRight);
                Game.DisableControlThisFrame(Control.VehicleAccelerate);
                Game.DisableControlThisFrame(Control.VehicleBrake);
                Game.DisableControlThisFrame(Control.VehicleHandbrake);
                Game.DisableControlThisFrame(Control.VehicleRadioWheel);

                // THE PANEL'S OWN CONTROLS. These are the pad's way in and out of every row,
                // and they are the phone's buttons -- so without this, navigating the panel on
                // a pad rings the phone up behind it and the D-pad changes the radio station
                // while you are reading a row about the radio.
                Game.DisableControlThisFrame(Control.Phone);
                Game.DisableControlThisFrame(Control.PhoneUp);
                Game.DisableControlThisFrame(Control.PhoneDown);
                Game.DisableControlThisFrame(Control.PhoneLeft);
                Game.DisableControlThisFrame(Control.PhoneRight);
                Game.DisableControlThisFrame(Control.PhoneSelect);
                Game.DisableControlThisFrame(Control.PhoneCancel);

                // And whatever the chord is made of, so opening the panel does not also do
                // whatever those two buttons do in the world.
                _openChord.Deafen();
            }
            catch
            {
                // Worst case the game hears the same key we did.
            }
        }

        private void Navigate()
        {
            if (_tab.Fired)
            {
                TurnPage(1, true);
                return;
            }

            if (_up.Fired) _row--;
            if (_down.Fired) _row++;

            // OFF THE END OF A PAGE GOES TO THE NEXT PAGE, not round to the top of this one.
            //
            // This is what makes a D-pad enough on its own. TAB changes page and TAB is a
            // keyboard key; there is no spare pad button to give it that is not already a
            // gameplay action, and inventing a second chord for it would be another thing
            // guessed rather than tried. Making the three pages one continuous list removes
            // the need for it: seventeen rows in a row, and everything is reachable with UP
            // and DOWN alone.
            //
            // TAB stays, because jumping straight to a page is still faster than scrolling to
            // it, and the keyboard has the key to spare.
            if (_row < 0) TurnPage(-1, false);
            else if (_row >= _pages[_page].Items.Count) TurnPage(1, true);

            var page = _pages[_page];

            // Keep the highlight on screen with a margin, so the next row is visible before
            // you get to it rather than appearing as you land on it.
            if (_row < _scroll) _scroll = _row;
            if (_row >= _scroll + Rows) _scroll = _row - Rows + 1;

            var item = page.Items[_row];

            if (_left.Fired && item.Nudge != null) Touch(item, -1);
            if (_right.Fired && item.Nudge != null) Touch(item, 1);

            if (_accept.Fired && item.Press != null)
            {
                item.Press();
                if (item.Section != null) _changed.Add(item);
            }

            if (_back.Fired) Close();
        }

        /// <summary>Moves to another page, landing on its first or last row.</summary>
        private void TurnPage(int direction, bool top)
        {
            _page = ((_page + direction) % _pages.Count + _pages.Count) % _pages.Count;

            var count = _pages[_page].Items.Count;

            _row = top ? 0 : count - 1;
            _scroll = _row < Rows ? 0 : _row - Rows + 1;
        }

        private void Touch(Item item, int direction)
        {
            item.Nudge(direction);
            if (item.Section != null) _changed.Add(item);
        }

        /// <summary>
        /// Writes the changed settings back, in place, and shuts.
        ///
        /// Only what actually moved. IniFile.SetValue rewrites one line and leaves every
        /// comment where it was, so a panel that saved everything would still be correct -- it
        /// would just churn a hundred and forty lines to record two.
        ///
        /// THIS ONE SPEAKS ON SCREEN, and it is allowed to. The rule everywhere else is that
        /// the mod does not comment on things the player did not ask about; a save is the
        /// player asking, and a save that failed silently is a setting that springs back on the
        /// next reload with nothing to explain why.
        /// </summary>
        public void Dismiss()
        {
            _capturing = false;

            // Called on a reload or a shutdown. SHVDN reloads scripts on a keypress, and doing
            // that with the panel open used to be the one way to lose a change you had just
            // made: applied to the live settings, never written, and gone the moment the
            // assembly was replaced.
            if (_open) Close();
        }

        private void Close()
        {
            _open = false;

            if (_changed.Count == 0) return;

            var written = 0;

            foreach (var item in _changed)
            {
                try
                {
                    if (IniFile.SetValue(Paths.Ini, item.Section, item.Key, item.Written())) written++;
                    else Log.Warn("Could not write [" + item.Section + "] " + item.Key + " to VehicleTweaks.ini.");
                }
                catch (Exception ex)
                {
                    Log.Once("menu-save", "Could not write the settings: " + ex.Message);
                }
            }

            Log.Info("Panel: " + written + " of " + _changed.Count +
                     " setting(s) written to VehicleTweaks.ini.");

            try
            {
                GTA.UI.Notification.PostTicker(written == _changed.Count
                    ? "~g~" + written + " setting(s) saved~s~ to VehicleTweaks.ini."
                    : "~y~Only " + written + " of " + _changed.Count +
                      " settings saved~s~ - see VehicleTweaks.log.", false, false);
            }
            catch { /* the log already has it */ }

            _changed.Clear();
        }

        // ==================================================================
        // Drawing
        // ==================================================================

        private void Render()
        {
            var page = _pages[_page];
            var shown = Math.Min(Rows, page.Items.Count);

            var bodyH = shown * RowH;
            var totalH = TitleH + bodyH + FootH;

            Draw.Bar(PanelX, PanelTop, PanelW, totalH, Panel);
            Draw.Bar(PanelX, PanelTop, PanelW, TitleH, Head);
            Draw.Bar(PanelX, PanelTop + TitleH - 0.0022f, PanelW, 0.0022f, Amber);

            // MEASURED, NOT GUESSED. "Vehicle Tweaks" is a long title for a narrow panel and
            // the width it takes depends on the aspect ratio it is read at.
            const float titleWanted = 0.46f;
            var titleScale = Draw.FitScale("VEHICLE TWEAKS", titleWanted, PanelW - 0.026f, Plain);

            Draw.Text("VEHICLE TWEAKS", PanelX + 0.012f, PanelTop + 0.005f, titleScale, Amber, Plain);

            // THE PAGES, NAMED rather than numbered. "IGNITION 1/3" reads as a value belonging
            // to the row underneath it; all three names with the current one lit says the same
            // thing and needs no explaining.
            Tabs();

            for (var i = 0; i < shown; i++)
            {
                var index = _scroll + i;
                if (index >= page.Items.Count) break;

                var item = page.Items[index];
                var y = PanelTop + TitleH + i * RowH;
                var selected = index == _row;

                if (selected)
                {
                    Draw.Bar(PanelX, y, PanelW, RowH, Color.FromArgb(38, 245, 196, 60));
                    Draw.Bar(PanelX, y, 0.0022f, RowH, Amber);

                    // AN ASCII CARET, because the pretty one does not exist.
                    //
                    // This was U+25B6 BLACK RIGHT-POINTING TRIANGLE, chosen on the house rule
                    // of text symbols over emoji. GTA's Chalet Comprime has no glyph for it and
                    // drew the missing-character box instead -- a small hollow rectangle, which
                    // on the selected row of a settings panel reads as a checkbox. It was
                    // decoration and it survived being wrong, which is exactly why it was made
                    // decoration; but a box that looks like a control is worse than no caret,
                    // and ">" is in every font there has ever been.
                    Draw.Text(">", PanelX + 0.0055f, y + 0.0052f, 0.26f, Amber, Plain);
                }

                Draw.Text(item.Label, PanelX + 0.017f, y + 0.0044f, 0.295f,
                          selected ? Ink : Color.FromArgb(200, 205, 205, 208), Plain);

                Draw.Text(item.Show(), PanelX + PanelW - 0.010f, y + 0.0044f, 0.295f,
                          selected ? Amber : Dim, Plain, false, true);
            }

            // The scroll bar, only when there is something to scroll. Nothing scrolls today;
            // see the note on Rows.
            if (page.Items.Count > Rows)
            {
                var track = bodyH;
                var thumb = track * Rows / page.Items.Count;
                var at = track * _scroll / page.Items.Count;

                Draw.Bar(PanelX + PanelW - 0.0018f, PanelTop + TitleH, 0.0018f, track,
                         Color.FromArgb(60, 255, 255, 255));
                Draw.Bar(PanelX + PanelW - 0.0018f, PanelTop + TitleH + at, 0.0018f, thumb, Amber);
            }

            var foot = PanelTop + TitleH + bodyH;

            Draw.Bar(PanelX, foot, PanelW, 0.0016f, Color.FromArgb(70, 255, 255, 255));

            // The hint for the selected row, cut to the panel rather than run out across the
            // game. Falls back to the keys when a row has nothing to say for itself.
            var pad = Pad.InUse();

            var hint = _capturing
                ? (pad
                       // A pad has no key to give. Said plainly on the row rather than left for
                       // the player to work out from a panel that has stopped responding.
                       ? "This needs a keyboard.  B cancels."
                       : "Press the key you want the panel on.  ESC cancels.")
                : page.Items[_row].Hint;

            if (string.IsNullOrEmpty(hint)) hint = pad ? "D-PAD moves and changes" : "ARROWS change    TAB page";

            Draw.Text(Draw.Ellipsis(hint, 0.255f, PanelW - 0.024f, Plain),
                      PanelX + 0.012f, foot + 0.008f, 0.255f, Dim, Plain);

            // THE KEYS IT IS ACTUALLY BEING DRIVEN WITH. A footer that says TAB and BACKSPACE
            // to somebody holding a pad is worse than no footer: they are the two instructions
            // on screen and neither of them can be followed.
            Draw.Text(pad
                          ? "D-PAD move & change   A works a row   B saves & closes"
                          : "TAB page   ARROWS change   " + Binding() + " or BACKSPACE saves",
                      PanelX + 0.012f, foot + 0.026f, 0.235f, Faint, Plain);
        }

        /// <summary>
        /// The combination that opens this, written out.
        ///
        /// READ BACK FROM THE SETTINGS rather than typed into the footer as "F8". Both halves
        /// are configurable and both are rows on the GENERAL page, so the footer would start
        /// lying the moment somebody used them -- and a panel that tells you the wrong way to
        /// close itself is worse than one that says nothing.
        /// </summary>
        private string Binding()
        {
            return _cfg.BindingText();
        }

        /// <summary>
        /// The page names across the head of the panel, current one lit.
        ///
        /// LAID OUT BY MEASUREMENT rather than by fixed columns: the names are different
        /// lengths, so evenly spaced columns would either crowd BLINKERS or strand GENERAL.
        /// Each is measured, and they are spread across whatever room is left.
        /// </summary>
        private void Tabs()
        {
            const float scale = 0.26f;

            var left = PanelX + 0.012f;
            var right = PanelX + PanelW - 0.012f;
            var y = PanelTop + 0.030f;

            var widths = new float[_pages.Count];
            var total = 0f;

            for (var i = 0; i < _pages.Count; i++)
            {
                widths[i] = Draw.Width(_pages[i].Title, scale, Plain);
                total += widths[i];
            }

            var gap = _pages.Count > 1 ? (right - left - total) / (_pages.Count - 1) : 0f;
            if (gap < 0.004f) gap = 0.004f;

            var x = left;

            for (var i = 0; i < _pages.Count; i++)
            {
                var on = i == _page;

                Draw.Text(_pages[i].Title, x, y, scale, on ? Amber : Faint, Plain);

                if (on) Draw.Bar(x, y + 0.0165f, widths[i], 0.0016f, Amber);

                x += widths[i] + gap;
            }
        }

        // ==================================================================
        // Small print
        // ==================================================================

        private static bool Held(Keys key)
        {
            try { return Game.IsKeyPressed(key); }
            catch { return false; }
        }

        private static bool Edge(Keys key, ref bool wasDown)
        {
            var down = Held(key);
            var edge = down && !wasDown;
            wasDown = down;
            return edge;
        }
    }
}
