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
| jump to a page | `TAB` | **LB** / **RB** |

**Seven pages**, grouped by *when* a setting applies rather than by which feature owns it:
**DRIVING**, **GRIP**, **LEAVING**, **AUTOPILOT**, **INDICATORS**, **SPEEDO**, **GENERAL**.
Headings group each page, and a row hanging off a toggle that is currently off is drawn faint —
so a page says at a glance which of it is live.

Directions repeat when held, which matters on a deadzone that steps in hundredths.

**The whole panel has one size.** Every dimension in it — margins, row height, each of six text
scales — is a fraction of a single `Zoom` constant, so it can be made bigger or smaller without
anyone having to get thirty ratios right by hand. That is how a menu ends up with a title that no
longer fits its own bar.

### It moves

Four things are eased rather than switched, all of them **exponentially** — each moves a fraction
of the distance still left, every frame. That means no timeline to remember, no end to detect, and
an animation interrupted half way simply changes where it is heading. Four quick presses of `DOWN`
are one continuous movement instead of four that cancel each other, which is the failure the
obvious version has and it reads as the menu skipping rows.

- **The panel** slides in from the left edge it is pinned to, and fades. It keeps being drawn
  while it leaves — the half nobody writes. A panel that vanishes on the frame you dismiss it has
  an opening animation and no closing one, which reads as being interrupted rather than put away.
  Input stops immediately either way: `IsOpen` is the gate, not the picture.
- **The highlight** travels between rows, and is drawn once wherever it has got to rather than by
  whichever row owns it — a thing drawn inside the row loop is tied to a row, and a thing tied to
  a row cannot be between two of them. It *snaps* on opening and on a page change, because
  sweeping the length of the panel to reach the row you were already on is not feedback.
- **The tab underline** travels on a page change. It is the one part of the panel that can say
  which way you just went; the names cannot, because they do not move.
- **A nudged value** lights up for a fifth of a second. On a number moving in hundredths, held
  down on a D-pad, a digit going from 0.34 to 0.35 is not a signal at that size. The flash is.

Frame time is sanity-checked before any of it: a loading screen or an alt-tab hands back a delta
of a second or more, and an eased value given that arrives instantly — so the animation would be
missed on exactly the frames where the game was busy.

### On a controller

**The D-pad alone reaches everything.** `UP` and `DOWN` run off the end of one page onto the next,
so the seven pages are one continuous list. That was the deliberate answer to a pad having no
spare buttons, and it is still what the panel is built on.

**LB and RB jump a page** on top of that. They are the *script* control group, not the frontend
one — both have an LB and an RB in the enum and only one of them is meant for scripts, which is
the same reason everything else here is driven off the phone's buttons. They are safe because the
panel is deaf: whatever those buttons otherwise do in a car, they are disabled for as long as it
is open. Nothing depends on them firing, so if it turns out they do not read during gameplay, all
that is lost is a shortcut.

The footer shows whichever set of controls you are actually holding.

### Saving

A change applies the instant you make it, so you can try it on the next corner rather than
alt-tabbing to a text file and reloading. When the panel closes, only the settings you actually
touched are written back — **in place, keeping every comment in the ini**.

**73 of the 81 settings are on the panel**, including every keyboard binding — the panel, the
hazards, the seatbelt, the locks, the cabin light. Each is a rebind row that waits for you to
press the key you want (`ESC`, or **B** on a pad, cancels). "Everything on" is here too, and the
panel deliberately keeps working when it is off — a switch you can only flip one way is a trap.

The **8** that are not on it are the controller chords. Those are names out of GTA's own control
list, and a row cycling through three hundred and sixty of them would not be a menu. They live in
the ini, and the log says what each resolved to at start-up — including, loudly, when it could
not.

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

Hold the handbrake *and* the throttle in a front-driver and the fronts keep pulling — **and
spin**. The handbrake alone still stops the car dead, which is what it is for.

Those are two different things and only the second one is visible. Pushing the car moves the
*car*; the wheels under it roll at whatever speed the road is going past, which against a
handbrake is barely at all. Wheelspin is not a fast roll — it is the tyre losing to the engine,
and no force produces it. `SET_VEHICLE_BURNOUT` does, and it spins the *driven* wheels, which
on a front-driver are the front ones.

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

Position, size, opacity, colour, units and every part of it are settings. **The panel keeps
drawing the speedo while it is open, including on foot**, so holding LEFT or RIGHT on *Across*
moves the thing while you watch it — the default position is an estimate off a screenshot, and
where the minimap really ends depends on your safe-zone slider and aspect ratio, which no script
can ask about.

