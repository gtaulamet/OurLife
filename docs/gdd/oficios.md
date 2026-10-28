# Oficios (IP4)

Spec de contrato de IP4 (`docs/ip/IP4.md`): pilas con calidad, habilidades de oficio, talleres, recetas, órdenes de producción, herramientas y el contrato común del resto de IP4. Las partes grandes tienen su propia spec:

| Spec | Contenido |
|---|---|
| `docs/gdd/oficios.md` (esta) | Objetos, calidad, oficios, talleres, recetas, órdenes de producción, herramientas, contrato y guardado de toda la IP |
| `docs/gdd/conocimientos.md` | Árbol de conocimientos medieval, scriptorium, práctica y desbloqueos |
| `docs/gdd/agricultura.md` | Zonas de cultivo, cultivos, comidas cocinadas y deterioro de alimentos |
| `docs/gdd/habitaciones.md` | Detección de habitaciones, calidad, dormitorios asignados y comedor |
| `docs/gdd/subsuelo.md` | Vetas de mineral y niebla subterránea |

**Estado:** propuesta de O1, pendiente de aprobación del usuario. Los números son el punto de partida, viven en defs y se ajustan en O11 con partidas largas. Los JSON de esta spec son el modelo de datos propuesto: **no entran en `game/content/defs` en O1**, porque el cargador no conoce todavía esos tipos de def. Cada def entra en el hito que lo implementa (regla de `vida-y-generaciones.md`).

## Qué pide el plan

Calidad de lo producido según la habilidad; talleres de carpintería, cantería, cocina, fundición y herrería con recetas en JSON; órdenes de producción (hacer N, mantener N, repetir); conocimientos que bloquean recetas y edificios; agricultura; comidas cocinadas y deterioro; vetas y niebla; panel de inventario; habitaciones, dormitorios y comedor. **Jugable:** cadenas tronco → tablón → mesa, cosecha → comida y mineral → lingote → herramientas, desbloqueadas investigando, y una casa digna para cada familia.

Además, `libre-albedrio.md` dejó para IP4 el lugar de reunión (fogón y mesas) y sembrar árboles a pedido: ver `habitaciones.md` y `agricultura.md`.

## Qué hace el juego hoy

| Tema | Hoy | Desde |
|---|---|---|
| Objetos | Tronco, piedra, bayas y provisiones; una pila por celda de almacén, sin calidad | IP2 |
| Habilidades | Una por tipo de trabajo (crianza, excavar, talar, recolectar, construir, acarrear), nivel 0 a 20; velocidad `80 + 2 × nivel` por ciento | IP3 |
| Muebles | Cama, cuna, mesa, silla y cofre, de madera (tronco); sin calidad; mesa y silla decorativas | IP2, IP3 |
| Conocimientos | `KnowledgeDef`, `Def.Requires`, `UnlockService` y `ProgressState.Knowledge` existen y se guardan, pero no hay conocimientos ni cómo ganarlos | IP1 |
| Comida | Se come de una pila, parado; bayas (400) y provisiones (2500); nada se pudre | IP1 |
| Mapa | Todo el subsuelo se ve; no hay mineral | IP1 |

## Objetos

### Categorías

Las tres existentes (`food`, `wood`, `stone`) y cuatro nuevas. El almacén filtra por categoría (`almacenes.md`); filtrar por objeto queda en el backlog.

| Categoría | Objetos | es | en |
|---|---|---|---|
| `food` | bayas, provisiones, repollo, guisantes, pan, potaje, repollo encurtido | Comida | Food |
| `wood` | tronco | Madera | Wood |
| `stone` | piedra | Piedra | Stone |
| `grain` | trigo, harina | Cereales | Grain |
| `refined` | tablón, mango, bloque de piedra, lingote de hierro | Materiales trabajados | Worked materials |
| `ore` | mineral de hierro, carbón vegetal | Minerales y combustible | Ore and fuel |
| `tool` | hacha, pico, azada y martillo de hierro | Herramientas | Tools |

Las metas de existencias del libre albedrío siguen siendo sólo `food` y `wood` (los troncos no incluyen tablones ni carbón). Todo lo comestible es `food`.

### Objetos nuevos

`Item` suma los campos `spoilDays` (días hasta pudrirse, 0 = no se pudre), `meal` (es un plato preparado), `reserveValue` (nutrición que cuenta para la reserva de comida de la aldea aunque no se coma directo, ver `agricultura.md`) y `tool` (ver "Herramientas"). `stackLimit` y `nutrition` como hoy.

| Objeto | Categoría | `kind` | Pila | Nutrición | `spoilDays` | Motivo |
|---|---|---|---|---|---|---|
| `berries` (cambia) | food | food | 75 | 400 | 60 | Hoy no se pudren; 60 días cubren de otoño a fines de invierno con margen (ver `agricultura.md`) |
| `provisions` | food | food | 50 | 2500 | 0 | Seco, de viaje: no se pudre |
| `cabbage` repollo | food | food | 50 | 300 | 30 | Verdura fresca, dura un mes |
| `peas` guisantes | food | food | 75 | 250 | 90 | Secos duran |
| `bread` pan | food | food, `meal` | 50 | 800 | 20 | Más nutritivo que lo crudo, pero se pone duro: se hornea a pedido |
| `pottage` potaje | food | food, `meal` | 25 | 800 | 6 | Plato caliente, dura pocos días |
| `pickled_cabbage` repollo encurtido | food | food, `meal` | 50 | 350 | 150 | La conservación gana en duración lo que pierde en nutrición |
| `wheat` trigo | grain | raw | 75 | 0 (`reserveValue` 800) | 200 | El grano se guarda todo el invierno |
| `flour` harina | grain | raw | 50 | 0 (`reserveValue` 800) | 90 | |
| `plank` tablón | refined | raw | 25 | | 0 | |
| `handle` mango | refined | raw | 50 | | 0 | |
| `stone_block` bloque de piedra | refined | raw | 25 | | 0 | |
| `iron_ingot` lingote de hierro | refined | raw | 25 | | 0 | |
| `iron_ore` mineral de hierro | ore | raw | 25 | | 0 | |
| `charcoal` carbón vegetal | ore | raw | 50 | | 0 | |
| `iron_axe`, `iron_pickaxe`, `iron_hoe`, `iron_hammer` | tool | raw | 1 | | 0 | Una herramienta por pila: cada una lleva su calidad |

