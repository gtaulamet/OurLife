# IP2.5: Pulido visual

**Objetivo.** Que el valle se vea mejor antes de sumar los oficios de IP3. Hoy el pasto, la tierra y la roca son bloques de un solo color, los colonos no se mueven y varios modelos son provisionales. Además, al colocar un mueble no se sabe hacia dónde mira, y los textos se ven chicos en pantallas grandes. No se busca realismo: el estilo sigue siendo low-poly (ADR-009), pero con textura, variación y vida.

**Origen.** Pedido del usuario del 14 de octubre de 2026, después de cerrar IP2. Se hace antes de planificar IP3.

**Estimación:** 38 a 55 h. **Versión al cerrar:** `v0.2.5`, para Windows y macOS.

**Estado:** en curso. V1 a V5 hechos; V6 espera el playtest humano.

## Estado de los hitos

| Hito | Estado | Rama | Notas |
|---|---|---|---|
| V1 Prueba de estilo | Hecho | `ip2.5/v1-prueba-estilo` | El usuario eligió terreno B, GL Compatibility y muebles de KayKit (ADR-012) |
| V2 Tamaño de la interfaz | Hecho | `ip2.5/v2-tamano-interfaz` | Opción en Opciones con Automático (100 % a 300 % según la ventana); clic probado con la UI al 200 % |
| V3 Vista previa de muebles | Hecho | `ip2.5/v3-vista-previa` | Modelo translúcido girado con flecha al frente, rojo donde no va; planos de muebles con su modelo |
| V4 Terreno y decoración | Hecho | `ip2.5/v4-terreno`, `ip2.5/v4-opcion-decoracion` | Sólo el estilo B; patrones, verdes y decoración en el catálogo; matas, flores, piedritas y hongos sobre el pasto, con la opción Decoración del pasto |
| V5 Modelos y animaciones | Hecho | `ip2.5/v5-modelos` | Muebles y provisiones de KayKit, bayas del Food Kit, 12 aldeanos recoloreados con animaciones |
| V6 Integración | En curso | `ip2.5/v6-integracion` | Paquetes de Windows y macOS armados y pasada automática hecha; falta el playtest humano (`docs/ip/playtest-ip2.5.md`) |

Los hitos se llaman V (de visual) para no confundirlos con H2.5, "Muebles y camas", de IP2.

**Ruta crítica:** V1 → V4 → V5 → V6. V2 y V3 no dependen del estilo: se hacen mientras el usuario revisa las capturas de V1.

## Alcance

- **Terreno:** el pasto cubre sólo la cara de arriba, y los costados muestran la tierra con un borde de pasto. Cada material tiene textura y variación de tono (tierra, roca, mampostería, madera), sin dejar de leerse los estratos en el corte. Luz y sombras revisadas, con el renderizador que se elija en V1.
- **Decoración del suelo:** matas de pasto, flores y piedritas sobre el pasto. Son sólo visuales: no son entidades, no se guardan y no cambian la simulación. Desaparecen cuando la celda se excava o se construye encima.
- **Modelos provisionales:** bayas, provisiones y un arbusto que muestra sus bayas cuando las tiene.
- **Muebles medievales:** cama, mesa, silla y cofre de un estilo acorde a la Edad Media, de otro kit CC0.
- **Colonos:** de 4 a 6 variantes de aldeanos según el género del colono, con las animaciones que ya trae el kit (quieto, caminar, trabajar, levantar y acarrear).
- **Vista previa de muebles:** el modelo del mueble, translúcido y girado, dentro del cubo de selección, con una flecha que marca su frente. Los planos de muebles ya colocados también se dibujan así, no como una caja.
- **Tamaño de la interfaz:** una opción con modo automático según la resolución y tamaños fijos. Se adelanta de la accesibilidad de IP8.

**Fuera de alcance:** cambiar la forma de los bloques (la grilla sigue siendo de cubos), animaciones de los muebles, partículas, sonido y modelos para otras épocas.

## Hitos en detalle

