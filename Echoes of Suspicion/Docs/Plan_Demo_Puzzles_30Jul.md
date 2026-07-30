# Plan de Implementación — Demo: Puzzles y Tensión
*Jueves 30 de julio de 2026*

> **Objetivo del día:** Implementar 2 puzzles nuevos, el sistema de latidos progresivos, y el evento final (jumpscare) para cerrar la demo con 3 puzzles totales.

---

## Resumen de la demo

**Flujo del jugador:**
```
Garaje (Puzzle 1: Reparar carro) 
  → Pasillo de lava (Puzzle 2: Código Morse sonoro)
    → Casa de Carlos (Puzzle 3: Recibos por fax)
      → Puerta azul se abre → Jumpscare → "Continuará..."
```

**Latidos del corazón:** Suben progresivamente con cada puzzle resuelto. En el puzzle 3, laten extremadamente rápido.

---

## Sistema 1: Latidos Progresivos por Puzzle (DemoHeartbeatManager)

### Concepto
Un sistema **independiente** del heartbeat por proximidad a criaturas (`RunnerCreatureAwareness`). Este heartbeat de "tensión ambiental" sube con cada puzzle resuelto, creando una sensación de que algo malo se acerca sin que el jugador entienda por qué.

### Script: `DemoHeartbeatManager.cs`
- **Ubicación:** `Assets/_Project/Scripts/Audio/DemoHeartbeatManager.cs`
- **Tipo:** `NetworkBehaviour` (vive en el servidor, envía `TargetRpc` al Runner)
- **Responsabilidad:** Escucha `Puzzle.OnPuzzleSolved` de cada puzzle en la escena y ajusta la frecuencia de latidos.

### Parámetros configurables (SerializeField)
| Campo | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `heartbeatClip` | AudioClip | — | Mismo clip "tun" que usa HeartbeatAudioFeedback |
| `volume` | float [0-1] | 0.5 | Volumen base |
| `puzzleHeartbeatIntervals` | float[] | {3.0, 1.5, 0.6} | Intervalo entre latidos después de completar puzzle 1, 2, 3 |
| `finalPanicInterval` | float | 0.3 | Intervalo durante el evento final (puerta abriéndose) |
| `puzzleNodes` | Puzzle[] | — | Referencias a los 3 puzzles de la demo (arrastrar en Inspector) |

### Lógica
1. **Estado inicial:** Sin latidos ambientales (solo los de criatura si aplica).
2. **Puzzle 1 completado:** Latidos suaves cada 3 segundos. Apenas perceptibles.
3. **Puzzle 2 completado:** Latidos cada 1.5 segundos. El jugador los nota.
4. **Puzzle 3 completado:** Latidos cada 0.6 segundos. Claramente algo va mal.
5. **Evento final (puerta abriéndose):** Latidos a 0.3 segundos. Pánico total.

### Interacción con HeartbeatAudioFeedback existente
- **No se tocan.** Son sistemas paralelos. El heartbeat de criatura suena cuando hay una criatura cerca. El heartbeat de demo suena según progresión de puzzles.
- Si ambos coinciden, el jugador escucha ambos (el de criatura es por percepción, el de demo es por tensión narrativa). Usar un clip ligeramente diferente o pitch distinto para distinguirlos.

### Integración
- El `DemoHeartbeatManager` se suscribe al evento `OnPuzzleSolved` (UnityEvent) de cada `Puzzle` referenciado.
- Solo reproduce audio en el cliente del Runner (usa `TargetRpc` al `connectionToClient` del Runner, igual que `RunnerCreatureAwareness`).
- Cuando el `DemoFinalEventManager` (ver Sistema 4) dispara el evento final, le avisa al `DemoHeartbeatManager` para cambiar al intervalo de pánico.

---

## Sistema 2: Puzzle 2 — Código Morse en el Pasillo de Lava

