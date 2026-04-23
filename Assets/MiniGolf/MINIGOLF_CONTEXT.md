# MiniGolf Game — Full Context Document

> This document exists so a new Claude session can fully understand this project without any prior conversation history.
> Project path: `C:\EstotyDemoTest`

---

## What Was Built

A 2D top-down mini-golf style ball-shooting game for Unity 6, built to the following specification:

- Player holds finger → trajectory line appears from the ball
- Drag direction sets shoot direction; drag distance sets power
- **Blue hole (Good)** → +5 seconds on timer
- **Red hole (Bad)** → −10 seconds on timer
- 30-second countdown timer at top of screen
- Holes randomly change type (Good ↔ Bad) at random intervals (4–9s)
- Holes **flash** for ~2 seconds before changing type as a warning
- Holes **reposition** after every shot (success or miss)
- Holes must NOT reposition while player is actively aiming
- Game over when timer hits 0

---

## Unity Project Info

| Field | Value |
|---|---|
| Unity Version | 6000.4.3f1 |
| Render Pipeline | URP (Universal Render Pipeline) |
| Active Input Handling | `1` = New Input System Package ONLY |
| Physics | 2D (Rigidbody2D, CircleCollider2D, BoxCollider2D) |
| Camera | Orthographic, size 5, positioned at z = −10; portrait 9:16 reference (1080×1920) |

**Critical:** This project uses the **New Input System only**. Never use `UnityEngine.Input`. Always use `UnityEngine.InputSystem.Pointer.current`.

### Key Unity 6 API Names (differ from older Unity)
- `Rigidbody2D.linearVelocity` — was `velocity` in older versions
- `Rigidbody2D.linearDamping` — was `drag` in older versions
- `Pointer.current.press.wasPressedThisFrame / isPressed / wasReleasedThisFrame`
- `Pointer.current.position.ReadValue()` → `Vector2` screen position

---

## How to Run

1. Open `C:\EstotyDemoTest` in Unity 6000.4.3f1
2. Wait for compilation to complete (no errors expected)
3. Press **▶ Play**

---

## File Structure

```
Assets/MiniGolf/
│
├── MINIGOLF_CONTEXT.md          ← This file
│
├── Config/
│   └── GameConfig.asset         ← ScriptableObject
│
├── Prefabs/                     ← Auto-created by Setup Scene
│   ├── Hole.prefab
│   └── TrajectoryDot.prefab
│
├── Sprites/                     ← Auto-created by Setup Scene (generated PNGs)
│   ├── Circle.png               (128×128 circle, used for ball and holes)
│   └── Dot.png                  (32×32 circle, used for trajectory dots)
│
├── Scripts/
│   ├── Config/
│   │   └── GameConfig.cs        ← ScriptableObject with all tunable values
│   │
│   ├── Core/
│   │   ├── GameState.cs         ← Enum: Idle, Aiming, InFlight, Resolving, GameOver
│   │   └── GameManager.cs       ← Main orchestrator; owns the state machine
│   │
│   ├── Services/
│   │   ├── Interfaces/
│   │   │   └── ITimerService.cs ← Interface (SRP/DIP)
│   │   └── TimerService.cs      ← MonoBehaviour implementing ITimerService
│   │
│   ├── Ball/
│   │   ├── BallController.cs    ← Input + Rigidbody2D + events
│   │   └── TrajectoryRenderer.cs← Pool of dot SpriteRenderers along aim path
│   │
│   ├── Hole/
│   │   ├── HoleType.cs          ← Enum: Good, Bad
│   │   ├── HoleController.cs    ← Per-hole logic: type change, flash, collision
│   │   └── HoleManager.cs       ← Spawns/repositions all holes
│   │
│   ├── UI/
│   │   ├── TimerDisplay.cs      ← Binds to ITimerService; shows countdown + bonus popup
│   │   └── GameOverPanel.cs     ← Shown on time expiry; restart button reloads scene
│   │
│   └── Audio/
│       └── AudioManager.cs      ← Singleton; null-safe PlayXxx() methods
│
```

---

## Architecture & SOLID Principles

### Single Responsibility
Each class does exactly one thing:
- `TimerService` only counts down time
- `TrajectoryRenderer` only draws dots
- `HoleController` only manages one hole's lifecycle
- `AudioManager` only plays sounds

### Open/Closed
- `ITimerService` interface allows the timer implementation to be swapped without touching `GameManager` or `TimerDisplay`
- `HoleType` enum allows adding new hole types without rewriting `HoleController`

### Liskov Substitution
- `TimerService` can be replaced with any `ITimerService` implementation and nothing breaks

### Interface Segregation
- `ITimerService` is a narrow, focused interface (not a god-interface)
- Components only depend on what they need