| Hito | Contenido | Agentes | Horas | Entregable verificable |
|---|---|---|---|---|
| V1 Prueba de estilo | Prototipo descartable del terreno en dos variantes: (A) colores planos con variación por celda y ruido suave; (B) textura pintada generada en el shader, con grano, vetas y estratos. Pasto sólo arriba en las dos. Comparación de GL Compatibility con Forward+ (sombras suaves y oclusión ambiental). Búsqueda de kits CC0 para los muebles medievales y los aldeanos (Kenney, KayKit, Quaternius) y prueba de cómo combinan | godot-engineer, art-integrator, humano | 5–8 | Capturas de la misma escena (semilla 42, nivel 14, zoom 22) al mediodía, al atardecer, de noche y en un corte bajo tierra, para cada variante. El usuario elige; ADR-012 y `docs/art.md` registran la decisión |
| V2 Tamaño de la interfaz | Spec; opción "Tamaño de la interfaz" en Opciones; escala de la UI 2D sin tocar el render 3D; modo automático según el tamaño de la ventana; preferencia guardada; `--ui-scale` para capturas | godot-engineer, i18n-specialist | 3–5 | Capturas a 1280×720, 1920×1080 y 2560×1440 en es y en, sin textos cortados ni superpuestos; la selección de celdas con el ratón sigue acertando con la UI escalada |
| V3 Vista previa de muebles | Spec; modelo translúcido del mueble en la celda bajo el cursor, girado con G, y en cada celda válida del arrastre; flecha en el suelo hacia el frente; tinte rojo donde no se puede colocar; planos de muebles dibujados con su modelo y su giro | godot-engineer, sim-engineer (consulta de solo lectura, si hace falta) | 4–6 | Captura con una cama y una silla en vista previa, en los cuatro giros; un plano de cama colocado muestra su orientación; las huellas no cambian |
| V4 Terreno y decoración | Shader del terreno de la variante elegida; parámetros por material y colores del pasto en el catálogo de visuales, con su test (hoy están en `TerrainStyle` y en el shader); borde de pasto en los costados; oscurecido por nivel como hoy; decoración por chunk con `MultiMesh`, elegida por un hash de la celda (nunca con el RNG de la simulación) y actualizada con los chunks sucios; tests del catálogo | godot-engineer, art-integrator | 10–14 | Capturas antes y después de la misma escena; corte bajo tierra con los estratos legibles; benchmark de la aldea dentro del presupuesto; mismas huellas |
| V5 Modelos y animaciones | Bayas, provisiones y arbusto con bayas; cama, mesa, silla y cofre medievales; variantes de aldeanos según el género; animaciones por estado del colono (quieto, caminar, trabajar, acarrear) sincronizadas con la velocidad del juego; catálogo, `CREDITS.md` y licencias | art-integrator, godot-engineer | 12–16 | Capturas de la aldea con colonos distintos y la casa amueblada; colonos que caminan, trabajan y acarrean, vistos a 1× y 4×; benchmark dentro del presupuesto |
| V6 Integración | Checklist de playtest, capturas es/en, benchmark, paquetes de Windows y macOS, prueba en la Mac si cambia el renderizador, release `v0.2.5` | qa-tester, `/code-review`, humano | 4–6 | Checklist completo y `v0.2.5` publicada |

## Cómo se va a hacer

### V1: prueba de estilo

- El prototipo vive sólo en la rama de V1 y no se mezcla con el código final: lo que se elige se implementa limpio en V4.
- Las dos variantes del terreno usan un shader propio en lugar del `StandardMaterial3D` de hoy. El color base sigue saliendo del `MaterialDef`. El patrón se genera en el shader, en coordenadas del mundo, así que no hacen falta texturas nuevas ni se ven costuras entre celdas.
- **Pasto:** hoy la marca `Grass` tiñe el bloque entero. Con el shader, la cara de arriba es pasto, los costados son del material y tienen una franja de pasto arriba.
- **Renderizador:** GL Compatibility no tiene oclusión ambiental en pantalla ni sombras suaves. Forward+ sí, pero exige más a la placa de video. Se comparan capturas y FPS del benchmark en esta PC. Si gana Forward+, ADR-012 reemplaza ese punto de ADR-009 y V6 lo prueba en la Mac.
- **Kits:** el art-integrator arma una tabla de candidatos con licencia, estilo, cantidad de polígonos y si traen texturas. Se prueban en una captura junto a los modelos del Nature Kit, para ver si combinan.

