# Propuesta técnica — Echoes of Suspicion
*Documento complementario: biomas, criaturas, tipos de sala y puzzles.*
*Ver también: `Propuesta_Juego_Summer_Camp.md` (concepto, mecánicas, historia, sistema de vida/voluntad, finales).*

---

## 1. Tipos de sala (plantilla aplicable a ambos laberintos)

1. **Sala de exploración pura (espacio seguro):** los monstruos **no pueden entrar** a este tipo de sala — es zona franca garantizada. Sin embargo, sí pueden **detectar actividad dentro** (por ejemplo, el sonido de un envío de la maleta) y posicionarse justo afuera de la salida, esperando — así que salir de una sala segura después de haber generado ruido adentro es, por sí mismo, un momento de tensión. El reto interno es encontrar objetos/materiales y quizás resolver un puzzle ambiental menor. También sirve como refugio: si el Corredor está en peligro, llegar a una de estas salas es una meta táctica válida.
2. **Sala de puzzle compartido:** el Corredor encuentra un mecanismo/panel/símbolo que debe describirle al Guía por voz, y el Guía busca la respuesta en su propio cuarto. Corazón de la comunicación asimétrica.
3. **Sala de sigilo:** hay uno o más monstruos patrullando, sin puzzle central — el reto es puramente de movimiento, ruido/luz y paciencia.
4. **Sala mixta (puzzle + sigilo):** la más tensa — hay que resolver algo mientras se evita ser detectado.
5. **Sala de bifurcación/backtracking:** varios caminos posibles, alguno requiere retroceder tras descubrir que era un camino falso o que falta un objeto conseguido en otra sala.

La densidad de salas tipo 3 y 4 debe subir ligeramente en el Laberinto 2, para que se sienta más difícil sin depender solo de "más enemigos".

### Regla general — ningún jugador espera pasivamente

En todo puzzle compartido, **ambos roles deben tener una acción simultánea que cumplir**, no una secuencia de "el Corredor describe → espera → el Guía resuelve → el Corredor ejecuta". Se logra añadiendo una capa de tiempo, reflejos o riesgo del lado del Corredor mientras el Guía investiga/decodifica, de modo que ninguno de los dos se queda sin hacer nada mientras el otro trabaja.

### Regla general — densidad de puzzles por bioma

Cada bioma (Control, Aislamiento, Fricción, y sus equivalentes en el Laberinto 2) debe tener **mínimo 2-3 puzzles** (obligatorios y/o opcionales) más **1 easter egg** — un hallazgo de exploración sin mecánica, puramente de sabor/humor/referencia, no obligatorio y sin pistas de progresión.

---

## 2. Regla especial de una sola vez — Laberinto 1

En algún punto del Laberinto 1 (preferiblemente en el bioma "Aislamiento", ver abajo), hay un tramo donde el Corredor debe **apagar completamente la linterna** y depender al 100% de las instrucciones del Guía — visión cero, comunicación pura. Refuerza el tema de confiar en el otro, y queda fuera del tramo final del Laberinto 2 para no competir con el clímax del regreso por el compañero (donde el Corredor sí necesita su linterna disponible).

---

## 3. Biomas — dependen del arquetipo del personaje-Guía

**Cambio importante de diseño:** los biomas ya no son un set fijo compartido por todos los personajes. En su lugar, **cada personaje jugable trae sus propios 3 biomas + 9 puzzles**, basados en su arquetipo de personalidad, historia y profesión — el "laberinto" es la psiquis del personaje que ejerce de Guía en ese acto (ver sección 5.5 del documento conceptual).

### Regla general de diseño por personaje
Cada personaje debe traer:
- **3 biomas visualmente distintos entre sí**, cada uno reflejando una faceta o tensión del arquetipo de ese personaje.
- **3 puzzles por bioma** (9 en total por acto), diseñados para conectar temáticamente con el bioma en el que están.
- **Progresión de tensión creciente** entre los 3 biomas: el primero más tranquilo/exploratorio, el último con más presencia de sigilo y criaturas.
- **Un easter egg opcional por bioma**, sin mecánica, solo hallazgo de sabor/humor/referencia.

