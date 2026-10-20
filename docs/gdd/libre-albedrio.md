# Libre albedrío (IP3.5)

Spec de IP3.5 (`docs/ip/IP3.5.md`): trabajo por iniciativa, metas de existencias, vida social con intención, preferencias por rasgos y ánimo, y bosque que vuelve. Las estaciones visibles (L4c) tienen su spec en `docs/gdd/estaciones-visibles.md`. Los números son el punto de partida, viven en defs y se ajustan en L5 con partidas largas.

**Estado:** aprobada por el usuario el 19 de octubre de 2026 (L1), con dos cambios: la aldea es un círculo de 30 celdas alrededor de la llegada, y en otoño los pinos también cambian de color. La dirección se aprobó el 16 de octubre.

## Qué pidió el usuario

1. Si un colono tiene hambre, busca comida; si no hay, la recolecta.
2. Si no tiene nada que hacer, según su personalidad y su ánimo, puede hacer por su cuenta las tareas de las prioridades que tiene asignadas.
3. Según el vínculo con los demás, puede querer estar con ciertas personas para fortalecer el lazo, o alejarse de las conflictivas.
4. Que los árboles vuelvan a crecer sin llenar el mapa (17 de octubre).

Decisiones del usuario: el interruptor va en Opciones, para toda la aldea; por ahora la iniciativa sólo recolecta comida, tala y acarrea; las metas de existencias las elige el jugador en el panel Colonia; la iniciativa respeta las prioridades; "cerca de la aldea" es un círculo de 30 celdas alrededor de la llegada (19 de octubre; el 17 se había dicho 40 de diámetro).

## Qué hace el juego hoy

| Conducta | Hoy | Desde |
|---|---|---|
| Comer con hambre | Come de una pila; si no hay, recolecta un arbusto con comida aunque nadie lo haya ordenado | IP1 |
| Acarrear | Lleva lo suelto a los almacenes sin que nadie lo ordene, si tiene la prioridad | IP2 |
| Trabajar | Toma las órdenes del jugador según sus prioridades (1 a 4 o nunca) | IP2 |
| Personalidad y ánimo | Un perezoso atiende sus necesidades bajas antes de trabajar; en crisis no trabaja; aislado, se aleja | H3.3, H3.6 |
| Sin nada que hacer | Un niño va a jugar; los demás deambulan cerca de donde están | IP1, H3.7 |
| Vida social | Interactúa con quien pasa a 6 celdas o menos; no busca a nadie | H3.5, H3.10 |

Así, el pedido 1 ya está y el acarreo ya es por iniciativa. Lo nuevo es recolectar y talar sin órdenes, buscar y evitar a otros, y el bosque que vuelve.

## Interruptor

- **Opción "Libre albedrío"** en Opciones (sí o no), para toda la aldea. **Prendida por defecto.** Se guarda en `settings.cfg`, como el resto de las opciones.
- La simulación tiene que saberlo, para que dos partidas iguales den lo mismo y para que el guardado lo conserve. La sesión le manda `SetFreeWillCommand(prendido)` al empezar o cargar una partida y cada vez que el jugador cambia la opción. La simulación guarda el valor (`Simulation.FreeWill`), que entra en la huella con la opción prendida (ver "Guardado").
- Con la opción apagada no hay trabajo por iniciativa ni vida social con intención: los colonos se comportan como en `v0.3.0`. El bosque que vuelve no depende de la opción: es la naturaleza.

## Aldea y metas

