using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using GTA.UI;
using VehicleTweaks.Core;

namespace VehicleTweaks.UI
{
    /// <summary>
    /// A PNG from disk, drawn on the screen.
    ///
    /// CUSTOMSPRITE IS THE WHOLE TRICK, and it is the same trick Fumes uses for its pump and its
    /// digits: it loads an ordinary PNG off disk at runtime through ScriptHookVDotNet's own
    /// texture loader. The only alternative is packing a .ytd into one of the game's archives,
    /// which turns a mod anybody can drop into scripts into an asset mod needing OpenIV, a limit
    /// adjuster and a different install per game edition.
    ///
    /// The colour comes from the tint at draw time: a white-on-transparent title takes the panel's
    /// amber and fades with it; a photograph is drawn white, which leaves it alone.
    ///
    /// SIZED BY THE IMAGE'S OWN SHAPE, given once. CustomSprite does not know how big the file
    /// is, and a size guessed at is a picture stretched to whatever box it was given -- so the
    /// aspect is measured when the PNG is made and written down here.
    ///
    /// A TEXTURE STAYS ON SCREEN FOR A TENTH OF A SECOND AFTER ITS LAST DRAW. ScriptHookV keeps
    /// every drawn texture up for the time it was given, and SHVDN gives a hundred milliseconds.
    /// For a title that is drawn every frame that is invisible. For a speedometer digit it is
    /// not: the moment a 3 becomes a 4, the 3 is still there for six frames, over the 4 -- and a
    /// car that is accelerating changes its last digit faster than that, so the digit was never
    /// clean. That is what "it flickers when driving" was. EndFrame is the cure: every texture
    /// drawn fewer times this frame than last has the difference drawn again, invisibly, so the
    /// slot ScriptHookV would have kept showing is overwritten with nothing.
    /// </summary>
    internal sealed class Sprite
    {
        /// <summary>
        /// CustomSprite positions and sizes in a fixed 1280x720 canvas, NOT in real pixels --
        /// which is what makes a position written here look the same on every monitor.
        /// </summary>
        private const float CanvasW = 1280f;
        private const float CanvasH = 720f;

        private readonly string _path;
        private readonly float _aspect;
        private readonly bool _quiet;

        private CustomSprite _sprite;
        private bool _missing;

        /// <param name="file">The file, relative to the mod's own folder beside the log.</param>
        /// <param name="aspect">Its width over its height, as the picture was made.</param>
        /// <param name="quiet">Say nothing in the log when it is missing: for a picture whose
        /// absence is expected and whose caller has something else to draw.</param>
        public Sprite(string file, float aspect, bool quiet = false)
        {
            _path = Paths.Asset(file);
            _aspect = aspect;
            _quiet = quiet;
        }

        /// <summary>Whether the file is there, without loading it.</summary>
        public bool Exists => File.Exists(_path);

        /// <summary>
        /// Draws it into a box the caller has already worked out, shape and all.
        ///
        /// FOR THE THINGS THAT USED TO BE RECTANGLES. A seven-segment digit and a tab icon each
        /// had a box before they had a picture, and the picture was made to that box exactly --
        /// so the caller's width and height are the truth here and the aspect is not consulted.
        /// </summary>
        public bool DrawBox(float left, float top, float width, float height, Color tint)
        {
            if (!Ready(tint)) return false;

            return Put(left * CanvasW, top * CanvasH, width * CanvasW, height * CanvasH, tint);
        }

        /// <summary>
        /// Draws it with its top-left at the given screen fractions, so tall, keeping its shape.
        /// Says whether it did, so a caller can draw something else when the file is not there.
        /// </summary>
        public bool Draw(float left, float top, float height, Color tint)
        {
            if (!Ready(tint)) return false;

            var h = height * CanvasH;

            return Put(left * CanvasW, top * CanvasH, h * _aspect * Squash(), h, tint);
        }

        /// <summary>
        /// Draws it as large as it goes inside a box, keeping its shape, centred in the room left.
        /// </summary>
        public bool DrawFit(float left, float top, float width, float height, Color tint)
        {
            if (!Ready(tint)) return false;

            var boxW = width * CanvasW;
            var boxH = height * CanvasH;
            var shape = _aspect * Squash();

            var h = boxH;
            var w = h * shape;

            if (w > boxW)
            {
                w = boxW;
                h = w / shape;
            }

            return Put(left * CanvasW + (boxW - w) * 0.5f, top * CanvasH + (boxH - h) * 0.5f, w, h, tint);
        }

