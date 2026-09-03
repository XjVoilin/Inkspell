#!/usr/bin/env python3
"""Generate Inkspell's original procedural music and sound-effect library."""

from __future__ import annotations

import json
import math
import wave
from pathlib import Path

import numpy as np


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/Game/Res/Audio"
MANIFEST = ROOT / "Design/Audio/audio-manifest.json"
SFX_RATE = 32_000
BGM_RATE = 24_000
RNG = np.random.default_rng(0x1A2B3C4D)


def envelope(length: int, attack: float = 0.02, release: float = 0.2, curve: float = 1.5) -> np.ndarray:
    env = np.ones(length, dtype=np.float64)
    attack_count = min(length, max(1, int(length * attack)))
    release_count = min(length, max(1, int(length * release)))
    env[:attack_count] = np.linspace(0.0, 1.0, attack_count, endpoint=False) ** curve
    env[-release_count:] = np.linspace(1.0, 0.0, release_count) ** curve
    return env


def chirp(duration: float, rate: int, start_hz: float, end_hz: float | None = None,
          amplitude: float = 1.0, attack: float = 0.01, release: float = 0.45,
          waveform: str = "sine") -> np.ndarray:
    count = max(1, int(duration * rate))
    t = np.arange(count, dtype=np.float64) / rate
    end_hz = start_hz if end_hz is None else end_hz
    phase = 2.0 * math.pi * (start_hz * t + (end_hz - start_hz) * t * t / (2.0 * duration))
    if waveform == "triangle":
        signal = 2.0 / math.pi * np.arcsin(np.sin(phase))
    elif waveform == "softsquare":
        signal = np.tanh(2.2 * np.sin(phase))
    else:
        signal = np.sin(phase)
    return amplitude * signal * envelope(count, attack, release)


def band_noise(duration: float, rate: int, low_hz: float, high_hz: float,
               amplitude: float = 1.0, attack: float = 0.02, release: float = 0.4,
               seed: int | None = None) -> np.ndarray:
    count = max(1, int(duration * rate))
    local = np.random.default_rng(seed) if seed is not None else RNG
    spectrum = np.fft.rfft(local.normal(0.0, 1.0, count))
    frequencies = np.fft.rfftfreq(count, 1.0 / rate)
    width = max(20.0, (high_hz - low_hz) * 0.12)
    low_gate = np.clip((frequencies - low_hz) / width, 0.0, 1.0)
    high_gate = np.clip((high_hz - frequencies) / width, 0.0, 1.0)
    spectrum *= low_gate * high_gate
    signal = np.fft.irfft(spectrum, count)
    peak = np.max(np.abs(signal)) or 1.0
    return amplitude * signal / peak * envelope(count, attack, release)


def bell(duration: float, rate: int, frequency: float, amplitude: float = 1.0,
         brightness: float = 1.0) -> np.ndarray:
    count = max(1, int(duration * rate))
    t = np.arange(count, dtype=np.float64) / rate
    signal = np.zeros(count, dtype=np.float64)
    partials = ((1.0, 1.0), (2.01, 0.34), (3.97, 0.16), (6.12, 0.08))
    for ratio, level in partials:
        decay = np.exp(-t * (3.2 + ratio * 0.65) / max(0.35, brightness))
        signal += level * np.sin(2.0 * math.pi * frequency * ratio * t) * decay
    signal *= np.minimum(1.0, t / 0.006)
    return signal * amplitude


def add(target: np.ndarray, source: np.ndarray, start_seconds: float, rate: int, gain: float = 1.0) -> None:
    start = max(0, int(start_seconds * rate))
    if start >= len(target):
        return
    end = min(len(target), start + len(source))
    target[start:end] += source[:end - start] * gain


def mono_mix(duration: float, rate: int, layers: list[tuple[np.ndarray, float, float]]) -> np.ndarray:
    output = np.zeros(max(1, int(duration * rate)), dtype=np.float64)
    for signal, start, gain in layers:
        add(output, signal, start, rate, gain)
    return output


