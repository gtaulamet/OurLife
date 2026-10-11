# Playtest de IP1 (`v0.1.0`)

Checklist para la prueba humana de H1.8. Cubre la definición de hecho de IP1 (`docs/ip/IP1.md`) y los criterios de aceptación de las specs de H1.4, H1.6 y H1.7. Se juega con el ejecutable exportado, no con el editor.

## Preparación

1. Armar el paquete con `./tools/package-windows.ps1` y descomprimir `builds/OurLife-v0.1.0-windows-x86_64.zip` en una carpeta nueva.
2. Perfil limpio: borrar la carpeta `%APPDATA%\OurLife` si existe. Ahí quedan las preferencias (`settings.cfg`) y el registro (`logs\godot.log`).
3. Los atajos de esta guía se lanzan desde PowerShell en la carpeta del juego, por ejemplo `.\OurLife.exe -- --play --seed 42 --ticks 43200`. Sin argumentos, el juego abre el menú principal. Con `--ticks`, la partida empieza con ese tiempo ya corrido; un día son 1440 ticks.

| Atajo | Empieza en |
|---|---|
| `--ticks 43200` | El primer día del otoño, a las 06:00 |
| `--ticks 64800` | El primer día del invierno, a las 06:00 |
| `--ticks 74880` | El día 8 del invierno, con la colonia sin reservas |

A 4×, un día de juego dura unos 36 segundos y una estación unos 9 minutos.

## Checklist

Marcar cada punto con ✅ (bien), ❌ (falla, anotar el hallazgo abajo) o ➖ (no se probó).

### Arranque e idioma

- [✅] Con el perfil limpio, el juego abre el menú principal en español y todo texto visible está en español.
- [✅] "Cargar partida" se ve deshabilitado y avisa que llega más adelante.
- [✅] En Opciones, elegir English cambia todos los textos en el acto, también el registro de una partida en curso (abrir Opciones desde la pausa).
- [✅] Cerrar y volver a abrir el juego: sigue en inglés. Volver a español.
- [✅] Pantalla completa: se activa y se desactiva desde Opciones, y la elección persiste al reiniciar.
- [✅] En cada fila de Opciones se nota cuál es la opción activa.

### Nueva partida

- [✅] Nueva partida trae un nombre de aldea y una semilla; "Al azar" cambia la semilla.
- [✅] El nombre elegido aparece en la barra superior.
- [✅] Misma semilla, mismo valle: empezar dos partidas con la semilla 42 y comparar el relieve y los árboles cerca de la llegada.
- [✅] Llegan 5 colonos y el registro dice "Llegan 5 colonos al valle."

### Cámara y niveles

- [✅] WASD o las flechas mueven la cámara; la rueda acerca y aleja; Q y E rotan 90°.
- [✅] RePág y AvPág cambian el nivel; el HUD lo muestra y la vista corta el mapa en ese nivel, con los niveles de abajo más oscuros.
- [✅] Bajo tierra se distinguen los estratos de piedra.

### Tiempo

- [✅] El reloj avanza; Espacio y el botón Pausa lo detienen y lo reanudan.
- [✅] 1, 2 y 3 (o los botones) cambian a 1×, 2× y 4×; el botón activo se resalta.
- [✅] Con el menú de pausa (Esc) abierto, el tiempo no corre.

### Órdenes

- [✅] Cada orden se elige con su tecla (M, B, T, R, X) o su botón; el botón se resalta y aparece una línea de ayuda.
- [✅] Excavar: arrastrar sobre un muro de una colina marca sólo ese nivel; los colonos lo excavan y dejan suelo.
- [✅] Escalera: marcar una celda de suelo; los colonos cavan la escalera hacia abajo. Con AvPág durante el arrastre se abarcan varios niveles.
- [✅] Talar: arrastrar sobre un bosque en una ladera marca todos los árboles visibles del rectángulo, a cualquier altura; quedan troncos en el suelo.
- [✅] Recolectar: los arbustos marcados se recolectan y quedan bayas en el suelo; el arbusto cambia de modelo.
- [✅] Cancelar quita las marcas del rectángulo.
- [✅] Soltar el arrastre fuera del mapa o sobre la barra marca la caja que se veía.
- [✅] Esc o el clic derecho salen de la orden.

### Selección e inspector

- [✅] Un clic en un colono (en el cuerpo, no sólo en los pies) lo selecciona: nombre, tarea y necesidades en porcentaje, que cambian con el tiempo.
- [✅] Un clic en una celda muestra coordenadas, forma y material, órdenes, planta ("sin frutos" si está recolectada) y objetos.
- [✅] Un clic en el cielo, Esc o el clic derecho quitan la selección.
- [✅] El marco bajo el ratón sigue al terreno al mover la cámara y no aparece sobre la barra.

