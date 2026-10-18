# Vida y generaciones (IP3)

Spec de IP3 (`docs/ip/IP3.md`): edad y etapas de la vida, ritmo de la vida, personalidad, habilidades, interacciones, relaciones y familia, pensamientos y ánimo, deseos, parejas, nacimientos, crianza y muerte por vejez. Las decisiones de diseño del usuario están en `docs/ip/IP3.md`. Los números son el punto de partida, viven en defs y se ajustan en H3.10 con pruebas de juego.

**Estado:** aprobada por el usuario el 16 de octubre de 2026 (H3.1).

## Tiempo de la vida

- El calendario no cambia: un día del juego dura 1440 ticks (2,4 minutos a 1×) y un año, 60 días (unas 2,4 horas a 1×).
- **Ritmo de la vida:** cuántos días de vida pasan en un día del juego. Se elige en Nueva partida, se guarda con la partida y no cambia después. Lo define el def nuevo `LifePace`:

| Ritmo | `lifeDaysPerDay` | Un bebé llega a adulto en | Una vida de 75 años dura | Motivo |
|---|---|---|---|---|
| Normal (`normal`) | 1 | 18 años del juego, unas 43 h | unas 180 h | Como RimWorld: pocas generaciones por partida |
| Rápido (`fast`), por defecto | 4 | 4,5 años del juego, unas 11 h | unas 45 h | Se ven nietos en una partida larga |
| Muy rápido (`very_fast`) | 8 | 2,25 años del juego, unas 5,4 h | unas 22 h | Para ver pasar generaciones |

- **Edad:** cada colono tiene `ageDays`, sus días de vida. A medianoche suma `lifeDaysPerDay`. Sus años son `ageDays / 60` (el año de la vida tiene los días del año del calendario).
- Los tiempos de la vida (etapas, embarazo, fertilidad, vejez) se cuentan en días de vida. Los de la rutina (necesidades, pensamientos, deseos, crisis) se cuentan en horas y días del juego, porque los vive el jugador.

## Etapas de la vida

Def nuevo `LifeStage`. El código no conoce los ids: sólo lee los campos.

| Etapa | Desde | Trabaja | Velocidad de trabajo | Movimiento | Hambre | Descanso | Interactúa | Motivo |
|---|---|---|---|---|---|---|---|---|
| Bebé (`baby`) | 0 años | No | — | 25 % | 40 % | 150 % | No | Gatea, no come solo y duerme mucho |
| Niño (`child`) | 3 años | No: sólo juega | — | 90 % | 70 % | 110 % | Sí | Decisión del usuario: los niños sólo juegan |
| Adolescente (`adolescent`) | 13 años | Sólo trabajos livianos | 80 % | 100 % | 100 % | 100 % | Sí | Ayuda en la aldea sin reemplazar a un adulto |
| Adulto (`adult`) | 18 años | Todos | 100 % | 100 % | 100 % | 100 % | Sí | El colono de IP1 e IP2 |
| Anciano (`elder`) | 60 años | Todos | 80 % | 80 % | 90 % | 100 % | Sí | Trabaja más despacio, pero sabe más |

- **Trabajos livianos:** los tipos de trabajo con `light: true`: recolectar (`gathering`), acarrear (`hauling`) y la crianza nueva (`childcare`).
- **Hambre y descanso:** el porcentaje multiplica `decayPerHour` de la necesidad.
- El cambio de etapa se avisa en el registro, emite `EntityChangedEvent` (cambia el modelo) y se ve en el inspector.
- Las prioridades de trabajo de un colono que todavía no trabaja se guardan igual: valen cuando crezca.

## Llegada

El def `Scenario` suma la edad, los apellidos y la pareja de los fundadores:

| Valor | Cantidad | Motivo |
|---|---|---|
| Sexo de los fundadores | Los dos primeros, una mujer y un varón en orden al azar; el resto, al azar (desde H3.2) | Pedido del usuario: una aldea sin mujeres o sin varones no puede tener hijos |
| Edad de los fundadores | De 18 a 40 años, al azar | Adultos en edad de trabajar |
| Parejas fundadoras | 4 de los 5, en dos parejas de una mujer y un varón de 20 a 30 años, con orientaciones compatibles (`foundingCouple.count`, desde H3.10; antes, una) | Decisión del usuario; la aldea puede crecer desde el principio, y los hijos de las dos familias pueden formar pareja entre sí |
| Opinión inicial entre fundadores | De −10 a +20, al azar | Se conocen poco; las amistades se forman jugando |
| Opinión y romance de la pareja | 70 y 100 | Ya son pareja |
| Habilidades | De 3 a 10 en cada una, y una al azar con 4 más | Cada fundador tiene un fuerte; el promedio ronda la velocidad de IP2 |

- **Apellidos:** cada colono tiene `surname`. Los fundadores lo sacan de la lista de apellidos de su criatura (`NameList` suma `surnames`), sin repetir mientras queden. Los hijos heredan el del padre (decisión del usuario). Nadie cambia de apellido al formar pareja.
- **Nombre completo:** el registro y la ficha muestran nombre y apellido ("Elvira Pérez"); las listas angostas, sólo el nombre.
- **Lista de apellidos medievales** (`medieval_names.surnames`), mezcla, como los nombres, de patronímicos castellanos (hereditarios desde los siglos XIII y XIV), toponímicos y de oficio ingleses (que se vuelven hereditarios entre los siglos XIII y XIV): Pérez, Fernández, González, Martínez, Sánchez, Díaz, Rodríguez, Álvarez, Núñez, Gómez, Muñoz, de Lara, de Haro, de Castro, Navarro, Smith, Miller, Fletcher, Cooper, Baker, Wright, Thatcher, Hill, Ford, Marsh, Atwood, Brook, Shepherd. Los apellidos no se traducen.

## Personalidad

Def nuevo `Trait`: cada rasgo es un eje de 0 a 100. Contenido de IP3:

