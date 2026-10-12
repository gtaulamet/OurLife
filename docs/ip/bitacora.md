# Bitácora de sesiones

Registro de trabajo entre sesiones de Claude Code. Al retomar, leer **Estado actual** y **Pendientes**. Al terminar una sesión, actualizar esas dos secciones y agregar una entrada al principio del **Historial**.

## Estado actual

*Actualizado el 2026-10-11.*

| Dato | Valor |
|---|---|
| Rama de trabajo | `ip2/h2-3-almacenes`, creada desde `main` (H2.2) |
| `main` | En el cierre de IP1, con el tag `v0.1.0`, también en `origin` |
| Hitos hechos | IP1 completa: H1.1 a H1.8. Cerrada el 2026-10-11 |
| IP en curso | IP2 "Asentamiento", planificada en `docs/ip/IP2.md`: siete hitos, 77 a 101 h |
| Hitos de IP2 hechos | H2.1 Guardado y carga, H2.2 Día, noche y sueño |
| En curso | H2.3 Almacenes y acarreo |
| Validación | `dotnet build` sin advertencias, 435 tests en verde |
| Huella del smoke | `19A91FF3BCECB119` con `--seed 1 --ticks 5000`; `9D2B68F847F36435` con `--seed 42 --ticks 5000`. Cambiaron en H2.2 por el sueño nocturno y el balance del descanso |
| Versión | `v0.1.0`, publicada como release de GitHub con el zip: https://github.com/gtaulamet/OurLife/releases/tag/v0.1.0. `./tools/package-windows.ps1` vuelve a armar el zip (no se versiona) |

## Pendientes

En orden:

1. **Probar en el editor** (lo puede hacer el usuario): guardar, cargar y el autoguardado de H2.1; ver pasar un día entero con el ciclo de H2.2 y apagarlo en Opciones.
2. **H2.3** (`docs/gdd/almacenes.md`):
   1. Revisión de código, documentación (`overview.md`, `IP2.md`) y cierre.
3. **En la otra PC**, para exportar hay que instalar las plantillas de Godot 4.7.2 .NET (README, "Exportar para Windows").

## Notas prácticas

- **`GODOT_BIN`** está definida como variable de usuario, pero la sesión de PowerShell de Claude Code no la hereda. Leerla con `[System.Environment]::GetEnvironmentVariable('GODOT_BIN','User')`.
- **Mensajes de commit:** en PowerShell, `git commit -F -` con un here-string no funciona porque el texto llega como argumento. Escribir el mensaje en un archivo del scratchpad y usar `git commit -F <archivo>`.
- **Códigos de salida:** no cortar la salida de Godot con `Select-Object -First`, porque `$LASTEXITCODE` deja de ser confiable. Guardar la salida en una variable y filtrarla después.
- **Reimportación:** si un `.import` cambia dentro del mismo segundo que la importación anterior, Godot no lo reimporta. Basta con borrar `game/.godot/imported/<archivo>-*` y volver a correr `--import`.
- **GitHub CLI:** `gh` está instalado en esta PC y autenticado como `gtaulamet`. Una sesión de PowerShell abierta antes de instalarlo no lo tiene en el `PATH`: usar `& 'C:\Program Files\GitHub CLI\gh.exe'`. Las releases se crean con `gh release create <tag> <zip> --verify-tag --notes-file <archivo>`.
- **Archivos `.uid`:** un script nuevo no tiene `.uid` hasta que el editor abre el proyecto. `& $env:GODOT_BIN --headless --path game --import` los genera sin abrirlo; correrlo antes de cerrar un hito y versionarlos.
- **Fuente nítida:** el smoke y `--screenshot` fallan si la caché de la fuente tiene suavizado. El mensaje indica cómo reimportar.
- **Comandos bloqueados:** un filtro de seguridad rechaza los comandos de PowerShell cuyo texto contiene `/code-review`, aunque sea dentro de un string. Esos textos se editan con la herramienta de edición.
- **Mirar el valle sin render:** un programa de consola temporal que referencie `OurLife.Core` puede volcar el valle en ASCII (alturas, materiales y plantas). Así se revisó H1.4.

## Historial

### 2026-10-11: H2.3 Almacenes y acarreo (en curso)

