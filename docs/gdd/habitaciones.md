# Habitaciones, dormitorios y comedor (IP4)

Parte de la spec de IP4 (`docs/gdd/oficios.md`): detección de habitaciones y su calidad por tamaño, muebles y materiales; dormitorios asignados a una persona o a una familia; comedor, y el lugar de reunión que `libre-albedrio.md` dejó para IP4. Todo suma pensamientos al ánimo de IP3 (`vida-y-generaciones.md`).

**Estado:** aprobada en O1. El Core está hecho en O9 (28 de octubre de 2026: detección, puntaje, dormitorios, familias, comedor y reunión, formato 19); la presentación también (28 de octubre de 2026: capa, inspector, lista, asignación de camas y de familia en la UI, colono sentado, íconos y capturas es/en). Números iniciales, a ajustar en O11.

## Qué hay hoy

- Cada cama tiene dueño o está libre (`muebles-y-camas.md`): un colono sin cama toma la libre más cercana al acostarse. Dormir en cama deja "Dormí en mi cama" (+3, 12 h); en el suelo, "Dormí en el suelo" (-3). Dos noches seguidas en el suelo despiertan el deseo de una cama propia.
- Mesa y silla son decorativas. Se come de una pila, parado.
- El libre albedrío busca compañía, pero no hay a dónde reunirse.
- No existe el concepto de habitación: los muros, las puertas y los muebles no se relacionan.

## Qué es una habitación

Una **habitación** es un espacio cerrado, dato derivado (ADR-011): se calcula a partir del mapa y los muebles y no se guarda.

1. **Búsqueda.** Desde una celda, un recorrido por las cuatro direcciones (no las diagonales) del mismo nivel, sobre celdas pisables.
2. **Límites.** Un muro (natural o construido) y una puerta cierran el paso. La celda de la puerta no pertenece a la habitación: es el límite. Un hueco sin suelo, una rampa, una escalera, un canal o el borde del mapa abren la habitación: el recorrido fuga.
3. **Tamaño.** Una habitación tiene entre `minCells` (4) y `maxCells` (150) celdas. Si el recorrido supera el máximo o fuga, es **exterior**: no es una habitación, y sus muebles no suman a nada.
4. **Techo.** No hace falta (construir un techo con las reglas de H2.4 es difícil en una casa sola; la temperatura y la lluvia, que lo piden, llegan con IP6). Cada habitación calcula `roofed` (todas sus celdas tienen algo sólido arriba), un dato que usan el deterioro de alimentos y, más adelante, el clima.
5. **Cavar vale.** Una cámara cavada en la roca con una puerta es una habitación, como una casa.
6. **Cuándo se recalcula.** Al cambiar un muro, una puerta, un suelo o un mueble, sólo la habitación afectada y las vecinas. Se pide bajo demanda (`Rooms.At(celda)`) y se guarda en caché con un número de versión del mapa.

### Funciones de una habitación

Cada habitación tiene una o más **funciones**, que salen de lo que contiene; una habitación puede tener varias (una casa con cama, mesa y silla es dormitorio y comedor). Def `RoomRole`:

| Función | Condición | Qué hace |
|---|---|---|
| `bedroom` dormitorio | Al menos una cama o cuna | Los pensamientos de dormir (abajo) |
| `dining` comedor | Al menos una mesa y una silla | Comer sentado y los pensamientos de comer; lugar de reunión |
| `workshop` taller | Al menos un taller | Sólo la etiqueta; los talleres restan belleza al resto |
| `storage` almacén | La mitad de las celdas son de almacenes, o al menos dos cofres | Sólo la etiqueta |

La condición se escribe por etiqueta de mueble (`Buildable.tags`): cama y cuna `bed`; mesa y mesa larga `table`; silla `seat`; talleres `workshop`; cofre `chest`. Así los muebles de otras épocas se suman con datos.

## Calidad de una habitación

`puntaje = tamaño + muebles + materiales`, con los números en `RoomConfig`:

| Término | Cuenta | Número inicial |
|---|---|---|
| Tamaño | Una por celda, hasta `sizeCap` | 1 por celda, tope 36 |
| Muebles | La `beauty` de cada mueble por el `valuePercent` de su calidad (`oficios.md`) | Cama 4, cuna 3, mesa 3, mesa larga 5, silla 2, cofre 2, scriptorium 1, cocina 0, bancos -1, carbonera -2, horno de fundición y fragua -4 |
| Suelo | La `beauty.floor` del material de cada celda | Tierra y roca 0, madera 1, mampostería 1, tablones 2, sillería 3 |
| Paredes | La `beauty.wall` de cada pieza del límite (muro o puerta) | Roca natural 0, madera 0, mampostería 1, tablones 1, sillería 2 |

| Nivel | Puntaje | Motivo |
|---|---|---|
| `miserable` mísera | menos de 8 | Un cuarto de 4 celdas con una cama (8) ya sale de aquí |
| `poor` pobre | 8 a 17 | Una cama sola en una casa chica de tierra |
| `decent` decente | 18 a 35 | Una casa de 3 × 3 con cama, mesa, silla y cofre (20) |
| `comfortable` cómoda | 36 a 60 | La misma casa con suelo de tablones y muros de mampostería (54) |
| `luxurious` lujosa | 61 o más | Sillería en todo (79), o mucho mobiliario bueno en un salón |

- **Ejemplo:** una casa de cuatro muros de 5 × 5 (interior de 3 × 3, 16 piezas de límite) con cama, mesa, silla y cofre de calidad normal: 9 + 11 = 20 (decente). Con tablones en el piso (18) y muros de mampostería (16): 54 (cómoda). Con sillería en el piso (27) y los muros (32): 79 (lujosa).
- Un taller dentro de una habitación resta, así que un dormitorio con fragua no es lujoso. No es un error: es la reputación de la fragua.
- El nivel y el puntaje se ven en el inspector y en la lista de habitaciones.

```json
{ "type": "RoomLevel", "id": "decent", "order": 2, "from": 18,
  "bedroomThought": "bedroom_decent", "diningThought": "dining_decent" }
```

```json
{ "type": "RoomConfig", "id": "default", "minCells": 4, "maxCells": 150,
  "sizePointsPerCell": 1, "sizeCap": 36,
  "diningRadius": 25, "gatherRadius": 30, "gatherChancePercent": 50,
  "gatherFromHour": 18, "gatherToHour": 22, "gatherTicks": 180,
  "crowdedBeds": 3 }
```

`Material` suma `beauty: { wall, floor }` y `Buildable` suma `beauty` y `tags`.

## Dormitorios

- Una cama con dueño vivo (`Bed.Owner`) sólo la usa su dueño, y la asignación automática de camas libres la saltea; lo nuevo es que el jugador puede fijar el dueño. No hay un indicador aparte de «reservada»: si el dueño muere o deja de existir, la cama queda libre sola.
- `AssignBedCommand(cama, colono?)` reserva la cama para el colono, o la libera si no hay colono. Si el colono ya tenía otra cama, la deja libre. Se rechaza si la etapa del colono no puede usar la cama (`forStages`) o si el colono no existe.
- **Asignar una casa:** al seleccionar una habitación con camas, el panel muestra la lista de camas con su dueño y un botón **Asignar familia**: se elige un colono y la UI manda un `AssignBedCommand` para él y para su pareja, sus hijos vivos y sus hermanos, cada uno a una cama de la habitación según `RoomRules.SuggestAssignments(habitación, colono)`, que prefiere las camas de la etapa de cada uno (la cuna para un bebé). La consulta es del Core; la lógica de elegir no va en la UI.
- **Pensamientos al despertar** (se suman al "Dormí en mi cama" de IP3):

