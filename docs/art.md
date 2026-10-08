# Guía de arte

## Dirección

- Pixel art 16×16 con vista cenital.
- Paleta sobria y terrosa para la Edad Media. Cada época podrá tener su paleta.
- Legibilidad antes que detalle: muro, suelo, árbol, objeto y colono se distinguen a simple vista.

## Fuentes de assets

1. Packs CC0 de Kenney (kenney.nl), por ejemplo Tiny Town y Tiny Dungeon, que están pensados para combinarse. Verificar la licencia de cada pack antes de usarlo.
2. Otros packs CC0 o CC-BY, con atribución en `CREDITS.md`.
3. Sprites propios o encargados para lo que falte, sobre todo en épocas futuras.

## Técnica

- Sprites base en escala de grises, teñidos por material. El color está en el def del material.
- En el mapa, el tinte se aplica con "alternative tiles" generados en código (`TileData.Modulate`).
- Profundidad: el nivel actual a brillo completo y hasta 3 niveles inferiores cada vez más oscuros.
- Variantes por época con sufijo `@<era>`. El id sin sufijo es el de respaldo.

## Formato de atlas.json

Borrador; el formato definitivo se fija en H1.5.

```json
{
  "tileSize": 16,
  "sheets": {
    "town": "res://assets/sprites/kenney_tiny-town/tilemap_packed.png"
  },
  "sprites": {
    "terrain.grass": { "sheet": "town", "x": 0, "y": 0 },
    "wall.rough": { "sheet": "town", "x": 1, "y": 3, "tint": true },
    "plant.oak": { "sheet": "town", "x": 4, "y": 0 }
  }
}
```

## Sprites necesarios en IP1

| Grupo | Sprites |
|---|---|
| Terreno | Pasto, tierra, suelo de piedra, muro en gris para teñir, escalera hacia arriba, hacia abajo y en ambos sentidos, rampa |
| Plantas | Roble, pino, arbusto de bayas con y sin bayas |
| Objetos | Tronco, piedra, bayas, provisiones |
| Colonos | Entre 4 y 6 variantes de aldeanos |
| Overlays | Iconos de excavar, escalera, talar y recolectar; cursor y selección |
| UI | Iconos de pausa y velocidad |

## Fuente tipográfica

Fuente pixel libre con cobertura completa del español; ver `docs/i18n.md`.

## Licencias

Todo asset de terceros se registra en `CREDITS.md` antes de entrar al repositorio.