Nombres y géneros propuestos en "Textos". Cada objeto necesita un modelo `item.<id>` en el catálogo de visuales (ver "Modelos").

### Pilas con calidad y frescura

- Una pila (`ItemStack`) suma dos datos, ambos opcionales y sólo escritos al guardar cuando no son los de por defecto: `Quality` (id del def `Quality`, por defecto `normal`) y `Freshness` (de 0 a 10 000, por defecto 10 000 = fresco).
- **Pila = objeto + calidad.** Dos pilas del mismo objeto con distinta calidad no se mezclan; acarrear, almacenar y entregar a una obra comparan la calidad, y una celda de almacén guarda una sola pila de un objeto y una calidad. Sólo las herramientas y lo que sale de recetas con `quality` tienen calidad distinta de la normal.
- **Frescura:** al juntar dos pilas, la frescura es el promedio ponderado por cantidad, redondeado hacia abajo. Las pilas con `spoilDays` 0 no la usan. El deterioro es de `agricultura.md`; en O2 sólo existe el dato.
- El Core cuenta existencias por objeto y calidad con una consulta nueva (`Stocks`), que el HUD, las metas, las órdenes de producción y el panel de inventario comparten.

## Calidad

Def nuevo `Quality`, uno por nivel, ordenado, y `QualityConfig` con la regla de la tirada.

| Calidad | Tirada menor que | `valuePercent` | `comfortPercent` | Motivo |
|---|---|---|---|---|
| `poor` pobre | 30 | 75 | 90 | Un principiante a veces falla |
| `normal` | 100 | 100 | 100 | El resultado típico |
| `good` buena | 130 | 120 | 110 | |
| `excellent` excelente | 150 | 150 | 120 | |
| `masterwork` obra maestra | 9999 | 200 | 130 | Sólo con nivel 17 o más (99 + 3 × 17 = 150) |

- **Tirada:** `puntaje = d100 + 3 × nivel de la habilidad` (el d100 es un entero de 0 a 99 del flujo `craft.quality`). Se toma la primera calidad cuya cota supera el puntaje. Con los números de arriba:

| Nivel | Pobre | Normal | Buena | Excelente | Obra maestra |
|---|---|---|---|---|---|
| 0 | 30 % | 70 % | | | |
| 5 | 15 % | 70 % | 15 % | | |
| 10 (promedio) | | 70 % | 30 % | | |
| 15 | | 55 % | 30 % | 15 % | |
| 20 | | 40 % | 30 % | 20 % | 10 % |

- **Qué lleva calidad:** los objetos de recetas con `"quality": true` (en IP4, las herramientas) y los muebles (la tirada la hace quien termina de construir, con su habilidad de construcción; el mueble guarda `Quality`). Los materiales (tablón, bloque, lingote), la comida y los muros no.
- **Efectos:**
  - Cama: `restRecoveryPercent` por `comfortPercent` (la cama de 150 % pasa a 135 % pobre y 195 % obra maestra).
  - Herramienta: su bonus de velocidad por `valuePercent` (ver "Herramientas").
  - Mueble: su belleza por `valuePercent`, que cuenta en la habitación (`habitaciones.md`).
- El inspector muestra la calidad de una pila, un mueble o una herramienta ("Hacha de hierro (calidad buena)").

## Habilidades de oficio

Siete tipos de trabajo nuevos, cada uno con `skill: true`, que dan siete habilidades (nivel 0 a 20, mismas reglas de IP3: experiencia por tick de trabajo, inteligencia, enseñanza, velocidad `80 + 2 × nivel`). Se suman a `work_types.json`:

| Tipo de trabajo | Habilidad en español | Livianos (`light`) | Qué hace | Hito |
|---|---|---|---|---|
| `carpentry` | Carpintería | No | Recetas del banco de carpintero | O3 |
| `stonecutting` | Cantería | No | Recetas del banco de cantero | O3 |
| `cooking` | Cocina | Sí | Recetas de la cocina | O3 |
| `farming` | Agricultura | Sí | Sembrar y cosechar en zonas de cultivo | O6 |
| `research` | Investigación | Sí | Trabajar en el scriptorium | O5 |
| `smelting` | Fundición | No | Carbonera y horno de fundición | O8 |
| `smithing` | Herrería | No | Fragua | O8 |

- **Los trabajos de taller no se listan en `jobs`:** `craft` es un solo trabajo y su tipo sale del taller (`Buildable.workstation.workType`), así que `carpentry`, `stonecutting`, `cooking`, `farming`, `research`, `smelting` y `smithing` llevan `jobs` vacío y `workshop: true` (el cargador sólo admite `jobs` vacío con esa marca). El test de contenido comprueba que cada taller apunta a un tipo de trabajo que existe.
- **Prioridades:** cada colono tiene prioridad de 1 a 4 o "nunca" para cada tipo, como hoy. El valor por defecto de un tipo nuevo es 3 (el mismo de los demás tipos). En la tabla de trabajo del panel Colonia las columnas pasan de 6 a 13: la tabla se desplaza en horizontal (O2).
- **Fundadores:** las siete habilidades se sortean como las de IP3 (de 3 a 10, y una al azar con 4 más).
- **Niños y adolescentes:** un niño no trabaja; un adolescente sólo hace los trabajos livianos, así que ayuda en la cocina, el campo y el scriptorium.
- **Migración:** una partida anterior recibe las siete habilidades sorteadas como las de un fundador (flujo `life.migration`, orden de `EntityId`) y las prioridades por defecto.

