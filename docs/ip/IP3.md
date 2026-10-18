# IP3: Vida y generaciones

**Objetivo.** Que los colonos sean personas y no sólo trabajadores: que tengan edad y crezcan, una personalidad propia que cambia con lo que viven, que se relacionen entre sí, aprendan unos de otros, deseen cosas, formen parejas, tengan hijos y mueran de viejos. Al terminar, una partida larga ve pasar generaciones en la aldea.

**Origen.** Pedido del usuario del 15 de octubre de 2026, durante el playtest de IP2.5. Junta tres partes del plan anterior: los rasgos, las relaciones y el ánimo de la antigua IP4 "Vida en la colonia", las habilidades de la antigua IP3 "Oficios" y las generaciones de IP9. Oficios pasa a ser IP4 y usa las habilidades de esta IP. Las decisiones del usuario:

- Esta IP va antes de Oficios.
- El ritmo del envejecimiento lo elige el jugador al crear la partida.

**Estimación:** 95 a 135 h. **Versión al cerrar:** `v0.3.0`, para Windows y macOS.

**Estado:** en curso desde el 15 de octubre de 2026. H3.1 a H3.6 cerrados el 16 de octubre, H3.9, H3.7 y H3.8 el 18; sigue H3.10, la integración con el playtest.

## Estado de los hitos

| Hito | Estado | Rama | Notas |
|---|---|---|---|
| H3.1 Spec y contrato | Hecho | `ip3/h3-1-spec` | `docs/gdd/vida-y-generaciones.md` aprobada el 16 de octubre de 2026, con el rasgo inteligencia que sumó el usuario |
| H3.2 Edad y etapas de la vida | Hecho | `ip3/h3-2-edad` | Edad, etapas, ritmo en Nueva partida, muerte por vejez, apellidos y migración de v0.2.x (ADR-013); revisión de código con 8 de 9 hallazgos corregidos |
| H3.3 Personalidad | Hecho | `ip3/h3-3-personalidad` | Rasgos, sorteo, herencia, deriva con tope diario, velocidad por diligencia y perezosos que comen antes de trabajar; formato de guardado 3 |
| H3.4 Habilidades | Hecho | `ip3/h3-4-habilidades` | Una habilidad por tipo de trabajo, experiencia por la inteligencia, velocidad por nivel (también al acarrear), fundadores con un fuerte, enseñanza lista para H3.5 y formato 4 |
| H3.5 Interacciones y relaciones | Hecho | `ip3/h3-5-interacciones` | Siete interacciones por datos, opiniones, amistad y rivalidad avisadas una vez, parejas y rupturas, pareja fundadora, familia y viudez, olvido y formato 5 |
| H3.6 Pensamientos, ánimo y deseos | Hecho | `ip3/h3-6-animo` | Pensamientos de situación y de hechos, ánimo y su efecto en el trabajo, dos crisis, cinco deseos (tener un hijo, en H3.7) y formato 6 |
| H3.7 Parejas, nacimientos y crianza | Hecho | `ip3/h3-7-nacimientos` | Embarazo, parto, alimentar bebés, gateo, juego, cuna, deseo de un hijo y opción de hijos en la ficha; formato 7. Cuatro rondas de revisión, 37 hallazgos corregidos y uno descartado con su motivo |
| H3.8 Arte de las etapas | Hecho | `ip3/h3-8-arte` | Etapas con escala y cabeza más grande, ancianos con canas derivados del kit, animaciones de estar sentado, jugar y alimentar, caminar al paso real, globos de interacción; benchmark dentro del presupuesto. Cuatro rondas de revisión, 33 hallazgos corregidos |
| H3.9 Ficha del colono | Hecho | `ip3/h3-9-ficha` | Adelantada a pedido del usuario: ficha (F) y columnas Edad y Ánimo; la opción de hijos llegó con H3.7 |
| H3.10 Integración | En curso | `ip3/h3-10-integracion` | Balance de diez años con dos decisiones del usuario y un tope por lo que da la tierra, benchmark de una partida larga, paquetes de `v0.3.0` y checklist (`docs/ip/playtest-ip3.md`). Falta el playtest del usuario |

**Ruta crítica:** H3.1 → H3.2 → (H3.3 ∥ H3.4) → H3.5 → H3.6 → H3.9 (adelantada el 16 de octubre, a pedido del usuario) → H3.7 → H3.10. H3.8 se hace en paralelo desde H3.2, apenas estén fijadas las etapas.

## Pedidos fuera de los hitos

| Pedido | Fecha | Rama | Estado |
|---|---|---|---|
| Fundadores de los dos sexos: siempre al menos una mujer y un varón | 16 de octubre de 2026 | `ip3/h3-2-edad` | Hecho |
| Existencias de comida, madera y piedra en el HUD, y la opción de ver los FPS (`docs/gdd/indicadores.md`) | 16 de octubre de 2026 | `ip3/indicadores-hud` | Hecho |
| Ficha del colono para ver los datos de IP3 en el juego (H3.9 adelantada) | 16 de octubre de 2026 | `ip3/h3-9-ficha` | Hecho, salvo la opción de hijos |
| Libre albedrío: iniciativa, buscar y evitar personas (`docs/gdd/libre-albedrio.md`) | 16 de octubre de 2026 | — | Planificado como IP3.5 (`docs/ip/IP3.5.md`), después de IP3 |