- Rama `ip2/h2-3-almacenes` desde `main`. Spec `docs/gdd/almacenes.md`.
- Avance 1 (Core):
  - Def `ItemCategory` (madera, piedra y comida) y `Item.category`. Tipo de trabajo `hauling` con el trabajo `haul`, después de recolectar.
  - Componentes `Stockpile` (celdas ordenadas y categorías permitidas) y `Carried`. `DesignateCommand` con `Stockpile` crea una zona con las celdas libres y pisables; `Cancel` además saca celdas de las zonas. `SetStockpileFilterCommand` cambia un filtro. `StockpilesChangedEvent` para la vista.
  - `StorageIndex` (derivado, se arma una vez por tick si hace falta): zona de cada celda y qué hay en cada celda de almacén.
  - Acarreo: el colono elige la pila suelta más cercana con destino; el destino es la celda de almacén más cercana a la pila que acepta el objeto y tiene lugar. Reserva la pila y la celda. Levanta lo que entra, lo lleva y lo deja. Si deja el trabajo con algo en las manos, lo suelta donde está; si muere, también.
  - Tests: `StockpileTests` (troncos apilados en el almacén, filtro de comida, el que tiene hambre suelta la carga y después se guarda, dos acarreadores nunca comparten pila ni destino, Cancelar y celdas ocupadas o no pisables). La continuidad del guardado incluye un almacén, un filtro y un colono cargando, en Core y con el contenido real. 435 tests.
- Avance 2 (Godot):
  - Herramienta Almacén (Z): sale sola de `DesignationKind`, con su tecla. Las celdas de almacén se pintan de azul, finas y sobre el suelo.
  - El inspector de una celda de almacén lista las categorías con un botón Sí/No que manda `SetStockpileFilterCommand`; el de un colono dice qué lleva. El colono muestra el objeto en las manos.
  - Los almacenes sólo toman suelos planos: sobre las rampas la losa quedaba enterrada y las pilas se veían mal (spec actualizada).
  - Las órdenes del smoke y de `--orders` incluyen un almacén; el smoke informa cuánto quedó guardado (todos los troncos en las semillas 1 y 42). Opción `--inspect-stockpile` para capturas.
  - Huellas del smoke nuevas por el almacén en sus órdenes: `0988439535AF5339` con la semilla 42 (la de la semilla 1 se toma al cerrar).
  - Capturas revisadas: acarreo y carga en es, filtros en en.

### 2026-10-11: H2.2 Día, noche y sueño

- Rama `ip2/h2-2-dia-noche` desde `main`. Spec `docs/gdd/dia-y-noche.md`.
- Avance 1 (Core):
  - `AiConfig` tiene `nightStartHour` y `nightEndHour` (21 a 6 en el contenido; la noche puede cruzar la medianoche). `NeedDef.nightThreshold` (9000 para el descanso): de noche, un colono con el descanso por debajo se acuesta antes que cualquier trabajo. De día sólo duerme con el descanso crítico, y de noche no se levanta aunque esté lleno.
  - Balance: el descanso baja 500 por hora (antes 600) y sube 1200 durmiendo (antes 900): 15 h despierto y 9 h dormido se compensan.
  - Tests: `NightSleepTests` (se acuestan a las 21 y se levantan a las 6, de día no se acuestan cansados pero sí agotados, siguen durmiendo llenos hasta las 6, noches que cruzan o no la medianoche), validación de los defs y, con el contenido real, la aldea duerme entera a las 2 y nadie duerme al mediodía durante tres días. Los tests del invierno siguen en verde. 427 tests.
- Avance 2 (Godot):
  - `DayNightCycle` dentro de `WorldView`: el sol cruza el cielo de 6 a 21 y una luna fría hace el mismo arco de noche; luz, ambiente y cielo se mezclan entre amanecer, día, atardecer y noche. Apagado, la luz es la del mediodía de IP1.
  - Opción "Ciclo de día y noche: Sí | No" en Opciones, guardada en `settings.cfg`.
  - El registro agrega una línea con la fecha antes del primer mensaje de cada día.
  - `--hour <h>` lleva la partida hasta esa hora para capturas.
  - 4 claves nuevas en es y en. Capturas revisadas a las 6, 12, 20 y 0, y Opciones en inglés.
  - Huellas nuevas del smoke por el balance del descanso (ver Estado actual).