## Talleres

Un taller es un `Buildable` con `shape: furniture` y un campo nuevo `workstation`. Usa todo lo de `construccion.md` y `muebles-y-camas.md` (plano, entrega de material, construcción, giro, desmontar, vista previa), no bloquea el paso y ocupa **una celda** (decisión 7). Quien trabaja en él se para en una celda vecina, con la regla de las ocho direcciones de construir.

```json
{ "type": "Buildable", "id": "carpenter_bench", "era": "medieval", "requires": ["carpentry"],
  "shape": "furniture", "materials": ["timber"], "cost": 6, "workTicks": 240, "order": 20,
  "visual": "furniture.carpenter_bench", "beauty": -1,
  "workstation": { "workType": "carpentry" } }
```

| Taller | Material | Costo | Trabajo | Tipo de trabajo | Conocimiento | Recetas |
|---|---|---|---|---|---|---|
| `carpenter_bench` banco de carpintero | madera | 6 | 240 | carpentry | carpentry | tablón, mango |
| `mason_bench` banco de cantero | mampostería | 6 | 240 | stonecutting | stonecutting | bloque de piedra |
| `kitchen` cocina | mampostería | 8 | 300 | cooking | cooking | harina, pan, potaje, encurtido |
| `charcoal_kiln` carbonera | mampostería | 8 | 300 | smelting | charcoal_burning | carbón vegetal |
| `bloomery` horno de fundición | mampostería | 12 | 450 | smelting | metallurgy | lingote de hierro |
| `forge` fragua | mampostería | 12 | 450 | smithing | ironwork | hacha, pico, azada y martillo de hierro |
| `scriptorium` | madera (tablón) | 6 | 240 | research | (ninguno) | (ninguna: investiga) |

- **Motivo de los costos:** los bancos, la cocina, la carbonera y el scriptorium cuestan entre 6 y 8 unidades, un poco más que una cama (4); el horno de fundición y la fragua, 12, como los muros de una casa chica. Son inversiones de la primera semana, no de la primera hora.
- El scriptorium (ver `conocimientos.md`) no necesita ningún conocimiento: es el que los da. Su material es el tablón, así que la aldea necesita carpintería (inicial) para el primero.
- **`beauty`:** un número que la habitación suma (`habitaciones.md`). Los talleres restan: una fragua en el dormitorio no es un lujo.
- Un taller inactivo no consume nada. Cualquier colono puede usar cualquier taller construido; el taller no tiene dueño.

## Recetas

Def nuevo `Recipe`:

```json
{ "type": "Recipe", "id": "iron_axe", "era": "medieval", "requires": ["ironwork"],
  "workshop": "forge",
  "inputs": [ { "item": "iron_ingot", "count": 1 }, { "item": "handle", "count": 1 } ],
  "outputs": [ { "item": "iron_axe", "count": 1 } ],
  "workTicks": 360, "quality": true, "order": 40 }
```

| Campo | Qué es |
|---|---|
| `workshop` | Id del `Buildable` donde se hace |
| `inputs` | Lista de `{ item, count }` o `{ category, count }` (cualquier objeto de la categoría); `minQuality` opcional |
| `outputs` | Lista de `{ item, count }` |
| `workTicks` | Ticks de trabajo con todo el material en el taller (se divide por la velocidad como siempre) |
| `quality` | Si es verdadero, las salidas llevan la calidad que sale de la tirada |
| `order` | Lugar en la lista de recetas del taller |
| `era`, `requires` | Como todo contenido: la receta está disponible si `UnlockService` la desbloquea |

Contenido de IP4 (el conocimiento sale de la columna "Requiere"):

| Receta | Taller | Entradas | Salidas | Ticks | Requiere | Motivo |
|---|---|---|---|---|---|---|
| `plank` | carpentry | 1 tronco | 2 tablones | 90 | carpentry | Un tronco rinde dos tablones; las camas siguen costando 4 piezas |
| `handle` | carpentry | 1 tablón | 2 mangos | 60 | carpentry | Pieza de herrería |
| `stone_block` | mason | 1 piedra | 1 bloque | 90 | stonecutting | El sillar cuesta tiempo, no material |
| `flour` | kitchen | 2 trigo | 2 harina | 90 | milling | Molienda a mano |
| `bread` | kitchen | 2 harina | 3 panes | 150 | milling | 2 trigos rinden 3 panes: 2400 de nutrición por 2 granos |
| `pottage` | kitchen | 2 repollos + 1 guisante | 2 potajes | 120 | cooking | 850 de nutrición cruda se vuelven 1600 |
| `pickled_cabbage` | kitchen | 3 repollos | 3 encurtidos | 100 | food_preservation | |
| `charcoal` | charcoal_kiln | 4 troncos | 2 carbones | 240 | charcoal_burning | Carbón de leña: la leña rinde bastante menos que el carbón |
| `iron_ingot` | bloomery | 2 minerales + 2 carbones | 1 lingote | 300 | metallurgy | Relación carbón y mineral cercana a 1 a 1 en un bajo horno; el lingote es una fracción del mineral |
| `iron_axe`, `iron_pickaxe` | forge | 1 lingote + 1 mango | 1 herramienta, `quality` | 360 | ironwork | |
| `iron_hoe`, `iron_hammer` | forge | 1 lingote + 1 mango | 1 herramienta, `quality` | 300 | ironwork | |

Cuántos troncos cuesta una herramienta: un lingote son 2 minerales y 2 carbones (4 troncos, un roble entero), más un mango (medio tronco).

### El trabajo `craft`