### Referencia estética general (aplicable a cualquier personaje)
Aunque cada personaje tiene su propio mundo, todos los biomas comparten una base estética coherente con la premisa: mezcla de estética clínica-industrial con elementos deteriorados o distorsionados por la mente del personaje. La paleta y ambiente concretos de cada bioma son responsabilidad del diseño de cada personaje.

### Los 3 biomas "genéricos" que ya estaban en el documento
Los biomas previamente definidos (Control, Aislamiento, Fricción, Óxido, Codicia, Vacío) quedan como **referencias de mood y estructura**, no como el set canónico del juego. Sirven como plantilla de tipos de tensión que se pueden replicar en los biomas específicos de cada personaje (uno "clínico y controlado", uno "monocromático y de aislamiento", uno "cargado y de tensión creciente"), adaptados temáticamente a cada arquetipo.

### Biomas fijos (independientes del personaje)
Solo hay **dos biomas fijos que no dependen del personaje**:
- **Bioma del mini-puzzle final del Acto 1** (falso final): un espacio dedicado al reencuentro y al mini-puzzle físico compartido que lleva a la "puerta final" falsa.
- **Bioma del mini-puzzle final del Acto 2** (final real, solo si ambos cooperan): un espacio dedicado al mini-puzzle físico compartido que lleva a la salida real.

Estos dos biomas se diseñan una sola vez y funcionan igual para todas las combinaciones de personajes.

---

## 5. Criaturas

Ninguna criatura del laberinto está ahí por voluntad propia — son **híbridos robótico-orgánicos**, creadas por el propio laboratorio combinando tecnología con tejido vivo animal, no capturadas de la naturaleza. Esto las conecta directamente con el destino de los sujetos humanos: ambos son, en distintos grados, material fabricado y explotado por la misma instalación. Este dato aparece en el reporte de laboratorio que el Guía puede encontrar (ver sección 8.2), con el mismo tono clínico y frío del resto del juego. Ejemplo de línea de referencia:

> *"Sujeto no-humano 01 (unidad híbrida). Se registró aturdimiento ante exposición directa a fuente lumínica (aprox. 2 segundos). Tras exposición repetida, el sujeto mostró comportamiento agresivo y aparente insensibilidad al estímulo. Se recomienda no depender de este método como medida de contención prolongada."*

### Criatura 1 — Laberinto 1 (detecta por sonido, teme a la luz)
- **Fortaleza:** oído excelente — detecta al Corredor por ruido (pasos, superficie, volumen de voz), como ya está definido en el sistema de sonido general.
- **Debilidad:** le teme a la luz directa. Apuntarle la linterna directamente a la cara la **aturde durante 2 segundos**.
- **Enojo (uso único):** tras ese primer aturdimiento por luz, la criatura se enoja y **deja de importarle la luz por completo** para el resto del encuentro — el truco de la linterna ya no la aturde ni la afecta después de la primera vez.
- **Nerfeo tras enojarse:** aunque ya no se aturde con luz, entra en un modo de miedo/supervivencia — sus ataques son **más débiles y más lentos** que antes de enojarse. No se vuelve más peligrosa al enojarse, se vuelve más errática y menos efectiva, aunque ya no se pueda controlar con luz.

### Criatura 2 — Laberinto 2 (detecta por luz, teme al sonido fuerte)
- **Fortaleza:** atraída directamente por la luz de la linterna — cuanta más luz, más fácil de atraer.
- **Debilidad:** un sonido fuerte (por ejemplo, gritar por el altavoz) la **aturde y la ralentiza**.
- **Enojo (uso único, simétrico a la Criatura 1):** tras el primer aturdimiento por sonido, se enoja y **deja de importarle el sonido** para el resto del encuentro — gritar de nuevo no vuelve a aturdirla.
- **Nerfeo tras enojarse:** igual que la Criatura 1, sus ataques se vuelven más débiles y lentos tras enojarse — miedo, no furia real.