- Avance 4: segunda ronda de revisión, 10 hallazgos, 7 corregidos y 3 que son lo que pide la spec (detalle en `IP2.md`). Mismas huellas.
- Avance 3 (revisión y cierre): 9 hallazgos, todos corregidos (detalle en `IP2.md`). Lo principal: `nightThreshold` pasa a 10 000 para que nadie quede despierto de noche, y la luz ya no salta al amanecer. Documentación y `main` adelantado a H2.2. 429 tests.

### 2026-10-11: planificación de IP2

- `docs/ip/IP2.md`: siete hitos (guardado, día y noche con sueño, almacenes, construcción, muebles y camas, gestión, integración), con horas, entregables y definición de hecho.
- El guardado va primero para que el test de continuidad cubra todo lo que se agregue después.
- Pendientes de IP1 repartidos: el nombre de la aldea y la pantalla de error en H2.1, el día en el registro y el balance del invierno en H2.2, `terrainAmplitude` se evalúa en H2.4 y los argumentos de entidad en `TextResolver` en H2.6. Lo de arte que no entra quedó en el backlog.
- Rama `ip2/h2-1-guardado` creada desde `main`.
- H2.1, avance 1: spec `docs/gdd/guardado.md` (qué se guarda, ranuras, autoguardado a las 00:00, pantallas, errores, textos y criterios) y ADR-010 (ZIP con `header.json` y `state.json`, tipos con nombre estable en `SaveTypes`, materiales por id, versión de formato y errores tipados).
- H2.1, avance 2 (Core):
  - `Simulation.ColonyName`, que fija `ColonyStart.Settle` y entra en la huella. El smoke usa el nombre por defecto del español, para que la huella no dependa de `--lang`.
  - `SaveGame` (`OurLife.Core.Save`): `Write`, `ReadHeader` y `Load` sobre un `Stream`. `SaveTypes` registra los nombres estables de componentes y comandos. `[DefRef]` marca las propiedades con ids de defs, que se validan al cargar.
  - Ganchos internos para restaurar: ranuras de entidades, flujos del RNG y tick.
  - Las entradas del ZIP llevan una fecha fija (1980): el ZIP no admite fechas fuera de 1980–2107 y así el archivo no depende del reloj.
  - Tests: 16 en Core (continuidad con una colonia que excava, tala, recolecta y tiene una orden inalcanzable y un comando pendiente; versiones; archivos dañados; contenido faltante) y 4 con el contenido real (continuidad en las semillas 1, 7 y 42 cortando a los 20 000 ticks; un valle guardado pesa menos de 1 MB).
- H2.1, avance 3 (Godot):
  - `SaveSlots`: un archivo `.olsave` por ranura en `user://saves`, escrito en un temporal que después reemplaza al anterior; ranuras dañadas listadas al final para poder borrarlas.
  - `GameScreen` tiene un identificador de partida y autoguarda en `auto-<id>` al empezar cada día del juego. Sin ranuras (capturas y smoke sin `--saves`) no guarda ni autoguarda.
  - Pantallas `LoadGameScreen`, `SaveGameScreen` (desde la pausa, con "Partida guardada" al volver) y `ErrorScreen`; confirmación de Sí/No en `UiBuilder.Confirm`. "Cargar partida" del menú principal se habilita con alguna ranura.
  - Error al arrancar: con un jugador mirando, el juego muestra la pantalla de error en lugar de cerrarse; si fallan los textos, muestra el detalle tal cual.
  - Opciones `--saves <carpeta>`, `--load <ranura>` y `--show load|save|error`.
  - El smoke guarda y carga en memoria (misma huella, y la misma 600 ticks después), escribe, lista y carga una ranura en `user://smoke-saves` y arma las pantallas nuevas. Un valle guardado pesa 33 KB y tarda unos 33 ms en guardarse y otro tanto en cargarse.
  - 23 claves nuevas en es y en; se quitó `ui.mainmenu.load_later`.
