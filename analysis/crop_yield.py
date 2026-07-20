"""
Crop yield simulation: tiles per colonist vs latitude, vanilla vs modded.

Models the full chain:
  sun glow → GrowthRateFactorFor_Light
  temperature → GrowthRateFactorFor_Temperature + GrowthSeasonNow
  rest period → GrowthPerTick gating
→ annual nutrition per tile → tiles needed per pawn
"""

import numpy as np
import matplotlib.pyplot as plt
import matplotlib.ticker as ticker
import matplotlib.transforms as mtransforms

# ============================================================
# Game constants — rice (most common crop)
# ============================================================
GROW_DAYS          = 6.0
HARVEST_YIELD      = 22
NUTRITION_PER_ITEM = 0.05
GROW_MIN_GLOW      = 0.51   # default growMinGlow for outdoor plants
GROW_OPTIMAL_GLOW  = 1.0
MIN_GROW_TEMP      = 0.0    # GrowthSeasonNow lower bound
MAX_GROW_TEMP      = 58.0   # GrowthSeasonNow upper bound
MIN_OPTIMAL_TEMP   = 6.0
MAX_OPTIMAL_TEMP   = 42.0

NUTRITION_PER_PAWN_PER_DAY = 1.6
DAYS_PER_YEAR              = 60

# Simulation resolution
N_HOUR_SAMPLES = 48   # per day
N_DAYS         = 60

# ============================================================
# Rest period definitions
# ============================================================
def is_resting_vanilla(hour_frac):
    return (hour_frac < 0.25) | (hour_frac > 0.80)

def is_resting_modded(hour_frac):
    return (hour_frac > 11/12) | (hour_frac < 1/12)

# ============================================================
# Sun glow models
# ============================================================
# CelestialSunGlowPercent computes: dot(SurfaceNormal(lat), SunPosition(lat,day,hour))
# then maps via InverseLerp(0, 0.7, dot).
#
# Our derivation from the C# source shows the dot product equals:
#   sin(lat)*sin(decl) + cos(lat)*cos(decl)*cos(hourAngle)
# where hourAngle = (dayPercent - 0.5) * 2π.
#
# Vanilla uses a smaller effective declination (~0.2 vs sin(23.45°)=0.40)
# and a cos-based (rather than sin-based) seasonal phase, matching its
# SunPositionUnmodified y-offset: -cos(day/60*2π) * 0.2.

GLOW_SCALE = 0.7   # InverseLerp upper bound in CelestialSunGlowPercent

def sun_glow_modded(lat_rad, day, hour_frac, tilt_rad):
    sin_decl = np.sin(tilt_rad) * np.sin(day / DAYS_PER_YEAR * 2 * np.pi)
    cos_decl = np.sqrt(np.clip(1.0 - sin_decl**2, 0, None))
    hour_angle = (hour_frac - 0.5) * 2 * np.pi
    sin_elev = np.sin(lat_rad) * sin_decl + np.cos(lat_rad) * cos_decl * np.cos(hour_angle)
    return np.clip(sin_elev / GLOW_SCALE, 0, 1)

def sun_glow_vanilla(lat_rad, day, hour_frac):
    # Effective sinDecl from SunPositionUnmodified with scale=0.2 (lat < 70)
    vanilla_sin_decl = -0.2 * np.cos(day / DAYS_PER_YEAR * 2 * np.pi)
    hour_angle = (hour_frac - 0.5) * 2 * np.pi
    sin_elev = np.cos(lat_rad) * np.cos(hour_angle) + np.sin(lat_rad) * vanilla_sin_decl
    return np.clip(sin_elev / GLOW_SCALE, 0, 1)

# ============================================================
# Growth rate factors (from decompiled PlantUtility.cs)
# GrowthRateFactorFor_Light:  InverseLerp(growMinGlow, growOptimalGlow, glow)
# GrowthRateFactorFor_Temperature: piecewise InverseLerp
# ============================================================
def light_factor(glow):
    return np.clip((glow - GROW_MIN_GLOW) / (GROW_OPTIMAL_GLOW - GROW_MIN_GLOW), 0, 1)

