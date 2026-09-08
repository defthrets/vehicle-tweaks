using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using VehicleTweaks.Core;

namespace VehicleTweaks.UI
{
    /// <summary>
    /// A speed readout: three digits and a unit, drawn as a seven-segment display.
    ///
    /// SEGMENTS, NOT A FONT, and that is the whole reason it looks like a clock. GTA ships four
    /// fonts and not one of them is seven-segment -- Chalet, Chalet Comprime, Chalet fixed-width
    /// and Pricedown -- so any font would have been an ordinary number in an ordinary typeface
    /// with a dark box behind it. A digit is seven rectangles, this mod already draws everything
    /// out of rectangles, and the result is the actual thing rather than an impression of it.
    ///
    /// It is also the half of this I could be sure of without seeing it. Text metrics depend on
    /// a font, a resolution and a safe-zone setting, and I have none of those in front of me;
    /// geometry is the same everywhere.
    ///
    /// THE GHOST IS THE DETAIL THAT SELLS IT. A real display's unlit segments are not invisible,
    /// they are dark grey -- which is why an LCD showing 42 also faintly shows the 8s it is not
    /// lighting. Drawing them costs seven more rectangles per digit and is the difference
    /// between a number on the screen and a panel in a car.
    ///
    /// THE FIRST THING THIS MOD PUTS ON SCREEN AND LEAVES THERE. Everything else is silent, and
    /// deliberately so, but silence was always about not INTERRUPTING -- no prompts, no
    /// notifications, nothing that has to be dismissed. A dial you glance at is what a car
    /// already has.
    /// </summary>
    internal sealed class Speedo
    {
        private readonly Settings _cfg;

        /// <summary>
        /// One picture per digit, in two sets: with the unlit segments faintly there, and without.
        ///
        /// THE DIGITS WERE TWENTY-ONE RECTANGLES, AND RECTANGLES ARE RATIONED. The game keeps a
        /// budget of immediate draws per frame that every script shares, and when another mod puts
        /// up a big menu the calls that arrive after it are dropped without a word -- which on a
        /// seven-segment display is a digit with one bar lit and a rev strip that is not there.
        /// A picture is one draw. It is the same trick Fumes uses for the same reason, and the
        /// files are made from the exact geometry Digit draws, so nothing moved.
        ///
        /// The rectangles stay as the fallback, for a scripts folder the pictures did not reach.
        /// </summary>
        private readonly Dictionary<string, Sprite> _glyphs = new Dictionary<string, Sprite>();

        /// <summary>Whether something other than the driver's right foot is setting the speed.</summary>
        private bool _cruising;
        private bool _chauffeured;

        public Speedo(Settings cfg)
        {
            _cfg = cfg;
        }

        // The shape of one digit at scale 1, ALL OF IT IN FRACTIONS OF SCREEN HEIGHT.
        //
        // Every number here is a height, including the widths, and the horizontal ones are
        // converted on use by Across(). That is not fussiness -- it is the bug this display
        // shipped with. A screen fraction is a fraction of the screen IN THAT AXIS, so on a
        // sixteen-by-nine display 0.015 across is 29 pixels while 0.030 down is 32: digits that
        // were meant to be half as wide as they were tall came out very nearly square, and the
        // upright bars came out almost twice as thick as the flat ones, because one thickness
        // number was being used for both and only one of them was in the right axis.
        //
        // Six-tenths of the height is roughly what a real seven-segment digit is.
        private const float DigitW = 0.0180f;
        private const float DigitH = 0.0300f;
        private const float Thick = 0.0042f;
        private const float Gap = 0.0060f;

        // THE SPACE EITHER SIDE OF THE UNIT, which is not the same space as the one between two
        // digits and was using it. Digits in a cluster sit close together ON PURPOSE -- that
        // tight spacing is most of what makes three of them read as one number rather than as
        // three separate figures. Setting KPH one digit-gap away from the last digit therefore
        // put it INSIDE the number: same spacing, same apparent group, so the eye read "0KPH" as
        // one object. A label beside a number has to be further off than the number's own parts
        // are from each other, or it is not beside it at all.
        private const float UnitGap = 0.0130f;
        private const float RevHeight = 0.0060f;
        private const float LampH = 0.0190f;
        private const float LampW = 0.0230f;

