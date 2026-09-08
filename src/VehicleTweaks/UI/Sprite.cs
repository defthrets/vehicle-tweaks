using System;
using System.Drawing;
using System.IO;
using GTA.UI;
using VehicleTweaks.Core;

namespace VehicleTweaks.UI
{
    /// <summary>
    /// A PNG from disk, drawn on the screen at its own shape.
    ///
    /// CUSTOMSPRITE IS THE WHOLE TRICK, and it is the same trick Fumes uses for its pump and its
    /// digits: it loads an ordinary PNG off disk at runtime through ScriptHookVDotNet's own
    /// texture loader. The only alternative is packing a .ytd into one of the game's archives,
    /// which turns a mod anybody can drop into scripts\ into an asset mod needing OpenIV, a limit
    /// adjuster and a different install per game edition. For one word in one font.
    ///
    /// The PNG is white on transparent; the colour comes from the tint at draw time, so it fades
    /// with the panel exactly as the text it replaced did.
    ///
    /// SIZED BY THE IMAGE'S OWN SHAPE, given once. CustomSprite does not know how big the file
    /// is, and a size guessed at is a title stretched to whatever box it was given -- so the
    /// aspect is measured when the PNG is made and written down here, and the height is the only
    /// thing the caller chooses.
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

        private CustomSprite _sprite;
        private bool _missing;

        public Sprite(string file, float aspect)
        {
            _path = Paths.Asset(file);
            _aspect = aspect;
        }

        /// <summary>
        /// Draws it into a box the caller has already worked out, shape and all.
        ///
        /// FOR THE THINGS THAT USED TO BE RECTANGLES. A seven-segment digit and a tab icon each
        /// had a box before they had a picture, and the picture was made to that box exactly --
        /// so the caller's width and height are the truth here and the aspect is not consulted.
        /// </summary>
        public bool DrawBox(float left, float top, float width, float height, Color tint)
        {
            if (_missing) return false;

            try
            {
                if (_sprite == null)
                {
                    if (!File.Exists(_path))
                    {
                        _missing = true;
                        Log.Warn("No " + Path.GetFileName(_path) + " beside the log; drawing that " +
                                 "out of rectangles instead. Deploy copies it from assets.");
                        return false;
                    }

                    _sprite = new CustomSprite(_path, new SizeF(64f, 64f), new PointF(0f, 0f),
                                               tint, 0f, false);
                }

                _sprite.Size = new SizeF(width * CanvasW, height * CanvasH);
                _sprite.Position = new PointF(left * CanvasW, top * CanvasH);
                _sprite.Color = tint;
                _sprite.Draw();

                return true;
            }
            catch (Exception ex)
            {
                _missing = true;
                Log.Once("sprite", "Could not draw " + Path.GetFileName(_path) + ": " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Draws it with its top-left at the given screen fractions, so tall, keeping its shape.
        /// Says whether it did, so a caller can draw something else when the file is not there.
        /// </summary>
        public bool Draw(float left, float top, float height, Color tint)
        {
            if (_missing) return false;

            try
            {
                if (_sprite == null)
                {
                    if (!File.Exists(_path))
                    {
                        // ONCE, AND THEN THE FALLBACK FOREVER. A missing picture is not a reason
                        // to check the disk sixty times a second, and the text it stands in for
                        // is perfectly good.
                        _missing = true;
                        Log.Warn("No " + Path.GetFileName(_path) + " beside the log; drawing the " +
                                 "title as text instead. Deploy copies it from assets\\.");
                        return false;
                    }

                    _sprite = new CustomSprite(_path, new SizeF(64f, 64f), new PointF(0f, 0f),
                                               tint, 0f, false);
                }

                var h = height * CanvasH;

                _sprite.Size = new SizeF(h * _aspect, h);
                _sprite.Position = new PointF(left * CanvasW, top * CanvasH);
                _sprite.Color = tint;
                _sprite.Draw();

                return true;
            }
            catch (Exception ex)
            {
                _missing = true;
                Log.Once("sprite", "Could not draw " + Path.GetFileName(_path) + ": " + ex.Message);
                return false;
            }
        }
    }
}
