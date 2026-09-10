using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace VehicleTweaks.Core
{
    /// <summary>
    /// Resolves where Vehicle Tweaks reads and writes.
    ///
    /// Lifted from Fumes, which lifted it from Hoodrich, because the lesson behind it cost
    /// days there: SHVDN SHADOW-COPIES script assemblies into the .NET download cache, so
    /// Assembly.Location points at AppData\Local\assembly\dl3\... and not at scripts\.
    /// Anything hung off it silently "does not exist" and the mod runs on built-in defaults
    /// forever without a single exception.
    ///
    /// So no single path API is trusted. Several candidates are tested against a file we know
    /// we shipped, and the first that actually holds it wins.
    /// </summary>
    internal static class Paths
    {
        private static string _scripts;

        /// <summary>The game's scripts\ folder.</summary>
        public static string Scripts
        {
            get
            {
                if (_scripts != null) return _scripts;

                var candidates = new List<string>();

                // SHVDN builds its script AppDomain with the scripts folder as the base.
                TryAdd(candidates, SafeGet(() => AppDomain.CurrentDomain.BaseDirectory));

                var cwd = SafeGet(Directory.GetCurrentDirectory);
                if (!string.IsNullOrEmpty(cwd))
                {
                    TryAdd(candidates, Path.Combine(cwd, "scripts"));
                    TryAdd(candidates, cwd);
                }

                // Last resort, and only because an unshadowed load would still be correct.
                TryAdd(candidates, SafeGet(() =>
                {
                    var loc = Assembly.GetExecutingAssembly().Location;
                    return string.IsNullOrEmpty(loc) ? null : Path.GetDirectoryName(loc);
                }));

                foreach (var dir in candidates)
                {
                    if (LooksLikeOurFolder(dir)) { _scripts = dir; return _scripts; }
                }

                _scripts = candidates.Count > 0 ? candidates[0] : cwd ?? ".";
                return _scripts;
            }
        }

        /// <summary>
        /// True when this folder is the one the deploy puts us in.
        ///
        /// The ini is the only file this mod ships -- there is no data folder to corroborate it
        /// with, which is what Fumes used as a second test. So the fallback is the folder's own
        /// NAME: SHVDN loads scripts out of a directory called scripts, and a player who has
        /// deleted the ini has not moved the game. A weaker test than two shipped files, and
        /// weak in the harmless direction: getting it wrong means reading an ini that is not
        /// there and running on defaults, which is what a deleted ini means anyway.
        /// </summary>
        private static bool LooksLikeOurFolder(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
                if (File.Exists(Path.Combine(dir, "VehicleTweaks.ini"))) return true;

                return string.Equals(new DirectoryInfo(dir).Name, "scripts",
                                     StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void TryAdd(List<string> list, string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;

            try
            {
                dir = Path.GetFullPath(dir.TrimEnd(Path.DirectorySeparatorChar));
                if (Directory.Exists(dir) && !list.Contains(dir)) list.Add(dir);
            }
            catch
            {
                // Unusable path; skip it.
            }
        }

        private static string SafeGet(Func<string> get)
        {
            try { return get(); }
            catch { return null; }
        }

        private static string _writable;

        /// <summary>
        /// Where the log goes.
        ///
        /// The game normally lives under Program Files, which an unelevated process cannot
        /// write to -- and GTA5.exe is unelevated. Reads work, so the ini loads fine, while
        /// every write fails silently and there is no log at all. Since the log is this mod's
        /// only voice, that failure is total: a silent mod that cannot write its log is
        /// indistinguishable from one that never loaded. Fall back to Documents the moment the
        /// game folder proves unwritable, rather than asking anybody to run as administrator.
        /// </summary>
        public static string Writable
        {
            get
            {
                if (_writable != null) return _writable;

                var preferred = Path.Combine(Scripts, "VehicleTweaks");
                if (IsWritable(preferred))
                {
                    _writable = preferred;
                    return _writable;
                }

                var fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VehicleTweaks");

                try
                {
                    if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
                }
                catch
                {
                    fallback = Path.Combine(Path.GetTempPath(), "VehicleTweaks");
                    try { if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback); }
                    catch { /* nothing left to try */ }
                }

                _writable = fallback;
                return _writable;
            }
        }

        /// <summary>True when a real file can actually be created here.</summary>
        private static bool IsWritable(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var probe = Path.Combine(dir, ".vt_write_test");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Read, and sitting beside the dll where a player expects to find it.</summary>
        public static string Ini => Path.Combine(Scripts, "VehicleTweaks.ini");

        /// <summary>WRITTEN, so it follows the writability fallback rather than sitting next to the dll.</summary>
        public static string LogFile => Path.Combine(Writable, "VehicleTweaks.log");

        /// <summary>The game's own folder, which is where the other mods keep what they know.</summary>
        public static string Game => Path.GetDirectoryName(Scripts);

        /// <summary>Extra model names the player keeps by hand. See Models.Found.</summary>
        public static string ModelsFile => Path.Combine(Writable, "VehicleTweaks.models.txt");

        /// <summary>
        /// A picture shipped with the mod, next to the log.
        ///
        /// NEXT TO THE DLL'S OWN FOLDER, NOT THE WRITABLE ONE. Writable can fall back to Documents
        /// when scripts\ is locked down, and an asset that came out of the zip is where the zip
        /// put it regardless of where the log has to go.
        /// </summary>
        public static string Asset(string file) => Path.Combine(Scripts, "VehicleTweaks", file);
    }
}
