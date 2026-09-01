using System;
using GTA;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Your car is still there when you come back, and you can see where you left it.
    ///
    /// GTA THROWS AWAY CARS YOU ARE NOT LOOKING AT, and that is the single most everyday
    /// annoyance in the game: park, walk into a shop, come out, and the thing you drove there is
    /// gone. It is also the natural end of the idea the rest of this mod is built on -- a car
    /// left running, lit, locked and handbraked is not much use if the game deletes it while
    /// your back is turned.
    ///
    /// ONE CAR, AND ONLY ONE. Persistence is not free: it tells the game a vehicle may never be
    /// cleaned up, and a script that hands that out freely is a script quietly filling the
    /// world's budget with cars nobody is coming back for. So exactly one is held -- the last
    /// one you drove -- and taking a different car hands the previous one straight back.
    ///
    /// THE BLIP IS THE OTHER HALF OF THE SAME PROBLEM. A car that is still there is no good if
    /// you cannot remember which street you left it on.
    /// </summary>
    internal sealed class Parked
    {
        private readonly Settings _cfg;

        private Vehicle _mine;
        private Blip _blip;

        public Parked(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.KeepParked && !_cfg.ParkedBlip)
                {
                    Release();
                    return;
                }

                var car = Mine(me);

                if (car == null)
                {
                    // Nothing of his exists any more. Whatever we were holding is gone with it.
                    if (_mine != null && !Exists(_mine)) Release();
                    return;
                }

                if (_mine != null && _mine.Handle == car.Handle)
                {
                    // Same car. The two settings can be switched on and off while it is held, so
                    // what it is wearing is checked rather than assumed.
                    Hold(car);
                    return;
                }

                Release();

                _mine = car;
                Hold(car);

                Log.Debug("Parked: keeping " + Name(car) + ".");
            }
            catch (Exception ex)
            {
                Log.Once("parked", "Could not keep track of your car: " + ex.Message);
            }
        }

        /// <summary>
        /// The car that is his: the one he is in, or the last one he drove.
        ///
        /// LastVehicle is the game's own memory of it, which is the same thing the locks use.
        /// Nearest would be somebody else's car that happened to be parked closer.
        /// </summary>
        private static Vehicle Mine(Ped me)
        {
            try
            {
                if (me == null || !me.Exists()) return null;

                var inside = me.CurrentVehicle;
                if (inside != null && inside.Exists()) return inside;

                var last = me.LastVehicle;
                return last != null && last.Exists() && !last.IsDead ? last : null;
            }
            catch
            {
                return null;
            }
        }

        private void Hold(Vehicle car)
        {
            try
            {
                car.IsPersistent = _cfg.KeepParked;
            }
            catch
            {
                // Not something worth a line a frame.
            }

            if (_cfg.ParkedBlip) Mark(car);
            else Unmark();
        }

        private void Mark(Vehicle car)
        {
            try
            {
                if (_blip != null && _blip.Exists()) return;

                _blip = car.AddBlip();
                if (_blip == null) return;

                // SHORT RANGE, so it is a thing you find your way back to rather than a marker
                // sitting over the whole map at all times. It is your car, not a mission.
                _blip.Sprite = BlipSprite.PersonalVehicleCar;
                _blip.Color = BlipColor.White;
                _blip.IsShortRange = true;
                _blip.Name = "Your vehicle";
            }
            catch (Exception ex)
            {
                Log.Once("parked-blip", "Could not put a blip on your car: " + ex.Message);
            }
        }

        private void Unmark()
        {
            if (_blip == null) return;

            try
            {
                if (_blip.Exists()) _blip.Delete();
            }
            catch
            {
                // It was a blip.
            }

            _blip = null;
        }

        /// <summary>
        /// Hands the car back to the game and takes the blip off it.
        ///
        /// PERSISTENCE IS THE ONE THAT MATTERS HERE. A blip left behind is untidy; a car left
        /// permanent is a slot in the world that nothing will ever reclaim, and a hundred of
        /// those is a game that stops spawning traffic.
        /// </summary>
        public void Release()
        {
            Unmark();

            if (_mine == null) return;

            var car = _mine;
            _mine = null;

            try
            {
                if (car.Exists()) car.IsPersistent = false;
            }
            catch
            {
                // Gone already, which is the same outcome.
            }
        }

        private static bool Exists(Vehicle v)
        {
            try { return v != null && v.Exists(); }
            catch { return false; }
        }

        private static string Name(Vehicle v)
        {
            try { return v.LocalizedName; }
            catch { return "the car"; }
        }
    }
}
