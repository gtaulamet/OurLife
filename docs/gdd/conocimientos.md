# Conocimientos y scriptorium (IP4)

Parte de la spec de IP4 (`docs/gdd/oficios.md`): el árbol de conocimientos medieval, cómo se ganan (investigando en el scriptorium y practicando) y qué desbloquean. Es la base del progreso de épocas de `docs/gdd/eras.md` (el gancho de unas 10 h que el plan reservó para IP4).

**Estado:** propuesta de O1, pendiente de aprobación. Números iniciales, a ajustar en O11.

## Qué hay hoy

`KnowledgeDef` (vacío), `Def.Requires`, `UnlockService.IsUnlocked(def, progress)` y `ProgressState.Knowledge` (conjunto ordenado, guardado y en la huella). No hay conocimientos definidos, ningún comando que los gane ni ninguna pantalla. Todo el contenido actual (IP1 a IP3.5) no tiene `requires`: sigue disponible desde el primer día.

## Reglas

- Un **conocimiento** es un def `Knowledge`. Su `requires` son los conocimientos que hay que tener antes de poder aprenderlo (el mismo campo que ya valida `DefDatabase`). Su `era` dice desde qué época existe; los de IP4 son todos `medieval`.
- La colonia aprende **por colonia**, no por colono: lo que se sabe vale para todos.
- Se gana por dos caminos que suman al mismo avance:
  1. **Investigar** en un scriptorium: un colono con el trabajo `research` suma puntos al conocimiento en cabeza de la cola.
  2. **Practicar:** trabajar en algo relacionado suma puntos, sin cola ni scriptorium, hasta el 60 % del costo. El resto sólo se gana investigando.
- Un conocimiento está **bloqueado** (faltan sus `requires`), **disponible** (se puede investigar o practicar), **en cola**, **en curso** (el primero de la cola) o **aprendido**.
- **Sólo avanzan** los disponibles: no se practica lo que todavía no se puede aprender, para que un oficio no se aprenda antes de tener la base.

### Puntos

- Un punto es un tick de trabajo de investigación a velocidad 100 %. Un colono investigando suma, en cada tick que avanza su trabajo, `100 × velocidad / 100` centésimas de punto: la velocidad cuenta la habilidad de investigación, la etapa, la diligencia y el ánimo, como en cualquier trabajo.
- **Costo:** en puntos. Un conocimiento de 3000 puntos lo investiga un colono de nivel 10 en unas 50 horas del juego (poco más de 2 días); con un escritorio por colono, se reparte.
- **Práctica:** cada tick de trabajo de un tipo (el que el conocimiento declara en `practice.workType`) suma `centipointsPerTick` centésimas de punto a cada conocimiento disponible que lo declare. El tope total de práctica es `practiceCapPercent` (60) del costo. Hacer un hacha, por ejemplo, no se practica: lo que se practica es talar o minar.
- **Cola de investigación:** hasta 5 conocimientos, que el jugador ordena. La investigación sólo avanza el primero. Al aprenderse, sale de la cola. Si el primero ya está completo por práctica, se aprende en el próximo tick y pasa al siguiente.
- **Aprender:** al llegar la suma de investigación y práctica al costo, el conocimiento pasa a `ProgressState.Knowledge`, el Core avisa con `KnowledgeLearnedEvent` y con un aviso en el registro, y todo lo que lo pedía queda disponible.

### Qué desbloquea

Todo def puede declarar `requires` (ya existe). `UnlockService.IsUnlocked` decide, y desde IP4 también deciden:

| Dónde | Qué pasa si está bloqueado |
|---|---|
| Barra de construcción | La entrada se ve atenuada y dice "Requiere: ..." al apoyar el cursor |
| `PlaceBlueprintCommand` | El Core lo rechaza (la presentación no es la que decide) |
| Recetas | La orden de producción queda en estado `locked` y no se ejecuta; la lista del taller muestra la receta atenuada |
| Cultivos | La zona de cultivo no lo ofrece |
| Muebles, materiales | Igual que la barra de construcción |