## Alcance

- **Sexo:** cada colono es mujer o varón. El dato ya existe desde IP1 (`Gender`) y se usa para el nombre y la concordancia de los textos; ahora también decide quién puede tener hijos.
- **Edad:** cada colono tiene fecha de nacimiento y envejece con el calendario. Los fundadores llegan como adultos de edades distintas, y dos de ellos en pareja.
- **Ritmo de la vida:** al crear la partida se elige cuántos años de vida pasan en un año del juego (un año del juego dura 60 días, unas 2,4 h a 1×). Los valores están en los defs; para empezar, Normal (1 año), Rápido (4 años) y Muy rápido (8 años). Con Rápido, un bebé es adulto en unas 11 h de juego y una vida dura unas 40 h. El ritmo se guarda con la partida y no cambia después.
- **Etapas de la vida:** bebé, niño, adolescente, adulto y anciano, definidas en datos con sus edades. Cada etapa cambia qué puede hacer el colono (un bebé no camina solo, un niño juega y aprende pero no trabaja, un adolescente hace trabajos livianos, un anciano trabaja más despacio), cómo se ve y cuánto come y duerme.
- **Muerte por vejez:** desde cierta edad, cada día hay una probabilidad creciente de morir de viejo, con la curva en los defs. Se avisa en el registro, como la muerte por hambre.
- **Personalidad:** rasgos definidos en datos, como ejes de 0 a 100 (por ejemplo sociable, trabajador, amable, curioso, valiente, paciente, inteligente). Se sortean al llegar; los hijos heredan en parte los de sus padres. Las vivencias los ajustan de a poco: una amistad, una pelea, la muerte de alguien cercano o un nacimiento.
- **Interacciones:** dos colonos cerca y con un momento libre pueden interactuar: charlar, bromear, enseñar, discutir, consolar o cortejar. Los tipos son datos; la personalidad y la relación deciden cuál y con qué resultado.
- **Relaciones:** cada colono tiene una opinión de cada otro, que suben o bajan las interacciones. Con la opinión aparecen amistades, rivalidades y parejas. También se registran los lazos de familia: padres, hijos y hermanos.
- **Habilidades:** una por tipo de trabajo (excavar, talar, recolectar, construir, acarrear), con experiencia. Se ganan trabajando y enseñando o aprendiendo de otro colono que sabe más, y aceleran el trabajo. Oficios (IP4) suma la calidad y las habilidades de los talleres.
- **Pensamientos y ánimo:** las vivencias dejan pensamientos con intensidad y duración (comí bien, dormí en el suelo, charlé con un amigo, murió mi madre), que suman el ánimo. El ánimo cambia el rendimiento; muy bajo, provoca crisis leves (se niega a trabajar un rato, se aísla).
- **Deseos:** la personalidad y las interacciones despiertan deseos (aprender una habilidad de alguien, hacerse amigo de otro, formar pareja, tener un hijo, una cama propia). Cumplir uno da un buen pensamiento; frustrarse, uno malo.
- **Parejas y nacimientos:** dos adultos con buena opinión y afinidad pueden formar pareja. Una pareja de mujer y varón en edad fértil puede tener hijos: la concepción es un evento abstracto, sin contenido explícito, y sigue un embarazo de duración fija en los defs y un parto. El bebé recibe nombre, sexo al azar y rasgos heredados.
- **Crianza:** los adultos alimentan a los bebés y los llevan a dormir; los niños juegan e interactúan, y así aprenden y forman su personalidad.
- **Ficha del colono:** edad, etapa, rasgos, habilidades, ánimo con sus pensamientos, deseos, relaciones y familia.
- **Opción de nacimientos:** el jugador puede permitir o impedir que una pareja tenga hijos, para no quedarse sin comida.

**Fuera de alcance:** contenido sexual explícito; enfermedades, heridas y medicina (IP5); cadáveres, tumbas y duelo con cementerio (IP5); habitaciones con calidad, dormitorios asignados y comedor (pasan a IP4, y suman pensamientos al ánimo de esta IP); escuela e instituciones (IP10); migrantes (IP6); biografías narradas y crónica (IP7).

## Hitos en detalle

