# Sistema de Puzzles — Echoes of Suspicion

## Arquitectura general

El sistema usa un patrón de **árbol** con dos tipos de nodo que comparten la misma interfaz (`IPuzzleNode`):

```
CompositePuzzle "Bioma Cocina" (Rule: All)
├── LeafPuzzle "Colocar ingredientes" (Matches)
│   ├── SlotActor (recibe item A)
│   └── SlotActor (recibe item B)
├── CompositePuzzle "Reparar motor" (InOrder)
│   ├── LeafPuzzle "Paso 1: activar válvula" (Matches)
│   │   └── ToggleActor
│   └── LeafPuzzle "Paso 2: ajustar presión" (InRange)
│       └── DialActor
└── LeafPuzzle "Sintonizar radio" (InRange)
    └── DialActor
```

Esto permite anidar puzzles dentro de puzzles sin límite. Un `CompositePuzzle` puede contener `LeafPuzzles` u otros `CompositePuzzles`.

---

## Piezas del sistema

### Interfaces

| Interfaz | Archivo | Qué hace |
|---|---|---|
| `IPuzzleNode` | `Core/IPuzzleNode.cs` | Contrato compartido entre LeafPuzzle y CompositePuzzle. Expone `NodeId`, `IsSolved` y el evento `OnSolved`. |
| `IPuzzleActor` | `Core/IPuzzleActor.cs` | Contrato para objetos interactuables. Expone `ActorId`, `CanInteract`, `GetValue()` y `OnValueChanged`. |

### Nodos (Core)

| Clase | Archivo | Descripción |
|---|---|---|
| `LeafPuzzle` | `Core/LeafPuzzle.cs` | Puzzle simple. Lee los valores de sus actores y los valida contra un `PuzzleAnswer`. |
| `CompositePuzzle` | `Core/CompositePuzzle.cs` | Puzzle compuesto. No tiene actores propios; combina el estado `IsSolved` de sus hijos con una regla. |
| `PuzzleDoor` | `Core/PuzzleDoor.cs` | Escucha a cualquier `IPuzzleNode` (leaf o composite) y se desbloquea cuando se resuelve. |
| `PuzzleActorBase` | `Core/PuzzleActorBase.cs` | Clase base abstracta de todos los actores. Maneja identidad, estado de interacción y propagación de cambios por red. |
| `PuzzleEvents` | `Core/PuzzleEvents.cs` | Bus de eventos estático. Conecta puzzles con la criatura (`OnNoiseGenerated`) y la vida del Guía (`OnGuideHealthPenalty`). |
| `PuzzleValidation` | `Core/PuzzleValidation.cs` | Funciones de comparación puras (sin dependencias de Unity ni Mirror). |

### Actores (Actors)

Todos extienden `PuzzleActorBase` y por lo tanto implementan `IPuzzleActor`.

| Actor | Valor que expone | Uso típico |
|---|---|---|
| `SlotActor` | `PuzzleItemData` del item colocado (o `null`) | Recibir un objeto del inventario: llave, herramienta, ingrediente. |
| `ToggleActor` | `bool` | Palanca, botón, interruptor, trampa de presión. |
| `DialActor` | `float` | Perilla, válvula, sintonizador de frecuencia. |
| `PatternActor` | `string` (secuencia acumulada) | Teclado numérico, secuencia de botones. |

Componentes auxiliares de los actores:

| Componente | Archivo | Descripción |
|---|---|---|
| `SlotActorInteractable` | `Actors/SlotActorInteractable.cs` | Companion de SlotActor. Lo conecta al sistema de raycast (`RatInteractable`) para que el jugador pueda colocar items con E. |
| `PickableItem` | `Actors/PickableItem.cs` | Companion de `NetworkPickupItem`. Solo guarda `PuzzleItemData`. Se agrega a items que participan en puzzles. |

### Datos (Data)

| Asset | Archivo | Descripción |
|---|---|---|
| `PuzzleAnswer` | `Data/PuzzleAnswer.cs` | ScriptableObject que define la respuesta correcta de un LeafPuzzle. Contiene el tipo de validación y los valores esperados. |
| `PuzzleItemData` | `Data/PuzzleItemData.cs` | ScriptableObject con los datos de un item de puzzle: `ItemId`, `DisplayName`, `ItemTag`, `NumericValue`, `Icon`. |

### Tipos de validación

Se configuran en el `PuzzleAnswer`. El `LeafPuzzle` usa `PuzzleValidation` para evaluarlos.