Trabajo nuevo `craft` (hacer). Su tipo de trabajo (y por lo tanto su prioridad y su habilidad) sale del taller de la receta.

1. **Elige.** Un colono libre con prioridad para ese tipo busca, en el orden de las órdenes de producción (abajo), la primera orden ejecutable: receta desbloqueada, taller alcanzable y libre (nadie lo reserva), ingredientes en pilas alcanzables, sin reservar y a 30 celdas o menos (el radio de la aldea). Reserva el taller y los ingredientes.
2. **Trae.** Para cada entrada que falta en el taller, camina a la pila más cercana, levanta lo que entra en las manos (hasta la pila entera o lo que falta) y lo deja en el taller. Los ingredientes entregados quedan en el taller (`Workshop.Inputs`). Si deja el trabajo, quedan ahí para quien siga; si nadie los usa en un día, caen al suelo en la celda del taller y se acarrean.
3. **Trabaja** desde una celda vecina. El progreso es del trabajo: si lo deja, el siguiente empieza de cero. Gana experiencia de la habilidad del taller (1 punto por tick, por la inteligencia).
4. **Termina.** Consume las entradas, hace la tirada de calidad si la receta lo pide y deja las salidas en la celda del taller (o en la vecina pisable más cercana si ya hay otra cosa). Los acarreadores las llevan al almacén. La orden suma lo hecho.
- Un colono con las manos llenas las suelta donde está al dejar el trabajo, como al acarrear.
- Sólo un colono a la vez usa un taller.

## Órdenes de producción

Una orden es una entidad `WorkOrder` (como un plano). La lista de órdenes es el orden de las entidades que el jugador puede reordenar.

| Modo | Qué hace | Termina |
|---|---|---|
| Hacer N (`count`) | Hace la receta hasta sumar N unidades de la salida principal | Al llegar a N |
| Mantener N (`keep`) | Hace la receta mientras las existencias de la salida principal más lo que está en producción sean menos que N | Nunca; pausa sola cuando se cumple |
| Repetir (`forever`) | Hace la receta sin parar | Nunca |

- **Existencias que cuenta Mantener:** las que cuenta el HUD (suelo, almacenes y manos), de la calidad `minQuality` o mejor si la orden la tiene, más las unidades de trabajos `craft` en curso, para no hacer de más.
- **Taller:** la orden vale para cualquier taller de su tipo (por defecto) o para uno elegido.
- **Hacer N con tandas:** la salida de una tanda puede pasar de N (la última tanda no se recorta).
- **Estado** (calculado, no se guarda): `running`, `waiting_inputs` (faltan ingredientes), `no_workshop` (no hay taller construido), `locked` (falta un conocimiento: el panel dice cuál), `no_worker` (nadie tiene la prioridad para ese oficio), `paused`, `satisfied` (Mantener cumplida) y `done`. El panel lo muestra con color y texto; no se manda al registro, para no llenarlo.
- **Pausar** y **quitar** una orden son acciones del jugador. Quitar una orden con un `craft` en curso lo deja terminar; si no, los ingredientes entregados caen al suelo.
- Las cadenas (tronco → tablón → mango) se arman con varias órdenes; que una orden cree sola las intermedias queda en el backlog.
- Se evalúan cada 30 ticks y se guarda el resultado hasta el próximo (presupuesto en los riesgos).

## Herramientas

- **Equipar:** el campo `Item.tool` (`{ workType, speedPercent }`) hace de un objeto una herramienta. Cada colono lleva como mucho **una por tipo de trabajo** (componente `Equipment`, una lista de pila con calidad). Una herramienta equipada sale del mundo: no se acarrea ni se cuenta en las existencias.
- Antes de un trabajo de un tipo para el que no tiene herramienta, si hay una libre en un almacén a 30 celdas o menos, el colono la va a buscar (trabajo `equip`, que no pertenece a ningún tipo de trabajo: no da experiencia ni cuenta como trabajo) y se la queda. Una herramienta ya equipada se conserva para siempre: no se cambia por una mejor, no se gasta (backlog) y al morir su dueño cae en su celda.
- **Cómo se decide (O8):** cuando un colono toma un trabajo de un tipo para el que no tiene herramienta, y hay una libre en un almacén dentro de `craftFetchRadius` (30) celdas, deja ese trabajo y va a buscarla (trabajo `equip`); al volver a decidir toma el trabajo, ya con la herramienta. Sólo se desvía si el trabajo tomado da bonus de velocidad (un `deliver` o un acarreo no se sueltan por una herramienta), y se cancela si, de camino, la pila deja de estar en un almacén o el colono ya no puede hacer ese trabajo. Una pila fuera de un almacén no cuenta (se acarrea primero) y una que otro colono va a buscar tampoco. Entre varias, gana la que más velocidad da con su calidad, luego la más cercana y luego la de menor id. El componente `Equipment` se agrega con la primera herramienta, así una partida sin herramientas se guarda como antes.
- **Efecto:** su `speedPercent` por el `valuePercent` de su calidad se suma a la velocidad del trabajo (junto a la habilidad, la etapa, la diligencia y el ánimo). Contenido de IP4 (sólo hierro; herramientas de piedra o bronce quedan en el backlog):

| Herramienta | Tipo de trabajo | `speedPercent` | Motivo |
|---|---|---|---|
| `iron_axe` | woodcutting | 30 | Talar con hacha de hierro es claramente mejor, y se nota sin romper el balance de la leña |
| `iron_pickaxe` | mining | 30 | Igual, y adelanta la excavación de las vetas |
| `iron_hoe` | farming | 25 | La azada rinde un poco menos que el hacha |
| `iron_hammer` | construction | 20 | Construir ya tiene mucha habilidad en juego |