### Narrativa
El pasillo representa lo difícil que es para Carlos llegar a su casa. El camino está lleno de peligros, y para avanzar el Runner debe descifrar señales sonoras que el entorno emite — como si la simulación estuviera intentando comunicarse.

### Mecánica

**Runner (en el pasillo de lava):**
1. Al entrar al pasillo, se activan emisores de sonido en las paredes (AudioSources posicionales).
2. Cada emisor reproduce un patrón de morse (puntos y rayas con sonido).
3. El Runner escucha el patrón y lo describe al Guide por voz: "Tres cortos, uno largo, dos cortos..."
4. En las paredes hay secciones interactuables (paneles, piedras, símbolos). El Runner debe activar las secciones correctas en el orden que el Guide le indique.
5. Activar la sección correcta → avance. Activar la incorrecta → daño + ruido + reset del paso actual.

**Guide (en su cuarto):**
1. En su terminal/notas tiene una **tabla de decodificación Morse** que mapea patrones a letras/símbolos.
2. El Runner le describe lo que escucha.
3. El Guide decodifica: "Eso es la letra A... la sección A está a tu izquierda."
4. Le indica al Runner qué sección activar.

### Implementación técnica

#### Nuevos scripts:

**`MorsePuzzleEmitter.cs`** — `MonoBehaviour`
- Tiene un `AudioSource` con un patrón de morse configurable.
- Campo `string morsePattern` (ej: "... - .." para "STI").
- Reproduce el patrón en loop con sonido posicional (el Runner lo escucha al acercarse).
- Campo `float dotDuration = 0.15f`, `float dashDuration = 0.45f`, `float pauseDuration = 0.1f`.

**`MorsePuzzleController.cs`** — hereda de `Puzzle` (o usa composición con el Puzzle existente)
- Contiene la secuencia completa de morse que debe resolverse.
- 3 rondas (3 patrones distintos), cada una con su emisor y su sección correcta.
- Usa el `CompletionRule.InOrder` del sistema de Puzzle existente (los 3 sub-puzzles deben resolverse en orden).

**Estructura en la escena:**
```
MorsePuzzle_Root (Puzzle - CompletionRule: InOrder)
├── MorseEmitter_1 (AudioSource + MorsePuzzleEmitter)
│   └── WallSection_A (PuzzleInteractable → Puzzle hijo 1)
├── MorseEmitter_2 (AudioSource + MorsePuzzleEmitter)  
│   └── WallSection_B (PuzzleInteractable → Puzzle hijo 2)
└── MorseEmitter_3 (AudioSource + MorsePuzzleEmitter)
    └── WallSection_C (PuzzleInteractable → Puzzle hijo 3)
```

**Para el Guide — contenido necesario:**
- **Documento/Nota: "Tabla de Códigos Morse"** — Un `GuideFolderData` / `ReadableData` que contiene la tabla de morse. El Guide la tiene en una carpeta que escanea en su terminal, o como nota física en su cuarto.
- Contenido ejemplo:
  ```
  SISTEMA DE DECODIFICACIÓN — CLASIFICADO
  
  ·         = E          — · ·     = D
  — —       = M          · · ·     = S  
  · —       = A          — · · ·   = B
  · · — ·   = F          — — ·     = G
  [etc.]
  
  INSTRUCCIONES: Los emisores del corredor reproducen
  secuencias. Decodifique la letra y comunique la sección
  correspondiente.
  ```

#### Prefabs necesarios:
- Emisores de sonido (pueden ser cristales, grietas en la pared, o speakers oxidados que encajen con la estética de lava).
- Secciones de pared interactuables (paneles, símbolos tallados, etc.).
- VFX de éxito/error por sección.

#### Sonidos necesarios:
- Tono corto (dot): beep agudo, ~0.15s
- Tono largo (dash): beep agudo, ~0.45s
- SFX de sección correcta
- SFX de sección incorrecta

---

## Sistema 3: Puzzle 3 — Las Deudas de Carlos (Recibos por Fax)