### V2: tamaño de la interfaz

- Hoy la UI está a 16 px a cualquier resolución: el proyecto no estira el contenido.
- La UI 2D se escala con `Window.ContentScaleFactor`, sin modo de estiramiento, así el 3D se sigue dibujando a la resolución nativa.
- **Tamaños:** Automático, 100 %, 150 %, 200 %, 250 % y 300 %. Automático elige el mayor tamaño que deja un lienzo de al menos 1280×720: 100 % en 720p, 150 % en 1080p, 200 % en 1440p y 300 % en 4K. Los tamaños fijos llegan hasta el que deja un lienzo de 960×540 en la ventana actual.
- Los pasos de 50 % dan múltiplos de 8 px de la fuente Tiny5, que se mantiene nítida. Se confirma en las capturas; si 150 % no se ve nítido, los pasos pasan a ser de 100 %.
- El modo automático se recalcula al cambiar el tamaño de la ventana o al pasar a pantalla completa.
- Textos nuevos en es y en: el nombre de la opción y "Automático".

### V3: vista previa de muebles

- Con la herramienta Construir y un mueble elegido, la celda bajo el cursor muestra el modelo del mueble translúcido, con el giro actual. Al pulsar G gira en el acto.
- Una flecha plana en el suelo marca el frente del mueble, porque la mesa y el cofre se ven casi iguales en dos giros.
- Durante un arrastre, la vista previa aparece en cada celda válida de la caja, con un máximo para no llenar la pantalla. Las celdas que no admiten el mueble se tiñen de rojo.
- Para saber si una celda admite el mueble hace falta una consulta de solo lectura en Core con las reglas de `Construction`. Si ya existe, se usa; si no, se agrega con sus tests, sin comandos ni estado nuevos.
- Los planos de muebles ya colocados pasan de la caja de hoy al mismo modelo translúcido, con su giro. Su opacidad sigue indicando si el material ya se entregó.

### V4: terreno y decoración

- El shader elegido reemplaza el material del terreno en `MapRenderer`, conservando el oscurecido por profundidad de `LevelShading` y la tapa del corte.
- Los parámetros de cada material (color secundario, patrón, intensidad del grano) se agregan en `visuals.json`, en `terrain.materials`, por id de material. Son de presentación, no de balance, por eso no van en los defs. Los tests del catálogo verifican que cada material tenga sus parámetros.
- La decoración se elige por un hash de las coordenadas de la celda, siempre igual para la misma celda, y nunca usa el RNG de la simulación. Se dibuja con un `MultiMesh` por modelo y por chunk, y se rehace cuando el chunk se marca sucio.
- El rendimiento se mide con `--benchmark` antes y después, con la aldea construida.

### V5: modelos y animaciones

- La variante de cada colono sale del género que guarda la simulación y de un hash de su `EntityId`. Es sólo de presentación y no se guarda.
- La animación sale del estado que ya se consulta para el inspector y el panel Colonia: caminar si se mueve, trabajar si hace un trabajo, acarrear si lleva algo, quieto si no. Dormir conserva la pose acostada de hoy. La velocidad de la animación sigue la velocidad del juego, y la pausa la congela.
- Cada modelo nuevo entra al catálogo con su licencia en `CREDITS.md`, y se borran del repositorio los que dejan de usarse, como piden los tests del catálogo.

## Decisiones de V1

Cerradas el 14 de octubre de 2026 (ADR-012):

1. **Estilo del terreno:** B, textura pintada en el shader.
2. **Renderizador:** se queda GL Compatibility.
3. **Kits:** muebles, barriles y comida de KayKit Dungeon Remastered; aldeanos de Mini Characters de Kenney, con la ropa recoloreada.

## Riesgos

