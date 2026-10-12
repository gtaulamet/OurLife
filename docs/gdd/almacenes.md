# Almacenes y acarreo (H2.3)

Spec de H2.3 de IP2: zonas de almacén con filtros por categoría, y colonos que acarrean los objetos sueltos y los apilan.

## Categorías de objetos

- Un def nuevo, `ItemCategory`, con nombre, descripción y género en cada idioma. No tiene modelo: no se dibuja.
- Cada `Item` declara su `category`. Contenido de IP2:

| Categoría | Objetos |
|---|---|
| `wood` (madera) | tronco |
| `stone` (piedra) | piedra |
| `food` (comida) | bayas, provisiones |

- `kind` (crudo o comida) sigue diciendo si un objeto se come; la categoría sólo ordena el almacén.

## Zonas de almacén

- Una zona es un conjunto de suelos planos en los que se puede estar de pie (no rampas ni escaleras), con una lista de categorías permitidas. Al crearla permite todas.
- Se dibuja como las órdenes: la herramienta **Almacén (Z)** marca la caja arrastrada, en el nivel de la celda inicial. Las celdas que ya son de otra zona, las que no son suelo plano y las bloqueadas se saltean. Las celdas nuevas de una misma caja forman una zona nueva.
- **Cancelar (X)** también quita del almacén las celdas de la caja; una zona sin celdas desaparece.
- Una celda guarda **una sola pila**, de un solo objeto, hasta el límite de pila de ese objeto.
- Una celda que deja de ser pisable (por ejemplo, porque se construye un muro en H2.4) sigue en la zona, pero no recibe objetos mientras no se pueda pisar.
- **Filtros:** al seleccionar una celda de un almacén, el inspector muestra sus categorías, cada una con Sí o No. Cambiar un filtro no mueve lo que ya está guardado: lo que deja de estar permitido pasa a estar suelto y se acarrea a otro almacén, si lo hay.

## Acarreo

- **Objeto suelto:** una pila que no está en una celda de almacén que acepte su categoría. Acarrear un objeto guardado nunca tiene sentido.
- **Trabajo nuevo:** `haul` (acarrear), en un tipo de trabajo nuevo, `hauling` (acarreo), después de recolectar. Un colono acarrea cuando no tiene otro trabajo de más prioridad.
- **Elección:** el colono toma la pila suelta más cercana a la que puede llegar que tenga un destino. El destino es la celda de almacén más cercana a la pila, en la misma región, que acepte la categoría y tenga lugar: vacía, o con una pila del mismo objeto por debajo del límite. Ni la pila ni la celda de destino las puede tomar otro colono a la vez.
- **Viaje:** camina hasta la pila y levanta lo que entra en el destino: toda la pila o lo que falta para llenar la del destino. Camina hasta el destino y lo deja ahí, sumado a la pila que hubiera.
- **Lo que lleva:** un colono que lleva algo y deja el trabajo por cualquier motivo (hambre crítica, la noche, el destino que ya no acepta lo que lleva) lo deja en la celda donde está. Ahí queda suelto y otro, o él mismo, lo vuelve a acarrear. Si muere, lo que llevaba queda en su celda.
- Comer de una pila guardada está permitido: el almacén de comida es la despensa.

## Lo que se ve

- Las celdas de almacén se pintan con un tono propio, distinto del de las órdenes, en el nivel visible.
- Un colono que lleva algo muestra el modelo del objeto en las manos.
- El inspector de un colono dice qué lleva ("Lleva 25 troncos").
- El inspector de una celda de almacén muestra las categorías con Sí o No.

## Comandos y eventos

| Comando | Efecto |
|---|---|
| `DesignateCommand` con `Stockpile` | Crea una zona con las celdas libres y pisables de la caja |
| `DesignateCommand` con `Cancel` | Además de las órdenes, quita del almacén las celdas de la caja |
| `SetStockpileFilterCommand(zona, categoría, permitida)` | Cambia un filtro |

- `StockpilesChangedEvent` avisa a la vista que cambió alguna zona.

## Textos nuevos

| Clave | es | en |
|---|---|---|
| `ui.orders.stockpile` | Almacén (Z) | Stockpile (Z) |
| `ui.orders.name.stockpile` | Almacén | Stockpile |
| `ui.inspector.stockpile` | Almacén | Stockpile |
| `ui.inspector.carrying` | Lleva {count} {item} | Carrying {count} {item} |
| `job.haul.report` | Acarreando | Hauling |
| `def.item_category.*` | madera, piedra, comida | wood, stone, food |
| `def.work_type.hauling.*` | acarreo | hauling |

El i18n-specialist ajusta los textos finales; la tabla fija claves y argumentos.

## Criterios de aceptación

1. Escenario: troncos sueltos y un almacén de madera ⇒ todos los troncos quedan apilados en el almacén, a lo sumo una pila por celda y ninguna por encima del límite.
2. Escenario: un almacén que no acepta comida no recibe bayas; al permitir la comida, las recibe.
3. Escenario: un colono que lleva troncos y tiene hambre crítica los deja donde está, come y después alguien los lleva al almacén.
4. Escenario: dos colonos no toman la misma pila ni la misma celda de destino.
5. Quitar celdas con Cancelar deja sueltas sus pilas; quitar todas borra la zona.
6. El test de continuidad del guardado incluye almacenes, filtros y un colono que lleva algo.
7. En la partida: dibujar un almacén, ver a los colonos llenarlo y cambiar un filtro desde el inspector, en es y en.
8. Smoke y capturas es/en revisadas.