### Narrativa
La casa de Carlos está llena de deudas impagas. Para abrir la puerta azul (la salida), hay que poner la vida de Carlos en orden — pagar todos los recibos pendientes. Es una metáfora: Carlos nunca pudo con sus responsabilidades, y ahora el Runner tiene que resolverlas por él con la ayuda del Guide.

### Mecánica

**Runner (en la casa):**
1. Hay recibos físicos esparcidos por la casa (como `PickableItem` / `WorldItem`): recibo de luz, agua, renta, matrícula escolar de los hijos, etc.
2. Hay una **máquina de fax** en la casa (un interactable fijo, tipo `FolderScannerDock` pero para el Runner).
3. El Guide le dice cuál recibo buscar: "¡Busca el recibo del agua!"
4. El Runner corre por la casa, lo encuentra, lo recoge, corre al fax, lo inserta.
5. El fax lo "envía" al Guide.
6. Siguiente recibo. Cada ronda con menos tiempo.

**Guide (en su cuarto):**
1. En su terminal aparece el listado de deudas pendientes con el orden en que deben pagarse.
2. Le dice al Runner cuál buscar primero.
3. Cuando el Runner envía el recibo por fax, el Guide lo recibe en una **máquina receptora de fax** nueva en su cuarto (o en la terminal).
4. El Guide debe ir a la máquina receptora, recoger el documento recibido, y llevarlo a su terminal para escanearlo.
5. En la terminal aparece el recibo con un **botón "PAGAR"**. El Guide presiona pagar.
6. Confirmación → el Guide ve el siguiente recibo pendiente y le dice al Runner.

### Implementación técnica

#### Objetos de los recibos — Usar `DocumentData` existente

Cada recibo es un **`DocumentData` ScriptableObject** (el mismo que ya usan para documentos legibles). Se aprovecha todo el sistema de renderizado existente: secciones con tipo (Title, Body, Footer), dividers, imágenes, fuentes custom.

**Estructura de cada recibo como DocumentData:**
```
DocumentData: "Receipt_Water"
├── Section[0]: Type=Title,    Text="RECIBO DE AGUA POTABLE"
├── Section[1]: Type=Subtitle, Text="Empresa Municipal de Aguas", ShowDivider=true
├── Section[2]: Type=Body,     Text="Periodo: Ene-Mar 2024\nConsumo: 42m³\nMonto: $340.00\nEstado: VENCIDO — 3 meses de atraso"
├── Section[3]: Type=Footer,   Text="Código de pago: RCA-2024-0341", AnchorToBottom=true
└── ContentImage: (opcional, logo de la empresa de agua)
```

**Prefab del recibo en la casa:** Usa el **prefab de papel existente** con `ReadableInteractable` apuntando al `DocumentData` correspondiente. El Runner puede leerlo (E para ver el contenido renderizado con `ReadableUI`) y también recogerlo para llevarlo al fax.

> El prefab de papel necesita tener AMBOS componentes: `ReadableInteractable` (para leer) y ser recogible (para meter al fax). Se puede resolver con un `PickableItem` que además tenga una referencia al `DocumentData`, o con un script nuevo `ReceiptPickup` que combine ambas funciones.

#### Script nuevo: `ReceiptPickup.cs` — combina lectura + recogida

```
ReceiptPickup : RatInteractable
├── [SerializeField] DocumentData receiptDocument  ← el DocumentData del recibo
├── [SerializeField] string receiptId              ← identificador único ("water", "electric", etc.)
├── [SerializeField] PuzzleItemData puzzleItemData ← para el sistema de inventario existente
│
├── ServerInteract():
│   - Si el Runner NO tiene el recibo → lo recoge (igual que NetworkPickupItem)
│   - Si el Runner YA lo tiene y está mirando al fax → lo inserta
│
└── El DocumentData se usa para:
    - ReadableUI cuando el Runner lo examina
    - GuideTerminalView cuando el Guide lo escanea (vía GuideFolderData wrapper)
```