- H2.1, avance 7: tercera ronda de revisión, 10 hallazgos, 8 corregidos (detalle en `IP2.md`). Validación más estricta al cargar y `ConfirmDialog` con Esc propio. 409 tests, mismas huellas. `main` adelantado a H2.1.
- H2.1, avance 6: segunda ronda de revisión, 10 hallazgos, 8 corregidos (detalle en `IP2.md`). El smoke ahora guarda a mitad de sus ticks; las huellas no cambiaron. 406 tests.
- H2.1, avance 5 (revisión de código y cierre): 10 hallazgos, 9 corregidos y uno descartado con su motivo (detalle en el registro de H2.1 de `IP2.md`). Documentación: `overview.md` (sección Guardado), ADR-010 en la lista, skill `godot-run`, agentes y `CLAUDE.md` (registrar cada componente o comando en `SaveTypes`). H2.1 queda hecho; falta adelantar `main`.
- H2.1, avance 4 (capturas): menú principal, Cargar, Guardar y Error revisadas en es y en (en `game/out/h21-*.png`, con `--saves out/saves`; la carpeta es relativa a `game/`). El autoguardado apareció al pasar la medianoche con `--ticks 1500`. Los botones de menú se estiraban en las columnas anchas: ahora mantienen su ancho, centrados.

### 2026-10-10: H1.7 Interacción y UI (en curso)

- Adelanté `main` a H1.6 y creé la rama `ip1/h1-7-interaccion-ui`.
- El usuario pidió guardar cada avance en la bitácora y subirlo a GitHub para seguir desde otra PC. Subí `main` (estaba en H1.2 en `origin`) y la rama, y lo anoté en `CLAUDE.md`.
- Avance 1: spec `docs/gdd/interfaz-ip1.md`, con pantallas, controles, HUD, inspector, selección de celdas, textos nuevos y criterios de aceptación.
- Avance 2: `MapRaycast` en `OurLife.Core.Map` (recorrido de grilla de Amanatides y Woo que salta los niveles ocultos), con 8 tests; `Settings` guarda la pantalla completa. 364 tests en verde.

- Avance 3:
  - `GameSession` corre la simulación en tiempo real con `TickPacer`, con pausa, velocidades y espera de menú, y aplica en el acto las órdenes dadas en pausa (`Simulation.ApplyCommandsNow`, con test de que da la misma huella).
  - `GameScreen` une la sesión y la vista.
  - `EntityRenderer` reemplaza a `PlantRenderer`: plantas, pilas y colonos que siguen los eventos, con colonos que avanzan entre celdas y miran hacia donde caminan.
  - `OrderMarkers` marca las órdenes con colores.
  - Escalera en ambos sentidos con hueco.
  - Arbusto sin frutos (`plant.berry_bush.harvested`, `plant_bush.glb`).
  - Colonos a escala 2.
  - Opciones `--zoom` y `--ticks` fuera del smoke, para capturas.
  - Revisado en captura con `--seed 42 --level 14 --zoom 12 --ticks 300`. Los colonos están en pose de T porque el modelo no trae animación; queda para H1.8 o el backlog.
- Avance 4:
  - Excavar pasa a la tecla M, porque la E ya rotaba la cámara (spec y plan actualizados).
  - Textos de todo H1.7 en es y en: menús, HUD, órdenes, inspector y registro.
  - `CellPicker` usa el rayo de la cámara con `MapRaycast`.
  - `GameInput`: marco bajo el ratón, arrastre con orden que envía `DesignateCommand`, clic que selecciona colono o celda, teclas de órdenes y de tiempo, y Esc o clic derecho para salir.
  - `InspectorPanel` dentro de `Hud` (una `CanvasLayer`); `TextResolver.Capitalized`.
  - `UiTextTests` comprueba las claves que se arman en ejecución.
  - Opciones `--inspect` (selecciona al primer colono) y `--orders` (las órdenes del smoke) para capturas.
  - Las marcas de órdenes y de selección no proyectan sombra: rayaban el terreno.
  - Revisado en capturas es y en con `--seed 42 --level 14 --zoom 22 --ticks 200 --orders --inspect`.
- Avance 5:
  - `Hud` completo: fecha y hora, pausa y velocidades (resaltan la activa), nivel Z, inspector, registro, ayuda de la orden activa y barra de órdenes.
  - `EventLog` guarda `LocalizedText` y retraduce el historial al cambiar de idioma.
  - `ColonyStart` emite `event.colony.arrived` ("Llegan 5 colonos al valle"), que es el primer mensaje del registro.
  - Clave `ui.hud.date` en lugar de armar la fecha en el código.
  - Revisado en capturas es y en. Tiny5 a 16 px se lee sobre el 3D, aunque es chica: decidir en QA si se agranda.
