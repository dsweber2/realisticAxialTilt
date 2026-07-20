import numpy as np
import matplotlib.pyplot as plt
import matplotlib.ticker as ticker

# --- Vanilla curve (SimpleCurve linear interpolation) ---
_CURVE_X = np.array([0.0, 0.1, 0.5, 1.0])
_CURVE_Y = np.array([30.0, 29.0, 7.0, -37.0])

def vanilla_base_temp(lat_deg):
    x = np.abs(lat_deg) / 90.0
    return np.interp(x, _CURVE_X, _CURVE_Y)

# --- Legendre polynomials ---

def P2(y):
    return (3.0 * y**2 - 1.0) * 0.5

def P4(y):
    y2 = y**2
    return (35.0 * y2**2 - 30.0 * y2 + 3.0) / 8.0

def P6(y):
    y2 = y**2
    return (231.0 * y2**3 - 315.0 * y2**2 + 105.0 * y2 - 5.0) / 16.0

# σ₆(η, β) — Nadeau & McGehee (2017), Icarus 291:46-50
# η = sin(lat), β = obliquity; A₂=5/8, A₄=9/64, A₆=65/1024
def sigma6(eta, cos_beta):
    return (1.0
            - (5.0/8.0)   * P2(cos_beta) * P2(eta)
            - (9.0/64.0)  * P4(cos_beta) * P4(eta)
            - (65.0/1024.0) * P6(cos_beta) * P6(eta))

COS_EARTH_TILT = np.cos(np.radians(23.45))
TEMPERATURE_INSOLATION_SCALE = 70.0

def annual_temp_correction(lat_deg, tilt_deg):
    eta = np.sin(np.radians(lat_deg))
    cos_tilt = np.cos(np.radians(tilt_deg))
    dS = (-(5.0/8.0)    * (P2(cos_tilt) - P2(COS_EARTH_TILT)) * P2(eta)
          - (9.0/64.0)  * (P4(cos_tilt) - P4(COS_EARTH_TILT)) * P4(eta)
          - (65.0/1024.0) * (P6(cos_tilt) - P6(COS_EARTH_TILT)) * P6(eta))
    return dS * TEMPERATURE_INSOLATION_SCALE

def modded_base_temp(lat_deg, tilt_deg):
    return vanilla_base_temp(lat_deg) + annual_temp_correction(lat_deg, tilt_deg)

# --- Setup ---

TILTS = [0, 15, 23.45, 45, 70, 90]
TILT_COLORS = plt.cm.plasma(np.linspace(0.1, 0.9, len(TILTS)))
lats = np.linspace(-90, 90, 500)

# --- Fig 1: temperature correction by latitude for each tilt ---
fig, axes = plt.subplots(1, 2, figsize=(14, 5))
fig.suptitle("Annual average temperature — effect of axial tilt", fontsize=13)

ax = axes[0]
ax.set_title("Correction relative to Earth tilt (23.45°)")
for tilt, color in zip(TILTS, TILT_COLORS):
    label = f"{tilt:.0f}°" if tilt != 23.45 else "23.45° (Earth, Δ=0)"
    corr = annual_temp_correction(lats, tilt)
    ax.plot(lats, corr, color=color, lw=1.5, label=label)
ax.axhline(0, color="k", lw=0.5)
ax.set_xlabel("Latitude (°)")
ax.set_ylabel("Temperature correction (°C)")
ax.xaxis.set_major_locator(ticker.MultipleLocator(30))
ax.legend(fontsize=9)

ax = axes[1]
ax.set_title("Resulting annual average temperature")
ax.plot(lats, vanilla_base_temp(lats), "k--", lw=1, label="Vanilla (23.45°)", alpha=0.5)
for tilt, color in zip(TILTS, TILT_COLORS):
    if tilt == 23.45:
        continue
    ax.plot(lats, modded_base_temp(lats, tilt), color=color, lw=1.5, label=f"{tilt:.0f}°")
ax.axhline(0, color="k", lw=0.5, ls=":")
ax.set_xlabel("Latitude (°)")
ax.set_ylabel("Annual average temperature (°C)")
ax.xaxis.set_major_locator(ticker.MultipleLocator(30))
ax.yaxis.set_major_locator(ticker.MultipleLocator(10))
ax.legend(fontsize=9)

plt.tight_layout()
plt.savefig("annual_temperature.png", dpi=150)
print("saved annual_temperature.png")

# --- Fig 2b: all tilts on one graph ---
fig, ax = plt.subplots(figsize=(8, 5))
ax.set_title("Annual average temperature by axial tilt")
ax.plot(lats, vanilla_base_temp(lats), "k--", lw=1, label="Vanilla (23.45°)", alpha=0.5)
for tilt, color in zip(TILTS, TILT_COLORS):
    if tilt == 23.45:
        continue
    ax.plot(lats, modded_base_temp(lats, tilt), color=color, lw=1.5, label=f"{tilt:.0f}°")
ax.axhline(0, color="k", lw=0.5, ls=":")
ax.set_xlabel("Latitude (°)")
ax.set_ylabel("Annual average temperature (°C)")
ax.xaxis.set_major_locator(ticker.MultipleLocator(30))
ax.yaxis.set_major_locator(ticker.MultipleLocator(10))
ax.legend(fontsize=9)
plt.tight_layout()
plt.savefig("annual_temperature_by_tilt.png", dpi=150)
print("saved annual_temperature_by_tilt.png")

# --- Fig 2c: correction relative to vanilla ---
fig, ax = plt.subplots(figsize=(8, 5))
ax.set_title("Annual temperature correction relative to Vanilla (23.45°)")
for tilt, color in zip(TILTS, TILT_COLORS):
    if tilt == 23.45:
        continue
    ax.plot(lats, annual_temp_correction(lats, tilt), color=color, lw=1.5, label=f"{tilt:.0f}°")
ax.axhline(0, color="k", lw=0.5)
ax.set_xlabel("Latitude (°)")
ax.set_ylabel("Temperature correction (°C)")
ax.xaxis.set_major_locator(ticker.MultipleLocator(30))
ax.yaxis.set_major_locator(ticker.MultipleLocator(10))
ax.legend(fontsize=9)
plt.tight_layout()
plt.savefig("annual_temperature_correction.png", dpi=150)
print("saved annual_temperature_correction.png")

# --- Fig 2: σ₆ insolation distribution by latitude for each tilt ---
fig, ax = plt.subplots(figsize=(8, 5))
ax.set_title("Annual mean insolation σ₆(sin lat, obliquity) — Nadeau & McGehee 2017")
for tilt, color in zip(TILTS, TILT_COLORS):
    cos_tilt = np.cos(np.radians(tilt))
    eta = np.sin(np.radians(lats))
    label = f"{tilt:.0f}°" if tilt != 23.45 else "23.45° (Earth)"
    ax.plot(lats, sigma6(eta, cos_tilt), color=color, lw=1.5, label=label)
ax.set_xlabel("Latitude (°)")
ax.set_ylabel("Normalised annual mean insolation")
ax.xaxis.set_major_locator(ticker.MultipleLocator(30))
ax.legend(fontsize=9)
plt.tight_layout()
plt.savefig("annual_insolation.png", dpi=150)
print("saved annual_insolation.png")
