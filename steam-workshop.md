# Realistic Axial Tilt — Steam Workshop description

---

## What is axial tilt?

Every planet spins on an axis — and most planets don't spin perfectly upright.
Earth is tilted about 23°, which is why we have seasons: summer when your hemisphere leans toward the sun, winter when it leans away.

In vanilla RimWorld, the planet always behaves as if it has an Earth-like tilt.
This mod lets you change that.

---

## What does this mod do?

It adds a single slider to the world generation screen: **Axial Tilt**, from 0° to 90°.

That one number changes a surprising amount.

### At 0° tilt — no seasons

Every day is the same length everywhere.
There are no summers or winters — just a constant, unrelenting temperature year-round.
Polar regions stay cold but stable.
Equatorial regions are warm all year.
Solar panels produce the same amount every day.

### At 23° — Earth-like (the default, identical to vanilla)

The game's normal behaviour.
Moderate seasonal swings, longer summer days at high latitudes, standard biomes.

### At 45°–70° — intense seasons

High latitudes get long, bright summers and dark winters.
Seasonal temperature swings are dramatically larger.
The poles warm up significantly on annual average — the planet's biome distribution shifts.
Deserts can appear at mid-latitudes; tundra pushes further from the poles.

### At 90° — extreme

The poles spend half the year in total darkness and half in continuous sunlight.
Seasonal temperature swings at high latitudes become enormous.
Annual averages flatten out globally — the poles are no longer the coldest places.
Ice caps disappear; arid and desert biomes extend much further toward the poles.

---

## What exactly changes in gameplay?

- **Day length** varies by latitude and season. High-latitude colonies get very long summer days (great for solar) and very short winter days.
- **Seasonal temperature** swings scale with tilt. A 90° world at 60° latitude swings from scorching summers to brutal winters.
- **Biome placement** is reshaped by the shift in annual average temperatures. The world map will look different.
- **Plant growth** adapts to extended daylight with the optional "Realistic plant rest" setting (see below).
- **Solar panel output** and **shadow direction** use the corrected sun position.
- **Everything else** that reads ambient light or temperature — plant growth speed, mood from darkness, attack aiming — all downstream effects are included automatically.

---

## The two graphs

**Annual average temperatures by latitude:**
![steam_averages](analysis/steam_averages.png)

At 0° tilt, high latitudes get slightly colder than vanilla (no summer to warm them up over the year).
At 90°, everything flattens — the poles warm dramatically and the equator barely changes.

**Seasonal swings at 60° north:**
![steam_seasonal](analysis/steam_seasonal.png)

A 0° world is a flat line — no seasons.
At 90°, a 60°N colony goes from a scorching 88°C summer to a −30°C winter.
Plan accordingly.

---

## World gen settings

When creating a new world, you'll see two new sliders:

**Axial Tilt** — the tilt angle, 0° to 90°.
The slider snaps to 23.45° (Earth-like) so it's easy to find the vanilla equivalent.

**Axial Effect** — how strongly the tilt affects temperatures, from 0 to 1.
At 1.0, the physics is accurate.
At 0.0, the sun moves correctly but temperatures behave like vanilla regardless of tilt.
Useful if you want the visual effect of a crazy tilt without the gameplay consequences.

Below the sliders is a live preview table showing estimated winter low / summer high temperatures at the equator, mid-latitudes, and the pole for your current settings.

---

## Optional: Realistic plant rest

In mod settings (Options → Mod Settings → Realistic Axial Tilt), you can enable **Realistic plant rest**.

Vanilla plants stop growing for about 11 hours every night.
With this enabled, the rest window shrinks to 4 hours (10pm–2am), letting plants take advantage of the long daylight hours that a high-tilt world provides at extreme latitudes.

Recommended at tilts above 45° if you're playing near the poles.

---

## Compatibility

- Works with existing saves (changes only take effect on new world generation; the tilt is saved with the world).
- Should be compatible with most biome mods — those mods add biomes, this mod changes the temperatures that determine which biomes are selected.
- Not compatible with other mods that patch sun positioning or base temperature at latitude.

---

## Credits and references

Sun position and day length math:
Ward, W. R. (1974). *Climatic variations on Mars: I. Astronomical theory of insolation.* J. Geophys. Res. 79(22).

Annual insolation formula:
Nadeau, A. & McGehee, R. (2017). *A simple formula for a planet's mean annual insolation by latitude.* Icarus 291:46–50. [arXiv:1810.10081](https://arxiv.org/abs/1810.10081)

---

*Source code: [GitHub](https://github.com/dsweber2/realisticAxialTilt)*