- Avance 6:
  - `UiBuilder` y un archivo por pantalla: `MainMenu` (Cargar deshabilitado con aviso), `NewGameScreen` (nombre de la aldea y semilla, con "Al azar"), `OptionsScreen` (idioma en vivo y pantalla completa, guardados), `PauseMenu` y `GameOverScreen`.
  - `GameScreen` abre los menús en una capa encima del HUD y detiene el tiempo y la entrada mientras están abiertos; la partida termina con `ColonyLostEvent`.
  - `GameRoot` arranca en el menú principal y aplica la pantalla completa guardada.
  - Opciones `--play` (partida directa) y `--options` (opciones, sobre la partida con `--play`).
  - El nombre de la aldea se ve en la barra superior. Todavía no se guarda en la simulación: hará falta para el guardado de IP2.
  - El smoke arma la pantalla de partida con el inspector, la pausa, las opciones y los tres menús, y falla si falta un texto.
  - Revisadas las capturas del menú principal y de las opciones en es y en. La casilla de pantalla completa tiene poco contraste sobre el fondo oscurecido: revisar en QA.

### 2026-10-11: H1.8 Integración y cierre de IP1

- El usuario hizo el playtest humano con el checklist: todo aprobado, sin hallazgos nuevos, y visto bueno para publicar `v0.1.0`. El hallazgo menor de la pasada automática (el día en el registro) pasó al backlog.
- Registro de H1.8 en `IP1.md` e IP1 marcada como cerrada; README con el estado de la versión.
- Adelanté `main` al cierre, creé el tag anotado `v0.1.0` y subí los dos.
- El usuario pidió publicarla como release de GitHub. Instalé `gh` con winget, el usuario se autenticó con `gh auth login`, rearmé el zip desde el commit del tag (el anterior ya no estaba en `builds/`) y creé la release "OurLife v0.1.0: Primeros colonos" con el zip y notas en español.

- Creé la rama `ip1/h1-8-integracion` desde `main` y dividí el hito en seis avances (ver Pendientes).
- Avance 1:
  - Playtest automático con el smoke: 30 días (43 200 ticks) en las semillas 1, 42 y 7, y un año y medio (150 000 ticks) en la 1 y la 42. Los 5 colonos sobreviven siempre; unos 0,006 ms por tick.
  - Modo `--benchmark <s>`: juega a 4× con las órdenes, sin vsync, descarta 2 s y mide FPS y ms por tick; sale con 1 si no cumple 60 FPS y 2 ms por tick. `GameSession.TickTime` mide el tiempo de los ticks fuera del Core.
  - Resultado en esta PC (RTX 4090 de portátil, ventana de 1280×720): 447 FPS de media, 360 en el 1 % más lento, 0,033 ms por tick.
  - El def de otra época bloqueado ya lo cubre `UnlockServiceTests`.
- Hallazgo del playtest automático: no se podía perder, porque las bayas rebrotaban todo el año. El usuario eligió que el invierno sea la amenaza. Además pidió que yo descargara las plantillas de exportación: bajé el `.tpz` oficial de 4.7.2 mono (1,2 GB), instalé sólo las de Windows x86_64 y borré el paquete.
- Avance 5 (balance), adelantado porque cambia el checklist:
  - `PlantDef.dormantSeasons`: en esas estaciones una planta recolectada no rebrota. El cargador valida que las estaciones existan en el calendario. El arbusto de bayas duerme en invierno.
  - Con 8 bayas y 4 días de rebrote, los arbustos apenas daban lo que comen 5 colonos: aun recolectando, morían en invierno. Pasan a 10 bayas y 3 días.
  - Resultado, en tests del contenido real (`ColonyTests`, semillas 1 y 42): sin recolectar, la colonia muere en su primer invierno; recolectando en otoño en un radio de 25 celdas, lo pasa.
  - Al probarlo apareció un problema de la interfaz: la caja de una orden quedaba en un solo nivel y no tomaba las plantas de una ladera. Excavar y Escalera siguen en un nivel; Talar, Recolectar y Cancelar toman todo lo visible en el rectángulo.
  - El smoke hace clic en cualquier colono a la vista: en la semilla 42 el primero quedaba tapado por el terreno.
