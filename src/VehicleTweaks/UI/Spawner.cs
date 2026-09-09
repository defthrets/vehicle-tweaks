using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using VehicleTweaks.Core;
using VehicleTweaks.Input;

// Both namespaces have a Control and only one of them is a game control.
using Control = GTA.Control;

namespace VehicleTweaks.UI
{
    /// <summary>
    /// Every vehicle in the game, browsable, with a picture of the one it is pointing at.
    ///
    /// THE LIST IS SHIPPED, AND CHECKED AGAINST THE GAME. 921 model names covering the base game
    /// and every DLC and multiplayer pack up to the 2025 ones -- see Models, and see there for why
    /// it is not SHVDN's enumeration, which stops eighty vehicles short. Each is checked against
    /// IsInCdImage before it is offered, which is the game saying whether that model is actually
    /// installed here, so the list may run ahead of a Legacy install without the menu lying about
    /// it. A menu that offers a car it cannot
    /// spawn is worse than a shorter menu.
    ///
    /// THE PICTURE IS THE GAME'S OWN. GTA ships a streamed texture per model for the vehicle
    /// websites you buy cars from in game, named after the model, so there is no image to render
    /// and none to fake -- it is asked for by name and drawn on the card.
    ///
    /// IT USED TO SPAWN THE CAR IN FRONT OF YOU INSTEAD, on the argument that a script cannot
    /// render a model to a picture so the real thing is the honest preview. That was true and it
    /// was still the wrong job for it: as the ONLY preview it littered the street, loaded a model
    /// for every row, and put whatever you were pointing at between you and the menu.
    ///
    /// SO IT IS A SETTING NOW RATHER THAN GONE. Most cars have no picture -- the websites only
    /// ever sold a fraction of them and add-on cars bring none -- and for those the choice is the
    /// real thing or nothing at all. It is also the only way to judge the SIZE of something, which
    /// a photograph flattens out. Off by default; on when you want it.
    ///
    /// THE STAT BARS ARE RELATIVE TO THE FASTEST THING IN THE GAME, worked out from the catalogue
    /// as it is built rather than against numbers I would have had to invent. The game hands back
    /// a top speed in metres a second and an acceleration in units nobody has ever explained; what
    /// a person wants to know is whether this one is quick, and quick only means anything next to
    /// something else.
    /// </summary>
    internal sealed class Spawner
    {
        /// <summary>How many models to examine per frame while the catalogue is being built.</summary>
        private const int PerFrame = 60;

        /// <summary>How many rows of the list are on screen at once.</summary>
        private const int Rows = 16;

        private const int Plain = 4;

        private static readonly Color Ink = Color.FromArgb(235, 228, 228, 231);
        private static readonly Color Dim = Color.FromArgb(170, 150, 150, 156);
        private static readonly Color Faint = Color.FromArgb(120, 150, 150, 156);
        private static readonly Color Amber = Color.FromArgb(255, 245, 196, 60);
        private static readonly Color Panel = Color.FromArgb(236, 15, 15, 18);
        private static readonly Color Head = Color.FromArgb(243, 26, 26, 31);

        // Where it sits, in the same height-fraction geometry the rest of the UI uses.
        private const float X = 0.030f;
        private const float Y = 0.140f;
        private const float ListW = 0.210f;
        private const float StatW = 0.190f;
        private const float TitleH = 0.040f;
        private const float RowH = 0.0225f;
        private const float FootH = 0.030f;

        private sealed class Entry
        {
            public VehicleHash Hash;
            public string Name;
            public string Code;
            public float Speed;
            public float Accel;
            public float Brake;
            public float Grip;
            public int Seats;
            public int Price;

            /// <summary>The maker, lower-case, which is how mpcarhud names its badges.</summary>
            public string Make;

            /// <summary>The transport icon for its class, which everything has.</summary>
            public string Icon;
        }

        private readonly Settings _cfg;

        /// <summary>Every installed vehicle, in the order the game lists its classes.</summary>
        private readonly List<List<Entry>> _classes = new List<List<Entry>>();

        private readonly float[] _best = new float[4];

        private string[] _all;

        /// <summary>Hashes already catalogued, so a name the list happens to repeat is listed once.</summary>
        private readonly HashSet<uint> _seen = new HashSet<uint>();
        private int _built;

        private bool _open;
        private int _class;
        private int _row;
        private int _scroll;


        /// <summary>When the highlight last moved, which is the only thing the picture waits on.</summary>
        private int _movedAt;

        /// <summary>How long the highlight has to be still before a picture is loaded for it.</summary>
        private const int PictureSettleMs = 120;

