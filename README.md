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

**Hazards are on `J`** (or the pad's modifier + D-pad down — hold **R3**) — both sides at once. They get a key
of their own because there is no gesture left: you cannot hold the wheel left and right at the
same time, which is exactly why a real car puts hazards on a separate switch rather than the
stalk. They belong to the car, not to you — leave one on its hazards and it is still on them
when you come back.

## Settings panel — F12

Everything is on one panel, in the game, drawn out of rectangles with no UI library behind it.

| | keyboard | controller |
|---|---|---|
| open / close | `F12` | hold **R3**, press **D-pad left** |
| move | `UP` `DOWN` | **D-pad up / down** |
| change a setting | `LEFT` `RIGHT` | **D-pad left / right** |
| work a row | `ENTER` | **A** |
| save and close | `BACKSPACE` or `F12` | **B** |
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

### Pictures, because rectangles are rationed

The game keeps a budget of immediate draws per frame that **every script shares**. When another mod
puts up a big menu, the draw calls that arrive after it are dropped without a word — which on a
seven-segment display is a digit with one bar lit and a rev strip that is not there, and on the
GEARS page is a chart with labels and no bars. That is what "the speedo glitches when something else
is open" was.

So the things that were many rectangles are now one picture each: the eleven digit glyphs (two
sets, with and without the faint unlit segments, made from the exact geometry the rectangles used)
and the nine tab icons. A picture is one draw; the icons alone were about two hundred. The
rectangles stay as the fallback for a `scripts\` folder the pictures did not reach. Same trick
Fumes uses for its digits, for the same reason.

### A texture stays up for a tenth of a second

ScriptHookV keeps every drawn texture on screen for the time it was given, and SHVDN gives a
hundred milliseconds. Harmless for a title drawn every frame; on a seven-segment display it meant
the moment a 3 became a 4 the 3 was still there for six frames, over the 4 — and a car accelerating
changes its last digit faster than that, so the digit was never clean. That was "it flickers when
driving". `Sprite` keeps a ledger of how many times each file was drawn this frame and last, and
at the end of every tick draws the difference again — one pixel, off screen, transparent — so the
slot ScriptHookV would have kept showing is overwritten with nothing.

### The title

**"Vehicle Tweaks" is set in Saira** — heaviest weight, widest width, italic: squared off and
technical, and every letter the shape a person expects it to be.

It was blackletter (UnifrakturCook) first, and that was the right idea badly served by its own
alphabet. A Fraktur **k** is a shape you have to already know to read, and in a two-word title taken
in at a glance the one letter nobody could place was the k in *Tweaks*. A wordmark that has to be
deciphered is not a wordmark.

The game has neither font: `SET_TEXT_FONT` offers Chalet, House Script, Pricedown and a monospace.
So the title is rendered once, outside the game, to a white-on-transparent PNG in `assets\`, and
drawn through `CustomSprite` — the same path Fumes draws its pump with, and the only way a font the
game does not have can ship in a `scripts\` folder at all. Deploy copies it beside the log; the
colour is applied at draw time so it fades with the panel; and if the file is missing the title
falls back to text with one line in the log. It sits *inside* the title bar now — the blackletter
was a badge and stood proud of the edge it was pinned to; an italic wordmark is a name, and a name
belongs on the line with the page it names.

### Where you are, next to how far along

The page name used to sit small in the top-right corner — a whole panel's width away from the nine
icons it was the answer to. The strip said *you are on the second of nine* and the name said *which
page that was*, and the two never met, so working out where you were meant reading both and joining
them up. The name is now centred **under the strip** and half again as big: the lit icon and the
word beneath it are one control. The corner keeps the count, which was never in the name anyway.

Headings sit lower in their row, so the air falls *above* a group rather than either side of it and
a section reads as starting rather than continuing. The hint under the list — the line that says
what the row you are on actually does — is bigger and brighter than the controls line below it,
which is reference and was competing with it for the same attention.

### One thing on the right of every row

Each row used to say its state **twice**: a switch drew a sliding knob *and* the word ON beside it;
a number drew a slider *and* the figure. Two objects per row in two columns — and the control column
was ragged, because a pill and a track are not the same width. The word is the exact half and the
picture is the quick half, and a settings row does want both; it does not want them in two places.

**The word in the chip is never dark**, and that took two passes to get right. The first chip was a
solid amber block with a near-black `ON` in it — correct contrast on paper, a black smudge on
screen. At this size, in this font, the game does not render dark letters cleanly on a light field;
the black outline and drop shadow it puts round text made it worse, and turning those off was not
enough. So the state moved off the word and *under* it: an amber bar along the bottom edge of the
chip says on, the word goes amber with it, and off is the same block with a grey word and no bar.
The bar's width is the fraction rather than a flag, so a switch caught mid-change still slides. The
chip itself is *lighter* than the panel, not darker — on a near-black ground a dark block reads as a
hole rather than as something you can work.

So the word lives **inside** the switch now — one chip, filled amber for on, a hole in the panel for
off — and every chip is the same width, so the panel has a single right-hand edge to read down. The
slider lost its knob, which was the loudest thing on a row whose point is the number. A choice keeps
its arrows, but they hug the value rather than floating one column away and one off the panel edge.
A key and a door are the same chip, sized to what they hold.

The caret in front of the selected row is gone too: the amber tint and the bar down the left already
say which row you are on, and a third mark saying it pushed the label off the line every other row
sits on.

Headings hang on a rule that starts where their words stop, instead of a bullet with a full-width
line underneath — a bullet is a list marker and this is not a list, and a line under the words reads
as a divider belonging to the row below. The tab strip is grouped and centred with the live page on
a lit block, rather than nine icons pushed to the panel's corners with more gap than icon between
them. Rows are taller, and every live row's label is brighter: the old grey was legible on a monitor
two feet away and not from a sofa.

### Each row drawn as the thing it is

A row of text on the right said `ON` or `0.35 s` and left you to know what that meant. Now:

- a **switch** is a pill with a knob that slides to the side it is on — amber is on, and the word
  stays beside it, because a knob on the right means "on" in some countries and "off" in others
- a **number on a range** gets a track filled to where the value sits. `0.35 s` says how long; the
  track says how long *out of how long it could be*, which is what you actually want to know
  when deciding whether to nudge it
- a **choice from a list** shows the arrows that step through it, faint on every such row and lit
  on the one you are on — a row that cycles looks exactly like one that does not until you press
  LEFT, so the arrows say so in advance
- a **key** is drawn as a keycap, and the one row that opens something rather than holding a
  value is an amber keycap reading OPEN — a door reads as a button, not as a setting whose value
  happens to be "OPEN"
- a **section heading** carries a square bullet, and rows have a touch more air

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
- **A page turn** steps the whole body aside and dims it while the next page arrives, so a
  change of page reads as one page leaving and another coming rather than the words under your
  eyes being swapped for different words. Knobs slide and slider fills ease on their own rows,
  so a held D-pad reads as a bar sliding rather than stepping.
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

### The modifier is R3, because nothing on a pad is free

Not "few buttons are free" — **none**. Every button on an Xbox pad is bound in single player: LB is
the weapon wheel, RB is the handbrake, L3 is the horn in a car and stealth on foot, R3 looks behind,
VIEW cycles the camera, START pauses, and each D-pad direction is the phone, the character wheel,
the radio wheel or a detonator. So the modifier cannot be picked by being unbound. It is picked by
**what it costs to hold**:

| | held | cost |
|---|---|---|
| `LB` | weapon wheel | the wheel is *worked with the D-pad* — the chord and choosing a gun are the same gesture |
| `RB` | handbrake | pulls the handbrake at speed |
| `L3` | horn / stealth | sounds the horn, and stealth **stays** toggled |
| `VIEW` | camera | cycles the camera under a menu you are about to read |
| `R3` | look behind | camera swings, and comes back when you let go |

R3 is the only one that leaves nothing changed behind it: no wheel, no HUD, no sound, and nothing
taken away from the player, because look-behind is a *hold* action that undoes itself. It is also
the only candidate the **other** thumb can hold while the D-pad is being pressed.

**This shared modifier is all seven chords**, so the panel, the hazards, the locks, the seatbelt,
cruise, the autopilot and the cabin light all moved together. The panel had been on LB, which meant
that holding LB to pick a weapon and touching the D-pad opened the panel over the weapon wheel,
toggled the hazards, or locked the doors — the hazards being the one that looked like a bug of its
own. A gate was tried (`IS_HUD_COMPONENT_ACTIVE(19)`) and it silenced every chord instead, because
that native appears to answer "is the component enabled", not "is it on screen". Moving off LB
removes the question rather than answering it, so the gate is gone and a start-up warning takes its
place if anyone sets `PadModifier` back to LB.

Moving off LB also **gives the shoulders back**: `LB` and `RB` turn the page again, which they had
stood down from while the chord was using LB.

### You can drive with it open

The panel used to take the whole car off you — no throttle, brake, steering or handbrake — on the
reasoning that the arrow keys would otherwise be steering something nobody was looking at. They
would not: on a keyboard the panel is the **arrows** and driving is **WASD**; on a pad the panel is
the **D-pad** and driving is the **sticks and triggers**. Nothing collides.

So walking and driving are left alone, and with them the eight features that only *write* to the car
— power, torque, camber, track, ride height, drift grip, repairs, the brake flash — which now run
with the panel up. That is what a tuning panel is for: a slider you cannot feel move is a slider you
have to guess at, and the next corner is the test.

The seven that *read* a key or a chord still wait for it to shut, or moving the highlight down a
page would toggle the hazards underneath it. Getting out stays blocked, because the ignition's
hold-to-stop is one of the things standing down while the panel is up. The spawner still stops
everything — it is a browser, not a tuning surface, and the car it is about does not exist yet.

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

## Brake lights that flash when you stand on it

The **emergency stop signal** — a real thing rather than a game effect. UN regulation ECE R48 lets a
car flash its stop lamps under heavy braking at four hertz give or take one, and most of what has
come out of Europe since about 2005 does. The point is the driver *behind*: a steady red lamp says
"slowing", and by the time they have worked out how fast, the gap has gone. A flashing one says now.

**It fires on the car actually slowing down, not on a key.** The obvious way to build this is a key
combination — hold brake and handbrake, flash the lights — and that is a different feature wearing
this one's name, because it fires when you *ask* rather than when you brake hard, which is the one
moment your hands are busy. The speed is already sampled every frame for the speedometer, so the
deceleration is free: the difference between two samples over the time between them. Standing on the
brakes in a road car is around 8 m/s². Lifting off is one or two. The default threshold is **6**,
above 50 km/h, which sits above anything you do on purpose and below anything you do by accident.

**A crash is not braking.** A wall stops a car an order of magnitude harder than a tyre can, so a
sample over the ceiling is *thrown away* rather than clamped — an impact reads as no deceleration at
all instead of as the hardest braking ever recorded. What is left is smoothed, because one frame of
suspension noise is not a stop either. It lets go at half the force it takes hold at, so a stop that
is still a hard stop keeps flashing instead of stuttering as the number wobbles over the line.

**Timed in milliseconds, not frames.** The flash phase comes off the game clock, so it blinks at the
rate the setting says at 30 fps and at 144 — the bug every version of this in every game has had at
least once. Nothing is forced between flashes: the game goes back to deciding, which it does
correctly, so there is nothing to hand back when it ends.

## The car spawner

Every vehicle in the game, browsable by class, with a picture of the highlighted one.

**The first row of the panel** — top of the DRIVING page, where the highlight already is when it
opens, so it is the one row you can reach without moving at all. `F10`, then `ENTER`. That has
always been how a pad gets there, because the D-pad has four directions and all four are spoken
for.

It used to have `F7` as well. [Weapon Tweaks](../weapon-tweaks) opens its panel on F7 now, and two
mods on one key is both of them answering it — so this one let go, because it is the one with a
door already. `SpawnerKey` is still a row on the GENERAL page and still in the ini: press `ENTER`
on it and choose a key if you want the shortcut back. `F9` is free.

A door rather than a setting, and that is also why it is safe at the top. A held chord used to
nudge whatever row the highlight landed on, which made the first row the most dangerous seat in the
panel — manual ignition sat there and switched itself off. Buttons are disarmed until released now,
and a door has no value to nudge in any case: `LEFT` and `RIGHT` do nothing to it.

**The list ships with the mod, and it is not SHVDN's.** `GTA.VehicleHash` is frozen at whatever the
wrapper last shipped — 843 names, 841 distinct, and nothing added by a game update since. That was
**eighty vehicles short**: the whole of the 2024 and 2025 packs, the drift cars, the Christmas 2023
additions. So the spawner carries its own list of **921 model names**, taken from the community's
dump of the game's own vehicle metadata, every one verified to hash back to the hash that dump
records for it.

Each is still checked against `IsInCdImage` before it is offered — the game's own answer to whether
that model is actually installed — so the list is allowed to run ahead of a Legacy install without
the menu ever lying about what it can spawn. A menu that offers a car it cannot spawn is worse than
a shorter menu; one that cannot offer the car you just bought is worse than both.

**The pictures are the mod's own, because the game's are out of reach.** Two separate texture
inventories agree: the only per-vehicle artwork in a streamable dictionary is the manufacturer
badge in `mpcarhud`, and the photographs on the in-game websites live inside their web pages, not
in anything `DRAW_SPRITE` can be handed. So the mod ships one PNG per model — the car cut out on
transparent, 512×288, about 20 KB each and 16 MB the lot — in `scripts\VehicleTweaks\cars\`, drawn
through the same `CustomSprite` path as the title. They are the shots from the
[FiveM vehicle reference](https://docs.fivem.net/docs/game-references/vehicle-models/), which
are renders of the game's own models — 914 of them. The remaining seven are too new for that
reference, so those are in-game photographs from [gtacars.net](https://gtacars.net) instead, which
is why they carry a background where the rest are cut out. **All 921 have a picture.** A model with
no file there — an add-on car — still falls through to the badge:
a dictionary named after the model first, in case the add-on shipped one, then the badge, then the
class icon, each waited for in turn so a fast badge cannot beat a slow picture.

**Nothing is loaded until the highlight has been still for a moment.** A texture ScriptHookV has
loaded stays loaded until the scripts reload — there is no handing one back — so loading as you
scrolled would leave every car you passed in memory. What is loaded is what you stopped on, at
about half a megabyte each; the log says at start-up how many of the catalogue have a picture.

**No two rows read the same.** The game gives every variant of a vehicle one display name, so the
list had two rows called "Sentinel XS", three called "Bison", four called "Boxville" and ten called
"Freight Train" — 189 models sharing 72 names, with no way to tell which was which without spawning
one. The model name already carries the answer, so it is read rather than invented: a drift tune
says `drift` at the front, a variant says which one it is with the number on the end. Only the rows
that actually clash are marked, so `Boxville` stays **Boxville** and its variants become **(2)**,
**(3)** and **(4)**. Whatever is *still* doubled after that — `freight2` and `freightcar2` are both
"the second of their name" — gets the model code instead, which is ugly and unambiguous, in that
order of priority.

**The model name is the name, because the list is names.** It used to be SHVDN's enum names, ten of
which are not what the files call the car — `FireTruck` is `firetruk`, `RE7B` is `le7b`, `Khanjari`
is `khanjali` — and each had to be corrected by hand against its own hash. Building from a list of
names instead makes that whole class of mistake impossible: the MODEL row, the picture filename and
the string you would type into another mod's ini are one and the same thing.

**There is no live view any more, and that is what having pictures is for.** The highlighted car
used to be spawned as well — first straight ahead, where it sat behind the panels, then stood beside
them on a turntable, frozen and without collision. The whole argument for it was that most cars had
no picture and for those the choice was the real thing or nothing. Every model in the game has a
picture now, so the argument is gone: what is left is a car in the street, a model streamed for
every row you rest on, and a second thing moving while you are reading a list. The car arrives when
you press **A** and not before.

**The stats come from the game, and the bars are relative to the best in it.** Top speed,
acceleration, braking and grip, plus seats, price and — the one thing you might actually want to
type — the **model code**. The game reports a top speed in metres a second and an acceleration in
units nobody has ever explained; what you want to know is whether this one is quick, and quick only
means anything next to something else. So the maximum of each stat is worked out while the
catalogue is read, and every bar is drawn against it.

Reading 921 models is a couple of thousand native calls, so it is done sixty a frame with a
progress bar rather than in one tick — it finishes in about a quarter of a second and nobody sees
it happen.

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

## Gear by gear

A **chart** page: torque per gear, and how slidy the tyres are per gear, six bars each.

A chart rather than six numbers, because one number is not what anybody means when they say a car
needs more torque. They mean it needs more in *second* — the gear a slide is held in, the gear you
leave a junction in — and no more in fifth, where extra torque is a car that will not settle. More
in second than first, tailing off to fifth, is a **shape**, and a shape is something you can see
on a chart and cannot read off a column of decimals. The faint line across the torque chart is
"as it came", so every bar is read against it.

`ENTER` on a chart to shape it — `LEFT`/`RIGHT` pick the gear, `UP`/`DOWN` move it, `ENTER` or
`BACK` when done. That is the one **mode** this panel has, and it exists because a D-pad has four
directions: between rows, up and down *move* and left and right *change*; inside a chart, left and
right have to choose the bar, so up and down become the change. The footer says which you are in.

Both compose with what is already there rather than competing. Gear torque **multiplies** the
TUNING baseline, and the slide compensation multiplies that. Gear slide **adds** to the GRIP drift
slider — a slider at nought with second set to a half is a car planted everywhere except second,
which is exactly the picture drawn. Seventh gear and up use the sixth bar.

### Playing nicely with other mods' hotkeys

`Up` is the phone button — on a keyboard and on a pad — and `Up` is how you move through this
panel, so every press of it put the Hoodrich phone on top of the settings. Disabling the control
while the panel is open does not help: a script that reads controls the way this one does reads
through a disable, and which script runs first in a frame is not something either of you gets to
choose.

So the signal is a **variable, not a control**. Every SHVDN script lives in the one AppDomain and
its data slots are shared: while the panel or the spawner is up, this mod sets
`AppDomain.CurrentDomain.SetData("MenuOpen", "Vehicle Tweaks")`, and clears it on the way out. A
script with a hotkey reads it before it listens. Hoodrich does; anything else that adopts the same
name gets the same courtesy. No native, no frame order, nothing to be first at.

### Icons

The page tabs are icons now, with only the current page named. Nine pages of capitals across a
panel this narrow shrinks the type to a row of grey smudges, and the fix is not smaller type — it is
not drawing eight names nobody is reading. Each icon is a seven-by-seven grid of cells drawn from
rectangles, for the same reason the speedo's digits are: no font this game ships has a glyph for a
cog or a tyre, and the one time this mod tried a symbol it drew the missing-character box. They are
written in the source as rows of `#` and `.`, so an icon is readable as the thing it draws.

## Tuning

Sliders for what the car is, before anything happens to it.

| | range | what it is |
|---|---|---|
| **Power** | 0.25 – 3.00 | the top end — what the engine does once it is already spinning |
| **Torque** | 0.25 – 3.00 | the low end — pulling away, out of a corner, getting the back out |
| **Steering lock** | 1.00 – 2.50 | how far the front wheels turn, all the time |
| Tyres never burst | on / off | kerbs, spikes and gunfire stop mattering |
| Wheels never break off | on / off | a hard kerb strike leaves the wheel where it was |

**Torque is the half you actually feel**, and it is what most people mean when they say a car
needs more power. Torque without power is a car that leaps off the line and runs out of legs;
power without torque is one that does nothing until it is already moving. The game keeps them
separate, so this does too.

**None of it is handling data.** Every field here is per *car* and per moment. `HandlingData` is
per *model* and permanent for the session — write to it and every other example of that car in the
world changes, and stays changed until the game restarts. These are small instruments aimed at the
car you are in, and they are all given back when you get out, **by handle**.

**They compose with the slide settings rather than competing with them.** That is the whole reason
the slide compensation and these sliders live in one class: two features writing
`EnginePowerMultiplier` is not a merge conflict, it is a car that flickers between two numbers
depending on which ran last, and the symptom is a car that feels inconsistent rather than an error
anybody can find. So the sliders are the baseline and the slide compensation **multiplies** it —
1.5 power that finds another 1.4 while sideways is 2.1, which is what both settings said they
would do.

The two tyre flags are tracked differently from the multipliers, and the difference matters on the
way out. A multiplier of 1.00 *is* the standard value, so writing it costs nothing; those flags are
normally **true**, so anything setting them back to true on every car it touched would be re-arming
tyres another mod had deliberately disarmed. Only what this turned off gets turned back on.

There is no top-speed slider, deliberately: `MaxSpeed` is a cap rather than a raise, and cruise
control already owns that field. A second writer is the exact thing this page exists to avoid.

## Stance

Camber, track width and ride height, per axle. Six sliders on the TUNING page.

**The bones were the right idea and they do not work.** Camber *is* the Y rotation of a wheel bone
and track width *is* its X offset, both settable through `EntityBone` — and the first version set
them there, on the reasoning that a property is not an address and so cannot go stale with a game
update. What that missed is *when*: the game poses the vehicle skeleton from the wheel physics
**every frame, after scripts have run**, so the write was always correct and always thrown away
before anything was drawn. There is no ordering that fixes it, because the numbers the poser uses do
not live in the bone.

**They live in the wheel, and the offsets are not a guess.** Each `CWheel` carries its Y rotation at
`0x008`, the negation of it at `0x010`, and its X offset at `0x030`. Those are not measured here:
they are the constants [FiveM's own implementations](https://github.com/citizenfx/fivem/blob/master/code/components/extra-natives-five/src/VehicleExtraNatives.cpp)
of `SET_VEHICLE_WHEEL_Y_ROTATION` and `SET_VEHICLE_WHEEL_X_OFFSET` use — **hardcoded** in that file
rather than pattern-scanned, which is the interesting part: everything in it that *has* moved
between game builds is scanned for, and these are not. Same numbers on Legacy and Enhanced, and
exercised by every FiveM server running a stance script.

**SHVDN finds the wheel**, which is the half that does move. `VehicleWheel.MemoryAddress` is a
maintained API — the walk from a vehicle to its wheel array is somebody else's problem and theirs to
keep right. Nothing here scans for anything.

**And it looks before it writes.** Every wheel's own values are read once when you get in, and a
reading that is not a finite number in a sane range means the address is not what this thinks it is
— so that wheel is skipped and said out loud rather than written to. What gets written is that stock
value *plus* the setting, so `0` is genuinely the car as it came and a car with camber from the
factory keeps it. Everything is handed back by handle when you get out or change car.

**And the wheels have a size.** The tyre, the rim inside it and how wide it is — the three the game
keeps per wheel, at `0x110`, `0x114` and `0x118`, from the same file and the same provenance as the
three already proven. Bigger tyres lift the car and fill the arch; a bigger rim inside the same tyre
is a lower profile. These are the **colliders** — the wheel the physics uses, which the visual
follows because the car is stood on it. Scaling the wheel *model* on top of that lives on the
vehicle rather than the wheel and is found by scanning the executable for a code pattern, which this
mod does not do.

They are **multiplied** rather than added, which is the one place this feature changes its mind about
how a number works: five centimetres of camber means the same thing on a Panto and on a Barracks, and
five centimetres of tyre does not. That makes stock `1` rather than `0`, so the store had to learn
the difference — a car stanced before these existed carries six decorators, and reading the seventh
as the nought the game hands back for an absent one would shrink its wheels to nothing.

**Nothing overlaps, because the row is measured rather than assumed.** A column is only a column
while everything fits in it: the value column is wide enough for `0.35 s` and not for `-20.0 deg`,
and the chip is wide enough for `ON` and not for a key called `OEM_PERIOD`. So labels ran under
sliders and values ran over them. The slider now sits where the value leaves it, and the label is cut
to what is actually left — one text measurement per row, right for every string there will ever be.

**The sliders fill from stock, not from the left end.** Camber runs from twenty degrees one way to
twenty the other, and a bar filled from the end showed the most negative camber you can have as an
*empty* bar — which reads as "minimum", or as nothing set, and is the opposite of what it is. The
bar is now the amount you have **changed** and the side it is on is which way, with a mark at stock
and a handle at the value. On the selected row the rail, fill and handle all invert, because an
amber bar on an amber row is not a bar.

**Camber, track and height get a scale each, the width of the panel.** They are set by eye, not by
number — you look at the car, not at the decimal — and a forty-pixel track squeezed between a label
and a value cannot show where twelve degrees sits in a range of forty. Those six rows are two rows
tall: the name and the number share the top line, and the scale has the bottom one to itself, with
ends you can see, a mark at stock, and a handle far enough from both to be pointed at.

Everything else about them is an ordinary number row — same factory, same nudging, same `LEFT` and
`RIGHT`. It is a way of *drawing* a value, not a way of editing one, and a row that looked different
and behaved differently would be two things to learn. The machinery for a row taller than a row was
already there for the gear chart. The three wheel sizes stay one row each under a heading of their
own, because those are set by number: `1.15` means what it says.

**Per axle rather than per wheel** — which is both what stance actually is and what VStancer's own
menu offers. The two sides are mirrored, because one slider has to become two opposite numbers or a
wider track is one wheel out and one wheel in. Odd bone ids are the left of each axle.

**Positive is wider.** The field itself wants a *negative* number on the left for that, because the
wheel models are rotated to face outwards — VStancer's readme tells you to type a minus sign for a
wider track. This takes the sign out of the setting.

**Height is the one that is not memory.** It goes through the hydraulic suspension raise, per wheel,
which SHVDN exposes properly — so it is the safest of the three and the least certain, because a car
with no hydraulics in its handling may simply ignore it. The log says what was asked for either way.

**The game puts the wheels back, which is why this holds on to cars it is not driving.** Writing the
stance once is not enough, and neither is writing it only while you are sat in the car — something in
the game restores those fields, so a car straightened up the moment you walked away from it. So a
stance is not an event, it is a **lease**: every car this has stanced is written again every frame
for as long as it exists, in it or not, parked or driven. That is the same thing VStancer does by
patching the game's reset code, done the way a script is allowed to do it.

Once a second it also looks at what is nearby, and any car carrying a stance in its own decorators is
adopted and held like the rest — which is what makes one survive a save, a reload of the mod, and
driving something else for an hour. A car put back to nought gets its stock values written one last
time and is then let go, so the lease ends when you end it.

**It stays on the car, and it is remembered.** Getting out used to put the wheels back, on the house
rule that an override is handed back by handle — right for power, torque and grip, which are how a
car *behaves* while you are in it, and wrong for a stance, which is what the car *looks like* and is
meant to outlive the drive. So nothing straightens up when you step out, and what you set is written
to `VehicleTweaks.stances.ini` beside the log and given back to that car when you get in again —
through a save, through closing the game, through anything.

**On the car itself, as a decorator** — which is the only name an individual car has. A handle is
invented when the vehicle is created and thrown away when it is not, but a decorator is a named
value the game carries *on the entity* and hands back later; it is what the game itself uses to mark
a car as somebody's personal vehicle, and what VStancer uses for the same job. So this Panto keeps
its own stance and the other one keeps its own.

**And in a file, by model, as the fallback** for a car that has never been given a stance of its
own — so a fresh one of a kind you have already built comes out looking right. A car carrying its
own stance ignores it. A car with neither keeps whatever is already on the sliders rather than
snapping to nought, or switching this on would look like the feature breaking.
`StanceRemember = false` turns all of it off and the six sliders go back to being ordinary
settings.

The file is written a moment after the numbers stop moving, not on every nudge — a slider held down
on a D-pad moves twenty times a second, and each of those would otherwise be a write.

The one thing that cannot be checked from outside the game is which sign counts as leaning *in*. The
log reports the front left wheel's camber and track before and after, once per car; if it leans the
wrong way, use the other sign.


## Holding a slide

**The engine keeps pulling while the car is sideways.**

**Power *and* torque**, on the same ramp. Torque is the half that actually holds a slide: power is
the top end, what the engine does once it is already spinning, and a car that has just been thrown
sideways is not there — the revs have dropped into the middle of the range and what it needs is
pull, not speed. Boosting power alone was asking the engine for help in the one place a drifting
car never is.

One ramp for both, unlike the steering lock below. These are not two questions — the game bogs a
sideways car and takes both away at once — so separate arrival angles would invent a distinction
the problem does not have. Each multiplies its TUNING baseline rather than replacing it: 1.20
torque set there and another 1.40 found while sideways is 1.68. GTA bogs a car down the moment it stops
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

## The lowrider pose

**He drives everything the way he drives a lowrider** — sat back, one arm through the window. Off
by default, because it changes how the character *looks* in every car rather than how any car
behaves.

**The first attempt was wrong, and the log is why we know.** It asked the game to change his seat
*context* — the mechanism the game itself uses to pick which animation a ped sits in — and read the
resulting clipset back before and after to see whether anything changed. It never did: clipsets
`2462687501` and `3332998045` across two cars, unchanged every single time, `MINI_LOWRIDER` asked
for and ignored. The documented context list turns out to be mission-specific entries like
`MISSFBI5_TREVOR_DRIVING`, with no lowrider among them.

That readout is the only reason this is a settled question rather than an argument. It was wrong in
the way that is hardest to see: **a call that succeeds and does nothing.**

**So the pose is played, not selected.** `TASK_PLAY_ANIM` with `UpperBodyOnly | Secondary` lays an
animation over his top half while the game keeps driving the rest of him — which is how every
custom driving pose in this game is actually done. The steering still works, because the steering
is not his arms, it is the car.

**And the names are worked out rather than guessed.** The clipset hashes from the failed first
attempt turned out to be the key. `GET_IN_VEHICLE_CLIPSET_HASH_FOR_SEAT` returns a joaat hash *of a
name*, and joaat is reversible by search rather than by mathematics — hash a few thousand candidate
names and see which lands on the number the game gave you. `3332998045`, logged from an ordinary
car, is **`clipset@veh@std@ds@base`**. So the naming is `clipset@veh@LAYOUT@SEAT@STATE`, and the
seat token is `ds` — not the `front_ds` the first probe had been built around.

So the probe **names things** now. It takes the seat clipset of whatever car you are in and finds
the name that hashes to it, which means sitting in a real lowrider makes the game tell you what a
real lowrider's seat animation is *called*. That is the entire question, answered by one drive
rather than by another list of guesses.

The dictionary probe runs alongside it: `DOES_ANIM_DICT_EXIST` answers for a dictionary and
`GET_ANIM_DURATION` for a clip inside one, so a wrong name is a log line rather than the silence
that let the first attempt go unnoticed.

**And the probe settled it.** 32 dictionaries exist on this build, among them
`veh@low@front_ds@base` with a `sit` clip running 6.33 seconds — so the original guess was right
all along, and my "correction" to `veh@low@ds@base` from the clipset hash was wrong. The two are
named differently on purpose: the seat *clipset* is `clipset@veh@low@ds@base`, and the animation
*dictionary* behind it is `veh@low@front_ds@base`. Reasoning across from one to the other was a
guess wearing the clothes of a deduction, and the probe is what caught it.

**Cars only.** The filter was removed back when the pose was a seat context, on the argument that a
vehicle without that clipset would simply ignore the request — true of a context, completely false
of a played animation, which plays on whatever you give it. A car-seat animation on a bicycle is a
man folded over the handlebars with one arm reaching into the road. Bikes have no window, no door
and nothing to lean on; on them it is not a pose, it is a fault.

**The window goes down with it** and he winds it up on his way out — the signal being
`IsSittingInVehicle` going false while `CurrentVehicle` still names the car, which is the climb-out.
Climbing in and climbing out look identical from outside; what separates them is which came first.

## Crashes, and the dash

Hit something above 100 km/h and the world drops into slow motion for a moment. **This is the
only thing here that reaches outside your own car** — time scale is global and persistent, so
it is put back when the moment ends, when you leave the car, when you switch it off, when
anything throws, and when the script shuts down. A crash is detected as *speed that vanishes*
rather than as a collision: GTA will happily report a kerb, and 8 m/s lost in one frame is about
50 g, which braking cannot do and a scrape cannot do.

**The cabin light is on `I`**, for interior, and on nothing at all on a pad. It used to follow
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