| Rasgo | Bajo (0–25) | Alto (75–100) | Qué cambia |
|---|---|---|---|
| `sociability` | Reservado | Sociable | Probabilidad de iniciar una interacción |
| `kindness` | Áspero | Amable | Elige consolar o discutir; peso de las interacciones amables |
| `diligence` | Perezoso | Trabajador | Velocidad de trabajo de 90 % a 110 %; por debajo de 25 atiende sus necesidades bajas antes de trabajar (H3.3) |
| `curiosity` | Distraído | Curioso | Ganas de aprender: peso de enseñar como aprendiz y deseos de aprender |
| `intelligence` | Torpe | Inteligente | Experiencia ganada de 75 % a 125 %, trabajando y aprendiendo de otro |
| `patience` | Irritable | Paciente | Probabilidad de discutir y de entrar en crisis |

- **Sorteo:** el promedio de tres tiradas de 0 a 100, que concentra los valores cerca de 50 sin cortarlos.
- **Herencia:** un hijo saca, en cada rasgo, el promedio de sus padres en la mitad del valor y un sorteo en la otra mitad.
- **Deriva:** las vivencias mueven los rasgos de a poco (ver interacciones, pensamientos y crisis). Cada rasgo se mueve como mucho 3 puntos por día del juego, sumando las subidas y las bajadas, entre medianoche y medianoche. En niños y adolescentes, cada vivencia mueve el doble (`traitDriftPercent` de la etapa): es cuando se forma el carácter. El mecanismo llega en H3.3; las vivencias que lo usan, en H3.5 y H3.6.
- **Perezosos** (pedido del plan: que dos colonos con rasgos opuestos elijan distinto ya en H3.3): un colono con diligencia menor a 25 atiende sus necesidades bajas antes de trabajar; el resto trabaja hasta que una necesidad es crítica, como en IP2. Es el campo `needsBeforeWorkBelow` del rasgo.
- Un colono sin personalidad, o sin un rasgo, cuenta como promedio (50) en él.
- **Orientación** (decisión del usuario): parte de la personalidad, sorteada al nacer o al llegar, con pesos en los defs: le atraen las personas del otro sexo (80), del mismo sexo (8) o de los dos (12). No cambia con las vivencias. La ficha la muestra desde la adolescencia.
- La ficha muestra cada rasgo con una palabra ("Muy sociable", "Algo perezosa") en lugar del número, según tramos en los defs.

## Habilidades

- Una por tipo de trabajo: el `WorkType` suma `skill: true`. En IP3: excavar, talar, recolectar, construir, acarrear y criar. Sus nombres son los del tipo de trabajo.
- **Niveles** de 0 a 20. Para pasar del nivel N al N+1 hacen falta `600 + 150 × N` puntos de experiencia (del 7 al 8: 1650).
- **Experiencia trabajando:** 1 punto por tick de trabajo de ese tipo, por la inteligencia (de 75 % a 125 %). Un excavador de nivel 7 sube al 8 en unas 27 horas de excavar.
- **Velocidad:** `80 + 2 × nivel` por ciento (nivel 0: 80 %, nivel 10: 100 %, nivel 20: 120 %). Se multiplica con la etapa, la diligencia y el ánimo. Los trabajos duran `ticks × 100 / velocidad`, en enteros.
- **Aprender de otro:** la interacción enseñar (abajo) da al aprendiz `100 + 30 × diferencia de niveles` puntos, por su inteligencia.
- Subir de nivel deja el pensamiento "Aprendí algo" y se ve en la ficha; no se avisa en el registro.
- Las habilidades no se pierden con el tiempo en IP3 (ver fuera de alcance).
- **Qué cuenta como trabajo** (H3.4): cada tick que hace avanzar un trabajo para el jugador, y cada tick que un acarreador o un proveedor de obra lleva algo en las manos. Esperar a que se libere una celda no enseña. Recolectar para comer no es trabajo.
- En el acarreo, y al llevar materiales a una obra, la habilidad apura el paso mientras se lleva la carga.
- Un colono sin habilidades trabaja como uno de nivel promedio (`averageLevel`, 10) y no aprende. Si le falta la habilidad de un tipo de trabajo, por ejemplo uno que el contenido sumó después, trabaja como de nivel 0 y la aprende desde ahí. Subir de nivel emite `EntityChangedEvent`. La enseñanza (`SkillRules.Teach`) está desde H3.4; la interacción enseñar que la usa llega en H3.5.
- Los números están en el def nuevo `SkillConfig` (`skills.json`): nivel máximo, experiencia por nivel, velocidad por nivel y enseñanza. La experiencia se guarda en centésimas de punto, para que la inteligencia no pierda la fracción.

## Interacciones

- Cada `interactionCheckTicks` (30) ticks, escalonados por colono, un colono despierto, que no es bebé ni está en crisis de aislamiento, puede iniciar una interacción con otro despierto a `radius` (6, desde H3.10; antes 4) celdas o menos en el mismo nivel. No interrumpen el trabajo: se charla mientras se trabaja.
- **Probabilidad de iniciar:** `30 % × sociabilidad / 50` por revisión (desde H3.10; antes 20 %), si hay alguien cerca. Un mismo par no repite antes de `pairCooldownHours` (2) horas.
- **Con quién:** al azar entre los cercanos, con peso doble para la familia y los amigos.
- **Qué interacción:** al azar entre las permitidas, con peso `weight + Σ coef × (rasgo − 50) / 10`, nunca menos de 0. El def nuevo `Interaction` define:

| Interacción | Quién | Peso | Condición | Resultado |
|---|---|---|---|---|
| Charlar (`chat`) | Niño o mayor | 60, sociabilidad +2 | — | Opinión +1 a +3 los dos; "Charlé con alguien". Si el otro tiene paciencia < 30, 30 % de que termine mal: opinión −1 a −3 (H3.5) |
| Bromear (`joke`) | Niño o mayor | 20, sociabilidad +2 | Opinión ≥ 10 | Opinión +2 a +5 los dos y "Me reí". Si el otro tiene paciencia < 30, 20 % de que salga mal: opinión −2 |
| Enseñar (`teach`) | Adolescente o mayor, a otro adolescente o mayor | 30, curiosidad del otro +2 | El que enseña tiene 3 niveles más en una habilidad | Experiencia para el aprendiz; su opinión +2 |
| Discutir (`argue`) | Adolescente o mayor | 5, paciencia −6, amabilidad −4 (H3.5) | Opinión ≤ 20 (H3.5) | Opinión −3 a −8 los dos; "Discutí"; 20 % de paciencia −1 |
| Consolar (`comfort`) | Adolescente o mayor | 25, amabilidad +3 | El otro tiene ánimo < 30 | El otro: opinión +3 a +6 y "Me consolaron"; quien consuela: 20 % de amabilidad +1 |
| Cortejar (`flirt`) | Adultos solteros | 15 | Le atrae el otro; opinión ≥ 20 | Si el otro también se siente atraído y su opinión es ≥ 0: romance +10 a +20 los dos y opinión +2 a +4. Si no: "Me rechazaron" para quien cortejó |
| Jugar (`play`) | Un niño, con un niño, un adolescente o un familiar | 80 | — | Opinión +2 a +4 los dos; "Jugué"; 20 % de sociabilidad +1 para el niño |

