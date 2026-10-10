# ADR-011: Datos derivados de la colonia fuera de la huella

- **Estado:** aceptada
- **Fecha:** 2026-10-10
- **Relacionada con:** ADR-003, ADR-004

El número 010 queda reservado para el guardado de IP2, como anuncia el plan maestro.

## Contexto

H1.6 trae colonos que eligen trabajos, reservan objetivos, buscan rutas y cambian el mapa al excavar y talar. Para decidir rápido hacen falta varias estructuras de apoyo:

- Las regiones de conectividad.
- Las reservas de objetivos.
- La lista de celdas con órdenes.
- Saber qué celdas ocupan los árboles.

Si esas estructuras fueran estado, habría que guardarlas, incluirlas en la huella (ADR-004) y mantenerlas consistentes con el resto. Si se recalcularan enteras en cada tick, no alcanzaría el presupuesto de 2 ms por tick: la conectividad del valle de IP1 tarda unos 20 ms.

## Decisión

**Estado, en la huella:**

- El mapa, incluidas las banderas nuevas: `Blocked` (una planta impide el paso), `MineDesignation` y `StairsDesignation`.
- Los componentes de las entidades: `Colonist`, `Needs`, `Job`, `Movement`, `ItemStack`, `ChopOrder`, `HarvestOrder` y `Harvested`.

**Derivado, fuera de la huella y reconstruible desde el estado:**

| Dato | Dónde | Cómo se mantiene |
|---|---|---|
| Conectividad | `Simulation.Connectivity` | Se pone al día al consultarla. Si los cambios del mapa sólo abrieron caminos (un muro excavado, una celda liberada, un suelo vuelto escalera), se unen en el lugar las regiones de las 3 × 3 celdas alrededor de cada cambio. Si alguno pudo cerrar un camino, se calcula de nuevo |
| Reservas | `DecisionSystem` | Se reconstruyen en cada tick a partir de los `Job` de los colonos |
| Celdas con órdenes | `GameMap.OrderedCells` | Un conjunto ordenado que se actualiza con cada cambio de banderas |

- La bandera `Blocked` duplica a propósito un dato de las plantas: la navegación consulta sólo el mapa, sin buscar entidades. Sólo el Core la escribe, en el generador y al talar, siempre junto con la planta.
- Las reglas de movimiento son simétricas y cada camino que abre un cambio tiene un extremo en el nivel del cambio, junto a él. Por eso alcanza con revisar 3 × 3 celdas. Un test compara, después de cada cambio de una secuencia al azar, la conectividad mantenida con una calculada de nuevo.
- Los números de las regiones pueden diferir entre la versión mantenida y una calculada de nuevo. Ninguna decisión depende de ellos, sólo de si dos celdas comparten región. Así, cargar una partida y recalcular da las mismas decisiones.

## Consecuencias

- Excavar o talar cuesta lo que mide la región más chica que se une, casi siempre una sola celda, en lugar de 20 ms. El smoke mide unos 0,01 ms por tick con cinco colonos trabajando.
- El guardado de IP2 no necesita escribir estos datos: los reconstruye al cargar.
- Si un cambio futuro puede cerrar caminos con frecuencia, como construir muros o puertas, cada uno vuelve a calcular la conectividad entera. Habrá que hacerla incremental también para esos casos, o pasar a HPA* en IP5.
- `GameMap.SetTileQuietly` no registra cambios: es sólo para construir un mapa nuevo, antes de calcular la conectividad.