        /// <summary>One picture per model, made once and kept -- see Picture.</summary>
        private readonly Dictionary<string, Sprite> _photos = new Dictionary<string, Sprite>();

        /// <summary>How many of the catalogue have a picture beside the log, for the log.</summary>
        private int _pictured;

        /// <summary>How long to wait for each try to load before looking past it.</summary>
        private const int WaitMs = 500;

        /// <summary>The card being pictured, what was asked for on its behalf, and which one answered.</summary>
        private Entry _want;
        private string[][] _tries = new string[0][];
        private int _hit = -1;
        private int _since;
        private Vector3 _size;

        /// <summary>Said once per kind of picture, so the log answers which kinds this build has.</summary>
        private readonly bool[] _said = new bool[3];

        private bool _keyDown;

        private readonly Button _up = new Button(Keys.Up, Control.PhoneUp, true);
        private readonly Button _down = new Button(Keys.Down, Control.PhoneDown, true);
        private readonly Button _left = new Button(Keys.Left, Control.PhoneLeft, true);
        private readonly Button _right = new Button(Keys.Right, Control.PhoneRight, true);
        private readonly Button _accept = new Button(Keys.Return, Control.PhoneSelect, false);
        private readonly Button _back = new Button(Keys.Back, Control.PhoneCancel, false);

        public bool IsOpen => _open;

        public Spawner(Settings cfg)
        {
            _cfg = cfg;
        }

        /// <summary>Opened from the settings panel, which is the only way a pad can reach it.</summary>
        public void Open()
        {
            _open = true;
            _movedAt = Game.GameTime;

            // THE SAME TRAP AS THE PANEL'S, arriving through the same door. This is opened from a
            // row on the panel, so A or ENTER is held at the moment it appears -- and A is this
            // menu's spawn button. Without disarming, opening the spawner would spawn whatever
            // the highlight happened to start on.
            _up.Disarm();
            _down.Disarm();
            _left.Disarm();
            _right.Disarm();
            _accept.Disarm();
            _back.Disarm();
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.Spawner)
                {
                    if (_open) Close();
                    return;
                }

                // NOT BOUND IS NOT A KEY, AND IT IS ASKED BEFORE IT IS READ. The spawner's key
                // defaults to None now that Weapon Tweaks has F7, and Keys.None is nought --
                // what Game.IsKeyPressed does with a virtual key code of nought is not documented
                // and not something to find out sixty times a second. The panel row is the door.
                if (_cfg.SpawnerKey != Keys.None && Edge(_cfg.SpawnerKey, ref _keyDown))
                {
                    if (_open) Close();
                    else Open();
                }

                if (!_open) return;

                Build();

                _up.Poll();
                _down.Poll();
                _left.Poll();
                _right.Poll();
                _accept.Poll();
                _back.Poll();

                Deafen();

                if (_built >= (_all == null ? 1 : _all.Length))
                {
                    Navigate(me);
                }

