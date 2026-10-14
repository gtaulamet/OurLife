# IP2.5: Pulido visual

**Objetivo.** Que el valle se vea mejor antes de sumar los oficios de IP3. Hoy el pasto, la tierra y la roca son bloques de un solo color, los colonos no se mueven y varios modelos son provisionales. Además, al colocar un mueble no se sabe hacia dónde mira, y los textos se ven chicos en pantallas grandes. No se busca realismo: el estilo sigue siendo low-poly (ADR-009), pero con textura, variación y vida.

**Origen.** Pedido del usuario del 14 de octubre de 2026, después de cerrar IP2. Se hace antes de planificar IP3.

**Estimación:** 38 a 55 h. **Versión al cerrar:** `v0.2.5`, para Windows y macOS.

**Estado:** planificada el 14 de octubre de 2026.

## Estado de los hitos

| Hito | Estado | Rama | Notas |
|---|---|---|---|
| V1 Prueba de estilo | Pendiente | `ip2.5/v1-prueba-estilo` | Variantes del terreno y del renderizador comparadas en capturas; el usuario elige |
| V2 Tamaño de la interfaz | Pendiente | `ip2.5/v2-tamano-interfaz` | |
| V3 Vista previa de muebles | Pendiente | `ip2.5/v3-vista-previa` | |
| V4 Terreno y decoración | Pendiente | `ip2.5/v4-terreno` | |
| V5 Modelos y animaciones | Pendiente | `ip2.5/v5-modelos` | |
| V6 Integración | Pendiente | `ip2.5/v6-integracion` | |

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
| V4 Terreno y decoración | Shader del terreno de la variante elegida; parámetros por material en el catálogo de visuales; borde de pasto en los costados; oscurecido por nivel como hoy; decoración por chunk con `MultiMesh`, elegida por un hash de la celda (nunca con el RNG de la simulación) y actualizada con los chunks sucios; tests del catálogo | godot-engineer, art-integrator | 10–14 | Capturas antes y después de la misma escena; corte bajo tierra con los estratos legibles; benchmark de la aldea dentro del presupuesto; mismas huellas |
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

## Decisiones abiertas

Se cierran en V1, con las capturas:

1. **Estilo del terreno:** variante A (colores planos con variación) o B (textura pintada en el shader).
2. **Renderizador:** seguir en GL Compatibility o pasar a Forward+.
3. **Kits:** de dónde salen los muebles medievales y las variantes de aldeanos.

## Riesgos

| Riesgo | Mitigación |
|---|---|
| Forward+ deja afuera PCs con placas de video integradas o rinde mal en la Mac | Medir en el benchmark; probar en la Mac en V6. Si no rinde, se queda en Compatibility y se compensa con el shader |
| Los kits de otro autor no combinan con el Nature Kit | Probarlos en una captura en V1, antes de elegir. La `palette` del catálogo unifica colores |
| La fuente pixel se ve borrosa en algún tamaño | Pasos de 50 % (múltiplos de 8 px); si no alcanza, pasos de 100 % |
| La UI escalada desplaza el punto que elige el ratón en el 3D | Criterio de V2: probar la selección de celdas y de colonos con la UI al 200 % |
| La decoración pesa en los mapas grandes de IP8 | `MultiMesh` por chunk y un límite de densidad en el catálogo |

## Definición de hecho de IP2.5

- El terreno, los muebles y los colonos se ven como se eligió en V1, y el usuario aprueba las capturas antes y después.
- Al colocar un mueble se ve hacia dónde mira, antes y después de colocarlo.
- El tamaño de la interfaz se elige en Opciones, se guarda, y en Automático los textos se leen bien de 720p a 4K.
- La simulación no cambia: mismas huellas del smoke (`889B6049962A78AD` con la semilla 1 y `974758E0263469A3` con la 42, 5000 ticks).
- Todo texto nuevo en es y en.
- `dotnet test` y smoke test en verde; capturas es/en revisadas.
- Presupuesto: 60 FPS con la aldea construida en el benchmark.
- `/code-review` sin hallazgos pendientes, playtest humano en Windows y macOS, y `v0.2.5` publicada.
