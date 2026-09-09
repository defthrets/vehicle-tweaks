using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace VehicleTweaks.Core
{
    /// <summary>
    /// The stance you set on a car, kept for that car, in its own file beside the log.
    ///
    /// BY MODEL, BECAUSE A CAR HAS NO NAME THAT SURVIVES A SAVE. This is the honest limit of the
    /// feature and it is worth saying plainly: a vehicle's handle is made up when it is created
    /// and thrown away when it is not, so the Panto you stanced this evening is a different
    /// object from the Panto in tomorrow's session even when it is the same car in the story.
    /// What DOES survive is what it is -- "panto" -- so that is the key. Every Panto you drive
    /// gets the stance you gave a Panto, which is what a person means by "my car keeps it" right
    /// up until they own two of the same model.
    ///
    /// ITS OWN FILE, NOT THE SETTINGS. VehicleTweaks.ini is written by hand and read by a person;
    /// this could grow a section per vehicle in the game and would bury it. It is also DATA
    /// rather than settings -- nothing in here was decided, it was all recorded -- so it is
    /// rewritten wholesale rather than edited in place, which is the opposite of the rule the
    /// settings file lives by and right for the same reason.
    ///
    /// BESIDE THE LOG, NOT BESIDE THE DLL. Writable falls back to Documents when scripts\ cannot
    /// be written to, and a stance nobody can save is worse than one nobody can read.
    /// </summary>
    internal sealed class Stances
    {
        /// <summary>Camber, track and height, front then rear -- the order the file writes them.</summary>
        public const int Values = 6;

        private static readonly string[] Keys =
        {
            "CamberFront", "CamberRear", "TrackFront", "TrackRear", "HeightFront", "HeightRear",
        };

        private readonly Dictionary<string, float[]> _stances =
            new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);

        private readonly string _path;

        public Stances(string path)
        {
            _path = path;
            Read();
        }

        /// <summary>What was remembered for this model, or null for a car nobody has stanced.</summary>
        public float[] Get(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;

            float[] found;

            return _stances.TryGetValue(model, out found) ? (float[])found.Clone() : null;
        }

        /// <summary>
        /// Remembers a stance and writes the file, or forgets the model when it is all noughts.
        ///
        /// A CAR PUT BACK TO STOCK IS NOT A STANCE WORTH KEEPING. Left in, it would be a section
        /// per car you ever sat in, all of them saying nothing.
        /// </summary>
        public void Put(string model, float[] values)
        {
            if (string.IsNullOrEmpty(model) || values == null || values.Length != Values) return;

            var flat = true;

            foreach (var v in values)
            {
                if (Math.Abs(v) >= 0.0005f) flat = false;
            }

            if (flat)
            {
                if (!_stances.Remove(model)) return;
            }
            else
            {
                _stances[model] = (float[])values.Clone();
            }

            Write();
        }

        private void Read()
        {
            try
            {
                if (!File.Exists(_path)) return;

                var model = (string)null;
                var values = (float[])null;

                foreach (var raw in File.ReadAllLines(_path))
                {
                    var line = raw.Trim();

                    if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

                    if (line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        Keep(model, values);

                        model = line.Substring(1, line.Length - 2).Trim();
                        values = new float[Values];
                        continue;
                    }

                    if (values == null) continue;

                    var split = line.IndexOf('=');

                    if (split <= 0) continue;

                    var key = line.Substring(0, split).Trim();
                    var text = line.Substring(split + 1).Trim();

                    for (var i = 0; i < Keys.Length; i++)
                    {
                        if (!string.Equals(key, Keys[i], StringComparison.OrdinalIgnoreCase)) continue;

                        float value;

                        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
                                           out value))
                        {
                            values[i] = value;
                        }

                        break;
                    }
                }

                Keep(model, values);

                Log.Info("Stances: " + _stances.Count + " remembered from " +
                         Path.GetFileName(_path) + ".");
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read the remembered stances, so none are: " + ex.Message);
            }
        }

        private void Keep(string model, float[] values)
        {
            if (string.IsNullOrEmpty(model) || values == null) return;

            _stances[model] = values;
        }

        private void Write()
        {
            try
            {
                var text = new StringBuilder();

                text.AppendLine("; STANCES, REMEMBERED PER VEHICLE MODEL.");
                text.AppendLine(";");
                text.AppendLine("; Written by the mod rather than by you, and rewritten whole every time it changes -");
                text.AppendLine("; so anything you add here that is not a stance will not survive. Editing the numbers");
                text.AppendLine("; is fine, and so is deleting a section to put that model back to stock.");
                text.AppendLine(";");
                text.AppendLine("; The key is the MODEL, because a car has no name that survives a save: a vehicle's");
                text.AppendLine("; handle is made up when it is created and thrown away when it is not. Every Panto you");
                text.AppendLine("; drive gets the stance you gave a Panto.");
                text.AppendLine(";");
                text.AppendLine("; Camber is in degrees, track in metres, height is a hydraulic raise factor.");
                text.AppendLine("; Turn StanceRemember off in VehicleTweaks.ini and none of this is read or written.");

                var models = new List<string>(_stances.Keys);

                models.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (var model in models)
                {
                    var values = _stances[model];

                    text.AppendLine();
                    text.AppendLine("[" + model + "]");

                    for (var i = 0; i < Keys.Length; i++)
                    {
                        text.AppendLine(Keys[i] + " = " +
                                        values[i].ToString("0.####", CultureInfo.InvariantCulture));
                    }
                }

                var folder = Path.GetDirectoryName(_path);

                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.WriteAllText(_path, text.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Once("stances-write", "Could not save the stance: " + ex.Message);
            }
        }
    }
}
