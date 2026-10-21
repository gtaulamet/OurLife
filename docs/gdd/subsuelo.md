# Vetas de mineral y niebla subterránea (IP4)

Parte de la spec de IP4 (`docs/gdd/oficios.md`): vetas de mineral de hierro generadas en el valle, y la niebla que oculta lo que no se descubrió bajo tierra. Es lo que `mapa-y-generacion.md` dejó como "vetas de mineral y niebla subterránea (IP4)" y lo que el modelo del mapa llamaba "la niebla llega en IP4".

**Estado:** propuesta de O1, pendiente de aprobación. Números iniciales, a ajustar en O11.

## Qué hay hoy

- Valle de 96 × 96 × 32. Estratos de abajo hacia arriba: granito (6 niveles), caliza (4), arenisca hasta la tierra; la tierra cubre las colinas bajas y las cimas por encima de 7 niveles sobre el valle muestran roca.
- Cada celda tiene forma, material y banderas (`pasto`, `bloqueada`, órdenes). Todo el subsuelo se dibuja.
- Cada material de roca puede tener `minedItem` y `minedItemChance` (25 % para las tres rocas). No hay mineral ni materiales de otro tipo.

## Vetas de mineral

Def nuevo `OreVein`, en `worldgen.json` (lo lee la generación, como `strata` y `vegetation`):

```json
{ "type": "OreVein", "id": "iron", "era": "medieval",
  "material": "iron_vein", "hosts": ["sandstone", "limestone"],
  "veins": { "min": 6, "max": 9 }, "cells": { "min": 10, "max": 24 },
  "outcrop": { "veins": 1, "radius": 40 } }
```

| Campo | Qué es |
|---|---|
| `material` | El material de las celdas de la veta |
| `hosts` | Rocas donde puede haber veta; sólo reemplaza celdas de muro de esas rocas |
| `veins` | Cuántas vetas se generan (de un mínimo a un máximo) |
| `cells` | Tamaño de cada veta, en celdas |
| `outcrop` | Cuántas vetas deben tener al menos una celda ya descubierta (ver la niebla) a `radius` celdas o menos del centro del valle, medidas en el plano |

### Material

```json
{ "type": "Material", "id": "iron_vein", "era": "medieval", "kind": "ore", "color": "#8A4B3A",
  "mineWorkTicks": 200, "minedItem": "iron_ore", "minedItemChance": 10000 }
```

- `MaterialKind` suma `ore` (mineral). El material no tiene `builtFrom`: no se construye con él.
- **`mineWorkTicks` 200:** más duro que la arenisca (100) y la caliza (120), parecido al granito (180). El pico de hierro lo acelera.
- **`minedItemChance` 10 000:** cada celda de veta excavada da una unidad de mineral. A diferencia de la roca (25 %), el mineral es poco y no se pierde.
- El color rojizo se ve sobre la arenisca (`#C9A26B`) y la caliza (`#C8BFA8`) con el tinte por material de ADR-009. El art-integrator puede sumar una textura con vetas.

### Cantidad

Entre 6 y 9 vetas de 10 a 24 celdas dan unas 135 celdas, 135 minerales y unos 67 lingotes (2 minerales por lingote): para un puñado de herramientas, y para las armas de IP5. Más vetas se agregan en el backlog con más metales.

### Generación

1. Después de los estratos y antes de la tierra, la generación sortea con el flujo `world.ore`, en orden de `OreVein` y de veta (no se itera por diccionario).
2. Para cada veta: elige un punto de partida entre las celdas de muro de las rocas `hosts` a 2 o más niveles bajo la superficie, y la hace crecer a lo ancho añadiendo vecinas que sean de esas rocas hasta el tamaño sorteado (si no cabe, se queda más chica, de 6 celdas como mínimo).
3. **Veta a la vista (`outcrop`)**: si después de generar las vetas no hay `outcrop.veins` con una celda descubierta a `radius` celdas del centro del valle, se vuelve a sortear el punto de partida de una veta entre las celdas de `hosts` que ya estén descubiertas dentro del radio, hasta que se cumpla o se agoten los intentos. Es lo que garantiza que el jugador encuentre hierro sin cavar a ciegas. Una semilla que no puede cumplirlo (valle sin roca expuesta) es un error de generación, que un test debe cazar en las semillas de prueba.
4. La veta no se mezcla con la tierra, el granito ni lo construido.

## Niebla subterránea

- La niebla es un dato del mapa: la bandera `Discovered` de cada celda. Se guarda y entra en la huella.
- **Regla única:** una celda está **descubierta** si ella o alguna de sus 26 vecinas (el cubo de 3 × 3 × 3) **no es un muro**. Es una regla de un sentido: una vez descubierta, la celda no se vuelve a ocultar (construir un muro no reoculta nada).
  - Al generar el valle: sale de la forma de cada celda. La superficie, la capa de roca o tierra que está justo bajo el suelo, las paredes de las colinas expuestas al aire y las cimas están descubiertas; el interior (dos o más capas bajo el suelo) no.
  - Al jugar: cada vez que una celda deja de ser muro (excavar, canalizar, escaleras, desmontar un muro), esa celda y sus 26 vecinas se descubren. El Core marca los chunks afectados como sucios, como con cualquier cambio de celda.
