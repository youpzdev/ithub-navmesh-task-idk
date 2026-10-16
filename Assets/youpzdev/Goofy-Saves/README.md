# 💾 Goofy Save

> AES-256 encrypted save system for Unity. One line to save anything.

**by [youpzdev](https://github.com/youpzdev)**

---

## What is this

Save system with AES-256 encryption built in. Supports any type — no config, no setup, just works.

```csharp
Save.Set("coins", 100);
Save.Set("items", new List<string> { "sword", "shield" });

int coins = Save.Get<int>("coins");
var items = Save.Get<List<string>>("items");
```

---

## Requirements

> ⚠️ Requires **Newtonsoft.Json (Json.NET)**

Add to `Packages/manifest.json`:
```json
"com.unity.nuget.newtonsoft-json": "3.2.1"
```
Or install via **Window → Package Manager → Add by name**.

---

## Install

Install `goofy-saves.unitypackage` or just copy `Save.cs` into your `Assets/` folder. That's it.

---

## Usage

```csharp
// ── Set ────────────────────────────────────────────────────
Save.Set("coins", 100);
Save.Set("name", "youpzdev");
Save.Set("volume", 0.8f);
Save.Set("items", new List<string> { "sword", "shield" });
Save.Set("player", new PlayerData { name = "youpz", level = 5 });

// ── Get ────────────────────────────────────────────────────
int coins     = Save.Get<int>("coins");
string name   = Save.Get("name", "Player");       // default value if missing
float volume  = Save.Get("volume", 1f);
var items     = Save.Get<List<string>>("items");
var player    = Save.Get<PlayerData>("player");

// ── Check & Delete ─────────────────────────────────────────
if (Save.Has("coins")) Debug.Log("save exists");
Save.Delete("coins");
Save.DeleteAll();

// ── Custom encryption key (optional) ──────────────────────
// Call before any Set/Get — exactly 32 characters
Save.SetKey("mySecretKey1234567890abcdefghij");

// ── Disable encryption ─────────────────────────────────────
Save.UseEncryption = false;
```

---

## How it works

| | |
|---|---|
| 📁 **Storage** | `Application.persistentDataPath/save.dat` |
| 🔐 **Encryption** | AES-256 with random IV — same data, different cipher every time |
| 🔑 **Default key** | Derived from `deviceUniqueIdentifier` — unique per device, never stored |
| ⚡ **Lazy load** | File is read only on first access |
| 🛡️ **Resilience** | `try/catch` on load and Get — corrupted save won't crash the game |

---

## Part of the Goofy Tools collection

| | |
|---|---|
| [**goofy-pooling**](https://github.com/youpzdev/unity-goofy-pooling) | 🐟 Zero-config object pooling |
| [**goofy-timers**](https://github.com/youpzdev/unity-goofy-timers) | ⏱️ No-coroutine timer system |
| [**goofy-eventbus**](https://github.com/youpzdev/unity-goofy-eventbus) | 📡 Type-safe event bus |
| **goofy-save** | 💾 You are here |

---

## License

MIT — do whatever you want.
