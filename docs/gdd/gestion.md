# Gestión de la colonia (H2.6)

Spec de H2.6 de IP2: prioridades de trabajo por colono, una lista de la colonia con lo que hace cada uno, y alertas de colono ocioso y de hambre crítica.

## Prioridades de trabajo

- Cada colono tiene una prioridad por tipo de trabajo (minería, tala, recolección, construcción, acarreo): de **1** (la más alta) a **4** (la más baja), o **No**: ese colono no hace ese trabajo.
- Un colono sin prioridad propia para un tipo usa `AiConfig.defaultWorkPriority` (3). Así, en una colonia nueva todos hacen todo, como hasta ahora.
- **Elección:** al buscar trabajo, el colono recorre las prioridades de 1 a 4. En cada una, mira los tipos de trabajo que tiene con esa prioridad, en el orden de los defs (`order`), como hasta ahora. Toma el primer trabajo que encuentra. Las necesidades críticas siguen antes que cualquier trabajo y las bajas, después.
- **Comando:** `SetWorkPriorityCommand(colono, tipo de trabajo, prioridad)`, con prioridad de 0 (No) a 4. Un colono o un tipo que no existen se ignoran. No cambia el trabajo en curso: rige para la próxima elección.
- Las prioridades viven en el componente `WorkPriorities` del colono: una lista ordenada por tipo de trabajo, con sólo las que difieren de la de por defecto.

## Lista de la colonia

- Un panel, **Colonia (P)**, abre y cierra con la tecla P o con su botón en la barra de arriba.
- Una fila por colono, por nombre: el nombre, lo que hace ("Talando un árbol", "Sin tarea") y un botón por tipo de trabajo con su prioridad ("1" a "4" o "No").
- Un clic en el botón de prioridad pasa a la siguiente: 1 → 2 → 3 → 4 → No → 1. Clic derecho vuelve a la anterior. Cada cambio manda un `SetWorkPriorityCommand`; con el tiempo en pausa se aplica en el acto, como las órdenes.
- Un clic en el nombre selecciona al colono y la cámara no se mueve.
- El encabezado tiene el nombre de cada tipo de trabajo, y abajo hay una línea de ayuda.

## Alertas

Dos condiciones avisan al jugador. Cada una manda **un mensaje al registro** cuando empieza, y se muestra en la **lista de alertas del HUD** mientras dura.

| Alerta | Empieza cuando | Termina cuando | Gravedad |
|---|---|---|---|
| Colono ocioso | Un colono despierto pasa `AiConfig.idleAlertHours` (2) horas seguidas paseando: no encontró trabajo ni necesidad que atender | Toma cualquier otro trabajo, o se acuesta | Aviso |
| Hambre crítica | Una necesidad con `alertWhenCritical` (el hambre) baja de su umbral crítico | Vuelve a su umbral bajo o más arriba | Peligro |

- Un colono que muere deja de tener alertas.
- **Lista del HUD:** debajo de la barra de arriba, a la izquierda, una línea por alerta activa ("Elvira: sin tarea", "Sancho: hambre crítica"), en amarillo los avisos y en rojo los peligros. Un clic en una línea selecciona al colono. Sin alertas, la lista no se ve. Con el panel Colonia abierto se ven sólo los peligros: el panel ya dice qué colonos están ociosos (desde V2 de IP2.5).
- El estado de las alertas es parte de la simulación (se guarda y entra en la huella): el registro no repite un mensaje al cargar una partida.

## Textos con referencias

- `LocalizedText` ya acepta un `EntityId` como argumento. `TextResolver` lo expande: `{colonist}` con el nombre propio del colono y `{colonist_gender}` con `female` o `male`, para concordar el texto.
- El registro guarda los mensajes y los vuelve a traducir al cambiar de idioma. Como un colono puede morir después, el registro **fija** el nombre y el género al recibir el mensaje; el idioma sigue cambiando en vivo.
- Las referencias a defs (`{item}` con cantidad) siguen pendientes: ningún texto de IP2 las usa.

## Comandos, componentes y eventos

| Nombre | Qué es |
|---|---|
| `SetWorkPriorityCommand(colono, tipo, prioridad)` | Cambia una prioridad |
| `WorkPriorities` | Las prioridades propias de un colono |
| `Colonist.IdleSinceTick`, `Colonist.IdleAlerted` | Desde cuándo pasea y si ya avisó |
| `NeedState.CriticalAlerted` | Si la necesidad ya avisó que está crítica |
| `NotificationEvent` | Los mensajes de las alertas, con el colono como argumento |

- Un sistema nuevo, `AlertSystem`, al final del tick, lleva el estado de las alertas y emite los mensajes.
- `ColonyAlerts.Active(simulation)` es la consulta de solo lectura que arma la lista del HUD.

## Datos nuevos

| Def | Campo | Valor |
|---|---|---|
| `AiConfig` | `defaultWorkPriority` | 3 |
| `AiConfig` | `idleAlertHours` | 2 |
| `Need` | `alertWhenCritical` | `true` en el hambre |

## Textos nuevos

| Clave | es | en |
|---|---|---|
| `ui.colony.button` | Colonia (P) | Colony (P) |
| `ui.colony.title` | Colonia | Colony |
| `ui.colony.help` | Clic: la prioridad siguiente (1, 2, 3, 4, No); clic derecho: la anterior. 1 es la más alta; No, nunca | Click: next priority (1, 2, 3, 4, No); right-click: the previous one. 1 is the highest; No, never |
| `ui.colony.name` | Colono | Colonist |
| `ui.colony.activity` | Actividad | Activity |
| `ui.colony.priority` | {priority} | {priority} |
| `ui.colony.priority_off` | No | No |
| `event.colonist.idle` | {colonist} no tiene nada que hacer. | {colonist} has nothing to do. |
| `event.need.hunger.critical` | {colonist} tiene mucha hambre. | {colonist} is starving. |
| `alert.idle` | {colonist}: sin tarea | {colonist}: idle |
| `alert.need.hunger` | {colonist}: hambre crítica | {colonist}: starving |

- `event.need.<necesidad>.critical` y `alert.need.<necesidad>` existen para cada necesidad con `alertWhenCritical`; los tests de contenido lo verifican.

## Criterios de aceptación

1. Escenario: un colono con minería en 1 y tala en 3 excava antes de talar; con tala en 1 y minería en 3, tala primero.
2. Escenario: un colono con acarreo en No no acarrea aunque haya pilas sueltas y un almacén; con acarreo en 4, sí.
3. Escenario: un colono sin nada que hacer avisa una vez a las 2 horas de pasear; al darle trabajo, la alerta termina, y vuelve a avisar si queda ocioso de nuevo.
4. Escenario: un colono con hambre crítica avisa una vez; al comer hasta el umbral bajo, la alerta termina.
5. El test de continuidad del guardado incluye prioridades cambiadas y alertas activas.
6. `TextResolver` expande un colono en nombre y género; el registro conserva el nombre de un colono que murió después del mensaje.
7. En la partida: cambiar una prioridad desde el panel cambia qué trabajo toma el colono; las alertas se ven en el HUD y en el registro con el nombre del colono, en es y en.