- Cada interacción emite `InteractionEvent`: la presentación muestra un globo con un ícono sobre los dos colonos. No va al registro, salvo lo que cambia una relación (abajo).
- La ficha guarda las últimas 5 interacciones de cada colono.
- **Ajustes de H3.5:** con los números de arriba, dos colonos irritables y ásperos que conviven no llegaban a rivales: la charla subía la opinión y, pasando 10, ya no podían discutir. La charla termina mal con alguien impaciente (como la broma), la discusión pesa más según la paciencia y la amabilidad, y se permite hasta una opinión de 20. Para un colono promedio la discusión sigue con peso 5.
- Un colono sin etapa de la vida cuenta como de la primera etapa que hace todo trabajo (`LifeRules.AdultStage`).
- Hasta H3.6 todos tienen ánimo 50, así que nadie consuela todavía.
- En una aldea real, con los colonos repartidos en sus trabajos, hay unas 3 interacciones por día entre cinco: las amistades tardan más de 10 días. Se revisa en el balance de H3.10.

## Relaciones y familia

- **Opinión:** cada colono tiene una opinión de −100 a 100 de cada otro que conoce, que no tiene por qué ser la misma en los dos sentidos.
- **Amistad y rivalidad:** opinión ≥ 40 de los dos lados es amistad; ≤ −40 de uno de los dos lados, rivalidad. Se avisa al empezar y no se guarda: sale de las opiniones. Para no repetir el aviso cuando la opinión va y viene sobre el umbral, cada relación recuerda el último lazo avisado (`Relation.Told`), que se olvida cuando las dos opiniones se alejan `retellMargin` (10) puntos del umbral.
- **Olvido:** si un par no interactúa en un día, cada opinión se acerca 1 punto a 0, sin bajar de 30 entre familiares ni entre la pareja.
- **Familia:** padre, madre, pareja, hijos y hermanos (los que comparten padre o madre). Al nacer, la opinión entre padres e hijo empieza en 40, y entre hermanos en 30. Los familiares muertos quedan en la ficha con su nombre y "fallecido".
- **Pareja:** dos adultos solteros a los que se atraen mutuamente, con opinión ≥ 50 de los dos lados y romance ≥ 100, forman pareja. Cualquier pareja es posible (decisión del usuario), salvo entre familiares; sólo una de mujer y varón puede tener hijos.
- **Ruptura:** si la opinión de uno de los dos baja de 0, la pareja se rompe y el romance de los dos vuelve a 0: para ser pareja otra vez tienen que cortejarse de nuevo (H3.5). Al morir uno, el otro queda viudo y puede volver a formar pareja después de un año de vida.

## Pensamientos y ánimo

- **Ánimo** de 0 a 100: 50 más la suma de los pensamientos, recortado.
- Def nuevo `Thought`: intensidad, duración en horas del juego (0 = mientras dure la situación) y cuántos del mismo pueden sumarse. Contenido de IP3:

| Pensamiento | Ánimo | Duración | Se suman | Cuándo |
|---|---|---|---|---|
| Con hambre (`hungry`) | −6 | Mientras | 1 | Hambre bajo el umbral bajo |
| Hambriento (`starving`) | −15 | Mientras | 1 | Hambre bajo el umbral crítico (reemplaza al anterior) |
| Cansado (`tired`) | −4 | Mientras | 1 | Descanso bajo el umbral bajo |
| Dormí en mi cama (`slept_in_bed`) | +3 | 12 h | 1 | Al despertar en su cama o cuna |
| Dormí en el suelo (`slept_on_floor`) | −3 | 12 h | 1 | Al despertar en el suelo |
| Charlé con alguien (`chatted`) | +2 | 6 h | 3 | Charlar |
| Me reí (`laughed`) | +3 | 6 h | 2 | Bromear |
| Jugué (`played`) | +4 | 6 h | 2 | Jugar |
| Me consolaron (`comforted`) | +6 | 12 h | 1 | Consolar |
| Discutí (`argued`) | −5 | 12 h | 2 | Discutir |
| Me rechazaron (`rebuffed`) | −3 | 12 h | 1 | Cortejar sin suerte |
| Aprendí algo (`learned`) | +3 | 24 h | 2 | Subir un nivel |
| Tengo pareja (`new_partner`) | +10 | 3 días | 1 | Formar pareja |
| Nos separamos (`breakup`) | −10 | 3 días | 1 | Ruptura |
| Nació mi hijo (`child_born`) | +12 | 3 días | 2 | Nacimiento, para los padres |
| Murió mi pareja (`death_partner`) | −20 | 10 días | 1 | |
| Murió mi hijo (`death_child`) | −25 | 10 días | 2 | |
| Murió mi padre o mi madre (`death_parent`) | −15 | 7 días | 2 | |
| Murió mi hermano o hermana (`death_sibling`) | −12 | 7 días | 2 | Agregado en H3.6 |
| Murió un amigo (`death_friend`) | −10 | 5 días | 3 | |
| Murió alguien de la aldea (`death_colonist`) | −3 | 2 días | 3 | Si no es familiar ni amigo |
| Cumplí un deseo (`desire_fulfilled`) | +8 | 3 días | 2 | |
| Un deseo se me frustró (`desire_frustrated`) | −6 | 3 días | 2 | |

