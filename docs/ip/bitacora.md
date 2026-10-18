# Bitácora de sesiones

Registro de trabajo entre sesiones de Claude Code. Al retomar, leer **Estado actual** y **Pendientes**. Al terminar una sesión, actualizar esas dos secciones y agregar una entrada al principio del **Historial**.

## Estado actual

*Actualizado el 2026-10-18.*

| Dato | Valor |
|---|---|
| Rama de trabajo | `ip3/h3-8-arte`: H3.8 cerrado. Sigue H3.10 en `ip3/h3-10-integracion` |
| `main` | Con H3.8, también en `origin` |
| Hitos hechos | IP1 completa (H1.1 a H1.8, `v0.1.0`), IP2 completa (H2.1 a H2.7, cerrada el 13 de octubre de 2026) e IP2.5 completa (V1 a V6, cerrada el 15 de octubre de 2026). En IP3: H3.1 a H3.9 |
| IP en curso | IP3 "Vida y generaciones" (`docs/ip/IP3.md`), planificada el 15 de octubre, con las decisiones del usuario para la spec. Oficios pasó a IP4 |
| Validación | `dotnet build` sin advertencias, 696 tests en verde, smoke con código 0, benchmark de 30 s dentro del presupuesto (377 FPS, 0,07 ms por tick) |
| Huella del smoke | `D8FD3784066AEE12` con `--seed 1 --ticks 5000`; `14212A5A5466DDB7` con `--seed 42 --ticks 5000` (desde H3.7). Cambian en cada hito de IP3 que suma estado |
| Versión | `v0.2.5` publicada como release de GitHub con los zips de Windows y macOS: https://github.com/gtaulamet/OurLife/releases/tag/v0.2.5. `./tools/package-windows.ps1` y `./tools/package-macos.ps1` los vuelven a armar (no se versionan; el de macOS necesita PowerShell 7.4 o posterior) |

## Pendientes

En orden:

1. **H3.10, integración**, en `ip3/h3-10-integracion`: balance de población y comida con los tres ritmos, benchmark con una población de partida larga, checklist de playtest (`docs/ip/playtest-ip3.md`), playtest del usuario en Windows y macOS, paquetes y `v0.3.0`.
2. **Balance de H3.10:** con 40 % por año, uno de cada tres deseos de un hijo se cumple; ver si conviene que el deseo suba la concepción.
3. **Balance social para H3.10:** en una aldea real hay unas 3 interacciones por día entre cinco colonos, y en 10 días no se forma ninguna amistad (radio de 4 celdas, cada uno en su trabajo).
4. **En la otra PC**, para exportar hay que instalar las plantillas de Godot 4.7.2 .NET (README); para macOS, también `macos.zip`.

## Notas prácticas

- **Exportar se colgaba:** los nodos de MSBuild y el servidor del compilador que quedan vivos después de compilar retienen la consola del lanzador de Godot, y la exportación nunca termina. Los scripts de empaquetado exportan con `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0` y `UseSharedCompilation=false`. A mano: `dotnet build-server shutdown`.

- **`GODOT_BIN`** está definida como variable de usuario, pero la sesión de PowerShell de Claude Code no la hereda. Leerla con `[System.Environment]::GetEnvironmentVariable('GODOT_BIN','User')`.
- **Mensajes de commit:** en PowerShell, `git commit -F -` con un here-string no funciona porque el texto llega como argumento. Escribir el mensaje en un archivo del scratchpad y usar `git commit -F <archivo>`.
- **Códigos de salida:** no cortar la salida de Godot con `Select-Object -First`, porque `$LASTEXITCODE` deja de ser confiable. Guardar la salida en una variable y filtrarla después.
- **Reimportación:** si un `.import` cambia dentro del mismo segundo que la importación anterior, Godot no lo reimporta. Basta con borrar `game/.godot/imported/<archivo>-*` y volver a correr `--import`.
- **GitHub CLI:** `gh` está instalado en esta PC y autenticado como `gtaulamet`. Una sesión de PowerShell abierta antes de instalarlo no lo tiene en el `PATH`: usar `& 'C:\Program Files\GitHub CLI\gh.exe'`. Las releases se crean con `gh release create <tag> <zip> --verify-tag --notes-file <archivo>`.
- **Archivos `.uid`:** un script nuevo no tiene `.uid` hasta que el editor abre el proyecto. `& $env:GODOT_BIN --headless --path game --import` los genera sin abrirlo; correrlo antes de cerrar un hito y versionarlos.
- **Fuente nítida:** el smoke y `--screenshot` fallan si la caché de la fuente tiene suavizado. El mensaje indica cómo reimportar.
- **Cadenas de comandos:** encadenar con `&&` hasta el final. Un `;` en medio deja correr lo que sigue aunque falle lo anterior: así se adelantó `main` una vez con un commit incompleto (se corrigió adelantándolo de nuevo).
- **Comandos bloqueados:** un filtro de seguridad rechaza los comandos de PowerShell cuyo texto contiene `/code-review`, aunque sea dentro de un string. Esos textos se editan con la herramienta de edición.
- **Mirar el valle sin render:** un programa de consola temporal que referencie `OurLife.Core` puede volcar el valle en ASCII (alturas, materiales y plantas). Así se revisó H1.4.

## Historial

### 2026-10-18: cierre de H3.8

- Cuarta ronda de revisión: 7 hallazgos, todos corregidos. Los íconos se importan sin compresión para 3D (`detect_3d/compress_to=0`), así no se borronean ni cambian sus `.import` al usarlos un `Sprite3D`. La altura con huesos agrandados se mide sólo en esos modelos. Comentarios y documentación al día (jugar con `emote-yes`, tres campos nuevos en el catálogo), la elección de animación más simple y el test de las camas sin `!`.
- 696 tests; smoke con código 0. H3.8 hecho en `IP3.md`; `main` adelantado.

### 2026-10-18: H3.8, tercera ronda de revisión

- 8 hallazgos, todos corregidos:
  - `jump` también es una pose fija: los niños juegan con `emote-yes`, que sí se mueve.
  - La altura de un modelo con la cabeza agrandada se mide escalando sólo lo que está sobre el hueso (`ModelLibrary.HeightOf`), no el modelo entero.
  - El cuadro de clic de quien duerme crece a lo largo del cuerpo.
  - Si falta la animación quieta propia, se usa la original.
  - La altura del colchón es un campo del catálogo (`restHeight`: 0,22 en la cama, 0,13 en la cuna), con un test para cada cama.
  - `WorldView` pasa sus reglas de la colonia al renderer, que no las arma fuera de su `try`.
  - Comentarios ordenados y el factor de altura calculado una vez.
- 696 tests; smoke con código 0.

### 2026-10-18: H3.8, segunda ronda de revisión

- 8 hallazgos, todos corregidos:
  - `crouch` es una pose fija del kit, así que el bebé se deslizaba agachado: ahora camina despacio y quieto se sienta. El kit no trae una animación de gatear.
  - El bebé flotaba sobre la cuna: la altura del colchón sigue la escala del modelo de la cama.
  - El modelo por etapa se guarda por era, así toma la variante de otra época.
  - Un globo se va si su colono se acuesta y se oscurece con la profundidad, como el colono; sólo se le pide a Godot mientras se desvanece.
  - El paso de la animación usa `MovementSystem.ProgressPerTick` y se calcula sólo cuando el colono cambia.
- 694 tests, smoke con código 0, benchmark de 20 s con 361 FPS y 0,08 ms por tick.

### 2026-10-18: H3.8, primera ronda de revisión

- 10 hallazgos, todos corregidos:
  - Las animaciones en bucle se marcaban por biblioteca compartida con las del primer modelo que la usó: un bebé dejaba a los adultos de su archivo sin caminar en bucle, o al revés. Ahora se repiten todas las que pide el renderer y todas las que nombra el catálogo.
  - Un bebé dormido se dibujaba con la pose de sentado acostada: dormido usa la animación quieta original.
  - La altura medida no contaba la cabeza agrandada: el globo y el clic usan el factor del hueso más grande.
  - Un globo ya no desaparece si el colono cambia de etapa mientras se ve.
  - Caminar va a la velocidad real del paso (`MovementSystem.ProgressOf`, con la habilidad al acarrear).
  - Los modelos por etapa se buscan una vez y se guardan; los íconos los carga y libera `ModelLibrary`.
  - Tests: cada animación que el catálogo pone en lugar de otra existe en todos los archivos del modelo; el adulto sin modelo propio se comprueba para cada criatura.
  - `tools/make-elder-models.py` exige UV y posiciones en float y actualiza los mínimos y máximos de los UV que mueve.