### Por qué este diseño funciona bien
- **Situaciones cómicas naturales:** un jugador gritando de verdad por el altavoz para aturdir a la Criatura 2, o sosteniendo la linterna fija en la cara de la Criatura 1, genera momentos de tensión-comedia genuinos gracias al uso de voz real.
- **Uso único = decisión táctica, no repetible al infinito:** el jugador no puede spamear luz o sonido para controlar a la criatura indefinidamente — tiene que guardar el truco para el momento más crítico, porque después de usarlo una vez, esa herramienta deja de servir contra esa criatura específica.
- **El nerfeo evita que "enojarla" sea un castigo desproporcionado:** perder el control de aturdimiento no vuelve el encuentro imposible — la criatura sigue siendo una amenaza, pero una más lenta y débil, manejable con sigilo normal.
- **Implementación:** ambas criaturas reutilizan el mismo AI Perception Component (con estímulo Hearing o Sight según el caso) más una máquina de estados simple de 2 fases (normal → enojada), con una bandera booleana de "ya se enojó" que desactiva el estímulo de aturdimiento correspondiente y aplica un multiplicador de daño/velocidad reducido. Nada de sistemas nuevos, solo una capa de estados sobre lo que ya existía.

---

## 5.1 Cámara crafteable del Guía

- El Guía puede craftear una **cámara de video rudimentaria** (con materiales del Corredor, como cualquier otro objeto) y "conectarla" a su monitor para obtener una vista limitada del entorno del Corredor. La cámara terminada se envía al Corredor por el sistema normal de maleta/Receptor, y es él quien la coloca físicamente en el entorno.
- **Video en blanco y negro, con estática/interferencia visible, campo de visión estrecho y fijo** (no se puede controlar a distancia, solo muestra lo que enfoca desde donde el Corredor la coloca) — da una pista puntual, nunca una ventana clara y completa al mundo del Corredor.
- **Batería limitada, pero renovable de forma escasa:** la batería inicial no se recarga sola, pero se pueden craftear baterías adicionales usando materiales extraídos específicamente de criaturas híbridas **derrotadas** (nunca de exploración pasiva) — es decir, solo mediante la vía arriesgada de combate ya definida en el sistema de vida. Esto mantiene la cámara como un recurso escaso y ligado al riesgo, no una solución gratuita.
- **Totalmente opcional:** el juego debe poder completarse sin usar la cámara en ningún momento — su función es dar comodidad y variedad de juego al Guía, no ser un requisito de diseño.
- **Disponible en ambos laberintos** (cada Guía, al cambiar de rol, puede craftear la suya), pero con **menos batería disponible en el Laberinto 2**, coherente con que ese laberinto debe sentirse ligeramente más difícil que el primero.

---

## 6. Capacidades y restricciones — Corredor

### Puede
- Moverse: caminar, correr, agacharse/moverse sigilosamente (cada uno con distinto radio de ruido, modificado por superficie y bioma).
- Alternar perspectiva primera/tercera persona en cualquier momento.
- Encender/apagar la linterna a voluntad (afecta ruido no, pero sí detección del monstruo de luz en Laberinto 2, y consumo de batería si se decide implementar eso como límite adicional — a definir).
- Recoger materiales y objetos del entorno (los lleva cargando consigo — sin límite de espacio por el momento).
- Llegar al **punto fijo de envío/recepción de cada bioma** (que debe descubrir explorando) para iniciar una transmisión que envía todos los materiales acumulados al Guía y recibe los objetos crafteados. La animación se puede cancelar en cualquier momento, pero el sonido de transmisión se emite igual y las criaturas lo escuchan.
- Recibir y usar los objetos que llegan durante la transmisión (equipar arma, activar señuelo, usar objeto de utilidad).
- Hablar por el altavoz físico (voz real, sin opción de silenciarse salvo el volumen físico real del jugador).
- Interactuar con mecanismos/paneles de puzzle y describírselos al Guía.
- Entrar y salir libremente de salas de exploración pura (espacio seguro).
- Retroceder a salas ya visitadas.

