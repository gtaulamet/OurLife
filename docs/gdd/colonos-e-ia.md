# Colonos e IA (H1.6)

Spec de los colonos de la IP1: llegada al valle, necesidades, órdenes del jugador, trabajos, movimiento y muerte. Los números son el punto de partida y viven en defs (`scenario.json`, `ai.json`, `needs.json`, `work_types.json`); se ajustan en H1.8 con pruebas de juego.

## Llegada

El def `Scenario` describe el comienzo de la partida. Hasta IP7 el contenido tiene exactamente uno.

| Valor | Cantidad | Motivo |
|---|---|---|
| Criatura | `human` | Los colonos de la IP1 |
| Colonos | 5 | Alcance de la IP1 |
| Necesidades al llegar | Al azar entre 7000 y 10 000 | Que no coman ni duerman todos a la vez |
| Objetos | 30 provisiones | Alimentan a 5 colonos unos 3 días |

- **Punto de llegada:** la celda de superficie más cercana al centro del mapa que sea suelo llano, sin planta y en la región de conectividad más grande de la superficie. Un empate se resuelve por el índice de la celda.
- Los colonos aparecen en esa celda y en sus vecinas transitables del mismo nivel, en el orden norte, este, sur, oeste y diagonales. Si no alcanzan, se repiten.
- Las provisiones quedan en la celda de llegada.
- Cada colono tiene nombre y género. El género sale al azar, y el nombre de la lista de su criatura sin repetir mientras queden nombres.
- Todo sale del flujo aleatorio `colony.start`.

## Necesidades

Escala de 0 a 10 000. El hambre y el descanso usan los valores de `docs/gdd/contenido-ip1.md`.

- Cada necesidad baja `decayPerHour` puntos por hora de juego, con aritmética entera: el resto de cada tick se acumula y no se pierde.
- Cada `NeedDef` dice cómo se satisface (`satisfiedBy`): `eating` o `sleeping`. El código no conoce los ids `hunger` ni `rest`.
- **Comer:** un colono come de una pila de comida, una unidad cada `eatTicksPerUnit` ticks. Cada unidad suma la `nutrition` del objeto. Deja de comer cuando le falta menos de media unidad para llenarse o cuando se termina la pila.
- **Dormir:** se duerme en el suelo, donde esté. Mientras duerme, la necesidad no baja y sube `recoveryPerHour` puntos por hora; se despierta al llenarse. El hambre sigue bajando mientras duerme.
- **Muerte:** si una necesidad con `deathAfterHoursAtZero` pasa ese tiempo en cero, el colono muere. El hambre mata tras 72 horas en cero; el descanso no mata.
- Cuando muere el último colono, la partida termina.

## Órdenes del jugador

El jugador marca un área (una caja de celdas) con una orden. El comando `DesignateCommand(orden, desde, hasta)` se aplica al principio del tick siguiente.

| Orden | Marca | Condición |
|---|---|---|
| Excavar | Los muros de la caja | Ser muro |
| Escalera hacia abajo | Las celdas de la caja | Ser suelo o escalera hacia arriba, no ser el nivel 0, y que debajo haya muro, suelo o escalera hacia abajo |
| Talar | Las plantas de la caja que se pueden talar | El def de la planta tiene `chop` |
| Recolectar | Las plantas de la caja que se pueden recolectar | El def de la planta tiene `harvest` |
| Cancelar | Quita toda orden de la caja | — |

- Las órdenes sobre celdas son banderas del mapa; las órdenes sobre plantas, componentes de la planta.
- Una escalera no se marca sobre una planta, ni si la celda o la de abajo están bloqueadas.
- Una orden que pierde sentido, por ejemplo una celda que ya no es muro, se ignora y se borra cuando un colono la revisa.
- Cada vez que una orden cambia, también cuando un colono la cumple o la borra, se emite `DesignationsChangedEvent`.

## Trabajos

Cada trabajo de una orden pertenece a un solo tipo de trabajo. Cada `WorkType` lista sus trabajos en `jobs` y fija con `order` en qué orden los eligen los colonos. Comer, dormir y deambular no pertenecen a ningún tipo.