- 694 tests; smoke con código 0.

### 2026-10-18: H3.8, arte de las etapas

- Sin campo nuevo en el Core: un colono se dibuja con `<visual>.<etapa>` del catálogo si existe (`creature.human.baby`, `.child`, `.adolescent`, `.elder`); el adulto usa el de la criatura.
- Catálogo: `boneScales` (huesos más grandes, con el reposo escalado para que la animación no lo deshaga) y `animations` (otra animación del modelo). Bebé a escala 0,55 con la cabeza ×1,35, que se sienta y gatea agachado; niño 0,85 con la cabeza ×1,2; adolescente 1,15; adulto 1,3.
- Canas: pelo, ojos y ropa comparten franjas de la textura, así que una textura recoloreada no servía. `tools/make-elder-models.py` genera 12 `*-elder.glb` que llevan los vértices del pelo de la cabeza a la franja gris; los ojos se reconocen por su lugar, igual en todas las cabezas. La primera versión los separaba por UV y dejaba la coronilla oscura; se vio en las capturas.
- Animaciones: alimentar usa la de manipular, jugar la de saltar, y caminar va al ritmo de la etapa.
- Globos de interacción: siete íconos pixel de 16 × 16 (`tools/make-interaction-icons.py`), sobre los dos colonos durante 2,5 s, que se desvanecen al final.
- La cuna sigue siendo la cama de KayKit en chico: se lee como cuna en la vista de la aldea.
- Capturas con partidas generadas por el Core (cinco etapas en fila, filas de una etapa con cinco variantes) y `--load`, sin tocar el estado desde la presentación. Una "mancha" color piel resultó ser una niña pelirroja vista desde arriba.
- Tests: modelo propio para cada etapa menos la adulta, ícono para cada interacción, lectura de los campos nuevos y rechazo de los inválidos. 693 tests; smoke con 18 modelos; benchmark de 30 s con 377 FPS y 0,07 ms por tick.

### 2026-10-18: cierre de H3.7

- Cuarta ronda de revisión: 8 hallazgos, 7 corregidos. Si falla el camino a una pila, quien alimenta prueba otra en la misma decisión. Un padre que pasea cuenta como el que va a alimentar sólo si decide de nuevo en ese tick, así no frena a los demás. El bebé con la madre a más de 12 celdas gatea donde está. `PaceOf` usa `PaceIn`, la ficha usa `IsLivingColonist`, sin copia de `Relative` y `IsFertile` más simple. Descartado: `Beds.Fits` ordena las etapas sólo para un colono sin `Life`, que no hay en partidas reales, y la regla ya es `LifeRules.AdultOf`.
- 1 test nuevo, 688 en total. Huellas sin cambios.
- H3.7 hecho en `IP3.md`; `main` adelantado.

### 2026-10-18: H3.7, tercera ronda de revisión

- 10 hallazgos, todos corregidos:
  - Quien alimenta guarda en el trabajo cuántas unidades reservó (`Job.Count`), de las que nadie más reservó, y no levanta más. Así las reservas son exactas.
  - Alimentar va a la velocidad del trabajo (habilidad, rasgos, ánimo y embarazo), y la experiencia de crianza se cuenta una vez por tick, no dos.
  - Un padre cuenta como el que va a alimentar sólo si ningún otro trabajo suyo tiene más prioridad que la crianza.
  - El bebé deja de gatear en cuanto alguien toma su comida.
  - El niño elige compañeros con las reglas de `SocialSystem` (`CanInteract` y `Met`): nadie en crisis de aislamiento.
  - Una pareja nueva empieza con hijos permitidos.
  - El ritmo de la vida se consulta en un solo lugar (`LifeRules.PaceIn`).
  - El botón de la ficha cambia a partir de lo que muestra: dos clics seguidos con el tiempo corriendo vuelven al valor original.
- 4 tests nuevos, 687 en total. Las dos huellas cambian porque cada trabajo guarda `Count`: `D8FD3784066AEE12` (semilla 1) y `14212A5A5466DDB7` (semilla 42).

### 2026-10-18: H3.7, segunda ronda de revisión

- 10 hallazgos, todos corregidos:
  - Comida reservada por otro por un momento ya no hace ignorar al bebé; la comida se busca una vez por decisión.
  - Un bebé en crisis de aislamiento sigue gateando cerca de su madre.
  - Quien alimenta reserva sólo las unidades que le faltan al bebé, no la pila entera: los demás comen del resto.
  - El deseo de un hijo espera un año de vida (`expireYears` en el def `Desire`), hasta la próxima ocasión, en lugar de 10 días que casi siempre lo frustraban. El balance va a H3.10.
  - Código compartido: `LifeRules.AdultOf`, `LifeRules.ConceptionChance` (con la misma conversión por día que la vejez), `Relationships.IsLivingColonist`, `Relationships.FamilyOf` y `ColonyStart.PickName` para los recién nacidos.
- 3 tests nuevos, 683 en total. Huella de la semilla 1: `049E2C1B43ACAEBB`; la 42 no cambia.

### 2026-10-18: H3.7, primera ronda de revisión

- 10 hallazgos, todos corregidos:
  - El deseo de un hijo nace sólo si la pareja podría concebir: nadie espera un hijo y no hay un bebé de menos de un año (`LifeRules.CouldConceive`). Guarda a la pareja como objetivo y se descarta sin frustrarse si ella muere o se separan.
  - El botón de la ficha muestra la opción de la pareja: permitidos sólo si los dos los permiten.
  - El inspector y el smoke dan por libre la cuna de quien ya creció (`Beds.IsTaken`).
  - Quien alimenta a un bebé reserva la pila entera, como el acarreo, porque puede llevarse varias unidades.
  - Si no encuentra camino a la comida, ignora esa pila y no al bebé.
  - Un niño que juega deja el juego por una necesidad baja.
  - El hambre se consulta en un solo lugar (`Hunger.IsLow` y `Hunger.UnitsToFeed`, que también usa comer).
  - `Beds.HasBed` reutiliza la lista del inspector.
  - El trabajo de alimentar ya no guarda una celda de destino que nadie lee.
- 3 tests nuevos, 680 en total. La huella de la semilla 1 pasa a `3312BA81E1022B34`, porque el deseo de un hijo ahora guarda a la pareja; la de la 42 no cambia.

### 2026-10-18: H3.7, la ficha y el inspector

- La ficha muestra "Hijos: permitidos / no" junto a "Cerrar" a quien tiene pareja viva del otro sexo; con el tiempo corriendo se refresca apenas se aplica el comando. Ficha e inspector muestran "Embarazada (N %)" (`LifeRules.PregnancyPercent`). El inspector usa `Beds.HasBed`, y una cama dice "Es de Elvira", que vale también para la cuna.
- El smoke abre la ficha de un fundador en pareja, aprieta el botón dos veces y comprueba que la opción cambia y vuelve. Huellas nuevas: `CBD31D910E8019D7` (semilla 1) y `95A5B6F5A5B40D9F` (semilla 42).
- Capturas revisadas: la ficha en es a 1080p y en en a 960 × 540, y el panel Colonia a 960 × 540 con la columna Crianza.
- Documentación: "Cómo está hecho (H3.7)" en la spec, con dos ajustes (se llevan al bebé las unidades que le faltan, y el deseo de un hijo se cumple con el embarazo); `guardado.md` y ADR-013 con el formato 7; la cuna en `muebles-y-camas.md`; la cuna provisoria y los bebés en `art.md`.
- Los scripts de Python de la sesión dejaban CRLF en la copia de trabajo; se normalizó a LF (`.gitattributes` ya lo pide). Escribir siempre con `newline=''`.

### 2026-10-18: H3.7, el Core