- Con calidad obra maestra un hacha da +60 % de velocidad (30 × 200 %); pobre, +22 %. El bonus multiplica la velocidad del colono como los demás factores (`velocidad × (100 + bonus) / 100`), así que un hacha normal de 30 sobre una velocidad de 80 da 104.

## Materiales de construcción nuevos y muebles de tablones

- Dos materiales nuevos, con `builtFrom` como los de H2.4: `planks` (tablones; madera, `builtFrom: plank`) y `ashlar` (sillería; piedra, `builtFrom: stone_block`).
- **Los muebles pasan a tablones** (decisión 1): cama, cuna, mesa, silla, cofre y los talleres de madera piden `planks` en lugar de `timber`. Los muros, suelos, rampas, escaleras y puertas aceptan los cuatro materiales; la puerta pasa a pedir tablones o madera. El costo en unidades no cambia.
- La llegada suma **16 tablones** (`Scenario.startingItems`): cuatro camas, o dos camas, una mesa y una silla, mientras se arma el primer banco de carpintero. Una partida anterior no los recibe.
- Un mueble nuevo de ebanistería, la **mesa larga** (`long_table`, tablones, costo 5, trabajo 200, `beauty` 5, `requires: ["joinery"]`), para los comedores.
- `beauty` por mueble (suma a la habitación): cama 4, cuna 3, mesa 3, mesa larga 5, silla 2, cofre 2, scriptorium 1, cocina 0, banco de carpintero y de cantero -1, carbonera -2, horno de fundición -4, fragua -4.

## Libre albedrío

- Los oficios **no son iniciativa**: nadie fabrica, cultiva ni investiga por su cuenta; lo hacen por órdenes de producción, zonas y la cola de investigación, que son del jugador. Por eso funcionan con la opción apagada, y la huella de la partida con la opción apagada sólo cambia por el contenido nuevo.
- Las prioridades mandan: un colono con una prioridad en "nunca" no toma ese oficio, ni por orden ni por zona.
- El orden de las decisiones de `libre-albedrio.md` no cambia: crítico, órdenes por prioridad, necesidades bajas, iniciativa, jugar, buscar compañía, deambular. Cultivar, fabricar e investigar entran como órdenes del jugador, antes de la iniciativa.
- Las metas de comida y madera se mantienen. El nuevo contenido pide dos ajustes que se revisan en O11: la meta de madera por defecto (40) queda corta si el carbón y los tablones la consumen, y la meta de comida cuenta unidades, no nutrición, así que el pan (800) cuenta lo mismo que una baya (400). No se cambia la meta, que cuenta lo que cuenta el HUD.
- `gather_food` sigue recolectando sólo arbustos (la cosecha de las zonas es una orden del jugador). El lugar de reunión del comedor (`habitaciones.md`) es una conducta social y se apaga con la opción.
- Los árboles que planta una zona de silvicultura son árboles de la aldea: la iniciativa de talar puede talarlos.

## Contrato con la presentación

| Tipo | Nombre | Uso |
|---|---|---|
| Comando | `CreateWorkOrderCommand(receta, modo, cantidad, taller?)` | Crea una orden (taller 0 = cualquiera) |
| Comando | `SetWorkOrderCommand(orden, modo, cantidad, pausada)` | Cambia modo, cantidad o pausa |
| Comando | `MoveWorkOrderCommand(orden, posición)` | Reordena |
| Comando | `RemoveWorkOrderCommand(orden)` | Quita una orden |
| Comando | `PlaceBlueprintCommand` (existente) | También para talleres: rechaza lo bloqueado por `UnlockService` |
| Comando | `SetResearchQueueCommand(lista)`, `SetFarmZoneCommand(...)`, `AssignBedCommand(...)` | Ver `conocimientos.md`, `agricultura.md` y `habitaciones.md` |
| Componente | `Workshop`, `WorkOrder`, `Equipment` | En entidades de taller, de orden y en colonos |
| Campo | `ItemStack.Quality`, `ItemStack.Freshness`, `Furniture.Quality` | Calidad y frescura |
| Trabajos | `Craft`, `Equip`, `Research`, `Sow`, `Reap`, `EatAtTable`, `Gather` | `Research` en `conocimientos.md`; `Sow` y `Reap` en `agricultura.md`; `EatAtTable` y `Gather` en `habitaciones.md` |
| Evento | `WorkOrdersChangedEvent` | Una orden cambió |
| Evento | `EntitySpawnedEvent`, `EntityChangedEvent`, `EntityRemovedEvent` | Talleres, órdenes, herramientas |
| Consulta | `Stocks.Count(objeto, calidadMínima?)`, `Stocks.Totals()` | HUD, metas, órdenes y panel de inventario |
| Consulta | `Recipes.Of(taller)`, `WorkOrderRules.Status(orden)` | Panel de producción |
| Consulta | `UnlockService.IsUnlocked`, `UnlockService.Missing(def)` | Qué falta para desbloquear algo; la barra de construcción lo atenúa |

- Todo componente y comando nuevo se registra en `SaveTypes` con un nombre estable, y sus ids de defs llevan `[DefRef]` (ADR-010).
- Flujos aleatorios nuevos: `craft.quality`, `farm.yield` y `world.ore` (ver cada spec). Todo se recorre por `EntityId`.

## Defs nuevos y cambios

| Def | Campos | Archivo |
|---|---|---|
| `ItemCategory` | cuatro nuevos | `items.json` |
| `Item` | `spoilDays`, `meal`, `reserveValue`, `tool`; objetos nuevos | `items.json` |
| `Quality`, `QualityConfig` | tabla y tirada | `quality.json` |
| `WorkType` | siete nuevos con `skill: true`; `light` en cocina, agricultura e investigación | `work_types.json` |
| `Material` | `planks`, `ashlar`; `iron_vein` en `subsuelo.md`; `beauty` (ver `habitaciones.md`) | `materials.json` |
| `Buildable` | `workstation`, `beauty`, `tags`, `quality`; talleres, scriptorium, mesa larga; los muebles piden `planks` | `buildables.json` |
| `Recipe` | `workshop`, `inputs`, `outputs`, `workTicks`, `quality` | `recipes.json` |
| `Scenario` | `startingItems`, `startingKnowledge` | `scenario.json` |
| `Knowledge`, `Crop`, `SpoilageConfig`, `RoomConfig`, `OreVein` | en sus specs | `knowledge.json`, `crops.json`, `spoilage.json`, `rooms.json`, `worldgen.json` |