## Holding a slide

**The engine keeps pulling while the car is sideways.** GTA bogs a car down the moment it stops
pointing where it is going, which is what makes long drifts collapse — the back comes out, the
power falls away underneath you, and the slide dies of its own accord rather than because you
ended it.

The slide is *measured*, not guessed: the angle between where the car points and where it is
actually travelling is what a drift **is**. The compensation ramps in as that angle opens up
rather than switching on at a line, because a car that suddenly found more power at twelve
degrees would be harder to hold than one that never found any.

**And more steering lock to catch it with**, on a ramp of its own. Catching a slide means winding on
more opposite lock than the car came with, and once you run out there is nothing to do but wait
and see where it goes — which is most of why GTA slides feel like they end of their own accord
rather than because you saved them. Extended steering angle is the single most common
modification made to a real drift car, for exactly this reason.

**How much** extra lock and **how far sideways** it has all arrived by are both settings. The
second one used to be hard-coded at three times the slide angle, and it is the half you actually
feel: too early and the car goes vague the moment it steps out, too late and you run out of
steering in the one moment you needed it. The power keeps its own ramp — it is undoing something
the game does to a sideways car, so it follows the game's problem rather than your taste.

Both are per *car* and per moment — `EnginePowerMultiplier` and `SteeringLimitMultiplier` — not
handling data, which is per *model* and would change every other example of that car in the world
for the session. And it asks the game which wheels steer rather than assuming the front two.

## Drift mode

**A slider, 0.00 to 1.00, on any car** — 0.20 by default. A slider rather than a short list,
because how much a car drifts is plainly continuous and picking between *Light* and *Medium* is
not the same as finding the one that feels right. Nudge it on the panel while driving and the car
changes under you.

Two of Rockstar's own mechanisms underneath, and nothing here models grip or fakes a slide.

`SET_DRIFT_TYRES` is the Drift Races tuning and it is the better of the two — the handling
Rockstar wrote, engaging on their terms. But it is **gated to the cars that update gave it to**,
and on anything else it does not fail: it is simply ignored, which is the worst way for a
feature to not work. On its own it was never going to be "drift mode on any car".

So the second one: the **friction override**, which goes on anything and takes a *float*. That
last part is what makes this a slider at all — the obvious partner native takes a whole number on
a scale nobody has written down, which is three steps wearing a decimal point. Friction is scaled
by the slider on every car, so turning the knob does something whatever you are driving. Full
tilt takes half the grip away — half and not all, because a car with no friction does not drift,
it stops being connected to the road.

The real tuning is asked for first and the game is asked back whether it took. The log says which
one each car got.

**Off by default** — it is the only setting in this mod that changes how a car goes round a
corner. Everything else adds something the game was missing; this replaces something it had.

Cars lose it again when you switch it off or reload. A car that *already* had drift tuning is
left alone — somebody paid for that, and it is not ours to remove.

## Coming back to it

**Your car is still there.** GTA throws away vehicles nobody is looking at, which makes nonsense
of everything else here — a car left running, lit, locked and handbraked is not much use if it
is deleted the moment your back is turned. Exactly one car is held, and taking a different one
hands the previous back; persistence is a promise the game may never clean something up, and
handing that out freely fills the world with cars nobody is returning for.

**A blip on it**, because still being there is no good if you cannot remember which street — and
it goes away while you are sitting in the car. The blip answers *where did I leave it*, which is
not a question anybody has from the driver's seat.

**It remembers its station**, so getting back in puts you on what you left it on rather than
whatever the game picks. This session only — remembering across sessions means a file, and this
mod writes nothing but its log.

## The autopilot

**The speed digits change colour** when the speed is not your own doing — one colour while
cruise control is holding, another while the car is driving itself. A cruise control that
silently refuses to go faster is a bug, not a feature.

**Cruise control** (`U`) holds the speed you set it at. A *cap*, not a throttle: the game is told
the car may not exceed a speed, so steering and braking stay entirely yours and the worst a bug
can do is limit a car rather than drive one. Braking cancels it, as in every car that has ever
had it.

**Self driving** (`O`, off by default) hands over to `CruiseWithVehicle` — the task every ambient
driver in the city is already running, so the car obeys lights, overtakes and gives way exactly
as traffic does. Nothing here steers.

