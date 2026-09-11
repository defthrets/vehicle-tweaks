using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GTA;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The size a wheel is DRAWN at, which lives on the car and not on the wheel.
    ///
    /// THE WHEEL'S OWN RADIUS SHOWS AND ITS OWN WIDTH DOES NOT. The tyre radius at 0x110 is what
    /// the game draws a stock wheel to, so writing it changes what you see; the width beside it
    /// is the collider's, and changes the skid marks and nothing else. The size and width you
    /// can see on a FITTED wheel are two factors the game keeps for the Arena wheel sizes, on the
    /// vehicle's render data: each a proportion of the model, each one as the car came.
    ///
    /// THREE HOPS FROM THE VEHICLE, AND EVERY ONE IS CHECKED. Vehicle to draw handler, draw
    /// handler to the streamed render data, then the two floats. FiveM finds the offsets by
    /// pattern, so this searches the game's code in memory for the same patterns first; where
    /// they do not fit -- and on Enhanced none of the three do -- it falls back to the offsets
    /// VStancer found on this build and wrote in its own log, which is where the fallback below
    /// comes from. Before any car is written the chain is walked on it, every pointer checked,
    /// and the two factors have to read like factors -- or the whole thing is off, and says so.
    ///
    /// AND STOCK WHEELS IGNORE IT, which is the game's rule and not this mod's. The factors
    /// scale a streamed wheel drawable, and a stock wheel is part of the car's own model. So a
    /// car that has not been to a wheel shop keeps the size it came with whatever the slider
    /// says, and the log says why, once per car.
    /// </summary>
    internal sealed class Drawn
    {
        /// <summary>The three patterns, FiveM's, and where each offset sits in what they find.</summary>
        private const string HandlerPattern = "44 0F 2F 43 48 45 8D";       // byte at +4
        private const string GfxPattern = "4C 8D 48 ? 80 E1 01";             // dword at -4
        private const string SizesPattern = "48 89 01 B8 00 00 80 3F 66 44 89 51"; // byte at +20, dword at +23

        /// <summary>
        /// What VStancer 0.4.0 found on Enhanced 1.0.1158.13 and wrote in its log, for when the
        /// patterns do not fit. Trusted only after a real car reads sanely through them.
        /// </summary>
        private const int EnhancedHandler = 0x48;
        private const int EnhancedGfx = 0x4B0;
        private const int EnhancedSize = 0x8;
        private const int EnhancedWidth = 0xBA0;

        private int _handler = -1;
        private int _gfx = -1;
        private int _size = -1;
        private int _width = -1;

        private bool _looked;
        private bool _broken;

        /// <summary>Render data that has read sanely once, so it is not re-read every frame.</summary>
        private readonly HashSet<long> _trusted = new HashSet<long>();

        /// <summary>Cars already told about their stock wheels, so they are told once.</summary>
        private readonly HashSet<int> _toldStock = new HashSet<int>();

        /// <summary>Finds the offsets once, and says where they came from.</summary>
        public void Find()
        {
            if (_looked) return;

            _looked = true;

            try
            {
                var started = Environment.TickCount;

                var a = Scan.Find(HandlerPattern);
                var b = Scan.Find(GfxPattern);
                var c = Scan.Find(SizesPattern);

                if (a != IntPtr.Zero && b != IntPtr.Zero && c != IntPtr.Zero)
                {
                    _handler = Marshal.ReadByte(a, 4);
                    _gfx = Marshal.ReadInt32(b, -4);
                    _size = Marshal.ReadByte(c, 20);
                    _width = Marshal.ReadInt32(c, 23);

                    Log.Info("Drawn wheels: the patterns fit this build (" + (Environment.TickCount - started) +
                             " ms): draw handler +0x" + _handler.ToString("X") + ", render data +0x" +
                             _gfx.ToString("X") + ", size +0x" + _size.ToString("X") + ", width +0x" +
                             _width.ToString("X") + ".");
                    return;
                }

                _handler = EnhancedHandler;
                _gfx = EnhancedGfx;
                _size = EnhancedSize;
                _width = EnhancedWidth;

                Log.Info("Drawn wheels: " + Missing(a, b, c) + " of the three patterns do not fit this " +
                         "build (" + (Environment.TickCount - started) + " ms), so the offsets are the " +
                         "ones VStancer found on Enhanced: draw handler +0x48, render data +0x4B0, size " +
                         "+0x8, width +0xBA0. They are trusted once a car reads sanely through them.");
            }
            catch (Exception ex)
            {
                _broken = true;
                Log.Warn("Drawn wheels: looking for them fell over, so the drawn sizes are off: " + ex.Message);
            }
        }

        private static string Missing(IntPtr a, IntPtr b, IntPtr c)
        {
            var n = (a == IntPtr.Zero ? 1 : 0) + (b == IntPtr.Zero ? 1 : 0) + (c == IntPtr.Zero ? 1 : 0);

            return n == 3 ? "all" : n == 2 ? "two" : "one";
        }

        /// <summary>Writes the two factors for one car, this frame, if the chain is trusted and the car can show them.</summary>
        public void Write(Vehicle car, float size, float width)
        {
            if (_broken || _handler < 0) return;

            try
            {
                var gfx = Gfx(car);

                if (gfx == IntPtr.Zero)
                {
                    TellStock(car, "has no render data yet");
                    return;
                }

                if (!Scan.Readable(IntPtr.Add(gfx, Math.Max(_size, _width)), 4)) return;

                var key = gfx.ToInt64();

                if (!_trusted.Contains(key))
                {
                    // THE FACTORS HAVE TO READ LIKE FACTORS before they are written. A wrong offset
                    // reads as a pointer's half, a count, nought or garbage; a right one reads as
                    // one, or as whatever the last mod left there, and either is a small number.
                    var s = Read(gfx, _size);
                    var w = Read(gfx, _width);

                    if (float.IsNaN(s) || float.IsNaN(w) || s < 0.2f || s > 5f || w < 0.2f || w > 5f)
                    {
                        _broken = true;
                        Log.Warn("Drawn wheels: through these offsets the factors read " + s + " and " + w +
                                 " rather than about one, so the offsets are wrong for this build and the " +
                                 "drawn sizes are off.");
                        return;
                    }

                    if (_trusted.Count > 64) _trusted.Clear();

                    _trusted.Add(key);

                    Log.Info("Drawn wheels: the chain reads sanely on this car (" + s.ToString("0.00") + ", " +
                             w.ToString("0.00") + "), so the drawn sizes are on.");
                }

                Put(gfx, _size, size);
                Put(gfx, _width, width);

                if (OnStockWheels(car)) TellStock(car, "is on its stock wheels");
            }
            catch (Exception ex)
            {
                Log.Once("drawn-write", "Could not write a drawn wheel size: " + ex.Message);
            }
        }

        /// <summary>Whether the car is on the wheels it came with, which the factors do not touch.</summary>
        public static bool OnStockWheels(Vehicle car)
        {
            try
            {
                return car.Mods[VehicleModType.FrontWheel].Index < 0;
            }
            catch
            {
                return false;
            }
        }

        private void TellStock(Vehicle car, string why)
        {
            if (_toldStock.Contains(car.Handle)) return;

            if (_toldStock.Count > 64) _toldStock.Clear();

            _toldStock.Add(car.Handle);

            Log.Info("Drawn wheels: this car " + why + ", and the drawn sizes only scale a FITTED wheel " +
                     "-- the game's rule, not this mod's. Fit any rim and they take.");
        }

        /// <summary>The render data for a car, walked fresh every time, because it is streamed and can move.</summary>
        private IntPtr Gfx(Vehicle car)
        {
            var vehicle = car.MemoryAddress;

            if (vehicle == IntPtr.Zero || !Scan.Readable(IntPtr.Add(vehicle, _handler), 8)) return IntPtr.Zero;

            var handler = Marshal.ReadIntPtr(vehicle, _handler);

            if (!Scan.Object(handler) || !Scan.Readable(IntPtr.Add(handler, _gfx), 8)) return IntPtr.Zero;

            var gfx = Marshal.ReadIntPtr(handler, _gfx);

            return Scan.Object(gfx) ? gfx : IntPtr.Zero;
        }

        private static float Read(IntPtr at, int offset)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(at, offset)), 0);
        }

        private static void Put(IntPtr at, int offset, float value)
        {
            Marshal.WriteInt32(at, offset, BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
        }
    }
}