| Riesgo | Mitigación |
|---|---|
| Forward+ deja afuera PCs con placas de video integradas o rinde mal en la Mac | Resuelto en V1: se queda GL Compatibility |
| Los kits de otro autor no combinan con el Nature Kit | Probarlos en una captura en V1, antes de elegir. La `palette` del catálogo unifica colores |
| La fuente pixel se ve borrosa en algún tamaño | Pasos de 50 % (múltiplos de 8 px); si no alcanza, pasos de 100 % |
| La UI escalada desplaza el punto que elige el ratón en el 3D | Criterio de V2: probar la selección de celdas y de colonos con la UI al 200 % |
| La decoración pesa en los mapas grandes de IP8 | Una malla por chunk, la probabilidad en el catálogo y la opción Decoración del pasto (Nada a Mucha) |
| Opciones ya no entra a 720p con una fila más | Al sumar la próxima opción, pasar a dos columnas o a pestañas |

## Definición de hecho de IP2.5

- El terreno, los muebles y los colonos se ven como se eligió en V1, y el usuario aprueba las capturas antes y después.
- Al colocar un mueble se ve hacia dónde mira, antes y después de colocarlo.
- El tamaño de la interfaz se elige en Opciones, se guarda, y en Automático los textos se leen bien de 720p a 4K.
- La simulación no cambia: mismas huellas del smoke (`889B6049962A78AD` con la semilla 1 y `974758E0263469A3` con la 42, 5000 ticks).
- Todo texto nuevo en es y en.
- `dotnet test` y smoke test en verde; capturas es/en revisadas.
- Presupuesto: 60 FPS con la aldea construida en el benchmark.
- `/code-review` sin hallazgos pendientes, playtest humano en Windows y macOS, y `v0.2.5` publicada.

## Registro de V1: Prueba de estilo

**Hecho hasta ahora (14 de octubre de 2026)**

- Shader del terreno `game/assets/shaders/terrain.gdshader`, en lugar del `StandardMaterial3D`. Cuatro estilos: el plano de IP2 (`flat`) y las variantes A (variación de tono y bisel), B (textura pintada: briznas, grano, vetas, estratos, tablones y ladrillos) y C (B con 16 píxeles por celda). Todo se calcula según la posición en el mundo, sin texturas.
- `MapRenderer` pasa a cada instancia el patrón de su material y las marcas de pasto (cara de arriba, toda la pieza o borde en los costados), con los datos propios del `MultiMesh`. El pasto ya no tiñe los costados de las rampas, y los bloques bajo el pasto llevan un borde verde.
- `TerrainStyle` (provisorio) asigna un patrón a cada material y guarda el estilo; `--terrain-style flat|a|b|c` lo elige para las capturas. Forward+ se probó con `--rendering-method forward_plus` de Godot, con oclusión ambiental y la luz un 30 % más fuerte; al elegirse Compatibility, ese código se quitó.
- `CameraRig.Focus`: `--inspect-furniture` también centra la cámara en el mueble.
- Kits: KayKit Dungeon Remastered (CC0, en GitHub) trae cama, mesas, silla, banqueta, baúles, barriles y platos de comida medievales; se probaron en la casa del smoke sin dejarlos en el repositorio. Mini Characters de Kenney trae 12 aldeanos (6 y 6) con las animaciones; no hay un kit CC0 gratuito de aldeanos medievales animados, así que se propone recolorear su ropa.
- Benchmark de 20 s (RTX 4090 de portátil): Compatibility con el terreno actual, 431 FPS de media y 335 en el 1 % más lento; con B, 420 y 330; Forward+ con B, 674 y 540. El cuadro de unos 100 ms que se vio no era del shader: en V4 se comprobó que aparece cuando la ventana del benchmark cambia de tamaño a mitad de la medición.
- Capturas comparadas en una página privada para el usuario: https://claude.ai/artifact/JkWuPmFMuosFSxV8TgAGiD
- 480 tests en verde y mismas huellas del smoke.

**Decisión del usuario:** terreno B, GL Compatibility y KayKit. Se quitó del código lo que sólo servía para probar Forward+ (luz más fuerte y oclusión ambiental). ADR-012 registra la decisión, ADR-009 la referencia, y `docs/art.md` suma la dirección del terreno y las fuentes nuevas.