- **Efecto en el trabajo:** ánimo < 15: 80 %; < 30: 90 %; > 70: 105 %; > 85: 110 %.
- **Cómo está hecho (H3.6):** los pensamientos de una situación (`need` y `below` en el def) no se guardan: salen de las necesidades al consultar el ánimo. Los de un hecho se guardan con su tick de fin en el componente `Mood`. Qué pensamiento deja cada hecho lo dicen los datos: cada interacción (`initiatorThought`, `receiverThought`, `rejectedThought`) y `MoodConfig` para dormir, aprender, formar pareja, separarse, las muertes y los deseos. Así el código no nombra pensamientos.
- "Nació mi hijo" (`child_born`) llega con los nacimientos, en H3.7.
- **Por qué cambió "Reconciliarse":** dos amigos no pueden discutir (la discusión pide opinión ≤ 20 y la amistad ≥ 40), así que el disparador original nunca ocurría. Los hermanos tenían el pensamiento de un vecino; ahora tienen el suyo.
- Hacer una amistad no apunta a la familia ni a la pareja, y sólo nace de una charla que salió bien. Una cama propia no se desea si ya se tiene una, aunque no se llegue a ella.
- **Deriva:** perder a un familiar baja la sociabilidad 2 puntos; un hijo sube la amabilidad de los padres 2 puntos.

### Crisis

- Con ánimo < 15, cada hora hay 10 % de probabilidad de una crisis, elegida con pesos por rasgos. Def nuevo `Crisis`:

| Crisis | Peso | Dura | Qué hace |
|---|---|---|---|
| Se niega a trabajar (`refuse_work`) | 10, diligencia −2 | 6 h | Sólo atiende sus necesidades y deambula |
| Se aísla (`isolate`) | 10, sociabilidad −2 | 6 h | No interactúa y deambula lejos de los demás |

- Cada crisis baja la paciencia 2 puntos y se avisa en el registro (aviso amarillo), al empezar y al terminar.
- Las dos crisis cortan el trabajo en curso al empezar y no toman trabajo mientras duran. Quien se aísla, además, no interactúa, y de los lugares que sortea para deambular elige el más lejano de los demás (H3.6).

## Deseos

- Def nuevo `Desire`. Cada colono tiene como mucho 2 deseos a la vez y suma como mucho uno por día del juego. Si no lo cumple en `expireDays` (10) días del juego, se frustra.

| Deseo | Nace | Se cumple | Probabilidad |
|---|---|---|---|
| Aprender de alguien (`learn_from`) | Al recibir una enseñanza de alguien con 5 niveles más | Sube un nivel en esa habilidad | 30 %, por la curiosidad |
| Hacerse amigo (`befriend`) | Tras charlar con alguien con opinión de 20 a 39 | Amistad con esa persona | 20 %, por la sociabilidad |
| Formar pareja (`find_partner`) | Un adulto soltero, una vez por año de vida | Formar pareja | 30 % |
| Tener un hijo (`have_child`) | Un adulto en pareja fértil, una vez por año de vida | Nace un hijo de la pareja | 30 % |
| Una cama propia (`own_bed`) | Tras dos noches seguidas en el suelo | Tiene cama | 50 % |
| Reconciliarse (`reconcile`) | Tras discutir con alguien de quien tenía opinión ≥ 10, si la opinión bajó (H3.6) | Su opinión vuelve a la de antes de la discusión | 40 %, por la amabilidad |

- "Tener un hijo" no nace si los hijos están impedidos para esa pareja: la opción del jugador no castiga el ánimo. Este deseo llega con la fertilidad, en H3.7.
- **Ajuste de H3.7:** el deseo de un hijo se cumple con el embarazo, no con el parto: en Normal el embarazo dura 45 días del juego, más que los 10 que espera un deseo. Nace sólo si la pareja podría concebir (nadie espera un hijo y no hay un bebé de menos de un año), es sobre la pareja y se descarta sin frustrarse si ella muere o se separan. Espera un año de vida (`expireYears`), hasta la próxima ocasión, y no 10 días: con 40 % por año, uno de cada tres se cumple en cualquier ritmo. El balance se revisa en H3.10.
- **Cómo está hecho (H3.6):** el def `Desire` tiene un `kind` que el código conoce (`learnFrom`, `befriend`, `findPartner`, `ownBed`, `reconcile`) y los números en datos (`chancePercent`, `scaledBy`, `minLevels`, `minOpinion`, `maxOpinion`, `nights`, `stages`). Las interacciones dicen qué deseo pueden despertar (`desire`). Un deseo sobre alguien que murió se descarta sin frustrarse.
- Los deseos se ven en la ficha; no van al registro.

## Parejas, embarazo y nacimiento

- **Fertilidad:** mujeres de 18 a 45 años y varones de 18 a 60, adultos.
- **Concepción:** a medianoche, una pareja de mujer y varón fértiles, con hijos permitidos, sin embarazo en curso y sin un hijo de menos de un año de vida, concibe con probabilidad `conceptionChancePerYear` (40 % por año de vida) llevada al día: `40 % × lifeDaysPerDay / 60`. Si alguno de los dos tiene hambre bajo el umbral bajo, no concibe: la comida es el tope de la población (decisión del usuario). Desde H3.10 tampoco concibe si la aldea no tiene comida guardada para `foodReserveDays` (30) días por colono, ni si el mapa no tiene `foodPlantsPerColonist` (9) plantas de comida por colono, contando al que va a nacer (ver "Balance de población y comida"). Es un evento abstracto: no se muestra.
- **Embarazo:** 45 días de vida (nueve meses del año de 60 días): 45 días del juego en Normal, 12 en Rápido y 6 en Muy rápido. Se avisa al empezar, se ve en el inspector y la madre trabaja al 90 % en el último tercio.
- **Parto:** al cumplirse, nace un bebé en la celda de la madre, con sexo al azar, nombre de la lista que no lleve ningún colono vivo mientras queden, el apellido del padre, rasgos heredados y orientación sorteada. Habilidades en 0, necesidades llenas. Se avisa en el registro.
- **Opción del jugador:** "Hijos: permitidos / no" en la ficha de cada colono en pareja; vale para la pareja. Por defecto, permitidos.

## Balance de población y comida (H3.10)

Medido con partidas de diez años del juego en el valle real, semillas 1 y 42, con un jugador que mantiene la orden de recolectar en un radio de 25 celdas (la orden de un arbusto se cumple al recolectarlo, así que la repite cada dos días) y un almacén.