Four styles: **Cautious** stops for people as well as cars; **Normal** stops for cars and waits
at lights; **Brisk** goes around what is in the way instead of queueing; **Reckless** neither
stops nor waits. Reckless is not a joke setting — it will take you through a junction on red.

**Touch any control and it is yours again** — throttle, brake, handbrake or steering. It also
stops on the key, on getting out, on being switched off, and when the script shuts down. Handing
the player's own ped to a task is the most control this mod ever takes, and grabbing the wheel is
what anybody would try first, so that is what works.

## The warning lamps

**Their own cluster, in their own place — not part of the speedo.** The **engine lamp** is green,
amber or red by condition; the **oil lamp** lights only when the oil is low, because a warning
light that is on all the time is decoration. GTA has tracked both values since 2013 and never
once shown you either.

They keep their own colours rather than following the display's: the readout is *information*
and can be any colour you like, but a warning lamp is a *judgement*, and everybody already knows
what a red one means.

They were originally bolted onto the end of the speed readout, which was wrong. A readout is one
object you glance at continuously; a warning lamp is meant to catch your eye precisely by *not*
being part of what you were already looking at.

## Crashes, and the dash

Hit something above 100 km/h and the world drops into slow motion for a moment. **This is the
only thing here that reaches outside your own car** — time scale is global and persistent, so
it is put back when the moment ends, when you leave the car, when you switch it off, when
anything throws, and when the script shuts down. A crash is detected as *speed that vanishes*
rather than as a collision: GTA will happily report a kerb, and 8 m/s lost in one frame is about
50 g, which braking cannot do and a scrape cannot do.

**The cabin light is on `K`**, or the panel modifier and D-pad left on a pad. It used to follow
the headlights, on the argument that a real dashboard lights with the side lights — true of the
*instruments*, and not true of the cabin light, which is the one thing in a car that is
explicitly not automatic. Every car ever built has it on its own switch, because the point of it
is that you decide when the inside is lit; following the headlights meant it came on for every
tunnel and every dusk, which is exactly when you can see fine and don't want glare on the glass.

The switch is a preference and follows you, so leaving it on and getting into something else
lights that one too. The light itself goes out when you get out — a parked car glowing from the
inside forever would be this script's litter.

## Repairing as you drive

**Off by default**, like everything here that removes a consequence rather than adding one. The
rest of this mod corrects a car that still behaves the way you expect; this undoes the last few
minutes of your driving.

**The clock restarts whether or not anything was repaired**, and that is the whole difference
between a repair every few minutes and invincibility. Left to run on, an undamaged car would sit
with its timer already expired and fix the next scrape a frame after it happened — the same code,
a completely different game. Damage is meant to last up to the interval; that *is* the interval.

**A wreck is left as a wreck.** Repairing a dead car does not repair it, it resurrects it — engine
running, flames out, upright again — and nobody asking for their scratches buffed out is asking
for that.

It looks before it acts, so an undamaged car never gets the small visible jolt of having its
bodywork snapped straight for nothing. Three questions, because the game answers them separately:
damage decals are the bullet holes and scrapes and say nothing about a dead engine; the healths
cover the dents and the mechanicals and say nothing about a flat tyre. `IsDamaged` looks like a
fourth and is not — it is obsolete and means `HasDamageDecals`, the same question dressed as the
general one.

## Parking a car

The small print of leaving one behind, all of it silent.

- **The lights go out with the ignition.** Holding the key kills the whole car, which is what
  turning one off *is* — nobody has ever switched off an engine and left the headlights burning.
  A *tap* leaves them exactly as they were, which is the other half of the same idea: what you
  did not switch off stays on. They come back the moment the engine does.

  Underneath it is a scripted light **override** rather than a switch — there is no "turn the
  headlights off" in this game, only a setting that overrules the driver's own key for as long as
  it stands. So it is handed to the same bookkeeping that looks after abandoned cars and lifted
  when you get back in. Left in place it would be a car whose headlight key does nothing for the
  rest of the session, with nothing anywhere to say why.
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

A deploy **merges** any settings the installed ini has never heard of, comment blocks and all,
and leaves every existing line exactly where it is. That matters because the settings panel
writes to that same file: where your speedo sits, how big it is, which keys you rebound. A new
build needs the settings that did not exist yet — it does not need to touch the ones you set.

`-FreshIni` *replaces* the installed ini, values and all. It is rarely the right tool, and it
says plainly what it is about to destroy.
