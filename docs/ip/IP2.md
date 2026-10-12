# IP2: Asentamiento

**Objetivo.** Pasar de sobrevivir a asentarse: construir una casa con camas, ordenar el almacén, ver a la aldea dormir de noche y retomar la partida otro día.

**Estimación:** 77 a 101 h, igual que `plan-maestro.md` (sección 7.3). **Versión al cerrar:** `v0.2.0`.

**Estado:** planificada el 11 de octubre de 2026. H2.1 a H2.5 hechos; sigue H2.6.

## Estado de los hitos

| Hito | Estado | Rama | Notas |
|---|---|---|---|
| H2.1 Guardado y carga | Hecho | `ip2/h2-1-guardado` | Partidas en ZIP con JSON, ranuras, autoguardado diario y pantalla de error; continuidad probada en tres semillas |
| H2.2 Día, noche y sueño | Hecho | `ip2/h2-2-dia-noche` | La aldea duerme de 21 a 6, el sol cruza el cielo y el registro muestra el día |
| H2.3 Almacenes y acarreo | Hecho | `ip2/h2-3-almacenes` | Zonas con filtros por categoría, acarreo con reservas y la carga en las manos |
| H2.4 Construcción | Hecho | `ip2/h2-4-construccion` | Planos de muro, suelo, puerta, rampa y escalera con material; desmontar y canalizar; `terrainAmplitude` 2 |
| H2.5 Muebles y camas | Hecho | `ip2/h2-5-muebles-camas` | Cama, mesa, silla y cofre con giro; camas con dueño que descansan al 150 %; el cofre es un almacén |
| H2.6 Gestión de la colonia | Pendiente | `ip2/h2-6-gestion` | |
| H2.7 Integración | Pendiente | `ip2/h2-7-integracion` | |

**Ruta crítica:** H2.1 → (H2.2 ∥ H2.3) → H2.4 → H2.5 → H2.6 → H2.7.

**Por qué el guardado va primero:** el test "guardar a mitad y seguir = partida continua" queda corriendo desde el principio. Así, cada componente, comando o sistema nuevo de IP2 se prueba guardado y cargado en el mismo hito en que aparece, y no se descubre al final que algo no se puede serializar.

## Alcance

- **Guardado y carga:** ranuras con nombre, autoguardado diario y guardado manual desde el menú de pausa. "Cargar" se habilita en el menú principal. La partida guardada incluye el mapa, las entidades, los RNG, el reloj, la época, los conocimientos y el nombre de la aldea, que pasa a la simulación. Versión de formato y rechazo claro de un archivo incompatible o dañado.
- **Día y noche:** el sol cruza el cielo según la hora, y la luz, el ambiente y el cielo pasan por el amanecer, el día, el atardecer y la noche. La noche nunca queda a oscuras del todo y el ciclo se puede apagar en Opciones. Es de presentación: no cambia la simulación ni las huellas.
- **Sueño nocturno:** los colonos prefieren dormir de noche, con el horario en los defs. Un colono agotado duerme igual a cualquier hora. Se revisa el balance del hambre y del invierno.
- **Almacenes:** zonas dibujadas por arrastre con filtros por categoría de objeto. Los colonos acarrean los objetos sueltos al almacén y los apilan hasta un máximo por celda.
- **Construcción por planos:** muros, suelos, puertas, escaleras y rampas, con el material elegido. Los colonos entregan los materiales y después construyen. Desmontar devuelve parte del material. Canalizar abre un hueco hacia el nivel de abajo.
- **Muebles:** cama, mesa, silla y cofre, colocados como planos con orientación. El cofre funciona como un almacén de una celda.
- **Camas:** cada colono puede tener una cama asignada. Dormir en cama descansa más rápido que en el suelo.
- **Gestión:** panel de prioridades de trabajo (colono × tipo de trabajo) y lista de colonos con su actividad.
- **Alertas:** colono ocioso y hambre crítica, con aviso en el HUD y en el registro.

## Hitos en detalle