- **Sin cambios**, la aldea moría entera de hambre en un invierno en dos de cuatro partidas: los chicos nacían mientras había comida y después el valle no alcanzaba. El valle tiene unos 55 arbustos de bayas, que sin agricultura alimentan a unas 7 personas. Además había 2 o 3 interacciones por día y, en la semilla 1, ninguna amistad en años.
- **Decisiones del usuario (18 de octubre de 2026):** reserva de comida para concebir (30 días por colono), más vida social (radio 6 y 30 % de iniciar) y dos parejas fundadoras, para que los hijos de dos familias puedan formar pareja.
- **La reserva sola no alcanzaba:** se mide al concebir, en verano u otoño, y los chicos comen más al crecer. Con dos parejas, una aldea llegó a 12 y murió en el séptimo invierno. Por eso se suma un tope por lo que da la tierra: 9 plantas de comida por colono, contando al que va a nacer. Así el valle sostiene unas 6 personas.
- **Resultado:** ninguna aldea murió de hambre en diez años. En Rápido la aldea se mantiene entre 6 y 7 personas, con nacimientos que reemplazan a los que mueren de viejos. Hay de 4 a 16 interacciones por día y amistades en todas las partidas.
- **Límite conocido:** en Muy rápido los fundadores salen de la edad fértil en dos o tres años del juego y nacen pocos chicos; la aldea tiende a extinguirse en unos diez años. La agricultura (IP4) sube el tope de comida y los migrantes (IP6) traen gente nueva.
- **Rendimiento:** la simulación de diez años en Rápido corre a 0,017 ms por tick; en Godot, con esa partida cargada, 331 FPS y 0,10 ms por tick (presupuesto: 60 FPS y 2 ms).
- `BalanceTests` juega diez años en Rápido y comprueba que nadie muere de hambre, que nace alguien y que quedan al menos cinco colonos.

## Crianza

- **Bebés:** no comen solos. Con hambre bajo el umbral bajo, aparece el trabajo nuevo **alimentar** (`feed_baby`), del tipo de trabajo nuevo `childcare`: un colono que puede trabajar toma una unidad de comida de una pila, la lleva al bebé y se la da. Lo toman primero la madre o el padre, si están libres y llegan; si no, el más cercano. `childcare` va primero en el orden de los tipos de trabajo.
- Un bebé gatea: deambula a 3 celdas o menos de su madre (o de donde esté, si la madre no está). Con sueño, va a su cuna si tiene; si no, duerme donde está.
- **Cuna** (`cradle`): mueble nuevo de madera, costo 2 y trabajo 90, cama sólo para bebés (`forStages: [baby]`, `restRecoveryPercent` 150). Un bebé toma una cuna libre como un colono toma una cama.
- **Niños:** comen y duermen solos. En lugar de trabajar, juegan: van hacia otro niño o un familiar a 15 celdas o menos e interactúan con él; si no hay nadie, deambulan.
- Un bebé con hambre en cero muere como cualquier colono, y deja los pensamientos de muerte a su familia.

### Cómo está hecho (H3.7)

- **Etapas:** el def `LifeStage` suma `eatsAlone` (el bebé, `false`: no busca comida y otros lo alimentan), `crawlRadius` (el bebé, 3) y `playRadius` (el niño, 15). El código no nombra etapas.
- **Nacimientos:** `BirthSystem` corre a medianoche, después del envejecimiento, y recorre a las mujeres por `EntityId`. Los números están en el bloque `birth` de `LifeConfig`: `femaleFertileYears` y `maleFertileYears`, `conceptionChancePerYear`, `pregnancyLifeDays`, `lateFromLifeDays` y `lateWorkSpeedPercent` (el último tercio), `yearsBetweenChildren`, `parentOpinion` y `siblingOpinion`. Sin el bloque no nace nadie. Fértil quiere decir adulto de una etapa que hace todo trabajo y dentro de los años de su sexo.
- **Embarazo:** el componente `Pregnancy` de la madre guarda al padre con su nombre, por si muere antes del parto, y los días de vida que lleva. Si el padre murió, el hijo hereda la mitad de los rasgos de la madre y la otra mitad del promedio.
- **Parto:** padres, hermanos y bebé cuentan como que se encontraron al nacer, así la opinión no se olvida esa misma noche. Los padres sienten "Nació mi bebé" y su amabilidad sube 2 (`parenthood` en `MoodConfig`).
- **Alimentar:** quien cría va a la pila de comida más cercana, levanta las unidades que le faltan al bebé (no una sola, para no hacer un viaje por baya) y se las da de a una, al ritmo de comer. Si el bebé gateó, lo sigue. Mientras le traen comida, el bebé espera quieto. Quien va a buscar la comida reserva sólo esas unidades, de las que nadie más reservó (`Job.Count`), así los demás pueden comer del resto de la pila. Da de comer a la velocidad de su trabajo: cuentan la habilidad de crianza, los rasgos, el ánimo y el embarazo. El bebé gatea cerca de su madre aunque esté en crisis; si ella está a más de 12 celdas (cuatro veces su radio), gatea donde está y no busca caminos largos. La madre o el padre van primero si están libres (sin trabajo, o paseando o jugando y por decidir de nuevo en ese tick), ningún otro trabajo que hacen tiene más prioridad que la crianza y llegan al bebé; si no, lo toma el primer colono libre que lo encuentra entre sus trabajos más cercanos. Quien no encuentra comida deja al bebé a los demás por un rato; si la comida sólo está reservada por otro, no. Si no encuentra camino a una pila, prueba con otra en la misma decisión.
- **Jugar:** un niño sin nada que hacer sortea un compañero en su nivel a 15 celdas o menos, otro niño o un familiar, despierto y con quien no interactuó en las últimas `pairCooldownHours`. Va a su lado y se queda un rato, como al pasear; ahí la interacción jugar sale sola. Así no sigue siempre al mismo.
- **Camas:** `forStages` en la cama (niño, adolescente, adulto y anciano) y en la cuna (bebé). `Beds` decide qué cama le corresponde a cada uno; la cuna de un bebé que creció queda libre.
- **Opción de hijos:** `SetChildrenAllowedCommand` cambia la opción del colono y de su pareja viva. Prohibir borra el deseo de tener un hijo sin frustrarlo; un embarazo en curso sigue. Una pareja nueva empieza con hijos permitidos: la opción es de la pareja, no de cada uno.
- **Presentación:** la ficha muestra el botón "Hijos: permitidos / no" a quien tiene pareja viva del otro sexo; la ficha y el inspector muestran "Embarazada (N %)".

