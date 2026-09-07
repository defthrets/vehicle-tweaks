using System;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// He drives everything the way he drives a lowrider: sat back, one arm through the window.
    ///
    /// THE GAME ALREADY HAS THIS POSE and only ever gives it to you in the cars Benny built. It
    /// is not an animation somebody has to author -- it is a seat context, chosen per vehicle in
    /// the game's own layout data, and a script can ask for a different one. So this is not a new
    /// animation bolted on; it is the one that already exists, applied to the car you are in.
    ///
    /// THE WINDOW GOES DOWN WITH IT, because an arm hanging through glass is worse than no arm at
    /// all. That is the whole reason the pose looks right in a lowrider and wrong everywhere
    /// else: those cars are driven with the window down.
    ///
    /// AND HE WINDS IT UP AS HE GETS OUT, rather than the car doing it once he has gone. Both
    /// versions put the window back; only one of them looks like a person doing it. The signal is
    /// IsSittingInVehicle going false while CurrentVehicle still names the car, which is exactly
    /// the climb-out -- the same pair that has to be told apart for the radio, and for the pose
    /// itself on the way in.
    ///
    /// EVERY VEHICLE, WHICH IS THE POINT. There is no lowrider check and no convertible check --
    /// there was a cars-only filter here and it was MINE, not the game's. A vehicle whose seat
    /// layout has no such clipset simply ignores the context and sits him normally, so the filter
    /// was not preventing a broken pose, it was preventing an attempt.
    ///
    /// ASKED FOR EARLY, AND AGAIN A FEW TIMES. The seat clipset is resolved as he gets in, so a
    /// context set after he has landed in the seat can be a context set too late -- which looks
    /// exactly like a context the game does not have. So it starts the moment the car becomes his
    /// rather than waiting for IsSittingInVehicle, and is re-asserted at a few points across the
    /// first second and a half. Four times, not every frame: this is a state, and hammering a
    /// state is how you get an animation that restarts sixty times a second and never plays.
    ///
    /// AND IT SAYS WHETHER IT WORKED. GET_IN_VEHICLE_CLIPSET_HASH_FOR_SEAT is the game's own
    /// answer to "which seat animation is this ped actually using", read before the context goes
    /// on and again after the last attempt. A context that does nothing is otherwise
    /// indistinguishable from one that was never applied, and this mod has already lost a day to
    /// a feature that was deployed, never ran, and got judged anyway.
    ///
    /// THE CONTEXT IS A SETTING, and that is deliberate rather than lazy. Everything else here was
    /// verified against SHVDN before it was relied on; a context is a NAME, hashed at runtime, and
    /// there is no list to check it against from outside the game. Baking a guess into the build
    /// would mean a rebuild to try the next candidate. In the ini it is one line and a reload, and
    /// the log says what went on -- the same reasoning that put the pad control names there.
    /// </summary>
    internal sealed class Lowrider
    {
        /// <summary>The driver's seat, which the game numbers as minus one rather than nought.</summary>
        private const int Driver = -1;

        private readonly Settings _cfg;

        private int _car;

        /// <summary>Whether the pose is on, so there is something to put back.</summary>
        private bool _posed;

        /// <summary>When the car became his, which is when the seat animation is being decided.</summary>
        private int _since;

        /// <summary>How many of the attempts below have been made for this car.</summary>
        private int _step;

        /// <summary>The seat clipset the game had chosen before we asked for anything.</summary>
        private uint _was;

        /// <summary>Whether WE put the window down, so only our own is wound back up.</summary>
        private bool _wound;

        /// <summary>
        /// Whether he has actually been sat in this car yet.
        ///
        /// WITHOUT IT THERE IS NO WAY TO TELL THE TWO ANIMATIONS APART. Climbing in and climbing
        /// out look identical from outside: CurrentVehicle names the car and IsSittingInVehicle
        /// says no, in both. What separates them is which came first, and this is that.
        /// </summary>
        private bool _sat;

        /// <summary>Said once per context, not once per car.</summary>
        private string _said;

        /// <summary>
        /// When the context is asked for, in milliseconds from the car becoming his.
        ///
        /// SPREAD ACROSS THE WAY IN rather than fired once at a moment picked by guesswork. The
        /// seat clipset is chosen somewhere inside the entry animation and nothing tells a script
        /// when; one attempt means picking that moment correctly first time, and being silently
        /// wrong if not. Four cost nothing and cover the whole climb-in.
        /// </summary>
        private static readonly int[] Attempts = { 0, 300, 800, 1600 };

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

                // HIS SEAT, WHICH DURING THE CLIMB-IN IS A SEAT NOBODY IS IN YET. Waiting for a
                // driver to exist would be waiting until after the seat animation has been
                // decided, and that is the one thing this must not be late for.
                if (car == null || !car.Exists() || !Mine(car, me))
                {
                    Release(me);
                    return;
                }

                if (car.Handle != _car)
                {
                    Release(me);

                    _car = car.Handle;
                    _since = Game.GameTime;
                    _step = 0;
                    _was = Clipset(car);
                }

                // HE WINDS IT UP ON HIS WAY OUT. Sat in the car a moment ago, not sat in it
                // now, still attached to it: that is the climb-out, and it is the moment a person
                // would reach for the handle -- not two seconds later once he is stood beside it,
                // which is when the release below would otherwise get to it.
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

                if (_step >= Attempts.Length) return;

                var context = (_cfg.LowriderContext ?? string.Empty).Trim();

                if (context.Length == 0)
                {
                    _step = Attempts.Length;
                    return;
                }

                if (Game.GameTime - _since < Attempts[_step]) return;

                _step++;

                // StringHash.AtStringHash, not Game.GenerateHash: the latter is obsolete, and
                // this is the fifth time on this mod that SHVDN's own deprecation warning has
                // been the thing that caught an out-of-date call. They are worth reading.
                var hash = StringHash.AtStringHash(context);

                try
                {
                    Function.Call(Hash.SET_PED_IN_VEHICLE_CONTEXT, me.Handle, hash);
                    _posed = true;
                }
                catch
                {
                    // Nothing to put back: it either took or it did not.
                }

                if (_cfg.LowriderWindow && !_wound) _wound = Wind(car, down: true);

                if (_said != context)
                {
                    _said = context;

                    // THE HASH AS WELL AS THE NAME. A context that does nothing looks identical
                    // to a context that was never applied, and the number is the only way to tell
                    // a typo from a name the game simply does not have.
                    Log.Info("Lowrider pose: asking for '" + context + "' (" + hash + ").");
                }

                // AFTER THE LAST ATTEMPT, THE VERDICT. The game's own answer to which seat
                // animation he is using, before and after -- so "it did nothing" and "it was
                // never applied" stop looking like each other.
                if (_step >= Attempts.Length)
                {
                    var now = Clipset(car);

                    Log.Debug("Lowrider pose: seat clipset " + _was +
                              (now == _was ? " unchanged - this vehicle's layout has no '" +
                                             context + "'."
                                           : " became " + now + " - the context took."));
                }
            }
            catch (Exception ex)
            {
                Release(me);
                Log.Once("lowrider", "The lowrider pose fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// Puts him back the way the game sits him, and winds our own window up.
        ///
        /// BY HANDLE FOR THE WINDOW, like every other override in here -- stepping straight out
        /// of one car and into another has to close the first one's window, and that car stopped
        /// being anybody's CurrentVehicle a frame ago.
        ///
        /// The context goes back on the PED, which is why this takes one. There is no per-car
        /// version of it to release: it is how he sits, not something done to the car.
        /// </summary>
        public void Release(Ped me)
        {
            var handle = _car;
            var wound = _wound;
            var posed = _posed;

            _car = 0;
            _posed = false;
            _wound = false;
            _since = 0;
            _step = 0;
            _was = 0;
            _sat = false;

            if (posed && me != null)
            {
                try { Function.Call(Hash.RESET_PED_IN_VEHICLE_CONTEXT, me.Handle); }
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
        /// Which seat animation the game has actually put him in.
        ///
        /// THE ONLY HONEST TEST THERE IS. There is no native that reads back a ped's vehicle
        /// context, so the way to find out whether asking for one changed anything is to look at
        /// what it was supposed to change. Nought comes back for anything that has no such
        /// answer, which compares equal to itself and reports as unchanged -- which is correct.
        /// </summary>
        private static uint Clipset(Vehicle car)
        {
            try
            {
                return Function.Call<uint>(Hash.GET_IN_VEHICLE_CLIPSET_HASH_FOR_SEAT,
                                           car.Handle, Driver);
            }
            catch
            {
                return 0;
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
        /// it is: CurrentVehicle already names the car and Driver is still nobody. Asking only
        /// whether he IS the driver would mean never asking until the way in was over.
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
