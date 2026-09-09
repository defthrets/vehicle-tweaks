using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using VehicleTweaks.Core;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// The cars from every update, out on the roads with everything else.
    ///
    /// GTA DOES NOT PUT THEM THERE AND A SCRIPT CANNOT ASK IT TO. Which models the traffic system
    /// is allowed to spawn is a list inside popgroups.ymt, an asset baked into the game's own
    /// archives -- so every mod that does this properly does it with OpenIV, and every mod that
    /// does it from a script does what this does: puts the cars out itself and lets the traffic
    /// take them over. There is no third way and no native that opens the list.
    ///
    /// ON A ROAD, OUT OF SIGHT, AND THEN FORGOTTEN. A point is picked a couple of hundred metres
    /// off in a random direction, snapped to the nearest road with the heading that road runs at,
    /// given a car and a driver told to wander -- and then handed straight back to the game.
    /// Marked as no longer needed, it belongs to the population system: it will be cleaned up
    /// like any other traffic when you drive away, which is the whole reason this does not
    /// slowly fill the map with abandoned cars.
    ///
    /// AND IT CHECKS IT WAS NOT SEEN. A car that appears in front of you is worse than no car at
    /// all, so anything that turns out to be on screen when it lands is deleted again on the
    /// spot. Distance alone is not enough: two hundred metres down a straight road is in plain
    /// view, and two hundred metres round a corner is not.
    ///
    /// ONLY WHAT IS ACTUALLY INSTALLED, and only the road-going ones. Four hundred and eighty
    /// names ship for this, all of them from an update rather than the base game and all of them
    /// something you would believe on a street -- no tanks, no aircraft, no police. Each is put
    /// through IsInCdImage once, the first time, and the ones this copy of the game does not have
    /// are dropped from the bag rather than tried again every few seconds.
    /// </summary>
    internal sealed class Traffic
    {
        /// <summary>How far out to look for a road, and how close is too close to use one.</summary>
        private const float Far = 260f;
        private const float Near = 110f;

        /// <summary>How many of ours may be out at once before we stop adding.</summary>
        private const int Most = 14;

        private readonly Settings _cfg;
        private readonly Random _dice = new Random();

        /// <summary>The models this install actually has, worked out once.</summary>
        private List<string> _bag;

        /// <summary>What we have put out, so we can count them rather than the whole world.</summary>
        private readonly List<Vehicle> _ours = new List<Vehicle>();

        private int _at;
        private bool _said;

        public Traffic(Settings cfg)
        {
            _cfg = cfg;
        }

        public void Update(Ped me)
        {
            try
            {
                if (!_cfg.DlcTraffic || me == null || !me.Exists()) return;

                var now = Game.GameTime;

                if (now - _at < (int)(_cfg.DlcTrafficSeconds * 1000f)) return;

                _at = now;

                Forget();

                if (_ours.Count >= Most) return;

                if (_bag == null) Fill();
                if (_bag.Count == 0) return;

                Put(me);
            }
            catch (Exception ex)
            {
                Log.Once("traffic", "Adding a car to the traffic fell over: " + ex.Message);
            }
        }

        /// <summary>Works out once which of the names this copy of the game actually has.</summary>
        private void Fill()
        {
            _bag = new List<string>();

            foreach (var name in Models.Dlc)
            {
                try
                {
                    var model = new Model(name);

                    if (model.IsValid && model.IsInCdImage && model.IsVehicle) _bag.Add(name);
                }
                catch
                {
                    // One name that will not answer is not a reason to stop.
                }
            }

            Log.Info("DLC traffic: " + _bag.Count + " of " + Models.Dlc.Length +
                     " update cars are installed here.");
        }

        /// <summary>Drops the ones the game has already cleaned up out of our count.</summary>
        private void Forget()
        {
            for (var i = _ours.Count - 1; i >= 0; i--)
            {
                var car = _ours[i];

                if (car == null || !car.Exists()) _ours.RemoveAt(i);
            }
        }

        private void Put(Ped me)
        {
            var angle = _dice.NextDouble() * Math.PI * 2.0;
            var out_ = Near + (float)_dice.NextDouble() * (Far - Near);

            var at = me.Position + new Vector3((float)Math.Cos(angle) * out_,
                                               (float)Math.Sin(angle) * out_, 0f);

            float heading;
            var road = World.GetNextPositionOnStreetWithHeading(at, out heading, false);

            if (road == Vector3.Zero) return;

            // TOO CLOSE AFTER SNAPPING. The road nearest a point two hundred metres away can be
            // the one you are driving on.
            if (road.DistanceTo(me.Position) < Near * 0.6f) return;

            var name = _bag[_dice.Next(_bag.Count)];
            var model = new Model(name);

            try
            {
                model.Request(600);

                if (!model.IsLoaded) return;

                var car = World.CreateVehicle(model, road, heading);

                if (car == null) return;

                // SEEN IS THE ONE THING THAT CANNOT BE ALLOWED. Distance said it was safe and the
                // camera is the only thing that actually knows.
                if (car.IsOnScreen)
                {
                    car.Delete();
                    return;
                }

                car.PlaceOnGround();

                var driver = car.CreateRandomPedOnSeat(VehicleSeat.Driver);

                if (driver != null)
                {
                    // THE ORDINARY WAY A CAR DRIVES: stops for people and other cars, obeys the
                    // lights, goes round things rather than through them. Anything less and the
                    // update cars are the only ones in town running reds.
                    driver.Task.CruiseWithVehicle(car, 15f,
                                                  VehicleDrivingFlags.DrivingModeAvoidVehiclesStopForPedsObeyLights);
                    driver.MarkAsNoLongerNeeded();
                }

                // HANDED TO THE POPULATION SYSTEM. From here it is ordinary traffic: it ages out
                // when you drive away, exactly like the cars the game put there itself.
                car.MarkAsNoLongerNeeded();
                _ours.Add(car);

                Say(name, road.DistanceTo(me.Position));
            }
            finally
            {
                model.MarkAsNoLongerNeeded();
            }
        }

        private void Say(string name, float away)
        {
            if (_said)
            {
                Log.Debug("DLC traffic: put a " + name + " out " + away.ToString("0") + "m away.");
                return;
            }

            _said = true;

            Log.Info("DLC traffic: put a " + name + " out " + away.ToString("0") +
                     "m away, out of sight, and handed it to the traffic.");
        }
    }
}