| Pensamiento | Ánimo | Dura | Cuándo |
|---|---|---|---|
| `bedroom_miserable` Dormitorio mísero | -2 | 12 h | Despierta en una cama dentro de una habitación mísera |
| `bedroom_decent` Dormitorio decente | +1 | 12 h | Decente |
| `bedroom_comfortable` Dormitorio cómodo | +3 | 12 h | Cómoda |
| `bedroom_luxurious` Dormitorio lujoso | +5 | 12 h | Lujosa (el nivel pobre no deja pensamiento) |
| `family_home` Dormí en casa con mi familia | +2 | 12 h | La habitación tiene al menos 2 camas con dueño y todos sus dueños forman una sola familia: cada uno es pariente de otro (pareja, padres, hijos o hermanos) y, yendo de pariente en pariente, se llega de cualquiera a todos los demás. Dos hermanos y una pareja ajena en la misma habitación son dos grupos, no una familia: cuentan como extraños |
| `slept_crowded` Dormí amontonado | -2 | 12 h | La habitación tiene `crowdedBeds` (3) o más camas con dueño y alguno de los dueños no es pariente de ningún otro |

- Una cama fuera de toda habitación (a cielo abierto) sólo deja "Dormí en mi cama".
- El máximo de un buen hogar es +10 (cama 3, lujosa 5, familia 2). Un barracón de 7 camas asignadas a no parientes cuesta -2: es lo que empuja a hacer más de una casa.
- El deseo `own_bed` no cambia. Un deseo de "un dormitorio propio" queda en el backlog.

## Comedor

- **Comer sentado.** Un colono con hambre baja que elige qué comer (`agricultura.md`) busca una silla libre de un comedor a `diningRadius` (25) celdas o menos, alcanzable, con una mesa al lado (en las cuatro direcciones). Toma la comida, la lleva a la silla, se sienta (la silla se pisa) y come. Una silla ocupada la reserva su ocupante.
- **Sin comedor o con hambre crítica:** come parado, de la pila, como hoy. No hay castigo por comer sin mesa (decisión 6): el pensamiento es sólo un premio.
- Una silla puede estar junto a más de una mesa. Una mesa larga permite más sillas.
- **Pensamientos al terminar de comer:**

| Pensamiento | Ánimo | Dura | Cuándo |
|---|---|---|---|
| `ate_at_table` Comí a la mesa | +2 | 8 h | Comió sentado en un comedor |
| `dining_miserable` Comedor mísero | -1 | 8 h | Mísero |
| `dining_decent` Comedor decente | +1 | 8 h | Decente |
| `dining_comfortable` Comedor cómodo | +2 | 8 h | Cómodo |
| `dining_luxurious` Comedor lujoso | +4 | 8 h | Lujoso (el nivel pobre no deja pensamiento) |
| `ate_meal` Comí un plato preparado | +3 | 8 h | De `agricultura.md`; no se suma |

- Ningún pensamiento de comida se suma consigo mismo (`stackLimit` 1).

## Lugar de reunión

Con la opción de libre albedrío prendida:

- Entre las `gatherFromHour` (18) y las `gatherToHour` (22), un colono que podría buscar compañía (`FreeWillRules.CanSeekCompany`) y no tiene nada que hacer elige, con probabilidad `gatherChancePercent` (50) por su sociabilidad, ir a un comedor con una silla libre a `gatherRadius` (30) celdas o menos. Va, se sienta (trabajo `Gather`, que espera igual que deambular o visitar) y se queda `gatherTicks` (180) ticks, o hasta que una necesidad o un trabajo lo llame.
- Allí las interacciones de siempre ocurren, porque el radio es de 6 celdas y están juntos: es lo que el libre albedrío esperaba de un lugar de reunión. No hay un pensamiento propio.
- Antes de la hora de reunión, la búsqueda de compañía sigue como en L4. Con la opción apagada no hay reunión.
- Un fogón o una fogata como lugar de reunión (afuera) queda en el backlog. Una cocina en el comedor no lo convierte en un lugar de reunión por sí misma.

## Lo que se ve

- **Capa de habitaciones:** un botón del HUD la prende. Pinta el piso de cada habitación según su nivel (tonos de una sola paleta, de apagado a dorado) y su función con un ícono; sin habitación, nada. Es sólo presentación.
- **Inspector de una celda con habitación:** "Dormitorio decente: 22 puntos, 3 camas" y, si hay dueños, "De Elvira Pérez y Nuño". El inspector de un colono dice dónde duerme ("Duerme en un dormitorio decente").
- **Lista de habitaciones:** un panel con las habitaciones por función, nivel, puntaje y dueños, para hacer clic y moverse (O10).
- **Colono sentado:** usa la pose `sit` que ya tienen los bebés, de cara a la mesa.
- No hay modelos nuevos (la mesa larga está en `oficios.md`). Sí íconos de funciones y niveles para la capa, de 16 × 16.

