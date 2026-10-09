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

Es un borrador: el formato definitivo se fija en H1.5, y los nombres de archivo son ilustrativos.

```json
{
  "visuals": {
    "terrain.wall": { "model": "res://assets/models/own/block.glb", "tint": true },
    "terrain.grass": { "model": "res://assets/models/kenney_nature-kit/ground_grass.glb" },
    "plant.oak": { "model": "res://assets/models/kenney_nature-kit/tree_oak.glb", "scale": 1.0 },
    "plant.pine": { "model": "res://assets/models/kenney_nature-kit/tree_pineTallA.glb", "scale": 1.0, "rotationY": 0 }
  }
}
```

Los defs nombran su visual con el campo `visual`, que hasta H1.5 se llama `sprite`. Un test de contenido verifica que cada id exista en el catálogo.

## Modelos necesarios en IP1

| Grupo | Modelos | Origen probable |
|---|---|---|
| Terreno | Bloque gris para teñir (muro de tierra o roca), suelo de pasto, suelo de tierra, suelo de piedra para teñir, rampa, escalera hacia arriba, hacia abajo y en ambos sentidos | Nature Kit para el suelo y los acantilados; propios para el bloque y las escaleras |
| Plantas | Roble, pino, arbusto de bayas con y sin bayas | Nature Kit |
| Objetos | Tronco, piedra, bayas, provisiones | Nature Kit para el tronco y la piedra; otro kit o propios para el resto |
| Colonos | Entre 4 y 6 variantes de aldeanos | Kit de personajes CC0 de Kenney |
| Overlays | Marcadores de excavar, escalera, talar y recolectar; cursor de celda y selección | Mallas simples propias, semitransparentes |
| UI | Iconos de pausa y velocidad | Iconos 2D CC0 |

El art-integrator mantiene la lista de modelos que faltan y la revisa al empezar H1.5.

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
