# Indicadores: existencias y FPS

Pedido del usuario del 16 de octubre de 2026, durante IP3: ver cuánta comida, madera y piedra tiene la aldea, y poder ver los cuadros por segundo. Entra fuera de los hitos de IP3, como la decoración del pasto en IP2.5.

## Existencias

- La barra superior del HUD muestra, después de la fecha, una cuenta por categoría de objetos: "Comida: 64", "Madera: 25", "Piedra: 0".
- Cuenta las unidades de cada categoría que hay en todo el mapa, en un almacén o sueltas, más lo que llevan los colonos en las manos. No cuenta lo que ya se entregó a un plano: eso ya es parte de la obra.
- Las categorías son las del contenido (`ItemCategory`), en el orden de su campo nuevo `order` y después por id. El código no conoce ninguna: una categoría nueva aparece sola. En IP3: comida (0), madera (1) y piedra (2).
- Se actualiza con cada tick, como el reloj.
- Consulta de Core: `ColonyStock.ByCategory(simulation)`, de sólo lectura.

## FPS

- Opciones suma "Mostrar FPS" (Sí / No), guardado en `settings.cfg`. Por defecto, No.
- Con Sí, el HUD muestra "60 FPS" en un panel abajo a la derecha, en la fila del registro. Cuenta los cuadros dibujados en el último medio segundo; antes de la primera cuenta muestra la del motor.
- Para no sumar una fila a Opciones, que a 720p no entraría (riesgo de `IP2.5.md`), "Ciclo de día y noche" y "Mostrar FPS" comparten una fila, uno al lado del otro.
- `--fps` los muestra en una captura sin guardarlo.

## Textos

| Clave | es | en |
|---|---|---|
| `ui.hud.stock` | {category}: {count} | {category}: {count} |
| `ui.hud.fps` | {fps} FPS | {fps} FPS |
| `ui.options.show_fps` | Mostrar FPS | Show FPS |

## Criterios de aceptación

1. La cuenta suma las pilas sueltas, las almacenadas y lo que llevan los colonos, por categoría y en el orden del contenido (test de Core).
2. Con el contenido real, el HUD muestra comida, madera y piedra en ese orden (test de contenido).
3. La barra superior entra completa en un lienzo de 960 × 540, en es y en en; Opciones entra a 720p.
4. El smoke muestra las existencias y prende y apaga los FPS.