#### Nuevos scripts (Runner side):

**`FaxMachine.cs`** — hereda de `RatInteractable`
- Funciona similar a `FolderScannerDock` pero para el Runner.
- El Runner interactúa teniendo un recibo en su inventario → animación de envío (2-3 seg) → genera ruido → envía al servidor.
- El servidor notifica al Guide que llegó un fax.
- Al enviar, el servidor busca el `DocumentData` del recibo y lo asocia al fax del Guide.
- Campos: `AudioClip faxSendSound`, `float sendDuration = 2.5f`, `ParticleSystem sendVFX`.

#### Nuevos scripts (Guide side):

**`FaxReceiverDock.cs`** — hereda de `RatInteractable`
- Máquina receptora de fax en el cuarto del Guide.
- Cuando llega un fax, muestra indicador visual/sonoro (luz parpadeante, sonido de fax).
- El Guide interactúa → recibe el documento como `GuideFolderItem` en su inventario.
- El `GuideFolderData` del recibo envuelve el mismo `DocumentData` que tiene el Runner — así se renderiza idéntico en la terminal del Guide.
- Lo lleva al `FolderScannerDock` existente para escanearlo.

**Modificación: `GuideTerminalView.cs`** — agregar panel de pago
- Nuevo método `ShowPaymentScreen(string receiptName, float amount, string paymentCode)`.
- Cuando se escanea un recibo (detectado por `receiptId` o tag especial en el `GuideFolderData`), la terminal muestra el documento renderizado + un indicador de "PAGO PENDIENTE".
- El botón "PAGAR" es un `RatInteractable` físico al lado de la terminal.

**`BillPaymentButton.cs`** — hereda de `RatInteractable`
- Botón físico junto a la terminal del Guide.
- Solo interactuable cuando hay un recibo escaneado pendiente de pago.
- Al presionarlo: `[Command]` al servidor → valida que el recibo correcto esté escaneado → marca como pagado → `[ClientRpc]` confirmación.
- El `BillsPuzzleCoordinator` en el servidor recibe el evento y avanza el puzzle.

#### Estructura del puzzle:

Usar el sistema de `Puzzle` existente con `CompletionRule.InOrder`.

```
BillsPuzzle_Root (Puzzle - CompletionRule: InOrder, 4 hijos)
├── Bill_Water    (Puzzle hijo — se resuelve cuando Guide paga "agua")
├── Bill_Electric (Puzzle hijo — se resuelve cuando Guide paga "luz")
├── Bill_Rent     (Puzzle hijo — se resuelve cuando Guide paga "renta")
└── Bill_School   (Puzzle hijo — se resuelve cuando Guide paga "matrícula")
```

Un script coordinador `BillsPuzzleCoordinator.cs` (`NetworkBehaviour`) orquesta:
- Mantiene la cola de recibos pendientes.
- Expone al Guide cuál es el siguiente (vía `TargetRpc` o `SyncVar`).
- Cuando el Guide paga, valida que el `receiptId` del recibo escaneado coincida con el esperado y llama `SubmitValue` en el Puzzle hijo correspondiente.
- Controla la ventana de tiempo decreciente (SyncVar `float currentTimeLimit`).

#### DocumentData assets a crear (4 recibos):

**Receipt_Water.asset:**
- Title: "RECIBO DE AGUA POTABLE"
- Subtitle: "Empresa Municipal de Aguas" + divider
- Body: "Periodo: Ene–Mar 2024 · Consumo: 42m³ · Monto: $340.00 · Estado: VENCIDO"
- Footer (anchored): "Código: RCA-2024-0341"

**Receipt_Electric.asset:**
- Title: "AVISO DE CORTE — LUZ ELÉCTRICA"
- Subtitle: "Compañía Nacional de Energía" + divider
- Body: "Periodo: Feb–Abr 2024 · Consumo: 380kWh · Monto: $520.00 · ÚLTIMO AVISO ANTES DE CORTE"
- Footer (anchored): "Código: CNE-2024-1187"

