# Playtest de IP2.5 (`v0.2.5`)

Checklist para la prueba humana de V6. Cubre la definición de hecho de IP2.5 (`docs/ip/IP2.5.md`) y los criterios de las specs de V2 a V5. Se juega con el ejecutable exportado, no con el editor. Lo nuevo de esta versión es visual: lo más importante es mirarlo en movimiento y decir si se ve bien.

## Preparación

1. Descomprimir `builds/OurLife-v0.2.5-windows-x86_64.zip` en una carpeta nueva.
2. Perfil limpio: borrar `%APPDATA%\OurLife\settings.cfg`, para ver los valores por defecto (tamaño Automático, decoración Normal). Las partidas guardadas pueden quedar.
3. Los atajos de esta guía se lanzan desde PowerShell en la carpeta del juego, por ejemplo `.\OurLife.exe -- --play --seed 42 --ticks 900`.

| Atajo | Empieza en |
|---|---|
| `--play --seed 42` | La llegada, a las 06:00 |
| `--ticks 900` | El primer día, a las 21:00: la aldea se va a dormir |
| `--ticks 5000 --orders` | El día 4, con la casa del smoke construida y amueblada cerca de la llegada |

## Checklist

Marcar cada punto con ✅ (bien), ❌ (falla, anotar el hallazgo abajo) o ➖ (no se probó).

### Terreno y decoración (V1, V4)

- [✅] El pasto, la tierra, la roca bajo tierra, los muros de madera y los de mampostería tienen textura y se distinguen entre sí; el pasto no tiñe los costados de los bloques, salvo un borde arriba.
- [✅] Matas, flores y piedritas sobre el pasto; nada dentro de los arbustos, bajo los muebles ni metido en los muros.
- [✅] Opciones > Decoración del pasto: Nada, Poca, Normal y Mucha cambian el pasto en el acto, también desde la pausa de una partida; subir el nivel agrega sin cambiar lo que había. Persiste al reiniciar.
- [✅] Bajando de nivel (AvPág), el corte muestra los estratos de roca sin decoración.

### Tamaño de la interfaz (V2)

- [✅] Sin preferencia guardada, Opciones dice "Automático" con el tamaño que eligió para la pantalla, y los textos se leen bien.
- [✅] Probar 100 %, 150 % y 200 %: se aplican en el acto; los que no entran en la ventana se ven deshabilitados. Persiste al reiniciar.
- [✅] Con la interfaz al 200 %, un clic sobre un colono lo selecciona y un clic sobre el terreno elige la celda bajo el cursor.
- [✅] Achicar y agrandar la ventana, y pasar a pantalla completa: el tamaño automático se recalcula.
- [✅] Con el panel Colonia abierto en una pantalla chica, no tapa el registro ni el inspector.

### Vista previa de muebles (V3)

- [✅] Construir (C) > Cama: dentro del cubo del cursor se ve la cama translúcida, con una flecha amarilla hacia los pies. G la gira en el acto.
- [✅] Silla, mesa y baúl: la flecha marca hacia dónde mira la silla y el frente del baúl (el lado de la cerradura).
- [✅] Sobre un muro, otro mueble, un almacén o un arbusto, la vista previa se ve roja.
- [✅] Arrastrar con un mueble elegido: un mueble en cada celda donde va, rojo donde no.
- [✅] El plano colocado se ve con el modelo y su giro, y el mueble terminado mira igual que la vista previa.

### Modelos y animaciones (V5)

- [✅] Los aldeanos son distintos entre sí, con ropa en tonos de época, y cada uno conserva su aspecto al guardar y cargar.
- [✅] Caminan con animación; al cavar o talar golpean, al construir manipulan, al recolectar levantan, con carga la sostienen, y quietos respiran.
- [✅] Las animaciones se aceleran a 2× y 4× y se detienen en pausa y con el menú de pausa abierto.
- [✅] De noche duermen acostados a lo largo de la cama.
- [✅] La cama, la mesa, la silla y el baúl se ven medievales, con la madera a tono con los muros; el baúl recibe objetos como antes.
- [✅] Los arbustos con bayas muestran racimos rojos; recolectados, se ven sin bayas. Las bayas sueltas son cerezas y las provisiones, un barril.

### Lo de antes sigue igual

- [✅] Guardar a mitad de partida, salir, cargar y seguir: todo continúa igual.
- [✅] Día y noche, almacenes, construcción y panel Colonia como en v0.2.0.
- [✅] En español y en inglés, ningún texto cortado, superpuesto ni como clave cruda, también en Opciones.

### macOS (en una Mac con Apple Silicon)

