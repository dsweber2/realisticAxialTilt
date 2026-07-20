"""
Solar panel output analysis: annual average, seasonal pattern, and battery
requirements vs latitude, vanilla vs several axial tilts.

Power output is proportional to CurSkyGlow, which under clear skies equals
CelestialSunGlowPercent — the dot product of the surface normal and sun
position vector, mapped through InverseLerp(0, 0.7, dot).
Weather applies equally to vanilla and modded, so clear-sky comparisons are
representative of the relative difference.
"""

import numpy as np
import matplotlib.pyplot as plt
import matplotlib.ticker as ticker

DAYS_PER_YEAR  = 60
N_HOUR_SAMPLES = 48
GLOW_SCALE     = 0.7    # InverseLerp upper bound in CelestialSunGlowPercent
PANEL_MAX_W    = 1700   # basePowerConsumption = -1700 in SolarGenerator def
BATTERY_WD         = 600   # storedEnergyMax = 600 in Battery def
BATTERY_EFFICIENCY = 0.5   # charging efficiency
BATTERY_DRAIN_W    = 5.0   # W self-discharge per battery, constant

TILTS       = [0, 15, 23.45, 40, 60, 90]
TILT_COLORS = plt.cm.plasma(np.linspace(0.05, 0.92, len(TILTS)))


def sun_glow_modded(lat_rad, day, hour_frac, tilt_rad):
    sin_decl   = np.sin(tilt_rad) * np.sin(day / DAYS_PER_YEAR * 2 * np.pi)
    cos_decl   = np.sqrt(np.clip(1.0 - sin_decl**2, 0, None))
    hour_angle = (hour_frac - 0.5) * 2 * np.pi
    sin_elev   = np.sin(lat_rad) * sin_decl + np.cos(lat_rad) * cos_decl * np.cos(hour_angle)
    return np.clip(sin_elev / GLOW_SCALE, 0, 1)


def sun_glow_vanilla(lat_rad, day, hour_frac):
    vanilla_sin_decl = -0.2 * np.cos(day / DAYS_PER_YEAR * 2 * np.pi)
    hour_angle       = (hour_frac - 0.5) * 2 * np.pi
    sin_elev = np.cos(lat_rad) * np.cos(hour_angle) + np.sin(lat_rad) * vanilla_sin_decl
    return np.clip(sin_elev / GLOW_SCALE, 0, 1)


def annual_sustainable_load(lat_deg, tilt_deg, vanilla=False):
    """Maximum sustainable constant load (W) per panel over the full year,
    accounting for 50% charging efficiency and 5W/battery self-discharge."""
    lat_rad    = np.radians(lat_deg)
    tilt_rad   = np.radians(tilt_deg)
    hour_fracs = np.linspace(0, 1, N_HOUR_SAMPLES, endpoint=False)
    production = np.concatenate([
        sun_glow_vanilla(lat_rad, day, hour_fracs) if vanilla
        else sun_glow_modded(lat_rad, day, hour_fracs, tilt_rad)
        for day in range(DAYS_PER_YEAR)
    ]) * PANEL_MAX_W
    L, _ = _sustainable_load_and_batteries(production, 1.0 / N_HOUR_SAMPLES)
    return L


def seasonal_daily_output(lat_deg, tilt_deg, vanilla=False):
    """Wd/day for each day of the year, per panel."""
    lat_rad    = np.radians(lat_deg)
    tilt_rad   = np.radians(tilt_deg)
    hour_fracs = np.linspace(0, 1, N_HOUR_SAMPLES, endpoint=False)
    result = np.empty(DAYS_PER_YEAR)
    for day in range(DAYS_PER_YEAR):
        if vanilla:
            result[day] = np.mean(sun_glow_vanilla(lat_rad, day, hour_fracs))
        else:
            result[day] = np.mean(sun_glow_modded(lat_rad, day, hour_fracs, tilt_rad))
    return result * PANEL_MAX_W


