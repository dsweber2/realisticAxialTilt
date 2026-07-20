"""
Steam Workshop plots — accessible visualisation of axial tilt effects (k=1).

Two outputs:
  steam_averages.png   — Annual average temperature vs latitude for several tilts.
  steam_seasonal.png   — Full-year temperature at 60°N for several tilts.
"""

import numpy as np
import matplotlib.pyplot as plt
import matplotlib.ticker as ticker
import matplotlib

# ---------------------------------------------------------------------------
# Shared constants
# ---------------------------------------------------------------------------

EARTH_TILT = 23.45
COS_EARTH_TILT = np.cos(np.radians(EARTH_TILT))
SIN_EARTH_TILT = np.sin(np.radians(EARTH_TILT))
TEMPERATURE_INSOLATION_SCALE = 70.0

# Vanilla AvgTempByLatitudeCurve: (0,30),(0.1,29),(0.5,7),(1,−37)
_AVG_X = np.array([0.0, 0.1, 0.5, 1.0])
_AVG_Y = np.array([30.0, 29.0, 7.0, -37.0])

# Vanilla SeasonalTempVariationCurve: (0,3),(0.1,4),(1,28)
_AMP_X = np.array([0.0, 0.1, 1.0])
_AMP_Y = np.array([3.0, 4.0, 28.0])

# ---------------------------------------------------------------------------
# Core physics
# ---------------------------------------------------------------------------

def P2(y): return (3*y**2 - 1) * 0.5
def P4(y): y2 = y**2; return (35*y2**2 - 30*y2 + 3) / 8
def P6(y): y2 = y**2; return (231*y2**3 - 315*y2**2 + 105*y2 - 5) / 16

def sigma6(eta, cos_beta):
    return (1.0
            - (5/8)    * P2(cos_beta) * P2(eta)
            - (9/64)   * P4(cos_beta) * P4(eta)
            - (65/1024) * P6(cos_beta) * P6(eta))

def daily_insolation(phi, sin_decl):
    cos_decl = np.sqrt(np.clip(1.0 - sin_decl**2, 1e-12, None))
    sin_phi, cos_phi = np.sin(phi), np.cos(phi)
    tan_phi  = np.where(np.abs(cos_phi) > 1e-6, sin_phi / cos_phi, np.sign(sin_phi) * 1e6)
    tan_decl = np.where(cos_decl > 1e-6, sin_decl / cos_decl, np.sign(sin_decl) * 1e6)
    cos_h0   = -tan_phi * tan_decl
    h0 = np.where((cos_h0 > -1) & (cos_h0 < 1),
                  np.arccos(np.clip(cos_h0, -1, 1)), 0.0)
    return np.where(cos_h0 <= -1, sin_phi * sin_decl,
           np.where(cos_h0 >= 1,  0.0,
                    (1/np.pi) * (h0 * sin_phi * sin_decl + cos_phi * cos_decl * np.sin(h0))))

def annual_avg(lat_deg, tilt_deg):
    eta      = np.sin(np.radians(lat_deg))
    cos_tilt = np.cos(np.radians(tilt_deg))
    earth_s  = sigma6(eta, COS_EARTH_TILT)
    ratio    = np.where(earth_s > 1e-6, sigma6(eta, cos_tilt) / earth_s, 1.0)
    base     = np.interp(np.abs(lat_deg) / 90.0, _AVG_X, _AVG_Y)
    return base + (ratio - 1.0) * earth_s * TEMPERATURE_INSOLATION_SCALE  # k=1

def seasonal_amplitude(lat_deg, tilt_deg):
    sin_tilt = np.sin(np.radians(tilt_deg))
    phi      = np.radians(lat_deg)
    num = daily_insolation(phi, sin_tilt) - daily_insolation(phi, -sin_tilt)
    den = daily_insolation(phi, SIN_EARTH_TILT) - daily_insolation(phi, -SIN_EARTH_TILT)
    scale    = np.where(den > 1e-6, num / den, sin_tilt / SIN_EARTH_TILT)  # k=1
    vanilla  = np.interp(np.abs(np.sin(np.radians(lat_deg))), _AMP_X, _AMP_Y)
    return vanilla * scale  # always ≥ 0 for northern hemisphere

# day_of_year: 0-based, 60 days/year; winter peak at day 52
def temperature_curve(days, lat_deg, tilt_deg):
    avg  = annual_avg(lat_deg, tilt_deg)
    amp  = seasonal_amplitude(lat_deg, tilt_deg)
    WINTER_PEAK = 52.0 / 60.0
    return avg + amp * np.cos(2 * np.pi * (days / 60.0 - WINTER_PEAK)) * -1

