# IP2: Asentamiento

**Objetivo.** Pasar de sobrevivir a asentarse: construir una casa con camas, ordenar el almacén, ver a la aldea dormir de noche y retomar la partida otro día.

**Estimación:** 77 a 101 h, igual que `plan-maestro.md` (sección 7.3). **Versión al cerrar:** `v0.2.0`.

**Estado:** planificada el 11 de octubre de 2026. En curso: H2.1.

## Estado de los hitos

| Hito | Estado | Rama | Notas |
|---|---|---|---|
| H2.1 Guardado y carga | En curso | `ip2/h2-1-guardado` | |
| H2.2 Día, noche y sueño | Pendiente | `ip2/h2-2-dia-noche` | |
| H2.3 Almacenes y acarreo | Pendiente | `ip2/h2-3-almacenes` | |
| H2.4 Construcción | Pendiente | `ip2/h2-4-construccion` | |
| H2.5 Muebles y camas | Pendiente | `ip2/h2-5-muebles-camas` | |
| H2.6 Gestión de la colonia | Pendiente | `ip2/h2-6-gestion` | |
| H2.7 Integración | Pendiente | `ip2/h2-7-integracion` | |

**Ruta crítica:** H2.1 → (H2.2 ∥ H2.3) → H2.4 → H2.5 → H2.6 → H2.7.

**Por qué el guardado va primero:** el test "guardar a mitad y seguir = partida continua" queda corriendo desde el principio. Así, cada componente, comando o sistema nuevo de IP2 se prueba guardado y cargado en el mismo hito en que aparece, y no se descubre al final que algo no se puede serializar.

## Alcance

- **Guardado y carga:** ranuras con nombre, autoguardado diario y guardado manual desde el menú de pausa. "Cargar" se habilita en el menú principal. La partida guardada incluye el mapa, las entidades, los RNG, el reloj, la época, los conocimientos y el nombre de la aldea, que pasa a la simulación. Versión de formato y rechazo claro de un archivo incompatible o dañado.
- **Día y noche:** el sol cruza el cielo según la hora, y la luz, el ambiente y el cielo pasan por el amanecer, el día, el atardecer y la noche. La noche nunca queda a oscuras del todo y el ciclo se puede apagar en Opciones. Es de presentación: no cambia la simulación ni las huellas.
- **Sueño nocturno:** los colonos prefieren dormir de noche, con el horario en los defs. Un colono agotado duerme igual a cualquier hora. Se revisa el balance del hambre y del invierno.
- **Almacenes:** zonas dibujadas por arrastre con filtros por categoría de objeto. Los colonos acarrean los objetos sueltos al almacén y los apilan hasta un máximo por celda.
- **Construcción por planos:** muros, suelos, puertas, escaleras y rampas, con el material elegido. Los colonos entregan los materiales y después construyen. Desmontar devuelve parte del material. Canalizar abre un hueco hacia el nivel de abajo.
- **Muebles:** cama, mesa, silla y cofre, colocados como planos con orientación. El cofre funciona como un almacén de una celda.
- **Camas:** cada colono puede tener una cama asignada. Dormir en cama descansa más rápido que en el suelo.
- **Gestión:** panel de prioridades de trabajo (colono × tipo de trabajo) y lista de colonos con su actividad.
- **Alertas:** colono ocioso y hambre crítica, con aviso en el HUD y en el registro.

## Hitos en detalle

