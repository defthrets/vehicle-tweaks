# Vehicle Tweaks

Two small changes to how a car behaves in GTA V, for ScriptHookVDotNet. Neither of them
says a word while it works.

Both were written inside [Fumes](https://github.com/defthrets/fumes), the fuel mod, because
that is where the car code was. Neither is about fuel, so they live here instead.

## Manual ignition

The engine is something you operate rather than a side effect of sitting in the seat.

| | |
|---|---|
| **Hold** the exit key | The engine stops. You stay in the seat. |
| **Tap** it | You get out, and the car is left exactly as it stands -- running if it was running, dead if you turned it off first. |
| **Get in** | Nothing starts until you touch the throttle — and then the starter turns over before the engine catches. |

A car is left exactly as it stands: **running, playing, lit, and with the door open**. The radio
keeps its station, loud enough to hear from outside. The headlights stay on. The driver's door
hangs open until you get back in and shut it, or until a passing car shuts it for you.

Turn the engine off first and it all goes with it — a dead car with a stereo on and its lights
blazing is a flat battery, not a feature.

The starter is the game's own: its cranking, its sound, its length for the engine in that
particular car. A half-second we invented would be the same half-second in a moped and a tanker.

**Above a brisk walk (2.5 m/s) the exit key is handed straight back to the game**, whole and
untouched, so vanilla's hold-to-bail still works exactly as it always did. A tap that ejects
you at sixty is not what a tap should do, and refusing the tap on its own would have left no
way out of a moving car at all.

Aircraft are excluded by default. The gesture that parks a car is the one that kills you in a
helicopter, and it is the same key you use to climb out on the ground.

## Steering-activated indicators

Hold the wheel over for a second and that side comes on. It cancels when you hold the wheel
the **other** way for 0.6 s -- which is what straightening out of a turn is -- or after two
seconds of driving straight.

**Letting go does not cancel it**, and the straight-ahead cancel only counts while the car is
actually moving. That is the whole design rather than a detail: it is what lets you signal at
a red light and then release the wheel. A cancel that only asked "is the wheel straight" would
put the indicator out a second after you set it.

Turning the same way again does not put it out either, and that needs saying because
straightening between two turns the same way is itself a turn the other way. Only a *held*
turn the other way cancels; a flick to line the car up does not.

**Hazards are on `J`** (or the pad's modifier + D-pad down) — both sides at once. They get a key
of their own because there is no gesture left: you cannot hold the wheel left and right at the
same time, which is exactly why a real car puts hazards on a separate switch rather than the
stalk. They belong to the car, not to you — leave one on its hazards and it is still on them
when you come back.

## Settings panel — F8

Everything is on one panel, in the game, drawn out of rectangles with no UI library behind it.

| | keyboard | controller |
|---|---|---|
| open / close | `F8` | hold **View / Select / touchpad**, press **D-pad up** |
| move | `UP` `DOWN` | **D-pad up / down** |
| change a setting | `LEFT` `RIGHT` | **D-pad left / right** |
| work a row | `ENTER` | **A** |
| save and close | `BACKSPACE` or `F8` | **B** |
| jump to a page | `TAB` | — |

Directions repeat when held, which matters on a deadzone that steps in hundredths.

**On a controller there is no page button, and none is needed:** UP and DOWN run off the end
of one page onto the next, so the three pages are one continuous list of seventeen rows and
the D-pad alone reaches everything. `TAB` stays as a keyboard shortcut for jumping straight to
a page. The footer shows whichever set of controls you are actually holding.

A change applies the instant you make it, so you can try it on the next corner rather than
alt-tabbing to a text file and reloading. When the panel closes, only the settings you actually
touched are written back — **in place, keeping every comment in the ini**.

Twenty-eight of the thirty-three settings are on the panel across four pages, including every
keyboard binding — the panel, the hazards, the seatbelt, the locks. Each is a rebind row that
waits for you to press the key you want (`ESC`, or **B** on a pad, cancels). "Both features on"
is here too, and the panel deliberately keeps working when it is off — a switch you can only
flip one way is a trap.

The five that are not on it are the controller chords. Those are names out of GTA's own control
list, and a row that cycled through three hundred and sixty of them would not be a menu. They
live in the ini, and the log says what each resolved to at start-up — including, loudly, when it
could not.

## Tests

```bash
.uild.ps1 -Test
```

Fifty assertions, none of which need the game. `Core` references no SHVDN type, so it compiles
into a console exe and runs — which is why the ini writer and the indicator *rules* live there
rather than beside the code that uses them.

That line is where the value is. Every bug this mod has had was in code that could not be run
outside GTA, and both were found by reading, late, after being shipped as working. So the
indicator rules are tested in the words of the design: *a 300 ms flick of opposite lock does not
cancel it*, *ten seconds stopped with the wheel released and it is still on*. The ini suite runs
twice, over an LF and a CRLF copy of the real file, because preserving what the file already uses
is the thing it is checking.

## Parking a car

The small print of leaving one behind, all of it silent.

- **The handbrake goes on** behind you, and comes off when you get back in — otherwise a car
  parked on any of this city's hills is at the bottom of it when you return.
- **`L` locks it**, and the horn answers the way a real one does. Your car only: the one you are
  in, or the last one you drove if you are stood within twelve metres of it. A key in your pocket
  does not lock a stranger's car because they parked closer.
- **`K` takes your seatbelt off.** You are wearing it otherwise — a second after you get in, and
  until you get out. That is the wrong way round on purpose: a belt you have to fasten needs a
  permanent HUD to answer "am I belted?", and turned round the question never comes up.

## Install

Needs [ScriptHookV](http://www.dev-c.com/gtav/scripthookv/) and
[ScriptHookVDotNet 3](https://github.com/scripthookvdotnet/scripthookvdotnet).

Drop `VehicleTweaks.dll` and `VehicleTweaks.ini` into the game's `scripts\` folder. Works on
both the Legacy and Enhanced editions -- they ship the identical `ScriptHookVDotNet3.dll`, so
one build runs on both.

Zero external runtime dependencies: the BCL and SHVDN, nothing else. `scripts\` is one shared
assembly-resolution namespace, so a third-party dll in there is everybody's problem.

## Settings

Everything is in the panel above. The same settings live in `VehicleTweaks.ini`, which explains
what each one is *for* rather than just naming it, if you would rather edit text — hand edits
take effect on a script reload. The names are the ones Fumes used, so numbers you tuned there
can be copied straight across.

The log goes to `scripts\VehicleTweaks\VehicleTweaks.log`, or to
`Documents\VehicleTweaks\` when the game folder is not writable -- which is what happens on a
normal Program Files install. Neither feature says anything on screen, so that file is the
only way either of them can tell you what it did. (The panel does talk, but only to confirm a
save you asked for.)

## Building

The `.NET` SDK on the machine this was written on is broken, so the build drives a
self-contained Roslyn instead. See [tools/README.md](tools/README.md) to restore it, then:

```powershell
.\build.ps1 -Deploy
```

SHVDN shadow-copies script assemblies, so the dll in `scripts\` is usually not locked while
the game runs -- a live redeploy works, and the build tells you which key to press to reload
(it reads `ReloadKeyBinding` out of each install's own `ScriptHookVDotNet.ini` rather than
assuming; the two installs here disagree).
