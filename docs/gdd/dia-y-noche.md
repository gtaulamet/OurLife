# Día, noche y sueño (H2.2)

Spec de H2.2 de IP2: el ciclo visual de día y noche, el sueño nocturno de los colonos y el día en el registro de eventos.

## Sueño nocturno

Hoy un colono duerme cuando el descanso baja del umbral bajo y no tiene trabajo, o cuando baja del crítico, y despierta con el descanso lleno. Así el sueño se corre cada día y la aldea duerme a cualquier hora.

**Regla nueva.** La noche es un tramo de horas que fijan los defs (`AiConfig`, `nightStartHour` y `nightEndHour`; en el contenido, de 21:00 a 06:00):

| Momento | Cuándo se acuesta un colono | Cuándo se levanta |
|---|---|---|
| Noche | Si su descanso está por debajo del umbral nocturno de la necesidad (`nightThreshold` en `needs.json`: 9000 de 10 000), antes que cualquier trabajo | Cuando termina la noche y el descanso está lleno |
| Día | Sólo si el descanso está por debajo del umbral crítico: un colono agotado duerme igual | Con el descanso lleno |

- De día el umbral bajo del descanso ya no lleva a dormir: así nadie duerme la siesta y se desvela.
- Un colono que se llena antes del amanecer sigue durmiendo hasta que termine la noche.
- El hambre crítica sigue despertando a un colono que duerme, como en IP1.
- Una necesidad que se satisface durmiendo sin `nightThreshold` usa su umbral bajo de noche.
- Un tramo que cruza la medianoche (21 → 6) y uno que no (1 → 5) valen los dos. Inicio y fin iguales es un error del def.

**Balance.** Con la noche de 9 horas, el descanso tiene que alcanzar: baja 500 por hora despierto (15 h × 500 = 7500) y sube 1200 por hora dormido (9 h × 1200 = 10 800). Un colono llega a la noche con unos 2500 y amanece lleno. `needs.json` pasa de 600 a 500 de pérdida por hora y de 900 a 1200 de recuperación. Los tests del invierno (`ColonyTests`) se vuelven a correr con el horario nuevo.

## Ciclo visual

Es de presentación: no cambia la simulación ni las huellas.

- El sol gira alrededor del valle según la hora del juego: sale cerca de las 6, está alto al mediodía y se pone cerca de las 21. De noche, una luz de luna fría y débil toma su lugar.
- El color y la fuerza del sol, del ambiente y del cielo pasan por cuatro momentos: amanecer, día, atardecer y noche, mezclados de a poco.
- La noche nunca queda a oscuras del todo: el ambiente nocturno deja ver el terreno y los colonos.
- Se puede apagar en Opciones ("Ciclo de día y noche: Sí | No"). Apagado, la luz es la del mediodía de IP1. La preferencia se guarda.
- Los números de la luz son de presentación y viven en el código de la vista, como los del resto del render (ADR-009).

## El día en el registro

Hallazgo del playtest de IP1: en una partida larga no se sabe de qué día es cada mensaje. Cuando el día del mensaje cambia respecto del anterior, el registro agrega una línea con la fecha ("Día 3 de primavera de 1200"); cada mensaje sigue mostrando sólo la hora.

## Textos nuevos

| Clave | es | en |
|---|---|---|
| `ui.options.day_night` | Ciclo de día y noche | Day and night cycle |
| `ui.common.on` | Sí | On |
| `ui.common.off` | No | Off |
| `ui.log.day` | — {date} — | — {date} — |

## Línea de comandos

- `--hour <h>` lleva una partida de `--play` o `--load` hasta esa hora antes de mostrarla, para capturas del ciclo.

## Criterios de aceptación

1. Test: con colonos descansados y trabajo pendiente, a las 21:00 dejan el trabajo y se acuestan; se levantan a las 06:00.
2. Test: de día, un colono con el descanso bajo pero no crítico no se acuesta; con el descanso crítico, sí.
3. Test: un colono que se llena a las 03:00 sigue durmiendo hasta las 06:00.
4. Los tests del invierno de `ColonyTests` siguen en verde con el balance nuevo, y los colonos sobreviven 30 días en las semillas 1 y 42.
5. Capturas al amanecer, al mediodía, al atardecer y de noche; la noche se ve.
6. Con el ciclo apagado, la luz es la del mediodía. La opción persiste.
7. El registro muestra la línea del día cuando cambia, en es y en.
8. El ciclo no cambia la huella de la simulación.