- **Centro de la aldea:** el lugar de llegada de los colonos, fijo (`Simulation.VillageCenter`). "Cerca de la aldea" es a 30 celdas o menos de él en el plano, en cualquier nivel.
- **Metas de existencias:** una por categoría de objetos que la iniciativa junta, **comida** y **madera**. La meta es cuántas unidades quiere tener la aldea (las que cuenta el HUD: en el suelo, en almacenes o en las manos). Los valores por defecto están en los datos (comida 600, madera 40) y el jugador los cambia en el panel Colonia en un campo donde escribe la cantidad, de 0 hasta 100 000 (pedido del usuario del 20 de octubre). El campo sólo toma dígitos ("10.000" queda en 10000). Mientras el jugador escribe, las teclas del juego (velocidades, órdenes, cámara) no actúan; la meta se aplica con Enter, Tab o un clic afuera, y Esc vuelve a la meta anterior. Un número más alto que el máximo queda en el máximo. La línea dice "Comida: 42 de [150]": el campo va después del texto en todos los idiomas; uno que necesite otro orden partirá la clave. Con meta 0, esa iniciativa no ocurre.
- `SetStockTargetCommand(categoría, cantidad)` guarda la meta en la partida (`Simulation.StockTargets`).

## A. Trabajo por iniciativa

Un colono libre, sin órdenes que tomar, puede inventarse trabajo:

| Tarea | Tipo de trabajo | Cuándo | Qué hace |
|---|---|---|---|
| Recolectar (`gather_food`) | Recolección | Comida de la aldea bajo su meta | Recolecta el arbusto con comida más cercano, sin recolectar, cerca de la aldea, aunque nadie lo haya ordenado |
| Talar (`chop_wood`) | Tala | Madera de la aldea bajo su meta | Tala el árbol más cercano cerca de la aldea, aunque nadie lo haya ordenado |
| Acarrear | Acarreo | Ya ocurre sin órdenes desde IP2 | Sin cambios |

- **Cuándo decide:** después de las necesidades críticas, de las órdenes del jugador por prioridad y de las necesidades bajas; antes de jugar (niños), de la vida social con intención y de deambular. Las órdenes del jugador siempre van primero. Un trabajo por iniciativa cede a otro trabajo o a una necesidad baja mientras el colono va hacia la planta; ya trabajando en ella, termina (sólo una necesidad crítica lo corta), así no se pierde lo hecho.
- **Prioridades:** la tarea usa la prioridad del colono para su tipo de trabajo. Con "nunca", nunca la hace. Entre varias tareas posibles, la de prioridad más alta; con la misma, la de la meta más lejos de cumplirse.
- **Etapas:** sólo quien puede hacer ese trabajo: un adolescente recolecta (trabajo liviano) pero no tala; un niño no hace ninguna.
- **Ganas:** cada vez que decide, la tarea ocurre con una probabilidad: `initiativeChancePercent` (60 %) por la diligencia (de 50 % a 150 % del valor, de la diligencia más baja a la más alta) y por el ánimo (×50 % con ánimo bajo 30, ×120 % con ánimo sobre 70). Si no ocurre, deambula y vuelve a decidir en la próxima revisión, como hoy.
- **Reparto (L3):** cuánto le falta a una meta cuenta lo que ya traerá el trabajo por iniciativa en curso (los troncos del árbol que alguien tala, las bayas del arbusto que alguien recolecta). Así, con suficientes colonos talando para llegar a la meta de madera, el siguiente recolecta comida. Cuánto le falta se mide como parte de la meta (de 0 % a 100 %).
- **Lo que junta:** recolectar o talar por iniciativa deja lo obtenido en el suelo, como con una orden; los acarreadores lo llevan al almacén. Ganan experiencia como en una orden.
- **Lo que no toca:** un arbusto o árbol con una orden del jugador ya tiene quien lo haga; uno lejos de la aldea, nunca. No excava ni construye.
- **Cuando se cumple la meta**, deja de tomar esa tarea; el trabajo en curso termina.
- En crisis no hay iniciativa.

## B. Vida social con intención

En el tiempo libre, cuando no hay órdenes ni iniciativa:

