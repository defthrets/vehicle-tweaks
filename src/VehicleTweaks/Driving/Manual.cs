using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using VehicleTweaks.Core;
using VehicleTweaks.Input;

// Both namespaces have a Control and only one of them is a game control.
using Control = GTA.Control;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// A manual gearbox. You change gear; nothing else does.
    ///
    /// THE SAME THREE FIELDS AS EVERYTHING ELSE, finally used for the thing they are for. Capping
    /// HighGear removes everything above as an option, writing CurrentGear puts it where you
    /// asked, and NextGear closes the gap. Holding a gear against the box was a compromise;
    /// choosing the gear outright is what those fields actually describe.
    ///
    /// OFF BY DEFAULT, and it is the only feature in this mod that has to be. Everything else
    /// here is a small correction to a car that still drives the way you expect. This changes
    /// what the car IS: get in with it on and you will pull away in whatever gear you left it
    /// in. That is a choice, not an improvement, and it should be made deliberately.
    ///
    /// A AND X, WHICH ARE NOT FREE BUTTONS. There is no spare button on a pad -- the reasoning is
    /// written out in full over in Chord -- and A is where the handbrake already was. Two things
    /// on one button is two things happening, so rather than put the shift somewhere quieter,
    /// THE HANDBRAKE MOVES: to a shoulder, silenced on its old button, and applied to the car
    /// directly from the new one. See Brake, which owns that and is the one place anything is
    /// allowed to ask whether the handbrake is on.
    ///
    /// THE LOG STILL NAMES WHICH PHYSICAL BUTTON each vehicle action is on, once, from the
    /// game's own glyph table -- the handbrake is dealt with, but the horn, the duck and the
    /// cinematic camera are on buttons too, and which ones depends on a layout this cannot see.
    ///
    /// HANDS OFF AT A STANDSTILL, which is what makes reverse still work. Below walking pace it
    /// writes nothing at all and the game has its own gearbox back, so holding the brake picks
    /// reverse exactly as it always did -- and the gear is quietly set back to first, so pulling
    /// away is never in fifth because that is where you happened to stop.
    /// </summary>
    internal sealed class Manual
    {
        /// <summary>Below this, in metres a second, the box is the game's and reverse works.</summary>
        private const float Crawl = 2.0f;

        private readonly Settings _cfg;
        private readonly Brake _brake;

        private readonly Control? _up;
        private readonly Control? _down;

        private int _car;

        /// <summary>The gear being held, or nought for not engaged.</summary>
        private int _gear;

        /// <summary>The top gear the box has, which is also the highest you can select.</summary>
        private int _top;

        // Edges, worked out here rather than taken from IsControlJustPressed, because these are
        // read with the same call the rest of the mod trusts on a control the game may have
        // disabled this frame.
        private bool _upWas;
        private bool _downWas;

        /// <summary>Shifts asked for by keyboard since the last tick, which arrive off it.</summary>
        private int _pending;

        /// <summary>Whether the handbrake is currently being forced on from its new button.</summary>
        private bool _braking;

        private bool _glyphed;

        public Manual(Settings cfg, Brake brake)
        {
            _cfg = cfg;
            _brake = brake;

            _up = Pad.Parse(cfg.ManualUpPad, "shifting up on a pad");
            _down = Pad.Parse(cfg.ManualDownPad, "shifting down on a pad");
        }

        /// <summary>
        /// The keyboard half, which arrives off the tick rather than in it.
        ///
        /// BANKED RATHER THAN ACTED ON. SHVDN delivers KeyDown outside the update loop, so doing
        /// the work here would mean writing gears from a thread that is not the one holding the
        /// car. It is also the only free edge detection there is: KeyDown does not auto-repeat
        /// into a gearbox.
        /// </summary>
        public void OnKey(Keys key)
        {
            if (!_cfg.ManualBox) return;

            if (key == _cfg.ManualUpKey) _pending++;
            else if (key == _cfg.ManualDownKey) _pending--;
        }

        public void Update(Ped me)
        {
            try
            {
                var asked = Asked();

                if (!_cfg.ManualBox)
                {
                    Release();
                    return;
                }

                var car = me == null ? null : me.CurrentVehicle;

                if (car == null || !car.Exists() || !AtTheWheel(car, me) || !Geared(car))
                {
                    Release();
                    return;
                }

                if (car.Handle != _car)
                {
                    Release();
                    _car = car.Handle;
                }

                Glyphs();

                // BEFORE THE STANDSTILL CHECK BELOW, AND EVERY FRAME. The gear logic stands off
                // at walking pace so that reverse still works, but the handbrake does not get to
                // stand off with it -- a handbrake that comes back on its old button whenever the
                // car is stopped is a gearshift that pulls the handbrake at every junction.
                _brake.Silence();
                Handbrake(car);

                var gear = Read(car);
                var speed = Math.Abs(Speed(car));

                // A STANDSTILL IS THE GAME'S, and so is reverse. Writing a forward gear over the
                // top of the box at rest is a car that can never reverse, because reverse is
                // gear nought and this would be putting it back into first sixty times a second.
                if (speed < Crawl || gear < 1)
                {
                    _gear = 1;
                    Uncap(car);
                    return;
                }

                if (_gear == 0)
                {
                    _gear = gear;
                    _top = Top(car);

                    Log.Info("Manual gearbox: engaged in " + _gear + " of " + _top + ". Up on " +
                             Name(_cfg.ManualUpPad, _cfg.ManualUpKey) + ", down on " +
                             Name(_cfg.ManualDownPad, _cfg.ManualDownKey) + ".");
                }

                if (asked != 0)
                {
                    var was = _gear;

                    _gear += asked;

                    if (_gear < 1) _gear = 1;
                    if (_top > 0 && _gear > _top) _gear = _top;

                    if (_gear != was) Log.Debug("Manual gearbox: " + was + " to " + _gear + ".");
                }

                try
                {
                    car.HighGear = _gear;
                    car.NextGear = _gear;
                    car.CurrentGear = _gear;
                }
                catch
                {
                    // The next frame will try again.
                }
            }
            catch (Exception ex)
            {
                Release();
                Log.Once("manual", "The manual gearbox fell over: " + ex.Message);
            }
        }

        /// <summary>
        /// How many gears up, this frame. Negative for down.
        ///
        /// ALWAYS DRAINED, even when the feature turns out not to apply, or a press made while
        /// walking about would be banked and spent on the next car got into.
        /// </summary>
        private int Asked()
        {
            var asked = _pending;
            _pending = 0;

            // Only while the player is actually on a pad. Every one of these controls has a
            // keyboard binding too, and without the check each would quietly become a second
            // keyboard shortcut that nothing documents and nobody asked for.
            if (!Pad.InUse())
            {
                _upWas = false;
                _downWas = false;
                return asked;
            }

            var up = _up.HasValue && Pad.Held(_up.Value);
            var down = _down.HasValue && Pad.Held(_down.Value);

            if (up && !_upWas) asked++;
            if (down && !_downWas) asked--;

            _upWas = up;
            _downWas = down;

            return asked;
        }

        /// <summary>
        /// The handbrake, on its new button.
        ///
        /// SET_VEHICLE_HANDBRAKE RATHER THAN THE CONTROL, because the control is the thing being
        /// silenced. It is a forced state on the car, so it is written every frame it is wanted
        /// and cleared once on the way out -- and cleared again by handle on release, because a
        /// car left with its handbrake forced on is a car that will not move and says nothing
        /// about why.
        /// </summary>
        private void Handbrake(Vehicle car)
        {
            var want = _brake.Moved && _brake.Asked();

            if (!want && !_braking) return;

            try { car.IsHandbrakeForcedOn = want; }
            catch { /* the next frame will try again */ }

            _braking = want;
        }

        /// <summary>
        /// Which physical button each vehicle action is on, said once, from the game's own table.
        ///
        /// WORTH THE CALL BECAUSE THE ANSWER IS NOT KNOWABLE FROM HERE. A control is an action,
        /// not a button, and which button carries it depends on the layout the player has
        /// chosen. Binding a gearshift to A means sharing A with whatever else is on it, and
        /// there is no way to warn about that honestly without knowing what it is. This turns
        /// "something else may happen when you shift" into a line naming what.
        /// </summary>
        private void Glyphs()
        {
            if (_glyphed) return;
            _glyphed = true;

            if (!Pad.InUse()) return;

            try
            {
                var say = "Manual gearbox: on this pad, " +
                          Glyph(Control.VehicleHandbrake, "handbrake") + ", " +
                          Glyph(Control.VehicleHorn, "horn") + ", " +
                          Glyph(Control.VehicleDuck, "duck") + ", " +
                          Glyph(Control.VehicleAttack, "attack") + ", " +
                          Glyph(Control.VehicleCinCam, "cinematic camera") +
                          ". The handbrake has been moved off its own; a shift still shares " +
                          "with whichever of the rest is on it.";

                Log.Info(say);
            }
            catch (Exception ex)
            {
                Log.Debug("Manual gearbox: could not read the pad glyphs (" + ex.Message + ").");
            }
        }

        private static string Glyph(Control control, string what)
        {
            try
            {
                var s = Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTONS_STRING,
                                              2, (int)control, 1);

                return what + " is " + (string.IsNullOrEmpty(s) ? "unbound" : s);
            }
            catch
            {
                return what + " is unknown";
            }
        }

        private static string Name(string pad, Keys key)
        {
            var padded = string.IsNullOrEmpty(pad) ? "nothing" : pad;
            return padded + " / " + key.ToString().ToUpperInvariant();
        }

        /// <summary>Gives the top gear back, if it was ever taken.</summary>
        private void Uncap(Vehicle car)
        {
            if (_top == 0) return;

            var top = _top;
            _top = 0;

            try { car.HighGear = top; }
            catch { /* the release by handle is the other chance */ }
        }

        /// <summary>
        /// Uncaps the box wherever it was capped, and forgets the car.
        ///
        /// BY HANDLE, like every other override in here. A car left capped in second is a car
        /// that will not do more than forty, with nothing on screen to say why -- and stepping
        /// straight out of one and into another means the first is no longer anybody's
        /// CurrentVehicle by the time anyone thinks to put it back.
        /// </summary>
        public void Release()
        {
            var handle = _car;
            var top = _top;
            var braking = _braking;

            _car = 0;
            _gear = 0;
            _top = 0;
            _braking = false;
            _upWas = false;
            _downWas = false;

            if (handle == 0 || (top <= 0 && !braking)) return;

            try
            {
                var car = (Vehicle)Entity.FromHandle(handle);
                if (car == null || !car.Exists()) return;

                if (top > 0) car.HighGear = top;
                if (braking) car.IsHandbrakeForcedOn = false;
            }
            catch
            {
                // The car is gone, and its gearbox went with it.
            }
        }

        // ==================================================================
        // Asking the car things
        // ==================================================================

        private static bool Geared(Vehicle car)
        {
            try
            {
                var m = car.Model;
                return (m.IsCar || m.IsBike || m.IsQuadBike) && !m.IsBicycle && !m.IsElectricVehicle;
            }
            catch
            {
                return false;
            }
        }

        private static int Read(Vehicle car)
        {
            try { return car.CurrentGear; }
            catch { return 0; }
        }

        private static int Top(Vehicle car)
        {
            try
            {
                var top = car.HighGear;
                return top > 0 ? top : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static float Speed(Vehicle car)
        {
            try { return car.Speed; }
            catch { return 0f; }
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