| Hito | Contenido | Agentes | Horas | Entregable verificable |
|---|---|---|---|---|
| H2.1 Guardado y carga | Spec; nombre de la aldea en la simulación; formato versionado (JSON comprimido); guardar y cargar mapa, entidades, RNG, reloj, progreso y comandos pendientes; derivados (conectividad, reservas) recalculados al cargar; ranuras y autoguardado diario; pantalla de carga; guardar desde la pausa; pantalla de error al arrancar o al cargar | Plan, sim-engineer, godot-engineer, i18n-specialist | 10–14 | Test "guardar en el tick N, cargar y seguir hasta M" = misma huella que correr hasta M sin cortar, en varias semillas; partida guardada y cargada desde el menú en es y en |
| H2.2 Día, noche y sueño | Ciclo visual de luz, ambiente y cielo con opción para apagarlo; horario de sueño en los defs; prioridad de dormir de noche; balance de hambre e invierno; el día en el registro de eventos | godot-engineer, sim-engineer, game-designer | 7–11 | Capturas al amanecer, mediodía, atardecer y noche; la aldea duerme de noche; los tests del invierno (`ColonyTests`) siguen en verde con el balance revisado |
| H2.3 Almacenes y acarreo | Spec; categorías de objetos en los defs; zonas y filtros; acarreo con reservas; apilado; consultas de almacén para la UI; orden de zona en la barra | game-designer, sim-engineer, godot-engineer, i18n-specialist | 12–16 | Escenario "troncos sueltos + almacén de madera ⇒ todos apilados en la zona"; zona dibujada y filtrada desde la UI |
| H2.4 Construcción | Spec; defs de construibles con costo y tiempo; planos con material; entrega de materiales; trabajo de construcción; desmontaje; canalizar; modelos de muros, suelos, puertas y rampas | game-designer, sim-engineer, art-integrator, godot-engineer, i18n-specialist | 22–28 | Escenarios "plano de muro + troncos en el almacén ⇒ muro construido" y "canal ⇒ hueco hacia abajo y paso libre"; una casa de cuatro muros con puerta, construida en la partida |
| H2.5 Muebles y camas | Defs de cama, mesa, silla y cofre; colocación con orientación; cofre como almacén; asignación de camas; descanso mejorado | game-designer, sim-engineer, art-integrator, godot-engineer, i18n-specialist | 8–10 | Escenario "colono con cama asignada descansa más rápido que en el suelo"; muebles colocados y vistos en la casa |
| H2.6 Gestión de la colonia | Panel de prioridades; lista de colonos; alertas de ocioso y de hambre crítica; argumentos de entidad en `TextResolver` | sim-engineer, godot-engineer, i18n-specialist | 10–12 | Cambiar una prioridad cambia qué trabajo toma un colono (test y partida); alertas en es y en con el nombre del colono |
| H2.7 Integración | Playtest con checklist, balance, rendimiento, bugs, exportación y release `v0.2.0` | qa-tester, game-designer, `/code-review`, humano | 8–10 | `v0.2.0` exportada y checklist de playtest completo |

## Pendientes de IP1 que entran en IP2

- Guardar el nombre de la aldea en la simulación: H2.1.
- Pantalla de error al arrancar (backlog): H2.1, junto con el error al cargar una partida.
- El día en el registro de eventos (backlog, hallazgo del playtest): H2.2.
- Volver a correr los tests del invierno con el sueño nocturno: H2.2.
- Argumentos de entidad y de def en `TextResolver`: H2.6, con las alertas, que son el primer texto que los usa.
- Suavizar el fondo del valle bajando `terrainAmplitude` en `worldgen.json`: en H2.4 bajó de 3 a 2. Cambió las huellas.

Quedan en el backlog: variantes de aldeanos y animación de caminar, el arbusto que muestra las bayas, el cambio de modelos con la época y la agrupación de miles del español según CLDR.

## Definición de hecho de IP2