- **Buscar compañía:** con una probabilidad (`seekChancePercent`, 30 %, por la sociabilidad de 50 % a 150 % y por el ánimo), un colono camina hacia alguien que aprecia, a 30 celdas o menos y en su región, y se queda cerca un rato, como un niño que juega (el trabajo `Visit`, que espera lo mismo que deambular). Así las interacciones ocurren más. El inspector dice "Pasando un rato con alguien".
- **Quién busca:** cualquiera que pueda interactuar (despierto, de una etapa que interactúa y sin aislarse) y no esté en crisis, salvo un bebé que gatea. Un niño primero intenta jugar; si no tiene con quién, sólo busca a su familia.
- **A quién:** con peso doble la pareja, la familia y los amigos (el mismo `closeWeightPercent` que usan las interacciones para elegir con quién hablar, 200 %); luego quien tenga una opinión de 20 o más (`seekMinOpinion`) y el objetivo de un deseo de hacer una amistad o de reconciliarse. Nunca a un rival: alguien con quien uno de los dos tiene una opinión de -40 o menos, aunque sea de la familia (una pelea puede agriar a la familia, aunque el olvido no la baje de 30), y tampoco cuando desea reconciliarse con él. Ni a alguien dormido o que va a dormir, o en crisis de aislamiento, ni a quien vio en las últimas 2 horas (como al jugar), ni a quien ya viene a acompañarlo o a jugar con él. Si el otro está en camino, por lo que sea, va adonde el otro va, y no lo busca si eso queda a más de 30 celdas.
- **Cuándo deja de acompañar:** como el que juega, un trabajo o una necesidad baja lo interrumpen; la iniciativa no. También termina si el otro se va a dormir o se aísla, si el visitante entra en crisis, si se vuelven rivales, si, ya esperando, el otro sale para un lugar a más de 6 celdas (el radio de las interacciones), o si se apaga la opción. En camino, si el otro ya no va a estar al alcance de adonde va, lo sigue (lo mira en cada tick, así no llega adonde no hay nadie), mientras siga a 30 celdas o menos y haya cómo llegar; si no, la visita termina y pasea.
- **Evitar a los rivales:** al deambular, de los lugares que sortea elige el más lejos de sus rivales que estén a 10 celdas o menos, como quien se aísla en una crisis; también de un familiar con quien se peleó. Sólo los de su nivel, porque se aleja por su nivel. Con la opción prendida, un niño tampoco va a jugar con un rival, ni sigue jugando con alguien que se volvió su rival. Cuenta dónde está cada rival y, si camina, adónde va: así dos rivales que se esquivan no se van para el mismo lado.
- Acompañar a alguien sigue siendo no tener trabajo: el aviso de "no tiene nada que hacer" vale igual que al deambular.
- El lugar de reunión (fogón, mesas) llega con IP4.

## C. Preferencias por rasgos y ánimo

- Las ganas de cada conducta libre salen de un rasgo, por datos: diligencia para la iniciativa, sociabilidad para buscar compañía.
- Ánimo bajo (menos de 30): la mitad de ganas de trabajar por iniciativa y de buscar compañía. Ánimo alto (más de 70): un 20 % más.
- Los números en el def nuevo `FreeWillConfig`.

## D. Bosque que vuelve

- **Retoños:** a medianoche, cada árbol adulto de una especie que puede volver a crecer tiene una probabilidad (`seedChancePerDay`, 2 %) de dejar un retoño en una celda libre a 3 celdas o menos: pasto o tierra a cielo abierto, en la banda de altura donde la especie crece en el valle.
- **Crecer:** un retoño se ve chico y no bloquea el paso; en `growDays` días del juego (10) se vuelve árbol adulto, que bloquea el paso y se puede talar. Un retoño no se puede talar ni da madera.
- **Tope:** cada especie tiene un máximo de celdas ocupadas, árboles y retoños, como porcentaje de las celdas donde puede crecer: el mismo con que la genera el valle (robles 3,5 %, pinos 4 %). Llegado el tope, no deja retoños.
- **Dónde no:** nunca sobre construcciones, muebles, planos, almacenes, celdas con órdenes del jugador, objetos, colonos ni otras plantas.
- **No en invierno:** las especies que duermen en invierno no dejan retoños; los retoños siguen creciendo.
- Los arbustos de bayas no se reproducen en IP3.5: la comida ya tiene su tope y su balance.
- Sembrar a pedido del jugador queda para la agricultura de IP4.
- Usa el flujo aleatorio `nature.forest`, recorre los árboles por `EntityId` y cuenta las celdas posibles una vez por partida.

