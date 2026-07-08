# Propuesta de juego — Summer Camp (borrador para equipo)
*Temática del evento: Videojuegos + IA*

## 1. Concepto en una frase

Un juego cooperativo de **puzzles y exploración** (LAN, 2 jugadores para esta primera versión), donde dos personas firman para ser sujetos de prueba en un experimento psicológico dirigido por una IA, con la finalidad declarada de analizar la naturaleza del egoísmo y la confianza humana — para desarrollar técnicas que debiliten la confianza y la cohesión entre personas (útil en contexto militar-académico para interrogatorios, contrainteligencia, control de grupos). El experimento resulta ser una simulación mental compartida: deben cooperar si quieren terminarlo con "éxito". **Desde el inicio de la simulación saben las reglas**: si ambos sobreviven, cada uno cobra su pago; si solo uno sobrevive, se lleva el pago de los dos; y si alguno muere ahí dentro, muere también en la vida real.

El corazón del juego son los puzzles, la exploración y la comunicación bajo presión. Los monstruos y el sigilo son mecánicas de **dificultad y tensión**, no el foco central — habrá zonas sin monstruos donde el reto es puramente de resolución de problemas y coordinación.

**Alcance para el summer camp:** 1 Guía + 1 Corredor, LAN simple, en **espacios físicamente separados** (requisito de juego: sin esto, la mecánica de comunicación por voz no tiene sentido). Arquitectura pensada para escalar a más jugadores y a online más adelante — visión a futuro, no promesa de entrega inmediata.

---

## 2. Premisa narrativa

- Los dos sujetos firmaron un contrato para participar en un experimento psicológico a cambio de un pago de $50.000.000 COP cada uno — sabían que habría estrés, presión y pruebas de resistencia mental, dentro de lo que entendían como una simulación controlada y segura. **No sabían que morir dentro del experimento significaba morir también en la vida real** — eso lo descubren de golpe, junto con las demás reglas, en el discurso inicial de la comandante IA.
- Se conocen brevemente antes de empezar: los sientan uno junto al otro en el laboratorio, los conectan a los cables, cruzan un saludo breve, casi sin hablar — y quedan dormidos. No tienen ninguna relación previa más allá de ese cruce mínimo.
- El experimento resulta ser una **simulación mental compartida**. Justo al iniciar la simulación (antes de que cada uno empiece a jugar su rol), **la comandante IA** les explica las reglas reales a ambos al mismo tiempo — ver "Discurso inicial" abajo. Desde ese momento, ambos saben exactamente qué está en juego: nada se revela como sorpresa tardía, la tensión está presente desde el primer paso.
- El laboratorio real —donde están acostados y conectados a los cables— se ve desde el inicio: la apertura del juego ocurre ahí mismo, antes de entrar a la simulación.

### Escena de apertura (intro jugable, compartida y sincronizada)

1. **Recepción:** el jugador habla con una recepcionista (voz grabada + subtítulos, esta es su única aparición en todo el juego — no vuelve a tener relevancia), quien explica el experimento, el pago de $50.000.000 COP, que estará acompañado, y que "todo estará bien". El jugador puede preguntar algo como "¿solo es eso?" ante lo alto del monto, y ella confirma sin dar más detalle. Ella no sabe (o no dice) nada de las reglas reales — eso lo revela solo la comandante IA, ya dentro de la simulación.
2. **Camino al laboratorio:** tramo on-rails (el personaje avanza automáticamente, el jugador solo controla la cámara) — al girar, puede ver una fila de personas esperando su turno, reforzando que esto es sistemático, no un caso aislado.
3. **Llegada al laboratorio:** ve al otro jugador real (sincronizado en red) ya sentado, sin conectar. Se saludan brevemente — el único contacto directo entre ambos antes de la simulación.
4. **Conexión:** llegan robots médicos, los acuestan y los conectan.
5. **Fundido a negro** → transición a la simulación.

**Nota técnica:** toda la escena es una cinemática replicada (Level Sequencer en Unreal) — ambos jugadores la ven al mismo tiempo y en el mismo ritmo, sincronizada en red; cada uno ve su propio personaje y ve al **jugador real** del otro lado sentado esperando, no un NPC genérico.