**Receipt_Rent.asset:**
- Title: "NOTIFICACIÓN DE ADEUDO — ALQUILER"
- Subtitle: "Inmobiliaria Residencial del Valle" + divider
- Body: "Unidad: Depto 4B · Monto mensual: $1,200.00 · Meses pendientes: 2 · Total adeudado: $2,400.00"
- Footer (anchored): "Ref: IRV-DEPTO4B-2024"

**Receipt_School.asset:**
- Title: "MATRÍCULA ESCOLAR — PENDIENTE"
- Subtitle: "Colegio San Martín" + divider
- Body: "Alumno: Sofía Mendoza R. · Grado: 3° Primaria · Inscripción 2024: $850.00 · Fecha límite: VENCIDA"
- Footer (anchored): "Folio: CSM-INS-2024-0456"

#### Prefabs necesarios:
- 4 instancias del **prefab de papel existente** con `ReceiptPickup` + su `DocumentData` correspondiente, esparcidos por la casa.
- Máquina de fax del Runner (modelo simple con collider + `FaxMachine` script).
- Máquina receptora de fax del Guide (modelo simple con collider + `FaxReceiverDock` script).
- Botón de "PAGAR" físico junto a la terminal del Guide.

#### Sonidos necesarios:
- SFX fax enviando (brrr clásico de fax).
- SFX fax recibido (ding o alerta).
- SFX pago confirmado.
- SFX pago fallido / timeout.

---

## Sistema 4: Evento Final — Jumpscare (DemoFinalEventManager)

### Script: `DemoFinalEventManager.cs`
- **Ubicación:** `Assets/_Project/Scripts/Core/DemoFinalEventManager.cs`
- **Tipo:** `NetworkBehaviour`

### Flujo del evento final:
1. **Trigger:** El `BillsPuzzle_Root` (Puzzle 3) dispara `OnPuzzleSolved`.
2. `DemoFinalEventManager` escucha este evento.
3. **Latidos al máximo:** Notifica a `DemoHeartbeatManager` → intervalo de pánico (0.3s).
4. **Puertas se abren simultáneamente:** Puerta azul para el Runner + una puerta del cuarto del Guide.
5. **Spawn de criaturas (ambos lados):** Después de ~1 segundo, una `JumpscareCreature` aparece en cada puerta.
6. **Jumpscare simultáneo:** Cada criatura da 2 pasos con sonido de pisadas, luego lunge/ataque.
   - Usa `JumpscareCreature.cs` — modelo ligero con Animator, sin AI ni NavMesh.
   - Animaciones: walk (StateIndex 0) → chase (StateIndex 2) → Attack trigger.
7. **Fade a negro:** Después del lunge, panel UI negro con CanvasGroup fade en todos los clientes.
8. **Texto "Continuará...":** Después del fade, texto centrado con fade-in durante 4 segundos.
9. **Fin:** Se congela el input de ambos jugadores. Opcionalmente volver al menú.

### Scripts del jumpscare:

**`JumpscareCreature.cs`** — `Assets/_Project/Scripts/AI/JumpscareCreature.cs`
- Componente ligero: solo modelo + Animator + AudioSource. Sin CreatureController ni NavMeshAgent.
- `Execute(Vector3 targetPosition)`: secuencia scripted (walk N pasos → lunge).
- Footstep AudioClip por paso, lunge AudioClip al atacar.
- SyncVars para position/rotation (smooth sync en clientes).
- Prefab: modelo FBX de criatura como hijo + Animator + JumpscareCreature + NetworkIdentity + AudioSource en root.

