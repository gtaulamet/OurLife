# Mapa y generación del valle (H1.4)

Spec del mapa 3D, la generación del valle de la IP1 y la conectividad. Los números son el punto de partida y viven en el def `WorldGen` (`game/content/defs/worldgen.json`); se ajustan en H1.8 con pruebas de juego.

## Modelo del mapa

- Celdas en una grilla de `ancho × largo × profundidad`. `x` va de oeste a este, `y` de norte a sur y `z` de abajo hacia arriba: `z = 0` es el nivel más profundo.
- Cada celda tiene tres datos, guardados en arrays densos (ADR-003):

| Dato | Valores |
|---|---|
| Forma | Vacía (aire), suelo, muro, rampa, escalera hacia arriba, escalera hacia abajo, escalera en ambos sentidos |
| Material | Índice del `MaterialDef`, o ninguno en el aire |
| Banderas | `pasto`, `bloqueada` (una planta impide el paso) y las órdenes de excavar y de escalera; la niebla llega en IP4 |

- Un **muro** es roca o tierra maciza. Un **suelo** es una celda abierta con piso: el material del suelo es el del piso. Una **rampa** es un suelo inclinado que sube al nivel de arriba.
- Árboles, arbustos, objetos y criaturas no son celdas: son entidades con `Position`.
- El mapa se divide en chunks de 16×16 celdas por nivel. Cambiar una celda marca como sucios su chunk y los de sus vecinas, para que el render reconstruya sólo eso.
- Sólo el Core cambia el mapa; la presentación lo lee y envía comandos.

## Movimiento y conectividad

Una celda es **transitable** si es suelo, rampa o escalera. Desde una celda transitable se puede pasar:

| Hacia | Condición |
|---|---|
| Las 8 vecinas del mismo nivel | Que sean transitables. En diagonal, ninguna de las dos celdas ortogonales puede ser muro |
| Una de las 4 vecinas del nivel de arriba | Estar en una rampa, que la celda sobre la rampa no sea muro y que la vecina sea transitable |
| Una rampa de las 4 vecinas del nivel de abajo | La inversa de la anterior |
| La celda de arriba o de abajo | Escaleras compatibles: hacia arriba o en ambos sentidos abajo, y hacia abajo o en ambos sentidos arriba |

Las **regiones de conectividad** agrupan las celdas que se alcanzan entre sí. Sirven para descartar al instante objetivos inalcanzables antes del A* de H1.6. Desde H1.6, una celda con una planta que bloquea el paso, como un árbol, tiene la bandera `bloqueada` y no es transitable. La conectividad y las rutas la rodean, y la simulación la mantiene al día mientras los colonos excavan y talan (`docs/gdd/colonos-e-ia.md`).

## Generación del valle

Todo sale del RNG de la simulación, con un flujo por capa (`worldgen.terrain`, `worldgen.soil`, `worldgen.vegetation`…). La misma semilla y el mismo contenido dan siempre el mismo mapa.

1. **Altura de la superficie.** El valle corre de norte a sur con un eje que serpentea. En el fondo, la superficie está en `valleyFloor`. Hacia los bordes sube con una curva suave hasta `hillHeight` niveles más, y un ruido de `terrainAmplitude` niveles le da relieve.
2. **Pendientes.** Entre dos celdas vecinas, norte-sur o este-oeste, la superficie difiere como mucho en un nivel. Los picos que lo violan se rebajan. Así toda la superficie queda conectada y las colinas forman terrazas.
3. **Columnas.** Debajo de la superficie hay muro; en la superficie, suelo con el material del muro de abajo; encima, aire.
   - **Tierra:** las primeras `soilDepthMin` a `soilDepthMax` capas bajo la superficie son de `soilMaterial`, con un ruido que varía el espesor.
   - **Roca expuesta:** desde `rockExposureElevation` niveles sobre el fondo del valle no hay tierra y la superficie es de piedra. El borde sube y baja hasta `rockExposureWobble` niveles.
   - **Estratos:** debajo de la tierra, capas horizontales listadas de abajo hacia arriba en `strata`. Cada una tiene un espesor, salvo la última, que llega hasta la tierra. Los límites ondulan hasta `strataWobble` niveles. Cada espesor debe superar el doble de `strataWobble`, para que una capa intermedia nunca desaparezca entre las que la rodean. Donde la superficie es baja, las capas de arriba pueden faltar, como en la geología real.