## Vejez y muerte

- A medianoche, cada colono de 60 años o más puede morir de viejo. La probabilidad por año de vida sale de una tabla en los defs, con interpolación lineal; por día del juego es `tabla × lifeDaysPerDay / 60`:

| Edad | Por año | Motivo |
|---|---|---|
| 60 | 1 % | Pocos mueren a los 60 |
| 70 | 4 % | |
| 80 | 15 % | La mayoría muere entre los 70 y los 85 |
| 90 | 40 % | |
| 100 | 100 % | Nadie pasa de los 100 |

- Antes de la primera fila no hay muerte por vejez; después de la última vale la última. Con 100 % por año la muerte es segura ese mismo día: nadie pasa de los 100 en ningún ritmo.
- La muerte por vejez se avisa en el registro, libera su cama, deja viuda a su pareja y los pensamientos de muerte a su familia y amigos. Como la muerte por hambre, quita la entidad. Cadáveres y tumbas llegan en IP5.

## Ficha del colono

Adelantada a H3.7 por pedido del usuario (16 de octubre de 2026): quería ver en el juego lo que trajeron H3.2 a H3.6.

- Se abre con **F** o con el botón "Ficha (F)" del inspector, sobre el colono seleccionado, y sigue a la selección. Se cierra con F (aunque se haya seleccionado otra cosa), Esc, su botón "Cerrar", o cuando el colono muere. Se actualiza sólo cuando pasa el tiempo.
- Arriba: nombre y apellido; etapa y edad, ánimo, crisis si la hay y, desde las etapas que trabajan, la orientación ("Le atraen las mujeres").
- A la izquierda: **Personalidad** (sólo los rasgos que se apartan del promedio, en palabras: "Algo áspero", "Muy sociable", según los tramos `words` de `PersonalityConfig`: hasta 15 muy bajo, hasta 35 bajo, hasta 64 promedio, hasta 84 alto, hasta 100 muy alto), **Habilidades** (nivel y porcentaje hacia el siguiente) y **Familia** (padre, madre, pareja, parejas anteriores, hijos y hermanos; los muertos con "fallecido").
- A la derecha: **Pensamientos** (cada uno una vez, con "×2" si se suma y su efecto total), **Deseos** (con de quién o de qué, "Minería, de Alfonso Díaz", y los días que quedan), **Relaciones** (de la mejor opinión a la peor, con el lazo) y **Últimas interacciones** ("Le enseñó a Alfonso Díaz", "Nuño vino a charlar").
- El panel Colonia suma las columnas Edad y Ánimo.
- La opción de hijos se suma a la ficha con los nacimientos, en H3.7.
- Textos: `def.trait.<id>.low` y `.high` (con concordancia), `def.interaction.<id>.started` y las claves `ui.sheet.*`.

## Guardado y migración

- El formato de guardado sube con cada hito que suma estado (ADR-013): 2 en H3.2 y 3 en H3.3. Una partida vieja se migra al cargarla, paso por paso: cada colono recibe lo que le falta (edad y apellido desde la versión 1, personalidad desde la 2, habilidades desde la 3 y opiniones desde la 4; la 5 sólo empieza sin pensamientos ni deseos) como los fundadores, sorteado con el flujo `life.migration` en orden de `EntityId`. No hay pareja fundadora y el ritmo es Rápido. El formato es 6 desde H3.6 y 7 desde H3.7: la 6 recibe la habilidad de crianza, sorteada como la de un fundador, y empieza sin embarazos.
- Las huellas del smoke cambian en H3.2 (la edad entra en el estado) y en cada hito que suma estado. La bitácora registra las nuevas.

## Contrato con la presentación

| Tipo | Nombre | Uso |
|---|---|---|
| Parámetro | `ColonyStart.Settle(simulation, escenario, nombre, ritmo)` | El ritmo elegido en Nueva partida |
| Estado | `Simulation.LifePace` | Id del `LifePace`; entra en la huella y en el guardado |
| Componente | `Colonist.Surname` | Apellido |
| Componente | `Life` | `AgeDays` y la etapa actual |
| Componente | `Personality` | Rasgos (id y valor, ordenados por id), orientación y lo que se movió hoy cada rasgo |
| Componente | `Skills` | Por habilidad: nivel y experiencia, ordenados por id |
| Componente | `Relations` | Por otro colono: opinión, romance y tick de la última interacción, ordenados por `EntityId` |
| Componente | `Family` | Padre, madre, pareja e hijos, con nombre y sexo guardados para los que murieron; hijos permitidos |
| Componente | `Mood` | Pensamientos con su tick de fin; el ánimo sale de ellos |
| Componente | `Desires` | Deseo, objetivo (colono o habilidad) y tick de vencimiento |
| Componente | `Pregnancy` | En la madre: padre y días de vida transcurridos |
| Componente | `Crisis` | Crisis en curso y tick de fin |
| Componente | `RecentInteractions` | Las últimas 5 interacciones, para la ficha |
| Comando | `SetChildrenAllowedCommand(colono, permitidos)` | La opción de hijos de una pareja |
| Evento | `InteractionEvent(tick, iniciador, receptor, interacción)` | El globo sobre los colonos |
| Evento | `EntitySpawnedEvent`, `EntityChangedEvent`, `EntityRemovedEvent` | Nacimientos, cambios de etapa y muertes |
| Evento | `NotificationEvent` | Los mensajes de la tabla de textos |
| Consulta | `LifeRules.AgeYears`, `LifeRules.Stage`, `MoodRules.Value`, `SkillRules.Level`, `Relationships.Of`, `Relationships.Kind` | Ficha, inspector y panel Colonia |

- Todo componente y comando nuevo se registra en `SaveTypes` con un nombre estable, y sus ids de defs llevan `[DefRef]`.
- `TextResolver` expande `{colonist}` en nombre y apellido, y `{colonist_gender}` como hasta ahora.

## Flujos aleatorios