- `UnlockService.Missing(def, progress)` devuelve los conocimientos que faltan, para los textos de arriba. `UnlockService.Unlocks(knowledge)` devuelve los defs que un conocimiento abre, para el panel.
- **Conocimientos iniciales:** el `Scenario` suma `startingKnowledge: ["carpentry", "cooking"]`. Una partida anterior los recibe al migrar (formato 15).
- **Sin retroceso:** un conocimiento aprendido no se pierde (backlog: colapsos, IP9).

## El scriptorium

- Es un `Buildable` (taller de una celda, ver `oficios.md`) con `workstation: { workType: "research", job: "research" }`. No necesita ningún conocimiento. Cuesta 6 tablones y 240 ticks de trabajo: la llegada trae 16 tablones, y una partida anterior los hace en el banco de carpintero.
- **Trabajo `research`:** un colono con prioridad en Investigación, con un scriptorium libre, un conocimiento en cabeza de la cola y sin una orden o necesidad que valga más, va al escritorio y trabaja por tandas de `researchChunkTicks` (60 ticks). Cada tanda suma sus puntos y vuelve a decidir, así que otra orden más importante lo interrumpe sin perder nada. No consume objetos (decisión 5).
- **Un escritorio, un investigador:** más escritorios, más colonos a la vez.
- **Sin cola o sin escritorio**, nadie va a investigar; la práctica sigue sumando.
- La habilidad de investigación se gana investigando (1 punto por tick, por la inteligencia).

## El árbol medieval (contenido de IP4)

Doce conocimientos en tres ramas (oficios, campo, fuego y metal). `Costo` en puntos; `Práctica` es `workType` y centésimas de punto por tick.

| Id | Nombre | Rama | Costo | Requiere | Práctica | Desbloquea |
|---|---|---|---|---|---|---|
| `carpentry` | Carpintería | Oficios | inicial | | | Banco de carpintero; recetas de tablón y mango |
| `cooking` | Cocina | Oficios | inicial | | | Cocina; receta del potaje |
| `stonecutting` | Cantería | Oficios | 1500 | | mining 50 | Banco de cantero; bloque de piedra; sillería |
| `joinery` | Ebanistería | Oficios | 2500 | carpentry | carpentry 40 | Mesa larga |
| `agriculture` | Agricultura | Campo | 2000 | | gathering 80 | Zonas de cultivo; repollo y guisantes |
| `cereals` | Cultivo de cereales | Campo | 2500 | agriculture | farming 50 | Trigo |
| `milling` | Molienda y panadería | Campo | 3000 | cereals, cooking | cooking 40 | Recetas de harina y pan |
| `food_preservation` | Conservación de alimentos | Campo | 3000 | cooking | cooking 40 | Receta del encurtido |
| `silviculture` | Silvicultura | Campo | 2000 | agriculture | woodcutting 30 | Zonas de bosquecillo (árboles sembrados) |
| `charcoal_burning` | Carbonería | Fuego y metal | 2500 | stonecutting | woodcutting 30 | Carbonera; receta del carbón |
| `metallurgy` | Fundición | Fuego y metal | 4000 | charcoal_burning | | Horno de fundición; receta del lingote |
| `ironwork` | Herrería | Fuego y metal | 5000 | metallurgy | | Fragua; herramientas de hierro |

- **Total:** 28 000 puntos de investigación en los diez conocimientos que se aprenden, unos 19 días del juego de un investigador de nivel 10 a tiempo completo (un tercio del año de 60 días). La práctica le ahorra hasta el 60 % del costo en ocho de ellos, y más escritorios lo reparten.
- **Motivos:**
  - Carpintería y cocina son iniciales para que el primer día se pueda amoblar y cocinar (con los 16 tablones iniciales y las provisiones).
  - Las ramas de campo y de fuego se abren cuando la aldea tiene un motivo: comida segura antes de los metales.
  - Los costos van de 1500 (lo primero) a 5000 (la herrería, lo último): una herrería en el primer año exige poner un investigador casi a tiempo completo.
  - Los conocimientos de fuego y metal no se practican porque no se pueden hacer antes de saberlos.
- **Épocas:** todos llevan `era: medieval`. El árbol deja el lugar de `printing_press` y `gunpowder` (IP9) como conocimientos de otra época: un test comprueba que un conocimiento con `era` posterior queda bloqueado en la Edad Media aunque se cumplan sus `requires` (ADR-008). No hay cambio de época en IP4.