## Contrato con la presentación

| Tipo | Nombre | Uso |
|---|---|---|
| Comando | `SetFreeWillCommand(prendido)` | El interruptor de Opciones, al empezar, al cargar y al cambiarlo |
| Comando | `SetStockTargetCommand(categoría, cantidad)` | Las metas del panel Colonia |
| Estado | `Simulation.FreeWill`, `Simulation.VillageCenter`, `Simulation.StockTargets` | Entran en el guardado, y en la huella con la opción prendida o con metas elegidas (ver "Guardado") |
| Componente | `Sapling` | En un retoño: el tick en que se vuelve adulto |
| Campo | `Job.Initiative` | El trabajo es por iniciativa: vale sin la orden del jugador |
| Trabajo | `JobKind.Visit` | Ir con alguien que aprecia y quedarse cerca un rato |
| Consulta | `FreeWillRules.Targets`, `FreeWillRules.IsNearVillage` | El panel Colonia y el inspector |
| Evento | `EntitySpawnedEvent`, `EntityChangedEvent` | Un retoño aparece; un retoño crece |
| Texto | `job.<trabajo>.initiative` | El inspector dice "Recolectando por su cuenta" |
| Texto | `job.visit.report` | El inspector dice "Pasando un rato con alguien" |

## Defs nuevos y cambios

| Def | Campos | Archivo |
|---|---|---|
| `FreeWillConfig` | `villageRadius` (30), `initiativeChancePercent` (60), `initiativeTrait` (`diligence`), `seekChancePercent` (30), `seekTrait` (`sociability`), `seekRadius` (30), `seekMinOpinion` (20), `avoidRadius` (10), bandas de ánimo, `defaultTargets` por categoría | `free_will.json` |
| `InitiativeTask` | `job` (`harvest` o `chop`; el tipo de trabajo es el que agrupa ese trabajo), `category` (la meta que mira) | `free_will.json` |
| `PlantDef` | `regrowth`: `seedChancePerDay`, `seedRadius`, `growDays`, `maxCoverPercent`, `saplingVisual` | `plants.json` |

- Cada def con textos lleva `name`, `desc` y `gender` en es y en. El retoño necesita un modelo en el catálogo de visuales (el árbol en chico).

## Guardado

- Formato 8 (L2): `Simulation.FreeWill`, `VillageCenter` y `StockTargets`.
- Formato 9 (L3): `Job.Initiative`. Sólo se escribe cuando es verdadero, así una partida sin trabajo por iniciativa se escribe y da la huella de antes. No hay nada que migrar.
- Formato 10 (L4): el trabajo `Visit`, que una versión anterior no sabe leer. No hay nada que migrar. El componente `Sapling` llega con L4b, que vuelve a subir el formato (ADR-013: sube cada vez que llega a `main` un cambio del estado).
- Una partida de la versión 7 se migra: centro de la aldea en la celda del colono más cerca del medio de todos (la mediana en cada eje), así uno que trabaja lejos no lo corre; metas por defecto; el interruptor lo manda la sesión al cargar, como siempre.
- Con la opción apagada y sin metas elegidas, la huella no incluye el libre albedrío: una partida da la misma huella que en `v0.3.0` (criterio 3). El smoke, que juega sin la opción, lo comprueba con las huellas de `v0.3.0`.
- Una meta guardada que el contenido ya no admite (más alta que el máximo, o de una categoría que ya no tiene metas) baja al máximo o se descarta al cargar; la partida carga igual. Una categoría que ya no existe da "falta contenido", como cualquier def (ADR-013).
- El centro migrado es la celda de un colono: siempre alcanzable, aunque pueda estar bajo tierra. Sólo se usa en el plano ("cerca de la aldea"), así que el nivel no cambia nada.