### Dependency Inversion
- `GameManager` depends on `ITimerService` (abstraction), not `TimerService` (concretion)
- `TimerDisplay.Bind(ITimerService)` takes the interface, not the concrete class

---

## Game State Machine

```
         ┌──────────────────────────────────────────┐
         │                                          │
    ┌────▼────┐  OnAimStarted   ┌──────────┐       │
    │  Idle   ├────────────────►│  Aiming  │       │
    └─────────┘                 └────┬─────┘       │
         ▲                           │ OnShotFired  │
         │                           ▼              │
         │                     ┌──────────┐         │
         │                     │ InFlight │         │
         │                     └────┬─────┘         │
         │                          │               │
         │          OnBallStopped   │  OnBallEnteredHole
         │          ───────────────►▼◄──────────────│
         │                    ┌───────────┐         │
         │  ResolveShot()     │ Resolving │         │
         └────────────────────┤           │         │
          (reposition+reset)  └─────┬─────┘         │
                                    │ OnTimeExpired  │
                                    └───────────────►│
                                                ┌────▼────┐
                                                │GameOver │
                                                └─────────┘
```

**State rules:**
- `Idle` → Input accepted, holes unlocked
- `Aiming` → `HoleManager.LockPositions()` called (holes cannot reposition)
- `InFlight` → Input disabled, holes unlocked
- `Resolving` → Input disabled, holes locked; waits `ballResetDelay` then repositions holes, then resets ball
- `GameOver` → Everything frozen, panel shown

---

## Key Event Flow

```
HoleController.OnTriggerEnter2D("Ball" tag)
    → HoleController.SetActive(false) + PlayEntryEffect()
    → HoleController.OnBallEntered(this)
        → HoleManager.HandleBallEntered(hole)
            → HoleManager.OnBallEnteredHole(holeType)
                → GameManager.HandleBallEnteredHole(holeType)
                    → BallController.FreezeBall()
                    → TimerService.AddTime / SubtractTime
                    → TimerDisplay.ShowBonus(amount)
                    → AudioManager.PlayGoodHole / PlayBadHole
                    → StartCoroutine(ResolveShot())

BallController (velocity < stopThreshold for 0.4s+)
    → BallController.OnBallStopped
        → GameManager.HandleBallStopped
            → AudioManager.PlayMiss()
            → StartCoroutine(ResolveShot())

GameManager.ResolveShot()
    1. Wait ballResetDelay (0.6s)
    2. HoleManager.RepositionHoles()
    3. Wait holeRepositionDelay (0.3s)
    4. BallController.ResetToStartPosition()  ← elastic spawn animation
    5. Wait 0.35s
    6. State = Idle, EnableInput()
```

---

## Input Mechanic Detail

Uses `UnityEngine.InputSystem.Pointer.current` — works for both mouse (editor) and touch (mobile).

```
Press down anywhere on screen:
  _pressWorldPos = Camera.ScreenToWorldPoint(pointer.position)
  _isAiming = true
  Notify: OnAimStarted

Each frame while held:
  delta = currentWorldPos - _pressWorldPos
  shootDirection = delta.normalized
  normalizedForce = Clamp(delta.magnitude, 0, maxDragDistance) / maxDragDistance
  TrajectoryRenderer.UpdateTrajectory(ball.position, shootDirection, normalizedForce)

On release:
  Shoot: Rigidbody2D.linearVelocity = shootDirection * (normalizedForce * maxShootForce)
  Hide trajectory
  Notify: OnShotFired
  Start MonitorBallStop coroutine
```

**Trajectory dots**: 12 sprite instances pooled in `TrajectoryRenderer`. Each dot is placed at `ballPos + direction * (i * dotSpacing * normalizedForce)` with decreasing alpha (far dots are more transparent).

---

## Hole Lifecycle

```
HoleController.Initialize(config, initialType)
    → SetType(Good or Bad)
    → StartTypeChangeTimer()
        → wait (totalInterval - 2s)
        → FlashWarningRoutine(2s)    ← PingPong color between Good↔Bad colors
            → AudioManager.PlayWarning()
        → HoleType flipped
        → StartTypeChangeTimer()     ← loop forever
```

**Colors:**
- Good (Blue): `rgb(0.2, 0.55, 1.0)`
- Bad (Red): `rgb(1.0, 0.25, 0.25)`

**Position change** happens only via `HoleManager.RepositionHoles()`, called from `GameManager.ResolveShot()`. It is blocked while `_positionsLocked == true` (set by `LockPositions()`).

---

## GameConfig ScriptableObject Values (defaults)