| Trabajo | Tipo | Dónde se para el colono | Duración | Resultado |
|---|---|---|---|---|
| Excavar | `mining` | Una vecina transitable del mismo nivel, también en diagonal | `mineWorkTicks` del material | El muro pasa a suelo del mismo material. Puede dejar `minedItem` con `minedItemChance` sobre 10 000 |
| Escalera hacia abajo | `mining` | La celda o una vecina del mismo nivel | `mineWorkTicks` del material de abajo | La celda pasa a escalera hacia abajo (o en ambos sentidos) y la de abajo a escalera hacia arriba (o en ambos sentidos). Si abajo había muro, puede dejar `minedItem`. La celda de abajo conserva su propia orden de escalera, así un hueco de escalera sigue bajando |
| Talar | `woodcutting` | Una vecina del mismo nivel, o la celda si la planta no bloquea | `chop.workTicks` | La planta desaparece y deja `chop.count` de `chop.item` en su celda |
| Recolectar | `gathering` | Igual que talar | `harvest.workTicks` | Deja `harvest.count` de `harvest.item` en la celda, o en la del colono si la planta la bloquea, como un frutal. La planta vuelve a dar fruto en `regrowDays` días |
| Comer | — | La celda de la pila | `eatTicksPerUnit` por unidad | Ver necesidades |
| Dormir | — | Donde está | Hasta llenarse | Ver necesidades |
| Deambular | — | Una celda al azar a `wanderRadius` o menos | Camina y espera de `wanderWaitMin` a `wanderWaitMax` ticks | — |

- Un trabajo se hace en diagonal sólo si un colono podría pasar por esa diagonal: ninguna de las dos celdas de los costados es muro. Si no, la celda excavada o talada, y lo que cae en ella, quedarían donde nadie puede entrar.
- Los objetos nuevos se apilan con los del mismo tipo que haya en la celda, hasta `stackLimit`; el resto forma pilas nuevas.
- Una planta recolectada pierde la orden de recolectar. El jugador la vuelve a marcar si quiere otra cosecha.
- Las plantas que bloquean el paso (`blocksMovement`) marcan su celda como bloqueada. Una celda bloqueada no es transitable, así que la conectividad y las rutas las rodean. Al talarlas, la celda se libera.

## Decisión

Un colono sin trabajo elige uno con esta prioridad:

1. **Necesidad crítica** (por debajo de `criticalThreshold`): la satisface. Si hay varias, la que figura primero en la criatura.
2. **Trabajo:** el trabajo disponible más cercano del primer tipo de trabajo, según `order`, que tenga alguno.
3. **Necesidad leve** (por debajo de `lowThreshold`): la satisface.
4. **Deambular.**

- **Comida para una necesidad:** la pila de comida alcanzable más cercana. Si no hay ninguna, recolecta el arbusto maduro alcanzable más cercano, aunque no tenga orden, y en cuanto termina come lo que recolectó, antes de cualquier otro trabajo.
- **Más cercano:** por distancia estimada sobre la grilla (octil en el plano y niveles en vertical); un empate se resuelve por el índice de la celda o el id de la entidad. Sólo cuentan los objetivos de la misma región de conectividad.
- **Reservas:** dos colonos no toman el mismo objetivo. Una pila de comida admite tantos comensales como unidades tenga. Una escalera reserva también la celda de abajo, que se excava con ella.
- **Interrupciones:** cada `recheckTicks` ticks, escalonados por colono, un colono ocupado se revisa:
  - Si una necesidad se volvió crítica y hay cómo atenderla, deja el trabajo o el deambular y la atiende. Comer y recolectar para comer no se interrumpen.
  - Un colono que deambula busca trabajo o una necesidad leve que atender.
  - El sueño sólo se interrumpe por otra necesidad crítica.
  - Revisar de a intervalos evita que una necesidad sin solución, por ejemplo sin comida alcanzable, busque en todo el mapa en cada tick.
- **Objetivos inalcanzables:** si el A* no encuentra ruta, el colono ignora ese objetivo durante `unreachableCooldownTicks`.

## Movimiento