### No puede
- Ver el mapa completo del laberinto (solo lo que está directamente en su campo de visión).
- Ver el cuarto del Guía ni su contenido, hasta llegar físicamente a su puerta al final del laberinto.
- Comunicarse con el Guía por ningún medio que no sea el altavoz (no hay chat de texto, no hay señales).
- Craftear objetos — el crafteo es exclusivo del Guía.
- Usar audífono — solo altavoz, como ya está definido en el documento conceptual.
- Pausar el decaimiento de vida del Guía ni acelerar su propia regeneración de vida por sí mismo (depende de lo que el Guía le envíe).

---

## 7. Capacidades y restricciones — Guía

### Puede
- Ver el mapa esquemático completo del laberinto: geometría abstracta (paredes, puertas), posición en tiempo real del Corredor, y criaturas detectadas dentro de un **radio de percepción** alrededor del Corredor (ver detalle abajo).
- Investigar documentos, notas y mecanismos dentro de su propio cuarto.
- Craftear objetos usando los materiales que le envía el Corredor: objetos de recuperación de su propia vida, o herramientas/armas/utilidades para el Corredor.
- Elegir a quién beneficia cada objeto crafteado (a sí mismo o al Corredor) — esta elección es la base mecánica de la posibilidad de traición.
- Enviar objetos crafteados al Corredor mediante su aparato receptor/emisor ("el Receptor"), que llegan al Corredor durante su próxima transmisión en el punto fijo de envío del bioma en el que esté.
- Iluminar temporalmente una zona del mapa para el Corredor (cuesta voluntad).
- Marcar peligros directamente en el HUD del Corredor (cuesta voluntad).
- Hablar por el altavoz hacia el Corredor (voz real).
- Moverse e interactuar libremente dentro de los límites físicos de su propio cuarto.

### Radio de percepción y detección de criaturas

- El Corredor tiene un radio de percepción invisible a su alrededor (no es visión — el propio Corredor no necesita ver a la criatura para que esto funcione).
- Cuando una criatura entra en ese radio, el Guía **empieza a verla en su mapa**: puede contarlas (cuántas hay), identificarlas (qué tipo de criatura es cada una) y ver cómo se mueven en tiempo real, todo mientras la criatura permanezca dentro del radio.
- Si la criatura sale del radio, la información deja de actualizarse (se pierde el rastro, o queda como última posición conocida — a definir en balance).
- Esto le da al Guía información táctica real y jugable sin romper la regla de que nunca ve el entorno visual del Corredor — sigue siendo abstracto (puntos/íconos en el mapa esquemático), no una cámara del entorno.

### No puede
- Ver el entorno visual real del Corredor (texturas, monstruos en directo, iluminación de su sala) — solo la representación abstracta del mapa.
- Salir de su cuarto en ningún momento durante el laberinto (solo ocurre en la escena de reencuentro al final).
- Craftear sin los materiales necesarios, ni realizar ayuda activa (enviar objetos, iluminar, marcar peligros) sin voluntad disponible — forzarlo más allá de la voluntad disponible le cuesta vida directamente, como ya está definido en el documento conceptual.
- Ver ni resolver directamente los puzzles del entorno del Corredor — depende 100% de que el Corredor se los describa por voz.

---

## 8. Diseño detallado — Apertura + plantilla de referencia de un acto

> **Importante — cambio de estructura:** el diseño detallado que sigue (secuencia de 9 salas + 3 puzzles + easter egg) originalmente fue concebido como el Laberinto 1 canónico. Con la introducción de los personajes jugables (ver sección 5.5 del documento conceptual), este diseño **queda como plantilla de referencia** para el equipo — muestra la densidad, ritmo y tipos de sala esperados en cada acto — pero el contenido concreto (biomas, temas, puzzles específicos) debe re-diseñarse para cada personaje según su arquetipo.
>
> Lo que sigue siendo canónico y aplica a todos los personajes:
> - La apertura del juego (Escena 0 + despertar paralelo) — sección 8.0.
> - La densidad esperada por bioma (2-3 puzzles obligatorios + 1 opcional + 1 easter egg).
> - Los tipos de sala (exploración segura, puzzle compartido, sigilo, mixta, bifurcación).
> - La regla especial de "linterna apagada" (puede vivir en uno de los biomas de cualquier personaje).
> - El principio de que ambos jugadores están siempre activos en simultáneo, sin espera pasiva.
>
> Lo que cambia según el personaje: la estética, la ambientación, los temas de los puzzles (aunque su estructura mecánica sea similar), y los objetos/notas específicas del cuarto del Guía.

