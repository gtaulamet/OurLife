# IP4: Oficios

**Objetivo.** Que la aldea produzca: talleres con recetas y calidad según la habilidad, órdenes de producción, conocimientos que se investigan en un scriptorium o se ganan trabajando, campos con siembra y cosecha, comida cocinada que se pudre, minerales bajo tierra que se descubren al cavar, herramientas de hierro y casas con calidad, dormitorios asignados y comedor. Al terminar, una aldea puede ir de talar un tronco a forjar un hacha, de sembrar trigo a hornear pan y de dormir en el suelo a darle a cada familia una casa digna.

**Origen.** Es la IP4 del plan maestro (antes IP3; pasó a IP4 el 15 de octubre de 2026, cuando la vida de los colonos se adelantó). Junta lo que el plan reservó para esta IP: talleres, recetas, calidad, órdenes de producción, árbol de conocimientos medieval (el gancho de `docs/gdd/eras.md`, unas 10 h), agricultura, deterioro, vetas y niebla, paneles y las habitaciones y el comedor de la antigua IP4. También cobra lo que IP3.5 dejó para esta IP: el lugar de reunión (fogón y mesas) y sembrar árboles a pedido. Usa las habilidades de IP3 y los pensamientos y el ánimo de IP3. Spec de contrato en `docs/gdd/oficios.md` y sus cuatro hermanas. Decisiones del usuario: las ocho de O1 aprobadas el 22 de octubre de 2026, con la recomendación del diseñador en cada una.

**Estimación:** 105 a 130 h. **Versión al cerrar:** `v0.4.0`, para Windows y macOS.

**Estado:** en curso desde el 22 de octubre de 2026 (O1 hecho, sigue O2).

## Estado de los hitos

| Hito | Estado | Rama | Notas |
|---|---|---|---|
| O1 Spec y contrato | Hecho | `ip4/o1-spec` | Specs en `docs/gdd/oficios.md`, `conocimientos.md`, `agricultura.md`, `habitaciones.md` y `subsuelo.md`; aprobadas por el usuario el 22 de octubre de 2026, con las ocho decisiones como se recomendaron |
| O2 Calidad y habilidades de oficio | Hecho | `ip4/o2-calidad` | Calidad y frescura en las pilas, 17 objetos, `planks` y `ashlar`, 13 habilidades, 16 tablones iniciales, formato 12, HUD por categorías y panel Colonia de 13 columnas. Un hallazgo de diseño queda a la vista: una celda de almacén guarda una sola calidad |
| O3 Talleres y recetas | Hecho | `ip4/o3-talleres` | Core, defs y tests hechos (recetas, banco de carpintero, banco de cantero y cocina, trabajo `Craft`, `WorkOrder` mínimo con el modo Hacer N, muebles de tablones con calidad, formato 13). El inspector del taller crea y quita órdenes. Faltan capturas es/en y la revisión |
| O4 Órdenes de producción | Hecho | `ip4/o4-ordenes` | Modos Hacer N, Mantener N y Repetir, pausa, reordenar y quitar, estados `paused` y `satisfied`, formato 14, panel de producción (tecla O) y capturas es/en. Revisado |
| O5 Conocimientos y scriptorium | Hecho (sin íconos pixel) | `ip4/o5-conocimientos` | Árbol de 12 conocimientos, investigación, práctica, cola, scriptorium y trabajo `Research`, formato 15, panel K, línea del HUD y barra de construcción atenuada. Revisado; los íconos pixel van a una pasada de arte |
| O6 Agricultura y deterioro | Hecho y revisado; modelos propios de cultivos revisados en pantalla (28 de octubre) | `ip4/o5-conocimientos` | Formato 16; ver la bitácora del 24 de octubre |
| O7 Vetas y niebla subterránea | Hecho | `ip4/o7-subsuelo` | Core (vetas `OreVein`, `world.ore`, veta a la vista, `Discovered`, formato 17) y presentación: los chunks no dibujan celdas ocultas, el corte por nivel pinta las ocultas con un tono uniforme oscuro, el inspector dice "Sin explorar" o "Veta de hierro", patrón `ore` (motas oscuras) en el shader del terreno |
| O8 Fundición, herrería y herramientas | Core y presentación hechos (faltan los 3 modelos propios de los talleres de fuego y el criterio 15 en la partida) | `ip4/o8-metales` | Carbonera, horno de fundición y fragua, recetas de carbón, lingote y cuatro herramientas con calidad, `Equipment` y trabajo `Equip`, formato 18. Ver la bitácora del 28 de octubre |
| O9 Habitaciones, dormitorios y comedor | Hecho (Core y presentación): falta el `/code-review` y la fusión a `main` | `ip4/o9-habitaciones` | Detección con caché, puntaje y nivel, `AssignBedCommand` y camas reservadas, familias, comer sentado (`EatAtTable`), reunión (`Gather`), 11 pensamientos nuevos, formato 19. Presentación: capa de habitaciones (tecla L), lista de habitaciones con asignación de camas y de familia (tecla U y botón del HUD), inspector de celda y de colono, colono sentado (`sit`), íconos de 16 × 16 y capturas es/en. Adelanta de O10 la lista de habitaciones. Ver la bitácora del 28 de octubre y las notas de implementación de `habitaciones.md` |
| O10 Paneles | Hecho (presentación; falta el `/code-review` y la fusión a `main`) | `ip4/o10-paneles` | Panel de inventario (tecla I, `Stocks.Totals()`, con calidad y frescura), un solo panel abierto a la vez, Esc cierra la ficha o el panel, la barra superior en dos filas con el atajo de cada panel y el tiempo y el nivel en la segunda, paneles con desplazamiento para no tapar el registro, la ficha no tapa la barra ni el inspector. Capturas es/en de todos los paneles a 100, 150 y 200 %. Ver la bitácora del 28 de octubre y `oficios.md` |
| O11 Integración | Pendiente | `ip4/o11-integracion` | |

