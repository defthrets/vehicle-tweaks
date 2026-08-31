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
hangs open until you get back in — it is pulled shut once you are actually sitting down — or
until a passing car shuts it for you.

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

Four pages, grouped by **when a setting applies** rather than by which feature owns it:
**DRIVING** (the ignition, the seatbelt), **LEAVING** (what the car keeps, and the locks),
**INDICATORS**, **GENERAL**. Headings group each page, and a row that hangs off a toggle that is
currently off is drawn faint — so a page says at a glance which of it is live.

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

## The handbrake, on a front-driver

A handbrake is a **rear** brake — a cable to the back wheels, which is why a rear-drive car
spins on it. In a front-wheel-drive car the driven wheels are the ones it does not touch, so the
engine can still drag the car forward against a locked rear axle. GTA brakes the car as a unit
and the fronts give up with everything else.

Hold the handbrake *and* the throttle in a front-driver and the fronts keep pulling. The
handbrake alone still stops the car dead, which is what it is for.

**By pushing the car, not by editing its handling.** `HandlingData` has a `HandBrakeForce` on it
and turning that down looks like the obvious fix — it is not. Handling is loaded per *model*, so
weakening it weakens the handbrake on every other example of that model in the world, traffic
included, for the rest of the session, with nothing to put it back.

Front-drive is asked of the wheels themselves rather than inferred from handling numbers. Four
wheel drive is a different situation — its rear wheels *are* driven — and is left alone.

## The speedo

Three seven-segment digits, the unit, a rev strip and the gear — sitting just right of the
minimap, level with its bottom edge.

The digits are **drawn as segments rather than typed in a font**, because none of the four fonts
GTA ships is a seven-segment one: any of them would have been an ordinary number in an ordinary
typeface with a box behind it. Unlit segments are drawn faintly, the way a real display shows
them — an LCD reading 42 also faintly shows the 8 it is not lighting.

The rev strip is cells rather than a sliding bar, for the same reason. A bar that slides is a
progress bar; cells that snap on one at a time are a rev counter, and they read in peripheral
vision, which is the only way anybody looks at one. The last fifth is red whatever colour the
rest is. Reverse shows as a lower-case `r` — the only R seven segments can make.

Two warning lamps sit at the right-hand end. The **engine lamp** is green, amber or red by
condition; the **oil lamp** lights only when the oil is low, because a warning light that is on
all the time is decoration. Both colours are fixed rather than following the display's, since a
warning lamp is a judgement and everyone already knows what a red one means. GTA has tracked
both values since 2013 and never once shown you either.

The room for the lamps is always reserved, lit or not — a light that appears and disappears
would take the panel's width with it and make the box jump.

Position, size, opacity, colour, units and every part of it are settings. **The panel keeps
drawing the speedo while it is open, including on foot**, so holding LEFT or RIGHT on *Across*
moves the thing while you watch it — the default position is an estimate off a screenshot, and
where the minimap really ends depends on your safe-zone slider and aspect ratio, which no script
can ask about.

## Drift tyres

GTA Online's own, not an imitation. `SET_DRIFT_TYRES` is the flag the Los Santos Tuners update
added for the drift tuning you buy at a garage, so what this switches on is the handling
Rockstar wrote, engaging on their terms and on the vehicles they allowed it on. Nothing here
models grip or fakes a slide.

**Off by default** — it is the only setting in this mod that changes how a car goes round a
corner. Everything else adds something the game was missing; this replaces something it already
had.

Cars it was fitted to lose it again when you switch it off or reload. A car that *already* had
drift tuning is left alone — somebody paid for that, and it is not ours to remove.

## Crashes, and the dash

Hit something above 100 km/h and the world drops into slow motion for a moment. **This is the
only thing here that reaches outside your own car** — time scale is global and persistent, so
it is put back when the moment ends, when you leave the car, when you switch it off, when
anything throws, and when the script shuts down. A crash is detected as *speed that vanishes*
rather than as a collision: GTA will happily report a kerb, and 8 m/s lost in one frame is about
50 g, which braking cannot do and a scrape cannot do.

The cabin lights with the headlights, so a tunnel at noon lights your instruments the way a real
dash does — and it goes out again when you get out.

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
