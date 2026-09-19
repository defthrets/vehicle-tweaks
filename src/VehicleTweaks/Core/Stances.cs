using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace VehicleTweaks.Core
{
    /// <summary>
    /// Stances written down, one line a car, so reloading the script does not lose them.
    ///
    /// THE DECORATORS NEVER WORKED, AND THE LOG SAYS SO: two megabytes of it without a single
    /// recall, ever. DECOR_REGISTER only takes before the game locks its decorator registry
    /// during session startup, and a script registering from its own constructor is always
    /// later than that -- so every write was ignored and every read came back empty, in
    /// silence, because that native reports failure by returning false and nobody was asking
    /// it. A stance survived getting out of the car, because the mod was still holding it in
    /// memory, and died the moment the script was reloaded.
    ///
    /// KEYED BY HANDLE, CHECKED BY MODEL. A handle is what the game calls a car for as long as
    /// that car exists, which covers exactly the case this is for: reloading the script under a
    /// game that is still running. It means nothing across a restart, so every line also
    /// carries the model name and is believed only if the car at that handle still answers to
    /// it -- a handle since reused by something else is thrown away rather than dressing a
    /// stranger in somebody's stance.
    ///
    /// NOT BY MODEL, WHICH IS WHAT THE FIRST VERSION OF THIS FILE DID. Filing a stance under
    /// "sultan" put it on every Sultan in the world, and being asked to stop doing that is why
    /// the file was deleted in favour of decorators that turned out not to work. The model is a
    /// check here and never a key.
    /// </summary>
    internal static class Stances
    {
        /// <summary>
        /// How many cars are remembered at once.
        ///
        /// Bounded because this file is only ever added to. The oldest goes when the room runs
        /// out, which is the right one to lose: a stance you set an hour ago on a car you have
        /// not been near since.
        /// </summary>
        private const int Most = 200;

        private sealed class Kept
        {
            public string Model;
            public float[] Values;
        }

        private static readonly Dictionary<int, Kept> Held = new Dictionary<int, Kept>();

        /// <summary>Handles in the order they were last written, so the oldest can be dropped.</summary>
        private static readonly List<int> Order = new List<int>();

        private static bool _loaded;
        private static int _width;

        /// <summary>What each value is as the car came, for padding a shorter old line.</summary>
        private static float[] _stock;

        /// <summary>Reads the file once. The width is how many numbers a stance has.</summary>
        public static void Load(int width, float[] stock)
        {
            if (_loaded) return;

            _loaded = true;
            _width = width;
            _stock = stock;

            try
            {
                var path = Paths.StanceFile;

                if (!File.Exists(path)) return;

                foreach (var line in File.ReadAllLines(path))
                {
                    var text = line.Trim();

                    if (text.Length == 0 || text[0] == ';') continue;

                    var bits = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    // AT LEAST ONE VALUE, not exactly _width of them: a line from before a value
                    // was added is short by that value, and is worth keeping with the newcomer
                    // padded from stock rather than thrown away whole.
                    if (bits.Length < 3) continue;

                    int handle;

                    if (!int.TryParse(bits[0], NumberStyles.Integer, CultureInfo.InvariantCulture,
                                      out handle)) continue;

                    var have = bits.Length - 2;
                    var values = new float[_width];
                    var sound = true;

                    for (var i = 0; i < _width; i++)
                    {
                        if (i >= have)
                        {
                            values[i] = _stock != null && i < _stock.Length ? _stock[i] : 0f;
                            continue;
                        }

                        if (float.TryParse(bits[2 + i], NumberStyles.Float, CultureInfo.InvariantCulture,
                                           out values[i])) continue;

                        sound = false;
                        break;
                    }

                    if (!sound) continue;

                    Remember(handle, bits[1], values);
                }

                Log.Info("Stances: " + Held.Count + " remembered from " +
                         Path.GetFileName(path) + ".");
            }
            catch (Exception ex)
            {
                Log.Warn("Stances could not be read, so cars start stock: " + ex.Message);
            }
        }

        /// <summary>What this car was left at, or null if it was never stanced or is not that car any more.</summary>
        public static float[] Get(int handle, string model)
        {
            Kept kept;

            if (!Held.TryGetValue(handle, out kept)) return null;

            // THE MODEL IS THE CHECK. A handle from before a restart may well be in use again,
            // and a Sultan's stance on a Rhino is worse than no stance at all.
            if (!Same(kept.Model, model))
            {
                Held.Remove(handle);
                Order.Remove(handle);
                return null;
            }

            return (float[])kept.Values.Clone();
        }

        /// <summary>Writes one car down and saves the file.</summary>
        public static void Put(int handle, string model, float[] values)
        {
            if (values == null || values.Length < _width) return;

            Remember(handle, model, values);
            Save();
        }

        /// <summary>Forgets one car and saves the file, for a stance put back to nought.</summary>
        public static void Drop(int handle)
        {
            if (!Held.Remove(handle)) return;

            Order.Remove(handle);
            Save();
        }

        private static void Remember(int handle, string model, float[] values)
        {
            Held[handle] = new Kept { Model = model ?? "?", Values = (float[])values.Clone() };

            Order.Remove(handle);
            Order.Add(handle);

            while (Order.Count > Most)
            {
                Held.Remove(Order[0]);
                Order.RemoveAt(0);
            }
        }

        private static bool Same(string a, string b)
        {
            // An unknown name matches anything: an add-on car whose name this build cannot look
            // up should still keep its stance, and the handle is doing the work either way.
            if (string.IsNullOrEmpty(a) || a == "?" || string.IsNullOrEmpty(b)) return true;

            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static void Save()
        {
            try
            {
                var text = new StringBuilder();

                text.AppendLine("; Vehicle Tweaks - stances, one car a line.");
                text.AppendLine(";");
                text.AppendLine("; Written when you change one and read when the mod loads, which is what makes a");
                text.AppendLine("; stance survive reloading the script under a running game.");
                text.AppendLine(";");
                text.AppendLine("; The first number is the handle the game gave that car, which is what it is");
                text.AppendLine("; called for as long as it exists and nothing at all once the game restarts - so");
                text.AppendLine("; the model name beside it is checked before a line is believed, and a handle");
                text.AppendLine("; since given to something else is thrown away rather than used.");
                text.AppendLine(";");
                text.AppendLine("; Delete this file and every car starts stock. Nothing else reads it.");
                text.AppendLine(";");
                text.AppendLine("; handle  model  camberF camberR trackF trackR heightF heightR size width " +
                                "drawnSize drawnWidth");

                foreach (var handle in Order)
                {
                    Kept kept;

                    if (!Held.TryGetValue(handle, out kept)) continue;

                    text.Append(handle.ToString(CultureInfo.InvariantCulture)).Append(' ')
                        .Append(kept.Model);

                    foreach (var value in kept.Values)
                    {
                        text.Append(' ').Append(value.ToString("0.####", CultureInfo.InvariantCulture));
                    }

                    text.AppendLine();
                }

                File.WriteAllText(Paths.StanceFile, text.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.Once("stances-save", "Stances could not be written down: " + ex.Message);
            }
        }
    }
}