# ---------------------------------------------------------------------------
# Plot style
# ---------------------------------------------------------------------------

TILTS  = [0, 23.45, 45, 70, 90]
LABELS = ["0° — no seasons", "23° — Vanilla", "45°", "70°", "90° — extreme"]
COLORS = ["#4575b4", "#aaaaaa", "#fdae61", "#d73027", "#a50026"]
DASHES = ["-", "--", "-", "-", "-"]

plt.rcParams.update({
    "font.size": 12,
    "axes.titlesize": 14,
    "axes.labelsize": 12,
    "legend.fontsize": 11,
    "figure.facecolor": "#1a1a2e",
    "axes.facecolor": "#16213e",
    "axes.edgecolor": "#aaaaaa",
    "axes.labelcolor": "#dddddd",
    "xtick.color": "#cccccc",
    "ytick.color": "#cccccc",
    "text.color": "#eeeeee",
    "grid.color": "#333366",
    "grid.alpha": 0.6,
    "legend.facecolor": "#0f3460",
    "legend.edgecolor": "#555588",
})

# ---------------------------------------------------------------------------
# Plot 1: Annual average temperature vs latitude (0–90°N)
# ---------------------------------------------------------------------------

lats = np.linspace(0, 90, 500)

fig, ax = plt.subplots(figsize=(10, 6))
fig.suptitle("How axial tilt reshapes annual average temperatures",
             fontsize=15, weight="bold", y=0.97)

for tilt, label, color, ls in zip(TILTS, LABELS, COLORS, DASHES):
    ax.plot(lats, annual_avg(lats, tilt), color=color, lw=2.2, ls=ls, label=label)

ax.axhline(0, color="#888888", lw=0.8, ls=":")

ax.legend(title="Axial tilt", loc="lower left", bbox_to_anchor=(0.02, 0.12), framealpha=0.7)

ax.set_xlabel("Latitude")
ax.set_ylabel("Annual average temperature (°C)")
ax.set_xlim(0, 90)
ax.set_ylim(-60, 40)
ax.xaxis.set_major_locator(ticker.MultipleLocator(30))
ax.xaxis.set_minor_locator(ticker.MultipleLocator(10))
ax.xaxis.set_major_formatter(ticker.FuncFormatter(lambda x, _: f"{x:.0f}°N"))
ax.yaxis.set_major_locator(ticker.MultipleLocator(10))
ax.grid(True, which="major")
ax.tick_params(axis="x", which="minor", length=4, color="#aaaaaa")

for x, name in [(0, "Equator"), (23.5, "Tropics"), (45, "Mid-latitudes"), (66.5, "Arctic circle"), (90, "Pole")]:
    ax.axvline(x, color="#555577", lw=0.7, ls=":")
    ax.text(x + 0.5, -57, name, fontsize=8, color="#aaaacc", rotation=45, ha="left")

plt.tight_layout()
plt.savefig("steam_averages.png", dpi=150, bbox_inches="tight")
print("saved steam_averages.png")
plt.close()

# ---------------------------------------------------------------------------
# Plot 2: Full-year temperature at 60°N
# ---------------------------------------------------------------------------

days = np.linspace(0, 60, 500)
LAT  = 60.0

fig, ax = plt.subplots(figsize=(10, 6))
fig.suptitle(f"Seasonal temperatures at {LAT:.0f}°N for different axial tilts",
             fontsize=15, weight="bold", y=0.97)

for tilt, label, color, ls in zip(TILTS, LABELS, COLORS, DASHES):
    temp = temperature_curve(days, LAT, tilt)
    ax.plot(days, temp, color=color, lw=2.2, ls=ls, label=label)

ax.axhline(0, color="#888888", lw=0.8, ls=":")
ax.text(60.3, 0, "0°C (freezing)", fontsize=8, color="#888888", va="center")

for day in [0, 15, 30, 45]:
    ax.axvline(day, color="#555577", lw=0.8, ls=":")

ax.set_xlabel("Season")
ax.set_ylabel("Temperature (°C)")
ax.set_xlim(0, 60)
ax.xaxis.set_major_locator(ticker.FixedLocator([0, 15, 30, 45, 60]))
ax.xaxis.set_major_formatter(ticker.FixedFormatter(
    ["Spring\nequinox", "Summer\nsolstice", "Autumn\nequinox", "Winter\nsolstice", ""]))
ax.yaxis.set_major_locator(ticker.MultipleLocator(20))
ax.grid(True, which="major")
ax.legend(title="Axial tilt", framealpha=0.7)

plt.tight_layout()
plt.savefig("steam_seasonal.png", dpi=150, bbox_inches="tight")
print("saved steam_seasonal.png")
plt.close()
