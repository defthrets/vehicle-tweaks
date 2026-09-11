using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using GTA;
using VehicleTweaks.Core;
using VehicleTweaks.UI;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The size a wheel is DRAWN at, which lives on the car and not on the wheel.
    ///
    /// THE WHEEL'S OWN RADIUS SHOWS AND ITS OWN WIDTH DOES NOT. The tyre radius at 0x110 is what
    /// the game draws the wheel to, so writing it changes what you see; the width beside it is
    /// the collider's and changes nothing visible. The width you can see is one of two numbers
    /// the game keeps for the Arena wheel sizes, on the vehicle's render data: a size factor and
    /// a width factor, each a proportion of the model, each one point nought as the car came.
    ///
    /// THREE HOPS FROM THE VEHICLE, AND EVERY ONE IS CHECKED. Vehicle to draw handler, draw
    /// handler to the streamed render data, then the two floats. The offsets for all three come
    /// from FiveM's code, which finds them by pattern rather than hardcoding them -- so this
    /// searches the game's code for the same patterns at runtime, through Scan, and reads the
    /// offsets out of the instructions it finds. A pattern that does not match this build says
    /// so in the log rather than guessing. And before anything is trusted the whole chain is
    /// walked on a real car and both floats have to read as one: a chain that leads anywhere
    /// else is not used.
    ///
    /// WHEN THE PATTERNS DO NOT MATCH, IT LOOKS THE OTHER WAY. The draw handler is at 0x48 on
    /// every build anyone has looked at; from it, every pointer to a live object is a candidate
    /// for the render data, and in each candidate every float that reads exactly one is a
    /// candidate for the size or the width. That list is short, and the sweep in Trial pushes
    /// each for a moment with the offsets on screen, so a person can say which one fattened the
    /// wheel.
    /// </summary>
    internal sealed class Drawn
    {
        /// <summary>The three patterns, FiveM's, and where each offset sits in what they find.</summary>
        private const string HandlerPattern = "44 0F 2F 43 48 45 8D";       // byte at +4
        private const string GfxPattern = "4C 8D 48 ? 80 E1 01";             // dword at -4
        private const string SizesPattern = "48 89 01 B8 00 00 80 3F 66 44 89 51"; // byte at +20, dword at +23

        /// <summary>Where the draw handler sits when the pattern for it cannot be found.</summary>
        private const int UsualHandler = 0x48;

        private int _handler = -1;
        private int _gfx = -1;
        private int _size = -1;
        private int _width = -1;

        private bool _looked;
        private bool _found;

        public bool Found => _found;

        /// <summary>Looks for the chain once, on the first car that needs it, and says what it found.</summary>
        public void Find(Vehicle car)
        {
            if (_looked) return;

            _looked = true;

            try
            {
                var started = Environment.TickCount;

                var a = Scan.Find(HandlerPattern);
                var b = Scan.Find(GfxPattern);
                var c = Scan.Find(SizesPattern);

                _handler = a != IntPtr.Zero ? Marshal.ReadByte(a, 4) : UsualHandler;
                _gfx = b != IntPtr.Zero ? Marshal.ReadInt32(b, -4) : -1;
                _size = c != IntPtr.Zero ? Marshal.ReadByte(c, 20) : -1;
                _width = c != IntPtr.Zero ? Marshal.ReadInt32(c, 23) : -1;

                Log.Info("Drawn wheels: looked through the game's code in " +
                         (Environment.TickCount - started) + " ms. Draw handler " +
                         (a != IntPtr.Zero ? "at +0x" + _handler.ToString("X") : "pattern not found, assuming +0x48") +
                         "; render data " + (b != IntPtr.Zero ? "at +0x" + _gfx.ToString("X") : "pattern not found") +
                         "; sizes " + (c != IntPtr.Zero ? "at +0x" + _size.ToString("X") + " and +0x" + _width.ToString("X")
                                                        : "pattern not found") + ".");

                if (_gfx < 0 || _size < 0 || _width < 0)
                {
                    Log.Info("Drawn wheels: not every offset was found, so the drawn width is off. " +
                             "With the probe on, moving the width slider sweeps for it instead.");
                    return;
                }

                var gfx = Gfx(car);

                if (gfx == IntPtr.Zero)
                {
                    Log.Warn("Drawn wheels: the chain from the car does not lead to an object, so the " +
                             "offsets are not trusted and the drawn width is off.");
                    return;
                }

                var size = Read(gfx, _size);
                var width = Read(gfx, _width);

                if (Math.Abs(size - 1f) > 0.01f || Math.Abs(width - 1f) > 0.01f)
                {
                    Log.Warn("Drawn wheels: the chain leads to an object but the sizes read " +
                             size.ToString("0.000") + " and " + width.ToString("0.000") +
                             " rather than one, so they are not trusted and the drawn width is off.");
                    return;
                }

                _found = true;

                Log.Info("Drawn wheels: found. Both factors read 1.000 on this car, so the width " +
                         "slider now reaches the wheel you can see.");
            }
            catch (Exception ex)
            {
                Log.Warn("Drawn wheels: looking for them fell over: " + ex.Message);
            }
        }

        /// <summary>Writes the drawn width factor for one car, this frame, if the chain is trusted.</summary>
        public void Write(Vehicle car, float width)
        {
            if (!_found) return;

            try
            {
                var gfx = Gfx(car);

                if (gfx == IntPtr.Zero || !Scan.Readable(IntPtr.Add(gfx, _width), 4)) return;

                Put(gfx, _width, width);
            }
            catch (Exception ex)
            {
                Log.Once("drawn-write", "Could not write a drawn wheel width: " + ex.Message);
            }
        }

        /// <summary>The render data for a car, walked fresh every time, because it is streamed and can move.</summary>
        private IntPtr Gfx(Vehicle car)
        {
            if (_handler < 0 || _gfx < 0) return IntPtr.Zero;

            var vehicle = car.MemoryAddress;

            if (vehicle == IntPtr.Zero || !Scan.Readable(IntPtr.Add(vehicle, _handler), 8)) return IntPtr.Zero;

            var handler = Marshal.ReadIntPtr(vehicle, _handler);

            if (!Scan.Object(handler) || !Scan.Readable(IntPtr.Add(handler, _gfx), 8)) return IntPtr.Zero;

            var gfx = Marshal.ReadIntPtr(handler, _gfx);

            return Scan.Object(gfx) ? gfx : IntPtr.Zero;
        }

        // ==================================================================
        // The sweep, for a build the patterns do not fit
        // ==================================================================

        private const int TrialMs = 2500;
        private const float TrialTo = 1.7f;

        /// <summary>One thing to try: which pointer off the draw handler, and which float in what it points at.</summary>
        private sealed class Guess
        {
            public int Gfx;
            public int At;
        }

        private List<Guess> _guesses;
        private int _guessCar;
        private int _guessIndex = -1;
        private int _guessAt;
        private float _guessWas;
        private bool _guessDone;

        /// <summary>
        /// Tries every float that reads one in every object hanging off the draw handler, in turn.
        ///
        /// Runs only when the patterns did not fit, the probe is on, and the width slider has
        /// been moved off one -- the sign that somebody is looking at the wheels.
        /// </summary>
        public void Trial(Vehicle car, int now, bool wanted)
        {
            if (_found || _guessDone || !_looked || car == null || !car.Exists()) return;

            if (_guessIndex < 0 && !wanted) return;

            try
            {
                var vehicle = car.MemoryAddress;

                if (vehicle == IntPtr.Zero || !Scan.Readable(IntPtr.Add(vehicle, _handler), 8)) return;

                var handler = Marshal.ReadIntPtr(vehicle, _handler);

                if (!Scan.Object(handler)) return;

                if (_guesses == null || _guessCar != car.Handle)
                {
                    _guesses = Guesses(handler);
                    _guessCar = car.Handle;
                    _guessIndex = -1;

                    Log.Info("Drawn wheels: sweeping " + _guesses.Count + " float(s) that read one in the " +
                             "objects off the draw handler, " + (TrialMs / 1000f).ToString("0.0") +
                             " s each. Watch the wheels.");

                    if (_guesses.Count == 0)
                    {
                        _guessDone = true;
                        return;
                    }
                }

                if (_guessIndex < 0 || now - _guessAt >= TrialMs)
                {
                    if (_guessIndex >= 0 && _guessIndex < _guesses.Count) Restore(handler, _guesses[_guessIndex]);

                    _guessIndex++;

                    if (_guessIndex >= _guesses.Count)
                    {
                        _guessDone = true;
                        Log.Info("Drawn wheels: sweep done.");
                        return;
                    }

                    var g = _guesses[_guessIndex];
                    var gfx = Marshal.ReadIntPtr(handler, g.Gfx);

                    _guessWas = Read(gfx, g.At);
                    _guessAt = now;

                    Log.Info("Drawn wheels: trying handler+0x" + g.Gfx.ToString("X") + " -> +0x" +
                             g.At.ToString("X") + " (" + (_guessIndex + 1) + " of " + _guesses.Count +
                             "), which reads " + _guessWas.ToString("0.000") + ".");
                }

                var guess = _guesses[_guessIndex];
                var target = Marshal.ReadIntPtr(handler, guess.Gfx);

                if (Scan.Object(target) && Scan.Readable(IntPtr.Add(target, guess.At), 4))
                {
                    Put(target, guess.At, TrialTo);
                }

                Draw.Text("DRAWN WHEEL PROBE  handler+0x" + guess.Gfx.ToString("X") + " -> +0x" +
                          guess.At.ToString("X") + "   " + (_guessIndex + 1) + " / " + _guesses.Count +
                          "   watch the wheels", 0.5f, 0.14f, 0.55f, Color.FromArgb(255, 245, 196, 60),
                          4, true);
            }
            catch (Exception ex)
            {
                Log.Once("drawn-trial", "The drawn wheel sweep fell over: " + ex.Message);
                _guessDone = true;
            }
        }

        private void Restore(IntPtr handler, Guess g)
        {
            var target = Marshal.ReadIntPtr(handler, g.Gfx);

            if (Scan.Object(target) && Scan.Readable(IntPtr.Add(target, g.At), 4)) Put(target, g.At, _guessWas);
        }

        /// <summary>Every float reading one, in every object the draw handler points at, pairs first.</summary>
        private static List<Guess> Guesses(IntPtr handler)
        {
            var pairs = new List<Guess>();
            var singles = new List<Guess>();

            for (var slot = 0; slot < 0x100; slot += 8)
            {
                if (!Scan.Readable(IntPtr.Add(handler, slot), 8)) continue;

                var target = Marshal.ReadIntPtr(handler, slot);

                if (target == handler || !Scan.Object(target) || !Scan.Readable(target, 0x200)) continue;

                var ones = new List<int>();

                for (var off = 8; off < 0x200; off += 4)
                {
                    if (Marshal.ReadInt32(target, off) == 0x3F800000) ones.Add(off);
                }

                // TWO ONES BESIDE EACH OTHER IS THE SIGNATURE: the size and the width are set to
                // one together, and the code that sets them writes one and then the other.
                var into = ones.Count >= 2 ? pairs : singles;

                foreach (var off in ones) into.Add(new Guess { Gfx = slot, At = off });
            }

            pairs.AddRange(singles);
            return pairs;
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