                Render();
            }
            catch (Exception ex)
            {
                Close();
                Log.Once("spawner", "The spawner fell over: " + ex.Message);
            }
        }

        // ==================================================================
        // The catalogue
        // ==================================================================

        /// <summary>
        /// Walks the game's vehicle list, a slice at a time.
        ///
        /// SPREAD ACROSS FRAMES, because eight hundred models asked three questions each is a
        /// couple of thousand native calls, and doing that in one tick is a visible stutter at the
        /// exact moment somebody has just opened a menu. Sixty a frame finishes in about a quarter
        /// of a second and nobody sees it happen.
        ///
        /// IS_IN_CD_IMAGE IS THE GATE. The enumeration is what SHVDN knows about; that property is
        /// what this installation actually has. Offering a car that cannot be spawned would be a
        /// menu that lies, and the difference costs one property read.
        /// </summary>
        private void Build()
        {
            if (_all == null)
            {
                // THE SHIPPED LIST, AND THEN WHATEVER THIS MACHINE KNOWS THAT IT DOES NOT --
                // another mod's cache, the dlcpacks folder names, and the player's own list. All
                // of it goes through the same IsInCdImage below, so a bad guess is a lookup that
                // fails and nothing worse. See Core\Models.cs.
                var found = Models.Found(Paths.Game, Paths.ModelsFile);

                if (found.Length == 0)
                {
                    _all = Models.All;
                }
                else
                {
                    var every = new List<string>(Models.All);
                    every.AddRange(found);
                    _all = every.ToArray();

                    Log.Info("Spawner: " + Models.All.Length + " models shipped, " + found.Length +
                             " more found on this machine to try.");
                }

                for (var i = 0; i < 23; i++) _classes.Add(new List<Entry>());
            }

            if (_built >= _all.Length) return;

            var end = Math.Min(_built + PerFrame, _all.Length);

            for (; _built < end; _built++)
            {
                var name = _all[_built];

                try
                {
                    // MODEL DOES THE HASHING, with the game's own joaat, so a name out of the
                    // shipped list needs nothing from the enumeration to become a vehicle.
                    var model = new Model(name);

                    if (!model.IsValid || !model.IsInCdImage || !model.IsVehicle) continue;

                    var hash = (VehicleHash)model.Hash;

                    if (!_seen.Add((uint)model.Hash)) continue;

                    var group = Function.Call<int>(Hash.GET_VEHICLE_CLASS_FROM_NAME, (uint)hash);
                    if (group < 0 || group >= _classes.Count) continue;

                    var entry = new Entry
                    {
                        Hash = hash,
                        Code = name,
                        Name = Label(hash),
                        Speed = Stat(Hash.GET_VEHICLE_MODEL_ESTIMATED_MAX_SPEED, hash),
                        Accel = Stat(Hash.GET_VEHICLE_MODEL_ACCELERATION, hash),
                        Brake = Stat(Hash.GET_VEHICLE_MODEL_MAX_BRAKING, hash),
                        Grip = Stat(Hash.GET_VEHICLE_MODEL_MAX_TRACTION, hash),
                        Seats = Function.Call<int>(Hash.GET_VEHICLE_MODEL_NUMBER_OF_SEATS, (uint)hash),
                        Price = Function.Call<int>(Hash.GET_VEHICLE_MODEL_VALUE, (uint)hash),
                        Make = MakeOf(hash),
                        Icon = IconOf(group),
                    };

                    if (entry.Speed > _best[0]) _best[0] = entry.Speed;
                    if (entry.Accel > _best[1]) _best[1] = entry.Accel;
                    if (entry.Brake > _best[2]) _best[2] = entry.Brake;
                    if (entry.Grip > _best[3]) _best[3] = entry.Grip;

                    _classes[group].Add(entry);

                    if (Photo(entry).Exists) _pictured++;
                }
                catch
                {
                    // One model that will not answer is not a reason to stop cataloguing.
                }
            }

            if (_built < _all.Length) return;

            foreach (var list in _classes)
            {
                // BEFORE THE SORT, because the mark is part of the name it is sorted by.
                Distinguish(list);

                list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            }

            var total = 0;
            foreach (var list in _classes) total += list.Count;

            Log.Info("Spawner: " + total + " of " + _all.Length + " names are installed here, " +
                     _pictured + " with a picture beside the log.");

            Settle();
        }

        /// <summary>
        /// Tells apart the rows that would otherwise read exactly the same.
        ///
        /// SEVENTY-TWO NAMES ARE SHARED BY A HUNDRED AND EIGHTY-NINE MODELS. The game gives every
        /// variant of a vehicle ONE display name: two rows called "Sentinel XS", three called
        /// "Bison", four called "Boxville", ten called "Freight Train" -- and no way to tell which
        /// was which without spawning one and looking at it. That is a list with a hole in it in
        /// the same way the missing DLC was.
        ///
        /// THE MODEL NAME ALREADY CARRIES THE ANSWER, so it is read rather than invented: a drift
        /// tune says drift at the front, and a variant says which one it is with the number on the
        /// end. Only the rows that actually clash are marked, so "Boxville" stays "Boxville" and
        /// its three variants become (2), (3) and (4).
        ///
        /// AND THEN THE CODE ITSELF for whatever is STILL doubled -- freight2 and freightcar2 are
        /// both "the second of their name", and there the ugly answer is the right one, because a
        /// row you cannot tell from its neighbour is worse than a row with a model name in it. The
        /// second pass rebuilds from the plain name rather than stacking a mark on a mark.
        /// </summary>
        private static void Distinguish(List<Entry> list)
        {
            var plain = new string[list.Count];

            for (var i = 0; i < list.Count; i++) plain[i] = list[i].Name;

            Mark(list, plain, true);
            Mark(list, plain, false);
        }

        private static void Mark(List<Entry> list, string[] plain, bool variant)
        {
            var count = new Dictionary<string, int>();

            foreach (var entry in list)
            {
                int n;
                count.TryGetValue(entry.Name, out n);
                count[entry.Name] = n + 1;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (count[list[i].Name] < 2) continue;

                var mark = variant ? Variant(list[i].Code) : list[i].Code;

                if (string.IsNullOrEmpty(mark)) continue;

                list[i].Name = plain[i] + " (" + mark + ")";
            }
        }

        /// <summary>Which variant a model name says it is, or nothing when it does not say.</summary>
        private static string Variant(string code)
        {
            if (code.StartsWith("drift", StringComparison.Ordinal)) return "Drift";

            var i = code.Length;

            while (i > 0 && code[i - 1] >= '0' && code[i - 1] <= '9') i--;

            // A name that is ALL digits, or has none on the end, says nothing about which it is.
            return i == 0 || i == code.Length ? null : code.Substring(i);
        }

        /// <summary>
        /// The name a person would call it, falling back to the name the game files call it.
        ///
        /// The display name is a LABEL, not a word -- "SULTAN" is a key into the language files --
        /// so it is looked up. When the lookup gives nothing back the label itself is shown, which
        /// is still more use than a blank row.
        /// </summary>
        private static string Label(VehicleHash hash)
        {
            try
            {
                var label = Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, (uint)hash);

                if (string.IsNullOrEmpty(label)) return hash.ToString();

                var name = Game.GetLocalizedString(label);

                return string.IsNullOrEmpty(name) || name == "NULL" ? label : name;
            }
            catch
            {
                return hash.ToString();
            }
        }

        /// <summary>The maker's name as mpcarhud spells it, or nothing for a car with no maker.</summary>
        private static string MakeOf(VehicleHash hash)
        {
            try
            {
                var make = Function.Call<string>(Hash.GET_MAKE_NAME_FROM_VEHICLE_MODEL, (uint)hash);
                return string.IsNullOrEmpty(make) ? null : make.ToLowerInvariant();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The transport icon mpcarhud keeps for this sort of thing.</summary>
        private static string IconOf(int group)
        {
            switch ((VehicleClass)group)
            {
                case VehicleClass.Motorcycles: return "transport_bike_icon";
                case VehicleClass.Cycles: return "transport_bicycle_icon";
                case VehicleClass.Boats: return "transport_boat_icon";
                case VehicleClass.Helicopters: return "transport_heli_icon";
                case VehicleClass.Planes: return "transport_plane_icon";
                default: return "transport_car_icon";
            }
        }

        private static float Stat(Hash which, VehicleHash hash)
        {
            try { return Function.Call<float>(which, (uint)hash); }
            catch { return 0f; }
        }

        // ==================================================================
        // Moving about
        // ==================================================================

        private List<Entry> Current
        {
            get { return _classes.Count == 0 ? new List<Entry>() : _classes[_class]; }
        }

        private void Navigate(Ped me)
        {
            var list = Current;

            if (_left.Fired) { Turn(-1); return; }
            if (_right.Fired) { Turn(1); return; }

            if (list.Count > 0)
            {
                if (_up.Fired) { _row--; _movedAt = Game.GameTime; }
                if (_down.Fired) { _row++; _movedAt = Game.GameTime; }

                if (_row < 0) _row = list.Count - 1;
                if (_row >= list.Count) _row = 0;

                if (_row < _scroll) _scroll = _row;
                if (_row >= _scroll + Rows) _scroll = _row - Rows + 1;
            }

            if (_accept.Fired) Spawn(me);
            if (_back.Fired) Close();
        }

        /// <summary>To the next class that has anything in it. Several of them do not.</summary>
        private void Turn(int direction)
        {
            for (var guard = 0; guard < _classes.Count; guard++)
            {
                _class = (_class + direction + _classes.Count) % _classes.Count;

                if (_classes[_class].Count == 0) continue;

                _row = 0;
                _scroll = 0;
                _movedAt = Game.GameTime;
                return;
            }
        }

        private void Settle()
        {
            if (Current.Count == 0) Turn(1);
        }

        /// <summary>
        /// Puts the chosen vehicle on the ground beside the player, once, when it is asked for.
        ///
        /// THIS USED TO HAPPEN AS YOU SCROLLED, and it was the wrong idea dressed up as a clever
        /// one. "The preview IS the car" sounded good and meant that browsing the list littered
        /// the street, loaded a model per row, and put whatever you were pointing at between you
        /// and the menu. It came back once as a live view stood beside the menu, on the argument
        /// that most cars had no picture; now that every model in the game HAS one, that argument
        /// is gone and so is the live view. A picture is what a person means by a preview.
        ///
        /// THE MODEL IS LOADED HERE, NOT EARLIER, so nothing is streamed for the eight hundred
        /// cars you scrolled past on the way. It is marked as no longer needed the moment the car
        /// exists -- the game keeps it as long as the vehicle does, and a model this script never
        /// released is memory nothing will ever give back.
        /// </summary>
        private void Spawn(Ped me)
        {
            var list = Current;

            if (list.Count == 0 || _row < 0 || _row >= list.Count) return;

            var want = list[_row];

            var model = new Model(want.Hash);

            try
            {
                // A SECOND, WHICH IS A LONG TIME FOR A MODEL AND NOT LONG FOR A PERSON. Waiting
                // forever on a model that will never load would hang the game on a menu press.
                model.Request(1000);

                if (!model.IsLoaded)
                {
                    Log.Warn("Spawner: " + want.Name + " (" + want.Code + ") would not load.");
                    return;
                }

                var where = me.Position + me.ForwardVector * 5.5f + Vector3.WorldUp * 0.5f;
                var car = World.CreateVehicle(model, where, me.Heading + 90f);

                if (car == null)
                {
                    // SAID OUT LOUD, because a car that never arrives is otherwise identical to
                    // one that arrived somewhere you were not looking.
                    Log.Warn("Spawner: " + want.Name + " (" + want.Code + ") loaded but would " +
                             "not spawn -- there may be no room where you are stood.");
                    return;
                }

                car.PlaceOnGround();

                Log.Info("Spawner: spawned " + want.Name + " (" + want.Code + ").");
            }
            catch (Exception ex)
            {
                Log.Once("spawn", "Spawning fell over: " + ex.Message);
            }
            finally
            {
                model.MarkAsNoLongerNeeded();
                Close();
            }
        }

        public void Close()
        {
            _open = false;

            Forget();
        }

        // ==================================================================
        // Drawing it
        // ==================================================================

        private void Render()
        {
            var list = Current;
            var shown = Math.Min(Rows, list.Count);

            var bodyH = Math.Max(shown, 1) * RowH;
            var totalH = TitleH + bodyH + FootH;

            var statX = X + ListW + 0.004f;

            Draw.Bar(X, Y, ListW, totalH, Panel);
            Draw.Bar(X, Y, ListW, TitleH, Head);
            Draw.Bar(X, Y + TitleH - 0.0014f, ListW, 0.0014f, Amber);

            if (_built < (_all == null ? 1 : _all.Length))
            {
                Draw.Text("READING THE VEHICLE LIST", X + 0.010f, Y + 0.008f, 0.32f, Amber, Plain);

                var done = _all == null ? 0f : (float)_built / _all.Length;

                Draw.Bar(X + 0.010f, Y + TitleH + 0.012f, ListW - 0.020f, 0.004f,
                         Color.FromArgb(60, 255, 255, 255));
                Draw.Bar(X + 0.010f, Y + TitleH + 0.012f, (ListW - 0.020f) * done, 0.004f, Amber);
                return;
            }

            Draw.Text(ClassName(), X + 0.010f, Y + 0.008f, 0.34f, Amber, Plain);
            Draw.Text(list.Count + " CARS", X + ListW - 0.008f, Y + 0.010f, 0.26f, Faint, Plain,
                      false, true);

            for (var i = 0; i < shown; i++)
            {
                var index = _scroll + i;
                if (index >= list.Count) break;

                var entry = list[index];
                var y = Y + TitleH + i * RowH;
                var selected = index == _row;

                if (selected)
                {
                    Draw.Bar(X, y, ListW, RowH, Color.FromArgb(40, 245, 196, 60));
                    Draw.Bar(X, y, 0.0018f, RowH, Amber);
                }

                Draw.Text(Draw.Ellipsis(entry.Name, 0.28f, ListW - 0.026f, Plain),
                          X + 0.012f, y + 0.0035f, 0.28f, selected ? Ink : Dim, Plain);
            }

            if (list.Count > Rows)
            {
                var thumb = bodyH * Rows / list.Count;
                var down = bodyH * _scroll / list.Count;

                Draw.Bar(X + ListW - 0.0016f, Y + TitleH, 0.0016f, bodyH,
                         Color.FromArgb(55, 255, 255, 255));
                Draw.Bar(X + ListW - 0.0016f, Y + TitleH + down, 0.0016f, thumb, Amber);
            }

            var foot = Y + TitleH + bodyH;

            Draw.Bar(X, foot, ListW, 0.0012f, Color.FromArgb(70, 255, 255, 255));
            Draw.Text(Pad.InUse() ? "D-PAD move & class   A spawn   B close"
                                  : "ARROWS move & class   ENTER spawn   BACKSPACE close",
                      X + 0.010f, foot + 0.008f, 0.235f, Faint, Plain);

            if (list.Count > 0 && _row >= 0 && _row < list.Count) Stats(statX, list[_row], totalH);
        }

        /// <summary>
        /// The card beside the list: what it is called, what it is called in the files, and how it
        /// compares with everything else in the game.
        /// </summary>
        private void Stats(float x, Entry entry, float height)
        {
            Draw.Bar(x, Y, StatW, height, Panel);
            Draw.Bar(x, Y, StatW, TitleH, Head);
            Draw.Bar(x, Y + TitleH - 0.0014f, StatW, 0.0014f, Amber);

            Draw.Text(Draw.Ellipsis(entry.Name.ToUpperInvariant(), 0.36f, StatW - 0.020f, Plain),
                      x + 0.010f, Y + 0.007f, 0.36f, Ink, Plain);

            var y = Y + TitleH + 0.008f;

            y += Picture(x, y, entry);

            // THE MODEL NAME, WHICH IS THE ONE THING HERE YOU MIGHT WANT TO TYPE. Every other
            // trainer, every ini, every add-on car readme talks in these, not in display names.
            Draw.Text("MODEL", x + 0.010f, y, 0.24f, Faint, Plain);
            Draw.Text(entry.Code.ToLowerInvariant(), x + StatW - 0.010f, y, 0.26f, Amber, Plain,
                      false, true);

            y += 0.019f;

            Draw.Text("SEATS", x + 0.010f, y, 0.24f, Faint, Plain);
            Draw.Text(entry.Seats.ToString(), x + StatW - 0.010f, y, 0.26f, Dim, Plain, false, true);

            y += 0.019f;

            Draw.Text("PRICE", x + 0.010f, y, 0.24f, Faint, Plain);
            Draw.Text(entry.Price > 0 ? "$" + entry.Price.ToString("N0") : "-",
                      x + StatW - 0.010f, y, 0.26f, Dim, Plain, false, true);

            y += 0.026f;

            Bar(x, ref y, "TOP SPEED", entry.Speed, _best[0], (entry.Speed * 3.6f).ToString("0") + " km/h");
            Bar(x, ref y, "ACCELERATION", entry.Accel, _best[1], null);
            Bar(x, ref y, "BRAKING", entry.Brake, _best[2], null);
            Bar(x, ref y, "GRIP", entry.Grip, _best[3], null);
        }

        /// <summary>
        /// A picture for the card: the car itself when there is one beside the log, its maker's
        /// badge when there is not, and what sort of thing it is failing that. Returns the height
        /// it used.
        ///
        /// THE PICTURES ARE OURS, BECAUSE THE GAME'S ARE OUT OF REACH. Two texture inventories
        /// agree that the only per-vehicle artwork in a streamable dictionary is the maker's
        /// badge; the photographs on the in-game websites live inside their web pages, where
        /// DRAW_SPRITE cannot be pointed. So the mod ships its own: one PNG per model, the car
        /// cut out on transparent, in the cars folder beside the log, drawn through the same
        /// CustomSprite path as the title, one for every model in the shipped list. A model with
        /// no file there -- an add-on car -- falls through to the badge as before.
        ///
        /// ONLY ONCE THE HIGHLIGHT HAS SETTLED. A texture ScriptHookV has loaded stays loaded
        /// until the scripts reload; there is no handing one back. Loading as you scroll would
        /// mean a run down the list left every car you passed in memory, so nothing is loaded
        /// until the highlight has been still for a moment, and what is loaded is what you
        /// actually stopped on. Half a megabyte each; a long session of browsing is tens.
        /// </summary>
        private float Picture(float x, float y, Entry entry)
        {
            var w = StatW - 0.020f;

            // AS TALL AS A 16:9 PICTURE NEEDS, on whatever screen this is. The box is drawn in
            // screen fractions, which are wider than they are tall by the aspect ratio, so a box
            // that is 16:9 in pixels is w * aspect / (16/9) tall -- capped, because on a very
            // wide screen that is most of the card.
            var h = Math.Min(w * Across() / (16f / 9f), 0.200f);

            Draw.Bar(x + 0.010f, y, w, h, Color.FromArgb(120, 0, 0, 0));

            try
            {
                // STILL MOVING: an empty frame, rather than a badge the picture then shoves aside.
                if (Game.GameTime - _movedAt < PictureSettleMs) return h + 0.010f;

                if (Photo(entry).DrawFit(x + 0.010f, y, w, h, Color.White))
                {
                    if (_want != null) Forget();
                    return h + 0.010f;
                }

                Badge(x, y, w, h, entry);
            }
            catch
            {
                // A picture is the one thing on this card nobody needs.
            }

            return h + 0.010f;
        }

        /// <summary>The picture of this model, made once and kept: a texture cannot be handed back.</summary>
        private Sprite Photo(Entry entry)
        {
            Sprite photo;

            if (!_photos.TryGetValue(entry.Code, out photo))
            {
                photo = new Sprite(Path.Combine("cars", entry.Code.ToLowerInvariant() + ".png"), 16f / 9f, true);
                _photos[entry.Code] = photo;
            }

            return photo;
        }

        /// <summary>
        /// The game's own artwork, for a model the mod has no picture of: a dictionary named after
        /// the model if an add-on car shipped one, the maker's badge, or the class icon.
        ///
        /// IT TRIES IN ORDER, AND SAYS WHICH ONE IT FOUND. The tries are walked in priority,
        /// waiting a moment for each to load before giving up on it, so a slow car picture is not
        /// beaten by a fast badge.
        ///
        /// HANDED BACK WHEN WE MOVE ON. A streamed dictionary is memory the game has been told to
        /// hold; eight hundred of those left requested is somebody else's crash.
        /// </summary>
        private void Badge(float x, float y, float w, float h, Entry entry)
        {
            if (_want != entry)
            {
                Forget();

                _want = entry;
                _tries = Tries(entry);
                _hit = -1;
                _since = Game.GameTime;

                foreach (var t in _tries) Function.Call(Hash.REQUEST_STREAMED_TEXTURE_DICT, t[0], false);
            }

            if (_hit < 0) Resolve();

            if (_hit < 0)
            {
                var over = Game.GameTime - _since > WaitMs * _tries.Length;

                Draw.Text(over ? "NO PICTURE" : "...", x + 0.010f + w * 0.5f, y + h * 0.5f - 0.007f,
                          0.26f, Faint, Plain, true);

                return;
            }

            var dict = _tries[_hit][0];
            var tex = _tries[_hit][1];

            // ITS OWN SHAPE, INSIDE THE BOX. A fraction of the screen's width and a fraction of
            // its height are not the same size, so a picture drawn at equal fractions is
            // stretched by the aspect ratio. A badge is drawn smaller than a photo would be: a
            // logo filling a photo's frame reads as a mistake, and a logo sat in the middle of
            // one reads as a badge.
            var room = _hit == 0 ? 1f : 0.55f;
            var wants = _size.X / _size.Y / Across();

            var fw = w * room;
            var fh = fw / wants;

            if (fh > h * room)
            {
                fh = h * room;
                fw = fh * wants;
            }

            Function.Call(Hash.DRAW_SPRITE, dict, tex,
                          x + 0.010f + w * 0.5f, y + h * 0.5f, fw, fh, 0f, 255, 255, 255, 255);
        }

        /// <summary>
        /// Walks the tries in priority and settles on the first that is really there.
        ///
        /// HAS_STREAMED_TEXTURE_DICT_LOADED IS NOT AN EXISTENCE CHECK -- it answers yes for a
        /// dictionary that does not exist, which is how the first version drew a white box.
        /// GET_TEXTURE_RESOLUTION is: a texture that is there has a size. A try that has not
        /// loaded yet is waited for, up to a moment, so the badge cannot beat the car picture
        /// merely by loading faster; a try that has loaded and has no such texture is a miss
        /// and the next is looked at on the same frame.
        /// </summary>
        private void Resolve()
        {
            var waited = Game.GameTime - _since;

            for (var i = 0; i < _tries.Length; i++)
            {
                var dict = _tries[i][0];
                var tex = _tries[i][1];

                if (string.IsNullOrEmpty(tex)) continue;

                if (!Function.Call<bool>(Hash.HAS_STREAMED_TEXTURE_DICT_LOADED, dict))
                {
                    if (waited < WaitMs * (i + 1)) return;
                    continue;
                }

                var size = Function.Call<Vector3>(Hash.GET_TEXTURE_RESOLUTION, dict, tex);

                if (size.X <= 0f || size.Y <= 0f) continue;

                _hit = i;
                _size = size;

                if (!_said[i])
                {
                    _said[i] = true;
                    Log.Info("Spawner: " + Which[i] + " found for " + _want.Code + " in '" + dict +
                             "' / '" + tex + "' (" + (int)size.X + " by " + (int)size.Y + ").");
                }

                return;
            }
        }

        /// <summary>What to ask for, most specific first.</summary>
        private static string[][] Tries(Entry entry)
        {
            var model = entry.Code.ToLowerInvariant();

            return new[]
            {
                new[] { model, model },
                new[] { "mpcarhud", entry.Make },
                new[] { "mpcarhud", entry.Icon },
            };
        }

        private static readonly string[] Which = { "a picture of the car", "the maker's badge", "a class icon" };

        /// <summary>Gives back every dictionary asked for on the last card.</summary>
        private void Forget()
        {
            var had = _tries;

            _want = null;
            _tries = new string[0][];
            _hit = -1;

            if (had == null) return;

            foreach (var t in had)
            {
                try { Function.Call(Hash.SET_STREAMED_TEXTURE_DICT_AS_NO_LONGER_NEEDED, t[0]); }
                catch { /* it will be dropped with the session either way */ }
            }
        }

        private static float Across()
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

        /// <summary>
        /// One stat, against the best in the game rather than against a number I made up.
        ///
        /// The game hands back a top speed in metres a second and an acceleration in units nobody
        /// has ever explained. What a person wants to know is whether this one is quick, and quick
        /// only means anything next to something else.
        /// </summary>
        private void Bar(float x, ref float y, string label, float value, float best, string said)
        {
            Draw.Text(label, x + 0.010f, y, 0.235f, Faint, Plain);

            if (said != null)
            {
                Draw.Text(said, x + StatW - 0.010f, y, 0.235f, Dim, Plain, false, true);
            }

            y += 0.015f;

            var w = StatW - 0.020f;
            var part = best > 0f ? value / best : 0f;

            if (part < 0f) part = 0f;
            if (part > 1f) part = 1f;

            Draw.Bar(x + 0.010f, y, w, 0.0045f, Color.FromArgb(50, 255, 255, 255));
            Draw.Bar(x + 0.010f, y, w * part, 0.0045f, Amber);

            y += 0.017f;
        }

        private string ClassName()
        {
            try { return ((VehicleClass)_class).ToString().ToUpperInvariant(); }
            catch { return "VEHICLES"; }
        }

        // ==================================================================
        // Input
        // ==================================================================

        /// <summary>
        /// Holds off the controls this is driven by, and the ones that would drive the car.
        ///
        /// The same list the settings panel holds off, and for the same reason: a menu that steers
        /// while you read it is a menu you cannot use where you need it.
        /// </summary>
        private void Deafen()
        {
            try
            {
                Game.DisableControlThisFrame(Control.Attack);
                Game.DisableControlThisFrame(Control.Attack2);
                Game.DisableControlThisFrame(Control.Aim);
                Game.DisableControlThisFrame(Control.Jump);
                Game.DisableControlThisFrame(Control.Enter);
                Game.DisableControlThisFrame(Control.Context);
                Game.DisableControlThisFrame(Control.NextCamera);

                Game.DisableControlThisFrame(Control.VehicleExit);
                Game.DisableControlThisFrame(Control.VehicleAccelerate);
                Game.DisableControlThisFrame(Control.VehicleBrake);
                Game.DisableControlThisFrame(Control.VehicleMoveLeftRight);
                Game.DisableControlThisFrame(Control.VehicleRadioWheel);

                Game.DisableControlThisFrame(Control.Phone);
                Game.DisableControlThisFrame(Control.PhoneUp);
                Game.DisableControlThisFrame(Control.PhoneDown);
                Game.DisableControlThisFrame(Control.PhoneLeft);
                Game.DisableControlThisFrame(Control.PhoneRight);
                Game.DisableControlThisFrame(Control.PhoneSelect);
                Game.DisableControlThisFrame(Control.PhoneCancel);
            }
            catch
            {
                // Worst case the game hears the same key we did.
            }
        }

        private static bool Edge(Keys key, ref bool wasDown)
        {
            var down = false;

            try { down = Game.IsKeyPressed(key); }
            catch { /* no keyboard is not an error */ }

            var fired = down && !wasDown;
            wasDown = down;

            return fired;
        }

        /// <summary>
        /// One menu input, however it arrives -- the same shape the settings panel uses.
        ///
        /// A KEY AND A PAD CONTROL COLLAPSED INTO ONE STREAM, rather than two things checked in a
        /// row. Two edge detectors on the same logical button is a bug waiting for the frame when
        /// both fire, and DOWN would move two rows.
        /// </summary>
        private sealed class Button
        {
            private const int FirstRepeatMs = 350;
            private const int RepeatMs = 70;

            private readonly Keys _key;
            private readonly Control _pad;
            private readonly bool _repeats;

            private bool _down;
            private int _repeatAt;
            private bool _muted;

            public bool Fired { get; private set; }

            public Button(Keys key, Control pad, bool repeats)
            {
                _key = key;
                _pad = pad;
                _repeats = repeats;
            }

            /// <summary>Held from before we were listening, so it does not count until let go.</summary>
            public void Disarm()
            {
                _down = true;
                _muted = true;
                Fired = false;
            }

            public void Poll()
            {
                var down = Down(_key) || Pad.Held(_pad);

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

            private static bool Down(Keys key)
            {
                try { return Game.IsKeyPressed(key); }
                catch { return false; }
            }
        }
    }
}