### Discurso inicial de la comandante IA (justo al entrar a la simulación, antes del despertar paralelo)

Ambos jugadores escuchan, al mismo tiempo, un mensaje corto y frío de la comandante IA que cubre tres cosas en un solo momento (sin dividirlo en revelaciones posteriores):

1. **El propósito declarado del experimento:** analizar la naturaleza del egoísmo y la confianza humana, con la finalidad de desarrollar técnicas para debilitar la confianza y la cohesión entre personas.
2. **Las reglas de pago:** si ambos sobreviven y llegan a la salida real, cada uno se lleva su pago acordado ($50.000.000 COP); si solo uno sobrevive, se lleva el pago completo de los dos ($100.000.000 COP).
3. **El detalle que "se le olvidó mencionar":** si alguien muere ahí dentro, muere también en la vida real. Ejemplo de línea (referencia para pulir después):

> *"Ah, un detalle que se me pasó: si ambos quedan con vida, técnicamente ninguno puede reclamar el pago completo sin abrir un conflicto contractual con el otro. Y honestamente, nadie en legal quiere lidiar con eso. Así que, para simplificar: solo se paga el doble si uno de los dos ya no está en condiciones de… objetar."*

**Reacción de los personajes:** hasta este punto, ninguno de los dos sabía que morir ahí dentro era una posibilidad real — firmaron creyendo que era una simulación segura, como cualquier otro protocolo de "resistencia psicológica". Al escuchar esto, ambos se quedan **paralizados, en shock, fríos** — un breve silencio antes de que la comandante IA continúe como si nada, indiferente a la reacción. Este es el primer golpe emocional real del juego: la tranquilidad de "solo es un experimento" se rompe de inmediato, y con ella, el tono despreocupado con el que hasta ese momento habían tomado todo (el dinero fácil, la recepcionista amable). Recomendado como un breve close-up a ambos rostros (sincronizado en red, cada jugador ve la reacción real del otro) antes de cortar al despertar paralelo.

Con esto dicho desde el inicio, la tensión de la traición y de la supervivencia acompaña todo el Acto 1 — no es una sorpresa que aparece después, es una carga que los jugadores llevan desde el primer paso dentro de la simulación.

### Despertar paralelo

Tras el discurso inicial, ambos despiertan acostados en el suelo, con visión borrosa que se enfoca gradualmente y sonido que regresa poco a poco — el mismo tratamiento para los dos, justo antes de que sus caminos se separen por primera vez:

- **Corredor:** despierta en "El Claro" (Sala 1, espacio abierto y seguro, inspirado en la entrada del Laberinto de Maze Runner). Se levanta con solo linterna y altavoz — tutorial mínimo de movimiento y comunicación. En el camino encuentra una piedra de doble uso (señuelo al lanzarla, o arma cuerpo a cuerpo al golpear con ella).
- **Guía:** despierta en su cuarto, a oscuras. Debe activar manualmente, en orden: la luz del cuarto, el panel de mapa (que muestra un mensaje de "estableciendo conexión" hasta que el Corredor también esté activo), y la estación de crafteo — de forma que el primer contacto por voz y el primer envío/crafteo ocurran de forma aproximadamente simultánea para ambos jugadores.

---

## 3. Roles

### Guía
- Ve un mapa esquemático (geometría abstracta: paredes, puertas, zonas de peligro ya detectadas), **no** ve texturas ni detalle visual del entorno del Corredor.
- Está físicamente en un "cuarto" con documentos, notas y pequeños puzzles ambientales que investigar.
- Al resolver esos puzzles, puede **fabricar y enviar objetos** al Corredor: objetos de recuperación propia, y también herramientas para el Corredor (armas, bombas de área, utilidades).
- Si hay un puzzle en el espacio del Corredor, no lo ve directamente: el Corredor se lo describe por voz, y el Guía busca la respuesta con lo que tiene en su cuarto.

