# Guía de arte

## Dirección

- 3D low-poly sobre una grilla cuadrada, con cámara inclinada que rota en pasos de 90° (ADR-009). Hasta el 2026-10-09 el plan era pixel art 16×16 con vista cenital.
- Paleta sobria y terrosa para la Edad Media. Cada época podrá tener su paleta.
- Legibilidad antes que detalle: muro, suelo, árbol, objeto y colono se distinguen a simple vista desde la altura habitual de la cámara.

## Fuentes de assets

1. **[Nature Kit de Kenney](https://kenney.nl/assets/nature-kit)**, CC0: terreno, acantilados, árboles, arbustos, rocas y troncos para la superficie del valle. Al descargarlo hay que confirmar el contenido exacto y la licencia.
2. Otros kits 3D CC0 de Kenney para lo que falte, sobre todo colonos y objetos. Conviene elegir kits con el mismo estilo low-poly.
3. Modelos propios simples: el bloque de estrato para teñir, las escaleras y los marcadores de designación.
4. Otros assets CC0 o CC-BY, con atribución en `CREDITS.md`. Los modelos para épocas futuras pueden tener que encargarse o comprarse.

## Técnica

- Una celda del mapa mide una unidad de Godot de ancho y una de alto. La escala de cada modelo se ajusta en el catálogo, no en el modelo ni en el código.
- Formato `.glb`. Sólo se copian al repositorio los modelos que se usan, en `game/assets/models/<pack>/`, con sus `.import` versionados.
- Los modelos que se tiñen por material usan un material base gris. El color sale del `color` del `MaterialDef` y se aplica por instancia en el `MultiMesh`. Así un mismo bloque sirve para tierra, granito, caliza y arenisca.
- Profundidad: la vista corta el mapa en el nivel actual. La roca sin excavar del nivel actual muestra su cara superior oscurecida, y los niveles inferiores visibles se oscurecen según su distancia.
- Variantes por época con sufijo `@<era>`. El id sin sufijo es el de respaldo.

## Formato de visuals.json

Formato fijado en H1.5 (`game/assets/models/visuals.json`):

```json
{
  "terrain": { "grassColor": "#6A9A3C", "cutColor": "#3A332D" },
  "srgbAsLinearKits": ["res://assets/models/kenney_nature-kit/"],
  "palette": { "leafsGreen": "#4C7A2B", "woodBark": "#7A5233" },
  "models": {
    "plant.oak": { "model": "res://assets/models/kenney_nature-kit/tree_oak.glb", "scale": 1.5 },
    "house@modern": { "model": "res://assets/models/…/house_modern.glb" }
  }
}
```

- `terrain`: colores del terreno que no salen de un `MaterialDef`. `grassColor` es el suelo con pasto, y `cutColor` es el hueco de una escalera hacia abajo y la celda sin material.
- `srgbAsLinearKits`: carpetas de kits que guardan sus colores sRGB en el campo de color lineal de glTF, como los de Kenney. El importador los aclara una segunda vez, y el juego deshace esa conversión sólo en los modelos de esas carpetas.
- `palette`: reemplaza el color de un material de los modelos, por su nombre dentro del `.glb` (`leafsGreen`, `woodBark`, `grass`). Mantiene la paleta terrosa de la época. Sin entrada, se usa el color del kit corregido.
- `models`: el modelo de cada id visual y su escala (1 si falta). La escala hace que el modelo ocupe una celda.
- El terreno no tiene modelos: sus piezas se generan en código y se tiñen con el `color` del `MaterialDef` (ADR-009).

Los defs nombran su visual con el campo `visual`. Los tests de contenido (`VisualCatalogTests`) verifican que:

- Cada id exista sin sufijo de época.
- Cada modelo del catálogo lo use algún def.
- Las variantes nombren una época existente.
- Cada `.glb` exista con su `.import`.
- No haya `.glb` fuera del catálogo.
- Cada modelo de un kit de Kenney tenga su carpeta en `srgbAsLinearKits`.
- No haya campos desconocidos: un nombre mal escrito es un error, como en los defs.
- Cada color de la paleta reemplace un material de algún modelo.

## Modelos necesarios en IP1

| Grupo | Modelos | Origen probable |
|---|---|---|
| Terreno | Bloque gris para teñir (muro de tierra o roca), suelo de pasto, suelo de tierra, suelo de piedra para teñir, rampa, escalera hacia arriba, hacia abajo y en ambos sentidos | Nature Kit para el suelo y los acantilados; propios para el bloque y las escaleras |
| Plantas | Roble, pino, arbusto de bayas con y sin bayas | Nature Kit |
| Objetos | Tronco, piedra, bayas, provisiones | Nature Kit para el tronco y la piedra; otro kit o propios para el resto |
| Colonos | Entre 4 y 6 variantes de aldeanos | Kit de personajes CC0 de Kenney |
| Overlays | Marcadores de excavar, escalera, talar y recolectar; cursor de celda y selección | Mallas simples propias, semitransparentes |
| UI | Iconos de pausa y velocidad | Iconos 2D CC0 |

El art-integrator mantiene la lista de modelos que faltan.

### Modelos que faltan

Revisado en H1.5, el 10 de octubre de 2026:

| Modelo | Estado | Plan |
|---|---|---|
| Terreno | Resuelto con piezas generadas en código | — |
| Arbusto de bayas con bayas y sin bayas | Provisional: `plant_bushDetailed` del Nature Kit, más oscuro por la paleta, igual con o sin bayas | El kit no trae bayas. Hace falta un modelo propio o una variante con bayas rojas, como mucho en H1.7, cuando se recolecte |
| Bayas | Provisional: `flower_redA` | Buscar en otro kit de Kenney o hacer uno propio |
| Provisiones | Provisional: `pot_small` | Un saco o una canasta de otro kit de Kenney |
| Aldeanos | Provisional: un solo `character-male-a` de Mini Characters | Entre 4 y 6 variantes del mismo kit en H1.7, cuando se dibujen los colonos |
| Overlays de órdenes, cursor y selección | Pendiente | Mallas propias en H1.7 |
| Iconos de UI | Pendiente | H1.7 |

## Fuente tipográfica

**Tiny5**, de Stefan Schmidt, con licencia SIL OFL 1.1, en `game/assets/fonts/tiny5`. Se importa sin suavizado, sin hinting, sin posicionamiento subpíxel y sin fuentes de respaldo del sistema (`Tiny5.ttf.import`), y `UiTheme` la usa como fuente por defecto.

Se eligió en H1.3 tras comparar siete fuentes pixel con licencia OFL. El criterio fue cubrir todos los caracteres del español y distinguir a simple vista los caracteres que se confunden, porque el juego muestra muchas cantidades:

| Fuente | Resultado |
|---|---|
| Pixelify Sans | Descartada: 5 y S, 2 y Z, 0 y O se confunden |
| Silkscreen | Descartada: sólo mayúsculas, incómoda para textos largos |
| Micro5 | Descartada: demasiado pequeña |
| VT323 | Legible, con estética de terminal; alternativa si Tiny5 cansa en textos largos |
| DotGothic16, Jersey10 | Legibles, menos nítidas o más angostas |
| **Tiny5** | Elegida: nítida, caracteres bien diferenciados y cobertura completa |

Tamaños probados: 32, 48 y 64 píxeles. Se ven nítidos con zoom entero.

La UI sigue siendo 2D sobre el mapa 3D. En H1.5 se revisa sobre las capturas si la fuente pixel combina con el estilo low-poly; si no combina, se elige otra con el mismo criterio.

## Licencias

Todo asset de terceros se registra en `CREDITS.md` antes de entrar al repositorio.