| Hito | Contenido | Agentes | Horas | Entregable verificable |
|---|---|---|---|---|
| H2.1 Guardado y carga | Spec; nombre de la aldea en la simulación; formato versionado (JSON comprimido); guardar y cargar mapa, entidades, RNG, reloj, progreso y comandos pendientes; derivados (conectividad, reservas) recalculados al cargar; ranuras y autoguardado diario; pantalla de carga; guardar desde la pausa; pantalla de error al arrancar o al cargar | Plan, sim-engineer, godot-engineer, i18n-specialist | 10–14 | Test "guardar en el tick N, cargar y seguir hasta M" = misma huella que correr hasta M sin cortar, en varias semillas; partida guardada y cargada desde el menú en es y en |
| H2.2 Día, noche y sueño | Ciclo visual de luz, ambiente y cielo con opción para apagarlo; horario de sueño en los defs; prioridad de dormir de noche; balance de hambre e invierno; el día en el registro de eventos | godot-engineer, sim-engineer, game-designer | 7–11 | Capturas al amanecer, mediodía, atardecer y noche; la aldea duerme de noche; los tests del invierno (`ColonyTests`) siguen en verde con el balance revisado |
| H2.3 Almacenes y acarreo | Spec; categorías de objetos en los defs; zonas y filtros; acarreo con reservas; apilado; consultas de almacén para la UI; orden de zona en la barra | game-designer, sim-engineer, godot-engineer, i18n-specialist | 12–16 | Escenario "troncos sueltos + almacén de madera ⇒ todos apilados en la zona"; zona dibujada y filtrada desde la UI |
| H2.4 Construcción | Spec; defs de construibles con costo y tiempo; planos con material; entrega de materiales; trabajo de construcción; desmontaje; canalizar; modelos de muros, suelos, puertas y rampas | game-designer, sim-engineer, art-integrator, godot-engineer, i18n-specialist | 22–28 | Escenarios "plano de muro + troncos en el almacén ⇒ muro construido" y "canal ⇒ hueco hacia abajo y paso libre"; una casa de cuatro muros con puerta, construida en la partida |
| H2.5 Muebles y camas | Defs de cama, mesa, silla y cofre; colocación con orientación; cofre como almacén; asignación de camas; descanso mejorado | game-designer, sim-engineer, art-integrator, godot-engineer, i18n-specialist | 8–10 | Escenario "colono con cama asignada descansa más rápido que en el suelo"; muebles colocados y vistos en la casa |
| H2.6 Gestión de la colonia | Panel de prioridades; lista de colonos; alertas de ocioso y de hambre crítica; argumentos de entidad en `TextResolver` | sim-engineer, godot-engineer, i18n-specialist | 10–12 | Cambiar una prioridad cambia qué trabajo toma un colono (test y partida); alertas en es y en con el nombre del colono |
| H2.7 Integración | Playtest con checklist, balance, rendimiento, bugs, exportación y release `v0.2.0` | qa-tester, game-designer, `/code-review`, humano | 8–10 | `v0.2.0` exportada y checklist de playtest completo |

## Pendientes de IP1 que entran en IP2

- Guardar el nombre de la aldea en la simulación: H2.1.
- Pantalla de error al arrancar (backlog): H2.1, junto con el error al cargar una partida.
- El día en el registro de eventos (backlog, hallazgo del playtest): H2.2.
- Volver a correr los tests del invierno con el sueño nocturno: H2.2.
- Argumentos de entidad y de def en `TextResolver`: H2.6, con las alertas, que son el primer texto que los usa.
- Suavizar el fondo del valle bajando `terrainAmplitude` en `worldgen.json`: se evalúa en H2.4, donde construir pide suelo parejo. Cambia las huellas.

Quedan en el backlog: variantes de aldeanos y animación de caminar, el arbusto que muestra las bayas, el cambio de modelos con la época y la agrupación de miles del español según CLDR.

## Definición de hecho de IP2

- Guardar a mitad de partida y seguir da la misma huella que no haber cortado, en al menos tres semillas y con construcciones, almacenes y camas en juego.
- Un archivo de otra versión o dañado se rechaza con un mensaje localizado, sin cerrar el juego.
- Una casa de muros con puerta, camas y un almacén se construye en una partida, sin tocar código.
- Los colonos duermen de noche y la colonia sigue pasando el invierno si se recolecta en otoño.
- El ciclo de día y noche no cambia las huellas de la simulación.
- Todo texto nuevo en es y en, con cambio de idioma en vivo.
- `dotnet test`, smoke test y test de determinismo en verde. Capturas es/en revisadas.
- Presupuesto: menos de 2 ms por tick de media y 60 FPS con la aldea construida en el mapa de IP1.