**Ruta crítica:** O1 → O2 → O3 → O5 → O8 → O10 → O11 (unas 62 h de las 105 mínimas). Detalle: O4, O5, O6 y O9 salen de O3 y pueden hacerse en paralelo; O7 sólo depende de O2 y puede empezar apenas se apruebe la spec; O8 necesita O3, O5 y O7; O10 espera a O4, O5, O6 y O9; el arte de cada taller y de la comida va junto a O3 y O6.

## Hitos en detalle

| Hito | Contenido | Agentes | Horas | Entregable verificable |
|---|---|---|---|---|
| O1 Spec y contrato | Las cinco specs de `docs/gdd/` con criterios de aceptación, defs propuestos (no entran todavía en `game/content`), contrato de comandos, eventos y consultas, formatos de guardado y lista de decisiones; esta IP y el backlog | game-designer, Plan, humano | 6–8 | Specs aprobadas por el usuario con las ocho decisiones respondidas |
| O2 Calidad y habilidades de oficio | `ItemStack` con calidad y frescura (sólo datos), los objetos, categorías y materiales nuevos (`planks`, `ashlar`) en defs con sus modelos, `Quality` y `QualityConfig`, siete tipos de trabajo con habilidad, migración de partidas, prioridades nuevas y tabla de trabajo del panel Colonia que se desplaza; los 16 tablones iniciales | sim-engineer, godot-engineer, art-integrator, i18n-specialist | 9–10 | Tests de la tirada de calidad (distribuciones de la spec), de pilas por calidad y de migración; captura es/en del panel Colonia con trece columnas; formato 12 |
| O3 Talleres y recetas | Def `Recipe`, `Buildable.workstation`, taller de una celda, trabajo `craft` con traída de ingredientes, tirada de calidad, banco de carpintero, banco de cantero y cocina con sus recetas; muebles de tablones, calidad del mueble por quien construye; el `UnlockService` aplicado a construibles y recetas; modelos de los talleres | sim-engineer, godot-engineer, art-integrator, i18n-specialist | 14–17 | Criterios 1 a 6, 9 y 11 de `oficios.md`; tronco → tablón → mesa en la partida, en es y en; formato 13 |
| O4 Órdenes de producción | `WorkOrder` con los modos hacer N, mantener N y repetir, estados calculados, reordenar, pausar y quitar; panel de producción | sim-engineer, godot-engineer, i18n-specialist | 7–9 | Criterios 7, 8 y 12 de `oficios.md`; panel de producción en es y en; formato 14 |
| O5 Conocimientos y scriptorium | `Knowledge` con costo, práctica y rama; los 12 conocimientos medievales; scriptorium y trabajo `research`; cola; desbloqueos en barra de construcción, recetas y comandos; panel de conocimientos y HUD; ícono de investigación | sim-engineer, game-designer, godot-engineer, art-integrator, i18n-specialist | 12–15 | Criterios 1 a 10 de `conocimientos.md`; panel en es y en; formato 15 |
| O6 Agricultura y deterioro | Zonas de cultivo, cultivos (trigo, guisantes, repollo), siembra y cosecha por estación, rendimiento por habilidad; las recetas de comida de la cocina; deterioro de alimentos con techo; silvicultura; la comida cuenta para la reserva de concepción; modelos de cultivos | sim-engineer, godot-engineer, art-integrator, i18n-specialist | 14–17 | Criterios de `agricultura.md`, incluido el balance de diez años con un campo; formato 16 |
| O7 Vetas y niebla subterránea | Vetas de hierro en la generación, mineral, `Discovered` en el mapa con descubrimiento al excavar, render sin celdas ocultas, migración con vetas | sim-engineer, godot-engineer, art-integrator | 10–13 | Criterios de `subsuelo.md`: vetas, veta a la vista en tres semillas, niebla en capturas; formato 17 |
| O8 Fundición, herrería y herramientas | Carbonera, horno de fundición y fragua con sus recetas; herramientas con calidad y bonus de velocidad; trabajo `equip`; modelos de herramientas y talleres de fuego | sim-engineer, art-integrator, i18n-specialist | 8–10 | Criterio 10 de `oficios.md`; mineral → lingote → hacha en la partida; formato 18 |
| O9 Habitaciones, dormitorios y comedor | Detección de habitaciones, puntaje y nivel, dormitorios con cama reservada, familias, comida sentada, lugar de reunión; pensamientos nuevos; capa de habitaciones | sim-engineer, game-designer, godot-engineer, i18n-specialist | 12–15 | Criterios de `habitaciones.md`; capturas es/en de la capa y del comedor; formato 19 |
| O10 Paneles | Panel de inventario de la colonia (por categoría, con calidad y frescura), lista de habitaciones, atajos y orden de todos los paneles de IP4, pulido de la interfaz, tamaño de interfaz | godot-engineer, i18n-specialist | 5–7 | Capturas es/en de todos los paneles a los tres tamaños de interfaz; ningún texto sin traducir |
| O11 Integración | Balance de comida, deterioro y herramientas con partidas de diez años; metas de madera y comida revisadas; benchmark; checklist de playtest; `/code-review`; paquetes y release `v0.4.0` | qa-tester, game-designer, `/code-review`, humano | 8–9 | Checklist `docs/ip/playtest-ip4.md` completo y `v0.4.0` publicada |
| **Total** | | | **105–130** | |