Cada def con textos lleva `name`, `desc` y `gender` en es y en. Cada def nuevo con un modelo se anota en `game/assets/models/visuals.json` en su hito.

## Guardado

Cada hito que suma estado sube el formato (ADR-013). Los números dependen del orden de unión de las ramas; los de abajo son una propuesta.

| Versión | Hito | Cambios | Migración desde la anterior |
|---|---|---|---|
| 12 | O2 | `Quality` y `Freshness` en pilas (sólo escritos si no son los de por defecto); las siete habilidades de oficio y sus prioridades | Habilidades sorteadas como las de un fundador; prioridades 3 |
| 13 | O3 | `Workshop` (con `Inputs`), `Furniture.Quality`, trabajo `Craft` | Nada |
| 14 | O4 | `WorkOrder` | Nada |
| 15 | O5 | Cola de investigación, progreso por conocimiento (`Progress`), trabajo `Research` | Los conocimientos iniciales (`startingKnowledge`) |
| 16 | O6 | Zonas de cultivo, `Crop`, trabajos `Sow` y `Reap` | Nada |
| 17 | O7 | Bandera `Discovered` de cada celda y las vetas | Descubiertas por la regla de `subsuelo.md` y vetas sorteadas con `world.ore` (decisión 4) |
| 18 | O8 | `Equipment`, trabajo `Equip` | Nada |
| 19 | O9 | Trabajos `EatAtTable` y `Gather`, comando `AssignBedCommand` | Nada |

- Una partida de `v0.3.5` (formato 11) carga sin la llegada de los 16 tablones, sin conocimientos más que los iniciales, con los muebles de madera que tuviera (el material `timber` sigue existiendo) y con la niebla calculada. Los muebles viejos conservan su madera y calidad normal.
- Se guarda una partida real nueva de cada formato como fixture (`o2.olsave` ...), como en IP3.5.
- El test de continuidad del guardado incluye, al final de IP4: una orden de cada modo, un taller con ingredientes entregados, una herramienta equipada, una pila con calidad y con frescura baja, un cultivo a medio crecer, una investigación en curso, una cama reservada y celdas sin descubrir.
- Huellas del smoke: cambian en O2 (las habilidades nuevas consumen el flujo de la llegada) y de nuevo con cada hito que suma estado. La bitácora registra las nuevas.
- ADR sugeridos: uno sobre la identidad de las pilas (objeto + calidad, con frescura promediada) y otro sobre la niebla como bandera guardada del mapa.

## Textos nuevos (propuesta)

Las claves siguen `docs/i18n.md`. El i18n-specialist ajusta el texto final.

| Clave | es | en |
|---|---|---|
| `def.item_category.{grain,refined,ore,tool}.name` | Cereales; Materiales trabajados; Minerales y combustible; Herramientas | Grain; Worked materials; Ore and fuel; Tools |
| `def.item.{id}.name`/`desc`/`gender` | trigo (m), harina (f), tablón (m), mango (m), bloque de piedra (m), mineral de hierro (m), carbón vegetal (m), lingote de hierro (m), hacha de hierro (f), pico de hierro (m), azada de hierro (f), martillo de hierro (m), repollo (m), guisantes (m), pan (m), potaje (m), repollo encurtido (m) | wheat, flour, plank, handle, stone block, iron ore, charcoal, iron ingot, iron axe, iron pickaxe, iron hoe, iron hammer, cabbage, peas, bread, pottage, pickled cabbage |
| `def.quality.{poor,normal,good,excellent,masterwork}.name` | pobre, normal, buena, excelente, obra maestra | poor, normal, good, excellent, masterwork |
| `ui.quality.label` | {item} (calidad {quality}) | {item} ({quality} quality) |
| `def.work_type.{carpentry,stonecutting,cooking,farming,research,smelting,smithing}.name` | Carpintería, Cantería, Cocina, Agricultura, Investigación, Fundición, Herrería | Carpentry, Stonecutting, Cooking, Farming, Research, Smelting, Smithing |
| `def.buildable.{carpenter_bench,mason_bench,kitchen,charcoal_kiln,bloomery,forge,scriptorium,long_table}.name` | Banco de carpintero, Banco de cantero, Cocina, Carbonera, Horno de fundición, Fragua, Scriptorium, Mesa larga | Carpenter's bench, Mason's bench, Kitchen, Charcoal kiln, Bloomery, Forge, Scriptorium, Long table |
| `def.material.{planks,ashlar}.name` | tablones, sillería | planks, ashlar |
| `def.recipe.{id}.name` | Aserrar tablones, Tallar mangos, Cortar bloques, Moler harina, Hornear pan, Cocinar potaje, Encurtir repollo, Hacer carbón, Fundir hierro, Forjar hacha, Forjar pico, Forjar azada, Forjar martillo | Saw planks, Carve handles, Cut blocks, Grind flour, Bake bread, Cook pottage, Pickle cabbage, Burn charcoal, Smelt iron, Forge axe, Forge pickaxe, Forge hoe, Forge hammer |
| `job.craft.report` | Trabajando en {recipe} | Working on {recipe} |
| `job.equip.report` | Yendo a buscar una herramienta | Fetching a tool |
| `ui.production.title` | Producción | Production |
| `ui.production.new_order` | Nueva orden | New order |
| `ui.production.mode.{count,keep,forever}` | Hacer {n}, Mantener {n}, Repetir | Make {n}, Keep {n}, Repeat |
| `ui.production.status.{running,waiting_inputs,no_workshop,locked,no_worker,paused,satisfied,done}` | En marcha; Faltan ingredientes; Sin taller; Falta conocimiento: {knowledge}; Nadie tiene la prioridad; Pausada; Cumplida; Terminada | Running; Missing ingredients; No workshop; Needs knowledge: {knowledge}; Nobody has the priority; Paused; Met; Done |
| `ui.production.progress` | {done} de {target} | {done} of {target} |
| `ui.inspector.workshop_inputs` | En el taller: {items} | In the workshop: {items} |
| `ui.inspector.tool` | Herramienta: {item} | Tool: {item} |
| `ui.inventory.title` | Inventario | Inventory |
| `ui.inventory.button`, `ui.inventory.empty` | Inventario (I), La colonia no tiene nada todavía. | Inventory (I), The colony has nothing yet. |
| `ui.inventory.col.{item,amount,quality,freshness}` | Objeto, Cantidad, Calidad, Frescura | Item, Amount, Quality, Freshness |
| `ui.orders.locked_hint` | Requiere: {knowledge} | Requires: {knowledge} |
| `def.thought.*`, `def.desire.*` | Ver `habitaciones.md` | |

