# Libre albedrío

**Estado:** dirección aprobada por el usuario el 16 de octubre de 2026; va como IP3.5 (`docs/ip/IP3.5.md`). La spec con criterios de aceptación se escribe en L1.

## Qué pidió el usuario

1. Si un colono tiene hambre, busca comida; si no hay, la recolecta.
2. Si no tiene nada que hacer, según su personalidad y su ánimo, puede hacer por su cuenta las tareas de las prioridades que tiene asignadas.
3. Según el vínculo con los demás, puede querer estar con ciertas personas para fortalecer el lazo, o alejarse de las conflictivas.

## Qué hace el juego hoy

Parte del pedido ya existe; conviene saberlo para no rehacerlo:

| Conducta | Hoy | Desde |
|---|---|---|
| Comer con hambre | Come de una pila; si no hay, recolecta un arbusto con comida aunque nadie lo haya ordenado | IP1 |
| Trabajar sin que se lo digan colono por colono | Toma las órdenes del jugador según sus prioridades (1 a 4 o nunca) | IP2 |
| Personalidad en las decisiones | Un perezoso atiende sus necesidades bajas antes de trabajar; la diligencia da la velocidad | H3.3 |
| Ánimo en las decisiones | En crisis no trabaja; aislado, se aleja de los demás | H3.6 |
| Sin nada que hacer | Deambula cerca de donde está | IP1 |
| Vida social | Interactúa con quien pasa cerca (4 celdas); no busca a nadie | H3.5 |

Lo que falta es la **iniciativa**: que el colono decida hacer algo que nadie ordenó, y que busque o evite a otros.

## Propuesta: tres partes

### A. Trabajo por iniciativa

Un colono sin órdenes que tomar puede inventarse trabajo dentro de sus prioridades:

- Sólo tareas que no cambian la aldea sin permiso: recolectar comida, talar árboles sueltos cerca de la aldea, acarrear. Excavar y construir siguen siendo del jugador, porque cambian el mapa y gastan materiales.
- Cada tarea por iniciativa es un def con su condición, por ejemplo "si hay menos de N comidas en la aldea, recolectar arbustos a menos de M celdas". Los umbrales en datos, o elegidos por el jugador.
- La personalidad decide las ganas: un trabajador toma iniciativa seguido, un perezoso casi nunca. El ánimo también: con ánimo bajo, menos iniciativa.
- Las órdenes del jugador siempre van primero: la iniciativa sólo llena el tiempo libre.

### B. Vida social con intención

En el tiempo libre (sin órdenes, o de noche antes de dormir):

- **Buscar compañía:** un colono sociable, o con un deseo de hacer una amistad o de reconciliarse, camina hacia alguien con quien tiene buena opinión, familia o pareja, y se queda cerca un rato. Así las interacciones de H3.5 ocurren más, y las amistades se forman en días y no en semanas (hoy hay unas 3 interacciones por día entre cinco colonos).
- **Evitar conflictos:** con una rivalidad, al deambular elige lugares lejos del rival, como ya hace quien se aísla en una crisis.
- **Lugar de reunión:** con mesas o un fogón (IP4), los colonos libres se juntan ahí al atardecer.

### C. Preferencias por personalidad y ánimo

- Cada rasgo puede sumar ganas a una clase de conducta libre, como ya suma peso a las interacciones: sociabilidad a buscar compañía, diligencia a la iniciativa, curiosidad a acercarse a un maestro.
- Ánimo muy bajo: menos iniciativa y más aislamiento; ánimo alto: más ganas de compañía.

## Decisiones del usuario

Respondidas el 16 de octubre de 2026: el interruptor va en Opciones, para toda la aldea; por ahora sólo recolectar, talar y acarrear; el jugador elige en el juego cuánto alcanza; la iniciativa respeta las prioridades; y va como IP3.5, con las tres partes (el balance de H3.10 se hace sin ellas). Como la simulación tiene que saber si está prendido (determinismo y guardado), la preferencia de Opciones llega a la partida como un comando, al cambiarla y al cargar; se cierra en L1.

## Preguntas que se hicieron

1. **Interruptor:** ¿el libre albedrío se prende y apaga para toda la aldea, por colono, o siempre está?
2. **Qué puede hacer por su cuenta:** ¿sólo recolectar, talar y acarrear, o también otras tareas? ¿Excavar y construir quedan siempre en manos del jugador?
3. **Umbrales:** ¿cuánta comida o madera "alcanza"? ¿Fijo en los datos o lo elige el jugador (por ejemplo "mantener 50 comidas")?
4. **Prioridades:** ¿la iniciativa respeta las prioridades de cada colono (si talar está en "nunca", nunca tala por su cuenta)? La propuesta dice que sí.
5. **Cuándo:** ¿una IP corta propia después de IP3, dentro de IP4, o repartida?

## Dónde podría ir

| Opción | Qué entra | Ventaja | Contra |
|---|---|---|---|
| IP3.5 "Libre albedrío", después de IP3 | A, B y C | Usa todo lo de IP3 y arregla el balance social | Corre IP4 unas tres semanas |
| Parte en H3.10, el resto en IP4 | B (buscar y evitar) en el balance de H3.10; A y el lugar de reunión en IP4 | Lo social se arregla ya; la iniciativa llega con los oficios y el comedor | La iniciativa espera a IP4 |
| Dentro de IP4 | Todo | IP4 trae agricultura, existencias y comedor, con los que la iniciativa tiene más sentido | La vida social sigue lenta hasta IP4 |

Elegida: IP3.5, con las tres partes.

## Estimación preliminar

| Parte | Horas |
|---|---|
| A. Trabajo por iniciativa, con defs de tareas y umbrales | 12–18 |
| B. Buscar compañía, evitar rivales y lugar de reunión | 10–14 |
| C. Preferencias por rasgos y ánimo | 4–6 |
| Interruptor, textos, tests de escenario y balance | 6–8 |
| **Total** | **32–46** |

## Riesgos

- **El jugador pierde el control:** un colono que tala por su cuenta un árbol que el jugador quería conservar. Mitigación: zonas prohibidas, el interruptor, y que la iniciativa no toque lo que está lejos de la aldea.
- **Rendimiento:** buscar con quién estar recorre a todos los colonos; con una aldea grande hay que hacerlo cada tanto y con un radio.
- **Determinismo:** como todo en Core, con su propio flujo de RNG y recorriendo los colonos por `EntityId`.