## Contrato

| Tipo | Nombre | Uso |
|---|---|---|
| Comando | `AssignBedCommand(cama, colono?)` | Reservar o liberar una cama |
| Estado | `Bed.Owner` (ya existía) | Guardado y huella; las habitaciones son derivadas (no se guardan) |
| Trabajos | `JobKind.EatAtTable`, `JobKind.Gather` | Comer sentado; reunirse |
| Evento | `RoomsChangedEvent` | La vista actualiza la capa |
| Consulta | `Rooms.At(celda)`, `Rooms.All()` | Habitaciones |
| Consulta | `RoomRules.Score`, `RoomRules.Level`, `RoomRules.Roles`, `RoomRules.Owners`, `RoomRules.SuggestAssignments` | Inspector, capa y panel |
| Defs | `RoomConfig`, `RoomLevel`, `RoomRole`, `Buildable.tags`, `Buildable.beauty`, `Material.beauty` | `rooms.json`, `buildables.json`, `materials.json` |
| Datos de ánimo | `MoodConfig` suma los pensamientos de dormir, de comer, `family_home` y `slept_crowded`; el nivel dice cuál (`RoomLevel.bedroomThought`, `diningThought`) | `mood.json` |

- Flujos aleatorios: ninguno para las habitaciones. La reunión usa el flujo `colony.free_will` para la probabilidad de ir (ver las notas de implementación).
- Las habitaciones no entran en la huella (derivadas); sí entran los pensamientos que dejan.
- Una partida de `v0.3.5` carga sin cambios: las habitaciones se calculan solas.

## Textos nuevos (propuesta)