### 8.0 Apertura del juego (cinemática + despertar paralelo)

**Escena 0 (compartida, sincronizada en red):** recepción (recepcionista explica el experimento y el pago de $50.000.000 COP, voz + subtítulos) → tramo on-rails hacia el laboratorio (cámara libre, el jugador puede ver una fila de personas esperando su turno) → llegada al laboratorio, saludo breve con el otro jugador real ya sentado → robots médicos los acuestan y conectan → fundido a negro. Implementación: Level Sequencer replicado, cada cliente ve su propio personaje y el modelo real del otro jugador.

**Despertar paralelo:** ambos acostados en el suelo, visión borrosa que se enfoca, sonido que regresa gradualmente.
- **Corredor** despierta en "El Claro" (Sala 1) con solo linterna y altavoz equipados; encuentra una piedra de doble uso (señuelo al lanzarla / arma cuerpo a cuerpo al golpear).
- **Guía** despierta en su cuarto a oscuras; activa en orden luz → panel de mapa (muestra "estableciendo conexión" hasta que el Corredor esté activo) → estación de crafteo, de forma que el primer contacto por voz y el primer envío ocurran casi al mismo tiempo para ambos.

### 8.1 Secuencia de salas del Laberinto 1

| # | Bioma | Tipo de sala | Función |
|---|---|---|---|
| 0 | — | Cinemática de introducción | Escena 0 completa (ver 8.0). |
| 1 | Control | Exploración pura (segura) — **"El Claro"** | Despertar del Corredor, tutorial de movimiento, linterna + altavoz, piedra de doble uso, primer material encontrado, primer envío, primer objeto crafteado (tutorial de crafteo). Al final, un **candado tutorial** bloquea el paso a la Trifurcación (ver 8.1a — Puzzle 1). |
| 2-3 | Control | Zona de la Trifurcación (ver 8.1a) | Tres caminos desde un punto central: uno de "muerte inminente" (tutorial de riesgo real), y dos conectados entre sí en loop que juntos resuelven el Puzzle 2 (palancas) y esconden el Puzzle 3 opcional (caja fuerte) y el easter egg del bioma. |
| 4 | Aislamiento | Exploración pura (segura) | Cambio de tono ambiental; primeros documentos con lenguaje clínico (Etapa 1 de revelación progresiva). |
| 5 | Aislamiento | Sigilo — **regla especial: linterna apagada** | Primera aparición del monstruo de sonido. El Corredor debe apagar la linterna por completo y depender solo de las instrucciones por voz del Guía. |
| 6 | Aislamiento | Puzzle compartido | Puzzle de liberación de tensión tras la sala 5; requiere una segunda nota del cuarto del Guía (Nota B). |
| 7 | Fricción | Sigilo | Monstruo de sonido activo, gestión de ruido en un entorno más caótico (neón parpadeante = más estímulos visuales, mismo reto de sonido). |
| 8 | Fricción | Mixta (puzzle + sigilo) | La sala más difícil del laberinto — resolver un puzzle mientras se evita al monstruo. Última prueba antes de llegar a la puerta del Guía. |
| 9 | — | Llegada a la puerta del cuarto del Guía | El Corredor llega físicamente al cuarto del Guía → escena de reencuentro y revelación (ver documento conceptual, sección 6). |

### 8.1a Bioma Control — Puzzles y easter egg (densidad completa)