**Revisión de código:** 10 hallazgos, 6 corregidos.

- La cara inclinada de una rampa se pinta siempre desde arriba: la proyección ya no depende del redondeo y no hay costuras en la pendiente.
- El borde de pasto no se dibuja en el nivel que corta la vista, donde el pasto de arriba está oculto.
- El oscurecido de los costados se mide desde la base de cada pieza, no de la celda: los escalones lo tienen bien.
- La variación de tono sale de la celda de la instancia, así la tapa del corte coincide con su bloque.
- Código muerto en los tablones, y `IP2.5.md` y la bitácora al día.
- Quedan para V4, como prevé ADR-012: los colores del pasto fijos en el shader y los patrones por id de material en `TerrainStyle`, sin test. Pasan al catálogo de visuales con su test.
- Queda como está: `CameraRig.Focus` centra la cámara en la columna del mueble sin cambiar el nivel. Es para capturas, que ya pasan `--level`.
- 480 tests en verde y mismas huellas.

## Registro de V2: Tamaño de la interfaz

**Entregado**

- Spec `docs/gdd/tamano-de-la-interfaz.md`.
- `UiScaleRules`, sin Godot: Automático elige el mayor tamaño que deja un lienzo de 1280×720 (150 % en 1080p, 200 % en 1440p, 300 % en 4K); los fijos, hasta el que deja 960×540; si el elegido no entra, el mayor que entra.
- `Settings.UiScale` (`ui_scale` en `settings.cfg`, Automático por defecto; un valor desconocido vuelve a Automático) y `ApplyUiScale`, que pone `Window.ContentScaleFactor` sin modo de estiramiento: escala la UI 2D y el 3D sigue a la resolución nativa. Se recalcula al cambiar la ventana o la preferencia.
- Opciones: fila "Tamaño de la interfaz" con "Automático (150 %)" y los pasos; los que no entran, deshabilitados. Textos `ui.options.ui_scale`, `ui_scale_auto` y `ui_scale_percent` en es y en.
- HUD: con el panel Colonia abierto, las alertas muestran sólo los peligros; si el panel llega al inspector, el inspector baja debajo cuando entra. En un lienzo de 960×540 se superponían.
- `--ui-scale` para capturas y el smoke; las capturas a otra resolución usan `--resolution` de Godot (skill `godot-run`).
- Smoke: con la UI al 200 %, un clic mandado a la ventana en el píxel del colono lo selecciona. Se comprobó que falla si el clic cae en otro lugar. En el smoke sin ventana, la raíz mide 64×64 y el HUD la tapa: la prueba le da 1280×720 mientras corre.

**Verificación**

- 14 tests nuevos de la regla (494 en total) y mismas huellas del smoke.
- Capturas revisadas: Opciones a 1280×720 (es), 1920×1080 (en, Automático 150 %) y 2560×1440 (es, 200 %); partida con inspector y panel Colonia a 1280×720 (es), 1920×1080 (es, 150 % y 200 %) y 2560×1440 (en). La fuente se ve nítida al 150 % y al 200 %.

**Revisión de código:** 8 hallazgos, todos corregidos.

- La prueba del smoke no corría al 200 %: al cambiar el factor, `SizeChanged` lo devolvía a Automático. Ahora pone el tamaño por las preferencias, a 1920×1080, y comprueba que el factor sea 2 antes de hacer clic. Con el clic corrido a otro lugar, la prueba falla.
- `--ui-scale` ya no termina guardado cuando se cambia otra opción: el tamaño de la sesión y el guardado van por separado. Elegir el mismo tamaño no vuelve a escribir el archivo.
- El inspector baja debajo del panel Colonia sólo si entra arriba de las barras de abajo.
- Con el panel abierto se ocultan sólo los avisos; los peligros, como el hambre crítica, siguen a la vista.
- La pantalla de error del arranque sigue el tamaño de la ventana.
- En Opciones se ve presionado el tamaño que se usa, no uno deshabilitado.
- El registro de opciones muestra `ui-scale=auto`.

