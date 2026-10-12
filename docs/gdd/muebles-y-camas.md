# Muebles y camas (H2.5)

Spec de H2.5 de IP2: cama, mesa, silla y cofre, colocados como planos con orientación. El cofre es un almacén de una celda. Cada colono puede tener una cama, y dormir en ella descansa más rápido que en el suelo.

## Muebles como construibles

- Un mueble es un `Buildable` con `shape: furniture`. Usa todo lo de H2.4 (`docs/gdd/construccion.md`): plano, entrega de material, construcción, cancelar y desmontar.
- Campos nuevos del `Buildable`, sólo para muebles:

| Campo | Qué es |
|---|---|
| `visual` | Modelo del mueble en el catálogo de visuales (obligatorio en un mueble, prohibido en lo demás) |
| `restRecoveryPercent` | Si está, el mueble es una cama: quien duerme en ella recupera el descanso a ese porcentaje del normal |
| `storage` | El mueble es un cofre: un almacén de una celda |

- Contenido de IP2, todo de madera:

| Mueble | Costo | Trabajo | Extra | Modelo |
|---|---|---|---|---|
| Cama (`bed`) | 4 | 180 | `restRecoveryPercent` 150 | `bedSingle` del Furniture Kit |
| Mesa (`table`) | 3 | 150 | | `table` del Furniture Kit |
| Silla (`chair`) | 2 | 90 | | `chair` del Furniture Kit |
| Cofre (`chest`) | 4 | 150 | `storage` | `chest` del Survival Kit |

- Mesa y silla son decorativas en IP2: comer en la mesa llega con la cocina, más adelante.

## Colocación

- Un mueble ocupa una celda de suelo firme (como un muro, ver H2.4), sin otro mueble, sin plano, sin orden, sin planta y fuera de los almacenes. Un almacén nuevo saltea las celdas con muebles o con planos de muebles.
- No bloquea el paso: se camina sobre él.
- **Orientación:** el plano guarda un giro de 0 a 3 cuartos de vuelta. Con la herramienta Construir, **G** gira lo que se va a colocar; la pista de la barra lo dice. El giro es sólo de presentación: el mueble terminado lo conserva.
- Muros, escaleras, rampas, puertas y canales no se ponen sobre un mueble. Para cambiar la celda, primero se desmonta el mueble.

## Desmontar un mueble

- Desmontar (V) sobre una celda con mueble desmonta el mueble, no la celda: devuelve su parte del costo como cualquier construible. Lo que hubiera en un cofre queda en la celda, suelto.
- Si la celda deja de ser suelo firme, el mueble se pierde y deja la parte que devolvería al desmontarlo.

## El cofre

- Al terminarse, el cofre es un almacén de una celda que acepta todas las categorías, con los mismos filtros en el inspector. Guarda una pila, como una celda de almacén.
- No se dibuja con el tono de los almacenes. Cancelar (X) no lo quita: es un mueble, se desmonta.

## Camas

- Una cama tiene dueño o está libre. Al ir a dormir, un colono sin cama toma la cama libre más cercana a la que puede llegar, que pasa a ser suya. Si no hay, duerme en el suelo, donde está.
- Un colono con cama camina hasta ella para dormir. Si no puede llegar, duerme donde está. Una cama libre pasa a ser suya sólo si encuentra el camino hasta ella.
- Durmiendo sobre su cama, recupera el descanso a `restRecoveryPercent` del normal (150 %: 1800 por hora en lugar de 1200).
- La cama queda libre si su dueño muere o deja de existir; si la cama se desmonta, el colono se queda sin cama.

## Lo que se ve

- Los muebles se dibujan con su modelo, girado y teñido como el kit (sin tinte por material).
- Un colono que duerme se dibuja acostado boca arriba, centrado en su celda: en su cama, a lo largo de ella y sobre el colchón; en el suelo, con el rumbo que traía. Al despertar se vuelve a parar. Es sólo presentación.
- El plano de un mueble es una caja translúcida baja del color del material.
- La barra de Construir muestra los muebles después de las formas del terreno.
- Inspector de una celda con mueble: "Cama de madera", y en una cama "Cama de Elvira" o "Libre". En un cofre, los filtros del almacén.
- Inspector de un colono: "Duerme en su cama" o "Sin cama".

## Comandos y eventos

| Comando | Efecto |
|---|---|
| `PlaceBlueprintCommand(construible, material, desde, hasta, giro)` | El giro nuevo, de 0 a 3; los planos lo guardan |
| `DesignateCommand` con `Deconstruct` | También marca celdas con mueble |

- Los muebles son entidades con `Furniture` y `Position`; la cama suma `Bed` y el cofre `Stockpile`. Avisan con los eventos de entidad.

## Textos nuevos

| Clave | es | en |
|---|---|---|
| `def.buildable.{bed,table,chair,chest}.*` | cama, mesa, silla, cofre | bed, table, chair, chest |
| `ui.orders.rotate_hint` | G gira ({quarter} de 4) | G rotates ({quarter} of 4) |
| `ui.inspector.furniture` | {buildable} de {material} | {material} {buildable} |
| `ui.inspector.bed_owner` | Cama de {name} | {name}'s bed |
| `ui.inspector.bed_free` | Libre | Free |
| `ui.inspector.has_bed` | Duerme en su cama | Sleeps in their bed |
| `ui.inspector.no_bed` | Sin cama | No bed |

## Criterios de aceptación

1. Escenario: dos colonos cansados de noche, uno con cama y otro sin; al cabo de dos horas, el de la cama recuperó más descanso.
2. Escenario: un colono sin cama toma la cama libre al acostarse; un segundo colono no la usa y duerme en el suelo.
3. Escenario: un cofre terminado recibe troncos sueltos como un almacén; desmontarlo deja los troncos en la celda.
4. Escenario: un plano de cama con giro 1 da una cama con giro 1.
5. Un mueble no se pone fuera de suelo firme, ni sobre otro mueble, un almacén o un plano; un muro no se pone sobre un mueble.
6. La cama queda libre al morir su dueño.
7. El test de continuidad del guardado incluye muebles, una cama con dueño y un cofre con algo.
8. En la partida: la casa del smoke amueblada con cama, mesa, silla y cofre, en es y en.