- Avances 3 y 4 (exportación, README y CREDITS):
  - Preset "Windows Desktop" en `game/export_presets.cfg`: incluye los JSON de contenido y `visuals.json`, excluye `out/` y exporta a `builds/windows` (ignorado por git). `config/version="0.1.0"` en `project.godot`.
  - El ejecutable exportado pasa el smoke con la misma huella que el editor (`05BB6F15EEBBEDC9`), aunque corre sobre el .NET 8.0.27 empaquetado y el editor sobre .NET 10. La captura es idéntica.
  - Modo `--licenses <archivo>` (`LicenseNotices`): escribe la licencia de Godot, los avisos de sus componentes y el de .NET, leídos del propio motor. Godot exige distribuirlos con el juego.
  - `tools/package-windows.ps1`: exporta, corre el smoke sobre el exportado, agrega `LICENSES.txt` y `CREDITS.md`, y arma el zip (75 MB).
  - README con cómo se juega, cómo exportar y cómo medir; CREDITS con el motor y el runtime.
  - Checklist del playtest humano en `docs/ip/playtest-ip1.md`, con atajos `--ticks` para empezar en otoño (43 200), en invierno (64 800) o en el día 8 del invierno (74 880). El tick 0 es el día 1 a las 06:00.
  - Revisión de código de H1.8: 10 hallazgos, 8 corregidos.
    - Cancelar vuelve a un solo nivel, para no borrar órdenes enterradas que no se ven.
    - Talar y Recolectar siempre incluyen el nivel de la celda inicial.
    - El benchmark falla sin ventana, si no corrió ningún tick, o combinado con `--smoke` o `--screenshot`.
    - El script de empaquetado pone comillas a la ruta de `LICENSES.txt` y sus comentarios pasan al inglés.
    - El cargador rechaza `dormantSeasons` con todas las estaciones.
    - Quedan como están: que la caja de Talar y Recolectar se dibuje en un nivel, porque coincide en x e y con lo que se marca; y el costo de `PlantGrowthSystem` en invierno, que son unos 57 arbustos por tick con el benchmark en 0,03 ms por tick.
  - El usuario preguntó por un ciclo de día y noche. Decidió que vaya en IP2, junto con que los colonos prefieran dormir de noche, y no en `v0.1.0`. Quedó en `plan-maestro.md`: IP2 pasa a 77–101 h, el ciclo se adelanta de IP6 y a IP6 le quedan las antorchas.
  - El lanzador de consola (`OurLife.console.exe`) sólo se genera en las exportaciones debug: el ejecutable de release no devuelve su salida a la terminal, así que se espera con `Start-Process -Wait` y se lee `%APPDATA%\OurLife\logs\godot.log`.

### 2026-10-11: H1.7, avance 7 (QA y cierre)