## Criterios de aceptación

1. Sin órdenes, con la comida bajo la meta y arbustos cerca de la aldea, un colono con prioridad de recolección recolecta por su cuenta; con la meta cumplida, no.
2. Con la recolección en "nunca" para ese colono, no recolecta por su cuenta; otro colono sí.
3. Con la opción apagada, nadie trabaja por su cuenta ni busca compañía, y la partida da la misma huella que sin IP3.5 en esa situación.
4. Un árbol a más de 30 celdas del centro de la aldea nunca se tala por iniciativa; uno con orden del jugador lo toma quien toma órdenes.
5. Una orden del jugador siempre gana: un colono libre con una orden pendiente de su prioridad la toma antes que cualquier iniciativa.
6. Un perezoso toma iniciativa menos seguido que un trabajador en el mismo escenario (medido en muchas decisiones).
7. Dos colonos que se aprecian, trabajando lejos uno del otro, se encuentran en su tiempo libre y se hacen amigos en menos días que sin IP3.5.
8. Un colono no se acerca a su rival al deambular: en un escenario con un rival a 5 celdas, termina más lejos de él que al empezar.
9. Un valle con los árboles talados vuelve a tener bosque con el tiempo, sin pasar el tope de cada especie; misma semilla, mismo bosque.
10. Ningún retoño aparece sobre una construcción, un almacén, un mueble, un plano, una celda con orden u otra planta.
11. El interruptor y las metas se guardan y cargan; una partida de `v0.3.0` carga con el centro de la aldea y las metas por defecto.
12. En la partida: la opción en Opciones y las metas en el panel Colonia, en es y en; el inspector dice "por su cuenta".
13. Presupuesto: menos de 2 ms por tick de media con una aldea de diez años y la opción prendida.

## Decisiones del usuario (L1)

Respondidas el 19 de octubre de 2026:

1. La opción viene **prendida** por defecto en una instalación nueva. También al actualizar desde `v0.3.0`, cuyo `settings.cfg` no la tiene: el jugador la apaga en Opciones si no la quiere.
2. Metas por defecto: **600 de comida y 40 de madera**. Al principio la de comida era 150; el 20 de octubre, con L3, se vio que sin órdenes del jugador la aldea se extinguía en el primer invierno en dos de tres semillas, y el usuario eligió 600, con la que sobrevive en las tres. El mismo día pidió que la meta sea un campo que el jugador completa, con máximo 100 000.
3. El centro de la aldea es el lugar de llegada, fijo; "cerca" son **30 celdas** alrededor (se propusieron 20).
4. Ganas de iniciativa: 60 % para un colono promedio en cada decisión libre; un perezoso, la mitad; un trabajador, una vez y media.
5. Buscar compañía hasta 30 celdas; evitar a los rivales a 10 o menos.
6. Bosque: retoños sólo junto a árboles adultos de su especie, 2 % por árbol y por día, 10 días para crecer; los arbustos de bayas no se reproducen.
7. Estaciones visibles: el pasto se blanquea de a poco hacia el centro del invierno y hay nieve en la roca en el último tercio de la altura del valle. Además, en otoño **los robles y los pinos** se vuelven amarillos y rojizos, y en invierno tienen algo de nieve: el cambio de estación tiene que notarse en el paisaje (`docs/gdd/estaciones-visibles.md`).

## Fuera de alcance

Van a `docs/ip/backlog.md`: lugar de reunión con fogón y mesas (IP4), sembrar árboles a pedido (IP4), zonas donde la iniciativa no tala, iniciativa de excavar o construir, y libre albedrío por colono.