**Puzzle 1 — Candado tutorial (obligatorio, entre "El Claro" y la Trifurcación):** una rueda de símbolos gira sola; el Corredor debe detenerla en el momento justo (mini-reto de timing/reflejos), mientras el Guía, en paralelo, descifra en una nota corta cuál es el símbolo correcto y se lo dice por voz en tiempo real ("¡ahora! ¡el círculo!"). Ninguno puede resolverlo solo, y ambos están activos al mismo tiempo desde el inicio — primera práctica de comunicación bajo presión, más simple que el acertijo cifrado que sigue.

**Punto de partida de la Trifurcación:** al salir de "El Claro" (tras el candado), el Corredor llega a un cruce con **tres caminos visibles**.

**Camino 1 — "Muerte inminente":**
- Diseñado para tentar al jugador a entrar (materiales visibles cerca de la entrada, aspecto explorable normal) — la idea es que caigan ahí precisamente por estar explorando con curiosidad, no por obligación de la ruta principal.
- Casi con certeza cuesta una vida si se entra sin cuidado (trampa fuerte o encuentro directo con una criatura en espacio cerrado, sin ruta de escape fácil).
- **Completamente evitable** si el jugador nunca entra — no es necesario para avanzar ni para conseguir las piezas del puzzle de las palancas.
- Es aislado: no está conectado en loop con los caminos 2 y 3. Su función es puramente enseñar, de forma temprana y en un contexto de bajo costo relativo, que la muerte/pérdida de vida es real.

**Caminos 2 y 3 — conectados entre sí en loop:**
- Físicamente enlazados (se puede pasar de uno a otro), lo que permite **hacerle loop a una criatura** que esté persiguiendo al Corredor — clásico recurso de maze-chase.
- Dentro de este loop hay además **mini caminos sin salida** (ramales cortos, cerrados) para mantener la sensación de laberinto real y dar lugares donde esconder hallazgos opcionales — no todo es loop abierto.
- **Camino 2** contiene un **papel con un acertijo cifrado**: describe el orden correcto de unos símbolos, pero de forma que no tiene sentido por sí solo — necesita cruzarse con la Nota A del cuarto del Guía para decodificarse en un orden real y utilizable.
- **Camino 3** contiene una **secuencia de símbolos en las paredes**, cada uno conectado visualmente a una palanca/mecanismo físico distinto — el Corredor puede ver qué símbolo corresponde a qué palanca, pero no sabe en qué orden activarlas hasta que el Guía se lo diga.
- **En uno de los mini caminos sin salida**, el Corredor encuentra un **papel con la receta de curas**, que de paso explica en términos in-universo el sistema de vida del Guía: *"[Nombre del compuesto] detiene por 10 segundos el deterioro biológico del sujeto conectado, y restaura un porcentaje de su condición."*

**Puzzle 2 — Palancas (obligatorio):**
1. El Corredor describe por voz el acertijo cifrado del Camino 2 al Guía.
2. El Guía lo cruza con la Nota A de su cuarto y obtiene el orden real de los símbolos (ej. "círculo, triángulo, cuadrado").
3. El Guía le comunica ese orden al Corredor por voz.
4. El Corredor, que ya vio en el Camino 3 qué palanca corresponde a cada símbolo, activa las palancas físicas en el orden correcto.
5. La puerta que conecta con el siguiente tramo (bioma Aislamiento) se abre.

**Puzzle 3 — Caja fuerte (opcional, recompensa):** en otro de los mini caminos sin salida del loop 2-3, una caja fuerte con materiales extra de recompensa. El código está partido en dos: números grabados en la pared (solo los ve el Corredor) y una nota parcial en el cuarto del Guía con el resto. Una criatura patrulla cerca, así que el Corredor debe mantenerla a raya (sigilo, o usar la piedra) **mientras** transmite su mitad del código y el Guía busca la nota — ambos activos a la vez, sin espera pasiva.

**Easter egg de Control (pendiente de definir el contenido exacto):** una sala pequeña y oculta, sin mecánica ni obligación de encontrarla — solo una referencia/guiño (posiblemente al psicoanálisis, a definir con el equipo) que recompensa la curiosidad con humor o extrañeza, sin dar pistas de progresión.

Este mismo patrón (2-3 puzzles con ambos roles siempre activos + 1 easter egg) se replica en Aislamiento y Fricción, ajustando el contenido a cada bioma.