- [✅] Descomprimir `OurLife-v0.2.5-macos-universal.zip`, volver a firmar con los pasos del README (`codesign --force --deep --sign - OurLife.app` y `xattr -cr OurLife.app`) y abrir la app, sin `chmod`: con el zip del 15 de octubre `OurLife.app/Contents/MacOS/OurLife` ya es ejecutable (hallazgo 1).
- [✅] Zoom con el trackpad: pellizcar acerca y aleja, y deslizar dos dedos arriba y abajo también. Mantener + acerca y mantener - aleja (hallazgo 2). Anotar si la velocidad de cada uno resulta cómoda.
- [✅] Smoke en la terminal: `./OurLife.app/Contents/MacOS/OurLife --headless -- --smoke --seed 42 --ticks 5000` termina con `[smoke] OK` y la huella `974758E0263469A3`, la misma que en Windows.
- [✅] El terreno, la decoración y los aldeanos se ven como en Windows, y se juega sin tirones con la decoración en Normal. Si no, probar con Poca o Nada y anotar.

### Rendimiento

- [✅] `.\OurLife.exe -- --benchmark 20 --seed 42 --ticks 5000` termina con "within the budget" en `%APPDATA%\OurLife\logs\godot.log`.
- [✅] Jugando a 4× con la aldea construida y la decoración en Mucha no hay tirones visibles.

## Pasada automática

Lo que no necesita manos, verificado por Claude Code antes de la prueba humana.

| Punto | Cómo se verificó | Resultado |
|---|---|---|
| La simulación no cambió | Smoke con las semillas 1 y 42: huellas `889B6049962A78AD` y `974758E0263469A3`, las de v0.2.0 | ✅ |
| Tests | 509 en verde: Core, localización y contenido (catálogo de visuales con patrones, decoración, variantes, partes y giro) | ✅ |
| Clic con la interfaz al 200 % | El smoke manda un clic a la ventana en el píxel de un colono y lo selecciona; comprobado que falla si el clic cae en otro lugar | ✅ |
| Vista previa | Test de Core: `CellsToPlan` da las mismas celdas que el comando; el smoke muestra la vista previa de una cama | ✅ |
| Decoración en vivo | El smoke pasa por los cuatro niveles de decoración sobre la partida | ✅ |
| Paquete de Windows | `./tools/package-windows.ps1`: exporta, corre el smoke sobre el ejecutable exportado y arma `OurLife-v0.2.5-windows-x86_64.zip` (72,7 MB) | ✅ |
| Paquete de macOS | `./tools/package-macos.ps1`: app universal con firma ad hoc (121,8 MB). Hay que volver a firmarla en la Mac. Rearmado el 15 de octubre por los hallazgos 1 y 2: las 387 entradas del zip quedan marcadas como Unix, con el ejecutable y los 28 archivos nativos de .NET en `rwxr-xr-x` | ✅ (probado en la Mac por el usuario) |
| Rendimiento | `--benchmark 20 --seed 42 --ticks 5000` a 1280×720 con decoración Normal: 261 FPS de media, 210 en el 1 % más lento, 0,046 ms por tick, dentro del presupuesto (RTX 4090 de portátil, que varió entre corridas). Decoración Nada contra Mucha: unos 325 contra 300 FPS | ✅ |
| Capturas | Menú y partida en es, Opciones y construcción en en, a 1920×1080 (Automático 150 %); antes y después de cada hito en https://claude.ai/artifact/JkWuPmFMuosFSxV8TgAGiD | ✅ |

## Hallazgos

| # | Qué pasó | Pasos para reproducirlo | Gravedad (bloquea `v0.2.5` o no) |
|---|---|---|---|
| 1 | En la Mac, la app no abría: `OurLife.app/Contents/MacOS/OurLife` no tenía permiso de ejecución. Se arreglaba con `chmod +x`. Causa: al agregar `LICENSES.txt` y `CREDITS.md`, el `ZipArchive` de .NET marca todas las entradas como hechas en MS-DOS, y macOS ignora sus permisos Unix. **Corregido:** `package-macos.ps1` las vuelve a marcar como Unix y falla si el ejecutable no tiene permiso de ejecución | Descomprimir el zip anterior al 15 de octubre en una Mac y abrir la app | Bloqueaba |
| 2 | En la Mac, el zoom con el trackpad no andaba: el trackpad no manda rueda, sino gestos. **Corregido:** pellizcar y deslizar dos dedos hacen zoom, y también + y - (mantener). El smoke lo comprueba | En una Mac, pellizcar o deslizar dos dedos sobre el valle | Bloqueaba |

## Resultado

- Fecha y PC: 15 de octubre de 2026, la PC con Windows y la Mac con Apple Silicon. La Mac se probó dos veces: con el zip anterior a las correcciones (hallazgos 1 y 2) y con el de `v0.2.5`.
- Quién jugó: el usuario (gtaulamet).
- ¿Se puede publicar `v0.2.5`? Sí, publicada el 15 de octubre. Windows aprobado y, con el zip de `v0.2.5`, el usuario dio por completada la sección de macOS.
