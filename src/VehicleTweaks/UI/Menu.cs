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

        /// <summary>
        /// What to run when the spawner row is pressed.
        ///
        /// HANDED IN RATHER THAN REACHED FOR. The panel is built before the spawner exists and has
        /// no business knowing what one is; it knows there is a door on the GENERAL page and that
        /// somebody else decided what is behind it.
        /// </summary>
        public Action OpenSpawner;

        /// <summary>
        /// True whenever the panel is taking input, so the rest of the mod can stand off.
        ///
        /// THE INPUT GATE, NOT THE PICTURE. It goes false the instant the panel is dismissed,
        /// while the panel itself is still sliding out -- which is the right way round: the car
        /// should have its controls back on the frame you close the menu, not a tenth of a
        /// second later because something is still being drawn.
        /// </summary>
        public bool IsOpen => _open;

        /// <summary>How present the panel is, nought to one. Drawn whenever it is not nought.</summary>
        private float _show;

        /// <summary>Where the highlight actually is, which is not always the row it belongs to.</summary>
        private float _rowAt;

        /// <summary>Where the lit tab underline actually is, and how wide.</summary>
        private float _tabAt;
        private float _tabWide;

        /// <summary>Set when something has to arrive where it belongs rather than travel there.</summary>
        private bool _snap = true;

        /// <summary>
        /// Inside a chart, choosing a bar and moving it, rather than moving between rows.
        ///
        /// A MODE, WHICH THIS PANEL OTHERWISE HAS NONE OF, and it is there because the D-pad has
        /// only four directions. Between rows, UP and DOWN move and LEFT and RIGHT change; inside
        /// a chart LEFT and RIGHT have to choose the bar, so UP and DOWN have to become the change
        /// -- and that is a different meaning for the same button, which is what a mode is.
        /// ENTER goes in, ENTER or BACK comes out, and the footer says which you are in.
        /// </summary>
        private bool _editing;
        private int _bar;

        /// <summary>How tall the highlight is, in rows, eased like its position.</summary>
        private float _rowTall = 1f;

        /// <summary>Which way the body is stepping aside for a page turn, one to nought.</summary>
        private float _turn;

        /// <summary>A further fade on everything drawn while the body is mid-turn.</summary>
        private float _dip = 1f;

        /// <summary>When a value was last nudged, so it can be lit for a moment afterwards.</summary>
        private int _touchedAt;

        /// <summary>Where the panel is being drawn this frame, which is left of home while it arrives.</summary>
        private float _drawX = PanelX;

        /// <summary>
        /// How big the whole panel is. One number, and everything below is a fraction of it.
        ///
        /// THE POINT IS THAT THERE IS ONLY ONE. Every size in here used to be its own literal,
        /// most of them written inline in the drawing code -- so making the panel smaller meant
        /// finding thirty numbers and getting the ratios between them right by hand, which is
        /// how a menu ends up with a title that no longer fits its bar. Now the panel has a
        /// size, and the parts have proportions.
        ///
        /// Turned down from 1.0 because it was a big panel for what it says: eighteen short
        /// rows of two words and a number, taking up a quarter of the screen in a game you are
        /// meant to still be driving.
        /// </summary>
        private const float Zoom = 0.78f;

        // Where it sits. NOT scaled -- these are the corner it is pinned to, not its size.
        private const float PanelX = 0.030f;
        private const float PanelTop = 0.155f;

        // How big, as fractions of the screen.
        private const float PanelW = 0.262f * Zoom;
        private const float TitleH = 0.056f * Zoom;
        private const float RowH = 0.0295f * Zoom;
        private const float FootH = 0.044f * Zoom;

        // The margins inside it.
        private const float PadX = 0.012f * Zoom;
        private const float LabelX = 0.017f * Zoom;
        private const float ValueX = 0.010f * Zoom;
        private const float Hair = 0.0016f * Zoom;

        // The pictures on the right of a row: how wide the value's own column is, and the
        // switch and the slider that sit to its left.
        private const float ValueCol = 0.058f * Zoom;
        private const float TrackW = 0.050f * Zoom;
        private const float TrackH = 0.0034f * Zoom;
        private const float PillW = 0.026f * Zoom;
        private const float PillH = 0.0115f * Zoom;

        /// <summary>How far the body steps aside on a page turn, and how fast it settles.</summary>
        private const float Slide = 0.018f * Zoom;
        private const float TurnTau = 0.065f;
        private const float KnobTau = 0.055f;
        private const float FillTau = 0.050f;

        // And the type. Text does not scale with a rectangle on its own.
        private const float TitleText = 0.46f * Zoom;
        private const float TabText = 0.26f * Zoom;
        private const float HeadText = 0.235f * Zoom;
        private const float RowText = 0.295f * Zoom;
        private const float HintText = 0.255f * Zoom;
        private const float FootText = 0.235f * Zoom;

        /// <summary>
        /// How far the panel comes in from, and how quickly everything settles.
        ///
        /// TIME CONSTANTS, NOT DURATIONS. Each of these is the time an eased value takes to
        /// cover about two thirds of the distance left, so nothing has a moment where it stops
        /// dead -- and, more usefully, an animation interrupted half way simply changes where it
        /// is heading rather than having to be cancelled and restarted. Pressing DOWN four times
        /// quickly is one continuous movement, not four that fight each other.
        ///
        /// Small numbers on purpose. A settings panel is a thing you use, not a thing you watch:
        /// past about a tenth of a second the movement stops being feedback and starts being a
        /// wait.
        /// </summary>
        private const float ShowTau = 0.055f;
        private const float RowTau = 0.045f;
        private const float TabTau = 0.060f;
        private const float SlideIn = 0.022f;

        /// <summary>How long a nudged value stays lit after it changes.</summary>
        private const int FlashMs = 220;

        /// <summary>
        /// How many rows fit before it scrolls.
        ///
        /// Larger than the longest page, deliberately, so nothing scrolls today -- and the rows
        /// were tightened and the panel raised to keep it that way rather than letting it grow
        /// down into the minimap. There is a limit to how many times that can be done, and the
        /// scrolling below is what happens when it runs out: it is not dead weight, it is what
        /// stops a page silently losing its last row the day somebody adds one more setting.
        /// </summary>
        private const int Rows = 20;

        private const int Plain = 4;   // Chalet Comprime Cologne

        private static readonly Color Ink = Color.FromArgb(232, 228, 228, 231);
        private static readonly Color Dim = Color.FromArgb(165, 150, 150, 156);
        private static readonly Color Faint = Color.FromArgb(120, 150, 150, 156);

        /// <summary>Indicator amber, which is the one colour this mod has any claim on.</summary>
        private static readonly Color Amber = Color.FromArgb(255, 245, 196, 60);

        private static readonly Color Panel = Color.FromArgb(234, 15, 15, 18);
        private static readonly Color Head = Color.FromArgb(242, 26, 26, 31);

        /// <summary>
        /// The name, in blackletter, as a picture.
        ///
        /// THE GAME HAS NO SUCH FONT. Chalet, House Script, Pricedown and a monospace are what
        /// SET_TEXT_FONT offers, and none of them is a Fraktur. So the title is rendered once,
        /// outside the game, in UnifrakturCook -- the same face Fumes keeps in its tools -- to a
        /// white-on-transparent PNG, and drawn here through the same CustomSprite path Fumes draws
        /// its pump with. The aspect is the one the render reported: 1308 by 241.
        /// </summary>
        private readonly Sprite _title = new Sprite("title.png", 5.4274f);

        /// <summary>The chord that opens this on a pad.</summary>
        private readonly Chord _openChord;

        public Menu(Settings cfg)
        {
            _cfg = cfg;
            _openChord = new Chord(cfg.PadModifier, cfg.PadOpen, "panel");

            // THE PAGE SHOULDERS STAND DOWN IF THE CHORD IS USING THEM. With the panel opened by
            // LB and a direction, LB is held every time it is opened or closed -- and a shoulder
            // that also turns the page would turn one on the way in and another on the way out.
            // The chord is what the player asked for, so the shortcut is what gives way.
            var modifier = Pad.Parse(cfg.PadModifier, "the panel chord");

            _padPrev = Free(modifier, Control.ScriptLB) ? new Button(Control.ScriptLB, false) : null;
            _padNext = Free(modifier, Control.ScriptRB) ? new Button(Control.ScriptRB, false) : null;

            if (_padPrev == null || _padNext == null)
            {
                Log.Info("Panel: the shoulder page shortcut is off, because the chord that opens " +
                         "the panel is using that button.");
            }

            Log.Info("Panel on " + cfg.BindingText() + ", or on a pad " + _openChord.Describe() + ".");

            Build();
        }


        // ==================================================================
        // The icons, seven cells square. Readable here as the thing each draws.
        // ==================================================================

        private static readonly string[] IconKey =
        {
            "..###..",
            ".#...#.",
            ".#...#.",
            "..###..",
            "...#...",
            "...##..",
            "...#...",
        };

        private static readonly string[] IconCog =
        {
            ".#.#.#.",
            "#######",
            ".##.##.",
            "##...##",
            ".##.##.",
            "#######",
            ".#.#.#.",
        };

        private static readonly string[] IconWrench =
        {
            "....###",
            "....#.#",
            "...###.",
            "..##...",
            ".##....",
            "##.....",
            "#......",
        };

        private static readonly string[] IconTyre =
        {
            "..###..",
            ".##.##.",
            "##...##",
            "#.....#",
            "##...##",
            ".##.##.",
            "..###..",
        };

        private static readonly string[] IconDoor =
        {
            "######.",
            "#....#.",
            "#....#.",
            "#...##.",
            "#....#.",
            "#....#.",
            "######.",
        };

        private static readonly string[] IconWheel =
        {
            "..###..",
            ".#...#.",
            "#..#..#",
            "#.###.#",
            "#..#..#",
            ".#...#.",
            "..###..",
        };

        private static readonly string[] IconArrow =
        {
            "...#...",
            "..##...",
            ".#####.",
            "######.",
            ".#####.",
            "..##...",
            "...#...",
        };

        private static readonly string[] IconGauge =
        {
            "..###..",
            ".#...#.",
            "#.....#",
            "#..#..#",
            "#..##.#",
            ".#...#.",
            "..#.#..",
        };

        /// <summary>Which picture is a given icon, by which bitmap it is.</summary>
        private static string IconFile(string[] icon)
        {
            if (icon == null) return null;
            if (ReferenceEquals(icon, IconKey)) return "icon_key.png";
            if (ReferenceEquals(icon, IconCog)) return "icon_cog.png";
            if (ReferenceEquals(icon, IconWrench)) return "icon_wrench.png";
            if (ReferenceEquals(icon, IconTyre)) return "icon_tyre.png";
            if (ReferenceEquals(icon, IconDoor)) return "icon_door.png";
            if (ReferenceEquals(icon, IconWheel)) return "icon_wheel.png";
            if (ReferenceEquals(icon, IconArrow)) return "icon_arrow.png";
            if (ReferenceEquals(icon, IconGauge)) return "icon_gauge.png";
            if (ReferenceEquals(icon, IconSliders)) return "icon_sliders.png";
            return null;
        }

        private static readonly string[] IconSliders =
        {
            ".......",
            "#######",
            "..###..",
            ".......",
            "#######",
            "...###.",
            ".......",
        };

        // ==================================================================
        // The items
        // ==================================================================

        private sealed class Page
        {
            public string Title;
            public string[] Icon;

            /// <summary>The icon as a picture, one draw, with the cells as the fallback.</summary>
            public Sprite Art;
            public readonly List<Item> Items = new List<Item>();
        }

        /// <summary>The sorts of row there are, which is what decides how each is drawn.</summary>
        private enum Kind
        {
            Setting,
            Toggle,
            Number,
            Choice,
            Bind,
            Go,
            Header,
            Chart,
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

            /// <summary>
            /// A chart: several numbers in one row, drawn as bars.
            ///
            /// Bar and SetBar are indexed by bar; Keys is the ini key for each, because six
            /// values written back is six lines in the file, not one.
            /// </summary>
            public bool IsChart;
            public int Bars;
            public Func<int, float> Bar;
            public Action<int, float> SetBar;
            public float Min;
            public float Max;
            public float Step;
            public string Format;
            public string[] Keys;

            /// <summary>What sort of row this is, and the two things a picture of it needs.</summary>
            public Kind Kind;
            public Func<bool> On;
            public Func<float> Value;

            /// <summary>Where the knob or the fill has got to, eased, so each row moves on its own.</summary>
            public float Anim;

            /// <summary>
            /// A heading, not a setting. Drawn differently and skipped by the highlight.
            ///
            /// Rows on this panel used to all look identical, which meant a page of eight was
            /// eight things to read rather than two groups of four. The grouping was real
            /// -- half of IGNITION was about the key and half was about what the car keeps
            /// after you have gone -- and nothing on screen said so.
            /// </summary>
            public bool IsHeader;

            /// <summary>
            /// Whether this row currently does anything.
            ///
            /// Most settings hang off a toggle above them: the hold time means nothing with the
            /// manual ignition off, and the deadzone means nothing without the indicators. They
            /// stay reachable -- you may be about to turn the thing on -- but they are drawn
            /// faint, so a page says at a glance which of it is live.
            /// </summary>
            public Func<bool> Live;
        }

        /// <summary>
        /// A row that DOES something rather than holding a value.
        ///
        /// Every other row on this panel is a setting, and a setting reads as a thing you change.
        /// This one is a door, so it shows an arrow instead of a value and nothing is written to
        /// the ini when the panel closes -- there is nothing to write.
        /// </summary>
        private static Item Go(string label, Action press, string hint, Func<bool> live = null)
        {
            return new Item
            {
                Label = label,
                Hint = hint,
                Show = () => ">",
                Press = press,
                Live = live,
                Kind = Kind.Go,
            };
        }

        private static Item Chart(string label, int bars, Func<int, float> get, Action<int, float> set,
                                  float min, float max, float step, string format,
                                  string section, string[] keys, string hint)
        {
            return new Item
            {
                Label = label,
                Hint = hint,
                Section = section,
                IsChart = true,
                Kind = Kind.Chart,
                Bars = bars,
                Bar = get,
                SetBar = set,
                Min = min,
                Max = max,
                Step = step,
                Format = format,
                Keys = keys,
                Show = () => string.Empty,
            };
        }

        private static string[] Numbered(string prefix, int count)
        {
            var keys = new string[count];

            for (var i = 0; i < count; i++) keys[i] = prefix + (i + 1);

            return keys;
        }

        private static Item Header(string text)
        {
            return new Item { Label = text, IsHeader = true, Kind = Kind.Header };
        }

        private Page Add(string title, string[] icon = null)
        {
            var p = new Page { Title = title, Icon = icon };

            // THE TAB ICONS WERE TWO HUNDRED RECTANGLES A FRAME, which is most of what this panel
            // spent from a budget every script shares -- and when Hoodrich put a menu up beside
            // it, the draws that came after the icons were the ones dropped: the chart bars, on
            // the page where the bars are the point. The same cells, drawn once each as a
            // picture, cost nine.
            var file = IconFile(icon);
            if (file != null) p.Art = new Sprite(file, 1f);
            _pages.Add(p);
            return p;
        }

        private static Item Toggle(string label, Func<bool> get, Action<bool> set,
                                   string section, string key, string hint, Func<bool> live = null)
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
                Live = live,
            };

            item.Kind = Kind.Toggle;
            item.On = get;
            item.Nudge = d => set(!get());
            item.Press = () => set(!get());
            return item;
        }

        private static Item Number(string label, Func<float> get, Action<float> set,
                                   float step, float min, float max, string format,
                                   string unit, string section, string key, string hint,
                                   Func<bool> live = null)
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
                Live = live,
            };

            item.Kind = Kind.Number;
            item.Value = get;
            item.Min = min;
            item.Max = max;
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
                                      string section, string key, string hint,
                                      Func<bool> live = null)
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
                Live = live,
            };

            item.Kind = Kind.Choice;
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
                          string section, string key, string hint, Func<bool> live = null)
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
                Live = live,
            };

            item.Kind = Kind.Bind;
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

        /// <summary>
        /// The pages, grouped by WHEN A SETTING APPLIES rather than by which class implements it.
        ///
        /// It used to be the other way round, and it showed. IGNITION held the hold time and the
        /// hand-back speed -- which are about the key in your hand -- directly above whether the
        /// radio keeps playing, which is about a car you are walking away from. SAFETY was
        /// whatever was left over: a seatbelt, a handbrake and a set of door locks, three things
        /// with nothing in common except having nowhere else to go, and the handbrake plainly
        /// belonged with the rest of parking a car.
        ///
        /// DRIVING is what happens while you are in it. LEAVING is what the car does once you
        /// are not. Those are the two halves this mod is actually about, and a page you can name
        /// in one word is a page you can find a setting on.
        /// </summary>
        private void Build()
        {
            var drive = Add("DRIVING", IconKey);

            // THE FIRST ROW OF THE FIRST PAGE, which is where the highlight lands when the panel
            // opens -- so it is the one row you can reach without moving at all. That is the right
            // place for the only row here that is a door rather than a setting.
            //
            // AND IT IS SAFE THERE NOW, which it would not have been last week. A held chord used
            // to nudge whatever the highlight had landed on, so the top row was the most dangerous
            // seat in the panel; manual ignition sat there and turned itself off. Buttons are
            // disarmed until released now, and a door has no value to nudge in any case -- LEFT
            // and RIGHT do nothing to it.
            drive.Items.Add(Go("Car spawner", () =>
                               {
                                   // Two menus reading the same D-pad is the second one to look
                                   // winning every press, so this one gets out of the way.
                                   Close();

                                   if (OpenSpawner != null) OpenSpawner();
                               },
                               "Every vehicle in the game. Press A or ENTER.",
                               () => _cfg.Spawner));

            drive.Items.Add(Header("THE IGNITION"));

            drive.Items.Add(Toggle("Manual ignition", () => _cfg.ManualIgnition,
                                   v => _cfg.ManualIgnition = v, "Driving", "ManualIgnition",
                                   "Hold the exit key to stop the engine. Tap it to get out."));

            drive.Items.Add(Number("A hold takes", () => _cfg.ExitHoldSeconds,
                                   v => _cfg.ExitHoldSeconds = v, 0.05f, 0.1f, 3f, "0.00", "s",
                                   "Driving", "ExitHoldSeconds",
                                   "Held longer than this stops the engine. Shorter gets you out.",
                                   () => _cfg.ManualIgnition));

            drive.Items.Add(Number("Give the key back above", () => _cfg.ManualIgnitionMaxSpeed,
                                   v => _cfg.ManualIgnitionMaxSpeed = v, 0.5f, 0f, 60f, "0.0", "m/s",
                                   "Driving", "ManualIgnitionMaxSpeed",
                                   "Faster than this and the game's own hold-to-bail is back.",
                                   () => _cfg.ManualIgnition));

            drive.Items.Add(Toggle("Aircraft too", () => _cfg.ManualIgnitionAircraft,
                                   v => _cfg.ManualIgnitionAircraft = v,
                                   "Driving", "ManualIgnitionAircraft",
                                   "Off. The gesture that parks a car kills you in a helicopter.",
                                   () => _cfg.ManualIgnition));

            drive.Items.Add(Toggle("Starter cranks", () => _cfg.StarterCranks,
                                   v => _cfg.StarterCranks = v, "Driving", "StarterCranks",
                                   "The engine turns over before it catches, instead of just being on.",
                                   () => _cfg.ManualIgnition));

            drive.Items.Add(Toggle("Lights off with it", () => _cfg.LightsOffWithEngine,
                                   v => _cfg.LightsOffWithEngine = v,
                                   "Driving", "LightsOffWithEngine",
                                   "Holding the key kills the lights too. A tap leaves them.",
                                   () => _cfg.ManualIgnition));

            drive.Items.Add(Header("THE DASH"));

            drive.Items.Add(Toggle("Cabin light", () => _cfg.DashLight,
                                   v => _cfg.DashLight = v, "Driving", "DashLight",
                                   "On its own switch, like every car ever built."));

            drive.Items.Add(Bind("Cabin light key", () => _cfg.DashLightKey,
                                 v => _cfg.DashLightKey = v, "Driving", "DashLightKey",
                                 "ENTER, then press the key you want. On a pad, modifier + left.",
                                 () => _cfg.DashLight));

            drive.Items.Add(Header("HOW HE SITS"));

            drive.Items.Add(Toggle("Lowrider pose", () => _cfg.LowriderPose,
                                   v => _cfg.LowriderPose = v, "Driving", "LowriderPose",
                                   "Sat back, arm out the window, in every car."));

            drive.Items.Add(Toggle("Window down with it", () => _cfg.LowriderWindow,
                                   v => _cfg.LowriderWindow = v, "Driving", "LowriderWindow",
                                   "An arm hanging through glass is worse than no arm.",
                                   () => _cfg.LowriderPose));

            drive.Items.Add(Header("THE SEATBELT"));

            drive.Items.Add(Toggle("Seatbelt", () => _cfg.Seatbelt, v => _cfg.Seatbelt = v,
                                   "Driving", "Seatbelt",
                                   "You are wearing it unless you take it off. No prompt, no icon."));

            drive.Items.Add(Bind("Unbuckle key", () => _cfg.SeatbeltKey, v => _cfg.SeatbeltKey = v,
                                 "Driving", "SeatbeltKey",
                                 "Takes it off, and it stays off until you get out of this car.",
                                 () => _cfg.Seatbelt));

            drive.Items.Add(Number("Belts up after", () => _cfg.SeatbeltSeconds,
                                   v => _cfg.SeatbeltSeconds = v, 0.1f, 0f, 10f, "0.0", "s",
                                   "Driving", "SeatbeltSeconds",
                                   "About as long as reaching over your shoulder takes.",
                                   () => _cfg.Seatbelt));

            // A PAGE OF ITS OWN, because DRIVING was carrying five groups and had run out of
            // room three features ago. Ignition, handbrake, tyres, dash and seatbelt is not one
            // page, it is a page and a bit -- and the three that are about how much the car
            // holds the road belong together anyway.
            //
            // Their ini section is still [Driving]. Pages and sections have matched everywhere
            // else and here they do not, deliberately: moving the keys would mean the merge
            // adding fresh defaults under a new heading while somebody's real values sat under
            // the old one, and the new copy would win. A page is a way to find a setting; the
            // section is where the value has always lived.
            var tune = Add("TUNING", IconWrench);

            tune.Items.Add(Header("THE ENGINE"));

            tune.Items.Add(Number("Power", () => _cfg.PowerMultiplier,
                                  v => _cfg.PowerMultiplier = v, 0.05f, 0.25f, 3f, "0.00", null,
                                  "Driving", "PowerMultiplier",
                                  "The top end. 1.00 is the car as it came."));

            tune.Items.Add(Number("Torque", () => _cfg.TorqueMultiplier,
                                  v => _cfg.TorqueMultiplier = v, 0.05f, 0.25f, 3f, "0.00", null,
                                  "Driving", "TorqueMultiplier",
                                  "The low end, which is the half you actually feel."));

            tune.Items.Add(Header("THE STEERING"));

            tune.Items.Add(Number("Steering lock", () => _cfg.SteeringLock,
                                  v => _cfg.SteeringLock = v, 0.05f, 1f, 2.5f, "0.00", null,
                                  "Driving", "SteeringLock",
                                  "All the time. Counter-steer multiplies this while sideways."));

            tune.Items.Add(Header("THE TYRES"));

            tune.Items.Add(Toggle("Tyres never burst", () => _cfg.TyresNeverBurst,
                                  v => _cfg.TyresNeverBurst = v, "Driving", "TyresNeverBurst",
                                  "Kerbs, spikes and gunfire stop mattering."));

            tune.Items.Add(Toggle("Wheels never break off", () => _cfg.WheelsNeverBreak,
                                  v => _cfg.WheelsNeverBreak = v, "Driving", "WheelsNeverBreak",
                                  "A hard kerb strike leaves the wheel where it was."));

            tune.Items.Add(Toggle("Wheels never bend", () => _cfg.WheelsNeverDeform,
                                  v => _cfg.WheelsNeverDeform = v, "Driving", "WheelsNeverDeform",
                                  "A bent wheel is permanent, and it steers."));

            tune.Items.Add(Header("THE SUSPENSION"));

            tune.Items.Add(Toggle("Softer springs", () => _cfg.SoftSuspension,
                                  v => _cfg.SoftSuspension = v, "Driving", "SoftSuspension",
                                  "The game's own reduced suspension force."));

            tune.Items.Add(Number("Hydraulics sit at", () => _cfg.HydraulicRaise,
                                  v => _cfg.HydraulicRaise = v, 0.05f, 0f, 1f, "0.00", null,
                                  "Driving", "HydraulicRaise",
                                  "0.00 leaves them alone. Only does anything on Benny's cars."));

            tune.Items.Add(Header("STANCE"));

            tune.Items.Add(Number("Camber front", () => _cfg.CamberFront,
                                  v => _cfg.CamberFront = v, 0.5f, -20f, 20f, "0.0", "deg",
                                  "Driving", "CamberFront",
                                  "How far the tops of the wheels lean. 0 is standard."));

            tune.Items.Add(Number("Camber rear", () => _cfg.CamberRear,
                                  v => _cfg.CamberRear = v, 0.5f, -20f, 20f, "0.0", "deg",
                                  "Driving", "CamberRear",
                                  "If it leans the wrong way, use the other sign."));

            tune.Items.Add(Number("Track front", () => _cfg.TrackFront,
                                  v => _cfg.TrackFront = v, 0.01f, -0.3f, 0.3f, "0.00", "m",
                                  "Driving", "TrackFront",
                                  "How much further apart the front wheels sit."));

            tune.Items.Add(Number("Track rear", () => _cfg.TrackRear,
                                  v => _cfg.TrackRear = v, 0.01f, -0.3f, 0.3f, "0.00", "m",
                                  "Driving", "TrackRear",
                                  "Mirrored across the axle, so both go the same way."));

            tune.Items.Add(Number("Height front", () => _cfg.HeightFront,
                                  v => _cfg.HeightFront = v, 0.01f, -0.3f, 0.3f, "0.00", "m",
                                  "Driving", "HeightFront",
                                  "Where the wheel sits in the arch, not how soft it is."));

            tune.Items.Add(Number("Height rear", () => _cfg.HeightRear,
                                  v => _cfg.HeightRear = v, 0.01f, -0.3f, 0.3f, "0.00", "m",
                                  "Driving", "HeightRear",
                                  "Applied every frame, because the game poses wheels every frame."));

            var gears = Add("GEARS", IconCog);

            gears.Items.Add(Header("TORQUE, GEAR BY GEAR"));

            gears.Items.Add(Chart("Torque multiplier", 6,
                                  i => _cfg.GearTorque[i], (i, v) => _cfg.GearTorque[i] = v,
                                  0.25f, 3f, 0.05f, "0.00",
                                  "Driving", Numbered("GearTorque", 6),
                                  "More in second than first is a curve you can see. ENTER to shape it."));

            gears.Items.Add(Header("HOW SLIDY THE TYRES ARE, GEAR BY GEAR"));

            gears.Items.Add(Chart("Extra slide", 6,
                                  i => _cfg.GearSlide[i], (i, v) => _cfg.GearSlide[i] = v,
                                  0f, 1f, 0.05f, "0.00",
                                  "Driving", Numbered("GearSlide", 6),
                                  "On top of the GRIP drift slider. 0 is that slider alone."));

            var grip = Add("GRIP", IconTyre);

            grip.Items.Add(Header("THE HANDBRAKE"));

            grip.Items.Add(Toggle("Front wheels keep pulling", () => _cfg.FwdHandbrake,
                                   v => _cfg.FwdHandbrake = v, "Driving", "FwdHandbrake",
                                   "On front-drive cars only. A handbrake is a rear brake."));

            grip.Items.Add(Number("How hard they pull", () => _cfg.FwdHandbrakePull,
                                   v => _cfg.FwdHandbrakePull = v, 0.5f, 0f, 15f, "0.0", "m/s2",
                                   "Driving", "FwdHandbrakePull",
                                   "A real acceleration: the same pull in a hatchback and a van.",
                                   () => _cfg.FwdHandbrake));

            grip.Items.Add(Number("Until they give up at", () => _cfg.FwdHandbrakeMaxSpeed,
                                   v => _cfg.FwdHandbrakeMaxSpeed = v, 0.5f, 0.5f, 30f, "0.0", "m/s",
                                   "Driving", "FwdHandbrakeMaxSpeed",
                                   "A locked rear axle should win eventually. Eight is a fast jog.",
                                   () => _cfg.FwdHandbrake));

            grip.Items.Add(Header("THE TYRES"));

            grip.Items.Add(Number("Drift", () => _cfg.DriftAmount,
                                  v => _cfg.DriftAmount = v, 0.05f, 0f, 1f, "0.00", null,
                                  "Driving", "DriftAmount",
                                  "0 is off. Nudge it while driving and the car changes under you."));


            grip.Items.Add(Header("UNDER A SLIDE"));

            grip.Items.Add(Toggle("Keep the power on", () => _cfg.DriftPower,
                                  v => _cfg.DriftPower = v, "Driving", "DriftPower",
                                  "The game bogs a car down the moment it goes sideways."));

            grip.Items.Add(Number("How much it finds", () => _cfg.DriftPowerBoost,
                                  v => _cfg.DriftPowerBoost = v, 0.05f, 1f, 3f, "0.00", null,
                                  "Driving", "DriftPowerBoost",
                                  "At full slide, on top of the TUNING power. 1.00 is none.",
                                  () => _cfg.DriftPower));

            grip.Items.Add(Number("And how much torque", () => _cfg.DriftTorqueBoost,
                                  v => _cfg.DriftTorqueBoost = v, 0.05f, 1f, 3f, "0.00", null,
                                  "Driving", "DriftTorqueBoost",
                                  "The half that holds a slide. Same ramp as the power.",
                                  () => _cfg.DriftPower));

            grip.Items.Add(Toggle("More lock to catch it", () => _cfg.CounterSteer,
                                  v => _cfg.CounterSteer = v, "Driving", "CounterSteer",
                                  "Running out of steering is why GTA slides end by themselves."));

            grip.Items.Add(Number("How much more lock", () => _cfg.CounterSteerLock,
                                  v => _cfg.CounterSteerLock = v, 0.05f, 1f, 3f, "0.00", null,
                                  "Driving", "CounterSteerLock",
                                  "At full slide, on top of the TUNING lock. 1.00 is none.",
                                  () => _cfg.CounterSteer));

            grip.Items.Add(Number("All of it by", () => _cfg.CounterSteerFull,
                                  v => _cfg.CounterSteerFull = v, 1f, 5f, 90f, "0", "deg",
                                  "Driving", "CounterSteerFull",
                                  "Where the extra lock has all arrived. Early is vague, late is short.",
                                  () => _cfg.CounterSteer));

            grip.Items.Add(Number("Counts as a slide past", () => _cfg.DriftAngle,
                                  v => _cfg.DriftAngle = v, 1f, 3f, 60f, "0", "deg",
                                  "Driving", "DriftAngle",
                                  "Between where it points and where it is going. Governs both.",
                                  () => _cfg.DriftPower || _cfg.CounterSteer));

            var leave = Add("LEAVING", IconDoor);

            leave.Items.Add(Header("WHAT THE CAR KEEPS"));

            leave.Items.Add(Toggle("Radio keeps playing", () => _cfg.RadioKeepsPlaying,
                                   v => _cfg.RadioKeepsPlaying = v, "Leaving", "RadioKeepsPlaying",
                                   "A car left running keeps its station, audible from outside."));

            leave.Items.Add(Toggle("Lights stay as left", () => _cfg.LightsStayAsLeft,
                                   v => _cfg.LightsStayAsLeft = v, "Leaving", "LightsStayAsLeft",
                                   "Headlights carry through the same way the engine and radio do."));

            leave.Items.Add(Toggle("Door left open", () => _cfg.LeaveDoorOpen,
                                   v => _cfg.LeaveDoorOpen = v, "Leaving", "LeaveDoorOpen",
                                   "Shut again when you get back in. Traffic may shut it for you."));

            leave.Items.Add(Toggle("Handbrake on exit", () => _cfg.HandbrakeOnExit,
                                   v => _cfg.HandbrakeOnExit = v, "Leaving", "HandbrakeOnExit",
                                   "Off is a car at the bottom of the hill you parked on."));

            leave.Items.Add(Header("LOCKING"));

            leave.Items.Add(Toggle("Locking", () => _cfg.Locking, v => _cfg.Locking = v,
                                   "Leaving", "Locking",
                                   "Your car only: the one you are in, or the last one you drove."));

            leave.Items.Add(Bind("Lock key", () => _cfg.LockKey, v => _cfg.LockKey = v,
                                 "Leaving", "LockKey",
                                 "Locks and unlocks. Works from outside if you are near it.",
                                 () => _cfg.Locking));

            leave.Items.Add(Toggle("The horn answers", () => _cfg.LockChirp,
                                   v => _cfg.LockChirp = v, "Leaving", "LockChirp",
                                   "The way a real one does. It is the only sign it worked.",
                                   () => _cfg.Locking));

            leave.Items.Add(Header("COMING BACK TO IT"));

            leave.Items.Add(Toggle("Your car stays put", () => _cfg.KeepParked,
                                   v => _cfg.KeepParked = v, "Leaving", "KeepParked",
                                   "GTA throws away cars nobody is looking at. This one it keeps."));

            leave.Items.Add(Toggle("Blip on it", () => _cfg.ParkedBlip,
                                   v => _cfg.ParkedBlip = v, "Leaving", "ParkedBlip",
                                   "Hidden while you are in it. It is for finding, not riding along."));

            leave.Items.Add(Toggle("Remembers its station", () => _cfg.RememberStations,
                                   v => _cfg.RememberStations = v, "Leaving", "RememberStations",
                                   "Get back in and it is on what you left it on."));

            var auto = Add("AUTOPILOT", IconWheel);

            auto.Items.Add(Header("CRUISE CONTROL"));

            auto.Items.Add(Toggle("Cruise control", () => _cfg.Cruise, v => _cfg.Cruise = v,
                                  "Autopilot", "Cruise",
                                  "Holds the speed you set it at. Braking cancels it."));

            auto.Items.Add(Bind("Cruise key", () => _cfg.CruiseKey, v => _cfg.CruiseKey = v,
                                "Autopilot", "CruiseKey",
                                "Sets it at whatever you are doing. Press again to let go.",
                                () => _cfg.Cruise));

            auto.Items.Add(Header("SELF DRIVING"));

            auto.Items.Add(Toggle("Self driving", () => _cfg.AutoDrive, v => _cfg.AutoDrive = v,
                                  "Autopilot", "AutoDrive",
                                  "The task every ambient driver runs. Touch any control to take over."));

            auto.Items.Add(Bind("Self driving key", () => _cfg.AutoDriveKey,
                                v => _cfg.AutoDriveKey = v, "Autopilot", "AutoDriveKey",
                                "Hands the car over, and takes it back.",
                                () => _cfg.AutoDrive));

            auto.Items.Add(Number("It drives at", () => _cfg.AutoDriveSpeed,
                                  v => _cfg.AutoDriveSpeed = v, 5f, 5f, 200f, "0", "kph",
                                  "Autopilot", "AutoDriveSpeed",
                                  "What it aims for. Traffic and corners have their own opinions.",
                                  () => _cfg.AutoDrive));

            auto.Items.Add(Choice("How it drives", () => _cfg.AutoDriveStyle,
                                  v => _cfg.AutoDriveStyle = v, "Autopilot", "AutoDriveStyle",
                                  "RECKLESS ignores red lights. It is not a joke setting.",
                                  () => _cfg.AutoDrive));

            var ind = Add("INDICATORS", IconArrow);

            ind.Items.Add(Header("THE STALK"));

            ind.Items.Add(Toggle("Steering indicators", () => _cfg.Blinkers,
                                 v => _cfg.Blinkers = v, "Indicators", "Blinkers",
                                 "Hold the wheel over and that side comes on."));

            ind.Items.Add(Number("Comes on after", () => _cfg.BlinkerArmSeconds,
                                 v => _cfg.BlinkerArmSeconds = v, 0.1f, 0.1f, 5f, "0.0", "s",
                                 "Indicators", "BlinkerArmSeconds",
                                 "Short enough to be deliberate, long enough not to be a wobble.",
                                 () => _cfg.Blinkers));

            ind.Items.Add(Number("Straight cancels after", () => _cfg.BlinkerCancelSeconds,
                                 v => _cfg.BlinkerCancelSeconds = v, 0.1f, 0.1f, 10f, "0.0", "s",
                                 "Indicators", "BlinkerCancelSeconds",
                                 "Only while moving, which is what lets you signal at a light.",
                                 () => _cfg.Blinkers));

            ind.Items.Add(Number("Opposite lock cancels after", () => _cfg.BlinkerOppositeSeconds,
                                 v => _cfg.BlinkerOppositeSeconds = v, 0.1f, 0f, 5f, "0.0", "s",
                                 "Indicators", "BlinkerOppositeSeconds",
                                 "A held turn the other way. A flick to line up should not count.",
                                 () => _cfg.Blinkers));

            ind.Items.Add(Header("READING THE WHEEL"));

            ind.Items.Add(Number("Deadzone", () => _cfg.BlinkerDeadzone,
                                 v => _cfg.BlinkerDeadzone = v, 0.05f, 0.05f, 0.95f, "0.00", null,
                                 "Indicators", "BlinkerDeadzone",
                                 "How far over the wheel counts as turned at all, 0 to 1.",
                                 () => _cfg.Blinkers));

            ind.Items.Add(Number("Cancels only above", () => _cfg.BlinkerMinSpeed,
                                 v => _cfg.BlinkerMinSpeed = v, 0.1f, 0f, 20f, "0.0", "m/s",
                                 "Indicators", "BlinkerMinSpeed",
                                 "Below this a centred wheel means nothing. Zero breaks signalling at lights.",
                                 () => _cfg.Blinkers));

            ind.Items.Add(Toggle("Invert the fallback axis", () => _cfg.BlinkerInvert,
                                 v => _cfg.BlinkerInvert = v, "Indicators", "BlinkerInvert",
                                 "Only for setups where the one-sided steering controls read nothing.",
                                 () => _cfg.Blinkers));

            ind.Items.Add(Header("HAZARDS"));

            ind.Items.Add(Bind("Hazards key", () => _cfg.HazardKey, v => _cfg.HazardKey = v,
                               "Indicators", "HazardKey",
                               "Both sides at once. On a pad it is the modifier and D-pad down.",
                               () => _cfg.Blinkers));

            var speed = Add("SPEEDO", IconGauge);

            speed.Items.Add(Header("THE READOUT"));

            speed.Items.Add(Toggle("Speedo", () => _cfg.Speedo, v => _cfg.Speedo = v,
                                   "Speedo", "Speedo",
                                   "Three digits, the unit, the revs and the gear."));

            speed.Items.Add(Choice("Units", () => _cfg.SpeedoUnits, v => _cfg.SpeedoUnits = v,
                                   "Speedo", "SpeedoUnits", "What the number means.",
                                   () => _cfg.Speedo));

            speed.Items.Add(Choice("Colour", () => _cfg.SpeedoColour, v => _cfg.SpeedoColour = v,
                                   "Speedo", "SpeedoColour",
                                   "Colours real displays are actually made in.",
                                   () => _cfg.Speedo));

            speed.Items.Add(Toggle("Rev strip", () => _cfg.SpeedoRevs, v => _cfg.SpeedoRevs = v,
                                   "Speedo", "SpeedoRevs",
                                   "Cells that fill as it revs. The last fifth is red.",
                                   () => _cfg.Speedo));

            speed.Items.Add(Toggle("Gear", () => _cfg.SpeedoGear, v => _cfg.SpeedoGear = v,
                                   "Speedo", "SpeedoGear",
                                   "Beside the unit. Reverse reads as r.",
                                   () => _cfg.Speedo));

            speed.Items.Add(Toggle("Engine lamp", () => _cfg.SpeedoEngineIcon,
                                   v => _cfg.SpeedoEngineIcon = v, "Speedo", "SpeedoEngineIcon",
                                   "Green, amber, red. Its own thing, not part of the speedo."));

            speed.Items.Add(Toggle("Oil lamp", () => _cfg.SpeedoOilLight,
                                   v => _cfg.SpeedoOilLight = v, "Speedo", "SpeedoOilLight",
                                   "Lit only when it is low, which is the point of a warning light."));

            speed.Items.Add(Header("WHERE AND HOW BIG"));

            // THE PANEL SHOWS THE SPEEDO WHILE IT IS OPEN, which is what makes these four rows
            // usable at all. Nudging a position you cannot see is not adjusting, it is guessing
            // and then going to look.
            speed.Items.Add(Number("Across", () => _cfg.SpeedoX, v => _cfg.SpeedoX = v,
                                   0.002f, 0f, 1f, "0.000", null, "Speedo", "SpeedoX",
                                   "It moves as you hold the arrow. Watch it, do not count.",
                                   () => _cfg.Speedo));

            speed.Items.Add(Number("Down", () => _cfg.SpeedoY, v => _cfg.SpeedoY = v,
                                   0.002f, 0f, 1f, "0.000", null, "Speedo", "SpeedoY",
                                   "Zero is the top of the screen, one is the bottom.",
                                   () => _cfg.Speedo));

            speed.Items.Add(Number("Size", () => _cfg.SpeedoScale, v => _cfg.SpeedoScale = v,
                                   0.05f, 0.4f, 3f, "0.00", null, "Speedo", "SpeedoScale",
                                   "Everything scales together, including the rev strip.",
                                   () => _cfg.Speedo));

            speed.Items.Add(Number("Opacity", () => _cfg.SpeedoOpacity,
                                   v => _cfg.SpeedoOpacity = v, 0.05f, 0.15f, 1f, "0.00", null,
                                   "Speedo", "SpeedoOpacity", "The whole thing at once.",
                                   () => _cfg.Speedo));

            speed.Items.Add(Toggle("Backing", () => _cfg.SpeedoBackground,
                                   v => _cfg.SpeedoBackground = v, "Speedo", "SpeedoBackground",
                                   "A dark panel, so it reads against a white car in daylight.",
                                   () => _cfg.Speedo));

            speed.Items.Add(Toggle("Unlit segments", () => _cfg.SpeedoGhost,
                                   v => _cfg.SpeedoGhost = v, "Speedo", "SpeedoGhost",
                                   "Faintly drawn, the way a real display shows them.",
                                   () => _cfg.Speedo));

            speed.Items.Add(Number("Lamps across", () => _cfg.SpeedoLampsX,
                                   v => _cfg.SpeedoLampsX = v, 0.002f, 0f, 1f, "0.000", null,
                                   "Speedo", "SpeedoLampsX",
                                   "The lamps have their own place. They are not part of the speedo.",
                                   () => _cfg.SpeedoEngineIcon || _cfg.SpeedoOilLight));

            speed.Items.Add(Number("Lamps down", () => _cfg.SpeedoLampsY,
                                   v => _cfg.SpeedoLampsY = v, 0.002f, 0f, 1f, "0.000", null,
                                   "Speedo", "SpeedoLampsY",
                                   "Watch them move. Zero is the top of the screen.",
                                   () => _cfg.SpeedoEngineIcon || _cfg.SpeedoOilLight));

            speed.Items.Add(Toggle("Only in a vehicle", () => _cfg.SpeedoOnlyInVehicle,
                                   v => _cfg.SpeedoOnlyInVehicle = v,
                                   "Speedo", "SpeedoOnlyInVehicle",
                                   "It shows here regardless while this panel is open.",
                                   () => _cfg.Speedo));

            var gen = Add("GENERAL", IconSliders);

            gen.Items.Add(Toggle("Everything on", () => _cfg.Enabled, v => _cfg.Enabled = v,
                                 "General", "Enabled",
                                 "Off leaves the game exactly as it was. This panel still opens."));

            gen.Items.Add(Toggle("Say hello on load", () => _cfg.AnnounceOnLoad,
                                 v => _cfg.AnnounceOnLoad = v, "General", "AnnounceOnLoad",
                                 "Off by default. Nothing here announces itself."));

            // THE LIVE LEVEL IS SET TOO, not just the stored one. Log.Level is what Log actually
            // reads; _cfg.LogLevel is only what gets written to the ini. Setting one without the
            // other gives a row that appears to work, changes nothing until a restart, and says
            // nothing about it -- on the one setting a person only ever touches because they are
            // already trying to find out why something is not working.
            gen.Items.Add(Choice("Log detail", () => _cfg.LogLevel,
                                 v => { _cfg.LogLevel = v; Log.Level = v; },
                                 "General", "LogLevel",
                                 "DEBUG is loud, and is what to send with a bug report."));

            gen.Items.Add(Header("CRASHES"));

            gen.Items.Add(Toggle("Slow motion on a big one", () => _cfg.CrashSlowMo,
                                 v => _cfg.CrashSlowMo = v, "General", "CrashSlowMo",
                                 "The only thing here that slows the whole world, not just you."));

            gen.Items.Add(Number("Only above", () => _cfg.CrashSlowMoSpeed,
                                 v => _cfg.CrashSlowMoSpeed = v, 5f, 10f, 400f, "0", "kph",
                                 "General", "CrashSlowMoSpeed",
                                 "How fast you were going the instant before it.",
                                 () => _cfg.CrashSlowMo));

            // ON THE PANEL RATHER THAN BURIED, because it is the number that decides whether this
            // fires at all. If a crash that plainly deserved it goes by untouched, this is the
            // one to lower -- and having to find a text file to do it is how a feature gets
            // written off as broken.
            gen.Items.Add(Number("Impact needed", () => _cfg.CrashSlowMoDrop,
                                 v => _cfg.CrashSlowMoDrop = v, 0.5f, 1f, 40f, "0.0", "m/s",
                                 "General", "CrashSlowMoDrop",
                                 "Speed lost in one frame. Lower it if big crashes go unnoticed.",
                                 () => _cfg.CrashSlowMo));

            gen.Items.Add(Number("How slow", () => _cfg.CrashSlowMoScale,
                                 v => _cfg.CrashSlowMoScale = v, 0.05f, 0.05f, 1f, "0.00", null,
                                 "General", "CrashSlowMoScale",
                                 "A fraction of normal speed. 1.00 is no slow motion at all.",
                                 () => _cfg.CrashSlowMo));

            gen.Items.Add(Number("For how long", () => _cfg.CrashSlowMoSeconds,
                                 v => _cfg.CrashSlowMoSeconds = v, 0.1f, 0.1f, 4f, "0.0", "s",
                                 "General", "CrashSlowMoSeconds",
                                 "Measured in game time, so it lasts longer than it reads.",
                                 () => _cfg.CrashSlowMo));

            gen.Items.Add(Header("THE SPAWNER"));

            gen.Items.Add(Toggle("Car spawner", () => _cfg.Spawner, v => _cfg.Spawner = v,
                                 "General", "Spawner",
                                 "Off hides the row at the top of DRIVING that opens it."));

            gen.Items.Add(Toggle("Show it in front of you", () => _cfg.SpawnerDemo,
                                 v => _cfg.SpawnerDemo = v, "General", "SpawnerDemo",
                                 "As well as the picture. Most cars have no picture.",
                                 () => _cfg.Spawner));

            gen.Items.Add(Bind("Spawner key", () => _cfg.SpawnerKey, v => _cfg.SpawnerKey = v,
                               "General", "SpawnerKey",
                               "ENTER, then press the key you want. F7 by default.",
                               () => _cfg.Spawner));

            gen.Items.Add(Header("REPAIRS"));

            gen.Items.Add(Toggle("Repair as you drive", () => _cfg.Repairs,
                                 v => _cfg.Repairs = v, "General", "Repairs",
                                 "Every few minutes, if there is anything to put right."));

            gen.Items.Add(Number("Every", () => _cfg.RepairMinutes,
                                 v => _cfg.RepairMinutes = v, 0.5f, 0.5f, 60f, "0.0", "min",
                                 "General", "RepairMinutes",
                                 "The clock restarts either way. A wreck is left as a wreck.",
                                 () => _cfg.Repairs));

            gen.Items.Add(Header("THIS PANEL"));

            gen.Items.Add(Choice("Modifier", () => _cfg.MenuModifier,
                                 v => _cfg.MenuModifier = v, "General", "MenuModifier",
                                 "NONE is the bare key. Pick one you do not drive with."));

            gen.Items.Add(Bind("Key", () => _cfg.MenuKey, v => _cfg.MenuKey = v,
                               "General", "MenuKey",
                               "ENTER, then press the key you want. ESC cancels."));

            // The highlight cannot start on a heading.
            _row = 0;
            Settle(1);
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
            private readonly bool _hasKey;
            private readonly bool _hasPad;
            private readonly bool _repeats;

            private bool _down;
            private int _repeatAt;

            /// <summary>Held from before we were listening, so it does not count until let go.</summary>
            private bool _muted;

            /// <summary>Whether this counted as a press on the frame Poll last ran.</summary>
            public bool Fired { get; private set; }

            public Button(Keys key, Control pad, bool repeats)
            {
                _key = key;
                _pad = pad;
                _hasKey = true;
                _hasPad = true;
                _repeats = repeats;
            }

            public Button(Keys key, bool repeats)
            {
                _key = key;
                _hasKey = true;
                _repeats = repeats;
            }

            /// <summary>
            /// A button with no key behind it at all.
            ///
            /// FOR THE SHOULDERS, which have no keyboard equivalent worth inventing -- TAB
            /// already turns the page and there is nothing to gain from giving it a second key.
            /// Written as its own constructor rather than passed Keys.None, because Keys.None
            /// would still be handed to IsKeyPressed every frame and what that answers for a
            /// key that does not exist is not something worth finding out at sixty hertz.
            /// </summary>
            public Button(Control pad, bool repeats)
            {
                _pad = pad;
                _hasPad = true;
                _repeats = repeats;
            }

            /// <summary>
            /// Treats this button as held-from-before, until it is actually released.
            ///
            /// THE ONE-FRAME GUARD WAS NOT ENOUGH, and the way it failed is worth writing down.
            /// The panel used to open on a chord ending in D-pad UP, which is the panel's own UP,
            /// and polling once on the opening frame was the fix: the button is recorded as
            /// already down, so there is no edge and nothing moves.
            ///
            /// Then the chord moved to D-pad RIGHT, which is the panel's NUDGE. One frame still
            /// stopped the edge -- but a button held past the repeat delay fires again on its own,
            /// and three hundred and fifty milliseconds is not a long press. So opening the panel
            /// and not letting go instantly nudged whatever row the highlight had landed on, which
            /// is the first row of the first page: manual ignition. It switched itself off, and
            /// the exit key quietly went back to being the game's.
            ///
            /// A frame was the wrong unit. The right one is "until you let go".
            /// </summary>
            public void Disarm()
            {
                _down = true;
                _muted = true;
                Fired = false;
            }

            public void Poll()
            {
                var down = (_hasKey && Held(_key)) || (_hasPad && Pad.Held(_pad));

                if (!down)
                {
                    _down = false;
                    _muted = false;
                    Fired = false;
                    return;
                }

                if (_muted)
                {
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

        /// <summary>Whether a shoulder is still available, given what the chord has claimed.</summary>
        private static bool Free(Control? modifier, Control shoulder)
        {
            return !modifier.HasValue || modifier.Value != shoulder;
        }

        /// <summary>
        /// The shoulders, which turn the page on a pad the way TAB does on a keyboard.
        ///
        /// THE SCRIPT GROUP, NOT THE FRONTEND ONE. Both have an LB and an RB in the enum and
        /// only one of them is meant for us: the frontend group belongs to the game's own
        /// menus, which is the same reason everything else in here is driven off the phone's
        /// buttons rather than the frontend's.
        ///
        /// SAFE BECAUSE THE PANEL IS DEAF. Whatever LB and RB otherwise do in a car, they are
        /// disabled for as long as this is open, so there is no collision to reason about --
        /// which is not true of the chord that OPENS the panel, and is why that one is a chord.
        ///
        /// Not load-bearing. Up and down already run continuously through every page, which was
        /// the deliberate answer to a pad having no spare buttons; this is a shortcut on top of
        /// that. If it turns out these do not read during gameplay, nothing is lost but a
        /// shortcut, and nothing anywhere depends on them firing.
        /// </summary>
        private readonly Button _padPrev;
        private readonly Button _padNext;

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
        /// <summary>
        /// Nothing counts until it has been let go of.
        ///
        /// EVERY BUTTON, NOT JUST THE CHORD'S. The panel cannot see which controls the chord is
        /// made of without asking, and the answer changes with the settings -- so rather than
        /// disarm the two it thinks are involved, it disarms all of them. Nothing here is meant
        /// to happen on the frame it opens anyway.
        /// </summary>
        private void Disarm()
        {
            _up.Disarm();
            _down.Disarm();
            _left.Disarm();
            _right.Disarm();
            _accept.Disarm();
            _back.Disarm();
            _tab.Disarm();

            if (_padPrev != null) _padPrev.Disarm();
            if (_padNext != null) _padNext.Disarm();
        }

        private void Poll()
        {
            _up.Poll();
            _down.Poll();
            _left.Poll();
            _right.Poll();
            _accept.Poll();
            _back.Poll();
            _tab.Poll();

            if (_padPrev != null) _padPrev.Poll();
            if (_padNext != null) _padNext.Poll();
        }

        public void Update()
        {
            // FIRST, AND WHETHER OR NOT IT IS OPEN. Closing is a movement too, and a panel that
            // only advanced its animation while it was open would freeze half way out and stay
            // there until the next time it was opened.
            Animate();

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

                    // NOTHING COUNTS UNTIL IT IS RELEASED. The chord that opened this is still
                    // being held, and its buttons are the panel's buttons.
                    Disarm();

                    // The highlight belongs on the row it is on, not wherever it was left when
                    // the panel last shut.
                    _snap = true;
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

                // STILL DRAWN WHILE IT LEAVES, and taking no input while it does. This is the
                // half of the animation nobody writes: a panel that vanishes the frame you
                // dismiss it has an opening animation and no closing one, which reads as the
                // menu being interrupted rather than put away.
                if (_show > 0f) Render();

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
                Game.DisableControlThisFrame(Control.ScriptLB);
                Game.DisableControlThisFrame(Control.ScriptRB);

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
            if (_editing)
            {
                var chart = _pages[_page].Items[_row];

                if (!chart.IsChart)
                {
                    _editing = false;
                }
                else
                {
                    if (_left.Fired && _bar > 0) _bar--;
                    if (_right.Fired && _bar < chart.Bars - 1) _bar++;

                    if (_up.Fired) Move(chart, 1);
                    if (_down.Fired) Move(chart, -1);

                    // BACK LEAVES THE CHART, NOT THE PANEL. Inside a chart the way out is one
                    // level up, the same as it would be from the rebind row; a B that shut the
                    // whole thing from in here would throw away the bar you were half way through.
                    if (_accept.Fired || _back.Fired) _editing = false;

                    return;
                }
            }

            if (_tab.Fired || (_padNext != null && _padNext.Fired))
            {
                TurnPage(1, true);
                Settle(1);
                return;
            }

            if (_padPrev != null && _padPrev.Fired)
            {
                TurnPage(-1, true);
                Settle(1);
                return;
            }

            if (_up.Fired) { _row--; Settle(-1); }
            if (_down.Fired) { _row++; Settle(1); }

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
            var page = _pages[_page];

            // Keep the highlight on screen with a margin, so the next row is visible before
            // you get to it rather than appearing as you land on it.
            if (_row < _scroll) _scroll = _row;
            if (_row >= _scroll + Rows) _scroll = _row - Rows + 1;

            var item = page.Items[_row];

            if (_left.Fired && item.Nudge != null) Touch(item, -1);
            if (_right.Fired && item.Nudge != null) Touch(item, 1);

            if (_accept.Fired && item.IsChart)
            {
                _editing = true;
                _bar = 0;
                return;
            }

            if (_accept.Fired && item.Press != null)
            {
                item.Press();
                if (item.Section != null) _changed.Add(item);
            }

            if (_back.Fired) Close();
        }

        /// <summary>
        /// Puts the highlight somewhere it is allowed to be, walking on in the given direction.
        ///
        /// HEADINGS ARE NOT ROWS YOU CAN LAND ON, and off the end of a page is the next page --
        /// which together mean a step is not simply "add one". Both are handled here, in a loop,
        /// because they compose: stepping off the bottom onto a page whose first row is a heading
        /// has to keep going, and so does a page that begins with two of them.
        ///
        /// The guard is a backstop for a page that is nothing but headings. It cannot happen with
        /// what Build makes today, and a menu that hangs the game is a bad way to find out that
        /// changed.
        /// </summary>
        private void Settle(int direction)
        {
            for (var guard = 0; guard < 64; guard++)
            {
                if (_row < 0) TurnPage(-1, false);
                else if (_row >= _pages[_page].Items.Count) TurnPage(1, true);

                if (!_pages[_page].Items[_row].IsHeader) return;

                _row += direction;
            }
        }

        /// <summary>Moves to another page, landing on its first or last row.</summary>
        private void TurnPage(int direction, bool top)
        {
            _editing = false;
            _turn = direction > 0 ? 1f : -1f;

            _page = ((_page + direction) % _pages.Count + _pages.Count) % _pages.Count;

            var count = _pages[_page].Items.Count;

            _row = top ? 0 : count - 1;
            _scroll = _row < Rows ? 0 : _row - Rows + 1;
        }

        /// <summary>One bar of a chart, up or down a step.</summary>
        private void Move(Item chart, int direction)
        {
            _touchedAt = Game.GameTime;

            var v = chart.Bar(_bar) + direction * chart.Step;

            if (v < chart.Min) v = chart.Min;
            if (v > chart.Max) v = chart.Max;

            // Snapped to the step, or a slider nudged in hundredths drifts into thousandths.
            v = (float)Math.Round(v / chart.Step) * chart.Step;

            chart.SetBar(_bar, v);
            _changed.Add(chart);
        }

        private void Touch(Item item, int direction)
        {
            _touchedAt = Game.GameTime;

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
            _editing = false;

            if (_changed.Count == 0) return;

            var written = 0;

            foreach (var item in _changed)
            {
                try
                {
                    if (item.IsChart)
                    {
                        // SIX LINES FOR ONE ROW. A chart is one thing on screen and six things in
                        // the file, and each has to go back on its own line under its own name.
                        for (var b = 0; b < item.Bars; b++)
                        {
                            var text = item.Bar(b).ToString(item.Format, CultureInfo.InvariantCulture);

                            if (IniFile.SetValue(Paths.Ini, item.Section, item.Keys[b], text)) written++;
                            else Log.Warn("Could not write [" + item.Section + "] " + item.Keys[b] + " to VehicleTweaks.ini.");
                        }
                    }
                    else if (IniFile.SetValue(Paths.Ini, item.Section, item.Key, item.Written())) written++;
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

        /// <summary>
        /// Moves everything that is between where it is and where it belongs.
        ///
        /// RUN EVERY FRAME, INCLUDING WHILE THE PANEL IS SHUT, because closing is a movement
        /// too. The panel stops taking input the instant it is dismissed and keeps being drawn
        /// until it has finished leaving, which is why IsOpen and _show are two different
        /// things rather than one.
        ///
        /// EXPONENTIAL, NOT A TIMELINE. Each value moves a fraction of the distance still left
        /// every frame, so there is no start time to remember, no end to detect, and an
        /// animation interrupted half way just changes where it is going. Four quick presses of
        /// DOWN are one continuous movement instead of four that cancel each other -- which is
        /// the failure the obvious version has, and it looks like the menu skipping rows.
        ///
        /// The frame time is sanity-checked because it is not always one. A loading screen, an
        /// alt-tab or a hitch hands back a delta of a second or more, and an eased value given
        /// that arrives instantly -- so the whole animation is simply missed on exactly the
        /// frames where the game was busy.
        /// </summary>
        private void Animate()
        {
            var dt = Delta();

            // THE HIGHLIGHT SNAPS, THE PANEL AND THE UNDERLINE DO NOT, and each for its own
            // reason. Opening on a row three pages from the last one would otherwise be the
            // highlight sweeping the length of the panel to get there. The panel itself is
            // MEANT to travel -- that is the animation. And the underline travelling is the
            // whole point of a page change, so it is never snapped at all.
            // IN ROWS, NOT IN ITEMS. A chart is one item six rows tall, so the highlight has
            // to travel by how far down the page a thing is, not by how many things are above it
            // -- and it has to grow to fit what it lands on.
            var want = _rowAt;
            var tall = _rowTall;

            if (_pages.Count > 0)
            {
                var page = _pages[_page];

                if (_row >= 0 && _row < page.Items.Count)
                {
                    want = Offset(page, _row);
                    tall = Tall(page.Items[_row]);
                }
            }

            if (_snap)
            {
                _snap = false;
                _rowAt = want;
                _rowTall = tall;
            }
            else
            {
                _rowAt = Toward(_rowAt, want, RowTau, dt);
                _rowTall = Toward(_rowTall, tall, RowTau, dt);
            }

            _show = Toward(_show, _open ? 1f : 0f, ShowTau, dt);
            _turn = Toward(_turn, 0f, TurnTau, dt);

            if (_show < 0.002f) _show = 0f;
            if (_show > 0.998f) _show = 1f;

            // IT ARRIVES FROM THE LEFT, which is the edge it is pinned to. Coming in from
            // anywhere else would be the panel travelling across the picture rather than out of
            // the side of it, and the eye reads that as something being thrown at it.
            _drawX = PanelX - (1f - _show) * SlideIn;
        }

        private static float Delta()
        {
            try
            {
                var dt = Game.LastFrameTime;

                // A tenth of a second is four frames at twenty-five. Anything longer than that
                // was not a frame, it was the game being somewhere else.
                if (dt <= 0f || dt > 0.1f) return 1f / 60f;

                return dt;
            }
            catch
            {
                return 1f / 60f;
            }
        }

        /// <summary>A step of the way from here to there, at a rate that does not depend on the frame rate.</summary>
        private static float Toward(float now, float want, float tau, float dt)
        {
            if (tau <= 0f) return want;

            var k = 1f - (float)Math.Exp(-dt / tau);
            return now + (want - now) * k;
        }

        /// <summary>The same colour, as present as the panel is.</summary>
        private Color Fade(Color c)
        {
            var a = (int)(c.A * _show * _dip);

            if (a < 0) a = 0;
            if (a > 255) a = 255;

            return Color.FromArgb(a, c.R, c.G, c.B);
        }

        /// <summary>Part of the way from one colour to another.</summary>
        private static Color Mix(Color from, Color to, float by)
        {
            if (by <= 0f) return from;
            if (by >= 1f) return to;

            return Color.FromArgb(
                from.A + (int)((to.A - from.A) * by),
                from.R + (int)((to.R - from.R) * by),
                from.G + (int)((to.G - from.G) * by),
                from.B + (int)((to.B - from.B) * by));
        }

        private void Render()
        {
            var page = _pages[_page];
            var shown = Shown(page);

            var bodyH = Units(page, shown) * RowH;
            var totalH = TitleH + bodyH + FootH;

            var x = _drawX;

            Draw.Bar(x, PanelTop, PanelW, totalH, Fade(Panel));
            Draw.Bar(x, PanelTop, PanelW, TitleH, Fade(Head));
            Draw.Bar(x, PanelTop + TitleH - Hair, PanelW, Hair, Fade(Amber));

            // MEASURED, NOT GUESSED. "Vehicle Tweaks" is a long title for a narrow panel and
            // the width it takes depends on the aspect ratio it is read at -- and the panel got
            // narrower the day it got a size of its own, which is exactly the change that turns
            // a title that just fitted into one that does not.
            Title(x);

            // THE PAGE, NAMED WHERE THERE IS ROOM FOR A NAME. It used to sit in the tab strip
            // beside its icon and squash the other eight; the title bar has a whole line spare to
            // the right of the title, and a name on its own line reads as a heading rather than
            // as a wider tab.
            Draw.Text(page.Title, x + PanelW - PadX, PanelTop + 0.0085f * Zoom, TabText * 1.10f,
                      Fade(Ink), Plain, false, true);

            // THE PAGES, NAMED rather than numbered. "IGNITION 1/3" reads as a value belonging
            // to the row underneath it; all the names with the current one lit says the same
            // thing and needs no explaining.
            Tabs();

            // THE HIGHLIGHT, DRAWN ONCE AND WHEREVER IT HAS GOT TO, rather than on whichever row
            // owns it. Drawing it inside the loop is what ties it to a row, and a thing tied to
            // a row cannot be between two of them.
            _dip = 1f - Math.Abs(_turn) * 0.85f;

            var bx = x + _turn * Slide;

            var at = _rowAt - Offset(page, _scroll);

            if (_show > 0f && at > -1f && at < Units(page, shown) && !page.Items[_row].IsHeader)
            {
                var hy = PanelTop + TitleH + at * RowH;
                var hh = _rowTall * RowH;

                Draw.Bar(bx, hy, PanelW, hh, Fade(Color.FromArgb(38, 245, 196, 60)));
                Draw.Bar(bx, hy, 0.0022f * Zoom, hh, Fade(Amber));

                // AN ASCII CARET, because the pretty one does not exist.
                //
                // This was U+25B6 BLACK RIGHT-POINTING TRIANGLE, chosen on the house rule of
                // text symbols over emoji. GTA's Chalet Comprime has no glyph for it and drew
                // the missing-character box instead -- a small hollow rectangle, which on the
                // selected row of a settings panel reads as a checkbox. It was decoration and it
                // survived being wrong, which is exactly why it was made decoration; but a box
                // that looks like a control is worse than no caret, and ">" is in every font
                // there has ever been.
                Draw.Text(">", bx + 0.0055f * Zoom, hy + 0.0052f * Zoom, RowText * 0.88f,
                          Fade(Amber), Plain);
            }

            // THE BODY MOVES ON A PAGE TURN, AND DIMS WHILE IT DOES. Everything below the tab
            // strip is drawn a little to one side and faded while _turn eases back to nought, so
            // a page change is seen as one page leaving and the next arriving rather than as the
            // words under your eyes being swapped for different words.
            var y = PanelTop + TitleH;

            for (var i = 0; i < shown; i++)
            {
                var index = _scroll + i;
                if (index >= page.Items.Count) break;

                var item = page.Items[index];
                var rowY = y;

                y += Tall(item) * RowH;

                var kind = Of(item);

                if (kind == Kind.Chart)
                {
                    ChartRow(bx, rowY, item, index == _row);
                    continue;
                }

                if (kind == Kind.Header)
                {
                    // A heading sits low in its row with a square bullet and a hairline under
                    // it, so the group it opens reads as hanging off it rather than as another
                    // setting that happens to be in capitals.
                    var sq = 0.0046f * Zoom;

                    Draw.Bar(bx + PadX, rowY + 0.0102f * Zoom, sq / Aspect(), sq, Fade(Amber));
                    Draw.Text(item.Label, bx + PadX + sq / Aspect() + 0.0035f * Zoom,
                              rowY + 0.0082f * Zoom, HeadText, Fade(Amber), Plain);
                    Draw.Bar(bx + PadX, rowY + RowH - 0.0035f * Zoom, PanelW - PadX * 2f,
                             0.0011f * Zoom, Fade(Color.FromArgb(45, 245, 196, 60)));
                    continue;
                }

                var selected = index == _row;

                // Whether this row currently does anything: most hang off a toggle above them,
                // and a deadzone with the indicators switched off is a number that changes
                // nothing. They stay reachable, because you may be about to turn the thing on.
                var live = item.Live == null || item.Live();

                var label = selected ? Ink : Color.FromArgb(200, 205, 205, 208);
                var value = selected ? Amber : Dim;

                if (!live)
                {
                    label = selected ? Dim : Faint;
                    value = Faint;
                }

                // A VALUE LIT FOR A MOMENT AFTER IT CHANGES. On a row whose number moves in
                // hundredths, held down on a D-pad, the only thing that says the press landed is
                // the digit itself -- and a digit going from 0.34 to 0.35 is not a signal at
                // that size. The flash is.
                if (selected) value = Mix(value, Color.FromArgb(value.A, 255, 255, 255), Flash());

                var ty = rowY + 0.0044f * Zoom;

                Draw.Text(item.Label, bx + LabelX, ty, RowText, Fade(label), Plain);

                var right = bx + PanelW - ValueX;

                // EACH KIND OF ROW DRAWN AS THE KIND OF THING IT IS. A row of text on the right
                // said ON or 0.35 s and left you to know what that meant; a switch is drawn as a
                // switch, a number on a range as a slider, a choice with the arrows that change
                // it, and a key as a keycap. The text stays, because the text is exact and the
                // picture is quick, and a settings row wants both.
                switch (kind)
                {
                    case Kind.Toggle: Pill(item, right, rowY, ty, live, value); break;
                    case Kind.Number: Track(item, right, rowY, ty, live, value); break;
                    case Kind.Choice: Chevrons(item, right, ty, selected, value); break;
                    case Kind.Bind: Keycap(item.Show(), right, rowY, ty, value, false); break;
                    case Kind.Go: Keycap("OPEN", right, rowY, ty, value, true); break;

                    default:
                        Draw.Text(item.Show(), right, ty, RowText, Fade(value), Plain, false, true);
                        break;
                }
            }

            _dip = 1f;

            // The scroll bar, only when there is something to scroll.
            if (Total(page) > Rows)
            {
                var track = bodyH;
                var thumb = track * Rows / Total(page);
                var down = track * Offset(page, _scroll) / Total(page);

                Draw.Bar(x + PanelW - 0.0018f, PanelTop + TitleH, 0.0018f, track,
                         Fade(Color.FromArgb(60, 255, 255, 255)));
                Draw.Bar(x + PanelW - 0.0018f, PanelTop + TitleH + down, 0.0018f, thumb, Fade(Amber));
            }

            var foot = PanelTop + TitleH + bodyH;

            Draw.Bar(x, foot, PanelW, Hair, Fade(Color.FromArgb(70, 255, 255, 255)));

            // The hint for the selected row, cut to the panel rather than run out across the
            // game. Falls back to the controls when a row has nothing to say for itself.
            var pad = Pad.InUse();

            var hint = _capturing
                ? (pad
                       // A pad has no key to give. Said plainly on the row rather than left for
                       // the player to work out from a panel that has stopped responding.
                       ? "This needs a keyboard.  B cancels."
                       : "Press the key you want the panel on.  ESC cancels.")
                : _editing
                    ? (pad ? "LEFT RIGHT pick the gear   UP DOWN move it   A done"
                           : "LEFT RIGHT pick the gear   UP DOWN move it   ENTER done")
                    : page.Items[_row].Hint;

            if (string.IsNullOrEmpty(hint)) hint = pad ? "D-PAD moves and changes" : "ARROWS change    TAB page";

            Draw.Text(Draw.Ellipsis(hint, HintText, PanelW - PadX * 2f, Plain),
                      x + PadX, foot + 0.008f * Zoom, HintText, Fade(Dim), Plain);

            // THE CONTROLS IT IS ACTUALLY BEING DRIVEN WITH. A footer that says TAB and
            // BACKSPACE to somebody holding a pad is worse than no footer: they are the two
            // instructions on screen and neither of them can be followed.
            Draw.Text(pad
                          ? (_padNext != null
                                 ? "LB RB page   D-PAD move & change   A works a row   B saves & closes"
                                 : "D-PAD move & change   A works a row   B saves & closes")
                          : "TAB page   ARROWS change   " + Binding() + " or BACKSPACE saves",
                      x + PadX, foot + 0.026f * Zoom, FootText, Fade(Faint), Plain);
        }

        /// <summary>
        /// What sort of row this is, for the ones whose factory did not say.
        ///
        /// The switch, number and choice factories mark their rows; a heading and a chart carry
        /// flags from before rows had kinds; and a door is the one row with something to press
        /// and nowhere in the ini to write. What is left is a plain value.
        /// </summary>
        private static Kind Of(Item item)
        {
            if (item.IsHeader) return Kind.Header;
            if (item.IsChart) return Kind.Chart;
            if (item.Kind != Kind.Setting) return item.Kind;
            if (item.Section == null && item.Press != null) return Kind.Go;

            return Kind.Setting;
        }

        /// <summary>
        /// A switch: a pill with a knob that slides to the side it is on.
        ///
        /// THE KNOB TRAVELS rather than jumping, eased on the row itself so every switch keeps
        /// its own place mid-slide. Amber is on. The word beside it stays, because a knob on the
        /// right of a pill is "on" in some countries and "off" in others and the word is exact.
        /// </summary>
        private void Pill(Item item, float right, float rowY, float ty, bool live, Color value)
        {
            var on = false;

            try { on = item.On != null && item.On(); }
            catch { /* drawn as off */ }

            item.Anim = Toward(item.Anim, on ? 1f : 0f, KnobTau, Delta());

            var px = right - ValueCol - PillW;
            var py = rowY + (RowH - PillH) * 0.5f;

            var track = on ? Color.FromArgb(150, 245, 196, 60) : Color.FromArgb(60, 255, 255, 255);
            if (!live) track = Color.FromArgb(track.A / 2, track.R, track.G, track.B);

            Draw.Bar(px, py, PillW, PillH, Fade(track));

            var pad = PillH * 0.16f;
            var kh = PillH - pad * 2f;
            var kw = kh / Aspect();
            var kx = px + pad / Aspect() + (PillW - kw - pad * 2f / Aspect()) * item.Anim;

            Draw.Bar(kx, py + pad, kw, kh, Fade(live ? (on ? Ink : Dim) : Faint));

            Draw.Text(item.Show(), right, ty, RowText, Fade(value), Plain, false, true);
        }

        /// <summary>
        /// A number on a range: a track filled to where the value sits, and the value beside it.
        ///
        /// "0.35 s" says how long; the track says how long OUT OF HOW LONG IT COULD BE, which is
        /// the thing you actually want to know when you are deciding whether to nudge it. The
        /// fill eases, so a held D-pad reads as the bar sliding rather than stepping.
        /// </summary>
        private void Track(Item item, float right, float rowY, float ty, bool live, Color value)
        {
            var part = 0f;

            try
            {
                var span = item.Max - item.Min;
                if (item.Value != null && span > 0f) part = (item.Value() - item.Min) / span;
            }
            catch { /* drawn empty */ }

            if (part < 0f) part = 0f;
            if (part > 1f) part = 1f;

            item.Anim = Toward(item.Anim, part, FillTau, Delta());

            var tx = right - ValueCol - TrackW;
            var tyy = rowY + (RowH - TrackH) * 0.5f;

            Draw.Bar(tx, tyy, TrackW, TrackH, Fade(Color.FromArgb(live ? 45 : 25, 255, 255, 255)));
            Draw.Bar(tx, tyy, TrackW * item.Anim, TrackH, Fade(live ? value : Faint));

            var tick = 0.0014f * Zoom;

            Draw.Bar(tx + TrackW * item.Anim - tick * 0.5f, tyy - TrackH * 0.9f, tick, TrackH * 2.8f,
                     Fade(live ? Ink : Faint));

            Draw.Text(item.Show(), right, ty, RowText, Fade(value), Plain, false, true);
        }

        /// <summary>
        /// A choice from a list: the value, with the arrows that step through it either side.
        ///
        /// A row that cycles looks exactly like a row that does not until you press LEFT, so the
        /// arrows say so in advance -- faint on every such row, lit on the one you are on.
        /// </summary>
        private void Chevrons(Item item, float right, float ty, bool selected, Color value)
        {
            var arrow = selected ? Amber : Faint;

            Draw.Text("<", right - ValueCol, ty, RowText, Fade(arrow), Plain, false, true);
            Draw.Text(item.Show(), right, ty, RowText, Fade(value), Plain, false, true);
            Draw.Text(">", right + 0.0012f * Zoom, ty, RowText, Fade(arrow), Plain);
        }

        /// <summary>
        /// A key, drawn as a key: the name inside a small cap.
        ///
        /// Also used for the one row that opens something rather than holding a value, lit
        /// amber, so a door reads as a button and not as a setting whose value is "OPEN".
        /// </summary>
        private void Keycap(string text, float right, float rowY, float ty, Color value, bool door)
        {
            var w = Draw.Width(text, RowText, Plain);
            var pad = 0.0042f * Zoom;
            var h = RowH * 0.80f;
            var top = rowY + (RowH - h) * 0.5f;

            var edge = door ? Color.FromArgb(160, 245, 196, 60) : Color.FromArgb(70, 255, 255, 255);
            var face = door ? Color.FromArgb(70, 245, 196, 60) : Color.FromArgb(120, 0, 0, 0);

            Draw.Bar(right - w - pad * 2f, top, w + pad * 2f, h, Fade(edge));
            Draw.Bar(right - w - pad * 2f + Hair / 2f, top + Hair / 2f, w + pad * 2f - Hair, h - Hair,
                     Fade(face));

            Draw.Text(text, right - pad, ty, RowText, Fade(door ? Ink : value), Plain, false, true);
        }

        /// <summary>
        /// The title, drawn as the picture when there is one and as text when there is not.
        ///
        /// A TOUCH ABOVE THE BAR'S TOP EDGE, on purpose. Text sat inside the bar because text has
        /// to; a blackletter word is a badge, and a badge that clears the edge it is pinned to by
        /// a hair reads as pinned to it rather than printed on it. Its colour goes through Fade,
        /// so it arrives and leaves with the panel the same as everything else does.
        /// </summary>
        private void Title(float x)
        {
            var h = 0.032f * Zoom;
            var top = PanelTop - 0.0060f * Zoom;

            if (_title.Draw(x + PadX, top, h, Fade(Amber))) return;

            var titleScale = Draw.FitScale("VEHICLE TWEAKS", TitleText, PanelW * 0.50f, Plain);

            Draw.Text("VEHICLE TWEAKS", x + PadX, PanelTop + 0.005f * Zoom, titleScale,
                      Fade(Amber), Plain);
        }

        /// <summary>How lit a just-changed value should be, one down to nought.</summary>
        private float Flash()
        {
            if (_touchedAt == 0) return 0f;

            var since = Game.GameTime - _touchedAt;
            if (since < 0 || since >= FlashMs) return 0f;

            return 1f - (float)since / FlashMs;
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
        /// The pages across the head of the panel, as icons, evenly spaced.
        ///
        /// ICONS ONLY, AND THE NAME MOVED UPSTAIRS. The first version named the current page
        /// beside its icon, and nine icons plus a name plus the gaps between them did not fit a
        /// strip this wide -- it read as squashed because it was. Worse, the icons were drawn a
        /// shade too tall and ran into the rule under the title bar. The name now sits in the
        /// title bar itself, right of "VEHICLE TWEAKS", where there is a whole line of room; the
        /// strip is nine things of one size spread evenly across it, which is the one layout of
        /// nine things that cannot be squashed; and the bar got taller so the icons have air.
        ///
        /// The underline under the current icon still travels.
        /// </summary>
        private void Tabs()
        {
            var cell = 0.0028f * Zoom;
            var cellAcross = cell / Aspect();
            var iconW = 7f * cellAcross;

            var left = _drawX + PadX;
            var right = _drawX + PanelW - PadX;
            var y = PanelTop + 0.0300f * Zoom;

            var n = _pages.Count;
            var gap = n > 1 ? (right - left - n * iconW) / (n - 1) : 0f;
            if (gap < 0f) gap = 0f;

            var x = left;
            var wantX = left;

            for (var i = 0; i < n; i++)
            {
                var on = i == _page;

                if (on) wantX = x;

                var tint = Fade(on ? Amber : Faint);
                var art = _pages[i].Art;

                if (art == null || !art.DrawBox(x, y, iconW, 7f * cell, tint))
                {
                    Draw.Icon(_pages[i].Icon, x, y, cell, cellAcross, tint);
                }

                x += iconW + gap;
            }

            // MEASURED HERE AND EASED HERE, because the positions only exist inside this method.
            var dt = Delta();

            if (_tabWide <= 0f)
            {
                _tabAt = wantX;
                _tabWide = iconW;
            }
            else
            {
                _tabAt = Toward(_tabAt, wantX, TabTau, dt);
                _tabWide = Toward(_tabWide, iconW, TabTau, dt);
            }

            Draw.Bar(_tabAt, y + 7f * cell + 0.0024f * Zoom, _tabWide, Hair, Fade(Amber));
        }

        /// <summary>
        /// One chart: a bar per gear, the number over each, the gear under each.
        ///
        /// A ROW SIX ROWS TALL, rather than six rows. Six separate "2nd gear torque" rows would
        /// hold the same numbers and say nothing about the SHAPE of them, and the shape is the
        /// point -- more in second than first, tailing off to fifth is a curve you can see and
        /// cannot read off a column of decimals.
        ///
        /// THE LINE AT ONE. On a chart whose range straddles the standard value, a bar on its own
        /// says how tall it is and nothing about whether that is more or less than the car came
        /// with. The faint line is "as it came", and every bar is read against it.
        /// </summary>
        private void ChartRow(float x, float y, Item item, bool selected)
        {
            var h = ChartRows * RowH;

            var left = x + LabelX;
            var right = x + PanelW - ValueX;
            var top = y + 0.020f * Zoom;
            var bottom = y + h - 0.013f * Zoom;

            Draw.Text(item.Label, x + PadX, y + 0.003f * Zoom, HeadText,
                      Fade(selected ? Ink : Dim), Plain);

            var slot = (right - left) / item.Bars;
            var barW = slot * 0.52f;
            var span = item.Max - item.Min;

            // "As it came", when the range has it. A chart of nought to one has no such line
            // because nought IS as it came, and that is the floor.
            if (item.Min < 1f && item.Max > 1f)
            {
                var one = bottom - (bottom - top) * ((1f - item.Min) / span);
                Draw.Bar(left, one, right - left, 0.0009f * Zoom, Fade(Color.FromArgb(70, 255, 255, 255)));
            }

            for (var i = 0; i < item.Bars; i++)
            {
                var v = item.Bar(i);
                var part = span > 0f ? (v - item.Min) / span : 0f;

                if (part < 0f) part = 0f;
                if (part > 1f) part = 1f;

                var bx = left + i * slot + (slot - barW) * 0.5f;
                var bh = (bottom - top) * part;
                var mine = selected && _editing && i == _bar;

                var lit = mine ? Amber
                              : selected ? Color.FromArgb(200, 245, 196, 60)
                                         : Color.FromArgb(150, 150, 150, 156);

                if (mine) lit = Mix(lit, Color.FromArgb(255, 255, 255, 255), Flash());

                // THE PROBE. The bars go through the same DRAW_RECT as every switch and slider on
                // the other pages, which draw; on this page they did not, and nothing about the
                // maths says why. So the numbers the first bar is asked to be are written down
                // once, and the answer comes from a log rather than from staring at a screenshot.
                if (i == 0 && _chartAt == 0) _chartAt = Game.GameTime;

                // AND AGAIN TWO SECONDS IN. The first reading lands on the frame after the page
                // turn, when the body is deliberately stepped aside and dimmed, so it describes
                // the animation rather than the chart. The second is the steady state.
                if (i == 0 && Game.GameTime - _chartAt > 2000)
                {
                    Log.Once("chart-probe-2", "Chart at 2s: part " + part.ToString("0.00") +
                             " litA " + lit.A + " show " + _show.ToString("0.00") +
                             " dip " + _dip.ToString("0.00") + " turn " + _turn.ToString("0.00") +
                             " rowTall " + _rowTall.ToString("0.0") + " bh " +
                             bh.ToString("0.0000") + ".");
                }

                if (i == 0)
                {
                    Log.Once("chart-probe", "Chart: top " + top.ToString("0.0000") +
                             " bottom " + bottom.ToString("0.0000") + " barW " + barW.ToString("0.0000") +
                             " part " + part.ToString("0.00") + " litA " + lit.A +
                             " show " + _show.ToString("0.00") + " dip " + _dip.ToString("0.00") +
                             " turn " + _turn.ToString("0.00") + " rowTall " + _rowTall.ToString("0.0") + ".");
                }

                Draw.Bar(bx, top, barW, bottom - top, Fade(Color.FromArgb(22, 255, 255, 255)));
                Draw.Bar(bx, bottom - bh, barW, bh, Fade(lit));

                Draw.Text(v.ToString(item.Format, CultureInfo.InvariantCulture),
                          bx + barW * 0.5f, top - 0.0135f * Zoom, 0.20f * Zoom,
                          Fade(mine ? Ink : Faint), Plain, true);

                Draw.Text((i + 1).ToString(), bx + barW * 0.5f, bottom + 0.0012f * Zoom,
                          0.22f * Zoom, Fade(mine ? Amber : Dim), Plain, true);

                if (mine) Draw.Bar(bx, bottom + 0.0115f * Zoom, barW, 0.0012f * Zoom, Fade(Amber));
            }
        }

        // ---- how tall things are, in rows --------------------------------------

        private const int ChartRows = 6;

        /// <summary>When a chart was first drawn, for the steady-state probe.</summary>
        private int _chartAt;

        private static int Tall(Item item)
        {
            return item.IsChart ? ChartRows : 1;
        }

        /// <summary>Rows above this item, counting a chart as the rows it takes.</summary>
        private static int Offset(Page page, int index)
        {
            var units = 0;

            for (var i = 0; i < index && i < page.Items.Count; i++) units += Tall(page.Items[i]);

            return units;
        }

        private static int Total(Page page)
        {
            return Offset(page, page.Items.Count);
        }

        /// <summary>How many items from the scroll point fit in the rows there are.</summary>
        private int Shown(Page page)
        {
            var units = 0;
            var count = 0;

            for (var i = _scroll; i < page.Items.Count; i++)
            {
                var tall = Tall(page.Items[i]);
                if (units + tall > Rows) break;

                units += tall;
                count++;
            }

            return count;
        }

        private int Units(Page page, int shown)
        {
            var units = 0;

            for (var i = 0; i < shown && _scroll + i < page.Items.Count; i++)
            {
                units += Tall(page.Items[_scroll + i]);
            }

            return units;
        }

        private static float Aspect()
        {
            try
            {
                var aspect = GTA.UI.Screen.AspectRatio;
                return aspect < 0.5f || aspect > 6f ? 16f / 9f : aspect;
            }
            catch
            {
                return 16f / 9f;
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
