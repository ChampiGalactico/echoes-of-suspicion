# Puzzle System — Echoes of Suspicion

## Architecture

Everything is a **Puzzle**. One universal component handles both leaf and parent roles.

```
Puzzle "Car Repair" (CompletionRule: InOrder)
├── Puzzle "Step 1: Wrench" + PuzzleInteractable (ToolUse)
├── Puzzle "Step 2: Gasoline" + PuzzleInteractable (ToolUse)
└── Puzzle "Step 3: Spark Plug" + PuzzleInteractable (ToolUse)
```

A **Puzzle** validates. A **PuzzleInteractable** captures player input and feeds it to the Puzzle.

---

## Components

### Core

| Class | File | Description |
|---|---|---|
| `Puzzle` | `Core/Puzzle.cs` | Universal puzzle node. Can be leaf (validates input), parent (combines children), or both. Has built-in feedback (VFX, sound, light), health impact, freeze/unfreeze, and retry/reset. |
| `PuzzleInteractable` | `Core/PuzzleInteractable.cs` | Optional companion (extends `RatInteractable`). Modes: ToolUse, SlotPlace, Toggle, Keypad, Dial. Captures player input and calls `Puzzle.SubmitValue()`. |
| `PuzzleDoor` | `Core/PuzzleDoor.cs` | Listens to any `IPuzzleNode` and unlocks when solved. |
| `PuzzleEvents` | `Core/PuzzleEvents.cs` | Static event bus. Connects puzzles to creature (`OnNoiseGenerated`) and guide health (`OnGuideHealthPenalty`). |
| `PuzzleValidation` | `Core/PuzzleValidation.cs` | Pure comparison functions (no Unity/Mirror dependencies). |
| `IPuzzleNode` | `Core/IPuzzleNode.cs` | Interface: `NodeId`, `IsSolved`, `OnSolved`. Used by PuzzleDoor. |

### Data

| Asset | File | Description |
|---|---|---|
| `PuzzleAnswer` | `Data/PuzzleAnswer.cs` | ScriptableObject defining the correct answer. Has ValidationType and expected values. |
| `PuzzleItemData` | `Data/PuzzleItemData.cs` | ScriptableObject with item data: `ItemId`, `DisplayName`, `ItemTag`, `NumericValue`, `Icon`. |

### Items

| Class | File | Description |
|---|---|---|
| `PickableItem` | `Actors/PickableItem.cs` | Companion to `NetworkPickupItem`. Holds `PuzzleItemData`. |

---

## Puzzle Fields

**Leaf puzzle** (receives direct input):
- `_answer`: PuzzleAnswer ScriptableObject
- `_healthImpact`: negative = damage runner, positive = heal
- `_useDelay`: seconds to wait before validating (for animation/sound)
- `_allowRetry` / `_resetDelay`: retry behavior
- Feedback: `_successVFX`, `_failVFX`, `_successSound`, `_failSound`, `_explosionSound`, `_redLight`
- Events: `OnPuzzleSolved`, `OnPuzzleFailed`, `OnPuzzleReset`

**Parent puzzle** (combines children):
- `_children`: child Puzzle array
- `_completionRule`: All, InOrder, Any, NOfM
- `_answer` (optional): for value-level validation of children's submitted values

**Hierarchy:**
- `_parent`: reference to parent Puzzle (leave empty for root)

---

## Validation Types (PuzzleAnswer)

| Type | What it checks | Example |
|---|---|---|
| `Matches` | One value matches exactly | "Is this the correct wrench?" |
| `SumEquals` | Sum of numeric values = target | "Do these 3 items cost $18,500 total?" |
| `SequenceMatches` | Values match in order | "Are the 4 switches in the right sequence?" |
| `InRange` | Float between min and max | "Is the dial between 97.0 and 97.5?" |
| `TimeWindow` | Action within a time window | "Did you do it in the first 30 seconds?" |
| `ContinuousGuard` | Fails on "true" value | "Navigate without stepping on any trap." |

## Completion Rules (Parent Puzzles)

| Rule | Condition |
|---|---|
| `All` | All children solved (any order) |
| `InOrder` | All solved in array order. Wrong order = child gets forced failure. |
| `Any` | Any one child solved |
| `NOfM` | At least N children solved |

---

## PuzzleInteractable Modes

| Mode | Behavior |
|---|---|
| `ToolUse` | Tool stays in player's hand. Sound plays. Puzzle validates. |
| `SlotPlace` | Item removed from inventory, snapped to point. Puzzle validates. |
| `Toggle` | Press E to flip boolean state. |
| `Keypad` | (Future) Enter a code via UI. |
| `Dial` | (Future) Rotate to a numeric value. |

Fields: `_mode`, `_acceptedTags` (item filter), `_snapPoint` (SlotPlace), `_useSound`.

---

## How It Connects to Inventory

1. Player picks up item with `NetworkPickupItem` → hidden, stored in inventory.
2. If item has `PickableItem`, inventory slot is marked `isPuzzle = true`.
3. Player looks at `PuzzleInteractable` and presses E.
4. PuzzleInteractable checks `_acceptedTags` against the item's `ItemTag`.
5. For ToolUse: item stays in hand, `SubmitValue(ItemId, NumericValue)` sent to Puzzle.
6. For SlotPlace: item placed at snap point, removed from inventory, value submitted.
7. Puzzle validates against its PuzzleAnswer → success or failure.

---

## Example: Car Repair Puzzle (InOrder, ToolUse)

### Step 1 — Create PuzzleAnswer assets

For each step, create a PuzzleAnswer (Create > EOS > Puzzles > PuzzleAnswer):
- Step 1: Type = `Matches`, ExpectedValues[0] = `"wrench"`
- Step 2: Type = `Matches`, ExpectedValues[0] = `"gasoline"`
- Step 3: Type = `Matches`, ExpectedValues[0] = `"sparkplug"`

### Step 2 — Create child puzzles (one per repair slot)

For each slot on the car, create a GameObject:
- `NetworkIdentity`
- `Puzzle`:
  - `_nodeId`: `"car_step_1"` (etc.)
  - `_answer`: the matching PuzzleAnswer
  - `_parent`: the parent Car puzzle (set in Step 3)
  - `_healthImpact`: `-33`
  - `_useDelay`: `2`
  - `_successVFX` / `_failVFX`: assign particle systems
  - `_successSound` / `_failSound`: assign audio clips
- `PuzzleInteractable`:
  - `_mode`: `ToolUse`
  - `_acceptedTags`: `["Tool"]` (or empty for any)
- `Collider` on Interactable layer

### Step 3 — Create parent puzzle

Create a GameObject "CarPuzzle":
- `NetworkIdentity`
- `Puzzle`:
  - `_nodeId`: `"car_repair"`
  - `_children`: drag all 3 child puzzles **in order**
  - `_completionRule`: `InOrder`
  - (no `_answer` needed — just uses CompletionRule)

### Step 4 — Connect the door

- `PuzzleDoor`:
  - `_nodeRef`: drag the "CarPuzzle" (the parent Puzzle)
  - `OnDoorOpened`: connect door animation

### Result

- Right tool, right order → success VFX, reports to parent, advances.
- Right tool, wrong order → parent rejects, fail VFX + damage.
- Wrong tool → local validation fails, fail VFX + damage.
- All 3 done in order → parent solves → door opens.
