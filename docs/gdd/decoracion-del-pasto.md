# Decoración del pasto (V4 de IP2.5)

Spec de la decoración del pasto y de su opción. Pedido del usuario del 14 de octubre de 2026, después de cerrar V4.

## Decoración

- Matas de pasto, flores amarillas y violetas, piedritas y algún hongo sobre las celdas de suelo con pasto. Los modelos y su probabilidad están en el catálogo de visuales (`decoration`, ver `docs/art.md`).
- Es sólo visual: no son entidades, no se guardan y no cambian la simulación ni las huellas. Un hash de la celda elige la pieza, así que una celda siempre se ve igual.
- No hay decoración bajo un mueble ni dentro de una planta, y una pieza no se corre hacia una celda vecina que no pueda decorarse.

## Opción

- **Opciones** suma la fila **Decoración del pasto**: Nada, Poca, Normal y Mucha. Por defecto, Normal.
- Multiplica la probabilidad de cada pieza del catálogo: Nada por 0, Poca por 0,4, Normal por 1 y Mucha por 1,8. Una celda con decoración en Poca la conserva en Normal y en Mucha: más decoración agrega piezas sin cambiar las que había.
- Se aplica en el acto, también sobre la partida, y se guarda en las preferencias. Al cambiarla, el terreno se arma de nuevo una vez (una fracción de segundo).
- Sirve para el gusto del jugador y para placas de video más lentas: menos decoración dibuja menos triángulos.
- `--decoration none|low|normal|high` fija el nivel para capturas y benchmarks, sin guardarlo.

## Textos

| Clave | es | en |
|---|---|---|
| `ui.options.decoration` | Decoración del pasto | Grass decoration |
| `ui.options.decoration_none` | Nada | None |
| `ui.options.decoration_low` | Poca | Low |
| `ui.options.decoration_normal` | Normal | Normal |
| `ui.options.decoration_high` | Mucha | High |

## Medición

Benchmark de 20 s de la aldea a 1280×720 (RTX 4090 de portátil), alternando dos veces: Nada da 327 y 324 FPS de media, y Mucha 303 y 300. Mucha cuesta unos 0,25 ms por cuadro en esa placa.