### Campos configurables de DemoFinalEventManager:
| Campo | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `finalPuzzle` | Puzzle | — | Referencia al puzzle 3 (BillsPuzzle_Root) |
| `runnerDoor` | InteractableDoor | — | La puerta azul de la casa |
| `runnerCreatureSpawn` | Transform | — | Punto en el marco de la puerta del Runner |
| `guideDoor` | InteractableDoor | — | Una puerta del cuarto del Guide |
| `guideCreatureSpawn` | Transform | — | Punto en el marco de la puerta del Guide |
| `jumpscareCreaturePrefab` | GameObject | — | Prefab con JumpscareCreature (modelo ligero) |
| `delayBeforeCreature` | float | 1.0 | Segundos entre puertas abiertas y spawn |
| `creatureSequenceDuration` | float | 2.5 | Tiempo para walk + lunge antes del fade |
| `fadeDuration` | float | 0.5 | Duración del fade |
| `continueTextDuration` | float | 4.0 | Cuánto tiempo se muestra "Continuará..." |

### Modificación necesaria: `ScreenEffectsController.cs`
- Agregar método `FadeToBlack(float duration)` que haga un fade con un panel negro de UI.
- Agregar método `ShowContinueText()` que muestre el texto sobre el negro.

---

## Sistema 5: Demo Progression Manager (DemoProgressionManager)

### Script: `DemoProgressionManager.cs`
- **Ubicación:** `Assets/_Project/Scripts/Core/DemoProgressionManager.cs`
- **Tipo:** `NetworkBehaviour` — Singleton que coordina todo.

### Responsabilidad:
Orquesta la secuencia completa de la demo. Conecta los puzzles con el heartbeat y el evento final.

```
Puzzle 1 resuelto → DemoHeartbeatManager.SetStage(1)
                   → Abrir puerta/pasaje al pasillo de lava

Puzzle 2 resuelto → DemoHeartbeatManager.SetStage(2)
                   → Abrir paso a la casa de Carlos

Puzzle 3 resuelto → DemoHeartbeatManager.SetStage(3) → pánico
                   → DemoFinalEventManager.TriggerFinalEvent()
```

### Campos:
- `Puzzle[] demoPuzzles` — Los 3 puzzles en orden.
- `InteractableDoor[] transitionDoors` — Puertas entre áreas (garaje→pasillo, pasillo→casa).
- `DemoHeartbeatManager heartbeatManager`
- `DemoFinalEventManager finalEventManager`

---

## Tareas para el compañero del Guide

### Puzzle 2 — Lo que necesita hacer el compañero:

1. **Crear el documento de tabla Morse** como `GuideFolderData` con un `ReadableData` que contenga la tabla de decodificación. El Guide lo escanea en su terminal para consultarlo.
2. Asegurarse de que el documento se muestre correctamente en `GuideTerminalView` cuando se escanea.
3. La tabla Morse debe ser una carpeta física (`GuideFolderItem`) que el Guide pueda tomar de un estante y escanear.

### Puzzle 3 — Lo que necesita hacer el compañero:

1. **Crear `FaxReceiverDock.cs`** — Máquina receptora de fax en el cuarto del Guide:
   - Hereda de `RatInteractable`.
   - Tiene un `SyncVar bool hasPendingFax` que se activa cuando el Runner envía algo.
   - Cuando el servidor le asigna un fax, guarda la referencia al `DocumentData` del recibo.
   - Indicador visual: luz roja parpadeante cuando hay fax pendiente.
   - Al interactuar: genera un `GuideFolderItem` (el recibo) en el inventario del Guide.
   - El `GuideFolderData` del item envuelve el `DocumentData` del recibo para que se renderice idéntico en la terminal.
   - El Guide lo lleva al `FolderScannerDock` existente para escanearlo.

2. **Modificar `GuideTerminalView.cs`** — Agregar panel de pago:
   - Nuevo método `ShowPaymentScreen(string receiptName, float amount, string paymentCode)`.
   - Cuando se detecta que el documento escaneado es un recibo (por tag o campo especial en el `GuideFolderData`), mostrar el documento renderizado normalmente + un indicador "PAGO PENDIENTE" debajo.
   - Método `ShowPaymentConfirmed(string receiptName)` para el feedback de éxito.
   - Método `ShowPaymentTimeout()` para cuando se pasa el tiempo.