        /// <summary>
        /// The engine, and an oil can, as rectangles in a unit box.
        ///
        /// FOUR NUMBERS A BAR -- left, top, right, bottom -- as fractions of whatever box the
        /// lamp is drawn into, so the shape is written once and scales with everything else.
        ///
        /// Drawn rather than typed for the same reason the digits are: there is no glyph for an
        /// engine in any font this game ships, and the shapes on a real dashboard are not
        /// letters. They are crude at this size and they are meant to be -- a warning lamp is
        /// recognised by its silhouette and its colour, not read.
        /// </summary>
        private static readonly float[] EngineShape =
        {
            0.20f, 0.34f, 0.80f, 0.86f,   // the block
            0.34f, 0.16f, 0.62f, 0.34f,   // rocker cover on top
            0.06f, 0.48f, 0.20f, 0.70f,   // mounting lug, left
            0.80f, 0.44f, 0.94f, 0.64f,   // mounting lug, right
            0.66f, 0.24f, 0.80f, 0.34f,   // the stub that makes it read as an engine
        };

        private static readonly float[] OilShape =
        {
            0.10f, 0.40f, 0.66f, 0.78f,   // the can
            0.66f, 0.26f, 0.98f, 0.37f,   // the spout
            0.34f, 0.83f, 0.48f, 1.00f,   // the drip
        };

        /// <summary>
        /// Which segments each numeral lights.
        ///
        /// Bit 1 is the top bar, then clockwise -- top right, bottom right, bottom, bottom left,
        /// top left -- and bit 64 is the middle. The usual naming for these is a to g and this
        /// is that order, so the table can be checked against any seven-segment reference rather
        /// than only against itself.
        /// </summary>
        private static readonly int[] Numerals =
        {
            0x3F, // 0  a b c d e f
            0x06, // 1  b c
            0x5B, // 2  a b d e g
            0x4F, // 3  a b c d g
            0x66, // 4  b c f g
            0x6D, // 5  a c d f g
            0x7D, // 6  a c d e f g
            0x07, // 7  a b c
            0x7F, // 8  all
            0x6F, // 9  a b c d f g
        };

