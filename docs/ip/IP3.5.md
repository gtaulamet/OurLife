# IP3.5: Libre albedrío

**Objetivo.** Que los colonos tengan iniciativa: que, sin órdenes que tomar, hagan por su cuenta las tareas que la aldea necesita según sus prioridades, su personalidad y su ánimo, y que busquen a quienes aprecian y eviten a quienes no soportan. Al terminar, una aldea sin órdenes se mantiene sola en lo básico y las amistades se forman en días.

**Origen.** Pedido del usuario del 16 de octubre de 2026, durante IP3. Propuesta y razones en `docs/gdd/libre-albedrio.md`. Decisiones del usuario:

- Se prende y se apaga para toda la aldea desde el menú de Opciones.
- Por ahora, la iniciativa sólo recolecta comida, tala y acarrea. Excavar y construir quedan en manos del jugador.
- Cuánto alcanza de cada cosa lo elige el jugador en el juego.
- La iniciativa respeta las prioridades de cada colono: con una tarea en "nunca", nunca la hace por su cuenta.
- Va como IP3.5, después de IP3 y antes de IP4.

**Estimación:** 32 a 46 h. **Versión al cerrar:** `v0.3.5`, para Windows y macOS.

**Estado:** planificada; empieza al cerrar IP3.

## Estado de los hitos

| Hito | Estado | Rama | Notas |
|---|---|---|---|
| L1 Spec y contrato | Pendiente | `ip3.5/l1-spec` | |
| L2 Interruptor y metas | Pendiente | `ip3.5/l2-metas` | |
| L3 Trabajo por iniciativa | Pendiente | `ip3.5/l3-iniciativa` | |
| L4 Vida social con intención | Pendiente | `ip3.5/l4-social` | |
| L5 Integración | Pendiente | `ip3.5/l5-integracion` | |

**Ruta crítica:** L1 → L2 → (L3 ∥ L4) → L5.

## Hitos en detalle

| Hito | Contenido | Agentes | Horas | Entregable verificable |
|---|---|---|---|---|
| L1 Spec y contrato | Spec en `docs/gdd/libre-albedrio.md` con criterios de aceptación; defs de tareas por iniciativa y de la configuración; comandos para el interruptor y las metas | game-designer, Plan, humano | 4–6 | Spec aprobada por el usuario |
| L2 Interruptor y metas | Opción "Libre albedrío" en Opciones, guardada en `settings.cfg` y aplicada a la partida con un comando; metas de existencias por categoría elegidas en el juego (por ejemplo desde las existencias del HUD), guardadas con la partida | sim-engineer, godot-engineer, i18n-specialist | 6–8 | Test: el comando prende y apaga la iniciativa y se guarda; capturas es/en de la opción y de las metas |
| L3 Trabajo por iniciativa | Tareas por datos (recolectar comida, talar árboles sueltos cerca de la aldea, acarrear) con su condición de existencias; sólo en el tiempo libre y dentro de las prioridades; ganas por la diligencia y el ánimo | sim-engineer, game-designer | 10–14 | Escenario: sin órdenes y con poca comida, un colono recolecta por su cuenta; con recolectar en "nunca", no; con la opción apagada, no |
| L4 Vida social con intención | En el tiempo libre, buscar a quienes aprecia (amigos, familia, pareja, deseos de amistad o de reconciliarse) y alejarse de los rivales; ganas por la sociabilidad y el ánimo | sim-engineer | 8–12 | Escenario: dos colonos que se aprecian, en trabajos lejanos, se hacen amigos en días; un colono se aleja de su rival |
| L5 Integración | Balance de iniciativa y de vida social en una aldea real; benchmark; checklist de playtest; paquetes y release `v0.3.5` | qa-tester, game-designer, `/code-review`, humano | 4–6 | Checklist completo y `v0.3.5` publicada |

## Decisiones de diseño a cerrar en L1

- **Opción en Opciones y simulación:** la simulación tiene que saber si el libre albedrío está prendido, para que dos partidas iguales den lo mismo y el guardado lo conserve. Propuesta: la preferencia vive en `settings.cfg` y, al cambiarla o al cargar una partida, la sesión le manda un comando a la simulación con el valor.
- **Dónde se eligen las metas:** desde las existencias de la barra superior, desde el panel Colonia o en un panel propio.
- **Qué cuenta como "cerca de la aldea"** para talar por iniciativa: un radio alrededor de los almacenes, o de la llegada.

## Riesgos

- **El jugador pierde el control:** un colono que tala un árbol que el jugador quería conservar. Mitigación: sólo cerca de la aldea, el interruptor y las metas.
- **Rendimiento:** buscar con quién estar y qué hacer recorre a todos los colonos; se hace cada tanto y con un radio.
- **Balance:** demasiada iniciativa vacía el valle de árboles; muy poca no se nota. Se ajusta en L5 con partidas largas.