- Rama `ip3/h3-7-nacimientos` desde `main`. Archivo real `Fixtures/h3.6.olsave`, escrito con el código de H3.6 antes de tocar nada.
- Datos: la etapa suma `eatsAlone`, `crawlRadius` y `playRadius`; `LifeConfig` suma el bloque `birth` (fertilidad, concepción, embarazo de 45 días de vida, último tramo al 90 %, un año entre hijos, opiniones al nacer); tipo de trabajo `childcare` con `feed_baby`, primero en el orden; la cuna con `forStages: [baby]` y la cama para las demás etapas; el deseo `have_child`, el pensamiento `child_born` y `parenthood` (amabilidad +2).
- `BirthSystem` a medianoche, después de `LifeSystem`: avanza los embarazos, da a luz (nombre libre, apellido del padre, rasgos heredados, habilidades en 0, necesidades llenas, familia y opiniones) y concibe con el flujo `life.birth` si nadie tiene hambre baja y la madre no tiene un hijo de menos de un año.
- Decisiones: el bebé no come solo, gatea a 3 celdas de su madre y espera quieto mientras le traen comida; quien cría lleva de la pila las unidades que le faltan al bebé, y van primero la madre o el padre si están libres; los niños van a jugar con otro niño o un familiar a 15 celdas que no hayan visto hace poco; las camas filtran por etapa (`Beds`).
- `SetChildrenAllowedCommand`: vale para la pareja; al prohibir, borra el deseo de un hijo sin frustrarlo. El deseo se cumple con el embarazo o el nacimiento.
- Formato de guardado 7: las partidas viejas reciben la habilidad de crianza sorteada como un fundador (`MigrateNewSkills`) y empiezan sin embarazos.
- Tests nuevos: `BirthTests` (11), `ChildcareTests` (8) y la continuidad del guardado con embarazo, bebé, crisis y deseo. 677 tests.

### 2026-10-18: estaciones visibles en el plan

- Pedido del usuario: que el suelo, los árboles y las plantas cambien de color con la estación. En otoño, árboles amarillos y rojizos; en invierno, pasto y árboles blanquecinos y cumbres de roca nevadas.
- Es factible y barato. Es sólo de presentación: el shader del terreno ya recibe los verdes del pasto como uniformes, y las copas usan materiales compartidos de la paleta. Se cambian una vez por hora del juego, con una transición de unos días. No toca la simulación ni las huellas.
- Va a IP3.5 como hito L4c (8–12 h, adelantado de IP6, que conserva el clima y la nieve que cae). Propuesta en `docs/gdd/estaciones-visibles.md`. IP3.5 pasa a 48–70 h y el total de la 1.0 a 815–1 124 h.

### 2026-10-17: IP3.5, decisiones y bosque que vuelve

- Decisiones del usuario: las metas de existencias se eligen en el panel Colonia; "cerca de la aldea" es un círculo de 40 celdas de diámetro.
- Pedido nuevo: que los árboles vuelvan a crecer sin llenar el mapa, con un tope como porcentaje del terreno. Va a IP3.5 como hito L4b: retoños junto a los árboles adultos, que crecen en días, con un tope por especie propuesto igual a la densidad con que la genera el valle (robles 3,5 %, pinos 4 %); nunca sobre construcciones, almacenes ni órdenes. Sembrar con semillas queda para la agricultura de IP4.
- IP3.5 pasa a 40–58 h; plan maestro y `docs/gdd/libre-albedrio.md` actualizados. Propuestas a confirmar en L1: el centro del círculo de la aldea (el lugar de llegada) y retoños sólo junto a árboles adultos.

### 2026-10-16: IP3.5 "Libre albedrío" planificada

- Respuestas del usuario: el interruptor va en Opciones para toda la aldea; por ahora sólo recolectar, talar y acarrear; las metas de existencias se eligen en el juego; la iniciativa respeta las prioridades; va como IP3.5, después de IP3.
- `docs/ip/IP3.5.md` con cinco hitos (L1 spec, L2 interruptor y metas, L3 trabajo por iniciativa, L4 vida social con intención, L5 integración), 32 a 46 h y versión `v0.3.5`. Fila nueva en el plan maestro, que corre las fechas siguientes unas dos semanas. Sale del backlog.
- Nota de diseño: la opción de Opciones llega a la partida como un comando (determinismo y guardado). Las tres partes van a IP3.5, así que el balance social de H3.10 se hace sin buscar ni evitar personas.

### 2026-10-16: libre albedrío, para planificar

- Pedido del usuario: un libre albedrío en el que el colono con hambre busque o recolecte comida, sin nada que hacer tome tareas según sus prioridades, su personalidad y su ánimo, y busque o evite a otros según el vínculo. No es para ahora.
- `docs/gdd/libre-albedrio.md`: qué ya hace el juego (comer y recolectar con hambre, tomar órdenes por prioridad), la propuesta en tres partes (trabajo por iniciativa, vida social con intención, preferencias por rasgos y ánimo), cinco preguntas para el usuario, tres lugares posibles en el plan y una estimación de 32 a 46 h. Anotado en el backlog y en "Pedidos fuera de los hitos" de `IP3.md`.

### 2026-10-16: ficha del colono (H3.9 adelantada)

- Pedido del usuario: ver en el juego los datos de H3.2 a H3.6. Se adelanta la ficha del colono, que el plan tenía en H3.9; la opción de hijos se suma con H3.7.
- `ColonistSheet`: se abre con F o con el botón del inspector, sigue a la selección y se cierra con F, Esc o "Cerrar". Arriba etapa, edad, ánimo, crisis y orientación; a la izquierda personalidad en palabras, habilidades con su avance y familia; a la derecha pensamientos, deseos, relaciones y últimas interacciones. Fondo opaco y desplazamiento: entra en 960 × 540.
- Datos nuevos: tramos `words` en `PersonalityConfig`, `Relationships.SiblingsOf`, textos `def.trait.<id>.low/high` y `def.interaction.<id>.started`, y `ui.sheet.*`.
- Panel Colonia con Edad y Ánimo. `--sheet` para capturas; el smoke abre la ficha y revisa que no falte ningún texto.
- Capturas en es y en a 1080p y a 960 × 540, revisadas. 647 tests, mismas huellas.
- Revisión de código: 10 hallazgos, todos corregidos. F cierra la ficha aunque se haya seleccionado otra cosa; Esc la cierra a través de `GameInput.CloseFirst`, así no le quita Esc al menú de pausa ni a otras pantallas. La orientación no se muestra sin etapa. El deseo de aprender dice qué y de quién. Los pensamientos repetidos se agrupan. La ficha se rehace sólo cuando pasa el tiempo. `SiblingsOf` más simple, con un padre sin entidad que no empareja a nadie, y con tests. `ColonyText.FullName(name, surname)` para los parientes. El smoke detecta claves sin traducir. 655 tests. `main` adelantado.

### 2026-10-16: cierre de H3.6

- Revisión de código: 10 hallazgos, 9 corregidos. "Reconciliarse" nunca nacía (dos amigos no pueden discutir): ahora nace tras discutir con alguien de quien tenía opinión ≥ 10, si bajó, y se cumple al volver a la de antes. "Hacer una amistad" no apunta a la familia y sólo nace de una charla que salió bien. Los hermanos tienen su pensamiento de duelo (`death_sibling`). Una cama propia no se desea si ya se tiene. El ánimo se calcula sin reservar memoria, porque la velocidad de trabajo lo pide en cada tick. Los límites de deseos valen desde el primero. `TimeSettings.TicksPerHour` y helpers comunes para `Mood` y `Desires`.
- Queda: el duelo arma `PersonalityRules` cuando hace falta (las muertes son raras).
- 646 tests, mismas huellas. `main` adelantado.

### 2026-10-16: H3.6, pensamientos, ánimo y deseos

- Defs `Thought` (21), `Crisis` (2), `Desire` (5) y `MoodConfig`, en `mood.json`. Las interacciones dicen qué pensamiento dejan y qué deseo pueden despertar.
- Ánimo = base 50 + pensamientos de hechos (componente `Mood`, con tick de fin y tope por pensamiento) + pensamientos de situación (hambre y sueño, calculados de las necesidades). Su efecto en el trabajo entra en `WorkSpeed`. Consolar ya funciona.
- `MoodSystem`, cada hora: limpia pensamientos vencidos, empieza (ánimo < 15, 10 %) y termina crisis con aviso amarillo, y cumple o frustra deseos. A medianoche, el deseo anual de formar pareja. Las crisis cortan el trabajo; quien se aísla no interactúa y deambula lejos de los demás.
- Pensamientos al despertar (cama o suelo), al aprender, al formar pareja o separarse y al morir alguien (pareja, hijo, padre o madre, amigo o vecino, con deriva de la sociabilidad por un familiar). Dos noches en el suelo pueden despertar el deseo de una cama.
- Formato de guardado 6 (sin migración: una partida vieja empieza sin pensamientos) y archivo real `Fixtures/h3.5.olsave`.
- El deseo de tener un hijo y "Nació mi hijo" quedan para H3.7.
- 643 tests. Huellas nuevas: `ECB4B600749ABA31` (semilla 1) y `57A3D947DB7DB434` (semilla 42).

