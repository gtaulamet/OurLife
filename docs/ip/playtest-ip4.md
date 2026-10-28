# Playtest de IP4 (`v0.4.0`)

Checklist para la prueba humana de O11. Cubre la definición de hecho de IP4 (`docs/ip/IP4.md`) y los criterios de aceptación de `docs/gdd/oficios.md`, `conocimientos.md`, `agricultura.md`, `habitaciones.md` y `subsuelo.md`. Se juega con el ejecutable exportado, no con el editor. Lo nuevo: calidad, talleres y recetas, órdenes de producción, conocimientos, campos y comida que se pudre, vetas de hierro y niebla, fundición y herramientas, habitaciones y comedor, y los paneles nuevos.

Marcar cada punto con [✅] (bien), [❌] (falla, anotar el hallazgo abajo) o [➖] (no se probó). Cada punto se prueba en español y, los marcados con (en), también en inglés (Opciones, Idioma).

## Preparación

1. Descomprimir el zip de `v0.4.0` en una carpeta nueva.
2. Atajos de línea de comandos:

| Atajo | Empieza en |
|---|---|
| `--play --seed 1` | La llegada, ritmo Rápido |
| `--saves docs/ip/playtest-o3 --load taller --play` | Un banco de carpintero ya construido y una orden de aserrar 5 tablones (semilla 1) |
| `--saves docs/ip/playtest-o6 --load campo --play` | Agricultura y cereales aprendidos, un campo con cultivos a medias y bayas al 35 % de frescura (semilla 1) |

3. Atajos del teclado: Colonia (P), Producción (O), Conocimientos (K), Habitaciones (U), Inventario (I), ficha del colono (F), capa de habitaciones (L), construir (C), zona de cultivo (J), cosechar a mano (N), marcar para cosechar plantas (R), minar (M), talar (T), almacén (Z), cancelar (X). Esc cierra la ficha o el panel antes de abrir el menú de pausa. Sólo hay un panel abierto a la vez.
4. Tiempos de referencia en Rápido: un año son 60 días; la investigación completa de la cadena del hierro lleva unos 80 días con un scriptorium.

## O2: calidad y habilidades de oficio

- [ ] El panel Colonia (P) tiene trece columnas de trabajo y se desplaza a los lados si no caben; las filas de colonos no se cortan (en).
- [ ] Los colonos empiezan con 16 tablones (Inventario, I, categoría de materiales).
- [ ] Un objeto con calidad distinta de normal muestra su calidad en el inventario y en el inspector de la pila (ej. una herramienta "buena").
- [ ] Un colono que trabaja un oficio sube su nivel en la ficha (F) con el tiempo.

## O3: talleres y recetas

- [ ] Con el banco de carpintero (cargar `taller`), el inspector del taller muestra las recetas "Tablón" y "Mango", con sus ingredientes y su estado (en).
- [ ] Criterio: crear "Hacer 4 tablones" desde el inspector; un colono trae 2 troncos, los aserra y los tablones aparecen guardados en el almacén. El inspector del colono dice que está fabricando.
- [ ] Un solo colono usa el taller a la vez; el otro toma otra tarea.
- [ ] Una cama, una mesa y una silla piden tablones; sin tablones el plano queda esperando materiales.
- [ ] Criterio 15, parte 1 (taller en uso): ver a un colono trabajando en un taller con la animación de trabajo, y la captura del inspector del taller con una orden en curso.

## O4: órdenes de producción

- [ ] Panel Producción (O): crear una orden con receta, modo y número; aparece una fila con su estado (en).
- [ ] "Mantener 6" hace tandas hasta tener 6 y se detiene (estado "satisfecha"); al gastar alguno, vuelve a hacer.
- [ ] "Repetir" no se detiene; Pausar y Reanudar funcionan; Subir y Bajar cambian el orden de ejecución; Quitar la elimina.
- [ ] Una receta sin su conocimiento aparece como bloqueada y dice cuál falta.

## O5: conocimientos y scriptorium

- [ ] Panel Conocimientos (K): árbol de 12 conocimientos por ramas, con costo y estado (en). La barra de construcción atenúa lo bloqueado.
- [ ] Construir un scriptorium y poner un conocimiento en la cola; un colono investiga y la línea del HUD muestra el avance; al terminar aparece el aviso y se desbloquea el taller o la receta que corresponde.
- [ ] Sin cola ni scriptorium, trabajar el oficio relacionado (ej. minar para "Cantería") suma práctica hasta el 60 %, y no más.
- [ ] No se puede encolar un conocimiento bloqueado ni más de cinco.

## O6: agricultura y deterioro

- [ ] Con `campo`: dibujar una zona de cultivo (J) de trigo; las celdas libres se marcan y se siembra (en).
- [ ] Los cultivos se ven crecer por etapas; maduran en unos 10 días y se cosechan solos; la celda se vuelve a sembrar.
- [ ] En el invierno nada crece ni muere; en la primavera vuelve a crecer.
- [ ] Con la cocina y las órdenes de harina y pan, aparecen panes; un colono con hambre baja come primero el plato preparado más fresco.
- [ ] Deterioro: la pila de bayas de `campo` desaparece en pocos días con un aviso de comida podrida; bajo techo dura el doble. El inventario muestra la frescura en amarillo y rojo.
- [ ] Cosecha manual (N): marcar cultivos maduros a mano y ver que se cosechan antes que el resto.
- [ ] La aldea con un campo puede tener hijos aun con pocos arbustos (la comida guardada cuenta para concebir).

