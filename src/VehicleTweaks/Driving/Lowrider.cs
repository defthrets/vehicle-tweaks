using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// He drives everything the way he drives a lowrider: sat back, one arm through the window.
    ///
    /// THE SEAT CONTEXT DID NOT WORK, AND THE LOG IS WHY WE KNOW. The first version asked the game
    /// for a different seat context -- the mechanism the game itself uses to choose which
    /// animation a ped sits in -- and read the resulting clipset back before and after to see
    /// whether anything changed. It never did: clipsets 2462687501 and 3332998045 across two
    /// different cars, unchanged every single time, with MINI_LOWRIDER asked for and ignored. The
    /// documented list of contexts turns out to be mission-specific things like
    /// MISSFBI5_TREVOR_DRIVING, and there is no lowrider entry in it.
    ///
    /// That readout is the only reason this is a settled question rather than an argument, and it
    /// is worth saying plainly: the first attempt was WRONG, and wrong in the way that is hardest
    /// to see -- a call that succeeds and does nothing.
    ///
    /// SO THE POSE IS PLAYED, NOT SELECTED. TASK_PLAY_ANIM with UpperBodyOnly and Secondary lays
    /// an animation over his top half while the game keeps driving the rest of him, which is how
    /// every custom driving pose in this game is actually done. The steering still works, because
    /// the steering is not his arms -- it is the car.
    ///
    /// AND THE NAMES ARE WORKED OUT RATHER THAN GUESSED, which is the part that matters.
    ///
    /// THE CLIPSET HASHES FROM THE FIRST ATTEMPT TURNED OUT TO BE THE KEY. GET_IN_VEHICLE_CLIPSET
    /// _HASH_FOR_SEAT hands back a joaat hash of a NAME, and joaat is reversible by search rather
    /// than by mathematics: hash a few thousand candidate names and see which one lands on the
    /// number the game gave you. 3332998045, logged from an ordinary car, turns out to be
    /// "clipset@veh@std@ds@base" -- so the naming is clipset@veh@LAYOUT@SEAT@STATE, and the seat
    /// is "ds", not the "front_ds" the first probe was built around.
    ///
    /// SO THE PROBE NAMES THINGS NOW. It takes the seat clipset of whatever car you are sat in and
    /// finds the name that hashes to it, which means sitting in a real lowrider makes the game
    /// tell you what a real lowrider's seat animation is CALLED. That is the whole question, and
    /// it is answered by one drive rather than by another list of guesses.
    ///
    /// The dictionary probe stays alongside it: DOES_ANIM_DICT_EXIST answers for a dictionary and
    /// GET_ANIM_DURATION for a clip inside one, so a wrong name is a log line rather than the
    /// silence that let the first attempt go unnoticed.
    ///
    /// AND THE PROBE SETTLED IT. 32 dictionaries exist, among them veh@low@front_ds@base with a
    /// clip called "sit" that runs 6.33 seconds -- so the original guess was right after all, and
    /// the "correction" to veh@low@ds@base from the clipset hash was wrong. The two are named
    /// differently ON PURPOSE: a seat CLIPSET is clipset@veh@low@ds@base and the animation
    /// DICTIONARY behind it is veh@low@front_ds@base. Reasoning across from one to the other was
    /// a guess wearing the clothes of a deduction, and the probe is what caught it.
    /// </summary>
    internal sealed class Lowrider
    {
        /// <summary>How long to let requested dictionaries load before asking what is in them.</summary>
        private const int LoadMs = 2000;

        /// <summary>The driver's seat, which the game numbers as minus one rather than nought.</summary>
        private const int Driver = -1;

        private readonly Settings _cfg;

        private int _car;

        /// <summary>Whether the pose has been put on, so it is asked for once rather than per frame.</summary>
        private bool _posed;

        /// <summary>Whether WE put the window down, so only our own is wound back up.</summary>
        private bool _wound;

        /// <summary>Whether he has actually been sat in this car yet. See the climb-out below.</summary>
        private bool _sat;

        /// <summary>Which dictionary and clip are actually playing, so the right one is stopped.</summary>
        private string _playing;
        private string _clip;

        /// <summary>Whether the seat clipset of this car has been named in the log yet.</summary>
        private bool _named;

        /// <summary>Nought for not started, one for dictionaries found, two for finished.</summary>
        private int _probe;
        private int _probedAt;
        private string[] _found;

        // THE PIECES THE NAMES ARE BUILT FROM, corrected against a hash the game itself gave us.
        // clipset@veh@std@ds@base hashes to 3332998045, which is what an ordinary car reported --
        // so the layout comes first, the seat is ds/ps/rds/rps, and the state is last.
        private static readonly string[] Layouts =
        {
            "low", "std", "lowrider", "low_restricted", "van", "truck", "bus", "bike", "quad",
            "tanker", "big", "coupe", "sports", "sportscar", "muscle", "suv", "mini", "luxor",
            "bodhi", "tank", "forklift", "hotknife", "dune", "rally", "freight", "tow", "semi",
            "boat", "heli", "plane", "sub", "jetski", "tractor", "trailer", "taxi", "police",
            "convertible", "speedo", "burrito", "journey", "camper", "hauler", "phantom",
        };

        private static readonly string[] Seats = { "ds", "ps", "rds", "rps" };

        private static readonly string[] States =
        {
            "base", "idle_a", "idle_b", "idle_c", "idle_d", "idle_e", "sit", "arm", "idle_duck",
        };

        /// <summary>Clip names worth asking a dictionary about, once it is known to exist.</summary>
        private static readonly string[] Clips =
        {
            "sit", "base", "idle_a", "idle_b", "idle_c", "still", "hangout", "arm_out", "sit_arm",
        };

        public Lowrider(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.LowriderPose)
                {
                    Release(me);
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists() || !Mine(car, me) || !Suits(car))
                {
                    Release(me);
                    return;
                }

                if (car.Handle != _car)
                {
                    Release(me);
                    _car = car.Handle;
                    _named = false;
                }

                if (_cfg.LowriderProbe) Name(car);

                Probe();

                // HE WINDS IT UP ON HIS WAY OUT. Sat in the car a moment ago, not sat in it now,
                // still attached to it: that is the climb-out, and it is the moment a person would
                // reach for the handle rather than two seconds later stood beside it.
                if (_sat && !Seated(me))
                {
                    _sat = false;

                    if (_wound)
                    {
                        _wound = false;
                        Wind(car, down: false);
                        Log.Debug("Lowrider pose: window up on the way out.");
                    }
                }
                else if (Seated(me))
                {
                    _sat = true;
                }

                if (!Seated(me) || _posed) return;

                _posed = true;

                if (_cfg.LowriderWindow && !_wound) _wound = Wind(car, down: true);

                Pose(me);
            }
            catch (Exception ex)
            {
                Release(me);
                Log.Once("lowrider", "The lowrider pose fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// The animation, on his top half only.
        ///
        /// UpperBodyOnly AND Secondary TOGETHER. Upper body leaves the legs and the seated base
        /// animation to the game; secondary means it plays ALONGSIDE whatever the game is doing
        /// rather than replacing the task -- so he is still driving, and the arm is laid over the
        /// top of it. Looped, and given no duration, because a pose is a state.
        ///
        /// CHECKED BEFORE IT IS ASKED FOR. Both names are settings, and a clip that does not exist
        /// makes TASK_PLAY_ANIM do nothing at all -- the same silent failure the seat context had,
        /// and the reason that one went unnoticed. GET_ANIM_DURATION turns it into a log line.
        /// </summary>
        private void Pose(Ped me)
        {
            var dict = (_cfg.LowriderAnimDict ?? string.Empty).Trim();
            var clip = (_cfg.LowriderAnimClip ?? string.Empty).Trim();

            if (dict.Length == 0 || clip.Length == 0) return;

            try
            {
                if (!Function.Call<bool>(Hash.DOES_ANIM_DICT_EXIST, dict))
                {
                    Log.Once("lowrider-dict", "Lowrider pose: this build has no animation " +
                                              "dictionary called '" + dict + "'. The probe lines " +
                                              "list the ones it does have.");
                    return;
                }

                Function.Call(Hash.REQUEST_ANIM_DICT, dict);

                if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict))
                {
                    // Still loading. Tried again on the next pass rather than given up on, which
                    // is what clearing the flag here does.
                    _posed = false;
                    return;
                }

                var seconds = Function.Call<float>(Hash.GET_ANIM_DURATION, dict, clip);

                if (seconds <= 0f)
                {
                    Log.Once("lowrider-clip", "Lowrider pose: '" + dict + "' has no clip called '" +
                                              clip + "'. The probe lines list what it does have.");
                    return;
                }

                me.Task.PlayAnimation(dict, clip, 4f, -1,
                                      AnimationFlags.Loop |
                                      AnimationFlags.UpperBodyOnly |
                                      AnimationFlags.Secondary);

                _playing = dict;
                _clip = clip;

                Log.Info("Lowrider pose: playing '" + dict + "' / '" + clip + "' (" +
                         seconds.ToString("0.00") + "s) on the upper body.");
            }
            catch (Exception ex)
            {
                Log.Once("lowrider-play", "Lowrider pose: could not play it: " + ex.Message);
            }
        }

        /// <summary>
        /// What the seat animation of the car you are in is actually CALLED.
        ///
        /// A JOAAT HASH IS ONE-WAY, BUT IT IS NOT WIDE. The game will tell you which clipset a seat
        /// is using and it answers with a number; a number cannot be turned back into a name, but a
        /// few thousand candidate names can be hashed and compared against it, and one of them
        /// lands. That is how clipset@veh@std@ds@base was identified from 3332998045 -- and it is
        /// how sitting in a real lowrider will name a real lowrider's seat animation, which is the
        /// only thing this feature has ever actually needed to know.
        ///
        /// WORTH RUNNING ON EVERY CAR, not just the ones we cannot pose. The interesting reading is
        /// the one taken in a car that already does it.
        /// </summary>
        private void Name(Vehicle car)
        {
            if (_named) return;

            _named = true;

            try
            {
                var want = Function.Call<uint>(Hash.GET_IN_VEHICLE_CLIPSET_HASH_FOR_SEAT,
                                               car.Handle, Driver);

                if (want == 0)
                {
                    Log.Debug("Lowrider probe: this seat reports no clipset at all.");
                    return;
                }

                foreach (var layout in Layouts)
                {
                    foreach (var seat in Seats)
                    {
                        foreach (var state in States)
                        {
                            var tail = layout + "@" + seat + "@" + state;

                            if (StringHash.AtStringHash("clipset@veh@" + tail) == want)
                            {
                                Log.Info("Lowrider probe: this seat is 'clipset@veh@" + tail +
                                         "' (" + want + "), so its animations are in 'veh@" +
                                         tail + "'.");
                                return;
                            }

                            if (StringHash.AtStringHash("veh@" + tail) == want)
                            {
                                Log.Info("Lowrider probe: this seat is 'veh@" + tail + "' (" +
                                         want + ").");
                                return;
                            }
                        }
                    }
                }

                Log.Info("Lowrider probe: seat clipset " + want + " matched none of the " +
                         (Layouts.Length * Seats.Length * States.Length * 2) +
                         " candidate names. The layout word is one this list has not got.");
            }
            catch (Exception ex)
            {
                Log.Once("lowrider-name", "Naming the seat clipset fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Which animation dictionaries and clips this build actually has.
        ///
        /// THE WHOLE REASON THE LAST ATTEMPT FAILED SILENTLY was that nothing could tell a name the
        /// game has from a name it does not. Animations are not like that: DOES_ANIM_DICT_EXIST
        /// answers for a dictionary and GET_ANIM_DURATION answers for a clip inside one. So the
        /// names stop being a guess and become a question the game answers.
        ///
        /// TWO PASSES, BECAUSE LOADING IS NOT INSTANT. A dictionary has to be requested and given a
        /// moment before its contents can be asked about, so the first pass finds and requests and
        /// the second, two seconds later, reads. Doing both at once reports every dictionary as
        /// empty -- which looks exactly like a wrong answer and is not one.
        /// </summary>
        private void Probe()
        {
            if (!_cfg.LowriderProbe || _probe > 1) return;

            try
            {
                if (_probe == 0)
                {
                    var found = new List<string>();

                    foreach (var layout in Layouts)
                    {
                        foreach (var seat in Seats)
                        {
                            foreach (var state in States)
                            {
                                var dict = "veh@" + layout + "@" + seat + "@" + state;

                                if (!Function.Call<bool>(Hash.DOES_ANIM_DICT_EXIST, dict)) continue;

                                found.Add(dict);
                                Function.Call(Hash.REQUEST_ANIM_DICT, dict);
                            }
                        }
                    }

                    _found = found.ToArray();
                    _probe = 1;
                    _probedAt = Game.GameTime;

                    Log.Info("Lowrider probe: " + _found.Length + " of " +
                             (Layouts.Length * Seats.Length * States.Length) +
                             " candidate dictionaries exist in this build.");

                    if (_found.Length == 0)
                    {
                        Log.Info("Lowrider probe: none exist, which would be a surprise now that " +
                                 "the naming has been confirmed from a hash. Check the seat line " +
                                 "above for what this car is actually using.");
                        _probe = 2;
                    }

                    return;
                }

                if (Game.GameTime - _probedAt < LoadMs) return;

                _probe = 2;

                foreach (var dict in _found)
                {
                    if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict))
                    {
                        Log.Info("Lowrider probe: '" + dict + "' exists but did not load in time.");
                        continue;
                    }

                    var clips = string.Empty;

                    foreach (var clip in Clips)
                    {
                        var seconds = Function.Call<float>(Hash.GET_ANIM_DURATION, dict, clip);
                        if (seconds > 0f) clips += (clips.Length > 0 ? ", " : "") + clip;
                    }

                    Log.Info("Lowrider probe: '" + dict + "' -> " +
                             (clips.Length == 0 ? "exists, but none of the guessed clip names." : clips));
                }

                Log.Info("Lowrider probe: done. Put a dictionary and a clip from the lines above " +
                         "into LowriderAnimDict and LowriderAnimClip, then set LowriderProbe false.");
            }
            catch (Exception ex)
            {
                _probe = 2;
                Log.Once("lowrider-probe", "The probe fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Stops the animation, and winds our own window up.
        ///
        /// THE ANIMATION IS STOPPED BY NAME. ClearAnimation takes the dictionary and clip that were
        /// started, which is why both are remembered rather than read back out of the settings --
        /// somebody who changed the setting while sat in the car would otherwise be asking the game
        /// to stop something that was never started, and the arm would stay out for good.
        /// </summary>
        public void Release(Ped me)
        {
            var handle = _car;
            var wound = _wound;
            var dict = _playing;
            var clip = _clip;

            _car = 0;
            _named = false;
            _posed = false;
            _wound = false;
            _sat = false;
            _playing = null;
            _clip = null;

            if (me != null && dict != null && clip != null)
            {
                // STOP_ANIM_TASK, through the native rather than the wrapper. ClearAnimation is
                // obsolete and its replacement takes a CrClipAsset, which is a type this file
                // would have to construct to say the two strings it already has. The native takes
                // the strings. Seventh time SHVDN's deprecation warnings have caught something
                // here, and the first time the tidier call is the worse one.
                try
                {
                    Function.Call(Hash.STOP_ANIM_TASK, me.Handle, dict, clip, -4f);
                }
                catch { /* he is out of the car either way */ }
            }

            if (!wound || handle == 0) return;

            try
            {
                var car = (Vehicle)Entity.FromHandle(handle);
                if (car != null && car.Exists()) Wind(car, down: false);
            }
            catch
            {
                // The car is gone, and the window went with it.
            }
        }

        /// <summary>
        /// Whether this is a thing you can hang an arm out of.
        ///
        /// THE FILTER IS BACK, AND FOR THE OPPOSITE REASON TO THE ONE IT LEFT ON. It was removed
        /// when the pose was a seat CONTEXT, on the argument that a vehicle whose layout has no
        /// such clipset would simply ignore the request -- which was true of a context, and is
        /// completely false of a played animation. An animation plays on whatever you give it.
        ///
        /// A CAR-SEAT ANIMATION ON A BICYCLE is a man folded over the handlebars with one arm
        /// reaching into the road, which is exactly what it looked like. Bikes have no window, no
        /// door and no armrest; there is nothing there to lean on and nothing to lean out of.
        ///
        /// Cars only, then. Not because the others would break, but because on the others it is
        /// not a pose, it is a fault.
        /// </summary>
        private static bool Suits(Vehicle car)
        {
            try { return car.Model.IsCar; }
            catch { return false; }
        }

        /// <summary>The driver's window, down or up. Says whether it actually did anything.</summary>
        private static bool Wind(Vehicle car, bool down)
        {
            try
            {
                var window = car.Windows[VehicleWindowIndex.FrontLeftWindow];

                if (window == null) return false;

                if (down) window.RollDown();
                else window.RollUp();

                return true;
            }
            catch
            {
                // Something without that window. There is nothing to lean out of.
                return false;
            }
        }

        /// <summary>
        /// Actually IN the seat, rather than part way through a door in either direction.
        ///
        /// CurrentVehicle answers yes for the whole climb-in AND the whole climb-out. This is the
        /// half of the pair that says which of those is happening, once you know whether he has
        /// been sat down yet.
        /// </summary>
        private static bool Seated(Ped me)
        {
            try { return me != null && me.IsSittingInVehicle(); }
            catch { return false; }
        }

        /// <summary>
        /// Whether that car is his to sit in, INCLUDING while he is still climbing into it.
        ///
        /// An empty driver's seat counts, because during the entry animation that is exactly what
        /// it is: CurrentVehicle already names the car and Driver is still nobody.
        /// </summary>
        private static bool Mine(Vehicle car, Ped me)
        {
            try
            {
                var driver = car.Driver;
                return driver == null || !driver.Exists() || driver.Handle == me.Handle;
            }
            catch
            {
                return false;
            }
        }
    }
}