```json
{ "type": "Knowledge", "id": "metallurgy", "era": "medieval", "requires": ["charcoal_burning"],
  "branch": "fire_metal", "tier": 3, "cost": 4000, "order": 10 }
```

```json
{ "type": "Knowledge", "id": "cereals", "era": "medieval", "requires": ["agriculture"],
  "branch": "field", "tier": 2, "cost": 2500, "order": 5,
  "practice": { "workType": "farming", "centipointsPerTick": 50 } }
```

| Campo | Qué es |
|---|---|
| `branch` | Rama del panel (`crafts`, `field`, `fire_metal`) |
| `tier` | Columna del panel |
| `cost` | Puntos; cero en los iniciales |
| `practice` | Opcional: `{ workType, centipointsPerTick }` |
| `order` | Lugar dentro de su rama |

`KnowledgeConfig` (`knowledge.json`): `practiceCapPercent` 60, `researchChunkTicks` 60, `queueMax` 5.

## Contrato

| Tipo | Nombre | Uso |
|---|---|---|
| Comando | `SetResearchQueueCommand(lista)` | El jugador pone, quita y reordena la cola (hasta `queueMax`, sólo conocimientos disponibles) |
| Estado | `Progress.Knowledge`, `Progress.ResearchQueue`, `Progress.KnowledgePoints` (investigación y práctica por conocimiento, en centésimas) | Entran en el guardado y en la huella |
| Trabajo | `JobKind.Research` | Ir al escritorio y trabajar una tanda |
| Evento | `KnowledgeLearnedEvent(conocimiento)` | Registro, pantalla del panel y desbloqueos |
| Evento | `ResearchChangedEvent` | La cola o el progreso cambiaron (a lo sumo una vez por hora del juego, para no saturar) |
| Evento | `NotificationEvent` | Aviso de aprendido |
| Consulta | `ResearchRules.State(conocimiento)`, `ResearchRules.Progress(conocimiento)` | Panel: estado y avance en tantos por diez mil |
| Consulta | `UnlockService.Missing`, `UnlockService.Unlocks` | Textos de bloqueo y panel |

- Flujos: `research` no usa aleatoriedad.
- Un conocimiento guardado que el contenido ya no tiene da "falta contenido" (ADR-013).

## Panel de conocimientos

- Se abre con **K**; vuelve a cerrar con K o Esc. Una fila por rama (tres) y una columna por nivel. Cada tarjeta muestra nombre, costo, barra de avance (investigación y práctica en tonos distintos) y estado (aprendido, disponible, en cola con su posición, bloqueado con "Requiere: ..."). Al apoyar el cursor: descripción y qué desbloquea. Clic en un disponible lo suma a la cola; clic derecho lo saca.
- Al lado, la cola con flechas para reordenar y la cantidad de escritorios y de investigadores activos. Sin escritorio, un aviso lo dice.
- El HUD muestra, junto a las existencias, un ícono de investigación con el conocimiento en curso y su avance, sólo si hay cola.
- Hace falta un ícono por rama y uno por estado (pixel 16 × 16, como los de interacciones).

## Textos nuevos (propuesta)

| Clave | es | en |
|---|---|---|
| `def.knowledge.{id}.name`/`desc` | Los 12 de la tabla (ver abajo) | |
| `def.knowledge_branch.{crafts,field,fire_metal}.name` | Oficios, Campo, Fuego y metal | Crafts, Field, Fire and metal |
| `ui.knowledge.title` | Conocimientos | Knowledge |
| `ui.knowledge.state.{learned,available,queued,blocked}` | Aprendido; Disponible; En cola ({position}); Bloqueado | Learned; Available; Queued ({position}); Locked |
| `ui.knowledge.cost` | {points, number} puntos | {points, number} points |
| `ui.knowledge.requires` | Requiere: {knowledge} | Requires: {knowledge} |
| `ui.knowledge.unlocks` | Desbloquea: {items} | Unlocks: {items} |
| `ui.knowledge.queue` | Cola de investigación | Research queue |
| `ui.knowledge.no_scriptorium` | Hace falta un scriptorium para investigar. | A scriptorium is needed to research. |
| `ui.knowledge.practice` | Se avanza trabajando: {work_type} | Progresses by working: {work_type} |
| `ui.hud.research` | Investigando: {knowledge} ({percent}) | Researching: {knowledge} ({percent}) |
| `event.knowledge.learned` | La aldea aprendió {knowledge}. | The village learned {knowledge}. |
| `job.research.report` | Investigando {knowledge} | Researching {knowledge} |
| `ui.orders.locked_hint` | Requiere: {knowledge} | Requires: {knowledge} |

