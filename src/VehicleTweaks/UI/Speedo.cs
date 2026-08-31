using System;
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

        public Speedo(Settings cfg)
        {
            _cfg = cfg;
        }

        // The shape of one digit at scale 1, as fractions of the screen.
        private const float DigitW = 0.0150f;
        private const float DigitH = 0.0300f;
        private const float Thick = 0.0034f;
        private const float Gap = 0.0044f;
        private const float RevHeight = 0.0060f;

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
        public void Update(Ped me, bool preview)
        {
            if (!_cfg.Speedo) return;

            try
            {
                var car = me == null ? null : me.CurrentVehicle;
                var inCar = car != null && car.Exists();

                if (!inCar && _cfg.SpeedoOnlyInVehicle && !preview) return;

                Render(inCar ? Reading(car) : 0,
                       inCar ? Revs(car) : 0f,
                       inCar ? Gear(car) : 1);
            }
            catch (Exception ex)
            {
                Log.Once("speedo", "The speedo fell over: " + ex.Message);
            }
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

        private void Render(int speed, float revs, int gear)
        {
            var scale = _cfg.SpeedoScale;

            var dw = DigitW * scale;
            var dh = DigitH * scale;
            var th = Thick * scale;
            var gap = Gap * scale;

            var digits = dw * 3f + gap * 2f;

            var unitScale = 0.30f * scale;
            var unitText = _cfg.SpeedoUnits == SpeedoUnits.Mph ? "MPH" : "KPH";
            var unitWidth = Draw.Width(unitText, unitScale);

            var x = _cfg.SpeedoX;
            var y = _cfg.SpeedoY;

            var lit = Tint(_cfg.SpeedoOpacity);

            // The gear sits right of the unit, smaller than the speed, because it is a thing you
            // check rather than a thing you read.
            var gearScale = scale * 0.72f;
            var gw = DigitW * gearScale;
            var gh = DigitH * gearScale;
            var gt = Thick * gearScale;

            var block = digits + gap + unitWidth + (_cfg.SpeedoGear ? gap * 2f + gw : 0f);

            var revH = RevHeight * scale;
            var revGap = 0.0040f * scale;
            if (_cfg.SpeedoBackground)
            {
                // A little wider than the digits and the unit together. Deliberately plain: the
                // game has no rounded rectangle, and a fake one drawn out of squares looks worse
                // than an honest box.
                var padX = 0.0060f * scale;
                var padY = 0.0055f * scale;

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

                Digit(dx, y, dw, dh, th, on, lit);
            }

            Draw.Text(unitText, x + digits + gap, y + dh * 0.5f - unitScale * 0.028f,
                      unitScale, lit, 4);

            if (_cfg.SpeedoGear)
            {
                // R FOR REVERSE, which on seven segments is the lower-case one: the middle bar
                // and the top-left upright. A capital R cannot be made out of these seven at all,
                // and every dashboard that has ever shown one has shown this shape.
                var shape = gear == 0 ? 0x50 : Numerals[gear];

                Digit(x + digits + gap + unitWidth + gap * 2f, y + (dh - gh) * 0.5f,
                      gw, gh, gt, shape, lit);
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
        private void Digit(float x, float y, float w, float h, float t, int on, Color lit)
        {
            var ghost = _cfg.SpeedoGhost
                            ? Color.FromArgb(Math.Max(6, lit.A / 9), lit.R, lit.G, lit.B)
                            : Color.FromArgb(0, 0, 0, 0);

            // The gap between the top of the middle bar and the top of the display, which is
            // also the length of each vertical once the bars at either end are taken off it.
            var half = (h - t) * 0.5f;
            var stem = half - t;

            Bar(x + t, y, w - t * 2f, t, (on & 0x01) != 0, lit, ghost);              // a  top
            Bar(x + w - t, y + t, t, stem, (on & 0x02) != 0, lit, ghost);            // b  top right
            Bar(x + w - t, y + half + t, t, stem, (on & 0x04) != 0, lit, ghost);     // c  bottom right
            Bar(x + t, y + h - t, w - t * 2f, t, (on & 0x08) != 0, lit, ghost);      // d  bottom
            Bar(x, y + half + t, t, stem, (on & 0x10) != 0, lit, ghost);             // e  bottom left
            Bar(x, y + t, t, stem, (on & 0x20) != 0, lit, ghost);                    // f  top left
            Bar(x + t, y + half, w - t * 2f, t, (on & 0x40) != 0, lit, ghost);       // g  middle
        }

        private static void Bar(float x, float y, float w, float h, bool on, Color lit, Color ghost)
        {
            var colour = on ? lit : ghost;

            if (colour.A == 0) return;

            Draw.Bar(x, y, w, h, colour);
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
