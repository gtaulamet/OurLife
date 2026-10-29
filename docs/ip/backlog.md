# Backlog

Ideas y tareas fuera del alcance de la IP en curso. Se revisan al planificar cada IP.

## Técnico

- Migrar de `net8.0` a `net10.0` cuando Godot lo adopte o se valide que el editor funciona con él. El soporte de .NET 8 termina el 10 de noviembre de 2026. Hacerlo antes de IP8. Dato a favor: Godot 4.7.2 ya ejecuta el juego sobre el runtime .NET 10.0.8; falta probar compilar con `net10.0` y exportar.
- Correr el smoke test de Godot en la CI, instalando Godot en el runner.
- Biblioteca compartida de utilidades para tests: raíz del repositorio y mapas ASCII.
- Autoguardado sin tirón: hoy el guardado corre en el hilo principal entre dos cuadros (unos 30 ms con el valle de IP1). Capturar el estado en el hilo principal y escribir el archivo en segundo plano, si con mapas más grandes se nota. Hallazgo de la revisión de H2.1.
- macOS sin Terminal: firmar con una cuenta de Apple Developer (99 USD por año) y notarizar, desde una Mac, para que la app abra con doble clic. Hoy la firma ad hoc que Godot genera desde Windows no la acepta macOS en Apple Silicon, y quien juega tiene que volver a firmarla con `codesign` (README). Alternativa sin cuenta: hacer la firma ad hoc en una Mac al empaquetar. Hallazgo del playtest de H2.7.
- Separador de miles en español según CLDR, que pide al menos dos dígitos de agrupación: "1234" y "12.345". .NET escribe "1.234".

## Interfaz

- Que `EntityRenderer` cambie los modelos cuando cambie la época. Llega con la segunda época.

## Diseño

- ¿Elementos de fantasía en la ambientación medieval? Por ahora no.
- Salto temporal opcional entre épocas. Se decide en IP9.
- ¿Retroceso de época tras un colapso de la colonia?
- Siglo XX y actualidad, después de IP10.
- Fluidos dinámicos, multijugador, modo aventura y mundos persistentes entre partidas.
- Generaciones en Muy rápido (H3.10): los fundadores salen de la edad fértil en dos o tres años del juego, el valle sostiene unas 7 personas y la aldea tiende a extinguirse en unos diez años. Revisar con la agricultura (IP4), que sube el tope de comida, y con los migrantes (IP6).
- Avisos de hambre crítica (H3.10): como los colonos trabajan hasta el umbral crítico, en una partida larga hay unos 8 avisos por mes. Ver en el playtest si molestan; podrían avisar sólo si nadie puede comer.
- Excavación y escaleras más legibles (playtest de IP3, hallazgo 2): una pendiente de montaña excavada se ve como dos triángulos dentro del cubo, y una escalera hacia abajo en el piso como un cubo vacío. Mostrar mejor que es una zona excavada y que la escalera es la entrada a una cueva.
- Oficios, fuera de IP4 (`docs/ip/IP4.md` y sus specs en `docs/gdd/`): crianza de animales, cuero, pergamino, tinta y libros como objetos (IP5, con la fauna); más cultivos (cebada, avena, centeno, lino), rotación de tres campos con fertilidad, semillas como objeto, malezas y plagas; riego y heladas (IP6); cerveza, queso y carne ahumada; órdenes de producción que crean solas los intermedios; filtros de almacén por objeto y no sólo por categoría; talleres de varias celdas; bodega con barriles y temperatura (IP6); desgaste, cambio y herramientas de piedra o bronce; cobre, estaño, acero, ladrillos, tejas y vidrio (IP9); molinos de agua o viento; prospección que revele vetas; derrumbes y apuntalado (IP5); camas dobles y el deseo de un dormitorio propio; biblioteca con estantes que aceleran la investigación y enseñar conocimientos (IP10, escuela); calidad de la comida y variedad de la dieta como pensamiento; fogón o fogata al aire libre como lugar de reunión; metas de libre albedrío para producir o sembrar; iniciativa de excavar y construir.
- Vida de los colonos, fuera de IP3 (`docs/gdd/vida-y-generaciones.md`): habilidades que se olvidan sin práctica, necesidad de ocio, camas dobles, casamientos y ceremonias, adopción, herencia de bienes y mellizos. Escuela en IP10, partos difíciles en IP5 y duelo con tumbas en IP5.

- Tocones de roble y de pino al talar un árbol: hoy el árbol talado desaparece. Pide dejar un tocón en el mapa (Core) ; los dos modelos propios (`stump_oak.glb`, `stump_pine.glb`) ya están hechos en `MODELS/entorno` y falta copiarlos a `game/assets/models/ourlife/` y al catálogo al hacer la función. Ver `docs/plan-modelos.md`, fase 6.
- Hallazgos de las partidas de diez años de O11 (`docs/ip/bitacora.md`, 29 de octubre de 2026): (1) un almacén lleno no avisa: lo que sobra queda suelto junto a los talleres y las órdenes esperan ingredientes sin decir por qué; un aviso "Almacén lleno" y un motivo en el estado de la orden lo arreglarían; (2) mostrar el radio de ingredientes de los talleres (45 celdas) al colocar un taller y en el estado `waiting_inputs`; (3) la regla de concepción (30 días de comida por boca) es invisible: mostrar en el panel Colonia la reserva y lo que falta para el próximo nacimiento; (4) una zona de almacén dibujada sobre árboles o pendiente se recorta a pocas celdas sin avisar al jugador; (5) herramientas ya equipadas que nadie usa (un pico sin minado) no se pueden pasar a otro colono: sumar a "desgaste y cambio de herramientas".
- Costo por tick del pueblo del año diez (`IntegrationBalanceTests`): sobre un día de juego completo cuesta 0,055 a 0,106 ms por tick, alrededor del presupuesto de 0,1 ms; el test usa 0,15 ms. Pide un perfil (decisiones de los colonos, órdenes de producción, habitaciones) antes de sumar más contenido.