Nombres de los 12 conocimientos (es / en): Carpintería / Carpentry, Cocina / Cooking, Cantería / Stonecutting, Ebanistería / Joinery, Agricultura / Agriculture, Cultivo de cereales / Cereal farming, Molienda y panadería / Milling and baking, Conservación de alimentos / Food preservation, Silvicultura / Silviculture, Carbonería / Charcoal burning, Fundición / Smelting, Herrería / Ironwork.

## Modelos y arte

- `furniture.scriptorium` (ver `oficios.md`).
- Íconos de rama y de estado de conocimiento, y el del HUD (pixel, con `tools/make-*-icons.py` como los de interacciones).
- No hay modelo de conocimiento: sólo UI.

## Criterios de aceptación

1. **Gana por investigar.** Escenario: un scriptorium, un investigador de nivel 10 (velocidad 100 %) y `stonecutting` en la cola, sin práctica ⇒ a los 1500 ticks de trabajo el conocimiento se aprende, sale de la cola y emite `KnowledgeLearnedEvent`; un investigador de nivel 0 (velocidad 80 %) tarda 1875.
2. **Gana practicando.** Escenario: colonos minan 3000 ticks sin cola ni escritorio ⇒ `stonecutting` llega al 60 % (900 de 1500) y no pasa de ahí; con investigación completa el resto.
3. **Sólo los disponibles.** `cereals` no avanza por práctica ni por investigación antes de `agriculture`; al aprenderse `agriculture`, empieza.
4. **Desbloqueos.** Antes de aprender `stonecutting`, `PlaceBlueprintCommand` del banco de cantero y una orden de producción de bloques se rechazan o quedan en `locked`; después, funcionan.
5. **Cola.** `SetResearchQueueCommand` rechaza un conocimiento bloqueado, uno ya aprendido o una cola de más de 5; reordenar cambia cuál avanza.
6. **Trabajo.** Un colono con Investigación en "nunca" no investiga; un adolescente sí; un niño no.
7. **Un escritorio, un investigador.** Dos colonos con un solo escritorio no investigan juntos; con dos, sí.
8. **Interrupción.** Una necesidad crítica corta la tanda sin perder los puntos ya sumados.
9. **Época.** Un `Knowledge` o un `Buildable` con `era: modern` queda bloqueado en `medieval` aunque tenga sus `requires` (test de contenido de prueba, ADR-008).
10. **Guardado.** El test de continuidad incluye una cola con dos conocimientos y avance de práctica; una partida de `v0.3.5` carga con los dos iniciales (formato 15).
11. **En la partida.** Panel de conocimientos en es y en, un conocimiento aprendido que habilita un taller, y la barra de construcción atenuando lo bloqueado, con capturas.
12. **Presupuesto.** La práctica y la investigación suman menos de 0,05 ms por tick.

## Fuera de alcance

Van a `docs/ip/backlog.md`: pergamino, tinta y libros como objetos (IP5, con la fauna), enseñar un conocimiento a otra aldea (IP7), biblioteca con estantes que aceleran la investigación, escuela (IP10), retroceso de conocimientos, y el cambio de época (IP9: la época avanza al reunir conocimientos clave, ver `eras.md`).

## Fuentes

- Los scriptoria medievales eran lugares de copia y custodia del saber, con los trabajos repartidos entre preparar las pieles, copiar e iluminar. [Wikipedia, Scriptorium](https://en.wikipedia.org/wiki/Scriptorium) y [Catholic Encyclopedia, Scriptorium](https://www.catholic.com/encyclopedia/scriptorium).
- Rotación de cultivos de tres campos, con la mitad del año de trigo o centeno y el resto de cebada, avena o legumbres, y guisantes y habas que fijan nitrógeno. [Britannica, Three-field system](https://www.britannica.com/technology/three-field-system).
- Los costos del árbol y las prácticas son de jugabilidad.
