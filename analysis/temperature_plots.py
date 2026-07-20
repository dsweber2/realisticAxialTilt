import numpy as np
import matplotlib.pyplot as plt
import matplotlib.ticker as ticker

# --- Vanilla curve (SimpleCurve linear interpolation) ---
# SeasonalTempVariationCurve: (0, 3), (0.1, 4), (1, 28)
_CURVE_X = np.array([0.0, 0.1, 1.0])
_CURVE_Y = np.array([3.0, 4.0, 28.0])

def vanilla_curve(u):
    return np.interp(u, _CURVE_X, _CURVE_Y)

def vanilla_amplitude(lat_deg):
    u = np.abs(np.sin(np.radians(lat_deg)))
    return np.sign(lat_deg) * vanilla_curve(u)

# --- Insolation model ---

def daily_insolation(phi, sin_decl):
    cos_decl = np.sqrt(np.clip(1.0 - sin_decl**2, 1e-12, None))
    sin_phi = np.sin(phi)
    cos_phi = np.cos(phi)
    tan_phi = np.where(np.abs(cos_phi) > 1e-6, sin_phi / cos_phi, np.sign(sin_phi) * 1e6)
    tan_decl = np.where(cos_decl > 1e-6, sin_decl / cos_decl, np.sign(sin_decl) * 1e6)
    cos_h0 = -tan_phi * tan_decl

    midnight_sun = cos_h0 <= -1.0
    polar_night  = cos_h0 >= 1.0
    normal = ~midnight_sun & ~polar_night

    h0 = np.where(normal, np.arccos(np.clip(cos_h0, -1, 1)), 0.0)
    h = np.where(
        midnight_sun, sin_phi * sin_decl,
        np.where(polar_night, 0.0,
                 (1.0 / np.pi) * (h0 * sin_phi * sin_decl + cos_phi * cos_decl * np.sin(h0)))
    )
    return h

SIN_EARTH_TILT = np.sin(np.radians(23.45))

def delta_insolation(lat_deg, sin_tilt):
    phi = np.radians(lat_deg)
    return daily_insolation(phi, sin_tilt) - daily_insolation(phi, -sin_tilt)

def amplitude_scale(lat_deg, sin_tilt, k=1.0):
    num = delta_insolation(lat_deg, sin_tilt)
    den = delta_insolation(lat_deg, SIN_EARTH_TILT)
    ratio = np.where(den > 1e-6, num / den, sin_tilt / SIN_EARTH_TILT)
    return ratio ** k

def modded_amplitude(lat_deg, tilt_deg, k=1.0):
    sin_tilt = np.sin(np.radians(tilt_deg))
    return vanilla_amplitude(lat_deg) * amplitude_scale(lat_deg, sin_tilt, k)

# --- Plotting ---

TILTS = [0, 15, 23.45, 45, 70, 90]
KS = [0.25, 0.5, 0.75, 1.0]
TILT_COLORS = plt.cm.plasma(np.linspace(0.1, 0.9, len(TILTS)))
K_COLORS = plt.cm.viridis(np.linspace(0.1, 0.9, len(KS)))

lats = np.linspace(-90, 90, 500)

# --- Fig 1: amplitude by latitude, various tilts, various k values ---
fig, axes = plt.subplots(2, 3, figsize=(15, 9), sharey=True)
fig.suptitle("Seasonal amplitude by latitude — effect of dampening exponent k", fontsize=13)

for ax, tilt in zip(axes.flat, TILTS):
    ax.plot(lats, vanilla_amplitude(lats), "k--", lw=1, label="Vanilla", alpha=0.5)
    for kk, color in zip(KS, K_COLORS):
        ax.plot(lats, modded_amplitude(lats, tilt, kk), color=color, lw=1.5, label=f"k={kk}")
    ax.set_title(f"Tilt {tilt:.0f}°" if tilt != 23.45 else "Tilt 23.45° (Earth)")
    ax.axhline(0, color="k", lw=0.5)
    ax.set_xlabel("Latitude (°)")
    ax.set_ylabel("Amplitude (°C)")
    ax.xaxis.set_major_locator(ticker.MultipleLocator(30))
    ax.legend(fontsize=8)

plt.tight_layout()
plt.savefig("amplitude_by_latitude.png", dpi=150)
print("saved amplitude_by_latitude.png")

# --- Fig 2: seasonal offset at fixed lat, 90° tilt, various k ---
days = np.linspace(0, 60, 500)
WINTER_MID_PCT = 52.0 / 60.0

def seasonal_offset(day, amplitude):
    year_frac = day / 60.0
    return np.cos(2 * np.pi * (year_frac - WINTER_MID_PCT)) * (-amplitude)

FIXED_LAT = 65.0
EXTREME_TILT = 90.0

fig, axes = plt.subplots(1, 2, figsize=(13, 5))
fig.suptitle(f"Seasonal temperature offset at {FIXED_LAT}° N, tilt {EXTREME_TILT}°", fontsize=13)

ax = axes[0]
ax.set_title("k comparison")
vanilla_amp = float(vanilla_amplitude(FIXED_LAT))
ax.plot(days, seasonal_offset(days, vanilla_amp), "k--", lw=1, label="Vanilla (23.45°)", alpha=0.6)
for kk, color in zip(KS, K_COLORS):
    amp = float(modded_amplitude(FIXED_LAT, EXTREME_TILT, kk))
    ax.plot(days, seasonal_offset(days, amp), color=color, lw=1.5, label=f"k={kk}  (±{amp:.0f}°C)")
ax.set_xlabel("Day of year")
ax.set_ylabel("Temperature offset (°C)")
ax.axhline(0, color="k", lw=0.5)
ax.set_xlim(0, 60)
ax.legend(fontsize=9)
for day, name in [(0, "Spr eq"), (15, "Sum sol"), (30, "Aut eq"), (45, "Win sol")]:
    ax.axvline(day, color="gray", lw=0.7, ls=":")

ax = axes[1]
ax.set_title("Max amplitude vs tilt for each k")
tilt_range = np.linspace(0, 90, 200)
for kk, color in zip(KS, K_COLORS):
    amps = [float(modded_amplitude(FIXED_LAT, tt, kk)) for tt in tilt_range]
    ax.plot(tilt_range, amps, color=color, lw=1.5, label=f"k={kk}")
ax.axhline(vanilla_amp, color="k", ls="--", lw=1, label="Vanilla", alpha=0.6)
ax.set_xlabel("Axial tilt (°)")
ax.set_ylabel(f"Amplitude at {FIXED_LAT}° N (°C)")
ax.legend(fontsize=9)
ax.axvline(23.45, color="gray", lw=0.7, ls=":")

plt.tight_layout()
plt.savefig("seasonal_offset.png", dpi=150)
print("saved seasonal_offset.png")
