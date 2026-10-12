---
name: godot-run
description: Compilar OurLife, correr los tests, lanzar el juego o el editor de Godot y ejecutar los modos --smoke y --screenshot. Usar cuando se pida ejecutar, probar, verificar o capturar el juego.
---

# Ejecutar OurLife

## Requisitos

- Godot 4.7.2 edición .NET.
- Variable de entorno `GODOT_BIN` con la ruta del ejecutable **de consola**, por ejemplo `Godot_v4.7.2-stable_mono_win64_console.exe`. El ejecutable normal es una aplicación de ventana: la terminal no espera a que termine y no recibe su salida ni su código.

La variable está definida a nivel de usuario, pero la sesión de PowerShell de Claude Code puede no heredarla. En ese caso, léela con:

```powershell
$env:GODOT_BIN = [System.Environment]::GetEnvironmentVariable('GODOT_BIN','User')
```

Si `GODOT_BIN` no existe, avisa al usuario y limítate a compilar y a correr los tests.

## Compilar y testear

```powershell
dotnet build OurLife.sln
dotnet test OurLife.sln
```

## Importar assets

Necesario la primera vez, después de agregar assets y después de cambiar un `.import`:

```powershell
& $env:GODOT_BIN --headless --path game --import
```

Si esa versión no admite `--import`, abre el editor una vez con `& $env:GODOT_BIN --path game --editor`.

Si un `.import` cambia dentro del mismo segundo que la importación anterior, Godot no lo reimporta: borra `game/.godot/imported/<archivo>-*` y vuelve a correr `--import`.

## Smoke test, sin ventana

```powershell
& $env:GODOT_BIN --headless --path game -- --smoke --seed 42 --ticks 5000
$LASTEXITCODE   # 0 = OK, 1 = fallo, 2 = argumentos inválidos
```

## Captura, con ventana

```powershell
# El valle en 3D desde la superficie más alta, y un corte en el nivel 12 (sin --play se ve el menú)
& $env:GODOT_BIN --path game -- --screenshot out/valle.png --seed 1 --play
& $env:GODOT_BIN --path game -- --screenshot out/corte.png --seed 1 --play --level 12
# La pantalla de prueba de idiomas
& $env:GODOT_BIN --path game -- --screenshot out/es.png --locale es --probe
& $env:GODOT_BIN --path game -- --screenshot out/en.png --locale en --probe
# El menú principal y las opciones, desde H1.7
& $env:GODOT_BIN --path game -- --screenshot out/menu.png --locale es
& $env:GODOT_BIN --path game -- --screenshot out/opciones.png --locale es --options
# La partida de cerca, con órdenes, el inspector y el registro; y las opciones sobre ella
& $env:GODOT_BIN --path game -- --screenshot out/partida.png --locale es --seed 42 --play --level 14 --zoom 22 --ticks 200 --orders --inspect
& $env:GODOT_BIN --path game -- --screenshot out/pausa.png --locale en --seed 42 --play --options
# Ciclo de día y noche: amanecer, mediodía, atardecer y noche
& $env:GODOT_BIN --path game -- --screenshot out/noche.png --seed 42 --play --level 14 --zoom 30 --orders --hour 0
# Partidas guardadas: --ticks 1500 pasa la medianoche y autoguarda en out/saves
& $env:GODOT_BIN --path game -- --screenshot out/partida.png --seed 42 --play --ticks 1500 --saves out/saves
& $env:GODOT_BIN --path game -- --screenshot out/cargar.png --locale es --saves out/saves --show load
& $env:GODOT_BIN --path game -- --screenshot out/guardar.png --locale en --saves out/saves --load <ranura> --show save
& $env:GODOT_BIN --path game -- --screenshot out/error.png --locale es --show error
```

Las capturas necesitan ventana, así que se corren sin `--headless`. Una ruta relativa parte de `game/`, porque Godot arranca en esa carpeta: los ejemplos guardan en `game/out/`. Revisa las imágenes con Read. La carpeta `out/` no se versiona.

## Jugar o abrir el editor

```powershell
& $env:GODOT_BIN --path game
& $env:GODOT_BIN --path game --editor
```