        /// <summary>
        /// How much narrower a canvas pixel is than it is tall, on this screen.
        ///
        /// THE CANVAS IS 16:9 WHATEVER THE MONITOR IS. Its 1280 columns are spread across the
        /// whole width and its 720 rows down the whole height, so on a 21:9 screen each column is
        /// a third wider than each row and a picture drawn at its own aspect in canvas units is a
        /// third too wide. That was the blackletter title looking wrong on an ultrawide. Width in
        /// canvas units is height times aspect times this.
        /// </summary>
        private static float Squash()
        {
            try
            {
                var screen = Screen.AspectRatio;
                if (screen < 0.5f || screen > 6f) return 1f;
                return (16f / 9f) / screen;
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>Loads it the first time, and says whether there is anything to draw.</summary>
        private bool Ready(Color tint)
        {
            if (_missing) return false;
            if (_sprite != null) return true;

            try
            {
                if (!File.Exists(_path))
                {
                    // ONCE, AND THEN THE FALLBACK FOREVER. A missing picture is not a reason to
                    // check the disk sixty times a second, and what it stands in for is fine.
                    _missing = true;

                    if (!_quiet)
                    {
                        Log.Warn("No " + Path.GetFileName(_path) + " beside the log; drawing that " +
                                 "another way instead. Deploy copies it from assets.");
                    }

                    return false;
                }

                _sprite = new CustomSprite(_path, new SizeF(64f, 64f), new PointF(0f, 0f), tint, 0f, false);
                return true;
            }
            catch (Exception ex)
            {
                _missing = true;
                Log.Once("sprite", "Could not load " + Path.GetFileName(_path) + ": " + ex.Message);
                return false;
            }
        }

        /// <summary>One draw, in canvas units, counted for EndFrame.</summary>
        private bool Put(float x, float y, float w, float h, Color tint)
        {
            try
            {
                _sprite.Size = new SizeF(w, h);
                _sprite.Position = new PointF(x, y);
                _sprite.Color = tint;
                _sprite.Draw();

                int n;
                s_now.TryGetValue(_path, out n);
                s_now[_path] = n + 1;
                s_any[_path] = this;

                return true;
            }
            catch (Exception ex)
            {
                _missing = true;
                Log.Once("sprite", "Could not draw " + Path.GetFileName(_path) + ": " + ex.Message);
                return false;
            }
        }

        // ==================================================================
        // The ledger
        // ==================================================================

        /// <summary>How many times each file was drawn this frame, and the frame before.</summary>
        private static readonly Dictionary<string, int> s_now = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> s_last = new Dictionary<string, int>();

        /// <summary>Any loaded sprite of each file, which is what can draw the invisible copies.</summary>
        private static readonly Dictionary<string, Sprite> s_any = new Dictionary<string, Sprite>();

        /// <summary>
        /// Called once at the end of every tick, after everything has drawn.
        ///
        /// SHVDN numbers the draws of one texture within a frame, first draw nought, and
        /// ScriptHookV keeps each number's last draw on screen for a tenth of a second. A file
        /// drawn three times last frame and twice this frame leaves its third still showing where
        /// it was -- so the third is drawn again here, one pixel, off the screen, transparent,
        /// and what ScriptHookV keeps for the next tenth of a second is nothing. These draws are
        /// not counted, or they would keep themselves alive forever.
        /// </summary>
        public static void EndFrame()
        {
            try
            {
                foreach (var kv in s_last)
                {
                    int now;
                    s_now.TryGetValue(kv.Key, out now);

                    Sprite who;
                    if (now >= kv.Value || !s_any.TryGetValue(kv.Key, out who)) continue;

                    for (var i = now; i < kv.Value; i++) who.Blank();
                }

                s_last.Clear();
                foreach (var kv in s_now) s_last[kv.Key] = kv.Value;
                s_now.Clear();
            }
            catch (Exception ex)
            {
                Log.Once("sprite-ledger", "The sprite ledger fell over: " + ex.Message);
            }
        }

        private void Blank()
        {
            if (_sprite == null) return;

            _sprite.Size = new SizeF(1f, 1f);
            _sprite.Position = new PointF(-8f, -8f);
            _sprite.Color = Color.FromArgb(0, 0, 0, 0);
            _sprite.Draw();
        }
    }
}