- El botón presionado del tema por defecto era apenas más oscuro que el normal: no se veía qué idioma, velocidad u orden estaba activa. `UiTheme` le da un fondo ocre (`PressedColor`) con texto blanco, y copia los márgenes por defecto para que los botones no cambien de tamaño.
- La casilla de pantalla completa pasa a una fila "Pantalla: Ventana | Pantalla completa", igual que la del idioma. Claves nuevas `ui.options.display` y `ui.options.windowed` en es y en, y en la spec.
- Tiny5 a 16 px se lee bien sobre el 3D en las capturas: no se agranda.
- Capturas revisadas en es y en: menú principal, opciones (sobre la partida y desde el menú) y partida con órdenes, inspector y registro.
- 367 tests en verde; smoke con código 0 y las mismas huellas (`AED7A63935A0B1F7` con la semilla 1).
- Primera ronda de revisión de código sobre toda la rama: 10 hallazgos, todos corregidos. El detalle está en el registro de H1.7 de `IP1.md`. Lo más importante: el rayo elegía la celda de adelante al apuntar un suelo con la cámara inclinada, y arrastrar una orden sobre una ladera marcaba todos los niveles enterrados. El smoke ahora dibuja la partida que trabajó, y `GameScreen` es el único lugar que arma una partida. 368 tests.
- Segunda ronda: 10 hallazgos más, todos corregidos (detalle en `IP1.md`). Lo más importante: un clic en el cuerpo de un colono elegía la celda de atrás; ahora se elige por la caja del modelo dibujado, y el smoke lo comprueba. La lista de órdenes sale del enum (`DesignationKinds.Id`). 369 tests y las mismas huellas.
- Tercera ronda: 10 hallazgos, 9 corregidos y uno descartado con su motivo (detalle en `IP1.md`). Lo más importante: un colono detrás de una loma ganaba el clic; `MapRaycast` ahora informa la distancia del impacto. También faltaban los `.uid` de 11 scripts: `--import` los genera sin abrir el editor. 370 tests.
- Documentación de cierre: `IP1.md` (H1.7 hecho), `plan-maestro.md` (árbol de `game/src`), `overview.md`, ADR-009 ("Resuelto en H1.7"), `art.md` (modelos que faltan), `i18n.md`, la skill `godot-run` y los agentes godot-engineer y qa-tester, con las opciones nuevas de captura. Los ejemplos de captura del valle necesitan `--play`, porque sin él el juego abre el menú.
- Cuarta ronda: 10 hallazgos, 7 corregidos y 3 descartados con su motivo (detalle en `IP1.md`). Lo principal: soltar una orden fuera del mapa o sobre el HUD marca la caja que se veía, un clic sobre un colono contra el cielo lo selecciona, y el primer cuadro ya no corre 40 ticks de golpe. Las capturas ignoran el ratón real.
- Adelanté `main` al cierre de H1.7 y lo subí.

### 2026-10-10: H1.6 Colonos e IA

- Retomé el trabajo sin commitear de la sesión anterior, que no había quedado en la bitácora:
  - Spec `colonos-e-ia.md` y ADR-011.
  - Defs `Scenario`, `AiConfig` y `WorkType`.
  - Sistemas de la colonia, A*, conectividad incremental y árboles que bloquean.
  - Tests de escenario y el smoke con la colonia.
- El smoke no llegaba a cavar la escalera: la caja de la orden era fija y caía en rampas o plantas. Ahora busca la celda válida más cercana a la llegada.
- `/code-review` en dos rondas, con 10 y 7 hallazgos, todos corregidos. El detalle está en el registro de H1.6 de `IP1.md`. Lo más importante: la búsqueda de árboles que cierran la superficie podía hacer fallar la generación del mundo, y ahora sigue las mismas reglas de movimiento que los colonos.
- `main` ya estaba en H1.5 (`e9e2407`); la bitácora decía H1.4.

### 2026-10-10: H1.5 Render y cámara

- Retomé el trabajo sin commitear de la sesión anterior, que no había quedado en la bitácora:
  - Modelos de Kenney, `visuals.json` y el cambio de `sprite` a `visual`.
  - Terreno por chunks, corte por nivel, plantas, cámara y los modos `--level` y `--probe`.
- Agregué `VisualCatalogTests`, que pedía ADR-009.
- Revisé las capturas y encontré tres problemas, que corregí:
  - Los árboles se veían celestes, porque Kenney guarda sRGB en el campo lineal de glTF. Ahora se corrige por kit.
  - El turquesa del kit chocaba con la paleta terrosa. Se reemplaza con la `palette` del catálogo.
  - La tapa del corte era de un solo color. Ahora toma el color del material oscurecido, así se leen los estratos.
- Ajusté la escala de los pinos y de los arbustos.
- `/code-review` en tres rondas, con 10, 10 y 9 hallazgos. El detalle está en el registro de H1.5 de `IP1.md`, y lo que quedó para después está en Pendientes.
- Actualicé ADR-009 ("Ajustes al implementar H1.5"), `art.md` (formato del catálogo y modelos que faltan), `plan-maestro.md`, `overview.md`, la skill `godot-run` y los agentes qa-tester y godot-engineer, que ahora sacan las capturas de idioma con `--probe`.
- Para mirar el valle de cerca, cambié a mano `StartDistance` de `WorldView` a 22 y lo volví a 85. Si hace falta seguido, conviene una opción `--zoom`.