### Colonos

- [✅] Los colonos caminan sin saltos entre celdas, suben y bajan rampas y escaleras.
- [✅] Comen cuando tienen hambre (primero las provisiones) y duermen cuando están cansados.
- [✅] Dos colonos no trabajan sobre el mismo objetivo.

### Invierno: la amenaza de IP1

- [✅] Con `--ticks 43200`, recolectar en otoño los arbustos alrededor de la aldea (un rectángulo de unas 50 × 50 celdas) y llevar la partida a 4× hasta la primavera: los 5 colonos sobreviven.
- [✅] Con `--ticks 74880`, sin hacer nada a 4×: las muertes aparecen en el registro ("… murió de hambre.") y, al morir el último, aparece "La colonia ha caído" con el botón Menú principal.
- [✅] En invierno, los arbustos recolectados no vuelven a dar fruto hasta la primavera.

### Textos

- [✅] En español y en inglés, ningún texto aparece cortado, superpuesto ni como clave cruda (por ejemplo `ui.algo` o `⟦nombre⟧`).
- [✅] Español neutro, sin voseo; plurales y géneros bien ("1 colono", "5 colonos").

### Rendimiento

- [✅] `.\OurLife.exe -- --benchmark 20` termina con "within the budget" en `%APPDATA%\OurLife\logs\godot.log`: al menos 60 FPS de media y menos de 2 ms por tick.
- [✅] Jugando a 4× no hay tirones visibles.

## Pasada automática (2026-10-11)

Antes del playtest humano, Claude Code verificó con el ejecutable exportado lo que no necesita manos, con capturas, el smoke, el benchmark y los tests. Lo que figura acá ya está probado; el resto del checklist queda para la prueba humana.

| Punto del checklist | Cómo se verificó | Resultado |
|---|---|---|
| Arranca en español, "Cargar partida" deshabilitado | Captura del menú sin `--locale` | ✅ |
| Textos en es y en, sin claves crudas ni cortes | Capturas del menú, las opciones, la partida, el inspector, el registro y el fin, en los dos idiomas; el smoke falla si falta un texto | ✅ |
| Cambio de idioma en vivo | El smoke cambia de idioma y compara lo que muestra Godot | ✅ (falta verlo a mano, con el registro de una partida) |
| Misma semilla, mismo valle | Tests de determinismo y de generación; el exportado da la misma huella que el editor | ✅ |
| Llegan 5 colonos y el registro lo dice | Capturas de la partida | ✅ |
| Corte por nivel y estratos | Captura con `--level 10` | ✅ |
| Velocidad activa resaltada | Capturas del HUD | ✅ |
| Excavar, escalera, talar y recolectar se cumplen | El smoke da las cuatro órdenes y comprueba que no quedan pendientes y que la escalera se cavó | ✅ |
| Inspector de colono | Captura con `--inspect` | ✅ |
| Clic en el cuerpo de un colono | El smoke hace clic en el pecho de un colono con la cámara real | ✅ |
| Invierno: recolectar en otoño salva a la colonia, no hacerlo la mata | Tests del contenido real, semillas 1 y 42 | ✅ |
| Muertes en el registro y "La colonia ha caído" | Captura con `--ticks 86400` en es y en: cinco muertes, el mensaje final y la pantalla de fin | ✅ |
| Rendimiento | `--benchmark 20` en el exportado: 431 FPS de media, 330 en el 1 % más lento, 0,039 ms por tick (RTX 4090 de portátil) | ✅ |

**Queda para la prueba humana:** persistencia del idioma y de la pantalla completa al reiniciar, pantalla de Nueva partida (nombre, semilla y "Al azar"), cámara (movimiento, zoom, rotación), niveles con RePág y AvPág, pausa y velocidades con teclado y ratón, arrastre de cada orden (incluidos Cancelar, la ladera y soltar fuera del mapa), inspector de celda, selección con clic y Esc, la sensación de los colonos al caminar, y jugar un otoño y un invierno a mano.

## Hallazgos

| # | Qué pasó | Pasos para reproducirlo | Gravedad (bloquea `v0.1.0` o no) |
|---|---|---|---|
| 1 | El registro muestra la hora de cada mensaje pero no el día: en el fin de partida, cinco muertes de días distintos aparecen como "00:02", "00:33", "13:09"… sin saber cuándo pasó cada una. Cumple la spec, que pide la hora | `--play --seed 42 --ticks 86400` | No bloquea. Propuesta: mostrar el día cuando cambia, para IP2 |
| 2 | | | |

## Resultado

- Fecha y PC: 11/10/2026, Rog strix G18
- Quién jugó: Gustavo
- ¿Se puede publicar `v0.1.0`? Sí.