## Argumentos propios del juego

Van después de `--`:

| Argumento | Efecto | Disponible desde |
|---|---|---|
| `--smoke` | Arranca, valida y sale con código 0 o distinto de 0 | H1.1 |
| `--ticks <n>` | Ticks a simular en `--smoke`; con `--play`, ticks que corren de una vez al empezar | H1.2, H1.7 |
| `--locale <es\|en>` | Fuerza el idioma | H1.3 |
| `--seed <n>` | Semilla de la partida | H1.2 |
| `--screenshot <png>` | Guarda una captura y sale | H1.3 |
| `--level <z>` | Nivel en el que arranca la vista 3D; sin él, la superficie más alta | H1.5 |
| `--probe` | Muestra la pantalla de prueba de idiomas de H1.3 en lugar del menú principal | H1.5 |
| `--play` | Empieza una partida en lugar de mostrar el menú principal, con `--seed` o la semilla 1 | H1.7 |
| `--options` | Abre las opciones: sobre la partida con `--play`, si no en lugar del menú | H1.7 |
| `--orders` | Con `--play`, da las órdenes del smoke alrededor de la llegada, con un almacén desde H2.3 y una casa de madera con puerta desde H2.4, amueblada desde H2.5 | H1.7 |
| `--inspect` | Con `--play`, selecciona al primer colono para ver el inspector | H1.7 |
| `--zoom <n>` | Distancia de la cámara a lo que mira; menos es más cerca (por defecto 85) | H1.7 |
| `--licenses <archivo>` | Escribe los avisos de licencia del motor y de .NET que viajan con el juego exportado, y sale. Lo usa `tools/package-windows.ps1` | H1.8 |
| `--inspect-stockpile` | Con `--play` o `--load`, selecciona la primera celda del primer almacén para ver sus filtros en el inspector | H2.3 |
| `--inspect-blueprint` | Con `--play` o `--load`, selecciona la celda del primer plano para verlo en el inspector | H2.4 |
| `--build` | Con `--play` o `--load`, enciende la herramienta de construcción para ver su barra | H2.4 |
| `--inspect-furniture` | Con `--play` o `--load`, selecciona la celda del primer mueble para verlo en el inspector | H2.5 |
| `--hour <h>` | Con `--play` o `--load`, después de `--ticks`, lleva la partida hasta esa hora, para capturas del ciclo de día y noche | H2.2 |
| `--saves <carpeta>` | Carpeta de las partidas guardadas en lugar de `user://saves`, relativa a `game/`. Las capturas y el benchmark sólo guardan y autoguardan si se da | H2.1 |
| `--load <ranura>` | Carga la partida de esa ranura (el nombre del archivo sin `.olsave`) en lugar del menú. Si no carga, sale con 1 | H2.1 |
| `--show <load\|save\|error>` | Abre Cargar partida en lugar del menú; Guardar partida sobre la partida de `--play` o `--load`; o el error de una partida dañada | H2.1 |
| `--benchmark <s>` | Juega `<s>` segundos a 4× con las órdenes del smoke, sin vsync, y mide FPS y ms por tick. Sale con 1 si no llega a 60 FPS de media o pasa de 2 ms por tick. Necesita ventana | H1.8 |

Sin argumentos, el juego abre el menú principal. El smoke, además de simular, guarda y carga la partida a mitad de los ticks (misma huella al cargar y al final), escribe y carga una ranura en una carpeta temporal, arma la pantalla de partida sobre el mundo que trabajó, selecciona un colono con un clic y abre la pausa, las opciones, Guardar, Cargar, el error y los menús; falla si falta un texto.

## Exportar

`./tools/package-windows.ps1` exporta el preset "Windows Desktop", corre el smoke sobre el ejecutable exportado y arma el zip en `builds/`. El ejecutable exportado es de ventana: para leer su código de salida se lanza con `Start-Process -Wait -PassThru`, y su salida queda en `%APPDATA%\OurLife\logs\godot.log`.

## Archivos `.uid`

Un script nuevo no tiene `.uid` hasta que el editor abre el proyecto. `& $env:GODOT_BIN --headless --path game --import` los genera sin abrir el editor; se versionan.
