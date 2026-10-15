# Progreso de épocas

**Estado:** diseño preliminar. Se implementa en IP9 (Edad Moderna) e IP10 (Edad Contemporánea), después de la versión 1.0 medieval. Desde la IP1 la arquitectura no asume ninguna época (ADR-008).

## Épocas previstas

| Id | Época | Qué la caracteriza | IP |
|---|---|---|---|
| `medieval` | Edad Media | Aldea, madera y piedra, herrería, arcos | IP1 a IP8 |
| `modern` | Edad Moderna | Imprenta, pólvora, vidrio, manufacturas, villa | IP9 |
| `contemporary` | Edad Contemporánea: Revolución Industrial | Vapor, electricidad, fábricas, saneamiento | IP10 |
| (backlog) | Siglo XX y actualidad | | Sin fecha |

## Modelo recomendado

Progreso tecnológico continuo, al estilo de Dawn of Man:

- La colonia avanza de época al reunir conocimientos clave y cumplir hitos de población, edificios y producción.
- El calendario sigue corriendo desde el año de inicio; no hay saltos obligatorios.
- Las generaciones se renuevan con nacimientos y vejez (IP3).
- El cambio de época es un evento: pantalla propia, desbloqueos y una entrada en la crónica de la colonia.

**Pregunta abierta para IP9:** si conviene además un salto temporal opcional entre épocas.

## Cómo se modela en datos

Cada época es un def:

```jsonc
{
  "type": "Era",
  "id": "modern",
  "order": 1,
  "advance": {
    "requiresKnowledge": ["printing_press", "gunpowder"],
    "minPopulation": 40
  },
  "theme": "modern",   // tema de UI
  "music": "modern"    // lista de música
}
```

Todo def de contenido puede declarar:

| Campo | Significado | Si falta |
|---|---|---|
| `era` | Época mínima en la que existe | Existe desde la primera época |
| `requires` | Conocimientos necesarios para desbloquearlo | No requiere conocimientos |

`UnlockService` decide qué está disponible: la época actual debe ser igual o posterior a `era` y la colonia debe tener todos los conocimientos de `requires`.

Otras piezas por época:

- **Modelos 3D:** variantes con sufijo `@<era>` en el catálogo de visuales, por ejemplo `house@modern`; si no existe, se usa `house`.
- **UI y música:** tema y lista de música por época.
- **Textos:** los términos que cambian usan `select` sobre `era`, por ejemplo aldea, villa o ciudad.
- **Nombres de colonos:** listas por época.

## Ganchos que se construyen antes de IP9

| IP | Gancho | Costo |
|---|---|---|
| IP1 | `EraDef` con sólo `medieval`, campos `era` y `requires`, `UnlockService`, calendario con año absoluto, variantes de modelo por época | Unas 4 h |
| IP2 | Época y conocimientos dentro del formato de guardado | Incluido |
| IP3 | Generaciones: edad, nacimientos y muerte por vejez, adelantadas de IP9 | Incluido |
| IP4 | Árbol de conocimientos medieval que bloquea recetas y edificios | Unas 10 h |
| IP5 | Armas a distancia genéricas por datos: arco y ballesta, luego mosquete | Incluido |
| IP7 | Facciones con nivel tecnológico propio | Incluido |

## Preguntas abiertas

- ¿Salto temporal opcional entre épocas?
- ¿Puede la colonia retroceder de época tras un colapso?
- ¿Qué pasa con los edificios obsoletos: se mantienen, se degradan o se pueden renovar?
- ¿Qué población y tamaño de mapa necesita cada época, y cuánto se simplifica la simulación de los habitantes fuera de foco?
- Arte de épocas modernas: ¿comprar packs o encargar modelos 3D?