| Tipo | Qué compara | Ejemplo |
|---|---|---|
| `Matches` | Un valor exacto contra un string esperado | "¿El SlotActor tiene el destornillador correcto?" |
| `SumEquals` | La suma de varios valores numéricos = objetivo | "¿Los precios de los 3 productos suman $18,500?" |
| `SequenceMatches` | Array de valores en orden exacto | "¿Los 4 switches están en la secuencia correcta?" |
| `InRange` | Un float entre min y max | "¿La frecuencia del dial está entre 97.0 y 97.5?" |
| `TimeWindow` | Una acción ocurre dentro de una ventana de tiempo | "¿Lo hiciste en los primeros 30 segundos?" |
| `ContinuousGuard` | Falla apenas algún actor entra en estado malo | "Navega sin pisar ninguna trampa" (no espera confirmación). |

### Reglas de combinación (CompositePuzzle)

| Regla | Condición para resolverse |
|---|---|
| `All` | Todos los hijos resueltos (cualquier orden) |
| `InOrder` | Todos resueltos, pero en el orden en que aparecen en la lista. Los hijos que "no les toca" se desactivan automáticamente. |
| `Any` | Basta con que uno se resuelva |
| `NOfM` | Al menos N de M hijos resueltos |

---

## Cómo se conecta con el inventario

1. El jugador recoge un item con `NetworkPickupItem` → el objeto se oculta y se guarda en el inventario.
2. Si el item tiene un `PickableItem` companion, el slot del inventario se marca como `isPuzzle = true`.
3. El jugador mira un `SlotActorInteractable` y presiona E → el sistema verifica que el slot activo sea de puzzle.
4. `SlotActor.TryPlace()` valida el `ItemTag` del `PickableItem` contra sus tags aceptados.
5. Si pasa, el item se coloca (se mueve al snap point, sigue oculto) y se remueve del inventario.
6. `SlotActor` llama `RaiseValueChanged()` → el `LeafPuzzle` que lo escucha re-evalúa su `PuzzleAnswer`.

---

## Ejemplo 1: Puzzle simple (LeafPuzzle solo)

**Escenario:** Una puerta se abre cuando el jugador coloca la llave correcta en una cerradura.

### Paso 1 — Crear los datos

1. **PuzzleItemData** (Create > EOS > Puzzles > PuzzleItemData):
   - `ItemId`: `"llave_cocina"`
   - `ItemTag`: `"Key"`
   - `DisplayName`: `"Llave de la cocina"`

2. **ItemData** (Create > Echoes > Inventory > Item Data):
   - `itemName`: `"Llave de la cocina"`
   - `worldPrefab`: el prefab del modelo 3D de la llave

3. **PuzzleAnswer** (Create > EOS > Puzzles > PuzzleAnswer):
   - `Type`: `Matches`
   - `ExpectedValues[0]`: `"llave_cocina"` (debe coincidir con el `ItemId` del PuzzleItemData)

### Paso 2 — Configurar el prefab de la llave

En el prefab de la llave, agregar:
- `NetworkIdentity`
- `NetworkTransform`
- `Rigidbody`
- `Collider`
- `NetworkPickupItem` → asignar el **ItemData**
- `PickableItem` → asignar el **PuzzleItemData**

Registrar el **ItemData** en el `ItemRegistry` de la escena.

### Paso 3 — Configurar la cerradura (SlotActor)

Crear un GameObject "Cerradura" en la escena:
- `NetworkIdentity`
- `SlotActor`:
  - `_snapPoint`: un Transform hijo donde se posiciona la llave visualmente
  - `_acceptedTags`: `["Key"]`
- `SlotActorInteractable` (se agrega automáticamente por el RequireComponent)
- Un `Collider` para que el raycast del jugador lo detecte

### Paso 4 — Configurar el LeafPuzzle

Crear un GameObject vacío "Puzzle_Cerradura":
- `NetworkIdentity`
- `LeafPuzzle`:
  - `_nodeId`: `"puzzle_cerradura_cocina"`
  - `_actorRefs`: arrastrar el GameObject "Cerradura" (su SlotActor)
  - `_answer`: asignar el PuzzleAnswer creado en el paso 1

### Paso 5 — Configurar la puerta

En el GameObject de la puerta:
- `PuzzleDoor`:
  - `_nodeRef`: arrastrar el GameObject "Puzzle_Cerradura" (su LeafPuzzle)
  - `OnDoorOpened`: conectar la animación de abrir o el método que desbloquea la puerta

