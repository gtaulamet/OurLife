# Estaciones visibles (L4c de IP3.5)

Pedido del usuario del 18 de octubre de 2026. Aprobada en L1 de IP3.5 el 19 de octubre, con un cambio del usuario: en otoño los pinos también cambian de color, y en invierno todos los árboles tienen algo de nieve. El cambio de estación tiene que notarse en el paisaje.

**Estado:** hecho en L4c (21 de octubre de 2026).

**Objetivo.** Que el valle cambie de color con la estación: verde pleno en primavera y verano, amarillos y rojizos en otoño, blanco en invierno. Así el jugador ve cómo pasa el año sin mirar la fecha.

Es de presentación, igual que el ciclo de día y noche: no cambia la simulación, ni el guardado, ni las huellas. La estación sale del calendario que ya existe (cuatro estaciones de 15 días).

## Qué cambia en cada estación

| Elemento | Primavera | Verano | Otoño | Invierno |
|---|---|---|---|---|
| Pasto del terreno y decoración | Verde claro y fresco | Verde de hoy, algo más seco | Verde amarillento y pardo | Casi blanco, como escarcha y nieve |
| Robles y arbustos (hoja caduca) | Verde claro | Verde de hoy | Amarillos, naranjas y rojizos, con variación entre árboles | Su color con nieve encima, más blanco arriba |
| Pinos (hoja perenne) | Verde de hoy | Verde de hoy | Verde con tonos amarillos y rojizos, con variación entre pinos, algo menos que los robles | Verde oscuro con nieve encima |
| Roca expuesta en las cumbres | Sin cambio | Sin cambio | Sin cambio | Blanca por encima de una altura, como nieve |
| Flores | Más flores | Como hoy | Pocas | Ninguna |

- **Transición suave:** el color pasa de una estación a la otra en los últimos días de cada una (propuesta: 3 días), sin saltos a medianoche.
- **Nieve en las cumbres:** sólo en las caras de arriba de la roca y por encima de una altura del valle. Las paredes verticales siguen mostrando la piedra. Más arriba, más blanco.
- **Variación del otoño:** cada árbol toma un tono propio entre amarillo y rojo, según su posición, para que el bosque no se vea de un solo color. Los pinos también, mezclado con su verde.
- **Nieve en los árboles:** en invierno, las copas de robles y pinos se blanquean por arriba, sin perder del todo su color, así se siguen distinguiendo. Con el pasto casi blanco, el paisaje cambia a la vista.
- **Arbustos sin bayas:** el arbusto ya tiene estaciones dormidas en la simulación (`dormantSeasons`). En invierno se ve pelado y con nieve.

## Datos

Todo vive en el catálogo de visuales (`game/assets/models/visuals.json`), en un bloque `seasons` por id de estación (`spring`, `summer`, `autumn`, `winter`):

- Los tres verdes del pasto del shader del terreno (`lush`, `dry`, `deep`) por estación.
- El tinte de las hojas por tipo (`deciduous` o `evergreen`), con dos colores para el otoño entre los que se reparte cada árbol, cuánto los toma cada tipo y cuánta nieve llevan en invierno.
- La nieve: color, altura donde empieza (como fracción de la altura del valle) y cuánto blanquea el pasto.
- La probabilidad de cada flor por estación.
- Los días de transición.

Cada modelo de planta dice en el catálogo si es de hoja caduca o perenne. Sin dato, no cambia de color.

## Cómo se hace

- **Terreno:** el shader del terreno (ADR-012) ya recibe los verdes del pasto como uniformes. Se agregan la nieve y su altura. La sesión los actualiza una vez por hora del juego, con la mezcla de las dos estaciones. Cuesta lo mismo que hoy.
- **Árboles y arbustos:** las copas usan materiales compartidos por nombre de la paleta (`leafsGreen`, `leafsDark`). Se cambian por un material con shader que mezcla el tinte de la estación y una variación por instancia. Un cambio por hora para todo el valle.
- **Decoración:** el pasto toma el mismo tinte que el terreno. Las flores aparecen según la probabilidad de la estación, al rearmar la decoración del chunk.
- **Capturas:** una opción `--season <id>` fuerza la estación para revisar las cuatro.

## Criterios de aceptación

- Capturas del mismo valle en las cuatro estaciones: otoño amarillo y rojizo, en robles y pinos; invierno blanco, con nieve sobre los árboles y las cumbres nevadas; primavera y verano verdes. El usuario tiene que poder decir la estación mirando la captura.
- Un día antes y uno después del cambio de estación, el color es parecido: la transición se nota en días, no de golpe.
- La huella del smoke no cambia con L4c.
- Los FPS con el valle completo no bajan más de un 5 % respecto de antes de L4c.

## Fuera de L4c

La nieve que cae, la lluvia, la niebla, la nieve que se acumula y el frío como riesgo siguen en IP6 "Estaciones". L4c sólo pinta: no hay nieve como objeto en el mapa, y en invierno los colonos caminan igual.

## Decisiones de L1

- El pasto se blanquea de a poco, y en el centro del invierno queda casi blanco.
- La nieve en la roca empieza en el último tercio de la altura del valle.
- Robles y pinos amarillos y rojizos en otoño, y con algo de nieve en invierno (pedido del usuario).