## Panel de inventario (O10)

- Tecla **I** y botón "Inventario (I)" en la segunda fila de la barra superior. Un bloque por categoría de objetos, en el orden del contenido y con su total, y una fila por objeto y calidad: nombre, cantidad, calidad (sólo si no es la usual) y frescura (sólo lo que se pudre; amarilla bajo 40 %, roja bajo 15 %). Lee `Stocks.Totals()`; se actualiza dos veces por segundo mientras está abierto y se desplaza si no entra sobre el registro.
- **Paneles de IP4** (colonia P, producción O, conocimientos K, habitaciones U, inventario I): se abre uno a la vez, abrir uno cierra el abierto y la ficha del colono, la misma tecla lo cierra y Esc cierra la ficha o el panel abierto antes de abrir el menú de pausa. La ficha (F) tapa el inspector mientras está abierta. Los botones muestran su tecla y se ven presionados mientras su panel está abierto.
- Captura: `--inventory`.

## Modelos 3D necesarios

Los kits del proyecto son Kenney (naturaleza, comida, personajes) y KayKit Dungeon Remastered (mobiliario). El art-integrator revisa qué trae cada kit antes de dibujar nada; lo que falte se arma sobre el estilo del kit.

| Visual | Qué es | Origen probable |
|---|---|---|
| `furniture.carpenter_bench`, `furniture.mason_bench` | Mesa de trabajo con sierra o con cinceles y bloque | KayKit (mesa) más piezas |
| `furniture.kitchen` | Fogón con olla | KayKit o Survival Kit (fogata, olla) |
| `furniture.charcoal_kiln` | Montículo de tierra con humo | Armado en Blender |
| `furniture.bloomery` | Chimenea de piedra con boca | Armado en Blender |
| `furniture.forge` | Fragua con yunque | KayKit (yunque, si trae) o Blender |
| `furniture.scriptorium` | Atril con pergamino | KayKit (mesa, libro, vela) |
| `furniture.long_table` | Mesa larga | KayKit (mesa) alargada |
| `item.plank`, `item.handle`, `item.stone_block`, `item.iron_ingot`, `item.iron_ore`, `item.charcoal` | Materiales | Armados en Blender; el tronco ya existe |
| `item.iron_axe`, `item.iron_pickaxe`, `item.iron_hoe`, `item.iron_hammer` | Herramientas en las manos | Blender |
| `item.wheat`, `item.flour`, `item.cabbage`, `item.peas`, `item.bread`, `item.pottage`, `item.pickled_cabbage` | Comida y granos | Kenney Food Kit, si trae; si no, Blender |

Los modelos de cultivos, la niebla y las vetas están en sus specs. Cada modelo se anota en `visuals.json` y cada def nuevo exige su visual (CLAUDE.md).

## Criterios de aceptación

Escritos como tests de escenario con el contenido de prueba o el real, salvo los de la presentación.

1. **Calidad.** Con la tirada fija, una herrera de nivel 0 hace una herramienta pobre con puntaje menor que 30 y una de nivel 20, obra maestra con 150 o más. En 1000 tiradas con nivel 10 no hay pobres ni excelentes y las buenas están entre el 25 % y el 35 %.
2. **Pilas.** Dos pilas del mismo objeto con distinta calidad no se mezclan en el acarreo ni en un almacén; con la misma calidad se suman y la frescura es el promedio ponderado.
3. **Habilidades.** Cada oficio se aprende trabajando (1 punto por tick) y acelera el trabajo; un adolescente puede cocinar pero no forjar.
4. **Taller.** Con un banco de carpintero y troncos en un almacén, una orden "Hacer 4 tablones" trae 2 troncos, los convierte en 4 tablones y los acarreadores los guardan; el colono gana experiencia de carpintería.
5. **Cadena.** Con un solo tronco en el almacén, las órdenes "Hacer 2 tablones" y "Hacer 2 mangos" en el mismo banco terminan, en dos tandas, con 1 tablón y 2 mangos; el tronco ya no está.
6. **Un solo colono por taller.** Dos colonos con la misma orden no usan el taller a la vez; el segundo toma otra cosa.
7. **Órdenes.** Mantener 6 tablones hace tandas hasta tener 6 o más y se detiene; si se gastan, vuelve a hacer. Repetir no se detiene. Hacer N termina y se marca hecha.
8. **Bloqueos.** Una receta sin su conocimiento no se ejecuta, `Status` dice `locked` con el conocimiento que falta, y una orden sin el taller dice `no_workshop`. Un colono con la prioridad del oficio en "nunca" no toma la orden.
9. **Interrupción.** Un colono con hambre crítica deja el `craft` a medias: los ingredientes quedan en el taller, otro continúa, y nada se pierde ni se duplica.
10. **Herramientas.** Un colono sin hacha, con una en un almacén y un árbol por talar, la busca, la equipa y tala más rápido; con calidad obra maestra tala más rápido que con una normal; al morir la deja en su celda.
11. **Muebles.** Una cama construida por una carpintera nivel 20 sale con la calidad que da la tirada y recupera descanso según `comfortPercent`; las camas viejas de madera siguen funcionando.
12. **Libre albedrío.** Con la opción apagada, las órdenes y las zonas se cumplen igual; con la opción prendida, nadie fabrica nada sin una orden.
13. **Guardado.** Una partida de `v0.3.5` carga con siete habilidades nuevas y prioridades 3; el test de continuidad (arriba) da la misma huella; misma semilla, mismo resultado.
14. **Contenido.** Cada def nuevo tiene `era`, textos en es y en y visual; cada receta apunta a un taller y objetos que existen; ninguna receta, taller ni cultivo declara una época posterior a `medieval`.
15. **En la partida.** Una casa amueblada con tablones, un taller en uso y una herramienta de hierro forjada, en es y en, con capturas revisadas.
16. **Presupuesto.** Menos de 2 ms por tick de media con 15 colonos, 8 talleres y 12 órdenes activas.