### Resultado

Jugador recoge llave → llave se oculta, entra al inventario → jugador mira la cerradura → presiona E → SlotActor valida tag "Key" → acepta → LeafPuzzle evalúa Matches("llave_cocina") → resuelto → PuzzleDoor se desbloquea.

---

## Ejemplo 2: Puzzle compuesto (dos sub-puzzles en orden)

**Escenario:** Para abrir la puerta del laboratorio, el jugador debe primero activar la electricidad (toggle) y luego colocar la tarjeta de acceso (slot). Debe hacerse en ese orden.

### Paso 1 — Crear los datos

1. **PuzzleItemData** para la tarjeta:
   - `ItemId`: `"tarjeta_lab"`
   - `ItemTag`: `"AccessCard"`

2. **ItemData** para la tarjeta (igual que en el ejemplo anterior).

3. **PuzzleAnswer A** — para el toggle de electricidad:
   - `Type`: `Matches`
   - `ExpectedValues[0]`: `"True"` (el ToggleActor expone un bool, se compara como string)

4. **PuzzleAnswer B** — para el slot de la tarjeta:
   - `Type`: `Matches`
   - `ExpectedValues[0]`: `"tarjeta_lab"`

### Paso 2 — Configurar los actores en la escena

**Interruptor eléctrico:**
- `NetworkIdentity`
- `ToggleActor`:
  - `_actorId`: `"switch_lab"`
- Un `Collider` + algún script que llame `toggleActor.Interact()` al presionar E (o conectarlo via `RatInteractable`)

**Lector de tarjetas:**
- `NetworkIdentity`
- `SlotActor`:
  - `_acceptedTags`: `["AccessCard"]`
  - `_snapPoint`: donde aparece la tarjeta visualmente
- `SlotActorInteractable`
- `Collider`

### Paso 3 — Crear los dos LeafPuzzles

**LeafPuzzle A** — "Activar electricidad":
- `_nodeId`: `"puzzle_switch_lab"`
- `_actorRefs`: arrastrar el ToggleActor
- `_answer`: PuzzleAnswer A

**LeafPuzzle B** — "Insertar tarjeta":
- `_nodeId`: `"puzzle_tarjeta_lab"`
- `_actorRefs`: arrastrar el SlotActor
- `_answer`: PuzzleAnswer B

### Paso 4 — Crear el CompositePuzzle

Crear un GameObject vacío "Puzzle_Laboratorio":
- `NetworkIdentity`
- `CompositePuzzle`:
  - `_nodeId`: `"puzzle_lab_completo"`
  - `_childRefs`: arrastrar **en orden** → [LeafPuzzle A, LeafPuzzle B]
  - `_rule`: `InOrder`

### Paso 5 — Configurar la puerta

- `PuzzleDoor`:
  - `_nodeRef`: arrastrar "Puzzle_Laboratorio" (el CompositePuzzle)

### Resultado

El CompositePuzzle con regla `InOrder` automáticamente **desactiva** el LeafPuzzle B hasta que A se resuelva. Así el jugador no puede insertar la tarjeta antes de activar la electricidad.

1. Jugador activa el interruptor → LeafPuzzle A resuelto
2. CompositePuzzle desbloquea LeafPuzzle B
3. Jugador inserta la tarjeta → LeafPuzzle B resuelto
4. CompositePuzzle evalúa: todos resueltos en orden → resuelto
5. PuzzleDoor se desbloquea

---

## Notas importantes

- Los actores **no saben** en qué puzzle están. Solo exponen su valor y avisan cuando cambia. Esto permite reusar el mismo tipo de actor en cualquier puzzle.
- Los puzzles **no saben** qué tipo de actor usan. Solo llaman `GetValue()`. Un `LeafPuzzle` con validación `Matches` funciona igual con un `SlotActor`, un `ToggleActor` o cualquier actor futuro.
- `PuzzleEvents` conecta los puzzles con sistemas externos (criatura, vida del Guía) sin acoplamiento directo. Los puzzles generan ruido al fallar; la criatura escucha ese ruido.
- Los `_actorRefs` y `_childRefs` se arrastran como `MonoBehaviour[]` en el Inspector porque Unity no puede serializar interfaces. El sistema los convierte a `IPuzzleActor` / `IPuzzleNode` en `OnStartServer()`.
- Si un actor arrastrado no implementa la interfaz esperada, se ignora silenciosamente.