| Flujo | Uso |
|---|---|
| `colony.start` | Lo de IP1, más edad, apellido, rasgos, orientación, habilidades, opiniones y la pareja fundadora |
| `life.aging` | Muerte por vejez |
| `life.birth` | Concepción, sexo, nombre, rasgos y orientación del bebé |
| `life.migration` | Migración de partidas de la versión 1 |
| `social.interactions` | Si se inicia, con quién, cuál y su resultado |
| `social.desires` | Nacimiento de deseos y deriva de los rasgos |
| `mood.crisis` | Crisis |

- Ningún sistema decide recorriendo un diccionario: los colonos se recorren por `EntityId` ascendente.

## Defs nuevos y cambios

| Def | Campos | Archivo |
|---|---|---|
| `LifePace` | `lifeDaysPerDay`, `default` | `life.json` |
| `LifeStage` | `fromYears`, `work` (`none`, `light`, `all`), `workSpeedPercent`, `moveSpeedPercent`, `needsPercent` por necesidad (H3.2); `traitDriftPercent` (H3.3); `interacts` (H3.5); `eatsAlone`, `crawlRadius` y `playRadius` (H3.7). El modelo de cada etapa va en el catálogo de visuales (H3.8) | `life.json` |
| `LifeConfig` | Tabla de vejez `oldAge` (H3.2); bloque `birth` con fertilidad, concepción, embarazo y opiniones al nacer (H3.7), y `foodReserveDays` y `foodPlantsPerColonist` (H3.10) | `life.json` |
| `Trait` | `workSpeed` (`low` y `high`) y `needsBeforeWorkBelow` (H3.3); `experience` (H3.4); los coeficientes de interacciones y crisis en sus hitos; tramos de palabras para la ficha (H3.9) | `personality.json` |
| `SkillConfig` | `maxLevel` (20), `averageLevel` (10), `experienceBase` (600), `experiencePerLevel` (150), `speedBase` (80), `speedPerLevel` (2), `teachingMinLevels` (3), `teachingBase` (100) y `teachingPerLevel` (30) (H3.4) | `skills.json` |
| `PersonalityConfig` | `rolls` (3), `inheritedPercent` (50), `maxDriftPerDay` (3) y los pesos de orientación `orientations` (H3.3) | `personality.json` |
| `Interaction` | Etapas de quien empieza y del otro (`familyReceivers` para jugar con la familia), peso y coeficientes por rasgo, condiciones (`minOpinion`, `maxOpinion`, `receiverMoodBelow`, `teaches`, `romantic`), resultados (`opinion`, `receiverOpinion`, `romance`, `backfire`) y deriva (H3.5) | `social.json` |
| `SocialConfig` | `checkTicks`, `radius`, `pairCooldownHours`, `startChancePercent`, `closeWeightPercent`, umbrales de amistad, rivalidad y pareja, `retellMargin`, `breakupBelow`, olvido, `founderOpinion`, `widowYears` y `recentInteractions` (H3.5) | `social.json` |
| `Thought` | `mood`, `durationHours`, `stackLimit`; para los de una situación, `need`, `below` (`low` o `critical`) y `replaces` (H3.6) | `mood.json` |
| `Crisis` | `weight`, `traits`, `durationHours`, `behavior` (`refuseWork`, `isolate`) y `drift` (H3.6) | `mood.json` |
| `MoodConfig` | `base`, bandas de `workSpeed`, `crisisBelow`, `crisisChancePercent`, `maxDesires`, `daysBetweenDesires`, `desireExpireDays`, `grief` y el pensamiento de cada hecho (H3.6); `childBorn` y `parenthood` (H3.7) | `mood.json` |
| `Desire` | `kind`, `chancePercent`, `scaledBy` y los números de cada clase (H3.6) | `mood.json` |
| `WorkType` | `skill`, `light`; tipo nuevo `childcare` con `feed_baby` | `work_types.json` |
| `NameList` | `surnames` | `names.json` |
| `Scenario` | `founderMinYears` y `founderMaxYears` (H3.2); `founderSkillMin` (3), `founderSkillMax` (10) y `founderSkillBonus` (4) (H3.4), con el máximo más el bonus hasta `maxLevel`; `foundingCouple`, con `count` desde H3.10 | `scenario.json` |
| `Buildable` | `cradle`, con `forStages`; la cama también lo lleva (H3.7) | `buildables.json` |

- Cada def con textos lleva `name`, `desc` y `gender` en es y en. Los defs entran en el hito que los usa, no en H3.1.

## Textos nuevos (propuesta)

Las claves siguen `docs/i18n.md`. El i18n-specialist ajusta el texto final en cada hito.