| Clave | es | en |
|---|---|---|
| `def.room_role.{bedroom,dining,workshop,storage}.name` | dormitorio, comedor, taller, almacén | bedroom, dining room, workshop, storage |
| `def.room_level.{miserable,poor,decent,comfortable,luxurious}.name` | mísero, pobre, decente, cómodo, lujoso | miserable, poor, decent, comfortable, luxurious |
| `ui.inspector.room` | {role} {level}: {score, plural, one {# punto} other {# puntos}} | {level} {role}: {score, plural, one {# point} other {# points}} |
| `ui.inspector.room_owners` | De {names} | {names}'s |
| `ui.inspector.sleeps_in_room` | Duerme en un {role} {level} | Sleeps in a {level} {role} |
| `ui.rooms.overlay` | Habitaciones | Rooms |
| `ui.rooms.title` | Habitaciones | Rooms |
| `ui.rooms.assign_family` | Asignar familia | Assign family |
| `ui.rooms.assign_bed` | Asignar cama | Assign bed |
| `ui.rooms.free_bed` | Libre | Free |
| `job.eat_at_table.report` | Comiendo a la mesa | Eating at the table |
| `job.gather.report` | Reunido con los demás | Gathered with the others |
| `def.thought.{bedroom_miserable,bedroom_decent,bedroom_comfortable,bedroom_luxurious}.name` | Dormitorio mísero; Dormitorio decente; Dormitorio cómodo; Dormitorio lujoso | Wretched bedroom; Decent bedroom; Comfortable bedroom; Luxurious bedroom |
| `def.thought.family_home.name` | Dormí en casa con mi familia | I slept at home with my family |
| `def.thought.slept_crowded.name` | Dormí amontonado | I slept packed in |
| `def.thought.ate_at_table.name` | Comí a la mesa | I ate at the table |
| `def.thought.{dining_miserable,dining_decent,dining_comfortable,dining_luxurious}.name` | Comedor mísero; Comedor decente; Comedor cómodo; Comedor lujoso | Wretched dining room; Decent dining room; Comfortable dining room; Luxurious dining room |

Los nombres de nivel concuerdan en género con la función ("dormitorio decente", "habitación cómoda"): se escriben con `select` sobre el género de la función, que cada `RoomRole` declara.

## Criterios de aceptación

1. **Detección.** Una casa de cuatro muros de 5 × 5 con una puerta es una habitación de 9 celdas; sin la puerta, con un hueco en un muro, no lo es (fuga); un salón de 13 × 13 sin tabiques supera `maxCells` y es exterior.
2. **Cavar vale.** Una cámara cavada de 3 × 3 en la roca, con una puerta, es una habitación.
3. **Puntaje.** El ejemplo de la tabla da 20 (decente), 54 (cómoda) y 79 (lujosa); un banco de cantero en el cuarto resta 1 y una fragua, 4.
4. **Calidad del mueble.** Una cama de calidad excelente (150 %) suma 6; una pobre (75 %), 3.
5. **Dormitorio.** Un colono que despierta en una cama de una habitación decente tiene "Dormí en mi cama" y "Dormitorio decente" (+4 en total); en una casa a cielo abierto, sólo el primero.
6. **Reservas.** `AssignBedCommand` reserva una cama; otro colono sin cama no la toma; si el dueño muere, queda libre; reasignar libera la anterior; una cuna no se asigna a un adulto.
7. **Familia.** En una habitación con tres camas asignadas a una pareja y su hijo, los tres tienen `family_home`; si la tercera cama es de un colono que no es pariente, se dan `slept_crowded` en lugar de `family_home` a todos.
8. **Sugerencias.** `RoomRules.SuggestAssignments` da una cama de cada etapa que corresponde (la cuna para el bebé) sin repetir cama.
9. **Comedor.** Una habitación con una mesa y una silla es comedor; un colono con hambre baja y comida a mano va a la silla, come sentado y recibe "Comí a la mesa" y el pensamiento del nivel; con hambre crítica come donde está; sin comedor nada cambia en la huella (el smoke no tiene mesas con sillas).
10. **Sin mesa no hay castigo.** Un colono sin comedor no recibe pensamientos negativos por comer parado.
11. **Reunión.** Con la opción prendida, entre las 18 y las 22, hay colonos sentados en el comedor, y charlan; con la opción apagada, nadie va. El resto de la rutina (trabajo, necesidades, camas) no cambia.
12. **Cambios del mapa.** Derribar un muro de un dormitorio lo vuelve exterior en el siguiente tick; cerrarlo lo recupera, con el mismo nivel.
13. **Guardado.** Una cama asignada y una habitación con asignaciones se guardan y cargan con la misma huella; las habitaciones se recalculan sin guardarse.
14. **En la partida.** La capa de habitaciones, el inspector y la lista en es y en, un comedor con colonos sentados y una familia asignada a una casa, con capturas.
15. **Presupuesto.** Menos de 0,1 ms por tick de media con 20 habitaciones, 15 colonos y los cambios normales del mapa.

## Notas de implementación (Core de O9)

Lo que el código decidió donde la spec dejaba margen, o donde se corrigió:

- **Defs.** `RoomConfig`, `RoomLevel` (con `bedroomThought` y `diningThought`, que el nivel pobre no tiene) y `RoomRole` viven en `game/content/defs/rooms.json`. Un `RoomRole` dice qué pide con `needs` (todas: etiqueta y cantidad) y, el almacén, con `storageCellsPercent` (50): tiene la función si cumple los `needs` **o** si las zonas de almacén cubren ese porcentaje de sus celdas. Las etiquetas (`Buildable.tags`) son `bed` (cama y cuna), `table`, `seat`, `chest` y `workshop` (todo mueble con `workstation`). `MoodConfig` suma `ateAtTable`, `familyHome` y `sleptCrowded`. Los textos de `RoomLevel` y `RoomRole` van en `def.room_level.*` y `def.room_role.*`; el nombre de un nivel es un `select` sobre el género de la función.
- **Sin `RoomConfig` no hay habitaciones** (el contenido de pruebas sin él sigue igual). Con más de uno, sin niveles, con niveles que no suben o que no empiezan en 0, `RoomSetup.From` lanza `InvalidOperationException`.
- **El límite incluye las esquinas.** La búsqueda es por las cuatro direcciones, pero las piezas del límite que puntúan son los muros y puertas del mismo nivel en las ocho celdas vecinas de cada celda de la habitación (así la casa de 5 × 5 tiene 16 piezas, como dice el ejemplo). Una puerta cuenta con el material con que está hecha su celda.
- **`Rooms.All()`** da las habitaciones que contienen algún mueble o alguna celda de una zona de almacén, por `Room.Id` ascendente (el índice de su celda más baja). Una habitación vacía se encuentra con `Rooms.At(celda)`, pero no sale en `All()` (no hace falta recorrer todo el mapa, y el resultado no depende de qué se preguntó antes).
- **Caché.** El índice (`RoomIndex`, uno por simulación, derivado: no entra en el guardado ni en la huella) se rehace entero, perezosamente, cuando cambia `GameMap.StructureVersion` (forma o material de una celda, o aparece o desaparece una marca de mueble), la cantidad de muebles o las celdas de las zonas de almacén. `RoomSystem` lo mira al final de cada tick y emite `RoomsChangedEvent` sólo si el conjunto de habitaciones con muebles cambió (identidad, tamaño, puntaje, techo o funciones), así cavar lejos de una casa no avisa. Se mide en `RoomBudgetTests`: unos 0,2 ms para volver a encontrar 20 habitaciones y unos 0,05 ms por tick de media (el presupuesto es 0,1) con 20 habitaciones, 15 colonos y una pared que cae y vuelve cada dos horas, todo en compilación Debug. 300 cofres a cielo abierto se recorren en unos 4 ms: lo ya visto como exterior se recuerda.
- **Puntaje.** Los muebles se suman en centésimas (`beauty × valuePercent`) y se dividen por 100 al final, truncando: una cama buena (4,8) cuenta 4, y un banco pobre (−0,75) cuenta 0. No hay mesa larga todavía (llega con el resto de `oficios.md`).
- **Camas reservadas.** `AssignBedCommand(cama, colono)` con `EntityId.None` libera. Asignar quita la cama a quien la tenía y libera las otras camas del colono. No hay indicador de reserva (se quitó en la revisión: el dueño vivo ya protegía la cama); al morir el dueño la cama queda libre. `RoomRules.SuggestAssignments` pone primero al colono, luego su pareja, sus hijos vivos y sus hermanos, y prefiere, en este orden, la cama que ya tiene, las libres y las de otro, y dentro de cada grupo las hechas para su etapa (la cuna para el bebé).
- **Pensamientos al despertar.** Los de nivel se reemplazan entre sí (un dormitorio mejor no suma al de la noche anterior), y lo mismo `family_home` y `slept_crowded`. La cuenta de camas con dueño es la de dueños distintos que viven.
- **Comer sentado.** La hace `JobKind.EatAtTable` (objetivo: la pila, destinatario: la silla) sólo cuando el colono elige qué comer (hambre baja). Va a la pila, toma las unidades que le faltan, camina con ellas (`Carried`) a la silla, se sienta y come una unidad cada `eatTicksPerUnit`; las sobras caen en su celda. "A `diningRadius` celdas" se mide en el plano desde donde está el colono, como los demás radios del libre albedrío, y entre las sillas libres gana la de menor costo de camino. Si al levantar la comida no hay camino a la silla, come parado ahí mismo. Una necesidad crítica que no sea el hambre interrumpe la comida; los pensamientos se dan al terminar si comió algo.
- **Reunión.** `JobKind.Gather` (destinatario: la silla) está dentro de `JobKinds.IsLeisure`. La probabilidad es `gatherChancePercent` escalada por la sociabilidad y el ánimo igual que la búsqueda de compañía (`FreeWillRules.Chance`), así que usa el flujo `colony.free_will`; sólo se tira entre las horas de reunión y si hay alguna silla libre. Si no va, el colono busca compañía como siempre. Dura `gatherTicks` o hasta que una necesidad o un trabajo lo llame, y deja de ser válida si ya no puede buscar compañía (crisis, libre albedrío apagado).
- **Guardado.** Formato 19: sólo los trabajos `EatAtTable` y `Gather` y el comando `AssignBedCommand`. Una partida de formato 18 carga sin migración (fixture `o9.olsave`).

## Notas de implementación (presentación de O9)

- **Capa de habitaciones** (`RoomLayer`, tecla L): losetas translúcidas sobre el piso de cada habitación de `Rooms.All` en el tono de su nivel (de gris pardo a oro: una sola paleta, por la posición del nivel en la escala, así que un nivel de otra época también tiene tono) y, sobre la celda más cercana al centro, una fila de íconos de 16 × 16 (funciones y nivel; `tools/make-room-icons.py`, `game/assets/icons/rooms/`). Se redibuja con `RoomsChangedEvent` y al cambiar el nivel de la vista, sólo si está prendida. Sólo muestra las habitaciones que `All()` da (con muebles o zonas de almacén).
- **Lista de habitaciones** (`RoomsPanel`, tecla U y botón del HUD): adelanta de O10 la lista, porque la capa la necesita. Una fila por habitación con sus íconos, "Dormitorio decente: 22 puntos" y "De …" con los dueños; un clic mueve la cámara (y el nivel) a su centro y la selecciona, con lo que el inspector la describe. Abrir la lista prende la capa y cerrarla la apaga; la tecla L la prende o apaga aparte. El botón del HUD es un ícono con el nombre en el tooltip: la barra de arriba ya no tenía lugar para otra palabra (la fecha ya se cortaba a 1280 de ancho).
- **Asignación de camas:** bajo la lista, las camas de la habitación elegida (también se elige seleccionando una celda de la habitación), cada una con un menú: Libre y los colonos para los que sirve la cama (una cuna sólo da bebés), que manda un `AssignBedCommand`. Un menú de colonos con el botón **Asignar familia** manda un `AssignBedCommand` por cada elemento de `RoomRules.SuggestAssignments`; la UI no elige camas.
- **Inspector:** la celda de una habitación dice "Dormitorio decente: 22 puntos, 3 camas", sus otras funciones y "De …" (nombre y apellido de los dueños); el colono dice "Duerme en un dormitorio decente" si su cama está en una habitación, y "Duerme en su cama" si no. Para una habitación con varias funciones el nombre es el de la primera (el dormitorio, por el orden de los defs), y el nivel concuerda con su género.
- **Colono sentado:** `EntityRenderer` usa la animación `sit` (todos los modelos de colonos la tienen: bebé, niño, adolescente, adulto y anciano) cuando el trabajo es `EatAtTable` o `Gather` en etapa `Working`, y lo gira hacia la mesa que tiene junto a la silla (se busca una vez al sentarse).
- **Desvíos de texto:** `ui.inspector.room_owners` es "De {names}" y en inglés "Belongs to {names}" (la spec decía "{names}'s", que con dos nombres se lee mal); "Gathered with the others" y "Reunido con los demás" pasan a gerundio ("Reuniéndose con los demás", "Gathering with the others") como los demás informes de trabajo, y el español evita el masculino. Claves nuevas: `ui.rooms.*` (button, title, overlay, empty, generic, generic_gender, kind, list_and, list_comma, beds, no_beds, assign_family, assign_bed, free_bed) y `ui.inspector.room`, `room_beds`, `room_roles`, `room_owners` y `sleeps_in_room`. La unión de nombres usa "y" o "e" (`i_sound`) en español.
- **Captura:** `--rooms` construye (con las funciones que sólo el Core puede llamar, como el smoke de la herramienta de cultivos) un dormitorio y un comedor junto al primer colono, da camas a la familia del primero, abre la lista con la capa y elige el dormitorio; `--rooms-dining` elige el comedor y sienta a los colonos en sus sillas con el trabajo `Gather`. Con `--inspect` se ve el inspector del colono. Capturas en `game/out/rooms-{es,en}.png`, `rooms-dining-{es,en}.png` y `rooms-colonist-{es,en}.png`.

## Fuera de alcance

Van a `docs/ip/backlog.md`: techos y temperatura de las habitaciones, lluvia y goteras (IP6), camas dobles, el deseo de un dormitorio propio, habitaciones especiales (enfermería, cuartel, templo; IP5), limpieza y suciedad, ruido, ventanas e iluminación, fogata o fogón al aire libre como lugar de reunión, la belleza del entorno exterior, y el estilo arquitectónico por época (IP9).
