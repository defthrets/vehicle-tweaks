using System;
using System.Collections.Generic;
using System.Drawing;
using GTA.Native;
using VehicleTweaks.Core;

namespace VehicleTweaks.UI
{
    /// <summary>
    /// The drawing primitives the settings panel needs, and nothing else.
    ///
    /// No LemonUI, no NativeUI, no menu framework. A GTA scripts\ folder is ONE shared
    /// assembly-resolution namespace, and every UI library dropped in it is a version fight
    /// waiting to happen with somebody else's mod -- one that the player experiences as this
    /// mod being broken. A menu is a list of strings and a highlight. It is not worth a
    /// dependency, and it is certainly not worth being the reason two unrelated mods stop
    /// working together.
    ///
    /// Taken from Fumes, which drew a fuel gauge out of the same four calls.
    /// </summary>
    internal static class Draw
    {
        /// <summary>
        /// A filled rectangle, positioned by its CENTRE.
        ///
        /// That is DRAW_RECT's own convention and it is worth stating, because every other
        /// coordinate in a HUD is a corner and getting it wrong shifts everything by half its
        /// own size -- which looks like a rounding error rather than a mistake.
        /// </summary>
        public static void Rect(float centreX, float centreY, float width, float height, Color colour)
        {
            try
            {
                Function.Call(Hash.DRAW_RECT, centreX, centreY, width, height,
                              colour.R, colour.G, colour.B, colour.A, false);
            }
            catch (Exception ex)
            {
                Log.Once("draw-rect", "DRAW_RECT failed: " + ex.Message);
            }
        }

        /// <summary>A rectangle drawn from its top-left, which is how a panel is actually thought about.</summary>
        public static void Bar(float left, float top, float width, float height, Color colour)
        {
            Rect(left + width / 2f, top + height / 2f, width, height, colour);
        }

        /// <summary>
        /// One line of text.
        ///
        /// ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME is the right component despite the name:
        /// it is the one that takes a literal string rather than a label from the game's text
        /// table. It also has a hard limit of 99 characters, so anything longer is cut here
        /// rather than silently drawing nothing at all.
        /// </summary>
        /// <summary>
        /// A small picture, out of squares.
        ///
        /// THE SAME REASON THE SPEEDO'S DIGITS ARE RECTANGLES: no font this game ships has a
        /// glyph for a cog or a tyre, and the one time this mod tried a symbol it drew the
        /// missing-character box. A seven-by-seven grid of cells is crude at any size bigger than
        /// this and exactly right at this one, and it matches the seven-segment look the rest of
        /// the mod already has.
        ///
        /// Rows are strings, '#' for a lit cell, so an icon is readable in the source as the thing
        /// it draws. Cell height and width are passed separately because a screen fraction is a
        /// fraction of that axis -- the same trap as everywhere else in here.
        /// </summary>
        public static void Icon(string[] rows, float left, float top, float cellTall, float cellWide,
                                Color colour)
        {
            if (rows == null) return;

            for (var r = 0; r < rows.Length; r++)
            {
                var row = rows[r];

                for (var c = 0; c < row.Length; c++)
                {
                    if (row[c] != '#') continue;

                    Bar(left + c * cellWide, top + r * cellTall, cellWide, cellTall, colour);
                }
            }
        }

        public static void Text(string text, float x, float y, float scale, Color colour,
                                int font = 4, bool centre = false, bool rightAlign = false,
                                bool outline = true)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.Length > 99) text = text.Substring(0, 99);

            try
            {
                Function.Call(Hash.SET_TEXT_FONT, font);
                Function.Call(Hash.SET_TEXT_SCALE, 0f, scale);
                Function.Call(Hash.SET_TEXT_COLOUR, colour.R, colour.G, colour.B, colour.A);

                // BOTH OF THESE DRAW IN BLACK, which is fine on light text over a dark panel
                // and actively harmful on dark text over a light one.
                if (outline)
                {
                    Function.Call(Hash.SET_TEXT_DROP_SHADOW);
                    Function.Call(Hash.SET_TEXT_OUTLINE);
                }
                Function.Call(Hash.SET_TEXT_CENTRE, centre);

                if (rightAlign)
                {
                    Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, true);

                    // A right-justified string is laid out against the RIGHT edge of a wrap
                    // window, and with no window set that edge is zero -- so the text is drawn
                    // off the left of the screen and looks like it never drew.
                    Function.Call(Hash.SET_TEXT_WRAP, 0f, x);
                }

                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
                Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y, 0);
            }
            catch (Exception ex)
            {
                Log.Once("draw-text", "Text drawing failed: " + ex.Message);
            }
        }

        /// <summary>
        /// How wide a string will be, as a fraction of the screen.
        ///
        /// The font and scale have to be set BEFORE the measuring command begins, exactly as
        /// they do before drawing -- the game measures with whatever is currently selected,
        /// not with anything passed to the measure call. Getting that order wrong returns the
        /// width the string would have had in the previous font, which is a very quiet way to
        /// mis-place a line.
        /// </summary>
        public static float Width(string text, float scale, int font = 4)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            if (text.Length > 99) text = text.Substring(0, 99);

            try
            {
                Function.Call(Hash.SET_TEXT_FONT, font);
                Function.Call(Hash.SET_TEXT_SCALE, 0f, scale);

                Function.Call(Hash.BEGIN_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);

                return Function.Call<float>(Hash.END_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT, true);
            }
            catch (Exception ex)
            {
                Log.Once("text-width", "Could not measure text: " + ex.Message);
                return 0f;
            }
        }

        /// <summary>
        /// A scale that makes the text fit the room available, never larger than asked for.
        ///
        /// ASKED, NOT ASSUMED. A title is written once and then read on somebody else's aspect
        /// ratio, in somebody else's language, at somebody else's UI scale -- and a string that
        /// overshoots its panel does not wrap or clip tidily, it simply runs out across the
        /// game. Measuring costs two native calls on a menu that is only drawn while it is
        /// open, which is nothing, and it removes a guess that could only ever have been
        /// checked by looking at it.
        /// </summary>
        public static float FitScale(string text, float wanted, float room, int font = 4)
        {
            try
            {
                var w = Width(text, wanted, font);
                if (w <= 0f || w <= room) return wanted;

                var fitted = wanted * (room / w);
                return fitted < 0.15f ? 0.15f : fitted;
            }
            catch
            {
                return wanted;
            }
        }

        /// <summary>
        /// Cuts a string to fit the room available, ending in an ellipsis if it had to.
        ///
        /// For the hint line, where shrinking the text instead would leave the panel with a
        /// different type size on every row depending on how much its author had to say.
        /// </summary>
        public static string Ellipsis(string text, float scale, float room, int font = 4)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (Width(text, scale, font) <= room) return text;

            var cut = text;

            // Word by word from the end, so the ellipsis never lands mid-word.
            var words = new List<string>(cut.Split(' '));

            while (words.Count > 1)
            {
                words.RemoveAt(words.Count - 1);
                cut = string.Join(" ", words.ToArray()) + "...";
                if (Width(cut, scale, font) <= room) return cut;
            }

            return cut;
        }
    }
}