def _sustainable_load_and_batteries(production_W, sample_duration_days):
    """
    Find the maximum sustainable constant load and battery count per panel,
    accounting for 50% charging efficiency and 5W/battery self-discharge.

    production_W: array of instantaneous panel output (W) at uniform intervals
    sample_duration_days: duration each sample represents (days)

    Returns (sustainable_load_W, batteries_per_panel).
    Self-discharge creates a circular dependency (N → drain → effective production → N),
    resolved by an inner fixed-point iteration inside a bisection on L.
    """
    dt = sample_duration_days

    def compute_N(L):
        N = 0.0
        for _ in range(40):
            drain = BATTERY_DRAIN_W * N
            net = (np.maximum(production_W - L, 0) * BATTERY_EFFICIENCY
                   - np.maximum(L - production_W, 0)
                   - drain)
            soc = np.cumsum(net * dt)
            N_new = (np.max(soc) - np.min(soc)) / BATTERY_WD
            if abs(N_new - N) < 1e-5:
                break
            N = N_new
        return N_new, float(np.sum(net * dt))

    if compute_N(0.0)[1] <= 0.0:
        return 0.0, 0.0

    L_lo, L_hi = 0.0, float(np.max(production_W))
    for _ in range(60):
        L_mid = (L_lo + L_hi) * 0.5
        balance = compute_N(L_mid)[1]
        if balance > 0:
            L_lo = L_mid
        else:
            L_hi = L_mid
        if L_hi - L_lo < 0.01:
            break

    L = (L_lo + L_hi) * 0.5
    N, _ = compute_N(L)
    return L, N


def worst_day_batteries(lat_deg, tilt_deg, vanilla=False):
    """Batteries per panel for the worst single day's day/night cycle."""
    lat_rad    = np.radians(lat_deg)
    tilt_rad   = np.radians(tilt_deg)
    hour_fracs = np.linspace(0, 1, N_HOUR_SAMPLES, endpoint=False)
    worst_N = 0.0
    for day in range(DAYS_PER_YEAR):
        if vanilla:
            glows = sun_glow_vanilla(lat_rad, day, hour_fracs)
        else:
            glows = sun_glow_modded(lat_rad, day, hour_fracs, tilt_rad)
        _, N = _sustainable_load_and_batteries(glows * PANEL_MAX_W, 1.0 / N_HOUR_SAMPLES)
        worst_N = max(worst_N, N)
    return worst_N


def seasonal_batteries(lat_deg, tilt_deg, vanilla=False):
    """Batteries per panel to sustain maximum year-round load, seasonal buffering only."""
    daily_W = seasonal_daily_output(lat_deg, tilt_deg, vanilla)
    _, N = _sustainable_load_and_batteries(daily_W, 1.0)
    return N


lats_plot = np.arange(0, 85.1, 0.5)
days_plot = np.arange(DAYS_PER_YEAR)

print("Computing annual sustainable load by latitude...")
vanilla_annual = np.array([annual_sustainable_load(lat, 23.45, vanilla=True) for lat in lats_plot])
modded_annual  = {tilt: np.array([annual_sustainable_load(lat, tilt) for lat in lats_plot])
                  for tilt in TILTS}

print("Computing battery requirements by latitude...")
vanilla_daily_batt    = np.array([worst_day_batteries(lat, 23.45, vanilla=True) for lat in lats_plot])
vanilla_seasonal_batt = np.array([seasonal_batteries(lat, 23.45, vanilla=True) for lat in lats_plot])
modded_daily_batt     = {tilt: np.array([worst_day_batteries(lat, tilt) for lat in lats_plot])
                         for tilt in TILTS}
modded_seasonal_batt  = {tilt: np.array([seasonal_batteries(lat, tilt) for lat in lats_plot])
                         for tilt in TILTS}

print("Computing seasonal patterns...")
SEASONAL_LATS = [0, 30, 60, 75]
seasonal = {}
for lat in SEASONAL_LATS:
    seasonal[(lat, "vanilla")] = seasonal_daily_output(lat, 23.45, vanilla=True)
    for tilt in TILTS:
        seasonal[(lat, tilt)] = seasonal_daily_output(lat, tilt)

xtick_labels = ["Spring\nequinox", "Summer\nsolstice", "Autumn\nequinox", "Winter\nsolstice"]
xtick_days   = [0, 15, 30, 45]
lat_pairs    = [(SEASONAL_LATS[0], SEASONAL_LATS[1]), (SEASONAL_LATS[2], SEASONAL_LATS[3])]

# --- Figure 1: sustainable load ---
fig, axes = plt.subplots(1, 2, figsize=(14, 5))
fig.suptitle("Solar panel sustainable load — vanilla vs modded axial tilt (clear sky)", fontsize=13)

ax = axes[0]
ax.set_title("Annual sustainable load per panel by latitude")
ax.plot(lats_plot, vanilla_annual, "k--", lw=2, label="Vanilla (23.45°, old sun)", zorder=5)
for tilt, color in zip(TILTS, TILT_COLORS):
    label = f"Tilt {tilt:.0f}°" + (" (Earth)" if tilt == 23.45 else "")
    ax.plot(lats_plot, modded_annual[tilt], color=color, lw=1.8, label=label)
