# Playtest de IP3 (`v0.3.0`)

Checklist para la prueba humana de H3.10. Cubre la definición de hecho de IP3 (`docs/ip/IP3.md`) y los criterios de aceptación de `docs/gdd/vida-y-generaciones.md`. Se juega con el ejecutable exportado, no con el editor. Lo nuevo es la vida de los colonos: lo más importante es jugar un rato largo y decir si se entiende lo que les pasa y si da ganas de seguir.

## Preparación

1. Descomprimir `builds/OurLife-v0.3.0-windows-x86_64.zip` en una carpeta nueva.
2. Las partidas preparadas están en `docs/ip/playtest-ip3/`. Copiarlas a `%APPDATA%\OurLife\saves\` para verlas en Cargar partida:
   - `etapas.olsave`: los cinco colonos en fila, de bebé a anciano, junto a la llegada. Están quietos a propósito, para mirarlos.
   - `diez-anos.olsave`: una aldea después de diez años del juego en Rápido, con la orden de recolectar repetida.
3. Los atajos se lanzan desde PowerShell en la carpeta del juego, por ejemplo `.\OurLife.exe -- --play --seed 42 --ticks 900`.

| Atajo | Empieza en |
|---|---|
| `--play --seed 42` | La llegada, a las 06:00, con el ritmo Rápido |
| `--load etapas` | Las cinco etapas en fila (con las partidas copiadas) |
| `--load diez-anos` | La aldea de diez años |

Para ver nacimientos sin esperar horas: Nueva partida con ritmo **Muy rápido**, recolectar en otoño y jugar a 4×. Una pareja concibe sólo si la aldea tiene comida guardada para 30 días por boca, el mapa tiene 9 arbustos de comida por boca y ninguno de los dos está bajo el 20 % de hambre. Las bocas son los colonos, los hijos en camino y el que se concebiría. El valle tiene unos 100 arbustos, así que alcanza para unas 11 personas: que no nazca nadie más allá de ese número no es un error.

**Cambios del 19 de octubre, después de la primera pasada:** llegan 7 colonos con 3 parejas, el valle tiene unos 100 arbustos y `diez-anos` se regeneró. Los puntos marcados como **(repetir)** dependen de eso.

## Checklist

Marcar cada punto con ✅ (bien), ❌ (falla, anotar el hallazgo abajo) o ➖ (no se probó).

### Nueva partida y llegada

- [✅] Nueva partida ofrece el ritmo de la vida (Normal, Rápido, Muy rápido) con su descripción; Rápido viene elegido.
- [✅] **(repetir)** Llegan siete colonos con nombre y apellido, de edades distintas, al menos una mujer y un varón; en la ficha, tres parejas.

### Ficha del colono (F)

- [✅] F o el botón del inspector abren la ficha del seleccionado; sigue a la selección y se cierra con F, Esc o Cerrar.
- [✅] Se leen la etapa y la edad, el ánimo, la personalidad en palabras ("Algo áspero"), las habilidades con su avance, la familia, los pensamientos, los deseos, las relaciones y las últimas interacciones.
- [✅] En un colono en pareja, el botón "Hijos: permitidos / no" cambia al apretarlo, también con el tiempo corriendo; vale para los dos de la pareja.
- [✅] El panel Colonia (P) muestra Edad, Ánimo y la columna Crianza en las prioridades.

### Vida social y ánimo

- [✅] Sobre los colonos que interactúan aparece un globo con un ícono (charlar, bromear, enseñar, discutir, consolar, cortejar, jugar) que se desvanece.
- [✅] Con los días se forman amistades o rivalidades, y el registro lo avisa una vez.
- [✅] El ánimo sube y baja con lo que viven (comer, dormir en cama o en el suelo, charlar); con ánimo muy bajo puede haber una crisis, avisada en amarillo.
- [✅] Los deseos aparecen en la ficha con los días que quedan.

### Parejas, nacimientos y crianza

- [✅] El registro avisa el embarazo y el nacimiento ("dio a luz a una niña: …"); el inspector y la ficha muestran "Embarazada (N %)".
- [✅] El bebé lleva el apellido del padre, aparece en la familia de los dos y crece: bebé, niño, adolescente, adulto, con un aviso en cada etapa.
- [✅] Cuando un bebé tiene hambre, la madre o el padre (o, si no están libres, otro colono) le lleva comida; el bebé no come solo y gatea cerca de su madre.
- [✅] Una cuna construida (Construir > Cuna) la toma un bebé; la cama, los demás.
- [✅] Los niños no trabajan: juegan con otros niños o con su familia. Los adolescentes hacen trabajos livianos.
- [✅] Con "Hijos: no", esa pareja no tiene hijos.

### Arte de las etapas (cargar `etapas`)

- [✅] Las cinco etapas se distinguen: bebé muy chico, niño chico con la cabeza grande, adolescente algo más bajo, adulto y anciano con canas.
- [✅] El bebé se sienta cuando está quieto; los ancianos caminan más despacio; dormidos, todos se acuestan sobre su cama o cuna.

### Vejez y partida larga (cargar `diez-anos`)

- [✅] Los ancianos mueren de viejos con un aviso, y la familia lo nota (pensamientos de duelo en la ficha).
- [✅] **(repetir)** La aldea sigue viva, con comida en el HUD; jugar un año más a 4× sin que muera de hambre si se sigue recolectando.
- [✅] Anotar si los avisos de "tiene mucha hambre" molestan (en una partida larga hay unos 8 por mes).

### Lo de antes sigue igual

- [✅] Una partida de `v0.2.5` carga, con los colonos con edad, apellido, personalidad, habilidades y opiniones.
- [✅] Guardar a mitad de partida, salir, cargar y seguir: todo continúa igual, también un embarazo en curso.
- [✅] Construcción, almacenes, día y noche y opciones como en `v0.2.5`.
- [✅] En español y en inglés, ningún texto cortado, superpuesto ni como clave cruda, también en la ficha.

### macOS (en una Mac con Apple Silicon)

- [✅] Descomprimir `OurLife-v0.3.0-macos-universal.zip`, volver a firmar con los pasos del README (`codesign --force --deep --sign - OurLife.app` y `xattr -cr OurLife.app`) y abrir la app.
- [✅] **(repetir)** Smoke en la terminal: `./OurLife.app/Contents/MacOS/OurLife --headless -- --smoke --seed 42 --ticks 5000` termina con `[smoke] OK` y la huella `1284D40F4588F8FE`, la misma que en Windows.
- [✅] Las etapas, los globos y la ficha se ven como en Windows.

### Rendimiento

- [✅] `.\OurLife.exe -- --benchmark 20 --seed 42 --ticks 5000` termina con "within the budget" en `%APPDATA%\OurLife\logs\godot.log`.
- [✅] Jugando a 4× con la partida de diez años no hay tirones visibles.

## Pasada automática

Lo que no necesita manos, verificado por Claude Code antes de la prueba humana.

| Punto | Cómo se verificó | Resultado |
|---|---|---|
| Tests | 705 en verde: Core, localización y contenido, con el balance de diez años (`BalanceTests`) y las partidas viejas de los formatos 1 a 6 | ✅ |
| Smoke | Código 0; abre la ficha, prueba el botón de hijos y revisa que no falten textos. Huellas `2E368CE2376689E4` (semilla 1) y `1284D40F4588F8FE` (semilla 42) | ✅ |
| Balance de población y comida | Siete fundadores, quince años en Normal, Rápido y Muy rápido (semillas 1, 7 y 42): ninguna muerte por hambre; Normal crece de 7 a 10, Rápido termina entre 4 y 8, Muy rápido se extingue o casi (límite conocido, en el backlog). Un valle sin órdenes de recolectar sigue muriendo en su primer invierno. Detalle en "Balance de población y comida" de la spec | ✅ |
| Rendimiento de una partida larga | Diez años en Rápido: 0,032 ms por tick en la simulación; `--load diez-anos --benchmark 30` en Godot, con 10 colonos: 280 FPS de media, 220 en el 1 % más lento, 0,11 ms por tick (RTX 4090 de portátil). Presupuesto: 60 FPS y 2 ms | ✅ |
| Rendimiento de una partida nueva | `--benchmark 20`: 361 FPS y 0,08 ms por tick | ✅ |
| Paquete de Windows | `./tools/package-windows.ps1`: exporta, corre el smoke sobre el ejecutable exportado y arma `OurLife-v0.3.0-windows-x86_64.zip` (73,3 MB) | ✅ |
| Paquete de macOS | `./tools/package-macos.ps1`: app universal con firma ad hoc (122,5 MB). Hay que volver a firmarla en la Mac | ✅ (probado en la Mac por el usuario) |
| Capturas | Ficha en es a 1080p y en en a 960 × 540, panel Colonia a 960 × 540, las cinco etapas en fila, globos de interacción, cuna y cama | ✅ |

## Hallazgos

| # | Qué pasó | Pasos para reproducirlo | Gravedad (bloquea `v0.3.0` o no) |
|---|---|---|---|
1|Aparecen muchas veces el mensaje de hambre crítica||no
2|Cuando se escava una pendiente de una montaña, queda como si fueran 2 triangulos en el cubo que seleccioné. Estaría bueno que se vea mejor que es una zona que se escavó. Lo mismo cuando pones una escalera en el piso, sería bueno no mostrar un cubo vacio, sino algo que sea más notorio de que es una entrada a una cueva.||no

## Resultado

- Fecha y PC: 18 y 19 de octubre de 2026, la PC con Windows y la Mac con Apple Silicon. La segunda pasada, con los zips del 19 de octubre (siete fundadores), repitió los tres puntos marcados.
- Quién jugó: el usuario (gtaulamet).
- ¿Se puede publicar `v0.3.0`? Sí: todo en ✅. Los dos hallazgos no bloquean y quedaron en el backlog.
