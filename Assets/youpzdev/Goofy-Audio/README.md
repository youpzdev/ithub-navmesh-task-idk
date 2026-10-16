# 🔊 Goofy Audio

> 🔊 Goofy AudioSource pooling for Unity. One line to play anything.

**by [youpzdev](https://github.com/youpzdev)**

---

## What is this

A pooled audio system for Unity. No AudioSource management, no GameObject spawning per sound. Just play.

```csharp
Audio.Play(shootClip, muzzle.position);
Audio.Play(clickClip);
```

---

## Requirements

> ⚠️ Requires **[Goofy Timers](https://github.com/youpzdev/unity-goofy-timers)**

Install `goofy-timers.unitypackage` or just copy `Timer.cs` into your `Assets/` folder before using Goofy Audio.

---

## Install

Install `goofy-audio.unitypackage` or just copy `Audio.cs` into your `Assets/` folder. Done.

---

## Usage

```csharp
// ── 2D sound (UI, music, ambient) ─────────────────────────
Audio.Play(clickClip);
Audio.Play(musicClip, volume: 0.5f);

// ── 3D sound (world space) ─────────────────────────────────
Audio.Play(explosionClip, transform.position);
Audio.Play(shootClip, muzzle.position, volume: 0.8f);

// ── Pitch ──────────────────────────────────────────────────
Audio.Play(shootClip, muzzle.position, pitch: Random.Range(0.9f, 1.1f));

// ── AudioMixer ─────────────────────────────────────────────
Audio.Play(shootClip, muzzle.position, mixerGroup: sfxGroup);
Audio.Play(ambienceClip, mixerGroup: ambienceGroup);

// ── All params ─────────────────────────────────────────────
Audio.Play(shootClip, muzzle.position, volume: 0.7f, pitch: 1.2f, mixerGroup: sfxGroup);
```

---

## How it works

| | |
|---|---|
| 🎵 **Pool** | Reuses `AudioSource` instances — no GameObject alloc per sound |
| 📦 **Container** | Single `DontDestroyOnLoad` GameObject holds all sources |
| ⏱️ **Auto-return** | Source returns to pool after `clip.length / pitch` via Goofy Timers |
| 🔊 **2D / 3D** | Separate overloads — no ambiguous `Vector3.zero` detection |
| 🛡️ **Safe pitch** | `Mathf.Max(Mathf.Abs(pitch), 0.01f)` — zero pitch won't break the timer |

---

## Part of the Goofy Tools collection

| | |
|---|---|
| [**goofy-pooling**](https://github.com/youpzdev/unity-goofy-pooling) | 🐟 Zero-config object pooling |
| [**goofy-timers**](https://github.com/youpzdev/unity-goofy-timers) | ⏱️ No-coroutine timer system |
| [**goofy-eventbus**](https://github.com/youpzdev/unity-goofy-eventbus) | 📡 Type-safe event bus |
| [**goofy-save**](https://github.com/youpzdev/unity-goofy-saves) | 💾 AES-256 encrypted save system |
| **goofy-audio** | 🔊 You are here |

---

## License

MIT — do whatever you want.