ax.set_xlabel("Latitude (°N)")
ax.set_ylabel("Sustainable constant load per panel (W)")
ax.xaxis.set_major_locator(ticker.MultipleLocator(10))
ax.legend(fontsize=8)

ax = axes[1]
ax.set_title("Sustainable load ratio to vanilla")
for tilt, color in zip(TILTS, TILT_COLORS):
    ratio = np.where(vanilla_annual > 1e-6, modded_annual[tilt] / vanilla_annual, np.nan)
    label = f"Tilt {tilt:.0f}°" + (" (Earth)" if tilt == 23.45 else "")
    ax.plot(lats_plot, ratio, color=color, lw=1.8, label=label)
ax.axhline(1.0, color="k", lw=1, ls="--", alpha=0.5, label="Vanilla baseline")
ax.set_xlabel("Latitude (°N)")
ax.set_ylabel("Modded sustainable load / vanilla sustainable load")
ax.xaxis.set_major_locator(ticker.MultipleLocator(10))
ax.legend(fontsize=8)

plt.tight_layout()
plt.savefig("solar_output.png", dpi=300)
print("saved solar_output.png")

# --- Figure 2: seasonal daily output ---
fig, axes = plt.subplots(1, 2, figsize=(14, 5))
fig.suptitle("Seasonal solar output — vanilla vs modded axial tilt (clear sky)", fontsize=13)

for ax, (lat_a, lat_b) in zip(axes, lat_pairs):
    for lat, ls in [(lat_a, "-"), (lat_b, "--")]:
        ax.plot(days_plot, seasonal[(lat, "vanilla")], color="k", lw=1.5, ls=ls,
                label=f"Vanilla {lat}°N", zorder=5)
        for tilt, color in zip(TILTS, TILT_COLORS):
            label = f"Tilt {tilt:.0f}°, {lat}°N" + (" (Earth)" if tilt == 23.45 else "")
            ax.plot(days_plot, seasonal[(lat, tilt)], color=color, lw=1.2, ls=ls,
                    alpha=0.85, label=label)
    ax.set_xticks(xtick_days)
    ax.set_xticklabels(xtick_labels, fontsize=8)
    ax.set_xlabel("Day of year")
    ax.set_ylabel("Daily energy per panel (Wd/day)")
    ax.set_title(f"Seasonal output — {lat_a}°N (solid) & {lat_b}°N (dashed)")
    ax.legend(fontsize=7, ncol=2)

plt.tight_layout()
plt.savefig("solar_seasonal.png", dpi=300)
print("saved solar_seasonal.png")

# --- Figure 3: batteries ---
fig, axes = plt.subplots(1, 2, figsize=(14, 5))
fig.suptitle("Batteries per panel — vanilla vs modded axial tilt", fontsize=13)

ax = axes[0]
ax.set_title("Worst-case day/night cycle")
ax.plot(lats_plot, vanilla_daily_batt, "k--", lw=2, label="Vanilla (23.45°, old sun)", zorder=5)
for tilt, color in zip(TILTS, TILT_COLORS):
    label = f"Tilt {tilt:.0f}°" + (" (Earth)" if tilt == 23.45 else "")
    ax.plot(lats_plot, modded_daily_batt[tilt], color=color, lw=1.8, label=label)
ax.set_xlabel("Latitude (°N)")
ax.set_ylabel("Batteries per panel")
ax.xaxis.set_major_locator(ticker.MultipleLocator(10))
ax.legend(fontsize=8)

ax = axes[1]
ax.set_title("Seasonal buffering to sustain year-round load")
ax.plot(lats_plot, vanilla_seasonal_batt, "k--", lw=2, label="Vanilla (23.45°, old sun)", zorder=5)
for tilt, color in zip(TILTS, TILT_COLORS):
    label = f"Tilt {tilt:.0f}°" + (" (Earth)" if tilt == 23.45 else "")
    ax.plot(lats_plot, modded_seasonal_batt[tilt], color=color, lw=1.8, label=label)
ax.set_xlabel("Latitude (°N)")
ax.set_ylabel("Batteries per panel")
ax.xaxis.set_major_locator(ticker.MultipleLocator(10))
ax.legend(fontsize=8)

plt.tight_layout()
plt.savefig("solar_batteries.png", dpi=300)
print("saved solar_batteries.png")