- Guardar a mitad de partida y seguir da la misma huella que no haber cortado, en al menos tres semillas y con construcciones, almacenes y camas en juego.
- Un archivo de otra versión o dañado se rechaza con un mensaje localizado, sin cerrar el juego.
- Una casa de muros con puerta, camas y un almacén se construye en una partida, sin tocar código.
- Los colonos duermen de noche y la colonia sigue pasando el invierno si se recolecta en otoño.
- El ciclo de día y noche no cambia las huellas de la simulación.
- Todo texto nuevo en es y en, con cambio de idioma en vivo.
- `dotnet test`, smoke test y test de determinismo en verde. Capturas es/en revisadas.
- Presupuesto: menos de 2 ms por tick de media y 60 FPS con la aldea construida en el mapa de IP1.

## Registro de H2.1: Guardado y carga

**Entregado**

- Spec `docs/gdd/guardado.md` y ADR-010.
- `Simulation.ColonyName`, fijado por `ColonyStart.Settle` y parte de la huella. Las huellas del smoke cambiaron: `580DB895C5F5B361` con la semilla 1 y `25578FB766A30761` con la 42 (5000 ticks).
- `SaveGame` en Core: ZIP con `header.json` y `state.json`; materiales por id; nombres estables en `SaveTypes`; `[DefRef]` para validar ids de defs al cargar; `SaveLoadException` con motivo (`Corrupt`, `Newer`, `Older`, `MissingContent`).
- `SaveSlots` en el juego: una ranura por archivo en `user://saves`, escritura atómica, ranuras dañadas listadas para poder borrarlas.
- Autoguardado al empezar cada día del juego, en una ranura propia de la partida.
- Pantallas Cargar partida, Guardar partida (desde la pausa) y Error, con confirmación para borrar y reemplazar. "Cargar partida" se habilita con alguna ranura.
- Error al arrancar en pantalla, en el idioma del jugador si los textos cargan.
- Opciones `--saves`, `--load` y `--show load|save|error`; el smoke guarda, carga y compara huellas.
- 23 textos nuevos en es y en.

**Verificación**

- Continuidad: en Core, con una colonia que excava, tala, recolecta, tiene una orden inalcanzable y un comando pendiente; con el contenido real, en las semillas 1, 7 y 42, cortando a los 20 000 ticks y siguiendo otros 20 000. Misma huella al cargar y al final.
- 409 tests en verde; smoke con código 0. Un valle guardado pesa 33 KB y se guarda o se carga en unos 30 ms.
- Capturas revisadas en es y en: menú principal con Cargar habilitado, Cargar partida, Guardar partida y Error.

**Revisión de código:** 10 hallazgos, 9 corregidos.

- La cabecera se lee sin rechazar campos desconocidos: una partida de una versión más nueva se informa como tal y no como dañada.
- Guardar no lanza desde el autoguardado si falta registrar un componente, y no deja el archivo temporal.
- `--load` con una ranura que no carga sale con código 1 en las ejecuciones automáticas.
- Listas `null` en el estado o en un componente, y entidades fuera del mapa, se rechazan como partida dañada.
- Borrar una ranura bloqueada ya no lanza desde el botón.
- El botón del error sin textos usa el ícono de cierre del motor, y la línea del autoguardado es un solo mensaje (`ui.slot.autosaved_at`).
- Comentario de documentación de `BuildScreens` en su lugar.
- Compresión: se probó `Fastest` por el tirón del autoguardado, pero ahorra poco tiempo (el costo está en el JSON) y duplica el tamaño. Queda `Optimal`, y la escritura en segundo plano pasa al backlog.
- Descartado: convertir en error de carga una excepción de `ColonySystems.AddTo`. Si el contenido no cumple las reglas de la colonia, tampoco arranca una partida nueva, y eso ya lo cubren los tests del contenido.

**Segunda ronda de revisión:** 10 hallazgos, 8 corregidos.

