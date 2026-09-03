using System;
using GTA;
using VehicleTweaks.Core;
using VehicleTweaks.Input;

// Both namespaces have a Control and only one of them is a game control.
using Control = GTA.Control;

namespace VehicleTweaks.Driving
{
    /// <summary>
    /// Where the handbrake is, and whether it is being asked for. One answer, for everybody.
    ///
    /// THE MANUAL BOX PUT A GEARSHIFT ON A, WHICH IS WHERE THE HANDBRAKE ALREADY WAS. Two things
    /// on one button is two things happening, and of the two the handbrake is the one that can
    /// be moved -- so it goes to a shoulder, the vanilla control is disabled so the old button
    /// only shifts, and the brake is applied from the new button instead.
    ///
    /// A CONTROL CANNOT ACTUALLY BE REBOUND FROM A SCRIPT. What this does is the nearest honest
    /// thing: silence the game's own handbrake for the frame, read a different button, and force
    /// the handbrake on the car directly. The result is a handbrake on RB. The mechanism is not
    /// a rebind, and the difference shows up in exactly one place -- anything that reads the
    /// handbrake has to ask HERE rather than reading the control, or it will be watching a
    /// button that no longer does it.
    ///
    /// WHICH IS THE REASON THIS EXISTS AS A CLASS AT ALL. The front-wheel handbrake pull reads
    /// the handbrake to know when to pull; if it kept reading Control.VehicleHandbrake it would
    /// fire on the gearshift and never on the actual brake. Same lesson as the slide angle:
    /// two features asking one question have to ask it in one place.
    ///
    /// ONLY ON A PAD, and only while the manual box is on. Disabling a control disables it for
    /// every input device, so doing this to a keyboard player would take Space away and give
    /// them nothing back -- the collision it fixes does not exist there.
    /// </summary>
    internal sealed class Brake
    {
        private readonly Settings _cfg;
        private readonly Control? _moved;

        public Brake(Settings cfg)
        {
            _cfg = cfg;
            _moved = Pad.Parse(cfg.ManualHandbrakePad, "moving the handbrake on a pad");

            if (_moved.HasValue)
            {
                Log.Info("Handbrake moves to " + _moved.Value +
                         " on a pad while the manual gearbox is on, because the shift is on the " +
                         "button it was.");
            }
        }

        /// <summary>Whether the handbrake is somewhere other than where the game put it.</summary>
        public bool Moved
        {
            get
            {
                try { return _cfg.ManualBox && _moved.HasValue && Pad.InUse(); }
                catch { return false; }
            }
        }

        /// <summary>
        /// Whether the player is asking for the handbrake, wherever it currently lives.
        ///
        /// IsControlPressed rather than the enabled variant, for the same reason the ignition
        /// reads the exit key that way: the control below is disabled every frame so the game
        /// cannot act on it, and read anyway so we can.
        /// </summary>
        public bool Asked()
        {
            try
            {
                return Moved
                           ? Game.IsControlPressed(_moved.Value)
                           : Game.IsControlPressed(Control.VehicleHandbrake);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Takes the handbrake off its old button for this frame.
        ///
        /// EVERY FRAME OR NOT AT ALL. DisableControlThisFrame lasts exactly one frame by
        /// definition, so a gap of one tick is one handbrake pull the player did not ask for --
        /// and it has to keep being called at a standstill too, which is where the gear logic
        /// otherwise stands off entirely.
        /// </summary>
        public void Silence()
        {
            if (!Moved) return;

            try { Game.DisableControlThisFrame(Control.VehicleHandbrake); }
            catch { /* one frame of handbrake is not worth a throw */ }
        }
    }
}
