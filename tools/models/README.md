# Generador de modelos propios

Scripts de Python (`numpy` y `trimesh`) que arman con cajas y cilindros los modelos low-poly de `docs/plan-modelos.md` y los exportan como `.glb`. No hace falta Blender.

| Archivo | Contenido |
|---|---|
| `lib.py` | `Model` (cajas, cilindros y esferas con materiales con nombre), paleta de colores, exportación y vista previa |
| `fase1.py` | Banco de carpintero, banco de cantero, cocina, escritorio de copista y cuna |
| `fase2.py` | Objetos de comida, tablón y mango |
| `fase3.py` | Sillar, mineral, lingote, carbón y las cuatro herramientas de hierro |
| `fase4.py` | Los tres cultivos en tres etapas |
| `fase5.py` | Mesa, cama, silla y baúl |
| `fase6.py` | Tocones de roble y de pino |

```powershell
cd tools/models
python fase1.py
```

- Cada script escribe los `.glb` en `MODELS/<tipo>/` (carpeta que no se versiona) y una vista previa en `tools/models/previews/` (ignorada por git), e imprime tamaño, triángulos y si cabe en una celda.
- Los modelos que usa el juego se copian a `game/assets/models/ourlife/` y se registran en `game/assets/models/visuals.json`.
- `pendientes/` guarda los modelos listos que todavía no están en el juego: los tocones (`stump_oak.glb`, `stump_pine.glb`) esperan a que el talado deje un tocón (`docs/ip/backlog.md`). No van a `game/assets` porque el test `Every_model_of_the_catalog_is_used_by_a_def` exige que un def use cada modelo del catálogo.
- Convenciones: origen en el centro de la base sobre Y = 0, frente hacia +Z, materiales con nombre (`wood`, `metal`, `stone`, `cloth`…) que el catálogo puede recolorear.