## Registro de V3: Vista previa de muebles

**Entregado**

- Spec `docs/gdd/vista-previa-de-muebles.md`.
- Core: `Construction.CellsToPlan` devuelve, sin cambiar nada, las celdas donde `PlaceBlueprintCommand` pondría planos, en su orden; el comando la usa. Test: mismas celdas que el comando en una caja mixta, y la huella no cambia.
- `FurnitureGhosts`: modelos translúcidos de muebles, girados, con un grupo de nodos por modelo que se reusa. La vista previa se dibuja por encima de todo (también del cubo del cursor, que la tapaba) con una flecha plana hacia el frente; lo que no se puede colocar, en rojo. `ModelLibrary.ShadeGhost` da las copias translúcidas de los materiales.
- `GameInput` muestra la vista previa en la celda del cursor o en cada celda del arrastre (hasta 64, si no sólo las válidas), con la misma consulta que el comando.
- `OrderMarkers` dibuja los planos de muebles con su modelo y su giro, más opacos con el material completo.
- Capturas: `--furniture`, `--rotation`, `--point` (también centra la cámara) y `--drag-to`; el smoke muestra la vista previa de una cama donde está un colono.
- Arreglo de V2: la salida ya no desconecta `SizeChanged` si el arranque falló antes de conectarla.

**Verificación**

- El frente de los modelos, antes de girar, es +Z. Capturas de la silla y de la cama en los cuatro giros: la flecha apunta hacia donde mira la silla y hacia los pies de la cama en cada uno.
- Capturas de una mesa sobre la silla de la casa (roja), de un arrastre de cofres sobre la casa (válidos en el piso, rojos sobre muros y muebles) y de los planos de la cama y la mesa a mitad de obra, girados.
- 495 tests en verde y mismas huellas del smoke.

**Revisión de código:** 10 hallazgos, 9 corregidos.

- La vista previa se calcula de nuevo sólo cuando cambian la caja, el mueble, el tick o la época, o después de una orden de la entrada; antes recorría planos, plantas y almacenes en cada cuadro. Una caja grande ya no arma la lista de todas sus celdas: se cuentan con el recorte de Core (`Designations.TryClampBox`), el mismo del comando.
- La vista previa y los planos de muebles siguen un cambio de época, que no tiene evento.
- La comprobación del smoke deja el último construible elegido como estaba.
- `--rotation` pide `--furniture`, y `--furniture` y `--point` piden `--play` o `--load`.
- `ModelLibrary` recorre las superficies en un solo lugar para los dos tipos de copia; se quitaron una rama muerta y una sangría mal puesta.
- Queda como está: cada grupo de fantasmas tiene su propia `ModelLibrary`. Los `PackedScene` son los mismos recursos de la caché de Godot y sólo se duplican diccionarios chicos; compartir la biblioteca complicaría quién la libera.
- 495 tests en verde y mismas huellas.

## Registro de V4: Terreno y decoración

**Entregado**

- El shader del terreno queda con el estilo B; se quitaron los otros estilos, `TerrainStyle` y `--terrain-style`.
- Catálogo de visuales: `terrain.grass` (los tres verdes del pasto) y `terrain.materials` (el patrón de cada material, por nombre) reemplazan a `grassColor`, y `decoration` lista los modelos que visten el pasto con su probabilidad. `VisualCatalog` los lee y rechaza patrones desconocidos, campos de más y probabilidades que sumen más de mil.
- Decoración: siete modelos del Nature Kit (tres matas de pasto, flores amarillas y violetas, piedritas y un hongo). Un hash de la celda, nunca el RNG de la simulación, elige modelo, giro, tamaño y lugar; nada bajo muebles ni plantas. Cada modelo se hornea en triángulos con su color en los vértices (`DecorationMesh`), y la decoración de un chunk es una sola malla que se rehace con el chunk y se oscurece por nivel como el terreno. No proyecta sombras.
- Tests del catálogo: cada material del contenido tiene patrón y cada patrón nombra un material; los modelos de la decoración existen, se importan, corrigen su color y cuentan para la paleta y para los `.glb` del repositorio.
- `docs/art.md` (formato del catálogo y reglas de la decoración), ADR-012 (consecuencias), `CREDITS.md` (15 modelos del Nature Kit).

