# Vision

Rain World's Outskirts ecosystem, living on the Windows desktop.

Every department works toward this one goal. Read this file before starting any task.

## The bar: 1:1 with the game

Creatures must look, move, sound and behave exactly as they do in Rain World. The only
intentional difference is the environment: the desktop replaces the room.

- **If it would look wrong side by side with the game, it is a bug.** That includes timing,
  physics, animation, AI decisions, colours and sounds.
- **Port the game's logic and constants; don't approximate them.** Use the local decompiled
  reference (see "Game reference" below) as the source of truth. Don't guess and don't
  "make it feel similar".
- **Sprites, palettes and sounds are the game's own,** loaded at runtime from the user's
  Rain World install. Placeholder shapes exist only as a fallback when no install is found,
  and for tests.
- **Spawning uses the game's own data.** Which creatures appear, how often and in what
  lineages comes from the Outskirts world files in the user's install. It is not a
  hand-written table.

## The desktop as a Rain World room

The desktop is rasterised into the game's 20 px tile grid. Desktop features map onto game
terrain like this:

| Desktop feature | Game equivalent |
|---|---|
| A desktop icon | A block of solid terrain |
| A row of icons | A ledge or floor |
| **Two icon columns side by side** | **A pipe (shortcut).** Creatures that use shortcuts in the game enter at one end, travel inside the pipe exactly as they do in the game, and come out at the other end. They do not climb the outside of the icons. |
| A single icon column | A solid pillar. Only creatures that can climb walls in the game climb it. |
| The taskbar | The floor |
| The screen edges | Room exits to the offscreen world. Creatures arrive and leave through them. |
| The top of the screen | The sky, where flying creatures enter and leave |

Every creature uses its own in-game movement rules on this terrain. No creature gets
desktop-specific abilities.

## Creature roster: the Outskirts

The full list of creatures that spawn in the Outskirts in Rain World v1.11.8, across every
campaign, with Downpour and The Watcher installed. It was taken from `world/su/world_su.txt`,
the `moreslugcats` and `watcher` modify files, the region's swarm rooms and its placed
objects. **Internal type** is the game's name for the creature, used in code and data.
Families share one implementation.

| Family | Creature | Internal type | Source |
|---|---|---|---|
| Lizards | Green Lizard | `GreenLizard` | Base |
| | Pink Lizard | `PinkLizard` | Base |
| | Blue Lizard | `BlueLizard` | Base |
| | Yellow Lizard | `YellowLizard` | Base |
| | White Lizard | `WhiteLizard` | Base |
| | Cyan Lizard | `CyanLizard` | Base |
| | Red Lizard | `RedLizard` | Base |
| | Caramel Lizard | `SpitLizard` | Downpour |
| | Strawberry Lizard | `ZoopLizard` | Downpour |
| | Salamander | `Salamander` | Downpour |
| | Eel Lizard | `EelLizard` | Downpour |
| Batflies | Batfly | `Fly` | Base (swarm rooms) |
| Squidcadas | Squidcada (white and dark) | `CicadaA`, `CicadaB` | Base |
| Centipedes | Infant Centipede | `SmallCentipede` | Base |
| | Centipede | `Centipede` | Base |
| | Red Centipede | `RedCentipede` | Downpour |
| | Centiwing | `Centiwing` | Downpour |
| Small bugs | Eggbug | `EggBug` | Base |
| | Dropwig | `DropBug` | Base |
| Spiders | Big Spider | `BigSpider` | Base |
| | Spitter Spider | `SpitterSpider` | Base |
| Noodleflies | Noodlefly | `BigNeedleWorm` | Base |
| | Infant Noodlefly | `SmallNeedleWorm` | Base |
| Scavengers | Scavenger | `Scavenger` | Base |
| Vultures | Vulture | `Vulture` | Base |
| | King Vulture | `KingVulture` | Base |
| | Miros Vulture | `MirosVulture` | Downpour |
| Plants | Pole Plant | `PoleMimic` | Base |
| | Monster Kelp | `TentaclePlant` | Base |
| Overseers | Overseer | `Overseer` | Base |
| Other | Hazer | `Hazer` | Placed in one Outskirts room; check before building it |

**Build order:** the lizards come first because one implementation covers 11 creatures and
exercises the physics, pathing, pipes and graphics. Batflies follow, since they're the
everyday food, and then the rest in the order of the table.

**Campaign spawn tables:** the game changes Outskirts spawns per campaign. The app offers a
setting to pick one campaign's table, or "All Outskirts creatures" (the default), which
merges every campaign so every creature above can appear.

## Rules that apply to everyone

- **Never commit Rain World content.** Its art, audio, data and code belong to
  Videocult/Akupara Games, and this repo is MIT. Game files are read at runtime from the
  user's install or a local cache, and the decompiled reference stays outside the repo. See
  the build-release department for the full policy.
- **Cite the game.** Any constant or behaviour ported from the game carries a comment naming
  the game class and member it comes from (e.g. `// LizardBreedParams.tongueAttackRange`).

## Game reference

Each department's `CONTEXT.md` lists the local paths of the operator's Rain World install
and of the decompiled reference. Those paths are machine-specific and are not stored in the repo.