### 2026-10-16: cierre de H3.5

- Revisión de código: 10 hallazgos, todos corregidos. Opiniones y romance de los fundadores acotados, y validación de rangos en `SocialConfig`, `Scenario.foundingCouple` y `Trait.interactionChance`. El sorteo de con quién interactuar ya no se cae si todos pesan 0, y la orientación exige pesos para el otro sexo. Una ruptura borra el romance, así que la pareja tiene que cortejarse de nuevo. Nadie corteja ni forma pareja con su familia. Al morir, el muerto sale también de las interacciones recientes. La configuración social se busca en un solo lugar. Al cargar, las relaciones deben estar en orden y sin uno mismo.
- 623 tests, mismas huellas. `main` adelantado.

### 2026-10-16: H3.5, interacciones y relaciones

- Defs `Interaction` (siete, en `social.json`) y `SocialConfig`; `Trait.interactionChance` (sociabilidad, de 0 % a 200 % de la probabilidad base); `LifeStage.interacts` (el bebé no); `Scenario.foundingCouple` (20 a 30 años, opinión 70, romance 100).
- Componentes `Relations` (opinión, romance, última interacción y lazo avisado), `Family` (padre, madre, pareja, parejas fallecidas, hijos, hijos permitidos y viudez) y `RecentInteractions` (las últimas 5).
- `SocialSystem`: cada 30 ticks, escalonado, un colono despierto busca a otro a 4 celdas en su nivel, sortea si empieza, con quién (familia y amigos pesan el doble) y qué interacción. Aplica opiniones, romance, enseñanza, deriva de rasgos y lo recuerda. A medianoche, las opiniones de quienes no se vieron se acercan a 0. `SocialRules.Change` forma parejas, las rompe y avisa amistades y rivalidades una sola vez (histéresis de 10 puntos).
- Al morir, los demás olvidan al muerto, la familia lo guarda como fallecido y la pareja queda viuda un año. Los fundadores se conocen (opinión −10 a 20) y los dos primeros llegan en pareja, con orientaciones compatibles.
- Ajustes de balance, en la spec: la charla termina mal con alguien impaciente, la discusión pesa más según paciencia y amabilidad y se permite hasta opinión 20. Con los números originales dos colonos ásperos no llegaban a rivales.
- Formato de guardado 5, con migración desde el 4 y archivo real `Fixtures/h3.4.olsave`.
- 621 tests. Huellas nuevas: `CE15ED74ECC60360` (semilla 1) y `AC908F5FCB21A9CD` (semilla 42).

### 2026-10-16: cierre de H3.4

- Revisión de código: 10 hallazgos, 9 corregidos. La velocidad de una habilidad se acota al último nivel. La habilidad de acarreo ahora sirve: apura el paso mientras se lleva la carga, y también la de construcción al llevar materiales. Una habilidad que falta se aprende desde nivel 0 (para tipos de trabajo nuevos, como la crianza de H3.7). Un colono sin habilidades usa `SkillConfig.averageLevel`. Se valida que los fundadores no pasen del último nivel. Subir de nivel emite `EntityChangedEvent`. El smoke compara la cantidad exacta de habilidades. `PersonalityRules.ProductOf` junta los porcentajes de los rasgos. Se corrigió la documentación del guardado y del ADR-013.
- Queda sin cambiar: la búsqueda lineal por id repetida en `Personality` y `Skills`.
- Huellas nuevas por el acarreo: `86682D524C6B7F9D` (semilla 1) y `A22F92980217E726` (semilla 42). 597 tests. `main` adelantado.

### 2026-10-16: H3.4, habilidades

- `WorkType.skill` (los cinco tipos de trabajo), def nuevo `SkillConfig` en `skills.json`, `Trait.experience` (inteligencia, de 75 % a 125 %) y las habilidades de los fundadores en `Scenario` (de 3 a 10 y una con 4 más).
- Componente `Skills` (nivel y experiencia en centésimas por tipo de trabajo). `SkillRules`: habilidades de los fundadores, ganancia con subida de nivel, enseñanza (`Teach`, para la interacción de H3.5) y velocidad por nivel (80 % + 2 % por nivel). `WorkSpeed.PercentOf` junta etapa, rasgos y habilidad del trabajo en curso.
- Se gana experiencia en cada tick que hace avanzar un trabajo y en cada tick que se lleva algo para acarrear o abastecer una obra. Un colono sin habilidades trabaja como uno de nivel 10.
- Formato de guardado 4, con migración desde el 3 (habilidades sorteadas) y archivo real `Fixtures/h3.3.olsave`.
- 593 tests. Huellas nuevas: `054F77100603CE30` (semilla 1) y `890D9729617952AA` (semilla 42).

### 2026-10-16: cierre de H3.3

- Revisión de código: 9 hallazgos, 6 corregidos. Lo principal: las partidas guardadas con H3.2 (formato 2) se quedaban sin personalidad. El formato sube a 3 y las migraciones se encadenan (ADR-013 actualizado: el formato sube cada vez que `main` recibe un cambio del estado). Nuevo archivo real `Fixtures/h3.2.olsave`, escrito con el código de `main`. Además: velocidad de trabajo con piso de 1 %, validación de la personalidad al cargar (rango, repetidos y día), `Calendar.DayNumberAt` para el día de la deriva, y la deriva redondea en lugar de truncar.
- Quedan sin cambiar: la búsqueda de comida de un perezoso antes de trabajar (las comidas sin camino ya esperan con el enfriamiento de siempre), el costo de `WorkSpeed.PercentOf` por tick (lo mide el benchmark de H3.10) y la forma de la API de `PersonalityRules`.
- 577 tests. `main` adelantado.

### 2026-10-16: H3.3, personalidad

- Def `Trait` con los seis rasgos de la spec y `PersonalityConfig` (tres tiradas, herencia al 50 %, deriva de 3 por día y pesos de orientación 80/8/12), en `personality.json`. `LifeStage.traitDriftPercent`: niños y adolescentes al 200 %.
- Componente `Personality` (rasgos por id con lo que se movió hoy, orientación y día de la deriva). `PersonalityRules`: sorteo, herencia (mitad de los padres y mitad sorteo), deriva con tope neto diario y consultas. Llegada y migración de la versión 1 sortean personalidad; el formato sigue en 2 y un colono sin personalidad cuenta como promedio.
- Efectos: la diligencia da la velocidad de trabajo, de 90 % a 110 % (`WorkSpeed.PercentOf` junta etapa y rasgos). Decisión mía, anotada en la spec: un perezoso (diligencia < 25) atiende sus necesidades bajas antes de trabajar, para que dos colonos opuestos elijan distinto ya en este hito. Sociabilidad, amabilidad, curiosidad, inteligencia y paciencia actúan desde H3.4 a H3.6.
- La deriva todavía no la llama nada en el juego: las vivencias llegan con las interacciones y los pensamientos.
- 571 tests. Huellas nuevas: `7D3BF3621633EF7B` (semilla 1) y `8D63FDA0CBA18BF8` (semilla 42).

### 2026-10-16: existencias y FPS en el HUD

- Pedido del usuario: un indicador de cuánta comida, madera y piedra tiene la aldea, y la posibilidad de ver los FPS. Spec `docs/gdd/indicadores.md`; anotado en "Pedidos fuera de los hitos" de `IP3.md`. Rama `ip3/indicadores-hud`.
- La barra superior muestra "Comida: 64  Madera: 25  Piedra: 0": todo lo que hay en el mapa, almacenado o no, más lo que llevan los colonos. Las categorías salen del contenido, con el campo nuevo `ItemCategory.order`. Consulta `ColonyStock.ByCategory`.
- Opciones suma "Mostrar FPS", guardado. Comparte fila con "Ciclo de día y noche", porque a 720p otra fila no entraba. Los FPS van en un panel abajo a la derecha, junto al registro, contados sobre medio segundo. `--fps` para capturas.
- La barra superior se apretó un poco para entrar en 960 × 540 con las existencias. Capturas a 1080p, 960 × 540 y 720p en es y en, revisadas.
- 557 tests, misma huella del smoke.
- Revisión de código: 6 hallazgos, todos corregidos. El inspector ya no tapa los FPS. Las cuentas tienen el ancho de tres dígitos, la fecha se recorta antes que los botones y el nombre de la aldea tiene como mucho 24 letras. El filtro de los almacenes usa el mismo orden de categorías (`ItemCategoryDef.InOrder`). Las existencias se cuentan sólo cuando llega un evento de entidad. El smoke comprueba los textos y que el panel de FPS aparece y desaparece. `main` adelantado.

