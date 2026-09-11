using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VehicleTweaks.Core
{
    /// <summary>
    /// Reads the game's own code and memory, carefully.
    ///
    /// THE EXECUTABLE ON DISK IS ENCRYPTED AND THE ONE IN MEMORY IS NOT. Every offset this mod
    /// could not get from FiveM's hardcoded constants lives in an instruction somewhere in fifty
    /// megabytes of code, and the only copy of that code anyone can read is the one the game is
    /// running from. So this looks there: it walks the main module a region at a time, asking
    /// Windows first whether each region can be read, and searches each for a byte pattern with
    /// wildcards -- the same thing ScriptHookVDotNet does at startup to find everything it knows.
    ///
    /// AND IT NEVER DEREFERENCES A NUMBER IT HAS NOT CHECKED. A pointer read out of a struct is a
    /// guess until the page it points at is known to be mapped, and a wrong guess is an access
    /// violation, which .NET does not let a script catch. Readable asks Windows; Object asks
    /// whether the first thing at the address is a vtable inside the module, which is what
    /// every C++ object in this game starts with and almost nothing else does.
    /// </summary>
    internal static class Scan
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryBasicInformation
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public uint Alignment1;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
            public uint Alignment2;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualQuery(IntPtr lpAddress, out MemoryBasicInformation lpBuffer,
                                                  IntPtr dwLength);

        private const uint MemCommit = 0x1000;
        private const uint PageGuard = 0x100;
        private const uint PageNoAccess = 0x01;

        /// <summary>Regions bigger than this are read in pieces, with an overlap so a match on the seam is not missed.</summary>
        private const int Piece = 8 * 1024 * 1024;

        private static IntPtr _base;
        private static long _size;

        /// <summary>Where the game's own module starts and how far it runs, found once.</summary>
        private static bool Module()
        {
            if (_base != IntPtr.Zero) return true;

            try
            {
                var main = Process.GetCurrentProcess().MainModule;

                _base = main.BaseAddress;
                _size = main.ModuleMemorySize;

                return _size > 0;
            }
            catch (Exception ex)
            {
                Log.Once("scan-module", "Could not find the game's module: " + ex.Message);
                return false;
            }
        }

        /// <summary>Whether every byte from here for this long is mapped and may be read.</summary>
        public static bool Readable(IntPtr at, int length)
        {
            if (at == IntPtr.Zero || length <= 0) return false;

            var address = at.ToInt64();
            var end = address + length;

            while (address < end)
            {
                MemoryBasicInformation info;

                if (VirtualQuery(new IntPtr(address), out info, new IntPtr(Marshal.SizeOf(typeof(MemoryBasicInformation)))) == IntPtr.Zero)
                {
                    return false;
                }

                if (info.State != MemCommit) return false;
                if ((info.Protect & PageGuard) != 0 || (info.Protect & PageNoAccess) != 0) return false;

                address = info.BaseAddress.ToInt64() + info.RegionSize.ToInt64();
            }

            return true;
        }

        /// <summary>Whether this looks like a live C++ object: readable, and starting with a vtable in the module.</summary>
        public static bool Object(IntPtr at)
        {
            if (!Module() || !Readable(at, 8)) return false;

            var vtable = Marshal.ReadIntPtr(at).ToInt64();
            var start = _base.ToInt64();

            return vtable >= start && vtable < start + _size;
        }

        /// <summary>
        /// The first place in the game's code this pattern appears, or nothing.
        ///
        /// The pattern is written the way FiveM writes them -- "44 0F 2F 43 48 45 8D", with "?"
        /// for a byte that may be anything -- so a line lifted from that file is a line that works
        /// here unchanged.
        /// </summary>
        public static IntPtr Find(string pattern)
        {
            byte[] bytes;
            bool[] care;

            if (!Parse(pattern, out bytes, out care) || !Module()) return IntPtr.Zero;

            var address = _base.ToInt64();
            var end = address + _size;

            try
            {
                while (address < end)
                {
                    MemoryBasicInformation info;

                    if (VirtualQuery(new IntPtr(address), out info, new IntPtr(Marshal.SizeOf(typeof(MemoryBasicInformation)))) == IntPtr.Zero)
                    {
                        break;
                    }

                    var regionStart = info.BaseAddress.ToInt64();
                    var regionEnd = regionStart + info.RegionSize.ToInt64();

                    if (regionEnd > end) regionEnd = end;

                    var readable = info.State == MemCommit && (info.Protect & PageGuard) == 0 &&
                                   (info.Protect & PageNoAccess) == 0;

                    if (readable)
                    {
                        var hit = Search(regionStart, regionEnd, bytes, care);

                        if (hit != IntPtr.Zero) return hit;
                    }

                    address = regionEnd;
                }
            }
            catch (Exception ex)
            {
                Log.Once("scan-find", "Scanning the game's code fell over: " + ex.Message);
            }

            return IntPtr.Zero;
        }

        private static IntPtr Search(long start, long end, byte[] bytes, bool[] care)
        {
            var length = bytes.Length;
            var at = start;

            while (at < end)
            {
                var take = (int)Math.Min(Piece, end - at);

                if (take < length) break;

                var buffer = new byte[take];

                Marshal.Copy(new IntPtr(at), buffer, 0, take);

                var first = bytes[0];
                var last = take - length;

                for (var i = 0; i <= last; i++)
                {
                    if (care[0] && buffer[i] != first) continue;

                    var ok = true;

                    for (var j = 1; j < length; j++)
                    {
                        if (care[j] && buffer[i + j] != bytes[j])
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (ok) return new IntPtr(at + i);
                }

                // Step back by a pattern so a match straddling two pieces is still seen once.
                at += take - length + 1;
            }

            return IntPtr.Zero;
        }

        private static bool Parse(string pattern, out byte[] bytes, out bool[] care)
        {
            var parts = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            bytes = new byte[parts.Length];
            care = new bool[parts.Length];

            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "?" || parts[i] == "??")
                {
                    care[i] = false;
                    continue;
                }

                byte b;

                if (!byte.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber, null, out b))
                {
                    return false;
                }

                bytes[i] = b;
                care[i] = true;
            }

            return parts.Length > 0 && care[0];
        }
    }
}