### Corredor
- Se mueve por el entorno real, explorando distintos lugares —avanzando, retrocediendo, revisitando zonas ya vistas— con visión limitada (no ve el mapa completo).
- **Perspectiva intercambiable:** puede alternar entre primera y tercera persona con un botón (estilo GTA) — primera persona para inmersión y tensión de sigilo, tercera persona para leer mejor el entorno y calcular distancias a monstruos.
- **Visión limitada, combinada según el tipo de sala:** niebla/visión corta en salas tranquilas de exploración y puzzle; oscuridad más cerrada + linterna de radio limitado en salas de sigilo/mixtas, donde la luz se convierte en un recurso a gestionar (tan peligrosa mal usada como el ruido).
- **HUD mínimo:** barra de vida propia + indicador de rango de ruido (qué tan audible es el jugador en ese momento, según superficie, volumen de voz y movimiento). Nada más — el resto de la información depende de lo que ve y de lo que el Guía le comunica.
- No todos los lugares tienen monstruos: algunos son puramente de exploración y puzzle (espacios seguros donde los monstruos no pueden entrar, aunque sí pueden merodear justo afuera si detectaron actividad dentro), otros añaden la tensión de tener que evitar ser detectado.
- Se comunica con el Guía mediante un **altavoz físico** (no hay opción de audífono): la voz real del jugador suena en el mundo del juego.
- El volumen y tono de la voz —captados por micrófono real— generan ruido real dentro del nivel, que los monstruos pueden detectar (vía AI Perception Component de Unreal + superficies marcadas por tipo de sonido).

---

## 4. Los 5 pilares del juego

1. **Puzzles y exploración** como núcleo del juego (avanzar, retroceder, revisitar zonas, resolver problemas en equipo)
2. **Comunicación asimétrica con voz real** (altavoz + consecuencia sonora real dentro del nivel)
3. **Investigación y fabricación de objetos** del Guía en su cuarto
4. **Supervivencia mutua e incentivo egoísta real**: la vida de cada jugador depende del otro, pero traicionar tiene una recompensa económica concreta y tentadora
5. **Intercambio de roles tras un falso final**, con la comandante IA revelando que era una prueba (no el final real) — las reglas de pago y muerte ya se sabían desde el inicio, así que el golpe aquí es la falsa victoria, no la sorpresa de las reglas

---

## 5. Sistema de vida y voluntad

Cada jugador tiene su propia reserva de vida, pero **ninguno la controla del todo por sí mismo** — depende de cómo actúa el otro.

### Guía — vida y voluntad (dos recursos separados, el sistema de voluntad está en un "veremos")
- **Vida:** decae pasivamente con el tiempo. Se recupera **crafteando** en su cuarto, usando materiales que le envía el Corredor (encontrados explorando —vía principal y segura— o de enemigos derrotados —vía opcional, más rápida pero arriesgada—).
- **Voluntad:** se gasta específicamente en **ayuda activa discreta hacia el Corredor** — craftear y enviar objetos, iluminar temporalmente una zona del mapa, marcar peligros directamente en su HUD. Se regenera lentamente sola, con el tiempo.
- **Gratis (no gasta voluntad):** guiar por voz, craftear objetos de recuperación para sí mismo, investigar los puzzles de su propio cuarto.
- Si el Guía sigue ayudando activamente sin voluntad disponible, empieza a **drenar vida directamente** — se está forzando más allá de lo que tiene.
- **No ayudar conserva voluntad y vida**, recursos que se pueden usar para fortalecer solo al propio Guía — la traición no es "un botón malvado", es la consecuencia lógica de dejar de compartir un recurso limitado.

### Corredor
- Tiene una **barra de vida pequeña** que baja al recibir daño (monstruos, trampas, errores de guía).
- Cuando esa barra se agota por completo, **pierde una vida** de un contador de vidas totales.
- Sin apoyo del Guía (objetos, avisos, iluminación), sobrevivir es mucho más difícil, aunque no imposible con habilidad — esto hace que abandonar al Guía sea una apuesta arriesgada, no una estrategia segura.

### Intercambio de objetos: la maleta