- La cabecera se valida: textos nulos o números negativos son una partida dañada, también al listar.
- Los enums se leen sólo por nombre, y toda celda de un componente (camino, trabajo, objetivos inalcanzables) debe estar dentro del mapa.
- Guardar tampoco lanza si un componente no se puede serializar.
- La pantalla de error de un arranque fallido no puede fallar ella misma: cualquier error vuelve a la versión sin traducir.
- Una partida que se lee bien pero que la pantalla no puede mostrar da el error de partida dañada.
- `SaveGame.Load` también devuelve la cabecera: cada ranura se lee una sola vez.
- `--saves` acepta rutas de Godot (`user://…`).
- `Settle` nombra la aldea al final, para que un fallo no deje un nombre sin colonia.
- El smoke guarda a mitad de sus ticks y sigue las dos partidas hasta el final: lo que informa y dibuja es la partida que corrió sus ticks, con las mismas huellas.
- Quedan como están: el tirón del autoguardado (en el backlog, unos 30 ms por día del juego) y validar la consistencia profunda de un archivo bien formado pero editado a mano. La partida la escribe el juego; los controles cubren archivos truncados, de otra versión o con datos imposibles.

**Tercera ronda de revisión:** 10 hallazgos, 8 corregidos.

- Se rechazan como dañados: elementos nulos dentro de una lista, un paso de camino fuera del camino y un mapa cuyo tamaño no coincide con sus arreglos. El tamaño se comprueba antes de reservar la memoria del mapa.
- Un `Job.Target` muerto no se valida: los sistemas ya lo toleran, porque un objetivo puede morir en una partida normal.
- Se quitó la captura amplia de excepciones al armar la pantalla de una partida cargada, que tapaba errores de programación: la validación vive en Core.
- Ids de ranura: cualquier nombre de archivo válido (`SaveSlots.IsValidId`); un `GameId` que no sirve para nombrar el autoguardado se reemplaza por uno nuevo, y guardar no lanza por un id inválido.
- `ConfirmDialog`: Esc cierra la pregunta y no la pantalla de debajo.
- `--show save` falla sin `--saves`, o sin `--play` ni `--load`.
- La caché de propiedades guarda también el tipo de def de `[DefRef]`.
- Quedan como están: el tirón del autoguardado (backlog) y la repetición de "cargar y agregar los sistemas" en el smoke, que carga en memoria y no desde una ranura.

## Registro de H2.2: Día, noche y sueño

**Entregado**

- Spec `docs/gdd/dia-y-noche.md`.
- `AiConfig.nightStartHour` y `nightEndHour` (21 a 6) y `NeedDef.nightThreshold` (10 000 en el descanso). De noche se acuesta antes que cualquier trabajo todo colono que no esté del todo descansado, y no se levanta hasta las 6. De día sólo duerme si está agotado.
- Balance: el descanso baja 500 por hora y sube 1200 durmiendo. Las huellas del smoke cambiaron: `19A91FF3BCECB119` con la semilla 1 y `9D2B68F847F36435` con la 42 (5000 ticks).
- `DayNightCycle`: el sol sale y se pone con la noche de los colonos (leída del `AiConfig`), una luna fría hace el arco de vuelta de noche, y luz, ambiente y cielo se mezclan entre amanecer, día, atardecer y noche. Opción "Ciclo de día y noche" en Opciones, guardada.
- El registro muestra la fecha antes de los mensajes de cada día, sin pasar de 7 líneas.
- `--hour <h>` para capturas.

**Verificación**

- `NightSleepTests`: a las 21 dejan el trabajo y duermen hasta las 6; de día no duermen cansados y sí agotados; siguen durmiendo llenos hasta las 6; vuelven a la cama después de comer de noche; noches que cruzan o no la medianoche. Con el contenido real, la aldea entera duerme a las 2 y nadie al mediodía durante tres días, en las semillas 1 y 42.
- Los tests del invierno de `ColonyTests` siguen en verde con el balance nuevo, y la continuidad del guardado también.
- 429 tests en verde; smoke con código 0.
- Capturas revisadas a las 6, 12, 20, 22 y 0, y Opciones en inglés. El ciclo vive en la presentación: no toca la simulación.