| Clave | es | en |
|---|---|---|
| `ui.newgame.life_pace` | Ritmo de la vida | Pace of life |
| `def.life_pace.normal.desc` | Un año del juego es un año de vida. | One game year is one year of life. |
| `def.life_pace.fast.desc` | Cada año del juego pasan 4 años de vida. | Four years of life pass in each game year. |
| `ui.inspector.life` | {stage}, {years, plural, one {# año} other {# años}} | {stage}, {years, plural, one {# year} other {# years}} old |
| `event.life.birth` | {mother} dio a luz a {child_gender, select, female {una niña} other {un niño}}: {child}. | {mother} gave birth to a {child_gender, select, female {girl} other {boy}}: {child}. |
| `event.life.pregnant` | {colonist} está embarazada. | {colonist} is pregnant. |
| `event.life.old_age` | {colonist} murió de vejez a los {years} años. | {colonist} died of old age at {years}. |
| `event.life.stage.<etapa>` | Uno por etapa que no empieza a los 0 años, por ejemplo {colonist} ya es adolescente. | {colonist} is a teenager now. |
| `event.social.couple` | {a} y {b} son pareja. | {a} and {b} are now a couple. |
| `event.social.breakup` | {a} y {b} se separaron. | {a} and {b} broke up. |
| `event.social.friends` | {a} y {b} se hicieron amigos. | {a} and {b} became friends. |
| `event.social.rivals` | {a} y {b} no se soportan. | {a} and {b} can't stand each other. |
| `event.mood.crisis.refuse_work` | {colonist} se niega a trabajar. | {colonist} refuses to work. |
| `event.mood.crisis.isolate` | {colonist} quiere estar {colonist_gender, select, female {sola} other {solo}}. | {colonist} wants to be alone. |
| `ui.sheet.children_allowed` | Hijos: permitidos | Children: allowed |
| `ui.sheet.children_forbidden` | Hijos: no | Children: no |

- Las etapas, los rasgos, los pensamientos, los deseos, las crisis y las interacciones llevan sus textos como defs (`def.<tipo>.<id>.name` y `desc`), con concordancia de género donde el texto la necesite ("niño" o "niña", "Algo perezosa").

## Modelos y arte (H3.8)

| Visual | Qué es | Cómo quedó (H3.8) |
|---|---|---|
| `creature.human.baby` | Bebé envuelto, muy chico, que gatea | El aldeano a escala 0,55 con la cabeza 1,35 veces más grande; quieto se sienta (`sit`) y camina despacio, como quien aprende. El kit no trae una animación de gatear: `crouch` es una pose fija, y con ella el bebé se deslizaba |
| `creature.human.child` | Aldeano a escala de niño, cabeza más grande | Escala 0,85 y cabeza 1,2 veces más grande; al jugar festeja (`emote-yes`): `jump` es una pose fija en el aire |
| `creature.human.adolescent` | Aldeano un poco más chico que el adulto | Escala 1,15 (el adulto, 1,3) |
| `creature.human.elder` | Aldeano con canas y paso más lento | Los 12 aldeanos con el pelo en la franja gris de su textura, generados por `tools/make-elder-models.py`; camina más despacio |
| `furniture.cradle` | Cuna medieval de madera | La cama de KayKit en chico (escala 0,2): se lee como una cuna en la vista de la aldea |
| Globo de interacción | Un ícono por interacción sobre los colonos | Íconos pixel de 16 × 16 en `game/assets/icons/interactions/<id>.png`, dibujados por `tools/make-interaction-icons.py`; se ven dos segundos y medio sobre los dos colonos |

- **Sin campo nuevo en `LifeStage`:** en lugar de un `visual` por etapa, el renderer busca en el catálogo `<visual de la criatura>.<etapa>` (como `creature.human.child`) y, si no está, usa el de la criatura, que es el del adulto. Así cada criatura tiene sus etapas y el Core no cambia.
- **El catálogo suma tres campos:** `boneScales` agranda huesos del esqueleto (la cabeza de bebés y niños), `animations` cambia una animación por otra del modelo (el bebé se sienta donde los demás se quedan parados) y `restHeight` dice a qué altura se acuesta quien duerme en una cama (0,22 en la cama, 0,13 en la cuna).
- **Animaciones:** quien alimenta a un bebé usa la de manipular; la de caminar va al paso real (la etapa, y la habilidad al acarrear: el anciano al 80 %, el bebé al 25 %). Dormido, todos usan la pose quieta. Un globo se va si su colono se acuesta, y se oscurece con la profundidad como el colono.
- **Canas:** pelo, ojos y ropa comparten franjas de la textura, así que una textura recoloreada no servía. El script mueve, en la malla de la cabeza, los vértices del pelo a la franja gris; distingue los ojos por su lugar, que es el mismo en todas las cabezas del kit.

## Criterios de aceptación

Escritos como tests de escenario con el contenido de prueba o el real, salvo los de la presentación.

1. Misma semilla, mismo ritmo y mismas órdenes ⇒ misma huella, con nacimientos y muertes.
2. Un colono con `ageDays` justo antes de los 13 años pasa a adolescente a medianoche, emite el cambio y empieza a tomar trabajos livianos y no otros.
3. Con Muy rápido, un colono de 59 años muere de viejo antes de los 101; con la tabla de vejez en 0, no muere.
4. Un niño nunca toma un trabajo, aunque haya órdenes y tenga prioridades.
5. Dos colonos sociables y amables que conviven 5 días se hacen amigos; dos irritables y ásperos, rivales.
6. Un aprendiz junto a un maestro con 5 niveles más gana experiencia por enseñanza; un colono que tala sube de nivel y tala más rápido.
7. Comer con hambre baja y charlar sube el ánimo; con ánimo bajo 15 durante un día aparece una crisis.
8. Un deseo de cama propia se cumple al tomar una cama y deja su pensamiento; si vence, deja el de frustración.
9. La pareja fundadora, con comida, tiene un hijo con el apellido del padre, rasgos heredados (la mitad del promedio de los padres más la mitad de un sorteo) y nombre que no lleva ningún colono vivo.
10. Con los hijos impedidos, una pareja no concibe ni le nace el deseo de tener un hijo; con hambre baja, tampoco concibe.
11. Dos mujeres en pareja no conciben; una pareja con orientaciones incompatibles no se forma.
12. Un bebé con hambre recibe comida de su madre o su padre si están libres; si no, del colono libre más cercano.
13. Una partida de la versión 1 carga, con los colonos con edad, apellido y rasgos, y la misma migración da la misma huella.
14. El test de continuidad del guardado incluye un embarazo, un bebé, una pareja, deseos y una crisis en curso.
15. En la partida: Nueva partida con los tres ritmos, la ficha de un colono en es y en, y el registro con un nacimiento.
16. Presupuesto: menos de 2 ms por tick de media con la población de una partida de 10 años del juego en Rápido.

## Fuera de alcance

Van a `docs/ip/backlog.md`: habilidades que se olvidan sin práctica, necesidad de ocio, camas dobles, casamientos y ceremonias, adopción, herencia de bienes, mellizos, escuela (IP10), enfermedades y partos difíciles (IP5), cadáveres, tumbas y duelo (IP5), migrantes (IP6) y biografías narradas (IP7).

## Fuentes

- Apellidos castellanos: patronímicos en -ez desde los siglos XI y XII, hereditarios en los siglos XIII y XIV; también toponímicos y gentilicios. [Heráldica, IEECC](https://ieecc.es/wp-content/uploads/2024/07/30-heral.pdf) y [EcuRed, Apellidos](https://www.ecured.cu/Apellidos).
- Apellidos ingleses: de lugar y de oficio, que se generalizan en el siglo XIII y son casi todos hereditarios a fines del XIV. [Surname Formation in Britain, Universidad de St Andrews](https://flemish.wp.st-andrews.ac.uk/2015/05/22/surname-formation-in-britain/) y [Occupation names, Guild of One-Name Studies](https://one-name.org/modern-british-surnames/taxonomy/occupation-names/).