## O7: vetas y niebla

- [ ] Al empezar, lo que está bajo tierra se ve como un tono oscuro uniforme en el corte por nivel; no se ven celdas ocultas desde arriba.
- [ ] Hay una veta de hierro a la vista cerca del valle (motas oscuras); el inspector dice "Veta de hierro" y, sobre roca no explorada, "Sin explorar" (en).
- [ ] Minar una veta da 1 mineral de hierro; al cavar se descubren las celdas vecinas.
- [ ] Probar en las semillas 1, 7 y 42.

## O8: fundición, herrería y herramientas

- [ ] Con el conocimiento correspondiente, construir carbonera, horno de fundición y fragua (modelos propios) (en).
- [ ] Cadena completa: troncos a carbón, 2 minerales + 2 carbón a un lingote, lingote + mango a un hacha, un pico, una azada o un martillo.
- [ ] Criterio 15, parte 2 (herramienta forjada): ver el hacha forjada con su calidad en el inventario; un colono sin hacha la busca, la equipa y la ficha (F) la muestra; tala más rápido que sin ella.
- [ ] Al morir un colono, su herramienta queda en su celda.

## O9: habitaciones, dormitorios y comedor

- [ ] Construir una casa de 5 × 5 con una puerta, una cama, una mesa y una silla: la capa de habitaciones (L) la pinta como habitación con su nivel; el inspector de la celda dice "Dormitorio decente: N puntos" (en).
- [ ] Un hueco en un muro la vuelve exterior en el siguiente tick; cerrarlo la recupera.
- [ ] Lista de habitaciones (U): asignar una cama a un colono y "Asignar familia" reparte las camas de una pareja y su hijo; la cuna es para el bebé.
- [ ] El colono despierta en su cama con "Dormí en mi cama" y el pensamiento del nivel (ficha, pensamientos).
- [ ] Comedor: un cuarto con mesa y silla; un colono con hambre baja come sentado ("Comí a la mesa"); sin comedor nadie recibe castigo.
- [ ] Con libre albedrío, entre las 18 y las 22 hay colonos sentados en el comedor charlando.

## O10: paneles y cosecha manual

- [ ] Los cinco paneles (P, O, K, U, I) se abren de a uno: abrir uno cierra el otro y la ficha; Esc cierra la ficha o el panel antes del menú de pausa.
- [ ] Los botones de la barra se ven presionados mientras su panel está abierto, y cada uno muestra su atajo (en).
- [ ] Inventario (I): bloques por categoría con total y filas por objeto con cantidad, calidad y frescura.
- [ ] La barra superior son dos filas: villa, fecha y existencias arriba; paneles, tiempo y nivel abajo. La fecha no se corta.

### Controles por tamaño de interfaz

Repetir, en Opciones, con el tamaño de interfaz en 100 %, 150 % y 200 % (y Automático), en español y en inglés, y con la ventana en 1280 × 720 y en 1920 × 1080:

- [ ] La barra superior, el registro de eventos y la barra de construcción no se tapan entre sí ni se salen de la pantalla.
- [ ] Cada panel (Colonia, Producción, Conocimientos, Habitaciones, Inventario) cabe sobre el registro y se desplaza si el contenido es más alto o ancho; ningún texto queda cortado.
- [ ] La ficha del colono no tapa la barra superior ni el inspector; el inspector no tapa el registro.
- [ ] Sin claves crudas con forma `⟦ui.algo⟧` en ninguna pantalla.

## Rendimiento y estabilidad

- [ ] Una partida de varios años en Rápido (unos 20 minutos) con campo, casas y talleres corre fluida (60 FPS o más) y sin cortes al abrir los paneles.
- [ ] Guardar, salir y cargar: la partida sigue igual (campos, órdenes, camas asignadas, cola de investigación).
- [ ] Cargar una partida de `v0.3.5`: aparece con las vetas, las habilidades nuevas, prioridades en 3 y las bayas frescas.

## Balance (observaciones del autor, para contrastar)

Detalle en `docs/ip/bitacora.md`, entrada de O11. Anotar si se siente distinto:

- [ ] Comida: la aldea nunca pasa hambre con un campo y las bayas ordenadas.
- [ ] Hijos: nacen a un ritmo razonable (con 40 % por año, uno de cada tres deseos de un hijo se cumple; se decidió dejarlo).
- [ ] Hierro: las cuatro herramientas se fabrican en el primer par de años.
- [ ] Camas: nadie duerme en el suelo una vez construidas las casas.

## Hallazgos

(Anotar aquí cada ❌: qué se hizo, qué se esperaba, qué pasó, semilla y captura si hay.)

## Cierre

Con todos los puntos en ✅ o ➖ justificado, el usuario decide el empaquetado y la publicación de `v0.4.0` (`tools/package-windows.ps1`, `tools/package-macos.ps1`, `gh release create`).