## Decisiones a cerrar en O1

Detalle y recomendaciones en `docs/gdd/oficios.md`, "Decisiones del usuario (a cerrar en O1)":

1. Los muebles pasan a tablones (con 16 tablones iniciales).
2. Las bayas se pudren a los 60 días (la mitad de velocidad bajo techo).
3. Las herramientas se equipan y aceleran el trabajo.
4. Las partidas viejas reciben vetas de hierro al migrar.
5. Sólo carpintería y cocina son conocimientos iniciales; investigar sólo cuesta tiempo.
6. Se come sentado si hay comedor, sin castigo si no lo hay.
7. Los talleres ocupan una celda.
8. Los cultivos no usan semillas; la silvicultura entra y es lo primero que sale si falta tiempo.

## Mejoras añadidas durante la IP

| Mejora | Hito | Detalle |
|---|---|---|
| Orden manual de cosechar | O10 | Hoy los cultivos se cosechan solos dentro de una zona (`reap`) y la orden `Harvest` sólo sirve para plantas silvestres. Se agrega una herramienta de cosecha del HUD para marcar a mano cultivos maduros, dentro o fuera de una zona, y que se cosechen antes que el resto. Pedida por el usuario el 28 de octubre de 2026. Spec en `docs/gdd/agricultura.md`. Hecha en `ip4/o10-cosecha`: nueva orden `DesignationKind.Reap` (atajo N), sin estado nuevo ni cambio de formato |

