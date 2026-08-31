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
| **Get in** | Nothing starts until you touch the throttle, and only if the engine was already off. |

A car left running keeps its radio on, loud enough to hear from outside it, on whatever
station was playing as you got out. A car left switched off gets its radio switched off too --
a dead car with a stereo on is a flat battery.

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

## Settings panel — SHIFT+V

Everything is on one panel, in the game, drawn out of rectangles with no UI library behind it.

| | |
|---|---|
| `SHIFT+V` | opens and closes it |
| `TAB` | IGNITION / BLINKERS / GENERAL |
| `UP` `DOWN` | move |
| `LEFT` `RIGHT` | change the selected setting |
| `ENTER` | works a row — toggles it, or starts a key rebind |
| `BACKSPACE` | saves and closes, same as `SHIFT+V` |

A change applies the instant you make it, so you can try it on the next corner rather than
alt-tabbing to a text file and reloading. When the panel closes, only the settings you actually
touched are written back — **in place, keeping every comment in the ini**.

All seventeen settings are here, including the panel's own key: GENERAL has a modifier row and
a rebind row that waits for you to press the key you want (`ESC` cancels). "Both features on" is
here too, and the panel deliberately keeps working when it is off — a switch you can only flip
one way is a trap.

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