| Field | Default | Notes |
|---|---|---|
| `initialTime` | 30s | Starting countdown |
| `goodHoleTimeBonus` | 5s | Added on blue hole |
| `badHoleTimePenalty` | 10s | Subtracted on red hole |
| `shootForceMultiplier` | 6 | — |
| `maxShootForce` | 18 | Max impulse applied |
| `maxDragDistance` | 2.5 units | World space |
| `ballStopThreshold` | 0.05 | velocity.magnitude below = stopped |
| `ballResetDelay` | 0.6s | Pause before repositioning |
| `holeRepositionDelay` | 0.3s | Pause after repositioning |
| `holeCount` | 4 | Holes on screen at once |
| `holeChangeMinInterval` | 4s | Fastest type change |
| `holeChangeMaxInterval` | 9s | Slowest type change |
| `holeFlashWarningDuration` | 2s | Warning flash before type change |
| `trajectoryDotCount` | 12 | Pooled dot count |
| `trajectoryDotSpacing` | 0.45 units | Distance between dots |
| `playfieldMin` | (-2.3, -4.5) | World space boundary (portrait) |
| `playfieldMax` | (2.3, 4.5) | World space boundary (portrait) |
| `ballStartPosition` | (0, -3.8) | Ball reset position (bottom of portrait screen) |
| `holeSafeRadiusFromBall` | 1.5 units | Holes won't spawn near ball |
| `holeRadius` | 0.42 units | Used for spawn spacing |

All values are editable in the Inspector on the `GameConfig.asset`.

---

## Scene Hierarchy

```
[MiniGolf] Main Camera          ← Orthographic, size=5, pos=(0,0,-10)
[MiniGolf] Background           ← Visual layers (green quads)
[MiniGolf] Trajectory           ← TrajectoryRenderer + dot pool
[MiniGolf] Ball                 ← BallController, Rigidbody2D, CircleCollider2D, tag="Ball"
[MiniGolf] Holes                ← HoleManager + 4× Hole instances
[MiniGolf] Walls                ← 4× invisible BoxCollider2D borders
[MiniGolf] AudioManager         ← AudioManager singleton + AudioSource children
[MiniGolf] GameManager          ← GameManager + TimerService
[MiniGolf] UI                   ← Canvas (ScreenSpaceOverlay, 1920×1080 reference)
    ├── TimerPanel               ← TimerDisplay + TMP timer text + bonus popup text
    └── GameOverPanel            ← GameOverPanel (starts disabled, shown on game over)
```

---

## Audio System

`AudioManager` is a singleton (`AudioManager.Instance`). All `PlayXxx()` calls are null-safe — if no clip is assigned, nothing happens (no errors).

**Assign clips in the Inspector** on the `[MiniGolf] AudioManager` GameObject:
| Slot | Triggered when |
|---|---|
| `shootClip` | Ball is fired |
| `goodHoleClip` | Ball enters blue hole |
| `badHoleClip` | Ball enters red hole |
| `missClip` | Ball stops without entering a hole |
| `warningClip` | Hole starts its pre-change flash |
| `gameOverClip` | Timer expires |
| `countdownClip` | (Reserved — not yet triggered by code) |

Free audio assets from the Unity Asset Store can be dropped into these slots.

---

## Ball Physics Settings

Applied to `[MiniGolf] Ball` Rigidbody2D:
- `gravityScale = 0` (top-down, no gravity)
- `linearDamping = 2.0` (ball slows down naturally)
- `angularDamping = 1.0`
- `collisionDetectionMode = Continuous`
- `constraints = FreezeRotation`
- `PhysicsMaterial2D`: bounciness=0.3, friction=0.1

Walls use: bounciness=0.3, friction=0 → slight bounce off walls.

---

## Potential Issues / Things to Know

1. **"Ball" tag must exist** — If holes don't react to the ball, verify the ball GameObject has the `Ball` tag.

2. **GameOverPanel starts disabled** — This is intentional. `GameOverPanel.Awake()` only runs the first time `Show()` is called (Unity deferred Awake). The button listener is registered at that point.

3. **HoleManager.RepositionHoles() checks `_positionsLocked`** — If holes don't move after a shot, check that `UnlockPositions()` was called (it's called in `HandleShotFired`).

4. **No score system** — Per the spec, only time is tracked.

5. **Input works on mobile** — `Pointer.current` unifies Mouse and Touchscreen. Both are handled by the same `Update()` code in `BallController`.

6. **If trajectory doesn't appear** — Ensure `_dotPrefab` is set on `TrajectoryRenderer`.

---

## What Could Be Added Next (not yet implemented)

- **Score counter** (e.g. count of successful shots)
- **Sound effects** (assign AudioClips to AudioManager slots)
- **Background music** (bgmSource AudioSource is wired, just needs a clip + `bgmSource.Play()`)
- **Screen shake** on bad hole or miss
- **DOTween** for smoother hole repositioning animation
- **Obstacles** on the course (static BoxCollider2D blocks)
- **Multiple ball types** or power-ups
- **High score persistence** via `PlayerPrefs`