**Revisión de código:** 9 hallazgos, todos corregidos.

- Un colono despierto de noche con el descanso alto (por ejemplo, después de dormir de día por agotamiento) se quedaba despierto toda la noche, y uno que se levantaba a comer no volvía a la cama. `nightThreshold` pasa de 9000 a 10 000, y su validación exige que no sea menor que el umbral bajo.
- La luz saltaba de lado al amanecer y al anochecer: la luna ahora hace el arco de vuelta.
- El sol sale y se pone con las horas del `AiConfig`, no con horas fijas en el código.
- `--hour` avanza de una vez hasta el primer tick a esa hora o después, sin riesgo de no terminar.
- El registro no crece con las líneas de fecha.
- El ciclo no reescribe la luz si no cambió el minuto ni la opción.

**Segunda ronda de revisión:** 10 hallazgos, 7 corregidos.

- `DayNightCycle` libera su referencia al ambiente al borrarse, no al salir del árbol; usa `TimeSettings.MinutesPerDay`; y con un día o una noche cortos achica las transiciones para que no se crucen.
- La vista toma la noche de `ColonyRules`, sin horas de respaldo en el código.
- `JobSystem` calcula la noche una vez por tick.
- El registro guarda sólo los mensajes que entran con su línea de fecha.
- `--hour` sin `--play` ni `--load` falla con código 2.
- Quedan como están, porque es lo que pide la spec: de día el umbral bajo no lleva a dormir; a las 21 todos dejan el trabajo, aunque les falte poco; y quien vuelve a la cama de madrugada se levanta con el descanso lleno, aunque sea pasadas las 6.

## Registro de H2.3: Almacenes y acarreo

**Entregado**

- Spec `docs/gdd/almacenes.md`.
- Def `ItemCategory` (madera, piedra y comida) y `Item.category`. Tipo de trabajo `hauling` con `haul`, después de recolectar.
- Componentes `Stockpile` y `Carried`, `DesignateCommand` con `Stockpile` y `Cancel` que quita celdas, `SetStockpileFilterCommand` y `StockpilesChangedEvent`, todo registrado en `SaveTypes`.
- Un almacén toma suelos planos, libres y fuera de otras zonas. Cada celda guarda una pila: la de menor id; cualquier otra que caiga ahí queda suelta y se acarrea.
- Acarreo: la pila suelta más cercana con destino, y el destino más cercano a la pila que la acepte y tenga lugar. Se reservan la pila (hasta levantarla) y la celda. Sin camino al destino no se levanta nada y la pila se deja de lado un rato. Quien deja el trabajo con algo en las manos lo suelta donde está; si muere, también.
- `StorageIndex` derivado, compartido por la decisión y los trabajos, armado una vez por tick.
- Godot: herramienta Almacén (Z), celdas pintadas de azul, filtros Sí/No en el inspector de una celda, "Lleva…" en el de un colono y el objeto en las manos. Las órdenes del smoke y de `--orders` incluyen un almacén; `--inspect-stockpile` para capturas.

**Verificación**

- `StockpileTests`: troncos apilados en un almacén de madera (una pila por celda, ninguna sobre el límite), filtro de comida, el que tiene hambre suelta la carga y después se guarda, dos acarreadores nunca comparten pila ni destino, una segunda pila en una celda se va a otra, Cancelar y celdas no válidas.
- La continuidad del guardado incluye un almacén, un filtro y un colono cargando, en Core y con el contenido real en tres semillas.
- 436 tests en verde; smoke con código 0, con todos los troncos guardados en las semillas 1 y 42. Huellas nuevas por el almacén de las órdenes del smoke: `70636FFDB3474767` con la semilla 1 y `0988439535AF5339` con la 42.
- Capturas revisadas: acarreo y carga en es, filtros en en.

**Revisión de código:** 10 hallazgos, todos corregidos.