def temp_factor(temp):
    below_optimal  = np.clip((temp - MIN_GROW_TEMP) / (MIN_OPTIMAL_TEMP - MIN_GROW_TEMP), 0, 1)
    above_optimal  = np.clip((MAX_GROW_TEMP - temp) / (MAX_GROW_TEMP - MAX_OPTIMAL_TEMP), 0, 1)
    in_optimal     = np.ones_like(temp)
    factor = np.where(temp < MIN_OPTIMAL_TEMP, below_optimal,
             np.where(temp > MAX_OPTIMAL_TEMP, above_optimal, in_optimal))
    # GrowthSeasonNow: no growth outside [MIN_GROW_TEMP, MAX_GROW_TEMP]
    return np.where((temp <= MIN_GROW_TEMP) | (temp >= MAX_GROW_TEMP), 0.0, factor)

# ============================================================
# Temperature model
# ============================================================
_BASE_CURVE_X = np.array([0.0, 0.1, 0.5, 1.0])
_BASE_CURVE_Y = np.array([30.0, 29.0, 7.0, -37.0])

def vanilla_base_temp(lat_deg):
    x = np.abs(lat_deg) / 90.0
    return np.interp(x, _BASE_CURVE_X, _BASE_CURVE_Y)

_AMP_CURVE_X = np.array([0.0, 0.1, 1.0])
_AMP_CURVE_Y = np.array([3.0, 4.0, 28.0])

def vanilla_amplitude(lat_deg):
    u = np.abs(np.sin(np.radians(lat_deg)))
    return np.interp(u, _AMP_CURVE_X, _AMP_CURVE_Y)

def P2(y): return (3*y**2 - 1) * 0.5
def P4(y):
    y2 = y**2; return (35*y2**2 - 30*y2 + 3) / 8
def P6(y):
    y2 = y**2; return (231*y2**3 - 315*y2**2 + 105*y2 - 5) / 16
def sigma6(eta, cos_beta):
    return (1 - (5/8)*P2(cos_beta)*P2(eta)
              - (9/64)*P4(cos_beta)*P4(eta)
              - (65/1024)*P6(cos_beta)*P6(eta))

SIN_EARTH_TILT = np.sin(np.radians(23.45))
COS_EARTH_TILT = np.cos(np.radians(23.45))
TEMP_INSOL_SCALE = 70.0

def daily_insolation(phi, sin_decl):
    cos_decl = np.sqrt(np.clip(1 - sin_decl**2, 1e-12, None))
    sin_phi, cos_phi = np.sin(phi), np.cos(phi)
    tan_phi  = np.where(np.abs(cos_phi) > 1e-6, sin_phi/cos_phi, np.sign(sin_phi)*1e6)
    tan_decl = np.where(cos_decl > 1e-6, sin_decl/cos_decl, np.sign(sin_decl)*1e6)
    cos_h0 = -tan_phi * tan_decl
    h0 = np.where(cos_h0 <= -1, np.pi, np.where(cos_h0 >= 1, 0, np.arccos(np.clip(cos_h0, -1, 1))))
    return np.where(cos_h0 <= -1, sin_phi*sin_decl,
           np.where(cos_h0 >= 1, 0,
                    (1/np.pi) * (h0*sin_phi*sin_decl + cos_phi*cos_decl*np.sin(h0))))

def amplitude_scale(lat_deg, sin_tilt, k=1.0):
    phi = np.radians(lat_deg)
    num = daily_insolation(phi, sin_tilt) - daily_insolation(phi, -sin_tilt)
    den = daily_insolation(phi, SIN_EARTH_TILT) - daily_insolation(phi, -SIN_EARTH_TILT)
    ratio = np.where(den > 1e-6, num/den, sin_tilt/SIN_EARTH_TILT)
    return ratio**k

def annual_temp_correction(lat_deg, tilt_deg):
    eta = np.sin(np.radians(lat_deg))
    cos_tilt = np.cos(np.radians(tilt_deg))
    dS = (-(5/8)*(P2(cos_tilt) - P2(COS_EARTH_TILT))*P2(eta)
          - (9/64)*(P4(cos_tilt) - P4(COS_EARTH_TILT))*P4(eta)
          - (65/1024)*(P6(cos_tilt) - P6(COS_EARTH_TILT))*P6(eta))
    return dS * TEMP_INSOL_SCALE

WINTER_MID_DAY = 52.0