## Decisiones del usuario (a cerrar en O1)

Cada una con la recomendación del diseñador.

1. **Muebles de tablones.** Cama, mesa, silla y cofre pasan a pedir tablones (y la llegada trae 16). Alternativa: que sigan pidiendo troncos y los tablones sean un extra de calidad. **Recomendación: sí**, es la cadena tronco → tablón → mesa que pide el plan, y los 16 tablones iniciales evitan un arranque más lento.
2. **Las bayas se pudren (60 días).** Sin deterioro no hay motivo para cocinar ni para almacenar bien. Riesgo: la meta de 3000 bayas y el invierno. **Recomendación: sí con 60 días**, con la mitad de velocidad bajo techo, y el balance de diez años en O11 antes de dar nada por bueno.
3. **Herramientas equipables** (+20 % a +30 % de velocidad, una por tipo de trabajo, sin desgaste). Alternativa: sólo se fabrican y sirven de material de IP5. **Recomendación: equipables**, es lo que cierra la cadena mineral → lingote → herramienta.
4. **Partidas viejas reciben vetas de hierro** al migrar (sorteadas en roca sin tocar). Alternativa: sin hierro en partidas de `v0.3.5`, sin herrería. **Recomendación: sí**, cuesta 1 a 2 h y no deja al usuario con una partida a medias.
5. **Conocimientos:** empiezan solo carpintería y cocina; el resto se investiga o se practica (hasta el 60 %); investigar no consume objetos (solo tiempo). **Recomendación: sí.** El pergamino y los libros llegan con la fauna de IP5.
6. **Comedor:** quien come, con comedor, se sienta a una mesa con silla; sin comedor, nada cambia y no hay castigo por comer parado. **Recomendación: sí, sin castigo**, para no empeorar el arranque.
7. **Talleres de una celda** (los de tres por tres, al backlog). **Recomendación: una celda**: un taller de 3 × 3 pide reservar celdas, rotar huellas y vista previa de huella; sale mucho más caro y no cambia el juego.
8. **Cultivos sin semillas** (sembrar es gratis) y silvicultura (sembrar árboles, que `libre-albedrio.md` dejó para IP4) dentro de IP4 pero recortable. **Recomendación: sin semillas; silvicultura incluida en O6**, y si O6 se pasa de horas es lo primero que sale a IP4.5.

## Fuera de alcance

Van a `docs/ip/backlog.md`: crianza de animales, cuero, pergamino, tinta y libros como objetos (IP5), más cultivos (cebada, avena, centeno, lino), rotación de campos y fertilidad, semillas, riego (IP6), cerveza, queso y carne ahumada, órdenes que crean solas los intermedios, filtros de almacén por objeto, talleres de varias celdas, bodega con barriles, desgaste y cambio de herramientas, bronce, cobre y acero, ladrillos, tejas y vidrio (IP9), molinos de agua o viento, prospección, derrumbes (IP5), camas dobles, el deseo de un dormitorio propio, biblioteca con estantes que aceleran la investigación, calidad de la comida, variedad de la dieta como pensamiento, enseñar conocimientos entre colonos (IP10, escuela), iniciativa de fabricar o sembrar por metas.

## Fuentes

- Bajo horno (bloomery): se carga carbón vegetal y mineral en una relación cercana a 1 a 1 y se obtiene una masa porosa de hierro y escoria, la `bloom`. [Wikipedia, Bloomery](https://en.wikipedia.org/wiki/Bloomery) y [EXARC, A new look at experimental ironmaking](https://EXARC.net/sites/default/files/exarc-eurorea_6_2009-a_new_look_at_experimental_ironmaking.pdf). Los rendimientos del juego (2 minerales y 2 carbones por lingote; 4 troncos por 2 carbones) son de jugabilidad, no mediciones.
- Scriptorium: sala o conjunto de trabajos de copia en monasterios, con repartos entre preparar pieles, copiar e iluminar; el nombre sigue vivo en el siglo XIII, cuando los talleres seglares empiezan a quitarles trabajo. [Wikipedia, Scriptorium](https://en.wikipedia.org/wiki/Scriptorium) y [Catholic Encyclopedia, Scriptorium](https://www.catholic.com/encyclopedia/scriptorium).
- Prioridad de plausibilidad histórica sobre exactitud: se usa un solo taller de cocina y un solo banco por oficio.