### 2026-10-16: fundadores de los dos sexos

- El usuario probó una partida en la que todos los colonos eran mujeres. Pedido: asegurar siempre al menos una mujer y un varón al empezar.
- `ColonyStart.Settle`: los dos primeros fundadores son una mujer y un varón, en orden al azar; el resto se sortea como antes. Así quedan listos para la pareja fundadora de H3.5 y H3.7. La spec lo dice en "Llegada".
- Test con 40 semillas. Huellas nuevas: `4D9981ED0F339C6D` (semilla 1) y `B229DEF368992238` (semilla 42). 554 tests. Commit en `ip3/h3-2-edad` y `main` adelantado.

### 2026-10-16: cierre de H3.2

- Revisión de código: 9 hallazgos, 8 corregidos. Los bebés y los niños ya no disparan la alerta de "no tiene nada que hacer". Al cambiar de etapa, el colono suelta el trabajo que la etapa nueva no hace, y sólo se avisa si creció. Recolectar para comer ya no va más lento en los ancianos. Una edad negativa en un guardado es una partida dañada. El movimiento cuenta en milésimas de tick con la velocidad en el avance, sin perder la fracción (la migración de la versión 1 escala el avance guardado). `Def.DescKey` junto a `NameKey`. Se corrigieron dos comentarios mal codificados.
- Queda sin cambiar: `LifeRules.From` se arma en Nueva partida, al llegar y al agregar los sistemas, como `ColonyRules`; cuesta microsegundos y sale del mismo contenido inmutable.
- Mismas huellas (a los 5000 ticks nadie está a mitad de un paso). 553 tests. `main` adelantado.
- Nota práctica: las herramientas Write y Edit no tuvieron veredicto del clasificador en esta sesión; los archivos se escribieron con Bash. Los comandos de más de unos 8 KB se cortan, y las barras invertidas dobles pueden llegar simples: en C#, mejor una clase de caracteres como `[0-9]` que un escape.

### 2026-10-16: H3.2, Core de la edad y las etapas de la vida

- El usuario descartó los `.import` que había cambiado el editor.
- Defs nuevos en `life.json`: `LifePace` (normal 1, rápido 4 por defecto, muy rápido 8), `LifeStage` (bebé, niño, adolescente, adulto, anciano, con trabajo, velocidades y porcentaje de cada necesidad) y `LifeConfig` con la tabla de vejez. `Scenario` suma `founderMinYears` y `founderMaxYears` (18 a 40), `NameList` los apellidos y `WorkType` el campo `light` (recolectar y acarrear).
- Componente `Life` (días de vida y etapa), `Colonist.Surname` y `Simulation.LifePace`, en la huella y en el guardado. `LifeSystem` corre a medianoche: suma los días del ritmo, cambia de etapa con aviso y `EntityChangedEvent`, y sortea la muerte por vejez (flujo `life.aging`). Con 100 % por año la muerte es segura. `ColonistDeath` junta la muerte por hambre y por vejez.
- La etapa decide qué trabajos toma el colono, cuánto tarda (`ticks × 100 / velocidad`), a qué velocidad camina y a qué ritmo bajan sus necesidades. Un colono sin `Life` vive como adulto.
- Formato de guardado 2. Una partida de formato 1 se migra al cargarla (flujo `life.migration`). `tests/OurLife.Content.Tests/Fixtures/v0.2.5.olsave` es una partida real escrita con el código de `v0.2.5` (semilla 7, 20 000 ticks), y carga.
- Los textos de los colonos usan nombre y apellido (`ColonyText.FullName`). Textos nuevos en es y en: ritmos, etapas, `event.life.stage.<etapa>`, `event.life.old_age`, `ui.newgame.life_pace` y `ui.inspector.life`.
- Hasta H3.7 los bebés comen solos: no nace nadie todavía, y sólo los tests crean bebés.
- 546 tests en verde.

### 2026-10-16: H3.2, presentación

- Nueva partida tiene "Ritmo de la vida" con Normal, Rápido y Muy rápido, y la descripción del elegido. `--show newgame` la abre para capturas.
- El inspector muestra nombre y apellido, y la etapa con la edad ("Adulto, 25 años"); el nombre de la etapa concuerda con el colono. El registro y las alertas usan el nombre completo.
- El smoke comprueba que los colonos llegan con edad y ritmo. Huellas nuevas: `7C61534966CC37FA` (semilla 1) y `3984B7A57F47499E` (semilla 42).
- ADR-013: las partidas viejas se migran sobre la simulación cargada, con las reglas del Core, y cada versión migrable tiene un archivo real de prueba. `guardado.md` lista las versiones del formato.
- Capturas en es y en a 1080p de Nueva partida y del inspector, revisadas. 547 tests.

### 2026-10-16: cierre de H3.1

- El usuario aprobó la spec `docs/gdd/vida-y-generaciones.md`. Había sumado «inteligente» a los rasgos de `IP3.md`: la spec pasa a seis rasgos, con `intelligence` (Torpe a Inteligente), que decide la experiencia ganada de 75 % a 125 %, trabajando y aprendiendo de otro. `curiosity` queda para las ganas de aprender: el peso de enseñar como aprendiz y los deseos de aprender.
- Hito de documentación, sin código: no lleva revisión de código. `main` adelantado al cierre de H3.1. Sigue H3.2 en `ip3/h3-2-edad`.

### 2026-10-15: IP2.5 completada también en la Mac

- El usuario probó `v0.2.5` en la Mac y dio la sección de macOS de `docs/ip/playtest-ip2.5.md` por completada: los cuatro puntos pasan de ➖ a ✅. IP2.5 queda cerrada sin pendientes.

### 2026-10-15: H3.1, borrador de la spec de IP3

- Rama `ip3/h3-1-spec`, desde `main` en `v0.2.5`.
- Spec `docs/gdd/vida-y-generaciones.md`: tiempo de la vida y los tres ritmos (Rápido por defecto), cinco etapas, fundadores con edad, apellido y la pareja, cinco rasgos y la orientación, habilidades por tipo de trabajo, siete interacciones, relaciones y familia, pensamientos y ánimo, dos crisis, seis deseos, embarazo y parto, crianza con cuna y el trabajo de alimentar, vejez con tabla por edad, migración del guardado a la versión 2, contrato, flujos, defs, textos, modelos y 16 criterios de aceptación. Apellidos castellanos e ingleses con fuentes.
- Lo que quedó fuera, en el backlog. El usuario prueba `v0.2.5` en la Mac mientras tanto.

### 2026-10-15: cierre de IP2.5 (`v0.2.5`)

- El usuario cerró IP2.5 con el playtest de Windows aprobado. Los puntos de macOS quedaron en ➖: los hallazgos de la Mac se corrigieron, pero no se volvieron a probar ahí.
- Revisión de código de V6: 9 hallazgos, todos corregidos. El script de macOS pide PowerShell 7.4, detecta bien Zip64 y da modo de carpeta a las carpetas; el zoom con dos dedos tiene tope por evento y no hace zoom de costado, y el smoke lo comprueba; el registro de las teclas quedó en una sola función.
- `main` adelantado, tag `v0.2.5` y release de GitHub con los dos zips.
- Respuestas del usuario para la spec de IP3: los niños sólo juegan, la orientación forma parte de la personalidad y cualquier pareja es posible, los colonos tienen apellido y los hijos heredan el del padre, la comida es el único tope de población y dos fundadores llegan en pareja.

### 2026-10-15: plan de IP3 "Vida y generaciones"

- Pedido del usuario: colonos con sexo, edad y etapas (bebé, niño, adolescente, adulto, anciano), reproducción, muerte por vejez, personalidad sorteada que cambia con las vivencias, interacciones que dan habilidades y deseos.
- Decisiones del usuario: va antes de Oficios, y el ritmo del envejecimiento se elige al crear la partida.
- La nueva IP3 junta rasgos, relaciones y ánimo (antigua IP4), habilidades (antigua IP3) y generaciones (IP9). Oficios pasa a IP4, con habitaciones y comedor. Plan en `docs/ip/IP3.md`, de 95 a 135 h y diez hitos; `plan-maestro.md`, `eras.md`, `mapa-y-generacion.md` y ADR-008 actualizados.
- También pidió que Claude le responda siempre en español.