def seasonal_temp(day, base_temp, amplitude):
    return base_temp - amplitude * np.cos(2*np.pi * (day - WINTER_MID_DAY) / DAYS_PER_YEAR)

# ============================================================
# Core simulation: annual nutrition per tile
# ============================================================
def annual_nutrition_per_tile(lat_deg, tilt_deg, k=1.0, modded_rest=True, modded_sun=True):
    lat_rad  = np.radians(lat_deg)
    tilt_rad = np.radians(tilt_deg)

    base_temp = vanilla_base_temp(lat_deg)
    if modded_sun and tilt_deg != 23.45:
        base_temp += annual_temp_correction(lat_deg, tilt_deg)

    sin_tilt = np.sin(tilt_rad)
    amplitude = vanilla_amplitude(lat_deg)
    if modded_sun:
        amplitude *= amplitude_scale(lat_deg, sin_tilt, k)

    is_resting = is_resting_modded if modded_rest else is_resting_vanilla

    hour_fracs = np.linspace(0, 1, N_HOUR_SAMPLES, endpoint=False)
    resting    = is_resting(hour_fracs)

    total_growth = 0.0

    for day in range(N_DAYS):
        temp = seasonal_temp(day, base_temp, amplitude)
        tf   = temp_factor(np.array([temp]))[0]
        if tf == 0.0:
            continue

        if modded_sun:
            glows = sun_glow_modded(lat_rad, day, hour_fracs, tilt_rad)
        else:
            glows = sun_glow_vanilla(lat_rad, day, hour_fracs)

        lf = light_factor(glows)
        lf[resting] = 0.0

        daily_rate = tf * np.mean(lf)
        total_growth += daily_rate

    # total_growth is in units of (full-rate grow days equivalent).
    # Each harvest requires GROW_DAYS of full-rate growth.
    return total_growth, total_growth / GROW_DAYS * HARVEST_YIELD * NUTRITION_PER_ITEM

def tiles_per_pawn(lat_deg, tilt_deg, k=1.0, modded_rest=True, modded_sun=True, discrete=False):
    annual_food_needed = NUTRITION_PER_PAWN_PER_DAY * DAYS_PER_YEAR
    total_growth, nutrition_continuous = annual_nutrition_per_tile(lat_deg, tilt_deg, k, modded_rest, modded_sun)
    if discrete:
        nutrition = np.floor(total_growth / GROW_DAYS) * HARVEST_YIELD * NUTRITION_PER_ITEM
    else:
        nutrition = nutrition_continuous
    return annual_food_needed / nutrition if nutrition > 0 else np.inf

def growing_season_days(lat_deg, tilt_deg, k=1.0, modded_sun=True):
    """Number of days per year where temperature is in the growing range [0, 58°C]."""
    base_temp = vanilla_base_temp(lat_deg)
    if modded_sun and tilt_deg != 23.45:
        base_temp += annual_temp_correction(lat_deg, tilt_deg)
    sin_tilt  = np.sin(np.radians(tilt_deg))
    amplitude = vanilla_amplitude(lat_deg)
    if modded_sun:
        amplitude *= amplitude_scale(lat_deg, sin_tilt, k)
    days = np.arange(N_DAYS)
    temps = seasonal_temp(days, base_temp, amplitude)
    return int(np.sum((temps > MIN_GROW_TEMP) & (temps < MAX_GROW_TEMP)))

# ============================================================
# Plotting
# ============================================================
TILTS       = [0, 15, 23.45, 40, 60, 90]
TILT_COLORS = plt.cm.plasma(np.linspace(0.05, 0.92, len(TILTS)))
lats_plot   = np.arange(0, 80.1, 0.5)   # dense: for smooth plot curves
lats_table  = np.arange(0, 81, 5)        # coarse: for markdown table

print("Computing vanilla baseline...")
vanilla_tiles_plot  = np.array([tiles_per_pawn(lat, 23.45, modded_rest=False, modded_sun=False, discrete=True) for lat in lats_plot])
vanilla_season_plot = np.array([growing_season_days(lat, 23.45, modded_sun=False) for lat in lats_plot])
vanilla_tiles_table  = np.array([tiles_per_pawn(lat, 23.45, modded_rest=False, modded_sun=False, discrete=True) for lat in lats_table])
vanilla_season_table = np.array([growing_season_days(lat, 23.45, modded_sun=False) for lat in lats_table])