        /// <summary>
        /// Draws it, if there is anything to draw.
        /// </summary>
        /// <param name="preview">
        /// True while the settings panel is open, which shows the readout even on foot. The
        /// position and size are numbers on that panel and there is no other way to judge them:
        /// nudging X while the thing you are moving is invisible is not adjusting, it is
        /// guessing and then going to look.
        /// </param>
        /// <param name="cruising">Cruise control is holding a speed.</param>
        /// <param name="chauffeured">The car is driving itself.</param>
        public void Update(Ped me, bool preview, bool cruising, bool chauffeured)
        {
            _cruising = cruising;
            _chauffeured = chauffeured;

            if (!_cfg.Speedo && !_cfg.SpeedoEngineIcon && !_cfg.SpeedoOilLight) return;

            try
            {
                var car = me == null ? null : me.CurrentVehicle;
                var inCar = car != null && car.Exists();

                if (!inCar && _cfg.SpeedoOnlyInVehicle && !preview) return;

                if (_cfg.Speedo)
                {
                    Render(inCar ? Reading(car) : 0,
                           inCar ? Revs(car) : 0f,
                           inCar ? Gear(car) : 1);
                }

                Lamps(inCar ? Engine(car) : 1f, inCar ? Oil(car) : 1f);
            }
            catch (Exception ex)
            {
                Log.Once("speedo", "The speedo fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// A height turned into the width that draws the same size on screen.
        ///
        /// Asked of the game rather than assumed to be sixteen by nine, so an ultrawide gets
        /// digits of the right shape instead of ones squashed by a third. A nonsense answer
        /// falls back to the common case rather than to a division by zero.
        /// </summary>
        private static float Across(float height)
        {
            float aspect;

            try { aspect = GTA.UI.Screen.AspectRatio; }
            catch { aspect = 16f / 9f; }

            if (aspect < 0.5f || aspect > 6f) aspect = 16f / 9f;

            return height / aspect;
        }

        /// <summary>Metres a second as whatever the player asked to read.</summary>
        private int Reading(Vehicle car)
        {
            float ms;

            try { ms = car.Speed; }
            catch { return 0; }

            var shown = _cfg.SpeedoUnits == SpeedoUnits.Mph ? ms * 2.2369363f : ms * 3.6f;

            if (shown < 0f) shown = 0f;

            // Three digits is the ask, and three digits is also every speed this game can
            // produce in either unit. Clamped rather than wrapped, because a jet reading 007
            // would be worse than one reading 999.
            var whole = (int)(shown + 0.5f);
            return whole > 999 ? 999 : whole;
        }

        /// <summary>The engine, from idle to the limiter, as nought to one.</summary>
        private static float Revs(Vehicle car)
        {
            try
            {
                var rpm = car.CurrentRPM;

                if (rpm < 0f) return 0f;
                return rpm > 1f ? 1f : rpm;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Which gear, where nought is reverse.
        ///
        /// That is the game's own convention and not a guess: CurrentGear counts up from one and
        /// uses nought for reverse, so there is no neutral to show and nothing to invent.
        /// </summary>
        private static int Gear(Vehicle car)
        {
            try
            {
                var g = car.CurrentGear;

                if (g < 0) return 0;
                return g > 9 ? 9 : g;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// Engine condition, from one down to nothing.
        ///
        /// A THOUSAND IS A WELL ONE. The game counts down from there and keeps going past zero
        /// into the negatives once it is finished, so this clamps rather than trusting the range
        /// -- a lamp showing minus four hundred per cent would be its own kind of wrong.
        /// </summary>
        private static float Engine(Vehicle car)
        {
            try
            {
                var h = car.EngineHealth / 1000f;

                if (h < 0f) return 0f;
                return h > 1f ? 1f : h;
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>
        /// How much oil is left, as a fraction of what the engine holds.
        ///
        /// AGAINST ITS OWN CAPACITY, not against a number picked here. Engines hold different
        /// amounts and the game knows how much each one takes; a fixed threshold in litres would
        /// have meant a warning light that came on at a quarter in one car and never in another.
        /// A car that reports no capacity has nothing to warn about.
        /// </summary>
        private static float Oil(Vehicle car)
        {
            try
            {
                var capacity = car.OilVolume;
                if (capacity <= 0f) return 1f;

                var level = car.OilLevel / capacity;

                if (level < 0f) return 0f;
                return level > 1f ? 1f : level;
            }
            catch
            {
                return 1f;
            }
        }

        private void Render(int speed, float revs, int gear)
        {
            var scale = _cfg.SpeedoScale;

            var dh = DigitH * scale;
            var dw = Across(DigitW * scale);

            // ONE THICKNESS, TWO AXES. A flat bar is this tall and an upright one is this wide,
            // and they have to be the same size on the glass or the digit looks like it is made
            // of two different pens.
            var ty = Thick * scale;
            var tx = Across(Thick * scale);

            var gap = Across(Gap * scale);
            var unitGap = Across(UnitGap * scale);

            var digits = dw * 3f + gap * 2f;

            var unitScale = 0.30f * scale;
            var unitText = _cfg.SpeedoUnits == SpeedoUnits.Mph ? "MPH" : "KPH";
            var unitWidth = Draw.Width(unitText, unitScale);

            var x = _cfg.SpeedoX;
            var y = _cfg.SpeedoY;

            var lit = Held(Tint(_cfg.SpeedoOpacity));

            // The gear sits right of the unit, smaller than the speed, because it is a thing you
            // check rather than a thing you read.
            var gearScale = scale * 0.72f;
            var gh = DigitH * gearScale;
            var gw = Across(DigitW * gearScale);
            var gty = Thick * gearScale;
            var gtx = Across(Thick * gearScale);

            var block = digits + unitGap + unitWidth + (_cfg.SpeedoGear ? unitGap + gw : 0f);

            var revH = RevHeight * scale;
            var revGap = 0.0040f * scale;
            if (_cfg.SpeedoBackground)
            {
                // A little wider than the digits and the unit together. Deliberately plain: the
                // game has no rounded rectangle, and a fake one drawn out of squares looks worse
                // than an honest box.
                // The same padding all the way round, which it was not: 0.0060 across was eleven
                // pixels and 0.0055 down was six, so the box had twice the margin at the sides
                // that it had at the top. That is most of what made the panel look stretched.
                var padY = 0.0055f * scale;
                var padX = Across(0.0055f * scale);

                var tall = dh + (_cfg.SpeedoRevs ? revGap + revH : 0f);

                Draw.Bar(x - padX, y - padY, block + padX * 2f, tall + padY * 2f,
                         Color.FromArgb((int)(150f * _cfg.SpeedoOpacity), 0, 0, 0));
            }

            // RIGHT ALIGNED, WITH NO LEADING ZEROS. A dashboard reading 042 is a stopwatch; a
            // dashboard reading 42 with two faint ghosts to the left of it is a dashboard.
            var text = speed.ToString();

            for (var i = 0; i < 3; i++)
            {
                var dx = x + i * (dw + gap);
                var at = i - (3 - text.Length);

                var on = at >= 0 ? Numerals[text[at] - '0'] : 0;

                Digit(dx, y, dw, dh, tx, ty, on, lit);
            }

            Draw.Text(unitText, x + digits + unitGap, y + dh * 0.5f - unitScale * 0.028f,
                      unitScale, lit, 4);

            if (_cfg.SpeedoGear)
            {
                // R FOR REVERSE, which on seven segments is the lower-case one: the middle bar
                // and the top-left upright. A capital R cannot be made out of these seven at all,
                // and every dashboard that has ever shown one has shown this shape.
                var shape = gear == 0 ? 0x50 : Numerals[gear];

                Digit(x + digits + unitGap + unitWidth + unitGap, y + (dh - gh) * 0.5f,
                      gw, gh, gtx, gty, shape, lit);
            }

            if (_cfg.SpeedoRevs) Tacho(x, y + dh + revGap, block, revH, revs, lit);
        }

        /// <summary>
        /// The revs, as a strip of blocks that fill from the left.
        ///
        /// BLOCKS RATHER THAN A SMOOTH BAR, for the same reason the digits are segments: a bar
        /// that slides is a progress bar, and a row of cells that snap on and off one at a time
        /// is a rev counter. It also reads at a glance in peripheral vision, which is the only
        /// way anybody ever looks at one -- a smooth fill needs to be measured against its own
        /// ends to mean anything, and a count of lit cells does not.
        ///
        /// THE LAST FIFTH IS RED whatever colour the rest is, because that is the one thing a
        /// rev counter is FOR. A shift light that matched the display would be decoration.
        /// </summary>
        private void Tacho(float x, float y, float width, float height, float revs, Color lit)
        {
            const int Cells = 16;

            var sgap = width * 0.012f;
            var cell = (width - sgap * (Cells - 1)) / Cells;

            var ghost = _cfg.SpeedoGhost
                            ? Color.FromArgb(Math.Max(6, lit.A / 9), lit.R, lit.G, lit.B)
                            : Color.FromArgb(0, 0, 0, 0);

            var red = Color.FromArgb(lit.A, 255, 64, 48);

            for (var i = 0; i < Cells; i++)
            {
                var on = revs > (i + 0.5f) / Cells;
                var colour = on ? (i >= Cells - Cells / 5 ? red : lit) : ghost;

                if (colour.A == 0) continue;

                Draw.Bar(x + i * (cell + sgap), y, cell, height, colour);
            }
        }

        /// <summary>
        /// One digit: seven bars, of which the unlit ones are drawn faintly rather than not
        /// at all.
        /// </summary>
        private void Digit(float x, float y, float w, float h, float tx, float ty, int on, Color lit)
        {
            // THE PICTURE FIRST. One draw instead of seven, out of the budget every script shares.
            var name = on == 0 ? (_cfg.SpeedoGhost ? "blank" : null)
                     : on == 0x50 ? "R"
                     : Which(on);

            if (name == null) return;

            // ONE SPRITE PER GLYPH, however many positions show it: SHVDN numbers the draws of a
            // texture within a frame, so the same file can stand in three places at once. What
            // it cannot do is take back a draw once the digit changes -- ScriptHookV keeps it up
            // for a tenth of a second regardless -- and that is dealt with in Sprite.EndFrame.
            var file = (_cfg.SpeedoGhost ? "g" : "n") + name + ".png";

            Sprite glyph;

            if (!_glyphs.TryGetValue(file, out glyph))
            {
                glyph = new Sprite(file, 0.6f);
                _glyphs[file] = glyph;
            }

            if (glyph.DrawBox(x, y, w, h, lit)) return;

            var ghost = _cfg.SpeedoGhost
                            ? Color.FromArgb(Math.Max(6, lit.A / 9), lit.R, lit.G, lit.B)
                            : Color.FromArgb(0, 0, 0, 0);

            // The drop from the top of the display to the top of the middle bar, which is also
            // the length of each upright once the bars at either end are taken off it.
            var half = (h - ty) * 0.5f;
            var stem = half - ty;

            var flat = w - tx * 2f;

            Bar(x + tx, y, flat, ty, (on & 0x01) != 0, lit, ghost);                  // a  top
            Bar(x + w - tx, y + ty, tx, stem, (on & 0x02) != 0, lit, ghost);         // b  top right
            Bar(x + w - tx, y + half + ty, tx, stem, (on & 0x04) != 0, lit, ghost);  // c  bottom right
            Bar(x + tx, y + h - ty, flat, ty, (on & 0x08) != 0, lit, ghost);         // d  bottom
            Bar(x, y + half + ty, tx, stem, (on & 0x10) != 0, lit, ghost);           // e  bottom left
            Bar(x, y + ty, tx, stem, (on & 0x20) != 0, lit, ghost);                  // f  top left
            Bar(x + tx, y + half, flat, ty, (on & 0x40) != 0, lit, ghost);           // g  middle
        }

        /// <summary>
        /// The warning lamps, as their own small cluster.
        ///
        /// NOT PART OF THE SPEEDO, and they were, which was the mistake. A speed readout is one
        /// object -- three digits, a unit, a gear, a strip of revs -- and bolting two crude
        /// symbols onto the end of it made it a wider object that happened to contain some
        /// warning lights. They are a different KIND of thing: the readout is information you
        /// glance at continuously, and a warning lamp is something that should catch your eye
        /// precisely because it is not part of what you were already looking at.
        ///
        /// So they have their own position, their own backing, and their own switches, and the
        /// speed readout is exactly what it was before they existed.
        ///
        /// THE ROOM IS STILL ALWAYS RESERVED for both, lit or not. A lamp that appears and
        /// disappears would take the cluster's width with it and the box would jump every time
        /// the oil got low.
        /// </summary>
        private void Lamps(float engine, float oil)
        {
            var on = (_cfg.SpeedoEngineIcon ? 1 : 0) + (_cfg.SpeedoOilLight ? 1 : 0);
            if (on == 0) return;

            var scale = _cfg.SpeedoScale;

            var h = LampH * scale;
            var w = Across(LampW * scale);
            var gap = Across(0.0110f * scale);

            var wide = on * w + (on - 1) * gap;

            var x = _cfg.SpeedoLampsX;
            var y = _cfg.SpeedoLampsY;

            var lit = Tint(_cfg.SpeedoOpacity);

            if (_cfg.SpeedoBackground)
            {
                var padY = 0.0055f * scale;
                var padX = Across(0.0055f * scale);

                Draw.Bar(x - padX, y - padY, wide + padX * 2f, h + padY * 2f,
                         Color.FromArgb((int)(150f * _cfg.SpeedoOpacity), 0, 0, 0));
            }

            if (_cfg.SpeedoEngineIcon)
            {
                Icon(EngineShape, x, y, w, h, Condition(engine, lit));
                x += w + gap;
            }

            if (_cfg.SpeedoOilLight)
            {
                // ONLY WHEN IT IS LOW. An oil lamp that is lit all the time is not a warning, it
                // is decoration -- the whole meaning of the thing is that seeing it is unusual.
                Icon(OilShape, x, y, w, h, oil < 0.25f ? Bad(lit.A) : Ghost(lit));
            }
        }

        /// <summary>A shape from the tables above, drawn into a box.</summary>
        private static void Icon(float[] shape, float x, float y, float w, float h, Color colour)
        {
            if (colour.A == 0) return;

            for (var i = 0; i + 3 < shape.Length; i += 4)
            {
                Draw.Bar(x + shape[i] * w,
                         y + shape[i + 1] * h,
                         (shape[i + 2] - shape[i]) * w,
                         (shape[i + 3] - shape[i + 1]) * h,
                         colour);
            }
        }

        /// <summary>
        /// Green, amber, red.
        ///
        /// NOT THE DISPLAY'S COLOUR, and that is the point of it. The rest of this readout is
        /// whatever colour was chosen, because it is information; a warning lamp is a judgement,
        /// and everybody on earth already knows what a red one means. An engine light that came
        /// out blue because the speed is blue would be a decoration that happened to change.
        ///
        /// Healthy is dimmer than the two warnings. A lamp you notice is a lamp that was quiet
        /// until it had something to say.
        /// </summary>
        private static Color Condition(float health, Color lit)
        {
            if (health < 0.34f) return Bad(lit.A);
            if (health < 0.67f) return Color.FromArgb(lit.A, 255, 176, 40);

            return Color.FromArgb(Math.Max(24, (int)(lit.A * 0.55f)), 90, 220, 120);
        }

        private static Color Bad(int alpha)
        {
            return Color.FromArgb(alpha, 255, 64, 48);
        }

        private Color Ghost(Color lit)
        {
            return _cfg.SpeedoGhost
                       ? Color.FromArgb(Math.Max(6, lit.A / 9), lit.R, lit.G, lit.B)
                       : Color.FromArgb(0, 0, 0, 0);
        }

        /// <summary>The numeral a segment pattern is, or null for a pattern that is not one.</summary>
        private static string Which(int on)
        {
            for (var i = 0; i < Numerals.Length; i++)
            {
                if (Numerals[i] == on) return i.ToString();
            }

            return null;
        }

        private static void Bar(float x, float y, float w, float h, bool on, Color lit, Color ghost)
        {
            var colour = on ? lit : ghost;

            if (colour.A == 0) return;

            Draw.Bar(x, y, w, h, colour);
        }

        /// <summary>
        /// The digits, in a different colour when the speed is not the driver's own doing.
        ///
        /// A CRUISE CONTROL HAS TO SAY SOMEWHERE THAT IT IS ON. A car quietly refusing to go
        /// faster with nothing on screen to explain it is a bug, not a feature -- that was the
        /// whole argument for why this could be built once the cluster existed, and then the
        /// cluster was never told. The Holding property on Cruise even carried a comment saying
        /// this read it. It did not.
        ///
        /// COLOUR RATHER THAN A BADGE, because the alternative is more things in a readout that
        /// has already had two ornaments taken back out of it. The number IS the thing being
        /// held, so the number is what changes -- nothing moves, nothing gets wider, and the
        /// panel does not learn a new shape.
        ///
        /// Self driving wins over cruise when both are true. It is the larger fact about who is
        /// in charge of the car.
        /// </summary>
        private Color Held(Color lit)
        {
            if (_chauffeured) return Color.FromArgb(lit.A, 120, 220, 255);
            if (_cruising) return Color.FromArgb(lit.A, 130, 240, 160);

            return lit;
        }

        /// <summary>
        /// The colour, as one choice rather than three numbers.
        ///
        /// A red, green or blue slider each is nine settings between the three of them and most
        /// of the combinations look like nothing any dashboard has ever used. A short list of
        /// colours that real displays are actually made in is a smaller setting and a better one.
        /// </summary>
        private Color Tint(float opacity)
        {
            var a = (int)(255f * opacity);
            if (a < 0) a = 0;
            if (a > 255) a = 255;

            switch (_cfg.SpeedoColour)
            {
                case SpeedoColour.Red: return Color.FromArgb(a, 255, 64, 48);
                case SpeedoColour.Green: return Color.FromArgb(a, 90, 240, 130);
                case SpeedoColour.Cyan: return Color.FromArgb(a, 90, 220, 240);
                case SpeedoColour.Blue: return Color.FromArgb(a, 110, 160, 255);
                case SpeedoColour.White: return Color.FromArgb(a, 240, 240, 245);
                default: return Color.FromArgb(a, 245, 196, 60);
            }
        }
    }
}