- **Cosas que no cambian:** la conectividad, las rutas, las órdenes y los colonos usan el mapa real. La niebla es de información para el jugador. Ningún colono "descubre" nada caminando: se descubre al abrir celdas.
- **Excavar a ciegas:** se puede ordenar excavar, canalizar o hacer una escalera en celdas ocultas (la orden no revela nada hasta que se cumple). Así un túnel de diez celdas es una sola orden. El inspector de una celda oculta dice "Sin explorar" y no nombra su material.
- **Lo oculto no se dibuja.** Los chunks no generan caras de celdas ocultas. En el corte por nivel Z, la cara superior de una celda oculta se pinta con un tono uniforme oscuro, para que se lea como roca sin explorar y no como un hueco (decisión del godot-engineer, con captura).
- **Vetas y niebla:** una veta sólo se ve en las celdas descubiertas. Una veta que asoma en una colina o una cima se ve desde el principio; el resto se descubre cavando.
- **Costo:** el cálculo inicial recorre el mapa una vez (96 × 96 × 32 celdas por 27 vecinas); descubrir al excavar toca 27 celdas por cambio. Entra en el presupuesto de generación (menos de 2 s) y de ticks.

## Migración (formato 17)

- Una partida de `v0.3.5` carga con `Discovered` calculado con la regla de arriba sobre el mapa actual: todo lo que ya se cavó y su entorno se ve.
- **Vetas en partidas anteriores** (decisión 4): al migrar se sortean con `world.ore` en celdas que sigan siendo muro natural de las rocas `hosts`, que no estén descubiertas ni sean vecinas de una celda abierta, con los mismos conteos. Si no hay lugar para el mínimo, se generan las que caben. Sin esto, las partidas viejas no podrían tener herrería.
- El centro del valle para `outcrop.radius` es el mismo que usa la generación (el eje del valle, no el centro de la aldea).

## Contrato

| Tipo | Nombre | Uso |
|---|---|---|
| Estado | `Map.Discovered(x, y, z)` | Bandera de celda; guardada y en la huella |
| Consulta | `Map.IsDiscovered(x, y, z)` | Render, inspector, el panel de orden |
| Evento | Cambio de celda existente | Marca sucios los chunks cuando se descubre |
| Def | `OreVein`, `Material.kind = ore` | `worldgen.json`, `materials.json` |
| Flujo | `world.ore` | Vetas |

- Comandos: ninguno nuevo. `DesignateCommand` no cambia.
- Textos: ver abajo. El Core no nombra materiales ocultos.

## Textos nuevos (propuesta)

| Clave | es | en |
|---|---|---|
| `def.material.iron_vein.name` / `desc` | veta de hierro | iron vein |
| `def.item.iron_ore.name` | mineral de hierro | iron ore |
| `ui.inspector.unexplored` | Sin explorar | Unexplored |
| `ui.inspector.ore_cell` | Veta de hierro | Iron vein |

## Modelos y arte

- Tinte rojizo del terreno para `iron_vein` en el catálogo (`visuals.json`) y, si se puede, una textura con motas oscuras.
- `item.iron_ore` (en `oficios.md`).
- El tono de la niebla en el corte por nivel; captura de comprobación con el valle recién generado y después de abrir un túnel.

## Criterios de aceptación

1. **Determinismo.** La misma semilla da las mismas vetas, celda por celda; otra semilla, otras.
2. **Vetas.** En las semillas 1, 7 y 42: entre 6 y 9 vetas de 10 a 24 celdas (o más chicas sólo si no caben), todas dentro de las rocas `hosts` y ninguna en tierra ni en granito.
3. **Veta a la vista.** En las semillas 1, 7 y 42, al menos una veta tiene una celda descubierta a 40 celdas o menos del centro del valle, en una pared, ladera o cima expuesta.
4. **Niebla inicial.** Una celda a dos capas bajo el suelo llano del valle está oculta; la superficie, la capa justo bajo ella y las paredes de las colinas, descubiertas. Más del 70 % de las celdas de roca del mapa está oculto al empezar.
5. **Descubrir al excavar.** Excavar una celda descubre ella y sus 26 vecinas, y marca sucios los chunks; el cambio no se deshace al construir un muro en esa celda.
6. **Excavar a ciegas.** Una caja de excavar que cruza celdas ocultas las marca; se cumple celda por celda y descubre mientras tanto.
7. **Mineral.** Una celda de veta excavada deja una unidad de mineral de hierro (100 %); tarda 200 ticks a velocidad 100 % (unos 154 con un pico de hierro de calidad normal, +30 %).
8. **La niebla no altera la simulación.** Con y sin niebla las regiones de conectividad y las rutas son iguales (test comparando la huella de la conectividad).
9. **Render.** Las celdas ocultas no se dibujan (capturas del valle y de un túnel recién abierto en es y en); el corte por nivel muestra el tono de niebla y no un hueco.
10. **Migración.** Una partida de `v0.3.5` carga con la niebla calculada y vetas sorteadas con `world.ore`; la misma migración da la misma huella.
11. **Guardado.** El test de continuidad incluye celdas descubiertas y ocultas y una veta a medio excavar.
12. **Presupuesto.** Generar el valle sigue tomando menos de 2 segundos; descubrir al excavar suma menos de 0,02 ms por tick de media.

## Fuera de alcance

Van a `docs/ip/backlog.md`: otros minerales (cobre, estaño, plomo, plata, carbón de piedra, sal), prospección (revelar vetas con un conocimiento), cuevas naturales y aire atrapado, derrumbes y apuntalado (IP5), niebla que se pueda volver a ocultar, minería profunda con peligros, y que las criaturas descubran caminando.