| Hito | Contenido | Agentes | Horas | Entregable verificable |
|---|---|---|---|---|
| H3.1 Spec y contrato | `docs/gdd/vida-y-generaciones.md` con criterios de aceptación; defs propuestos (etapas, ritmos de la vida, rasgos y orientación, interacciones, habilidades, pensamientos, deseos, curva de vejez, fertilidad, apellidos), que entran como archivos en el hito que los usa; contrato de comandos, eventos y consultas; balance de población contra comida, según las decisiones del usuario | game-designer, Plan, humano | 6–8 | Spec aprobada por el usuario; el contrato permite trabajar en paralelo |
| H3.2 Edad y etapas de la vida | Fecha de nacimiento y edad; etapas por datos con lo que permiten; ritmo de la vida elegido en Nueva partida y guardado; muerte por vejez; fundadores de edades distintas; migración de partidas de v0.2.x (edad sorteada en forma determinista); edad y etapa en el inspector | sim-engineer, godot-engineer, i18n-specialist | 10–14 | Test: con Muy rápido, un colono llega a anciano y muere de viejo antes de un tope de días; misma semilla ⇒ mismo resultado; una partida de v0.2.5 carga |
| H3.3 Personalidad | Rasgos por datos; sorteo al llegar; herencia de los padres; deriva por vivencias con un tope por día; efecto en las decisiones (preferencias de trabajo, ganas de interactuar) | sim-engineer, game-designer | 8–12 | Tests de sorteo, herencia y deriva; dos colonos con rasgos opuestos eligen distinto en el mismo escenario |
| H3.4 Habilidades | Una habilidad por tipo de trabajo; experiencia al trabajar; curva de niveles y efecto en la velocidad, en los defs; aprendizaje por enseñanza | sim-engineer | 8–12 | Test: un colono que tala sube de nivel y tala más rápido; un aprendiz gana experiencia junto a un maestro |
| H3.5 Interacciones y relaciones | Búsqueda de interlocutor cercano y libre; tipos de interacción por datos, elegidos por personalidad y relación; opinión entre pares; amistad, rivalidad y pareja; lazos de familia; mensajes en el registro con concordancia de género | sim-engineer, i18n-specialist | 14–18 | Escenario: dos colonos sociables que conviven se hacen amigos en N días; dos incompatibles, rivales; textos en es y en |
| H3.6 Pensamientos, ánimo y deseos | Pensamientos por datos con intensidad y duración; ánimo y su efecto en el rendimiento; crisis leves; deseos que nacen de la personalidad y de las interacciones, cumplidos o frustrados | sim-engineer, game-designer, i18n-specialist | 12–16 | Tests: comer bien y charlar suben el ánimo; un deseo cumplido deja su pensamiento; con ánimo muy bajo aparece una crisis |
| H3.7 Parejas, nacimientos y crianza | Formación de pareja; fertilidad por edad; concepción, embarazo y parto; bebé con nombre, sexo y rasgos heredados; alimentar y acostar bebés; niños que juegan; opción de permitir o impedir hijos por pareja | sim-engineer, game-designer, i18n-specialist | 14–20 | Escenario: una pareja con comida tiene un hijo que crece hasta adulto con Muy rápido; con la opción apagada no hay embarazos |
| H3.8 Arte de las etapas | Bebé, niño, adolescente y anciano a partir de los aldeanos de Mini Characters de Kenney o de otro kit CC0 (escala, proporciones, canas); cuna; animaciones de jugar, alimentar y caminar despacio; catálogo de visuales y créditos | art-integrator, godot-engineer | 8–12 | Capturas de la aldea con las cinco etapas; benchmark dentro del presupuesto |
| H3.9 Ficha del colono | Panel con edad, etapa, rasgos, habilidades, ánimo y pensamientos, deseos, relaciones y familia; columna de edad y ánimo en el panel Colonia; opción de hijos en la ficha; opción de ritmo en Nueva partida | godot-engineer, i18n-specialist | 10–14 | Capturas en es y en a 1080p y 1440p sin textos cortados; el smoke abre la ficha |
| H3.10 Integración | Balance de población y comida con los tres ritmos; benchmark con la población de una partida larga; checklist de playtest; paquetes de Windows y macOS; release `v0.3.0` | qa-tester, game-designer, `/code-review`, humano | 6–10 | Checklist completo y `v0.3.0` publicada |

## Decisiones del usuario para la spec (H3.1)

Respondidas el 15 de octubre de 2026:

- **Trabajo infantil:** los niños sólo juegan. Los adolescentes hacen trabajos livianos.
- **Parejas:** se modela la orientación como parte de la personalidad, y cualquier pareja es posible entre adultos con orientaciones compatibles. Sólo una pareja de mujer y varón puede tener hijos.
- **Apellidos:** cada colono lleva apellido. Los fundadores llegan con el suyo y los hijos heredan el del padre. La spec define la lista de apellidos medievales, que no se traducen, como los nombres.
- **Tope de población:** en principio, sólo la comida. El benchmark de H3.10 dice si hace falta un tope por rendimiento.
- **Fundadores:** dos de los cinco llegan en pareja.

## Riesgos

- **Rendimiento:** más colonos y las interacciones cuestan tiempo por tick. Las interacciones se revisan cada cierto tiempo y no en cada tick; el benchmark de H3.10 mide una población de partida larga.
- **Balance:** la colonia puede crecer más rápido que la comida, o extinguirse de vieja. Se balancea con los tres ritmos, y la opción de nacimientos le da control al jugador.
- **Arte:** el kit de aldeanos no trae niños ni bebés. Si escalar los modelos no convence, H3.8 busca otro kit CC0.
- **Determinismo:** nacimientos y muertes crean y borran entidades. Cada sistema usa su flujo de RNG y recorre los colonos por `EntityId`.