- Una entrega que no entraba, o una carga que caía en una celda de almacén, dejaba dos pilas en la celda para siempre. Ahora la celda guarda sólo la primera, las demás quedan sueltas y la celda no recibe nada hasta que se van.
- Un acarreador con la carga en las manos ya no reserva lo que quedó de la pila.
- Las reservas de celdas se cuentan: una celda puede ser destino de un acarreo y orden de escalera a la vez.
- Una celda de almacén que deja de ser suelo plano (una escalera) no recibe pilas.
- Antes de levantar se busca el camino al destino; si no hay, la pila se deja de lado un rato y no se repite el intento en cada tick.
- Un solo índice de almacenes por tick para los dos sistemas.
- `DesignateCommand` con `Stockpile` no recorre las plantas; el inspector reutiliza su lista al buscar la zona; documentación de `SelectCell` y `SelectColonist` en su lugar.

**Segunda ronda de revisión:** 8 hallazgos, 7 corregidos.

- Un colono con hambre podía ir a comer de una pila que un acarreador estaba por levantar entera, y llegar para nada. Nadie va a comer de una pila que alguien acarrea.
- Las pilas sin destino gastaban los intentos de buscar camino: varias pilas cercanas de comida sin lugar alcanzable dejaban sin acarrear una pila de troncos más lejana que sí lo tenía. Sólo cuenta como intento la búsqueda de un camino, y un objeto sin destino se descarta para el resto de la búsqueda.
- Sin camino al destino, el colono deja de lado esa celda un rato, no la pila: puede llevarla a otra.
- El índice de almacenes se vuelve a armar en el mismo tick cuando un acarreador levanta, entrega o suelta algo.
- Una entrega parcial avisa a la vista, que mostraba la carga vieja.
- Los ids de categoría de un almacén se validan al cargar: `[DefRef]` admite listas de ids.
- El smoke cuenta como guardado sólo lo que el almacén acepta y conserva (`Stockpiles.StoredItems`).
- Queda como está: `ItemPiles.Drop` recorre todas las pilas para encontrar las de una celda; pasa pocas veces por tick. Se corrigió su comentario.
- 439 tests en verde; smoke con código 0 y las mismas huellas.

## Registro de H2.4: Construcción

**Entregado**

- Spec `docs/gdd/construccion.md`.
- Materiales de construcción: `MaterialDef.builtFrom` (madera desde troncos, mampostería desde piedra) y `MaterialKind.Wood`. Una celda es construida si su material tiene `builtFrom`.
- Def `Buildable` (muro, suelo, puerta, rampa y escalera) con forma, materiales, costo, trabajo, `outline` y `order`. `AiConfig.deconstructRefundPercent` (50).
- Forma `Door` del mapa. Órdenes Desmontar (V) y Canalizar (H); Excavar y las escaleras sólo toman celdas naturales; Cancelar quita planos y deja lo entregado.
- `PlaceBlueprintCommand` y el componente `Blueprint`, registrados en `SaveTypes`. Trabajos `deliver`, `build` y `deconstruct` en el tipo nuevo `construction` (antes del acarreo) y `channel` en minería.
- Godot: herramienta Construir (C) con la fila de construibles y materiales, planos como cajas translúcidas del color del material, pieza de la puerta, marcas de desmontar y canalizar, el plano en el inspector. `--orders` y el smoke suman una casa de 5 × 5 con puerta; `--build` e `--inspect-blueprint` para capturas.
- `terrainAmplitude` bajó de 3 a 2 para que haya sitios llanos donde construir.

**Verificación**

- `ConstructionTests`: muro de troncos del almacén, plano que espera material, el muro no sube sobre un colono, borde de la caja, casa de cuatro muros con puerta y paso por la puerta, canal con rampa, desmontar, escalera de dos niveles, rampa junto a un muro, cancelar, plano que pierde su lugar, órdenes y planos que se excluyen, constructores vecinos, suelo firme y desmontar una escalera con algo arriba. Validaciones de los defs nuevos.
- La continuidad del guardado incluye planos, un canal y un muro por desmontar.
- 461 tests en verde; smoke con código 0, con la casa construida (15 de 16 celdas con la semilla 1, que se queda sin troncos; entera con la 42). Huellas: `7D097C8EDD26F1AC` con la semilla 1 y `B1F8CEE707B258FC` con la 42.
- Capturas revisadas: planos con la barra y el inspector en es y en; la casa terminada con su puerta.