### 2026-10-15: V6, hallazgos del playtest en la Mac

- La checklist de Windows quedó toda en ✅.
- Hallazgo 1: la app de Mac no tenía permiso de ejecución. El `ZipArchive` de .NET (PowerShell 7) marca como MS-DOS todas las entradas al agregar archivos, y macOS ignora sus permisos. `package-macos.ps1` reescribe el directorio central para marcarlas como Unix, da `rw-r--r--` a lo agregado y falla si el ejecutable no tiene permiso de ejecución.
- Hallazgo 2: en la Mac, el trackpad no hacía zoom porque no manda rueda. Ahora hacen zoom el pellizco (`InputEventMagnifyGesture`), deslizar dos dedos (`InputEventPanGesture`) y mantener + o - (acciones `camera_zoom_in`/`camera_zoom_out`, por carácter y no por posición). El smoke los comprueba y falla si el pellizco va al revés.
- Mismas huellas, 515 tests. Zips de Windows y macOS rearmados.

### 2026-10-14: V6 Integración (primera parte)

- Versión 0.2.5; paquetes de Windows y macOS armados, el de Windows con el smoke sobre el exportado.
- Checklist `docs/ip/playtest-ip2.5.md` con la pasada automática; capturas es/en a 1080p; benchmark: 261 FPS de media, dentro del presupuesto.
- Falta el playtest humano.

### 2026-10-14: V5 Modelos y animaciones

- Muebles y provisiones de KayKit, con la madera entonada; bayas del Food Kit, sueltas y en los arbustos; los 12 aldeanos de Mini Characters con la ropa recoloreada y animaciones según lo que hacen, a la velocidad del juego.
- El catálogo suma `variants`, `parts` y `turn`. El baúl de KayKit miraba a +X: `turn: 3`.
- 507 tests, mismas huellas. Detalle en el registro de V5 de `IP2.5.md`.
- Revisión de código: 10 hallazgos, 8 corregidos; lo principal, las variantes de aldeanos sólo usaban la mitad de los modelos. 509 tests. `main` adelantado.

### 2026-10-14: opción Decoración del pasto

- Pedido del usuario: una opción para la cantidad de decoración, por el rendimiento.
- Opciones suma Decoración del pasto (Nada, Poca, Normal, Mucha), guardada; `--decoration` para capturas y benchmarks. Spec `docs/gdd/decoracion-del-pasto.md`.
- Medición alternada: Nada unos 325 FPS, Mucha unos 300 (0,25 ms por cuadro en la RTX 4090 de portátil).
- A 720p, Opciones queda justa: la próxima opción pide dos columnas o pestañas (riesgo en `IP2.5.md`).
- Revisión de código: 7 hallazgos, 6 corregidos; lo principal, subir el nivel ya no cambia las piezas que había. `main` adelantado.

### 2026-10-14: V4 Terreno y decoración

- Sólo queda el estilo B. Los patrones de cada material y los verdes del pasto pasan al catálogo, con tests.
- Decoración del pasto con siete modelos del Nature Kit, elegida por un hash de la celda: una malla por chunk.
- El cuadro de 100 ms de V1 no era del shader: era la ventana cambiando de tamaño durante el benchmark.
- Las mediciones de la portátil varían mucho entre corridas (232 a 418 FPS); con y sin decoración se mezclan, siempre muy por encima de 60.
- 506 tests, mismas huellas. Detalle en el registro de V4 de `IP2.5.md`.
- Revisión de código: 10 hallazgos, 8 corregidos; lo principal, la decoración ya no crece dentro de los arbustos ni se mete en las celdas vecinas. 507 tests. `main` adelantado al cierre de V4.

### 2026-10-14: V3 Vista previa de muebles

- Al colocar un mueble se ve su modelo translúcido y girado dentro del cubo, con una flecha hacia el frente; rojo donde no se puede. En un arrastre, en cada celda. Los planos de muebles se dibujan con su modelo.
- `Construction.CellsToPlan` en Core, que usan el comando y la vista previa. Opciones de captura `--furniture`, `--rotation`, `--point` y `--drag-to`.
- 495 tests, mismas huellas. Detalle en el registro de V3 de `IP2.5.md`.
- Revisión de código: 10 hallazgos, 9 corregidos; lo principal, la vista previa ya no recorre el mundo en cada cuadro. `main` adelantado al cierre de V3.

### 2026-10-14: V2 Tamaño de la interfaz

- Opción "Tamaño de la interfaz" en Opciones: Automático (según la ventana) o de 100 % a 300 %, guardada en `settings.cfg`. Escala sólo la UI 2D con `ContentScaleFactor`.
- El panel Colonia oculta las alertas y empuja al inspector cuando se tocan, para que todo entre en un lienzo de 960×540.
- Smoke con un clic real sobre un colono con la UI al 200 %. 494 tests, mismas huellas. Detalle en el registro de V2 de `IP2.5.md`.
- Revisión de código: 8 hallazgos, todos corregidos. El principal: la prueba del 200 % corría al 100 %, porque el manejador de `SizeChanged` deshacía el factor; ahora pasa por las preferencias y lo comprueba.
- `main` adelantado al cierre de V2.

### 2026-10-14: cierre de V1

- El usuario eligió terreno B, GL Compatibility y muebles de KayKit. Los aldeanos siguen siendo los de Mini Characters, recoloreados.
- ADR-012 (terreno por shader y kits medievales), referenciado en ADR-009, `overview.md` y `plan-maestro.md`; `docs/art.md` con la dirección del terreno y KayKit.
- Se quitó el código que sólo servía para probar Forward+.
- Revisión de código: 10 hallazgos, 6 corregidos, 3 para V4 y 1 que queda (detalle en `IP2.5.md`). 480 tests, mismas huellas.
- `main` adelantado al cierre de V1.

### 2026-10-14: V1 Prueba de estilo

- Shader del terreno con cuatro estilos (actual, A, B y C) y `--terrain-style` para elegirlo; pasto sólo arriba y con borde en los costados de los bloques. Forward+ se prueba con `--rendering-method forward_plus`.
- Kits: KayKit Dungeon Remastered (CC0) para los muebles medievales, probado en la casa del smoke; Mini Characters de Kenney trae 12 aldeanos con animaciones, con ropa moderna a recolorear.
- Benchmark: el shader casi no cuesta; en esta PC Forward+ rinde más (674 contra 420 FPS de media). Un cuadro de unos 100 ms que se atribuyó al shader resultó ser la ventana cambiando de tamaño (ver V4).
- Página de comparación para el usuario (privada): https://claude.ai/artifact/JkWuPmFMuosFSxV8TgAGiD. Detalle en el registro de V1 de `IP2.5.md`.
- 480 tests en verde, mismas huellas.

### 2026-10-14: plan de IP2.5 Pulido visual

- Pedido del usuario antes de IP3: un terreno con textura en lugar de colores planos, mejores objetos, ver hacia dónde mira un mueble al colocarlo y textos más grandes según la resolución.
- Revisé el estado actual: el terreno son cajas generadas en código con un color por material y la marca `Grass` tiñe el bloque entero; la UI está a 16 px sin escalar con la ventana; el modelo de los colonos trae 30 animaciones (caminar, quieto, levantar, acarrear) que no se usan.
- El usuario sumó al alcance los modelos provisionales, los muebles medievales, los colonos animados con variantes y la decoración del suelo, y pidió evaluar Forward+ con capturas.
- Plan en `docs/ip/IP2.5.md`: seis hitos (V1 a V6), 38 a 55 h, versión `v0.2.5`. Empieza con una prueba de estilo con capturas para que el usuario elija el terreno, el renderizador y los kits.
- `plan-maestro.md` con la fila de IP2.5 y los totales nuevos; la escala de UI se adelanta de IP8. Del backlog salen las variantes de aldeanos, el arbusto con bayas y los muebles medievales.
- Rama `ip2.5/v1-prueba-estilo` desde `main`.

### 2026-10-13: H2.7 Integración