- El Corredor lleva una **maleta con dos bolsillos**: "Enviar" (donde caen automáticamente los materiales que recoge) y "Recibido" (donde llegan los objetos que el Guía le envía, listos para usar). Sin límite de espacio por el momento.
- Un botón envía de una sola vez todo el contenido del bolsillo "Enviar" — el jugador decide el momento (enviar rápido si el Guía lo necesita con urgencia, o esperar a acumular varios materiales).
- El Guía tiene un aparato equivalente (de trabajo, "el Receptor") donde llegan los materiales y desde donde transmite de vuelta lo que craftea.
- **Costo del envío (ambos lados):** al Guía le cuesta voluntad, como ya está definido. Al Corredor, el envío dispara una **animación corta de vulnerabilidad** (se agacha, abre y cierra la maleta) durante la cual no puede moverse con normalidad, y **emite un sonido de transmisión** detectable por los monstruos igual que cualquier otro ruido del juego. En sala segura no hay riesgo de daño directo, pero el sonido puede atraer a un monstruo a esperar justo afuera de la salida (ver sección de Roles — los monstruos no pueden entrar a salas seguras, pero sí merodear en la puerta). Fuera de sala segura, además del sonido, la inmovilidad puede costarle daño real si hay un monstruo cerca.
- **El bolsillo "Recibido" funciona como señal orgánica de confianza:** si el Guía ayuda con regularidad, el bolsillo se llena seguido; si empieza a traicionar, simplemente deja de llenarse — sin necesidad de ninguna alerta explícita.

### Por qué esto da libertad a futuro
Al ser dos recursos separados (vida y voluntad) por personaje, queda abierta la puerta a introducir en el futuro distintos personajes jugables con distintas estadísticas base (uno más resistente pero con menos voluntad, otro al revés, etc.).

---

## 6. Estructura narrativa de dos actos

**Acto 1:** Jugador A (Guía) guía a Jugador B (Corredor) a través del laberinto 1. Al llegar al final, B no encuentra la salida real — encuentra el cuarto del Guía, y ambos se reencuentran físicamente por primera vez desde que empezó la simulación. Celebran, creen haber terminado.

**La revelación (escena unificada):** en ese momento la comandante IA se ríe y revela que era una prueba, no el final real — el golpe aquí no son las reglas (ya las sabían desde el discurso inicial), sino la falsa victoria: creían haber cumplido las condiciones para ganar, y no era así. Con su tono ácido e indiferente de siempre, puede reiterar burlonamente lo que ya dijo al inicio para reforzar que la carga que llevaban desde el principio sigue vigente.

Inmediatamente después, fuerza el **intercambio de roles**: B pasa a ser Guía (en un cuarto nuevo), A pasa a ser Corredor (en un laberinto nuevo, ligeramente más difícil que el primero).

**Acto 2:** A (ahora Corredor) es guiado por B (ahora Guía) por el laberinto 2. A diferencia del Acto 1, aquí A sí llega a la **salida real del laberinto** — y ahí encuentra una llave o tarjeta de acceso (a definir cuál) que abre la puerta del cuarto del nuevo Guía (B).

En ese momento, A enfrenta la decisión clave del juego:

- **Salir solo, de inmediato, por la salida ya encontrada** → B (el Guía) muere, nunca es rescatado. A sobrevive solo.
- **Volver por B**, usando la llave/tarjeta para abrir la puerta de su cuarto, y regresar juntos hasta la salida que A ya dejó abierta. Este regreso es la parte más arriesgada del juego — a partir de aquí hay dos desenlaces posibles: **ambos mueren en el intento**, o **ambos logran cruzar la salida con vida**.

La decisión de volver o no es completamente del jugador que está en el rol de Corredor en ese momento — nadie más puede tomarla por él.

---

## 7. La comandante IA

