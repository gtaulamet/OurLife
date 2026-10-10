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
# El valle en 3D desde la superficie más alta, y un corte en el nivel 12
& $env:GODOT_BIN --path game -- --screenshot out/valle.png --seed 1
& $env:GODOT_BIN --path game -- --screenshot out/corte.png --seed 1 --level 12
# La pantalla de prueba de idiomas
& $env:GODOT_BIN --path game -- --screenshot out/es.png --locale es --probe
& $env:GODOT_BIN --path game -- --screenshot out/en.png --locale en --probe
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
| `--ticks <n>` | Ticks a simular en `--smoke` | H1.2 |
| `--locale <es\|en>` | Fuerza el idioma | H1.3 |
| `--seed <n>` | Semilla de la partida | H1.2 |
| `--screenshot <png>` | Guarda una captura y sale | H1.3 |
| `--level <z>` | Nivel en el que arranca la vista 3D; sin él, la superficie más alta | H1.5 |
| `--probe` | Muestra la pantalla de prueba de idiomas de H1.3 en lugar del valle | H1.5 |