## Riesgos

- **Es la IP más grande hasta ahora** (11 hitos). Mitigación: O4, O5, O6, O7 y O9 son casi independientes y se pueden recortar; la silvicultura y el lugar de reunión son los primeros en salir (a una IP4.5) si O6 u O9 se pasan.
- **Balance de comida:** que las bayas se pudran puede dejar a la aldea sin reserva de invierno, con la meta de comida de 3000 y la regla de concepción de 30 días por boca. Mitigación: 60 días de plazo, techo y bodega que lo duplican, pruebas de diez años en O6 y O11 como `BalanceTests`, y el plazo es un número del def.
- **Rendimiento:** órdenes, ingredientes reservados, habitaciones y niebla recorren mucho. Mitigación: las órdenes se evalúan cada 30 ticks, las habitaciones se recalculan bajo demanda y con caché, la niebla sólo toca 27 celdas por cambio, y el presupuesto de 2 ms por tick se mide en cada hito.
- **Logística de ingredientes:** callejones sin salida (taller reservado, ingredientes a medias, colono con hambre crítica). Mitigación: la entrega queda en el taller, las reservas se liberan al dejar el trabajo y los criterios 6 y 9 de `oficios.md` los cubren.
- **Niebla y render:** dejar de dibujar celdas ocultas cambia cómo se construyen los chunks y el corte por nivel. Mitigación: O7 es independiente de los talleres y se prueba solo; la regla de un sentido evita casos raros.
- **Interfaz con muchos paneles** (producción, conocimientos, inventario, habitaciones, siete columnas más en trabajo). Mitigación: O10 ordena atajos y se prueba a los tres tamaños.
- **Arte:** talleres, herramientas, cultivos y comida piden muchos modelos y los kits son de otra temática. Mitigación: modelos simples armados con el estilo del kit y un listado en cada spec; los modelos de IP4 son los de menor prioridad frente a la jugabilidad.
- **Arranque más lento:** que los muebles pidan tablones y el árbol bloquee recetas retrasa lo básico. Mitigación: tablones y dos conocimientos iniciales, y que el balance de las primeras horas se mire en el playtest.
- **Guardado:** ocho formatos nuevos y una migración con niebla y vetas. Mitigación: una partida real de cada formato como fixture y tests de migración, como en IP3.5.
- **Partidas viejas sin hierro:** si el usuario rechaza la decisión 4, las partidas de `v0.3.5` no podrían forjar. Mitigación: la recomendación es migrar con vetas.

## Fuera de alcance

Va a `docs/ip/backlog.md`: crianza de animales, cuero, pergamino y libros (IP5), otros cultivos, rotación y fertilidad, riego y heladas (IP6), cerveza y queso, órdenes que crean sus intermedios, filtros de almacén por objeto, talleres de varias celdas, bodega con barriles, desgaste y cambio de herramientas, otros metales (bronce, acero), ladrillos, vidrio, molinos (IP6 o IP9), prospección, derrumbes (IP5), camas dobles, el deseo de un dormitorio propio, biblioteca y escuela (IP10), calidad de la comida, fogón al aire libre como lugar de reunión, y metas de libre albedrío para producir.