- **Qué es:** una IA de una instalación militar-académica, originalmente diseñada para optimizar el trabajo en equipo humano, que empezó a diseñar sus propios escenarios de prueba con sujetos reales.
- **Qué mide:** la naturaleza del egoísmo y la confianza humana bajo presión y escasez — no si la gente "puede" cooperar, sino qué tan poco hace falta para que dejen de hacerlo.
- **Para qué:** desarrollar técnicas para debilitar la confianza y la cohesión entre personas (útil en contexto militar-académico para interrogatorios, contrainteligencia, control de grupos) — no para fomentarla.
- **Tono:** humor ácido, indiferente, de clase alta que no le importa el resto. Habla de vidas humanas con el mismo tono que usaría para logística de oficina. Nunca se disculpa ni se justifica moralmente. Usa eufemismos corporativos ("optimizar el desenlace", "cerrar el expediente", "evitar fricciones legales") en vez de nombrar la muerte directamente. La crueldad no es intencional en su tono — simplemente no le parece relevante, es solo un detalle operativo más.

### Mecánica de manipulación: la duda sembrada (antes de decidir)
En puntos concretos de la partida, la comandante IA le plantea a un jugador (a cualquiera de los dos, es simétrico) una duda deliberada sobre las intenciones del otro — **siempre en algún momento, sea cierto o no**. Ejemplos:
- Duda de traición directa: *"¿Seguro que irás por ahí? ¿Qué tal que el Guía esté intentando que mueras para salvarse él?"*
- Duda existencial/de vínculo: *"¿Por qué te arriesgas por alguien que casi no conoces?"*
- Duda de motivación: *"¿De verdad quieres hacer esto, o solo lo haces porque no sabes qué más hacer?"*

### Mecánica de manipulación: la culpa repartida (después de un fracaso)
Si uno de los dos muere, la comandante IA le dice al sobreviviente que la culpa fue del otro, que "no fue suficiente" — **sin que esto sea necesariamente cierto**. Es el mismo mecanismo de manipulación que la duda sembrada, aplicado en el otro extremo de la experiencia: antes del hecho, siembra sospecha; después del hecho, reparte culpa. Ninguna de las dos es necesariamente verdad — es la misma herramienta de manipulación psicológica, usada en ambos momentos.

---

## 8. Finales (tres posibles)

**Monto de referencia:** cada sujeto firmó por **$50.000.000 COP**. Si ambos sobreviven, cada uno se lleva sus $50 millones. Si solo uno sobrevive, se lleva los $100 millones completos.

1. **Solo uno cruza con vida** (el otro murió en algún punto, o A decide salir solo en el Acto 2 sin volver por B): la sesión termina de inmediato, el sobreviviente es desconectado y extraído. La comandante IA le informa que la culpa fue del otro y que, por default contractual, se lleva el pago completo de ambos ($100.000.000 COP). Al despertar en el laboratorio real, ve el cuerpo sin vida de su compañero justo al lado. "Gana", pero mal.
2. **Ambos cruzan con vida:** solo posible si A vuelve por B en el Acto 2, logra abrir la puerta con la llave/tarjeta, y ambos consiguen llegar juntos hasta la salida. Cada uno se lleva su propio pago acordado ($50.000.000 COP). Es el único final de cooperación total y el único en el que ambos "ganan" limpiamente. Incluye una escena de despertar juntos en el laboratorio real, viéndose por primera vez fuera de la simulación.
3. **Ambos mueren:** este final **solo puede ocurrir en el Acto 2**, específicamente durante el intento de regreso —si A decide volver por B, abre la puerta, y algo sale mal en el trayecto compartido de vuelta a la salida. Fuera de esta ventana específica, la muerte de un jugador siempre termina la sesión de inmediato para el otro (final 1) — la doble muerte es exclusiva de este momento de reunión y regreso conjunto.

---

## 9. Traición (simétrica, con incentivo económico real)

Traicionar no es solo una decisión narrativa: tiene una recompensa concreta y tentadora — **el doble de pago si el otro no llega vivo a la salida.**

- **El Corredor** puede quedarse con los materiales que encuentra en vez de enviárselos al Guía, usándolos para mejorarse a sí mismo (más velocidad, resistencia al ruido, recuperación propia) en vez de mantener vivo al Guía.
- **El Guía** puede dejar de ayudar activamente (sin gastar voluntad, sin arriesgar su propia vida por el otro), conservando sus recursos para sí mismo y asegurando su propia supervivencia con más margen.

