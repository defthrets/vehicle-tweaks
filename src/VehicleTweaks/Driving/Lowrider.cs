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
    /// AND THE NAMES ARE TESTED RATHER THAN GUESSED, which is the part that matters. A dictionary
    /// can be checked with DOES_ANIM_DICT_EXIST and a clip inside it with GET_ANIM_DURATION -- so
    /// instead of picking a name and hoping, the probe walks sixty-four candidates and the log
    /// says which ones this build has. One drive answers it, and the answer goes in the ini.
    /// </summary>
    internal sealed class Lowrider
    {
        /// <summary>How long to let requested dictionaries load before asking what is in them.</summary>
        private const int LoadMs = 2000;

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

        /// <summary>Nought for not started, one for dictionaries found, two for finished.</summary>
        private int _probe;
        private int _probedAt;
        private string[] _found;

        // GTA'S VEHICLE ANIMATIONS ARE NAMED BY PATTERN -- a family, a seat and a state -- so the
        // candidates are built from the pieces rather than typed out one at a time. Sixty-four
        // names cost sixty-four calls, once, on a native that only answers a question.
        private static readonly string[] Families = { "veh@low@", "veh@std@", "veh@lowrider@", "anim@veh@low@" };
        private static readonly string[] Seats = { "front_ds@", "ds@", "front_ps@", "ps@" };
        private static readonly string[] States = { "base", "idle_a", "idle_b", "sit" };

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

                if (car == null || !car.Exists() || !Mine(car, me))
                {
                    Release(me);
                    return;
                }

                if (car.Handle != _car)
                {
                    Release(me);
                    _car = car.Handle;
                }

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

                    foreach (var family in Families)
                    {
                        foreach (var seat in Seats)
                        {
                            foreach (var state in States)
                            {
                                var dict = family + seat + state;

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
                             (Families.Length * Seats.Length * States.Length) +
                             " candidate dictionaries exist in this build.");

                    if (_found.Length == 0)
                    {
                        Log.Info("Lowrider probe: none of the guessed families are right, so the " +
                                 "names are not veh@low@ / veh@std@ shaped on this build.");
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
