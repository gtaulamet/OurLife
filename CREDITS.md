# Créditos y licencias

Todo asset o código de terceros se registra aquí **antes** de entrar al repositorio. Sin licencia clara y compatible, no se usa. CC0 es la opción preferida; CC-BY se acepta con la atribución indicada abajo.

## Arte y fuentes

| Asset | Autor | Licencia | Fuente | Uso en el juego |
|---|---|---|---|---|
| Fuente Tiny5 | Stefan Schmidt, The Tiny5 Project Authors | SIL Open Font License 1.1; texto en `game/assets/fonts/tiny5/OFL.txt` | github.com/Gissio/font_tiny5, vía Google Fonts | Toda la interfaz |
| Nature Kit 2.1, 13 modelos | Kenney (www.kenney.nl) | CC0 1.0; texto en `game/assets/models/kenney_nature-kit/License.txt` | kenney.nl/assets/nature-kit | Árboles, arbustos, objetos del mapa y decoración del pasto |
| Mini Characters, 12 modelos y su textura, recoloreada | Kenney (www.kenney.nl) | CC0 1.0; texto en `game/assets/models/kenney_mini-characters/License.txt` | kenney.nl/assets/mini-characters | Colonos, con la ropa en tonos de época (la textura `colormap.png` se modificó en IP2.5). Los 12 `*-elder.glb` son derivados con canas, generados en H3.8 por `tools/make-elder-models.py` |
| Íconos de los globos de interacción | Propios del proyecto, dibujados por `tools/make-interaction-icons.py` | Del proyecto | `game/assets/icons/interactions` | Globos sobre los colonos que interactúan |
| Food Kit, 1 modelo y su textura | Kenney (www.kenney.nl) | CC0 1.0; texto en `game/assets/models/kenney_food-kit/License.txt` | kenney.nl/assets/food-kit | Bayas, sueltas y en los arbustos |
| KayKit Dungeon Remastered 1.0, 5 modelos | Kay Lousberg (www.kaylousberg.com) | CC0 1.0; texto en `game/assets/models/kaykit_dungeon-remastered/License.txt` | github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0 | Cama, mesa, silla, baúl y el barril de las provisiones |

## Código de terceros

| Componente | Autor | Licencia | Fuente | Uso |
|---|---|---|---|---|
| Algoritmo PCG32 (XSH-RR) | Melissa O'Neill | Algoritmo publicado; implementación propia en `src/OurLife.Core/Sim/Pcg32.cs`, verificada contra la salida de la implementación de referencia (Apache 2.0) | pcg-random.org | Generador aleatorio de la simulación |

## Motor y runtime que viajan con el juego exportado

| Componente | Licencia | Cómo se cumple |
|---|---|---|
| Godot Engine 4.7.2 y sus componentes de terceros (FreeType, ENet, mbedTLS y otros) | MIT (Expat) y las licencias de cada componente | `tools/package-windows.ps1` pide al ejecutable exportado sus avisos (`--licenses`) y los deja en `LICENSES.txt`, junto al juego |
| Runtime .NET 8 | MIT | Su aviso también va en `LICENSES.txt` |

## Paquetes NuGet

Las dependencias NuGet se declaran en los archivos `.csproj` y conservan sus propias licencias.

| Paquete | Licencia | Uso |
|---|---|---|
| System.IO.Hashing | MIT | Huella XxHash64 del estado de la simulación |
| xUnit, Shouldly, Microsoft.NET.Test.Sdk, coverlet.collector | Apache 2.0, BSD-3-Clause, MIT, MIT | Tests |
