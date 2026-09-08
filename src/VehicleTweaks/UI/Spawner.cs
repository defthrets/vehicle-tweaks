using System;
using System.Collections.Generic;
using System.Drawing;
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
    /// Every vehicle in the game, browsable, with the one it is pointing at stood in front of you.
    ///
    /// THE LIST IS THE GAME'S OWN. SHVDN's VehicleHash enumeration is 843 entries covering the base
    /// game and every DLC and multiplayer pack, so there is no list to maintain here and nothing to
    /// go stale -- and each one is checked against IsInCdImage before it is offered, which is the
    /// game saying whether that model is actually installed. A menu that offers a car it cannot
    /// spawn is worse than a shorter menu.
    ///
    /// THE PREVIEW IS THE CAR. There is no way to render a model to a picture from a script, so
    /// rather than fake one, the highlighted vehicle is spawned in front of you and turned side-on.
    /// It is a preview in the sense that matters: it is the actual thing, at actual size, in the
    /// actual light.
    ///
    /// AND IT WAITS BEFORE IT SPAWNS. Holding DOWN through forty cars would otherwise be forty
    /// models loaded and forty vehicles created and destroyed, which is a stutter for every one of
    /// them. Nothing is loaded until the highlight has been still for a moment, so scrolling is
    /// free and stopping is what costs.
    ///
    /// THE STAT BARS ARE RELATIVE TO THE FASTEST THING IN THE GAME, worked out from the catalogue
    /// as it is built rather than against numbers I would have had to invent. The game hands back
    /// a top speed in metres a second and an acceleration in units nobody has ever explained; what
    /// a person wants to know is whether this one is quick, and quick only means anything next to
    /// something else.
    /// </summary>
    internal sealed class Spawner
    {
        /// <summary>How long the highlight has to be still before its car is fetched.</summary>
        private const int SettleMs = 220;

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
        }

        private readonly Settings _cfg;

        /// <summary>Every installed vehicle, in the order the game lists its classes.</summary>
        private readonly List<List<Entry>> _classes = new List<List<Entry>>();

        private readonly float[] _best = new float[4];

        private VehicleHash[] _all;
        private int _built;

        private bool _open;
        private int _class;
        private int _row;
        private int _scroll;

        private int _movedAt;
        private Entry _showing;
        private Vehicle _preview;

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

                if (Edge(_cfg.SpawnerKey, ref _keyDown))
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
                    Preview(me);
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
                _all = (VehicleHash[])Enum.GetValues(typeof(VehicleHash));

                for (var i = 0; i < 23; i++) _classes.Add(new List<Entry>());
            }

            if (_built >= _all.Length) return;

            var end = Math.Min(_built + PerFrame, _all.Length);

            for (; _built < end; _built++)
            {
                var hash = _all[_built];

                try
                {
                    var model = new Model(hash);

                    if (!model.IsValid || !model.IsInCdImage || !model.IsVehicle) continue;

                    var group = Function.Call<int>(Hash.GET_VEHICLE_CLASS_FROM_NAME, (uint)hash);
                    if (group < 0 || group >= _classes.Count) continue;

                    var entry = new Entry
                    {
                        Hash = hash,
                        Code = hash.ToString(),
                        Name = Label(hash),
                        Speed = Stat(Hash.GET_VEHICLE_MODEL_ESTIMATED_MAX_SPEED, hash),
                        Accel = Stat(Hash.GET_VEHICLE_MODEL_ACCELERATION, hash),
                        Brake = Stat(Hash.GET_VEHICLE_MODEL_MAX_BRAKING, hash),
                        Grip = Stat(Hash.GET_VEHICLE_MODEL_MAX_TRACTION, hash),
                        Seats = Function.Call<int>(Hash.GET_VEHICLE_MODEL_NUMBER_OF_SEATS, (uint)hash),
                        Price = Function.Call<int>(Hash.GET_VEHICLE_MODEL_VALUE, (uint)hash),
                    };

                    if (entry.Speed > _best[0]) _best[0] = entry.Speed;
                    if (entry.Accel > _best[1]) _best[1] = entry.Accel;
                    if (entry.Brake > _best[2]) _best[2] = entry.Brake;
                    if (entry.Grip > _best[3]) _best[3] = entry.Grip;

                    _classes[group].Add(entry);
                }
                catch
                {
                    // One model that will not answer is not a reason to stop cataloguing.
                }
            }

            if (_built < _all.Length) return;

            foreach (var list in _classes) list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            var total = 0;
            foreach (var list in _classes) total += list.Count;

            Log.Info("Spawner: " + total + " of " + _all.Length + " vehicles are installed here.");

            Settle();
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
                if (_up.Fired) { _row--; Moved(); }
                if (_down.Fired) { _row++; Moved(); }

                if (_row < 0) _row = list.Count - 1;
                if (_row >= list.Count) _row = 0;

                if (_row < _scroll) _scroll = _row;
                if (_row >= _scroll + Rows) _scroll = _row - Rows + 1;
            }

            if (_accept.Fired && _preview != null) Keep(me);
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
                Moved();
                return;
            }
        }

        private void Settle()
        {
            if (Current.Count == 0) Turn(1);
            Moved();
        }

        private void Moved()
        {
            _movedAt = Game.GameTime;
        }

        // ==================================================================
        // The car in front of you
        // ==================================================================

        /// <summary>
        /// Puts the highlighted vehicle on the ground beside the player, and takes the last one away.
        ///
        /// ONLY ONCE THE HIGHLIGHT HAS SETTLED. Scrolling is meant to be free; a model loaded for
        /// every row passed through would make the list stutter in proportion to how fast you moved
        /// through it, which is exactly backwards.
        ///
        /// THE OLD ONE GOES FIRST AND ITS MODEL IS RELEASED. A preview that is not deleted is a car
        /// left in the street, and a model that is never marked as no longer needed is memory this
        /// script has told the game to hold on to forever -- eight hundred of those is a crash with
        /// somebody else's name on it.
        /// </summary>
        private void Preview(Ped me)
        {
            var list = Current;

            if (list.Count == 0 || _row < 0 || _row >= list.Count) return;

            var want = list[_row];

            if (_showing == want) return;
            if (Game.GameTime - _movedAt < SettleMs) return;

            var model = new Model(want.Hash);

            model.Request();

            if (!model.IsLoaded) return;

            Remove();

            try
            {
                var where = me.Position + me.ForwardVector * 6f + Vector3.WorldUp * 0.5f;

                _preview = World.CreateVehicle(model, where, me.Heading + 90f);

                if (_preview != null)
                {
                    _preview.IsPersistent = true;
                    _preview.PlaceOnGround();
                }

                _showing = want;
            }
            catch
            {
                _preview = null;
            }
            finally
            {
                model.MarkAsNoLongerNeeded();
            }
        }

        /// <summary>Stops it being a preview, and closes. The car is now simply a car.</summary>
        private void Keep(Ped me)
        {
            var kept = _preview;

            _preview = null;
            _showing = null;

            if (kept != null && kept.Exists())
            {
                Log.Info("Spawner: spawned " + kept.DisplayName + ".");
                kept.IsPersistent = false;
            }

            Close();
        }

        private void Remove()
        {
            var old = _preview;

            _preview = null;
            _showing = null;

            if (old == null) return;

            try
            {
                if (old.Exists())
                {
                    old.IsPersistent = false;
                    old.Delete();
                }
            }
            catch
            {
                // It was going to be cleaned up by the game eventually anyway.
            }
        }

        public void Close()
        {
            _open = false;
            Remove();
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

            var y = Y + TitleH + 0.010f;

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
