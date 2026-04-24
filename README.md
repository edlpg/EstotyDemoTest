# MiniGolf

A fast-paced 2D mobile mini golf game built in Unity. Shoot the ball into holes before the timer runs out — blue holes add time, red holes take it away.

---

## Requirements

- **Unity** 6000.4.3f1 or newer
- **Render Pipeline** Universal Render Pipeline (URP)
- **Input System** Unity New Input System (activeInputHandler: Both or InputSystem)
- **TextMeshPro** (included via Package Manager)

---

## Project Setup

1. Clone the repository
2. Open the project in Unity 6000.4.3f1
3. Open `Assets/Scenes/MainScene.unity`
4. In the top menu run **MiniGolf → Setup Scene** to rebuild all game objects
5. Wire the Inspector references on `[MiniGolf] GameManager` (see [Inspector Wiring](#inspector-wiring) below)
6. Press **Play**

---

## How to Play

| Action | Input |
|--------|-------|
| Aim | Hold finger / mouse button anywhere on screen |
| Adjust direction | Drag left / right while holding |
| Shoot | Release |
| Restart | Tap **Restart** on the Game Over screen |

- **Blue hole** → +5 seconds
- **Red hole** → −10 seconds
- Holes flash before changing type — watch for the warning ring
- Holes reposition after every shot

---

## Inspector Wiring

After running **MiniGolf → Setup Scene**, assign the following serialized fields:

### `[MiniGolf] GameManager` → `GameManager`
| Field | Assign |
|-------|--------|
| Config | `Assets/MiniGolf/Config/GameConfig.asset` |
| Timer Service | `[MiniGolf] GameManager` → `TimerService` component |
| Ball Controller | `[MiniGolf] Ball` → `BallController` component |
| Hole Manager | `[MiniGolf] Holes` → `HoleManager` component |
| Timer Display | `UI/TimerPanel` → `TimerDisplay` component |
| Game Over Panel | `UI/GameOverPanel` → `GameOverPanel` component |
| Score Text | Your in-game score `TMP_Text` object |

### `[MiniGolf] Ball` → `BallController`
| Field | Assign |
|-------|--------|
| Trajectory Renderer | `[MiniGolf] Trajectory` → `TrajectoryRenderer` component |
| Sprite Renderer | `[MiniGolf] Ball` → `SpriteRenderer` component |

### `[MiniGolf] Holes` → `HoleManager`
| Field | Assign |
|-------|--------|
| Hole Prefab | Your hole prefab (must have `HoleController`, `CircleCollider2D` set as trigger, `SpriteRenderer`, child cavity `SpriteRenderer`, child `ParticleSystem`) |

### `UI/TimerPanel` → `TimerDisplay`
| Field | Assign |
|-------|--------|
| Timer Text | Timer countdown `TMP_Text` child |
| Bonus Text | Bonus popup `TMP_Text` child |

### `UI/GameOverPanel` → `GameOverPanel`
| Field | Assign |
|-------|--------|
| Panel Root | `GameOverPanel` GameObject itself |
| Game Over Text | Game over `TMP_Text` child |
| Restart Button | `Button` child |

### `[MiniGolf] AudioManager` → `AudioManager`
All audio clips are optional — the game runs silently if none are assigned.

| Field | Description |
|-------|-------------|
| Shoot Clip | Ball launch sound |
| Good Hole Clip | Blue hole reward sound |
| Bad Hole Clip | Red hole penalty sound |
| Miss Clip | Ball stopped without entering a hole |
| Warning Clip | Hole about to change type |
| Game Over Clip | Timer reached zero |
| Countdown Clip | Tick sound for the last 5 seconds |

---

## Game Configuration

All tunable values live in `Assets/MiniGolf/Config/GameConfig.asset` and can be edited in the Inspector at runtime.

| Setting | Default | Description |
|---------|---------|-------------|
| Initial Time | 30s | Starting countdown |
| Good Hole Time Bonus | +5s | Time added for blue hole |
| Bad Hole Time Penalty | −10s | Time removed for red hole |
| Max Shoot Force | 18 | Maximum ball impulse |
| Max Drag Distance | 2.5 | World units of drag for full power |
| Ball Stop Threshold | 0.05 | Speed below which ball is considered stopped |
| Ball Reset Delay | 0.3s | Pause after shot outcome before holes reposition |
| Hole Reposition Delay | 0.04s | Pause between hole reposition and ball respawn |
| Hole Count | 4 | Number of simultaneous holes |
| Hole Change Min Interval | 4s | Minimum time before a hole changes type |
| Hole Change Max Interval | 9s | Maximum time before a hole changes type |
| Hole Flash Warning Duration | 2s | How long the warning ring flashes before a type change |

---

## Code Architecture

The codebase follows **SOLID** principles. `GameManager` is a pure orchestrator — it owns the state machine and wires subsystems via C# events. No subsystem talks directly to another.

```
Assets/MiniGolf/
├── Config/
│   └── GameConfig.cs           ScriptableObject — all tunable values
├── Editor/
│   └── SceneSetup.cs           MenuItem "MiniGolf/Setup Scene"
└── Scripts/
    ├── Core/
    │   ├── GameManager.cs      State machine + event wiring
    │   └── GameState.cs        Idle | Aiming | InFlight | Resolving | GameOver
    ├── Services/
    │   ├── ITimerService.cs    Timer interface (Dependency Inversion)
    │   └── TimerService.cs     Countdown implementation
    ├── Ball/
    │   ├── BallController.cs   Input, physics, shot lifecycle events
    │   └── TrajectoryRenderer.cs  Pooled dot-sprite aim line
    ├── Hole/
    │   ├── HoleController.cs   Single hole — type, flash warning, collision
    │   ├── HoleManager.cs      Spawning, repositioning, event relay
    │   └── HoleType.cs         Good | Bad enum
    ├── UI/
    │   ├── TimerDisplay.cs     Countdown label + bonus popup animation
    │   └── GameOverPanel.cs    End screen with score and restart button
    └── Audio/
        └── AudioManager.cs     Singleton, null-safe PlayXxx() methods
```

### State Machine

```
Idle ──[press down]──► Aiming ──[release]──► InFlight
 ▲                                               │
 │                                    [hole] or [stopped]
 │                                               ▼
 └──────────────────────────────────── Resolving
                                                 │
                                         [timer = 0]
                                                 ▼
                                            GameOver
```

---

## Assets Used

- **TextMeshPro** — UI text rendering (Unity built-in)
- Free audio/VFX assets from the Unity Asset Store can be dropped into `AudioManager` inspector slots without any code changes
