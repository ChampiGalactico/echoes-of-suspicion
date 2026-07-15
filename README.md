# Echoes of Suspicion

> Juego cooperativo de puzzles y exploración para 2 jugadores (LAN), desarrollado para el Summer Camp "Videojuegos + IA".

## Concepto
Dos personas firman para ser sujetos de prueba en un experimento psicológico dirigido por una IA, con la finalidad declarada de analizar la naturaleza del egoísmo y la confianza humana — para desarrollar técnicas que debiliten la confianza y la cohesión entre personas (útil en contexto militar-académico para interrogatorios, contrainteligencia, control de grupos). El experimento resulta ser una simulación mental compartida: deben cooperar si quieren terminarlo con "éxito". Desde el inicio de la simulación conocen las reglas: si ambos sobreviven, cada uno cobra su pago; si solo uno sobrevive, se lleva el pago de los dos; y si alguno muere ahí dentro, muere también en la vida real.

Uno de los jugadores es el **Guía** (ve el mapa, no ve el entorno real, investiga y craftea desde su cuarto). El otro es el **Corredor** (explora el entorno real con visión limitada, se comunica solo por altavoz físico — su voz real genera ruido real que las criaturas detectan). Juntos deben resolver puzzles, sobrevivir, y decidir hasta dónde llega su cooperación cuando traicionar es más rentable.

**La comandante IA** de la instalación mide, con humor ácido y total indiferencia, qué tan fácil es romper la confianza humana — y no solo observa el egoísmo, lo provoca.

## Estructura del juego

- **2 actos**, con **intercambio de roles** entre uno y otro.
- Cada acto = **3 biomas × 3 puzzles + 1 mini-puzzle final compartido** (estilo *It Takes Two*).
- **Personajes jugables con arquetipo propio:** los biomas y puzzles de cada acto están basados en la psiquis del personaje que ejerce de Guía en ese acto. Cambiar de personaje = cambiar de mundo, no solo de habilidades. Los mini-puzzles finales de cada acto son los mismos para todos los personajes.
- **Meta del summer camp:** 2 personajes jugables (1M + 1F). Visión final: 4 personajes.
- **3 finales posibles**, determinados por la doble decisión de traición al final del Acto 2.

## Documentos del proyecto

- **`Propuesta_Juego_Summer_Camp.md`** — Documento conceptual: premisa, roles, pilares, sistema de vida/voluntad, personajes y arquetipos, estructura de dos actos, finales, la comandante IA, y disclaimer del uso de IA.
- **`Propuesta_Tecnica_Juego_Summer_Camp.md`** — Documento técnico: tipos de sala, criaturas, capacidades específicas de cada rol, biomas dependientes del personaje, y plantilla de referencia de diseño de un acto.
- **`/referencias`** *(sugerido)* — Imágenes y juegos de referencia (estética de biomas, mecánicas similares, mood boards).

## Referencias de diseño

- **Escape Academy** — comunicación cooperativa bajo presión, resolución de puzzles con información asimétrica.
- **It Takes Two** — mecánicas asimétricas entre dos jugadores, y momentos físicos compartidos que combinan habilidades distintas.
- **Maze Runner** (película) — estética y premisa del laberinto, sensación de instalación que oculta más de lo que muestra.
- **Dead by Daylight** — tensión superviviente-cazador, gestión de riesgo frente a una amenaza que patrulla el entorno.
- **The Forest** — supervivencia y crafteo en un entorno hostil, escasez de recursos como motor de tensión.

---

## Pendientes — Narrativa

- [ ] **Diseño de los 2 personajes iniciales** (1M + 1F): nombre, arquetipo de personalidad, historia breve, posiblemente profesión, y descripción de su mundo interior que define sus 3 biomas.
- [ ] Guion completo de la comandante IA: discurso inicial (propósito, reglas de pago, muerte real), líneas de duda sembrada, líneas de culpa repartida, monólogo del falso final del Acto 1, y reacciones a los 3 finales del Acto 2.
- [ ] Diálogo específico de la recepcionista en la Escena 0.
- [ ] Reportes de laboratorio de las criaturas — solo está redactado el ejemplo de referencia de la Criatura 1.
- [ ] Contenido de las "historias random" del cuaderno de campo de cada personaje.
- [ ] Diseño narrativo de la doble puerta al final del Acto 2 (salida real vs. cuarto de control), y de cómo se ve cada uno de los 3 desenlaces.

## Pendientes — Mecánicas

- [ ] Habilidades específicas de cada personaje (fuerza/resistencia/visión/otros boosts) y cómo interactúan con el intercambio de roles.
- [ ] Diseño de los **3 biomas + 9 puzzles de cada personaje** (según su arquetipo), con easter egg opcional por bioma.
- [ ] Diseño de los **2 mini-puzzles finales compartidos** (estilo It Takes Two): uno para el falso final del Acto 1, otro para el final real del Acto 2 cuando ambos cooperan. Iguales para todas las combinaciones de personajes.
- [ ] Diseño concreto del **sistema de doble decisión de traición** al final del Acto 2: cómo el Guía elige cerrar/no cerrar la salida real, cómo el Corredor elige volver/no volver, sin saber lo que hace el otro.
- [ ] Diseño visual y diegético del **punto de envío/recepción de cada bioma** (¿mismo objeto físico repetido, o diseño único por bioma?).
- [ ] Balance numérico: velocidad de decaimiento de vida del Guía, costo de voluntad por acción, regeneración de voluntad, tamaño de la barra y número de vidas del Corredor.
- [ ] Radio y velocidad de detección de ambas criaturas (sonido y luz), y patrones de patrullaje.
- [ ] Decidir si la linterna tiene límite de batería/uso propio.
- [ ] Cantidad exacta de batería de la cámara crafteable, y cuántos materiales de criatura derrotada se necesitan para craftear una batería adicional.

## ❓ Preguntas y vacíos abiertos

- ¿Existen más de 2 tipos de criatura a futuro, o el juego se mantiene con solo Criatura 1 (sonido) y Criatura 2 (luz) para esta versión del summer camp?
- ¿El "Receptor" del Guía tiene alguna representación visual/diegética específica, o se deja para la fase de arte?
- Escalar a 3+ jugadores y/o modo online: sigue como visión a futuro — ¿en algún punto de este summer camp se quiere dejar la arquitectura ya preparada para eso, o se aborda después de la entrega?
- **Prioridad de recorte:** si el tiempo aprieta, ¿priorizamos 1 acto completo (sin intercambio de roles) sobre 2 actos incompletos?

---

*Este README se actualizará a medida que se resuelvan los pendientes y avance el desarrollo.*