**Revisión de código:** 10 hallazgos, todos atendidos.

- Dos constructores de muros vecinos podían esperarse para siempre, cada uno parado sobre el plano del otro. Nadie construye, canaliza ni desmonta desde una celda que otro trabajo va a cerrar o abrir.
- Desmontar una escalera desde abajo dejaba en el aire lo que había en la celda de arriba, y no esperaba a quien estuviera parado ahí. Las dos mitades quedan reservadas para quien desmonta.
- Un muro sobre un suelo natural que quedó sobre aire volvía, al desmontarlo, como un suelo construido que se podía desmontar otra vez por más material. Los muros, puertas, rampas y escaleras van sólo sobre suelo firme.
- Canalizar rearma el índice de almacenes; `PlaceBlueprintCommand` lleva `[DefRef]`; documentación y un buffer propio en `JobSystem`; el smoke busca la escalera una vez.
- La entrega elige como el acarreo (la pila más cercana al colono y el plano más cercano a la pila): se corrigió la spec.

## Registro de H2.5: Muebles y camas

**Entregado**

- Spec `docs/gdd/muebles-y-camas.md`.
- Los muebles son `Buildable` con `shape: furniture`, `visual`, `restRecoveryPercent` (cama) y `storage` (cofre): reusan planos, entrega, construcción y desmontaje de H2.4. Cama, mesa, silla y cofre de madera.
- `PlaceBlueprintCommand` con giro de 0 a 3, que conservan el plano y el mueble. Componentes `Furniture` y `Bed`; el cofre suma un `Stockpile` de su celda. Marca `TileFlags.Furniture` en el mapa.
- Camas: quien se acuesta sin cama toma la libre más cercana a la que llega; en su cama descansa al 150 %. La cama queda libre si su dueño ya no existe.
- Godot: modelos de la cama, la mesa y la silla del Furniture Kit y del cofre del Survival Kit (Kenney, CC0), centrados con `center` en el catálogo y girados; tecla G; el inspector muestra el mueble, el dueño de la cama y si un colono tiene cama. La casa del smoke se amuebla; `--inspect-furniture`.

**Verificación**

- `FurnitureTests`: descanso en cama contra suelo, tomar la cama libre, cama libre al morir el dueño, cofre que recibe troncos y se desmonta, giro, reglas de colocación, almacén y Cancelar con muebles. Validaciones de defs. La continuidad del guardado incluye cama, cofre y una mesa por construir.
- 471 tests en verde; smoke con código 0, con la casa amueblada y una cama tomada con la semilla 42. Huellas: `AC38B78A13976F40` con la semilla 1 y `5AB2EE5C863DA876` con la 42.
- Capturas revisadas: la casa amueblada en es y, de noche, un colono en su cama en en.

**Revisión de código:** 9 hallazgos, todos corregidos.

- Un almacén podía marcarse sobre un plano de mueble; al terminarse un cofre, la celda quedaba en dos zonas. Ahora el almacén saltea también los planos de muebles.
- Un cofre que se perdía por quedar sin suelo firme dejaba el índice de almacenes desactualizado en el mismo tick, y su orden de desmontar quedaba marcada para siempre.
- Un colono tomaba una cama aunque no encontrara el camino: ahora sólo la toma si puede llegar.
- La tasa de una cama se redondeaba hacia abajo: el resto de las necesidades cuenta en centésimas.
- Buscar el mueble de una celda recorría todos los muebles: la marca `Furniture` lo evita en las celdas sin mueble.
- Centrar un modelo conserva el desplazamiento propio de su raíz.
- `IHasVisual` vuelve a exigir visual; `DefVisuals.Of` reúne los visuales de los defs, incluidos los muebles.