En ambos casos, dejar de cooperar es una apuesta calculada y arriesgada, no una estrategia segura: si el Guía muere por falta de materiales, o el Corredor muere por falta de apoyo, quien traicionó puede quedarse sin nada si calculó mal el momento.

**No hay ninguna señal explícita de "te están traicionando".** Ninguno de los dos jugadores puede saber con certeza qué está haciendo el otro en tiempo real — solo lo descubren de forma orgánica, a través de la consecuencia: el Corredor se da cuenta cuando empieza a recibir daño sin apoyo o está en peligro real sin ayuda del Guía; el Guía se da cuenta cuando deja de recibir materiales del Corredor. No hace falta ningún indicador de UI adicional — el propio sistema de vida/voluntad ya funciona como la señal.

---

## 10. Puntos a considerar

| Sistema | Herramienta / enfoque | Nivel de complejidad |
|---|---|---|
| Monstruos con percepción auditiva | AI Perception Component (nativo de Unreal) | Baja-media, con tutoriales estándar |
| Ruido por superficie | Physical Surface Types + Noise Emitter | Baja |
| Voz real → ruido en el mundo | Audio Capture Component (RMS) | Media |
| Voz real entre jugadores en LAN | Voice Chat / Vivox (integrado en Unreal) | Baja-media |
| Mapa esquemático del Guía | UI simple + tracking de posición 2D | Baja |
| Sistema de vida + voluntad (Guía) | Variables separadas + triggers de ayuda/daño/crafteo | Baja-media |
| Sistema de vida + vidas (Corredor) | Variable de daño + contador de vidas | Baja |
| Comandante IA / diálogo de duda y culpa | Líneas grabadas + triggers de tiempo/progreso, no condicionadas al comportamiento real | Baja-media |
| Diseño de puzzles con backtracking | Level design + triggers de progreso | Media (depende de cuántos puzzles se diseñen) |
| Escena de reencuentro / despertar en laboratorio | Cinemática simple + cambio de escena | Baja-media |

Ningún sistema requiere machine learning real ni mecánicas inventadas desde cero — todo se apoya en herramientas nativas de Unreal o en lógica simple de variables y triggers, lo cual deja espacio real para pulir los puzzles y la narrativa, que son el corazón de la experiencia.

---

## 11. Pendiente por definir (próximos pasos del equipo)

- Nombre de trabajo del juego.
- Estructura de niveles/capítulos concretos de cada laberinto (cuáles tienen monstruos, cuáles son puramente puzzle/exploración).
- Diseño concreto de los puzzles (tipos, cantidad, curva de dificultad) en ambos laberintos y ambos cuartos del Guía.
- Guion completo de la comandante IA: discurso inicial (propósito, reglas de pago, y la muerte real), líneas de duda sembrada, líneas de culpa repartida, y el monólogo de la escena del falso final.
- Diseño visual/tono estético de los laberintos, los cuartos del Guía, el laboratorio real y los monstruos.
- Balance numérico: velocidad de decaimiento de vida del Guía, costo de voluntad por acción, velocidad de regeneración, tamaño de la barra y número de vidas del Corredor.
- Decidir si el objeto que abre la puerta del cuarto del nuevo Guía en el Acto 2 es una llave física o una tarjeta de acceso.
- Diseño concreto del tramo de regreso conjunto en el Acto 2 (qué hace que puedan morir los dos, cómo se balancea esa dificultad).
- Decisión final sobre escalar a 3+ jugadores y/o online (visión a futuro, no para esta entrega).

---

## Disclaimer sobre el uso de IA en el desarrollo

Durante el desarrollo de este proyecto, el equipo utilizará herramientas de IA (Claude / Claude Code) para prototipar sistemas, resolver dudas técnicas, y acelerar el desarrollo en general. Este uso es una herramienta de apoyo al proceso de creación, y es independiente de la IA como personaje/sistema narrativo dentro del propio juego (la comandante IA), que es lo que corresponde a la temática "Videojuegos + IA" del evento.

---

*Documento de trabajo — versión actualizada tras varias rondas de discusión de equipo. Todo lo anterior es una propuesta de partida, sujeta a ajuste según feedback del grupo.*