3. **Crear `BillPaymentButton.cs`** — Botón físico junto a la terminal:
   - Hereda de `RatInteractable`.
   - Solo interactuable cuando hay un recibo escaneado pendiente de pago en la terminal.
   - Al presionar: envía `[Command]` al servidor para confirmar pago.
   - El `BillsPuzzleCoordinator` en el servidor valida y avanza el puzzle.

4. **Crear la máquina receptora de fax** en la escena del cuarto del Guide:
   - Modelo simple (cubo con textura o asset básico).
   - Colocar separada de la terminal (para que el Guide tenga que caminar hasta ella).
   - Luz indicadora + AudioSource para el sonido de fax recibido.

5. **Crear los 4 `DocumentData` assets de los recibos** (ver detalle arriba en Sistema 3):
   - Receipt_Water, Receipt_Electric, Receipt_Rent, Receipt_School.
   - Usar el prefab de papel existente con `ReceiptPickup` para instanciarlos en la casa.
   - **IMPORTANTE:** También crear los `GuideFolderData` correspondientes que envuelvan cada `DocumentData`, para que el `FolderScannerDock` existente los pueda procesar.

---

## Decoración del pasillo de lava

El pasillo representa la dificultad de Carlos para llegar a su casa. Sugerencias para reforzar esa narrativa:

- **Paredes agrietadas** con texturas de deterioro — como si el camino se estuviera desmoronando.
- **Fotos familiares rotas** en las paredes — fotos de Carlos con su familia, pero rotas o quemadas en los bordes. Refuerza que está perdiendo todo.
- **Objetos personales semi-destruidos** flotando o hundidos en la lava — un zapato de niño, una mochila escolar, un portarretratos. Son las cosas que Carlos está perdiendo.
- **Iluminación roja/naranja** desde abajo (la lava) y oscuridad arriba — sensación de estar descendiendo.
- **Escrituras en las paredes** tipo graffiti desesperado: "No voy a llegar", "¿Por qué sigo intentando?", "Los niños me esperan" — pensamientos intrusivos de Carlos.
- **La puerta al final** (hacia la casa) debe verse como un refugio — la única fuente de luz cálida/blanca al final del pasillo rojo.

---

## Orden de implementación recomendado

### Bloque 1 — Infraestructura (hacer primero, ~1 hora)
1. `DemoHeartbeatManager.cs`
2. `DemoProgressionManager.cs`
3. `DemoFinalEventManager.cs` (estructura base, sin el jumpscare todavía)

### Bloque 2 — Puzzle 2: Morse (~2 horas)
1. `MorsePuzzleEmitter.cs`
2. Configurar puzzles hijos en la escena del pasillo
3. Crear contenido de tabla Morse para el Guide
4. Testing del flujo completo

### Bloque 3 — Puzzle 3: Recibos (~2-3 horas)
1. `FaxMachine.cs` (lado Runner)
2. `BillsPuzzleCoordinator.cs`
3. Crear recibos como PickableItems en la casa
4. **En paralelo, el compañero:** `FaxReceiverDock.cs`, modificar `GuideTerminalView`, `BillPaymentButton.cs`
5. Integración Runner ↔ Guide
6. Testing del flujo completo

### Bloque 4 — Evento final (~1 hora)
1. Completar `DemoFinalEventManager` con spawn de criatura
2. Fade a negro + texto "Continuará..."
3. Integrar con `DemoProgressionManager`
4. Testing del jumpscare

### Bloque 5 — Pulido (~30 min)
1. Ajustar intervalos de latidos (¿se siente bien la progresión?)
2. Ajustar ventanas de tiempo del puzzle 3
3. Decorar pasillo de lava si hay tiempo
4. Playtest completo del flujo garaje → pasillo → casa → jumpscare

---

*Tiempo total estimado: 6-7 horas de trabajo entre los dos. Es apretado pero alcanzable.*
