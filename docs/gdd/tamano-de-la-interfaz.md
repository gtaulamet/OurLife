# Tamaño de la interfaz (V2)

Spec de V2 de IP2.5: una opción para agrandar la interfaz según la resolución. Hasta IP2 la UI se dibuja siempre a su tamaño de diseño (textos del HUD de 16 px y de los menús de 28 px), así que en una pantalla de 1440p o 4K se ve chica. Se adelanta de la accesibilidad de IP8.

## Comportamiento

- **Opciones**, debajo de "Pantalla", suma la fila **Tamaño de la interfaz**: Automático, 100 %, 150 %, 200 %, 250 % y 300 %. Como las otras filas, el botón activo se ve presionado.
- Cambiar el tamaño se aplica en el acto, también sobre la partida, y se guarda en las preferencias. Sin preferencia guardada, el tamaño es Automático.
- Escala toda la interfaz 2D: HUD, paneles, registro, menús y pantallas. El mapa 3D se sigue dibujando a la resolución de la pantalla.
- **Automático** elige el mayor tamaño que deja un lienzo de al menos 1280×720, la resolución de diseño. El botón dice cuál eligió, por ejemplo "Automático (150 %)".

| Ventana | Automático |
|---|---|
| 1280×720 a 1919×1079 | 100 % |
| 1920×1080 | 150 % |
| 2560×1440 | 200 % |
| 3200×1800 | 250 % |
| 3840×2160 | 300 % |

- Un **tamaño fijo** sólo se puede elegir si deja un lienzo de al menos 960×540; los demás botones se ven deshabilitados. El 100 % siempre se puede elegir.
- Si la ventana se achica y el tamaño elegido ya no entra, se usa el mayor que entra, sin cambiar la preferencia: al agrandar la ventana vuelve el elegido. En Opciones se ve presionado el tamaño que se está usando.
- El tamaño se recalcula al cambiar el tamaño de la ventana o al pasar a pantalla completa.
- Los pasos de 50 % llevan la fuente Tiny5 a múltiplos de 8 px, donde se ve nítida. Si en las capturas algún paso se ve borroso, los pasos pasan a ser de 100 %.
- La selección de celdas y de colonos con el ratón no cambia con el tamaño de la interfaz.
- Con el panel Colonia abierto, la lista de alertas muestra sólo los peligros, y el panel sube: con cinco avisos de colono ocioso, en un lienzo de 960×540 el panel tapaba el registro. El panel ya dice qué hace cada colono.
- Si el panel Colonia llega hasta el inspector, como en español en un lienzo de 960×540, el inspector baja debajo del panel si entra arriba de las barras de abajo; si no entra, se queda arriba.

## Línea de comandos

- `--ui-scale auto|100|150|200|250|300` fija el tamaño para las capturas y el smoke, sin guardarlo: aunque después se cambie otra opción, el archivo conserva el tamaño guardado.
- Las capturas a otra resolución usan la opción de Godot `--resolution`, por ejemplo `--resolution 1920x1080`.

## Textos

| Clave | es | en |
|---|---|---|
| `ui.options.ui_scale` | Tamaño de la interfaz | Interface size |
| `ui.options.ui_scale_auto` | Automático ({percent} %) | Auto ({percent}%) |
| `ui.options.ui_scale_percent` | {percent} % | {percent}% |

`percent` es un número entero. En español el signo de porcentaje va separado del número; en inglés, pegado.

## Criterios de aceptación

1. Capturas en es y en a 1280×720, 1920×1080 y 2560×1440, en Automático: menú principal, Opciones y partida con el inspector y el panel Colonia. Sin textos cortados ni superpuestos, y la fuente nítida.
2. A 1920×1080 y 200 % (lienzo de 960×540), la partida sigue siendo usable: la barra de órdenes y el registro entran en la pantalla.
3. El smoke hace clic en un colono con la interfaz al 200 % mandando el clic como un evento de la ventana, y lo selecciona.
4. Tests de la regla: el tamaño automático de cada resolución de la tabla, los tamaños fijos disponibles en 1280×720 y 1920×1080, y el tamaño que se usa cuando el elegido no entra.
5. La preferencia persiste al reiniciar; un valor desconocido en `settings.cfg` vuelve a Automático.
