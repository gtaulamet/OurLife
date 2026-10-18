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

Para ver nacimientos sin esperar horas: Nueva partida con ritmo **Muy rápido**, recolectar en otoño y jugar a 4×. Una pareja concibe cuando la aldea tiene comida guardada para 30 días por colono.

## Checklist

Marcar cada punto con ✅ (bien), ❌ (falla, anotar el hallazgo abajo) o ➖ (no se probó).

### Nueva partida y llegada

- [ ] Nueva partida ofrece el ritmo de la vida (Normal, Rápido, Muy rápido) con su descripción; Rápido viene elegido.
- [ ] Llegan cinco colonos con nombre y apellido, de edades distintas, al menos una mujer y un varón; en la ficha, dos parejas.

### Ficha del colono (F)

- [ ] F o el botón del inspector abren la ficha del seleccionado; sigue a la selección y se cierra con F, Esc o Cerrar.
- [ ] Se leen la etapa y la edad, el ánimo, la personalidad en palabras ("Algo áspero"), las habilidades con su avance, la familia, los pensamientos, los deseos, las relaciones y las últimas interacciones.
- [ ] En un colono en pareja, el botón "Hijos: permitidos / no" cambia al apretarlo, también con el tiempo corriendo; vale para los dos de la pareja.
- [ ] El panel Colonia (P) muestra Edad, Ánimo y la columna Crianza en las prioridades.

### Vida social y ánimo

- [ ] Sobre los colonos que interactúan aparece un globo con un ícono (charlar, bromear, enseñar, discutir, consolar, cortejar, jugar) que se desvanece.
- [ ] Con los días se forman amistades o rivalidades, y el registro lo avisa una vez.
- [ ] El ánimo sube y baja con lo que viven (comer, dormir en cama o en el suelo, charlar); con ánimo muy bajo puede haber una crisis, avisada en amarillo.
- [ ] Los deseos aparecen en la ficha con los días que quedan.

### Parejas, nacimientos y crianza

- [ ] El registro avisa el embarazo y el nacimiento ("dio a luz a una niña: …"); el inspector y la ficha muestran "Embarazada (N %)".
- [ ] El bebé lleva el apellido del padre, aparece en la familia de los dos y crece: bebé, niño, adolescente, adulto, con un aviso en cada etapa.
- [ ] Cuando un bebé tiene hambre, la madre o el padre (o, si no están libres, otro colono) le lleva comida; el bebé no come solo y gatea cerca de su madre.
- [ ] Una cuna construida (Construir > Cuna) la toma un bebé; la cama, los demás.
- [ ] Los niños no trabajan: juegan con otros niños o con su familia. Los adolescentes hacen trabajos livianos.
- [ ] Con "Hijos: no", esa pareja no tiene hijos.

### Arte de las etapas (cargar `etapas`)

- [ ] Las cinco etapas se distinguen: bebé muy chico, niño chico con la cabeza grande, adolescente algo más bajo, adulto y anciano con canas.
- [ ] El bebé se sienta cuando está quieto; los ancianos caminan más despacio; dormidos, todos se acuestan sobre su cama o cuna.

### Vejez y partida larga (cargar `diez-anos`)

- [ ] Los ancianos mueren de viejos con un aviso, y la familia lo nota (pensamientos de duelo en la ficha).
- [ ] La aldea sigue viva, con comida en el HUD; jugar un año más a 4× sin que muera de hambre si se sigue recolectando.
- [ ] Anotar si los avisos de "tiene mucha hambre" molestan (en una partida larga hay unos 8 por mes).

### Lo de antes sigue igual

- [ ] Una partida de `v0.2.5` carga, con los colonos con edad, apellido, personalidad, habilidades y opiniones.
- [ ] Guardar a mitad de partida, salir, cargar y seguir: todo continúa igual, también un embarazo en curso.
- [ ] Construcción, almacenes, día y noche y opciones como en `v0.2.5`.
- [ ] En español y en inglés, ningún texto cortado, superpuesto ni como clave cruda, también en la ficha.

### macOS (en una Mac con Apple Silicon)

- [ ] Descomprimir `OurLife-v0.3.0-macos-universal.zip`, volver a firmar con los pasos del README (`codesign --force --deep --sign - OurLife.app` y `xattr -cr OurLife.app`) y abrir la app.
- [ ] Smoke en la terminal: `./OurLife.app/Contents/MacOS/OurLife --headless -- --smoke --seed 42 --ticks 5000` termina con `[smoke] OK` y la huella `40A56EC5ADD2BA9E`, la misma que en Windows.
- [ ] Las etapas, los globos y la ficha se ven como en Windows.

### Rendimiento

- [ ] `.\OurLife.exe -- --benchmark 20 --seed 42 --ticks 5000` termina con "within the budget" en `%APPDATA%\OurLife\logs\godot.log`.
- [ ] Jugando a 4× con la partida de diez años no hay tirones visibles.

## Pasada automática

Lo que no necesita manos, verificado por Claude Code antes de la prueba humana.

| Punto | Cómo se verificó | Resultado |
|---|---|---|
| Tests | 702 en verde: Core, localización y contenido, con el balance de diez años (`BalanceTests`) y las partidas viejas de los formatos 1 a 6 | ✅ |
| Smoke | Código 0; abre la ficha, prueba el botón de hijos y revisa que no falten textos. Huellas `5C715E8411C7DFC8` (semilla 1) y `40A56EC5ADD2BA9E` (semilla 42) | ✅ |
| Balance de población y comida | Partidas de diez años en Rápido y Muy rápido, semillas 1 y 42: ninguna muerte por hambre; en Rápido la aldea se mantiene en 6 o 7. En Muy rápido tiende a extinguirse en unos diez años (límite conocido, en el backlog). Detalle en "Balance de población y comida" de la spec | ✅ |
| Rendimiento de una partida larga | Diez años en Rápido: 0,017 ms por tick en la simulación; `--load diez-anos --benchmark 30` en Godot: 331 FPS de media, 251 en el 1 % más lento, 0,10 ms por tick (RTX 4090 de portátil). Presupuesto: 60 FPS y 2 ms | ✅ |
| Rendimiento de una partida nueva | `--benchmark 20`: 361 FPS y 0,08 ms por tick | ✅ |
| Paquete de Windows | `./tools/package-windows.ps1`: exporta, corre el smoke sobre el ejecutable exportado y arma `OurLife-v0.3.0-windows-x86_64.zip` (73,3 MB) | ✅ |
| Paquete de macOS | `./tools/package-macos.ps1`: app universal con firma ad hoc (122,5 MB). Hay que volver a firmarla en la Mac | ✅ (falta probarla en la Mac) |
| Capturas | Ficha en es a 1080p y en en a 960 × 540, panel Colonia a 960 × 540, las cinco etapas en fila, globos de interacción, cuna y cama | ✅ |

## Hallazgos

| # | Qué pasó | Pasos para reproducirlo | Gravedad (bloquea `v0.3.0` o no) |
|---|---|---|---|

## Resultado

- Fecha y PC:
- Quién jugó:
- ¿Se puede publicar `v0.3.0`?
