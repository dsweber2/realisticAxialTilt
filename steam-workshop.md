# Realistic Axial Tilt — Steam Workshop description
---
In vanilla, the planet always behaves as if it has an Earth-like tilt, fudging light outside of the poles to be at the same angle, and setting plants to always have a 12 hour rest/growth period.
This mod makes axial tilt have a realistic gameplay effects. If you're looking for purely visual effects for earth-like tilts specifically, see [Celestial Lighting](https://steamcommunity.com/sharedfiles/filedetails/?id=3771657885).
---
## What changes
- **Day length** varies by latitude and season. And anything affected by light level (mood, aim, surgery, work speed, etc).
- **Seasonal temperature** swings scale with tilt. A 90° world at 60° latitude swings from scorching summers to brutal winters.
- **Solstice/Equinox timing** in Vanilla, the peak seasonal daylight (Septober 1) is *after* the peak temperature (Jugust 7). This is silly. Solstice is now on Jugust/Decembary 1st, with the temperature peak 7 days later
- **Biome placement** is reshaped by the shift in annual average temperatures.
- **Plant growth** togglable, adapts to extended daylight (the plants still need 4 hours of rest though, just not the 12 of vanilla).
- **Solar panel output** uses the corrected sun position. Expect higher highs and lower lows.
- **Shadow direction** is latitude- and season-aware. Vanilla doesn't draw northern building shadows, which this adds.
---
### At 0° tilt — no seasons
![steam_averages](images/world_0.png)
Every day is the same length everywhere.
There are no summers or winters — just a constant, unrelenting temperature year-round.
### At 23° — Earth-like (the default, identical to vanilla)
![steam_averages](images/world_23.png)
Moderate seasonal swings, longer summer days at high latitudes, standard biomes. Plant growth is a bit faster but more concentrated in the summer, solar power is more swingy. Should play fairly similarly to base Rimworld. You'll notice the effects most below the arctic circle, the base game has it as a special case.
### At 45°–70° — intense seasons
![steam_averages](images/world_60.png)
Actual image is at a 60° tilt.
High latitudes get long, bright summers and dark winters.
Seasonal temperature swings are dramatically larger.
The poles warm up on average, with dramatic swings around that average.
### At 90° — extreme
![steam_averages](images/world_90.png)
The poles spend half the year in total darkness and half in continuous sunlight; if you don't have space gear it's going to be dangerous, possibly immediately lethal apart from the equinoxes. 80°C near the pole is totally possible at the solstice.
Annual averages flatten out globally; you typically get glaciers in the highest elevation equatorial mountains.
Ice caps disappear, replaced with deserts.
---
## Temperature graphs
**Annual average temperatures by latitude:**
![steam_averages](analysis/steam_averages.png)
The equator gets colder and poles get swingier; note you can adjust the world average too.

**Seasonal swings at 60° north:**
![steam_seasonal](analysis/steam_seasonal.png)
A 0° world is a flat line — no seasons.
At 90°, a 60°N colony goes from a scorching 88°C summer to a −30°C winter.
Plan accordingly.
I've made more plots if you're curious over on the [url=https://github.com/dsweber2/realisticAxialTilt]GitHub[/url]
---
## Settings
When creating a new world, you'll see two new sliders:
**Axial Tilt** — the tilt angle, 0° to 90°.
The slider snaps to 23.45° (Earth-like) so it's easy to find the vanilla equivalent.

**Axial Effect** — how strongly the tilt affects temperatures, from 0 to 1.
At 1.0, the physics is accurate.
At 0.0, the sun moves correctly but temperatures behave like vanilla regardless of tilt.
Useful if you want the visual effect of a crazy tilt without the gameplay consequences.
![steam_UI](images/steam_ui.png)
**Realistic Plant rest** — Vanilla plants stop growing for about 11 hours every night. With this enabled, the rest window shrinks to 4 hours (10pm–2am), letting plants take advantage of the long daylight hours that a high-tilt world provides at extreme latitudes. IRL plants need sleep too (it's more complicated than 4hrs, this is a compromise).
---
## Compatibility
- Works with existing saves, should be safe to remove (haven't tried that yet).
- Should be compatible with most biome mods — it changes selection, not the biomes selected.
- Patched for  [Celestial Lighting](https://steamcommunity.com/sharedfiles/filedetails/?id=3771657885) my current suggestion for a lighting-specifc extension of this mod.
- [Realistic Planets 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3244790542) — corrects RP2's precomputed climate arrays and tile temperatures, which otherwise ignore tilt.
- Patched for [Nice Plants Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=2877856030) to adjust growth calculations
- should work with dubs skylights, needs a bit more testing however (slight chance plants die in deep winter)

---
## Plans
This mod is specifically meant for just direct effects of the axial tilt. I may make some follow up mods or add adjustments directly related to solar light levels. Some ideas for related mods:
- Custom biome definitions tuned for non-Earth tilts (e.g. polar deserts, equatorial tundra), along with biome patching to add temperature *range* as part of biome map placement.
---
### Copyright
This is Creative Commons. Feel free to use it however you want, as long as you attribute me. I'm happy to adapt to integrate with other mods if it makes sense or help you do that.
---
### Code generation
I used Claude code in the process of making this package. Keep thinking.
---
*Source code: [GitHub](https://github.com/dsweber2/realisticAxialTilt)*
