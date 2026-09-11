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
    /// Watches a car's memory for anything another mod changes, and says what and where.
    ///
    /// THE OTHER STANCER KNOWS WHERE THE FIELDS ARE, AND IT WILL SHOW US. VStancer works on this
    /// build and this mod cannot find two of the numbers it writes -- the drawn width most of
    /// all. So instead of looking for the numbers, this looks for the WRITES: every frame it
    /// copies the front left wheel, the car, the car's draw handler and every object hanging
    /// off that handler, and compares each with the frame before. A field that changes while
    /// somebody is moving a slider in the other mod is a field the other mod writes, and the log
    /// names it, with what it was and what it became.
    ///
    /// THE NOISE IS LEARNED FIRST. A car sat still is not still in memory -- positions jitter,
    /// timers count, forces settle -- so for the first two seconds nothing is reported and
    /// every field that moves on its own is marked as noise. After that, only a field that has
    /// never moved before is worth a line. Which means the car must be stopped and the other
    /// mod idle for those two seconds, and the line on the screen says when they are over.
    ///
    /// Off, and it costs nothing. On, it is a few kilobytes copied and compared a frame, and a
    /// log that fills up if the car moves.
    /// </summary>
    internal sealed class Watch
    {
        /// <summary>How long the noise is learned for, in frames, before anything is said.</summary>
        private const int Settle = 150;

        /// <summary>No more lines than this a second, or a moving car writes a novel.</summary>
        private const int MostPerSecond = 40;

        private sealed class Region
        {
            public string Name;
            public IntPtr At;
            public int Length;
            public int[] Last;
            public bool[] Noisy;
            public int Frames;
        }

        private readonly List<Region> _regions = new List<Region>();
        private int _car;
        private int _saidAt;
        private int _saidCount;
        private bool _settled;

        public void Update(Vehicle car)
        {
            try
            {
                if (car == null || !car.Exists())
                {
                    _regions.Clear();
                    _car = 0;
                    return;
                }

                if (car.Handle != _car)
                {
                    _regions.Clear();
                    _car = car.Handle;
                    _settled = false;

                    Log.Info("Watch: watching a " + (car.DisplayName ?? "car") + ". Sit still for a moment " +
                             "while the noise is learned, then change ONE thing in the other mod.");
                }

                Gather(car);

                var settled = true;

                foreach (var region in _regions)
                {
                    if (!Compare(region)) settled = false;
                }

                if (settled && !_settled)
                {
                    _settled = true;
                    Log.Info("Watch: noise learned. Anything from here on is somebody writing.");
                }

                Draw.Text(_settled ? "WATCHING  change one thing in VStancer, then check the log"
                                   : "WATCHING  learning the noise, sit still",
                          0.5f, 0.14f, 0.55f, Color.FromArgb(255, 245, 196, 60), 4, true);
            }
            catch (Exception ex)
            {
                Log.Once("watch", "Watching fell over: " + ex.Message);
            }
        }

        /// <summary>The regions worth watching this frame, kept if unchanged and reset if moved.</summary>
        private void Gather(Vehicle car)
        {
            var wanted = new List<KeyValuePair<string, IntPtr>>();
            var lengths = new Dictionary<string, int>();

            var vehicle = car.MemoryAddress;

            if (vehicle != IntPtr.Zero)
            {
                wanted.Add(new KeyValuePair<string, IntPtr>("car", vehicle));
                lengths["car"] = 0x1000;

                if (Scan.Readable(IntPtr.Add(vehicle, 0x48), 8))
                {
                    var handler = Marshal.ReadIntPtr(vehicle, 0x48);

                    if (Scan.Object(handler))
                    {
                        // AS FAR AS THE RENDER DATA, AND AS DEEP AS THE WIDTH. VStancer's own log
                        // puts the render data 0x4B0 into the handler and the drawn width 0xBA0
                        // into that, which the first version of this never reached: it stopped
                        // at 0x100 and 0x300, and would have watched VStancer write the two
                        // numbers that matter most without seeing either.
                        wanted.Add(new KeyValuePair<string, IntPtr>("handler", handler));
                        lengths["handler"] = Fits(handler, 0x800, 0x100);

                        for (var slot = 0; slot < 0x800; slot += 8)
                        {
                            if (!Scan.Readable(IntPtr.Add(handler, slot), 8)) continue;

                            var target = Marshal.ReadIntPtr(handler, slot);

                            if (target == handler || target == vehicle || !Scan.Object(target)) continue;

                            var name = slot == 0x4B0 ? "render data (handler+0x4B0)"
                                                     : "handler+0x" + slot.ToString("X") + " object";

                            wanted.Add(new KeyValuePair<string, IntPtr>(name, target));
                            lengths[name] = Fits(target, 0x1000, 0x200);
                        }
                    }
                }
            }

            foreach (var wheel in car.Wheels)
            {
                if (wheel.BoneId != VehicleWheelBoneId.WheelLeftFront) continue;

                if (wheel.MemoryAddress != IntPtr.Zero)
                {
                    wanted.Add(new KeyValuePair<string, IntPtr>("wheel", wheel.MemoryAddress));
                    lengths["wheel"] = 0x230;
                }
            }

            // Keep the regions that are still where they were; anything new or moved starts over.
            var kept = new List<Region>();

            foreach (var pair in wanted)
            {
                var length = lengths[pair.Key];

                if (!Scan.Readable(pair.Value, length)) continue;

                Region have = null;

                foreach (var region in _regions)
                {
                    if (region.Name == pair.Key && region.At == pair.Value)
                    {
                        have = region;
                        break;
                    }
                }

                kept.Add(have ?? new Region { Name = pair.Key, At = pair.Value, Length = length });
            }

            _regions.Clear();
            _regions.AddRange(kept);
        }

        /// <summary>As much of a region as can be read, wanting this much and settling for that much.</summary>
        private static int Fits(IntPtr at, int wanted, int least)
        {
            for (var length = wanted; length >= least; length /= 2)
            {
                if (Scan.Readable(at, length)) return length;
            }

            return least;
        }

        /// <summary>Compares one region with its last frame. Says whether its noise is learned yet.</summary>
        private bool Compare(Region region)
        {
            var count = region.Length / 4;
            var bytes = new byte[region.Length];

            Marshal.Copy(region.At, bytes, 0, region.Length);

            var now = new int[count];

            Buffer.BlockCopy(bytes, 0, now, 0, region.Length);

            if (region.Last == null)
            {
                region.Last = now;
                region.Noisy = new bool[count];
                region.Frames = 0;
                return false;
            }

            region.Frames++;

            var learning = region.Frames <= Settle;

            for (var i = 0; i < count; i++)
            {
                if (now[i] == region.Last[i]) continue;

                if (learning)
                {
                    region.Noisy[i] = true;
                    continue;
                }

                if (region.Noisy[i]) continue;

                Say(region, i * 4, region.Last[i], now[i]);
            }

            region.Last = now;

            return !learning;
        }

        private void Say(Region region, int offset, int was, int now)
        {
            var t = Game.GameTime;

            if (t - _saidAt >= 1000)
            {
                _saidAt = t;
                _saidCount = 0;
            }

            if (++_saidCount > MostPerSecond) return;

            Log.Info("Watch: " + region.Name + "+0x" + offset.ToString("X3") + "  " +
                     Show(was) + " -> " + Show(now));
        }

        /// <summary>A dword as a float when it reads like one, and as hex when it does not.</summary>
        private static string Show(int bits)
        {
            var f = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);

            if (!float.IsNaN(f) && !float.IsInfinity(f) && Math.Abs(f) < 1e6f &&
                (Math.Abs(f) > 1e-5f || bits == 0))
            {
                return f.ToString("0.0000");
            }

            return "0x" + bits.ToString("X8");
        }
    }
}