- **Estación de crafteo (dispensador):** interfaz central donde el Guía combina materiales recibidos para producir objetos.
- **Panel de mapa esquemático:** muestra geometría del Laberinto 1, posición del Corredor, zonas de peligro marcadas.
- **Nota A:** contiene la clave necesaria para decodificar el acertijo cifrado que el Corredor encuentra en la Zona de la Trifurcación (ver 8.1a) — al cruzarla con lo que el Corredor describe, revela el orden real de los símbolos de las palancas.
- **Nota B:** contiene la clave/patrón necesario para resolver el puzzle de la Sala 6.
- **Nota C (opcional, de ambientación):** documento con lenguaje clínico que refuerza la Etapa 1 de revelación progresiva — no es necesaria para avanzar, es hallazgo opcional.
- **Cuaderno de campo:** objeto interactivo de lectura con dos secciones:
  - **Historias:** relatos cortos sueltos, de tono ambiguo (podrían ser de personal anterior, de otros sujetos, o simple ambientación) — alimentan la Etapa 1 de revelación progresiva sin ser obligatorios de leer.
  - **Reporte de laboratorio (antes "bestiario"):** documento clínico interno sobre la Criatura 1 (la única relevante en el Laberinto 1) — describe su fortaleza (oído), su debilidad (luz, aturde 2 segundos), su comportamiento tras enojarse (deja de importarle la luz, pero ataca más débil y lento), y su naturaleza de unidad híbrida robótico-orgánica creada por el propio laboratorio. El reporte de la Criatura 2 aparece más adelante, en el cuaderno equivalente de la Sala del Guía 2 — así el jugador aprende sobre cada criatura justo antes de necesitarlo, no todo de golpe.
- **Receta de cámara:** entre los materiales/planos disponibles en la estación de crafteo, el Guía puede craftear la cámara de video rudimentaria descrita en la sección 5.1, sujeta a su batería limitada.
- **Inventario visual:** lista de materiales recibidos disponibles para craftear en cualquier momento.

### 8.3 Nota de implementación

Toda esta secuencia usa exclusivamente sistemas ya definidos (AI Perception, triggers de sala, crafteo por variables, mapa 2D esquemático) — no introduce ningún sistema nuevo, por lo que es un buen punto de partida para empezar el desarrollo mañana mismo.

---

## 9. Pendiente por definir (documento técnico)

- **Diseño de los 2 personajes iniciales** (1M + 1F) — cada uno con: arquetipo, historia breve, profesión, habilidades específicas al rol de Corredor y al de Guía, sus 3 biomas propios (nombre, estética, temática), y sus 9 puzzles propios (3 por bioma).
- Diseño del easter egg de cada bioma de cada personaje (uno de referencia posible: psicoanálisis, a confirmar; el resto pendiente).
- Diseño de los **2 mini-puzzles finales compartidos** (uno para el falso final del Acto 1, otro para el final real del Acto 2) — biomas propios, dedicados, iguales para todas las combinaciones de personajes.
- Diseño concreto de las **dos puertas del clímax del Acto 2** (salida real vs. cuarto de control), del sistema de decisión simultánea de traición (Guía cierra/no cierra, Corredor vuelve/no vuelve), y de la secuencia narrativa que se dispara con cada una de las 4 combinaciones.
- Balance de detección de ambos monstruos (radio, velocidad de reacción, patrones de patrullaje).
- Decidir si la linterna tiene límite de batería/uso, o es de uso libre sin restricción de energía.
- Arte de referencia final para cada bioma de cada personaje (paleta de color exacta, assets de Unreal Marketplace o creados a mano).
- Duración estimada por sala / por bioma / por acto completo.
- Definir cómo se ve visual y mecánicamente el **punto de envío/recepción** en cada bioma (¿es el mismo objeto físico repetido con distinta estética, o cada bioma tiene su propio diseño diegético?).

---

*Documento de trabajo — complementa la propuesta conceptual. Sujeto a ajuste según diseño de niveles.*