def finalize(signal: np.ndarray, peak: float = 0.86) -> np.ndarray:
    signal = np.nan_to_num(signal.astype(np.float64))
    signal -= np.mean(signal, axis=0, keepdims=True)
    edge = min(len(signal) // 4, 96)
    if edge > 1:
        fade = np.linspace(0.0, 1.0, edge)
        signal[:edge] *= fade[:, None] if signal.ndim == 2 else fade
        signal[-edge:] *= fade[::-1, None] if signal.ndim == 2 else fade[::-1]
    maximum = float(np.max(np.abs(signal))) or 1.0
    return np.clip(signal * (peak / maximum), -1.0, 1.0)


def write_wav(path: Path, signal: np.ndarray, rate: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    signal = finalize(signal)
    pcm = np.round(signal * 32767.0).astype("<i2")
    channels = 1 if pcm.ndim == 1 else pcm.shape[1]
    with wave.open(str(path), "wb") as stream:
        stream.setnchannels(channels)
        stream.setsampwidth(2)
        stream.setframerate(rate)
        stream.writeframes(pcm.tobytes())


def paper_rustle(duration: float, rate: int, seed: int, reverse: bool = False) -> np.ndarray:
    signal = band_noise(duration, rate, 650.0, 7_500.0, 0.7, 0.06, 0.35, seed)
    t = np.linspace(0.0, 1.0, len(signal))
    motion = (t ** 1.8 if reverse else (1.0 - t) ** 1.3)
    flutter = 0.55 + 0.45 * np.sin(2.0 * math.pi * (8.0 * t + 3.0 * t * t)) ** 2
    return signal * motion * flutter


def wet_drop(duration: float, rate: int, seed: int, amplitude: float = 1.0) -> np.ndarray:
    return mono_mix(duration, rate, [
        (chirp(duration * 0.72, rate, 460.0, 82.0, 0.7, 0.0, 0.62), 0.0, amplitude),
        (chirp(duration * 0.48, rate, 190.0, 55.0, 0.45, 0.0, 0.72), 0.03, amplitude),
        (band_noise(duration * 0.56, rate, 90.0, 1_200.0, 0.38, 0.0, 0.82, seed), 0.0, amplitude),
    ])


def impact(duration: float, rate: int, seed: int, weight: float = 1.0) -> np.ndarray:
    return mono_mix(duration, rate, [
        (chirp(duration * 0.65, rate, 150.0 * weight, 48.0, 0.8, 0.0, 0.78), 0.0, 0.9),
        (band_noise(duration * 0.55, rate, 120.0, 4_800.0, 0.75, 0.0, 0.88, seed), 0.0, 0.72),
        (paper_rustle(duration * 0.8, rate, seed + 11), 0.04, 0.35),
    ])


def electric(duration: float, rate: int, seed: int, base_hz: float = 165.0) -> np.ndarray:
    count = max(1, int(duration * rate))
    t = np.arange(count, dtype=np.float64) / rate
    jitter = 1.0 + 0.12 * np.sin(2 * math.pi * 37.0 * t) + 0.06 * np.sin(2 * math.pi * 83.0 * t)
    carrier = np.tanh(2.4 * np.sin(2 * math.pi * base_hz * jitter * t))
    sparks = band_noise(duration, rate, 2_200.0, 12_000.0, 0.62, 0.0, 0.9, seed)
    return (0.55 * carrier + sparks) * envelope(count, 0.005, 0.55)


def crystal_cluster(duration: float, rate: int, base_hz: float, seed: int) -> np.ndarray:
    output = np.zeros(int(duration * rate), dtype=np.float64)
    local = np.random.default_rng(seed)
    for index, ratio in enumerate((1.0, 1.24, 1.5, 2.03, 2.49)):
        start = float(local.uniform(0.0, duration * 0.34))
        add(output, bell(duration - start, rate, base_hz * ratio, 0.34 / (1 + index * 0.12)), start, rate)
    add(output, band_noise(duration * 0.72, rate, 2_800.0, 11_500.0, 0.28, 0.0, 0.9, seed + 2), 0.0, rate)
    return output


def make_sfx() -> dict[str, tuple[np.ndarray, str]]:
    r = SFX_RATE
    result: dict[str, tuple[np.ndarray, str]] = {}

    def put(name: str, category: str, duration: float, layers: list[tuple[np.ndarray, float, float]]) -> None:
        result[name] = (mono_mix(duration, r, layers), category)

    put("CommonBtnClick", "UI", 0.14, [
        (chirp(0.07, r, 980, 680, 0.8, 0.0, 0.8, "triangle"), 0.0, 0.75),
        (band_noise(0.08, r, 900, 6_000, 0.45, 0.0, 0.9, 10), 0.0, 0.5),
    ])
    put("SfxUiCardPickup", "UI", 0.30, [
        (paper_rustle(0.27, r, 11, True), 0.0, 0.8),
        (bell(0.18, r, 740, 0.45), 0.07, 0.5),
    ])
    put("SfxUiCardDrop", "UI", 0.26, [
        (paper_rustle(0.20, r, 12), 0.0, 0.55),
        (impact(0.18, r, 13, 1.5), 0.07, 0.48),
    ])
    put("SfxUiInvalid", "UI", 0.34, [
        (chirp(0.18, r, 260, 175, 0.75, 0.0, 0.7, "triangle"), 0.0, 0.72),
        (chirp(0.15, r, 210, 145, 0.65, 0.0, 0.72, "triangle"), 0.13, 0.56),
        (paper_rustle(0.22, r, 14), 0.02, 0.25),
    ])
    put("SfxUiEquip", "UI", 0.46, [
        (paper_rustle(0.24, r, 15), 0.0, 0.62),
        (impact(0.20, r, 16, 1.4), 0.08, 0.5),
        (bell(0.30, r, 659.25, 0.48), 0.12, 0.7),
    ])
    put("SfxUiLock", "UI", 0.30, [
        (impact(0.16, r, 17, 1.8), 0.0, 0.48),
        (bell(0.18, r, 1_120, 0.38), 0.07, 0.65),
    ])
    put("SfxUiUnlock", "UI", 0.38, [
        (impact(0.13, r, 18, 1.5), 0.0, 0.38),
        (bell(0.28, r, 740, 0.38), 0.06, 0.65),
        (bell(0.24, r, 988, 0.30), 0.13, 0.55),
    ])

    put("SfxSpellGenerated", "System", 0.62, [
        (wet_drop(0.44, r, 20, 0.75), 0.0, 0.7),
        (paper_rustle(0.32, r, 21, True), 0.08, 0.45),
        (bell(0.38, r, 880, 0.48), 0.18, 0.66),
    ])
    put("SfxOfflineReward", "System", 1.30, [
        (paper_rustle(0.92, r, 22, True), 0.0, 0.7),
        (wet_drop(0.38, r, 23, 0.55), 0.25, 0.55),
        (bell(0.55, r, 587.33, 0.35), 0.42, 0.55),
        (bell(0.48, r, 739.99, 0.34), 0.62, 0.55),
        (bell(0.42, r, 880.0, 0.32), 0.82, 0.55),
    ])
    put("SfxSynthesisStart", "Synthesis", 0.72, [
        (paper_rustle(0.68, r, 24, True), 0.0, 0.75),
        (chirp(0.68, r, 120, 620, 0.55, 0.1, 0.06), 0.0, 0.7),
    ])
    put("SfxSynthesisSuccess", "Synthesis", 1.18, [
        (wet_drop(0.54, r, 25, 0.72), 0.0, 0.55),
        (impact(0.42, r, 26, 1.25), 0.16, 0.52),
        (bell(0.82, r, 523.25, 0.42), 0.22, 0.68),
        (bell(0.68, r, 659.25, 0.38), 0.36, 0.64),
        (bell(0.56, r, 783.99, 0.34), 0.50, 0.60),
        (band_noise(0.60, r, 4_500, 13_500, 0.24, 0.0, 0.9, 27), 0.30, 0.45),
    ])
    put("SfxSynthesisInk", "Synthesis", 1.02, [
        (wet_drop(0.78, r, 28, 1.0), 0.0, 0.85),
        (impact(0.35, r, 29, 0.85), 0.08, 0.45),
        (bell(0.48, r, 698.46, 0.30), 0.40, 0.58),
    ])
    put("SfxUpgradeSuccess", "System", 0.96, [
        (paper_rustle(0.58, r, 30, True), 0.0, 0.55),
        (chirp(0.62, r, 210, 920, 0.38, 0.08, 0.08), 0.04, 0.55),
        (bell(0.62, r, 659.25, 0.34), 0.20, 0.62),
        (bell(0.50, r, 987.77, 0.30), 0.36, 0.58),
        (impact(0.22, r, 31, 1.25), 0.56, 0.35),
    ])

    put("SfxFireballCast", "Spell", 0.52, [
        (band_noise(0.48, r, 260, 6_500, 0.64, 0.12, 0.2, 40), 0.0, 0.62),
        (chirp(0.46, r, 90, 360, 0.62, 0.03, 0.25), 0.0, 0.72),
    ])
    for index, seed in enumerate((41, 42), 1):
        put(f"SfxFireballImpact{index}", "Spell", 0.62, [
            (impact(0.58, r, seed, 1.0 + index * 0.08), 0.0, 0.82),
            (band_noise(0.50, r, 1_300, 12_000, 0.56, 0.0, 0.9, seed + 10), 0.02, 0.45),
        ])
    put("SfxChainCast", "Spell", 0.44, [
        (electric(0.40, r, 50, 145), 0.0, 0.82),
        (bell(0.28, r, 1_260, 0.22, 1.4), 0.08, 0.45),
    ])
    for index, base in enumerate((245, 315), 1):
        put(f"SfxChainJump{index}", "Spell", 0.29, [
            (electric(0.26, r, 50 + index, base), 0.0, 0.74),
            (chirp(0.20, r, 1_900 + index * 180, 760, 0.38, 0.0, 0.8), 0.0, 0.45),
        ])
    put("SfxFrostCast", "Spell", 0.62, [
        (band_noise(0.58, r, 2_000, 12_500, 0.42, 0.12, 0.35, 60), 0.0, 0.55),
        (crystal_cluster(0.50, r, 740, 61), 0.08, 0.58),
    ])
    put("SfxFrostImpact", "Spell", 0.82, [
        (crystal_cluster(0.78, r, 620, 62), 0.0, 0.72),
        (impact(0.42, r, 63, 1.4), 0.03, 0.32),
    ])
    put("SfxShieldCast", "Spell", 0.84, [
        (chirp(0.64, r, 95, 260, 0.58, 0.05, 0.3), 0.0, 0.62),
        (bell(0.76, r, 293.66, 0.38, 0.75), 0.08, 0.7),
        (bell(0.66, r, 440.0, 0.28, 0.8), 0.14, 0.55),
    ])
    for index, seed in enumerate((65, 66), 1):
        put(f"SfxShieldAbsorb{index}", "Spell", 0.38, [
            (impact(0.31, r, seed, 1.55), 0.0, 0.44),
            (bell(0.30, r, 350 + index * 35, 0.26, 0.6), 0.04, 0.55),
        ])
    put("SfxShieldBreak", "Spell", 0.94, [
        (crystal_cluster(0.82, r, 420, 67), 0.0, 0.60),
        (paper_rustle(0.76, r, 68), 0.08, 0.52),
        (impact(0.38, r, 69, 0.8), 0.0, 0.46),
    ])

    for index, seed in enumerate((80, 81, 82), 1):
        put(f"SfxEnemyHit{index}", "Battle", 0.27, [
            (impact(0.23, r, seed, 1.25 + index * 0.08), 0.0, 0.62),
            (paper_rustle(0.20, r, seed + 10), 0.01, 0.34),
        ])
    for index, seed in enumerate((84, 85), 1):
        put(f"SfxEnemyDeath{index}", "Battle", 0.72, [
            (wet_drop(0.62, r, seed, 0.72), 0.0, 0.52),
            (paper_rustle(0.68, r, seed + 10), 0.0, 0.66),
            (chirp(0.58, r, 320 - index * 25, 58, 0.5, 0.0, 0.72), 0.04, 0.55),
        ])
    for index, seed in enumerate((88, 89), 1):
        put(f"SfxBookHit{index}", "Battle", 0.34, [
            (impact(0.30, r, seed, 1.75), 0.0, 0.70),
            (paper_rustle(0.29, r, seed + 10), 0.02, 0.48),
        ])

    put("SfxVictory", "Stage", 1.48, [
        (bell(1.20, r, 392.0, 0.36), 0.0, 0.55),
        (bell(1.08, r, 523.25, 0.34), 0.18, 0.58),
        (bell(0.92, r, 659.25, 0.32), 0.36, 0.60),
        (bell(0.72, r, 783.99, 0.28), 0.58, 0.54),
        (paper_rustle(0.72, r, 91, True), 0.18, 0.26),
    ])
    put("SfxDefeat", "Stage", 1.44, [
        (bell(1.18, r, 293.66, 0.34, 0.62), 0.0, 0.58),
        (bell(1.00, r, 246.94, 0.30, 0.60), 0.20, 0.55),
        (bell(0.80, r, 196.0, 0.28, 0.58), 0.42, 0.52),
        (wet_drop(0.70, r, 92, 0.48), 0.30, 0.36),
    ])
    put("SfxRetry", "Stage", 0.76, [
        (paper_rustle(0.64, r, 93, True), 0.0, 0.70),
        (impact(0.20, r, 94, 1.4), 0.46, 0.40),
        (bell(0.24, r, 587.33, 0.24), 0.48, 0.50),
    ])
    put("SfxBossEnter", "Stage", 1.72, [
        (chirp(1.42, r, 46, 82, 0.64, 0.15, 0.18), 0.0, 0.74),
        (band_noise(1.45, r, 45, 1_600, 0.58, 0.18, 0.24, 95), 0.0, 0.55),
        (impact(0.62, r, 96, 0.58), 0.72, 0.78),
        (bell(0.72, r, 146.83, 0.32, 0.55), 0.78, 0.52),
    ])
    return result


def periodic_noise(count: int, rate: int, low_hz: float, high_hz: float, seed: int) -> np.ndarray:
    local = np.random.default_rng(seed)
    phases = local.uniform(0.0, 2.0 * math.pi, count // 2 + 1)
    frequencies = np.fft.rfftfreq(count, 1.0 / rate)
    amplitude = np.zeros_like(frequencies)
    mask = (frequencies >= low_hz) & (frequencies <= high_hz)
    amplitude[mask] = 1.0 / np.sqrt(np.maximum(frequencies[mask], 1.0))
    spectrum = amplitude * np.exp(1j * phases)
    signal = np.fft.irfft(spectrum, count)
    return signal / (np.max(np.abs(signal)) or 1.0)


def add_stereo(target: np.ndarray, source: np.ndarray, start_seconds: float,
               rate: int, gain: float, pan: float = 0.0, wrap: bool = False) -> None:
    left = math.sqrt((1.0 - pan) * 0.5)
    right = math.sqrt((1.0 + pan) * 0.5)
    stereo = np.column_stack((source * left, source * right)) * gain
    start = int(start_seconds * rate)
    if not wrap:
        end = min(len(target), start + len(stereo))
        if end > start:
            target[start:end] += stereo[:end - start]
        return
    indices = (np.arange(len(stereo)) + start) % len(target)
    np.add.at(target[:, 0], indices, stereo[:, 0])
    np.add.at(target[:, 1], indices, stereo[:, 1])


def make_bgm(boss: bool) -> np.ndarray:
    rate = BGM_RATE
    bpm = 80.0
    beat = 60.0 / bpm
    bar = beat * 4.0
    bars = 16
    duration = bar * bars
    count = int(duration * rate)
    output = np.zeros((count, 2), dtype=np.float64)

    ambience = periodic_noise(count, rate, 80.0 if boss else 180.0, 7_500.0, 401 if boss else 400)
    slow = 0.72 + 0.28 * np.sin(2.0 * math.pi * np.arange(count) / count * (3 if boss else 2))
    output[:, 0] += ambience * slow * (0.048 if boss else 0.035)
    output[:, 1] += np.roll(ambience, 311) * slow * (0.044 if boss else 0.032)

    main_chords = [
        (146.83, 174.61, 220.00),
        (130.81, 164.81, 196.00),
        (98.00, 146.83, 196.00),
        (110.00, 146.83, 220.00),
    ]
    boss_chords = [
        (73.42, 110.00, 146.83),
        (65.41, 98.00, 130.81),
        (69.30, 103.83, 138.59),
        (55.00, 82.41, 110.00),
    ]
    chords = boss_chords if boss else main_chords
    melody = [293.66, 349.23, 392.00, 440.00, 392.00, 349.23, 261.63, 293.66]
    boss_melody = [146.83, 174.61, 164.81, 146.83, 130.81, 146.83, 123.47, 110.00]

    for bar_index in range(bars):
        chord = chords[bar_index % len(chords)]
        start = bar_index * bar
        for tone_index, frequency in enumerate(chord):
            pad = chirp(bar * 0.96, rate, frequency, frequency, 0.25, 0.18, 0.22)
            add_stereo(output, pad, start + bar * 0.02, rate, 0.23 if boss else 0.18, -0.45 + tone_index * 0.45)
        root = chord[0] * (0.5 if boss else 1.0)
        add_stereo(output, chirp(beat * 0.46, rate, root * 1.02, root, 0.54, 0.005, 0.72, "triangle"),
                   start, rate, 0.34 if boss else 0.22, -0.18)
        add_stereo(output, impact(beat * 0.30, rate, 500 + bar_index, 0.62 if boss else 0.9),
                   start, rate, 0.28 if boss else 0.12, 0.0)
        add_stereo(output, impact(beat * 0.22, rate, 600 + bar_index, 1.45),
                   start + beat * 2.0, rate, 0.18 if boss else 0.08, 0.18)

        notes = boss_melody if boss else melody
        if bar_index % 2 == 0 or boss:
            for step in (1, 3):
                note = notes[(bar_index + step) % len(notes)]
                pluck = bell(beat * (0.80 if boss else 0.62), rate, note, 0.38, 0.65 if boss else 0.95)
                add_stereo(output, pluck, start + step * beat, rate, 0.23 if boss else 0.28,
                           -0.32 if (bar_index + step) % 2 else 0.32)

        if bar_index % 4 == 3:
            brush = paper_rustle(beat * 1.4, rate, 700 + bar_index, True)
            add_stereo(output, brush, start + beat * 2.45, rate, 0.12 if boss else 0.10, 0.42)

    if boss:
        drone_t = np.arange(count, dtype=np.float64) / rate
        drone = np.sin(2 * math.pi * 36.71 * drone_t) * (0.7 + 0.3 * np.sin(2 * math.pi * drone_t / duration * 4))
        output[:, 0] += drone * 0.085
        output[:, 1] += np.roll(drone, 173) * 0.082

    # The score is constructed on an exact 16-bar boundary. Match the seam's
    # short ambience window to suppress decoder-independent loop clicks.
    seam = int(rate * 0.08)
    common = (output[:seam] + output[-seam:]) * 0.5
    output[:seam] = common
    output[-seam:] = common
    return output


def analyze(path: Path) -> dict[str, object]:
    with wave.open(str(path), "rb") as stream:
        frames = stream.getnframes()
        rate = stream.getframerate()
        channels = stream.getnchannels()
        raw = stream.readframes(frames)
    pcm = np.frombuffer(raw, dtype="<i2").astype(np.float64) / 32768.0
    peak = float(np.max(np.abs(pcm))) if len(pcm) else 0.0
    rms = float(np.sqrt(np.mean(pcm * pcm))) if len(pcm) else 0.0
    return {
        "path": path.relative_to(ROOT).as_posix(),
        "durationSeconds": round(frames / rate, 3),
        "sampleRate": rate,
        "channels": channels,
        "sourceBytes": path.stat().st_size,
        "peakDbfs": round(20.0 * math.log10(max(peak, 1e-9)), 2),
        "rmsDbfs": round(20.0 * math.log10(max(rms, 1e-9)), 2),
    }


def main() -> None:
    sfx = make_sfx()
    records = []
    for name, (signal, category) in sorted(sfx.items()):
        path = OUTPUT / "SFX" / category / f"{name}.wav"
        write_wav(path, signal, SFX_RATE)
        record = analyze(path)
        record["category"] = category
        record["address"] = name
        records.append(record)

    for name, boss in (("BgmMainLoop", False), ("BgmBossLoop", True)):
        path = OUTPUT / "BGM" / f"{name}.wav"
        write_wav(path, make_bgm(boss), BGM_RATE)
        record = analyze(path)
        record["category"] = "BGM"
        record["address"] = name
        record["loop"] = True
        records.append(record)

    payload = {
        "schemaVersion": 1,
        "generatedBy": "Tools/Audio/generate_inkspell_audio.py",
        "license": "Original procedural audio generated for Inkspell; no third-party samples.",
        "sourceBudgetBytes": 14 * 1024 * 1024,
        "unityBuildBudgetBytes": 2 * 1024 * 1024,
        "assets": sorted(records, key=lambda item: (item["category"], item["address"])),
    }
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    total = sum(int(item["sourceBytes"]) for item in records)
    print(f"Generated {len(records)} clips; source bytes={total}; budget={payload['sourceBudgetBytes']}")
    if total > payload["sourceBudgetBytes"]:
        raise SystemExit("Audio source budget exceeded")


if __name__ == "__main__":
    main()