**Verificación**

- Capturas de después en las escenas de antes (valle, de cerca, corte bajo tierra, la casa, atardecer y noche): https://claude.ai/artifact/JkWuPmFMuosFSxV8TgAGiD tiene las de antes.
- Rendimiento: con un `MultiMesh` por modelo y por chunk la decoración costaba hasta siete llamadas de dibujo por chunk; con una malla por chunk el promedio volvió a 418 FPS. En corridas seguidas la portátil dio entre 232 y 418 FPS con decoración y entre 288 y 311 sin ella: la diferencia queda dentro del ruido de la máquina, siempre muy por encima de 60 FPS. La partida se arma en unos 300 ms (antes, 210 ms).
- El cuadro de 100 ms de V1 no era del shader (ver el registro de V1).
- 506 tests en verde y mismas huellas.

**Revisión de código:** 10 hallazgos, 8 corregidos.

- Los arbustos de bayas no bloquean el paso, y la decoración crecía dentro de ellos: ahora la vista le pasa al terreno las celdas con plantas (`Designations.PlantCells`, calculado de nuevo cuando aparecen o se van entidades).
- Una pieza sólo se corre hacia una vecina que también se puede decorar: no se mete en muros, muebles ni plantas, ni cuelga sobre un desnivel.
- El smoke también carga los modelos de la decoración, y uno que no carga deja un aviso.
- Un modelo sin normales o con superficies que no son triángulos ya no rompe el arranque. La decoración se dibuja por las dos caras, y un test pide que sus modelos no tengan texturas, porque el horneado guarda un color por material.
- La suma de probabilidades no desborda; las normales se giran una vez por pieza; la decoración se decide en el mismo paso que dibuja el suelo, sin recorrer el cielo ni la roca; `ModelLibrary` carga por ruta.
- Quedan como están: las matas toman el `grass` de la paleta, a propósito un poco más oscuro que el pasto del suelo para que se distingan (anotado en `docs/art.md`), y la biblioteca de modelos propia del terreno, por lo mismo que en V3.
- 507 tests en verde y mismas huellas.

## Agregado a V4: opción de decoración

Pedido del usuario del 14 de octubre de 2026, después de cerrar V4, en la rama `ip2.5/v4-opcion-decoracion`. Spec en `docs/gdd/decoracion-del-pasto.md`.

- Opciones suma **Decoración del pasto**: Nada, Poca, Normal (por defecto) y Mucha, que multiplican la probabilidad total del catálogo por 0, 0,4, 1 y 1,8. Se aplica en el acto, rearmando sólo la decoración de cada chunk, y se guarda (`decoration` en `settings.cfg`; un valor desconocido vuelve a Normal).
- Un hash de la celda dice si lleva decoración y otro elige la pieza: una celda decorada en un nivel lleva la misma pieza en los niveles mayores, y la mezcla no cambia.
- `--decoration none|low|normal|high` para capturas y benchmarks, sin guardarlo.
- Medición alternada, dos veces: Nada 327 y 324 FPS, Mucha 303 y 300. Mucha cuesta unos 0,25 ms por cuadro en la RTX 4090 de portátil; en una placa más lenta la proporción puede ser mayor.
- Capturas de los cuatro niveles y de Opciones en es y en. Con esta fila, Opciones a 720p queda justa: una más no entra.
- 507 tests en verde y mismas huellas.
- Revisión de código: 7 hallazgos, 6 corregidos. Lo principal: al escalar cada probabilidad, una celda podía cambiar de pieza al subir el nivel, contra lo que dice la spec; ahora la presencia y la pieza salen de hashes distintos. Cambiar el nivel rearma sólo la decoración, sin vaciar los chunks pendientes ni dejar vieja la tapa del corte. La vista se entera por el evento `Settings.DecorationChanged`, no consultando en cada cuadro. El smoke pasa por los cuatro niveles. Queda como está cuándo se guarda el nivel elegido con `--decoration` activo: la misma regla que el tamaño de la interfaz.