print("Computing modded scenarios...")
modded_tiles_plot  = {}
modded_season_plot = {}
modded_tiles_table  = {}
modded_season_table = {}
for tilt in TILTS:
    modded_tiles_plot[tilt]   = np.array([tiles_per_pawn(lat, tilt, discrete=True)    for lat in lats_plot])
    modded_season_plot[tilt]  = np.array([growing_season_days(lat, tilt)               for lat in lats_plot])
    modded_tiles_table[tilt]  = np.array([tiles_per_pawn(lat, tilt, discrete=True)    for lat in lats_table])
    modded_season_table[tilt] = np.array([growing_season_days(lat, tilt)               for lat in lats_table])

UNVIABLE = 200   # cap for display; above this is "don't bother"

def plot_safe(arr):
    """Replace inf/nan with UNVIABLE for plotting."""
    out = np.array(arr, dtype=float)
    out[~np.isfinite(out) | (out > UNVIABLE)] = UNVIABLE
    return np.minimum(out, UNVIABLE)

fig, axes = plt.subplots(2, 2, figsize=(14, 10))
fig.suptitle("Crop farming — vanilla vs modded axial tilt (rice, 100% fertility)", fontsize=13)

# --- [0,0] tiles/pawn, log scale ---
ax = axes[0, 0]
ax.set_title("Tiles per pawn (log scale)")
ax.plot(lats_plot, plot_safe(vanilla_tiles_plot), "k--", lw=2, label="Vanilla (23.45°, old rest)", zorder=5)
for tilt, color in zip(TILTS, TILT_COLORS):
    label = f"Tilt {tilt:.0f}°" + (" (Earth)" if tilt == 23.45 else "")
    ax.plot(lats_plot, plot_safe(modded_tiles_plot[tilt]), color=color, lw=1.8, label=label)
ax.axhline(UNVIABLE, color="gray", lw=0.8, ls=":", label=f"≥{UNVIABLE} = unviable")
ax.set_xlabel("Latitude (°N)")
ax.set_ylabel("Tiles per pawn")
ax.xaxis.set_major_locator(ticker.MultipleLocator(10))
ax.yaxis.set_major_formatter(ticker.ScalarFormatter())
ax.legend(fontsize=8, loc='upper right',
          bbox_to_anchor=(40, UNVIABLE - 25),
          bbox_transform=ax.transData)

# --- [0,1] ratio to vanilla ---
ax = axes[0, 1]
ax.set_title("Ratio to vanilla (log scale, <1 = easier)")
for tilt, color in zip(TILTS, TILT_COLORS):
    v = plot_safe(vanilla_tiles_plot)
    m = plot_safe(modded_tiles_plot[tilt])
    ratio = np.where(v > 0, m / v, np.nan)
    label = f"Tilt {tilt:.0f}°" + (" (Earth)" if tilt == 23.45 else "")
    ax.semilogy(lats_plot, ratio, color=color, lw=1.8, label=label)
ax.axhline(1.0, color="k", lw=1, ls="--", alpha=0.5, label="Vanilla baseline")
ax.set_xlabel("Latitude (°N)")
ax.set_ylabel("Tiles needed / vanilla tiles needed")
ax.xaxis.set_major_locator(ticker.MultipleLocator(10))
ax.yaxis.set_major_formatter(ticker.ScalarFormatter())
ax.legend(fontsize=8)

# --- [1,0] growing season days ---
ax = axes[1, 0]
ax.set_title("Growing season (days/year)")
ax.plot(lats_plot, vanilla_season_plot, "k--", lw=2, label="Vanilla (23.45°)", zorder=5)
for tilt, color in zip(TILTS, TILT_COLORS):
    label = f"Tilt {tilt:.0f}°" + (" (Earth)" if tilt == 23.45 else "")
    ax.plot(lats_plot, modded_season_plot[tilt], color=color, lw=1.8, label=label)
ax.set_xlabel("Latitude (°N)")
ax.set_ylabel("Days with temperature in growing range")
ax.xaxis.set_major_locator(ticker.MultipleLocator(10))
ax.yaxis.set_major_locator(ticker.MultipleLocator(10))
ax.legend(fontsize=8)