4. **Rampas.** Una celda de superficie con alguna vecina norte, sur, este u oeste un nivel más alta se vuelve rampa.
5. **Pasto.** El suelo de tierra lleva pasto en manchas, en una proporción cercana a `grassCoverage` sobre 10 000. Con 0 no hay pasto y con 10 000 lo cubre todo.
6. **Vegetación.** Sólo sobre suelo de tierra que no sea rampa, una planta por celda. Las reglas de `vegetation` se prueban en orden y la primera que acierta pone su planta.
   - Cada regla tiene una probabilidad sobre 10 000 y una franja de elevación respecto del fondo del valle.
   - Un ruido de bosque multiplica la probabilidad entre 0 y `groveFactorMax`, para que las plantas se agrupen en bosquecillos. Una probabilidad multiplicada por ese máximo no puede pasar de 10 000.

## Valores iniciales del valle

| Valor | Cantidad | Motivo |
|---|---|---|
| Tamaño | 96 × 96 × 32 | Alcance de la IP1 |
| `valleyFloor` | 14 | Deja de 9 a 14 niveles de roca debajo y aire de sobra arriba |
| `hillHeight` | 9 | Colinas que se ven desde el fondo del valle |
| `valleyHalfWidth` y `valleyMeander` | 14 y 8 celdas | Un fondo llano de unas 28 celdas que serpentea |
| `terrainAmplitude` | 2 niveles | Relieve sin terrazas excesivas, con sitios llanos para construir (bajó de 3 en H2.4) |
| Tierra | De 2 a 5 niveles | Como pide la IP1 |
| `rockExposureElevation` y `rockExposureWobble` | 7 y 2 niveles | Las cimas muestran roca con un borde irregular |
| Estratos de abajo hacia arriba, con `strataWobble` 1 | Granito de 6 niveles, caliza de 4, arenisca hasta la tierra | Al cavar desde el fondo del valle se cruzan arenisca y caliza, y el granito aparece unos 8 niveles más abajo |
| `grassCoverage` | 8 000 | Mucho pasto con claros de tierra |
| `groveFactorMax` | 2 | Bosquecillos con el doble de densidad y claros sin árboles |
| Arbusto de bayas | 150, elevación de −5 a 3 | Decenas de arbustos en el fondo del valle: hacen falta unos 30 para 5 colonos |
| Roble | 350, elevación de −5 a 4 | Bosquecillos en el valle |
| Pino | 400, elevación de 2 a 20 | Laderas y cimas |

## Criterios de aceptación

- La misma semilla genera el mismo mapa y las mismas plantas, celda por celda y con la misma huella del estado. Otra semilla genera otro mapa.
- La superficie de cada columna tiene muro debajo, suelo o rampa en la superficie y aire encima.
- La tierra tiene de 2 a 5 niveles, salvo donde la roca está expuesta. Bajo la tierra los estratos respetan el orden de `strata`, y cavando desde el fondo del valle se encuentran todos.
- Entre vecinas norte-sur o este-oeste la superficie difiere como mucho en un nivel. Cada escalón tiene su rampa en la celda baja.
- Toda la superficie forma una sola región de conectividad, con varias semillas.
- Las plantas sólo están sobre suelo de tierra, no sobre rampas, y hay como máximo una por celda. El valle real tiene al menos 30 arbustos de bayas.
- El valle de 96 × 96 × 32 se genera en menos de 2 segundos.
- El smoke test genera el valle y muestra el tiempo de generación, las plantas y las regiones de la superficie.

## Fuera de alcance

- Ríos, lagos y agua (IP6). Vetas de mineral y niebla subterránea (IP4).
- Elección de sitio y mapa del mundo (IP7).
- Caché de rutas y HPA*: IP5.
- La llegada de los colonos y las provisiones iniciales están en `docs/gdd/colonos-e-ia.md`.