- Se camina con las reglas de `docs/gdd/mapa-y-generacion.md`, sin pasar por celdas bloqueadas.
- Un paso recto, por rampa o por escalera cuesta `moveTicksPerTile` ticks; un paso en diagonal, 1,4 veces eso. Se cuenta en enteros y el sobrante pasa al paso siguiente.
- **A\*** 3D con heurística admisible: el máximo entre la distancia octil en el plano y los niveles a subir o bajar.
- Si un paso deja de ser posible porque el mapa cambió, el colono abandona el trabajo y vuelve a decidir.
- Varios colonos pueden ocupar la misma celda.
- **Conectividad:** la simulación la mantiene al día. Excavar, talar o abrir escaleras sólo abre caminos, así que se unen en el lugar las regiones de las 3 × 3 celdas alrededor de cada cambio. Un cambio que puede cerrar un camino la calcula de nuevo. El generador la calcula una vez y se la pasa a la simulación.
- **Superficie encerrada:** si un anillo de árboles separa parte de la superficie, el generador busca, desde la primera celda encerrada, el camino hasta la región principal que atraviesa menos árboles, y quita sólo esos. Repite hasta que la superficie vuelve a ser una sola región. Los árboles dentro del área encerrada se conservan.

## Plantas

- En el tick en que se cumplen sus `regrowDays`, un arbusto recolectado vuelve a estar maduro.
- Salvo en sus `dormantSeasons` (ids de estación del calendario): ahí no rebrota y espera a la primera estación que no está en la lista. Un arbusto que no se recolectó conserva sus frutos.

### Balance del invierno (H1.8)

El arbusto de bayas duerme en invierno, así que el invierno es la amenaza de IP1. Con 10 bayas por recolección, 3 días de rebrote y 400 de nutrición por baya:

- Una colonia que nunca recolecta llega al invierno sin reservas y muere entre los días 11 y 14.
- Una colonia que recolecta en otoño los arbustos de un radio de 25 celdas alrededor de la aldea pasa el invierno y llega a la primavera con comida de sobra.

Los dos casos son tests del contenido real (`ColonyTests`), con las semillas 1 y 42.

## Contrato con la presentación (H1.7)

| Tipo | Nombre | Uso |
|---|---|---|
| Comando | `DesignateCommand(orden, desde, hasta)` | Las órdenes del jugador |
| Evento | `EntitySpawnedEvent`, `EntityChangedEvent`, `EntityRemovedEvent` | Colonos, objetos y plantas que aparecen, cambian (por ejemplo, un arbusto recolectado) o desaparecen |
| Evento | `DesignationsChangedEvent(desde, hasta)` | Redibujar las marcas de órdenes |
| Evento | `NotificationEvent` | `event.need.<necesidad>.death`, como `event.need.hunger.death`, y `event.colony.lost` |
| Evento | `ColonyLostEvent` | Fin de partida |
| Consulta | Componentes `Colonist`, `Needs`, `Job`, `Movement`, `ItemStack`, `Plant`, `Harvested` y banderas del mapa | Inspector, HUD y render |

- El texto del trabajo actual es `job.<trabajo>.report`, por ejemplo `job.chop.report`.
- Los mensajes de un colono muerto llevan su nombre en `colonist` y su género en `colonist_gender`, porque la entidad ya no existe.

## Flujos aleatorios

| Flujo | Uso |
|---|---|
| `colony.start` | Género, nombre y necesidades al llegar |
| `colony.decision` | Destino al deambular |
| `colony.jobs` | Espera al deambular y objetos que deja la roca |

## Criterios de aceptación

1. Misma semilla y mismas órdenes ⇒ misma huella del estado, con colonos.
2. Escenario "5 colonos 3 días con bayas": cinco colonos con hambre baja, sin provisiones y con arbustos cerca. A los 3 días siguen vivos y ninguno tiene hambre crítica.
3. Escenario "área designada ⇒ excavada": un bloque de muro marcado para excavar termina convertido en suelo.
4. Un colono que llega a 0 de hambre y pasa 72 horas sin comer muere. Si era el último, la partida termina.
5. Dos colonos nunca trabajan sobre el mismo objetivo.
6. Talar un árbol libera su celda para las rutas.
7. Con el valle real y el escenario real, 5 colonos siguen vivos tras 3 días.
8. Menos de 2 ms por tick de media en el smoke.