# --- [1,1] tiles/pawn weighted by season compression ---
# tiles_per_pawn / (season_days / 60): how hard is the harvest crunch?
ax = axes[1, 1]
ax.set_title("Harvest crunch (tiles/pawn ÷ season fraction, log scale)")
ax.set_ylabel("Effective tiles to harvest in season")
vanilla_crunch = plot_safe(vanilla_tiles_plot) / np.maximum(vanilla_season_plot / DAYS_PER_YEAR, 1/DAYS_PER_YEAR)
ax.semilogy(lats_plot, vanilla_crunch, "k--", lw=2, label="Vanilla (23.45°)", zorder=5)
for tilt, color in zip(TILTS, TILT_COLORS):
    crunch = plot_safe(modded_tiles_plot[tilt]) / np.maximum(modded_season_plot[tilt] / DAYS_PER_YEAR, 1/DAYS_PER_YEAR)
    label = f"Tilt {tilt:.0f}°" + (" (Earth)" if tilt == 23.45 else "")
    ax.semilogy(lats_plot, crunch, color=color, lw=1.8, label=label)
ax.set_xlabel("Latitude (°N)")
ax.xaxis.set_major_locator(ticker.MultipleLocator(10))
ax.yaxis.set_major_formatter(ticker.ScalarFormatter())
ax.legend(fontsize=8)

plt.tight_layout()
plt.savefig("crop_yield.png", dpi=300)
print("saved crop_yield.png")

# --- Markdown table artifact ---
def fmt_tiles(v):
    if v == np.inf or np.isnan(v): return "—"
    return f"{v:.0f}"

def fmt_days(d):
    return str(int(d))

def fmt_ratio(m, v):
    if v == np.inf or np.isnan(v) or m == np.inf or np.isnan(m): return ""
    return f" ({m/v:.2f}x)"

tilt_labels = {0: "0°", 15: "15°", 23.45: "23° (Earth)", 40: "40°", 60: "60°", 90: "90°"}

lines = []
lines.append("# Crop yield by latitude and axial tilt")
lines.append("")
lines.append("Rice at 100% fertility. **Tiles/pawn** = rice tiles needed to sustain one colonist per year.")
lines.append("**Season** = days per year where temperature permits growth (0–58 °C).")
lines.append("Ratio in parentheses is relative to the vanilla baseline.")
lines.append("")

# --- Table 1: tiles per pawn ---
lines.append("## Tiles per pawn (rice)")
lines.append("")
header = "| Lat | Vanilla |" + "".join(f" {tilt_labels[t]} |" for t in TILTS)
sep    = "| ---: | ---: |" + "".join(" ---: |" for _ in TILTS)
lines.append(header)
lines.append(sep)
for ii, lat in enumerate(lats_table):
    v = vanilla_tiles_table[ii]
    row = f"| {lat}°N | {fmt_tiles(v)} |"
    for tilt in TILTS:
        m = modded_tiles_table[tilt][ii]
        row += f" {fmt_tiles(m)}{fmt_ratio(m, v)} |"
    lines.append(row)

lines.append("")

# --- Table 2: growing season days ---
lines.append("## Growing season (days/year)")
lines.append("")
header = "| Lat | Vanilla |" + "".join(f" {tilt_labels[t]} |" for t in TILTS)
lines.append(header)
lines.append(sep)
for ii, lat in enumerate(lats_table):
    v = vanilla_season_table[ii]
    row = f"| {lat}°N | {fmt_days(v)} |"
    for tilt in TILTS:
        s = modded_season_table[tilt][ii]
        diff = int(s) - int(v)
        sign = "+" if diff >= 0 else ""
        row += f" {fmt_days(s)} ({sign}{diff}) |"
    lines.append(row)

lines.append("")
lines.append("---")
lines.append("")
lines.append("**Notes**")
lines.append("- Vanilla modelled with original sun glow formula and 45% daily rest window (00:00–06:00, 19:12–24:00).")
lines.append("- Modded scenarios use corrected solar geometry, annual temperature correction, and 17% rest window (22:00–02:00).")
lines.append("- `—` = temperature never reaches growing range; the location cannot farm.")
lines.append("- Tiles/pawn assumes continuous replanting, no soil fertility bonus, fertility=100%.")

md_path = "crop_yield_table.md"
with open(md_path, "w") as fh:
    fh.write("\n".join(lines) + "\n")
print(f"saved {md_path}")