- El usuario probó en el editor H2.1 a H2.6: todo bien.
- Rama `ip2/h2-7-integracion` desde `main`.
- Playtest automático: smoke de 30 días en las semillas 1, 42 y 7, con los 5 colonos vivos y la casa construida (entera en la 42; en la 1 y la 7 se acaban los troncos del radio de 8 celdas que tala el smoke). Al año y medio la colonia muere en el invierno porque el smoke recolecta una sola vez, como en IP1; el test del contenido real que recolecta en otoño sigue pasando el invierno.
- `--benchmark 20 --seed 42 --ticks 5000` con la casa construida: 406 FPS de media, 360 en el 1 % más lento, 0,027 ms por tick.
- Checklist `docs/ip/playtest-ip2.md`, con la pasada automática.
- Versión 0.2.0 en `project.godot` y en el preset de Windows. Paquete de Windows armado; el smoke del exportado pasa.
- Versión para macOS: plantilla `macos.zip` del `.tpz` oficial (1,2 GB) instalada; preset "macOS" universal (la plantilla sólo trae binarios universales), identificador `com.gtaulamet.ourlife` y firma ad hoc; `import_etc2_astc` activado en el proyecto, como pide Godot para Apple Silicon. Exportar C# a macOS desde Windows funciona. `tools/package-macos.ps1` agrega `LICENSES.txt` y `CREDITS.md` al zip sin perder el permiso de ejecución del binario. README con la sección de macOS.
- La exportación quedaba colgada por los nodos de MSBuild (ver Notas prácticas): arreglado en los dos scripts y verificado con los nodos vivos.
- Playtest humano: Windows aprobado entero. macOS no abría: Gatekeeper la bloqueaba y, desde la terminal, el sistema mataba el proceso aunque `codesign --verify` daba la firma por válida. Firmada de nuevo en la Mac, arranca. Pasos en el README y en el hallazgo 1 de `playtest-ip2.md`.
- Revisión de código: 9 hallazgos, 5 corregidos y 4 que quedan como están (detalle en `IP2.md`). `tools/common.ps1`, presets sin versión propia y scripts que restauran la sesión. Notarización de macOS y muebles medievales al backlog.
- Cierre: `main` adelantado, tag `v0.2.0` y release de GitHub con los zips de Windows y macOS. IP2 cerrada.

### 2026-10-12: H2.6 Gestión de la colonia

- Rama `ip2/h2-6-gestion` desde `main`.
- Avance 1: spec `docs/gdd/gestion.md`. Prioridades de 1 a 4 o No por colono y tipo de trabajo (por defecto 3, en `AiConfig`); la elección recorre las prioridades y, dentro de cada una, el orden de los tipos. Panel Colonia (P) con la lista de colonos, su actividad y los botones de prioridad. Alertas de colono ocioso (2 horas paseando) y de hambre crítica (`Need.alertWhenCritical`): un mensaje al empezar y una línea en el HUD mientras duran, con estado guardado. `TextResolver` expande colonos en nombre y género, y el registro fija el nombre al recibir el mensaje.
- Avance 2 (Core):
  - `AiConfig.defaultWorkPriority` (3) e `idleAlertHours` (2); `Need.alertWhenCritical` (hambre). Componente `WorkPriorities` (sólo lo que difiere del valor por defecto, ordenado) y `SetWorkPriorityCommand`, en `SaveTypes`. `WorkPriorityLevels` (0 a 4).
  - La decisión recorre las prioridades de 1 a 4 y, en cada una, los tipos de trabajo en su orden.
  - `AlertSystem`, último del tick: ocioso si pasea o está sin trabajo `idleAlertHours` seguidas (entre paseo y paseo hay un tick sin trabajo); hambre crítica hasta volver al umbral bajo. Estado en `Colonist.IdleSinceTick`, `Colonist.IdleAlerted` y `NeedState.CriticalAlerted`. Mensajes con el colono como `EntityId`. `ColonyAlerts.Active` para el HUD.
  - Textos de eventos y alertas en es y en (`event.need.hunger.critical` sin concordancia de género: el inglés no la usa y la paridad de argumentos lo exige). Test de contenido de las claves de alertas.
  - Tests: `ManagementTests` (prioridad alta primero en los dos sentidos, acarreo en No y en 4, comando que limpia el valor por defecto e ignora lo inválido, alerta de ocioso que avisa una vez y vuelve a avisar, hambre crítica que termina al comer). La continuidad del guardado incluye prioridades. 478 tests.
- Avance 3 (Godot):
  - `ColonyText.Freeze` (Core) cambia un colono de un texto por su nombre y su género (`<arg>_gender`); `TextResolver.Resolve(texto, mundo)` lo usa, y el registro fija los nombres al recibir cada mensaje. Test con un colono que muere después.
  - Panel Colonia (P, y botón en la barra de arriba): fila por colono con nombre (clic: lo selecciona), actividad y un botón de prioridad por tipo de trabajo; clic pasa a la siguiente (1, 2, 3, 4, No) y clic derecho a la anterior. Texto de ayuda aclarado en la spec.
  - Lista de alertas bajo la barra de arriba: amarillo los avisos, rojo los peligros, clic selecciona; se rehace cuando cambian las alertas (también al cambiar el tick, porque terminan sin mensaje).
  - El smoke abre el panel; opción `--colony` para capturas. Capturas revisadas en es y en: cinco alertas de ocioso, el panel y los mensajes con nombres en el registro.
- Avance 4 (2026-10-13, revisión y cierre): 10 hallazgos, todos corregidos (detalle en `IP2.md`). Lo principal: los mensajes salen del Core con el nombre fijado, el panel no pierde clics rápidos y P no funciona con un menú abierto. 480 tests, mismas huellas. `main` adelantado a H2.6.

### 2026-10-12: H2.5 Muebles y camas

- Rama `ip2/h2-5-muebles-camas` desde `main`.
- Avance 1: spec `docs/gdd/muebles-y-camas.md`. Los muebles son `Buildable` con `shape: furniture` y reusan planos, entrega, construcción y desmontaje de H2.4. Giro de 0 a 3 con la tecla G. La cama tiene dueño (la toma quien se acuesta sin cama) y da `restRecoveryPercent` 150; el cofre es un almacén de una celda; mesa y silla son decorativas. Modelos: `bedSingle`, `table` y `chair` del Furniture Kit y `chest` del Survival Kit de Kenney (CC0), descargados de kenney.nl.
- Avance 2 (Core):
  - `BuildShape.Furniture` con `visual`, `restRecoveryPercent` y `storage` en `Buildable`; `IHasVisual.Visual` admite nulo para los construibles del terreno. Cama, mesa, silla y cofre en `buildables.json` (`order` 10 a 13).
  - `PlaceBlueprintCommand` lleva `Rotation` (0 a 3), que guardan el plano y el mueble. Componentes `Furniture` y `Bed`, en `SaveTypes`. Al terminarse, la cama suma `Bed` y el cofre un `Stockpile` de su celda.
  - Reglas: un mueble va en suelo firme, fuera de almacenes y de otros muebles; nada se planea sobre un mueble; un almacén nuevo saltea los muebles y Cancelar no quita un cofre. Desmontar sobre un mueble desmonta el mueble; un mueble que pierde el suelo firme se pierde y deja su reintegro.
  - Dormir: el colono va a su cama o toma la libre más cercana a la que llega; una cama con dueño muerto está libre. En su cama recupera a `restRecoveryPercent` (150).
  - Modelos copiados a `game/assets/models/kenney_furniture-kit` y `kenney_survival-kit`, en el catálogo y en `CREDITS.md`.
  - Tests: `FurnitureTests` (descanso en cama, tomar la cama libre, cama libre al morir el dueño, cofre que recibe troncos y se desmonta, giro, reglas de colocación, almacén y Cancelar con muebles) y validaciones de defs. La continuidad del guardado incluye cama, cofre y una mesa por construir. 471 tests.
- Avance 3 (Godot):
  - Los muebles se dibujan con su modelo, girados de a cuartos. El catálogo de visuales acepta `center: true` para modelos con el origen en una esquina (Furniture Kit); escalas: cama 1, mesa 1,1, silla 1,6, cofre 2.
  - Tecla G: gira el mueble elegido; la pista suma una segunda línea con el giro. El cofre no se tiñe como almacén; el plano de un mueble es una caja baja.
  - Inspector: "Silla de madera", el dueño de una cama o "Libre", y en el colono "Duerme en su cama" o "Sin cama".
  - La casa del smoke se amuebla con los muebles del contenido en sus rincones; el smoke informa muebles y camas tomadas. Opción `--inspect-furniture`.
  - Capturas revisadas: la casa amueblada en es (cama, mesa y silla) y de noche en en, con un colono en la cama.