## Registro de V5: Modelos y animaciones

**Entregado**

- Catálogo de visuales: un modelo puede tener `variants` por etiqueta, `parts` con su desplazamiento y escala, y `turn` para llevar su frente a +Z. `VisualModel.Files` lista todos sus archivos; los tests del catálogo (existencia, importación, paleta, archivos del repositorio) los recorren todos.
- Muebles de KayKit Dungeon Remastered: `bed_frame`, `table_small`, `chair` y `trunk_medium_A` (con `turn: 3`, porque su frente mira a +X). La paleta entona su madera (`texture`) para que combine con los muros. Salen el Furniture Kit y el Survival Kit.
- Bayas: `cherries` del Food Kit, sueltas y como tres racimos sobre el arbusto que tiene bayas. Provisiones: `barrel_small` de KayKit, con la misma madera. Salen `flower_redA` y `pot_small`.
- Aldeanos: los 12 de Mini Characters. Cada colono toma uno de los seis de su género según su id. La textura que comparten se recoloreó con tonos de época.
- Animaciones: caminar, golpear (cavar, tallar escaleras, talar, desmontar), manipular (construir), levantar (recolectar), sostener (con carga) y quieto, en bucle. Van a la velocidad del juego y se detienen con la pausa o con un menú abierto. Dormir conserva la pose acostada.
- El smoke carga todos los archivos de cada modelo, variantes y partes incluidas.
- `CREDITS.md` y `docs/art.md` (formato del catálogo, modelos que faltan, recoloreo).

**Verificación**

- Capturas: la llegada con los colonos, el trabajo con poses distintas, la casa de día con los muebles de KayKit y de noche con un colono dormido en la cama, los arbustos con cerezas.
- Vista previa de silla, cama y baúl en giro 0, y del baúl en giro 1: la flecha apunta al frente de cada modelo.
- 507 tests en verde y mismas huellas.

**Revisión de código:** 10 hallazgos, 8 corregidos.

- La elección de variante era, en la práctica, `id % 6`: con colonos de géneros alternados sólo salían tres de las seis variantes de cada género. Ahora un hash mezcla los bits.
- El registro de animaciones ya puestas en bucle comparaba envoltorios de C# que se liberan y crecía sin fin; ahora usa el id de Godot.
- La caché de materiales distingue la corrección de color, y un test pide que todos los archivos de un modelo (variantes y partes) necesiten la misma.
- La cama baja a escala 0,33 para no pasarse de su celda; el durmiente sigue acostado sobre ella.
- La carga en las manos toma la escala del catálogo, no la del nodo, que con `turn` o `parts` es un contenedor sin escala.
- Las animaciones no consultan Godot en cada cuadro: la lista de animaciones se guarda por modelo y la velocidad sólo se cambia si cambia.
- `GameSession.EffectiveSpeed` es la regla de velocidad del reloj y de las animaciones.
- Un test pide que las etiquetas de variantes sean géneros; se quitó una sobrecarga de `ModelLibrary.Load` sin uso y una mención vieja al Furniture Kit.
- Quedan como están: cada modelo de KayKit trae su copia de la misma textura (unificarla pide editar los `.glb`; son 16 KB cada una) y la clave `texture` de la paleta, el nombre del único material de KayKit (anotado en `docs/art.md`).
- 509 tests en verde y mismas huellas.

## Registro de V6: Integración

**Hecho hasta ahora (14 de octubre de 2026)**

- Versión 0.2.5 en `project.godot` (los presets la toman de ahí) y README al día.
- Paquetes: `OurLife-v0.2.5-windows-x86_64.zip` (72,7 MB, con el smoke corrido sobre el ejecutable exportado) y `OurLife-v0.2.5-macos-universal.zip` (121,8 MB, firma ad hoc a rehacer en la Mac).
- Checklist `docs/ip/playtest-ip2.5.md`, con la pasada automática: huellas iguales a las de v0.2.0, 509 tests, clic con la interfaz al 200 %, vista previa, decoración en vivo, benchmark dentro del presupuesto y capturas es/en a 1920×1080.
