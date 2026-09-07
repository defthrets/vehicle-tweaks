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
    /// APPLIED ONCE, NOT EVERY FRAME. A seat context is a state, and re-asserting a state that is
    /// already set is how you get an animation that restarts sixty times a second and never
    /// actually plays. It goes on when he is settled in the seat and comes off when he leaves.
    ///
    /// AND HE HAS TO BE SETTLED. Asked for during the climb-in, it is competing with the entry
    /// animation and loses -- so it waits for IsSittingInVehicle rather than taking CurrentVehicle
    /// as the answer, which is the distinction that has already caught the radio in this mod once.
    ///
    /// THE CONTEXT IS A SETTING, and that is deliberate rather than lazy. Everything else here was
    /// verified against SHVDN before it was relied on; a context is a NAME, hashed at runtime, and
    /// there is no list to check it against from outside the game. Baking a guess into the build
    /// would mean a rebuild to try the next candidate. In the ini it is one line and a reload, and
    /// the log says what went on -- the same reasoning that put the pad control names there.
    /// </summary>
    internal sealed class Lowrider
    {
        private readonly Settings _cfg;

        private int _car;

        /// <summary>Whether the pose is on, so it is asked for once rather than every frame.</summary>
        private bool _posed;

        /// <summary>Whether WE put the window down, so only our own is wound back up.</summary>
        private bool _wound;

        /// <summary>Said once per context, not once per car.</summary>
        private string _said;

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

                if (car == null || !car.Exists() || !Seated(me) || !AtTheWheel(car, me) || !Suits(car))
                {
                    Release(me);
                    return;
                }

                if (car.Handle != _car)
                {
                    Release(me);
                    _car = car.Handle;
                }

                if (_posed) return;

                _posed = true;

                var context = (_cfg.LowriderContext ?? string.Empty).Trim();

                if (context.Length == 0) return;

                // StringHash.AtStringHash, not Game.GenerateHash: the latter is obsolete, and
                // this is the fifth time on this mod that SHVDN's own deprecation warning has
                // been the thing that caught an out-of-date call. They are worth reading.
                var hash = StringHash.AtStringHash(context);

                try
                {
                    Function.Call(Hash.SET_PED_IN_VEHICLE_CONTEXT, me.Handle, hash);
                }
                catch
                {
                    // Nothing to put back: it either took or it did not.
                }

                if (_cfg.LowriderWindow) _wound = Wind(car, down: true);

                if (_said != context)
                {
                    _said = context;

                    // THE HASH AS WELL AS THE NAME. A context that does nothing looks identical
                    // to a context that was never applied, and the number is the only way to tell
                    // a typo from a name the game simply does not have.
                    Log.Info("Lowrider pose: asked for '" + context + "' (" + hash + ").");
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
        /// Whether this is a thing you can hang an arm out of.
        ///
        /// CARS ONLY. A bike has no window and no door to rest on, a boat's seat context is its
        /// own, and asking a helicopter pilot to sit like he is cruising Vespucci is a pose that
        /// would be applied to somebody holding a collective.
        /// </summary>
        private static bool Suits(Vehicle car)
        {
            try { return car.Model.IsCar; }
            catch { return false; }
        }

        /// <summary>
        /// Actually IN the seat, rather than most of the way through the door.
        ///
        /// CurrentVehicle answers yes for the whole climb-in, and a seat context asked for during
        /// the entry animation is competing with it. This is the same distinction that had the
        /// radio being set on a ped who had not left the seat yet.
        /// </summary>
        private static bool Seated(Ped me)
        {
            try { return me.IsSittingInVehicle(); }
            catch { return false; }
        }

        private static bool AtTheWheel(Vehicle car, Ped me)
        {
            try
            {
                var driver = car.Driver;
                return driver != null && driver.Exists() && driver.Handle == me.Handle;
            }
            catch
            {
                return false;
            }
        }
    }
}