### 2026-10-09: H1.4 Mapa y generación

- Rama `ip1/h1-4-mapa-generacion`, creada desde `ip1/h1-3-contenido-i18n`.
- Spec en `docs/gdd/mapa-y-generacion.md`. Hice yo mismo los pasos de spec, Core, tests y QA, siguiendo las reglas de los agentes, sin lanzar subagentes.
- Core:
  - `GameMap` con chunks sucios, `MapNavigation`, `Connectivity`, `GradientNoise` y `WorldGenerator`.
  - El def `WorldGen` con `worldgen.json` y las entidades `Position` y `Plant`. El mapa entra en la huella.
- El smoke genera el valle en unos 20 ms, con 55 a 57 arbustos de bayas y la superficie conectada.
- `/code-review` en dos rondas, con 10 y 8 hallazgos, todos corregidos. El detalle está en el registro de H1.4 de `IP1.md`.

### 2026-10-09: paso a 3D low-poly

- Analicé dos kits de Kenney. El Hexagon Kit se descartó porque obliga a usar una grilla hexagonal, y eso encarecía el Core y la construcción. El usuario eligió el Nature Kit, que es CC0 y está pensado para una grilla cuadrada.
- Escribí ADR-009, que reemplaza a ADR-007. Define la escala de una celda por unidad de Godot, el catálogo `visuals.json`, el terreno por chunk con `MultiMesh` y tinte por material, el corte por nivel Z, la cámara 3D, la selección por rayo y que se mantiene GL Compatibility.
- Actualicé `plan-maestro.md`, `IP1.md`, `art.md`, `overview.md`, `eras.md`, ADR-001, ADR-007, ADR-008, `CLAUDE.md` y los agentes art-integrator, godot-engineer y game-designer.
- El guardado pasa de ADR-009 a ADR-010.
- IP1 pasa de 100–135 h a 112–153 h, por los cambios en H1.5 y H1.7. La simulación no cambia.

### 2026-10-09: restauración de `.claude/`

- Tras reiniciar VS Code, `.claude/` dejó de estar bloqueada. Restauré sus archivos desde git y quité la marca `skip-worktree`.
- Actualicé la skill `godot-run`: `--locale` y `--screenshot` disponibles desde H1.3, rutas relativas desde `game/`, capturas sin `--headless`, reimportación tras cambiar un `.import` y cómo leer `GODOT_BIN` si la sesión no la hereda.
- Borré la memoria `claude-dir-restore-pending` y los pendientes correspondientes de `IP1.md`.

### 2026-10-09: cierre de H1.3

- Terminé el cambio que estaba sin commitear: la fuente Tiny5 se configura en `Tiny5.ttf.import` (sin suavizado, sin hinting, sin subpíxel y sin respaldo del sistema) y ya no desde el código.
- `/code-review` en tres rondas, con 15, 13 y 10 hallazgos. El detalle está en el registro de H1.3 de `docs/ip/IP1.md`. Lo principal:
  - El arranque está protegido: cualquier error sale con código y el smoke no se cuelga.
  - Godot recibe los mensajes ya formateados, no el patrón ICU.
  - ICU: estilo `integer`, un `select` sin argumento muestra `⟦nombre⟧`, los casos `=N` se analizan una sola vez y se normalizan, y el redondeo lleva las mitades al par.
  - Los cargadores exigen JSON estricto y rechazan `null` en los defs.
  - Las fuentes de contenido ignoran los archivos ocultos y los que empiezan con punto, con la regla en `IContentSource`.
  - `ProgressState` sólo cambia desde Core.
  - Tests nuevos: claves inexistentes o con argumentos usadas como texto visible, `ui.language.<idioma>` en cada idioma, y el cargador de catálogos real del juego enlazado en los tests.
- Decisiones que se mantienen frente a la revisión:
  - La fuente se configura sólo en la importación.
  - Un `select` con un valor sin caso usa `other`, como ICU.
  - Un cast deliberado a `ProgressState.Knowledge` no se impide.
- Se actualizaron `IP1.md` (H1.3 hecho), `backlog.md`, `i18n.md`, `art.md`, `overview.md`, ADR-005 y `plan-maestro.md`.
- Commit `007a389` en `ip1/h1-3-contenido-i18n`.
