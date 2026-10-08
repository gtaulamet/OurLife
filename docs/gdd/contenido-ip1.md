# Contenido inicial de la IP1

Spec de diseño del contenido que define H1.3 en `game/content/defs`. Los números son el punto de partida del balance y se ajustan en H1.8 con pruebas de juego.

## Tiempo

| Valor | Cantidad | Motivo |
|---|---|---|
| Ticks por segundo a 1× | 10 | Movimiento fluido sin exigir mucho al procesador |
| Minutos de juego por tick | 1 | Un día dura unos 2,4 minutos reales a 1× |
| Días por estación | 15 | Un año de 60 días se juega en unas 2,4 horas a 1× |
| Año inicial | 1200 | Plena Edad Media |
| Hora inicial | 6:00 | La partida empieza con luz |

## Materiales del terreno

| Id | Tipo | Ticks de excavación | Deja |
|---|---|---|---|
| `soil` | Tierra | 30 | Nada |
| `sandstone` | Piedra | 100 | Piedra, 25 % |
| `limestone` | Piedra | 120 | Piedra, 25 % |
| `granite` | Piedra | 180 | Piedra, 25 % |

- La tierra es rápida de cavar para que bajar al subsuelo no frustre.
- La piedra suelta sale una de cada cuatro veces, como en Dwarf Fortress: alcanza para construir sin llenar el mapa de objetos.

## Plantas

| Id | Bloquea el paso | Acción | Rinde | Ticks |
|---|---|---|---|---|
| `oak` | Sí | Talar | 4 troncos | 240 |
| `pine` | Sí | Talar | 3 troncos | 180 |
| `berry_bush` | No | Recolectar, vuelve a dar fruto en 4 días | 8 bayas | 60 |

## Objetos

| Id | Tipo | Pila máxima | Nutrición |
|---|---|---|---|
| `log` | Materia prima | 25 | |
| `stone` | Materia prima | 25 | |
| `berries` | Comida | 75 | 400 |
| `provisions` | Comida | 50 | 2500 |

## Necesidades

Escala de 0 a 10 000.

| Id | Baja por hora | Umbral bajo | Umbral crítico | Muerte en cero |
|---|---|---|---|---|
| `hunger` | 200 | 4000 | 1500 | Tras 72 horas |
| `rest` | 600 | 3000 | 1000 | Nunca |

- **Hambre:** un colono gasta 4800 puntos por día, unas dos raciones de provisiones. Las 30 raciones iniciales alimentan a 5 colonos durante unos 3 días; después hay que recolectar.
- **Bayas:** un arbusto da 3200 puntos cada 4 días, unos 800 por día. Hacen falta unos 6 arbustos por colono para vivir sólo de bayas, así que el valle debe tener decenas.
- **Descanso:** se agota en unas 16 horas de vigilia y se recupera durmiendo, lo que se define con los trabajos en H1.6.

## Criatura

`human`: tarda 2 ticks por casilla, tiene hambre y descanso, y toma sus nombres de la lista `medieval_names`. La lista mezcla nombres ibéricos e ingleses de la época, 20 masculinos y 20 femeninos. Los nombres no se traducen.

## Época

`medieval`, orden 0. Todo el contenido de la IP1 existe desde el principio; los campos `era` y `requires` quedan listos para las épocas futuras (ADR-008).