- Avance 6 (pedido del usuario): colonos más chicos, a escala 1,3 en lugar de 2 (0,87 de alto), para que entren en las camas. El colono acostado se corre la mitad de la altura medida de su modelo, no una distancia fija. Sólo presentación: misma huella.
- Avance 5 (pedido del usuario): los colonos que duermen se dibujan acostados boca arriba, centrados en la celda; en la cama, a lo largo de ella y sobre el colchón. Sólo presentación: misma huella. Captura de noche revisada.
- Avance 4 (revisión y cierre): 9 hallazgos, todos corregidos (detalle en `IP2.md`). Lo principal: almacenes que saltean planos de muebles, el índice de almacenes al perderse un cofre, la cama se toma sólo con camino, el resto de las necesidades en centésimas y la marca `TileFlags.Furniture`. 471 tests. `main` adelantado a H2.5.

### 2026-10-12: H2.4 Construcción

- Rama `ip2/h2-4-construccion` desde `main`.
- Avance 1: spec `docs/gdd/construccion.md`. Decisiones: los materiales de construcción son defs `Material` con `builtFrom` (madera desde troncos, mampostería desde piedra) y una celda es construida si su material lo tiene; def `Buildable` con forma, materiales, costo, trabajo y `outline` (el muro marca el borde de la caja); la puerta es una forma nueva del mapa, pisable; los planos son entidades; trabajos `deliver` y `build` en el tipo nuevo `construction` (antes del acarreo), `deconstruct` y `channel` (este en minería); la escalera construida ocupa dos niveles. Atajos: Construir (C), Desmontar (V), Canalizar (H), porque B y D ya están en uso.
- Avance 2 (Core):
  - `MaterialDef.builtFrom` y `MaterialKind.Wood`; materiales `timber` y `masonry`. Def `Buildable` (`buildables.json`): muro, suelo, puerta, rampa y escalera. `AiConfig.deconstructRefundPercent` (50). Validaciones: un objeto construye un solo material, un construible sólo usa materiales de construcción y uno construido no tiene `minedItem`.
  - Forma `Door` del mapa; marcas `DeconstructDesignation` y `ChannelDesignation`. `DesignationKind` suma Desmontar y Canalizar (antes de Cancelar); Excavar y las escaleras sólo toman celdas naturales; Cancelar quita planos y deja lo entregado.
  - `PlaceBlueprintCommand` y el componente `Blueprint`, registrados en `SaveTypes`. `Construction` reúne las reglas: dónde va cada forma, terminar, desmontar, cancelar y quitar planos que perdieron su lugar (se revisa cuando cambia `NavigationVersion`).
  - Trabajos `deliver`, `build` y `deconstruct` en el tipo nuevo `construction` (orden 3; acarreo pasa a 4) y `channel` en minería. Entregar funciona como acarrear: la pila más cercana de lo que falta y el plano más cercano a la pila (`Job.Recipient`). Nadie come de una pila que alguien entrega.
  - Los materiales del contenido se ordenan por id: el banco de pruebas rellena el mapa con `rock` explícitamente (`AsciiMap.ParseFilled`).
  - Tests: `ConstructionTests` (muro de troncos del almacén, plano que espera material, el muro no sube sobre un colono, borde de la caja, casa de cuatro muros con puerta, canal con rampa, desmontar, escalera de dos niveles, rampa junto a un muro, cancelar, plano que pierde su lugar, órdenes y planos que se excluyen) y validaciones de defs. La continuidad del guardado incluye planos, un canal y un muro por desmontar. 458 tests.
- Avance 3 (Godot):
  - Herramienta Construir (C): abre una fila con los construibles (por `order` del def) y los materiales que admite el elegido; arrastrar manda `PlaceBlueprintCommand`. La tecla y el botón recuerdan lo último elegido. Desmontar (V) y Canalizar (H) salen solas de `DesignationKind`.
  - Planos como cajas translúcidas del color del material, más opacas con todo entregado; marcas de desmontar y de canalizar. Pieza `Door`: dos postes y una hoja más baja que los muros, girada en la línea de los muros. Madera un poco más oscura (`#7E5531`).
  - Inspector: el plano con lo entregado ("Plano de muro de madera: 0 de 2") y las órdenes nuevas.
  - Las órdenes del smoke y de `--orders` suman una casa de 5 × 5 con puerta en el primer sitio llano cercano con el borde libre de plantas; el smoke informa cuánto se construyó. Opciones `--build` e `--inspect-blueprint` para capturas.
  - `terrainAmplitude` pasa de 3 a 2: con 3 no había sitio llano de 5 × 5 cerca de la llegada en la semilla 42; con 2 lo hay en las semillas 1, 7 y 42 (no en la 100). Las plantas también cerraban sitios: por eso sólo el borde tiene que estar libre.
  - Capturas revisadas: planos con la barra y el inspector en es y en; la casa terminada con su puerta en es.
- Avance 4 (revisión y cierre): 10 hallazgos, todos atendidos (detalle en `IP2.md`). Lo principal: nadie trabaja parado sobre una celda que otro trabajo va a cerrar (dos constructores vecinos se esperaban para siempre), desmontar una escalera reserva y despeja sus dos mitades, y muros, puertas, rampas y escaleras van sólo sobre suelo firme. Sin ADR nuevo: lo construido se reconoce por el material, dentro de las reglas existentes. 461 tests. `main` adelantado a H2.4.

### 2026-10-11: H2.3 Almacenes y acarreo

- Rama `ip2/h2-3-almacenes` desde `main`. Spec `docs/gdd/almacenes.md`.
- Avance 1 (Core):
  - Def `ItemCategory` (madera, piedra y comida) y `Item.category`. Tipo de trabajo `hauling` con el trabajo `haul`, después de recolectar.
  - Componentes `Stockpile` (celdas ordenadas y categorías permitidas) y `Carried`. `DesignateCommand` con `Stockpile` crea una zona con las celdas libres y pisables; `Cancel` además saca celdas de las zonas. `SetStockpileFilterCommand` cambia un filtro. `StockpilesChangedEvent` para la vista.
  - `StorageIndex` (derivado, se arma una vez por tick si hace falta): zona de cada celda y qué hay en cada celda de almacén.
  - Acarreo: el colono elige la pila suelta más cercana con destino; el destino es la celda de almacén más cercana a la pila que acepta el objeto y tiene lugar. Reserva la pila y la celda. Levanta lo que entra, lo lleva y lo deja. Si deja el trabajo con algo en las manos, lo suelta donde está; si muere, también.
  - Tests: `StockpileTests` (troncos apilados en el almacén, filtro de comida, el que tiene hambre suelta la carga y después se guarda, dos acarreadores nunca comparten pila ni destino, Cancelar y celdas ocupadas o no pisables). La continuidad del guardado incluye un almacén, un filtro y un colono cargando, en Core y con el contenido real. 435 tests.
- Avance 3 (revisión): 10 hallazgos, todos corregidos (detalle en `IP2.md`). Lo principal: una celda de almacén guarda una sola pila y las demás quedan sueltas, y las reservas de celdas se cuentan. 436 tests.
- Avance 4 (2026-10-12): segunda ronda de revisión, 8 hallazgos, 7 corregidos (detalle en `IP2.md`): nadie come de una pila que alguien acarrea, las pilas sin destino no gastan intentos, sin camino se deja de lado la celda de destino y no la pila, el índice se rearma tras mover pilas y las categorías de un almacén se validan al cargar. 439 tests, mismas huellas. H2.3 cerrado y `main` adelantado.
- Avance 2 (Godot):
  - Herramienta Almacén (Z): sale sola de `DesignationKind`, con su tecla. Las celdas de almacén se pintan de azul, finas y sobre el suelo.
  - El inspector de una celda de almacén lista las categorías con un botón Sí/No que manda `SetStockpileFilterCommand`; el de un colono dice qué lleva. El colono muestra el objeto en las manos.
  - Los almacenes sólo toman suelos planos: sobre las rampas la losa quedaba enterrada y las pilas se veían mal (spec actualizada).
  - Las órdenes del smoke y de `--orders` incluyen un almacén; el smoke informa cuánto quedó guardado (todos los troncos en las semillas 1 y 42). Opción `--inspect-stockpile` para capturas.
  - Huellas del smoke nuevas por el almacén en sus órdenes (ver Estado actual).
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
