---
name: godot-run
description: Compilar OurLife, correr los tests, lanzar el juego o el editor de Godot y ejecutar los modos --smoke y --screenshot. Usar cuando se pida ejecutar, probar, verificar o capturar el juego.
---

# Ejecutar OurLife

## Requisitos

- Godot 4.7.2 edición .NET.
- Variable de entorno `GODOT_BIN` con la ruta del ejecutable **de consola**, por ejemplo `Godot_v4.7.2-stable_mono_win64_console.exe`. El ejecutable normal es una aplicación de ventana: la terminal no espera a que termine y no recibe su salida ni su código.

Si `GODOT_BIN` no existe, avisa al usuario y limítate a compilar y a correr los tests.

## Compilar y testear

```powershell
dotnet build OurLife.sln
dotnet test OurLife.sln
```

## Importar assets

Necesario la primera vez y después de agregar assets:

```powershell
& $env:GODOT_BIN --headless --path game --import
```

Si esa versión no admite `--import`, abre el editor una vez con `& $env:GODOT_BIN --path game --editor`.

## Smoke test, sin ventana

```powershell
& $env:GODOT_BIN --headless --path game -- --smoke --seed 42 --ticks 5000
$LASTEXITCODE   # 0 = OK, 1 = fallo, 2 = argumentos inválidos
```

## Captura, con ventana (disponible desde H1.5)

```powershell
& $env:GODOT_BIN --path game -- --screenshot out/es.png --locale es --seed 42
& $env:GODOT_BIN --path game -- --screenshot out/en.png --locale en --seed 42
```

Revisa las imágenes con Read. La carpeta `out/` no se versiona.

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
| `--seed <n>` | Semilla de la partida | H1.4 |
| `--screenshot <png>` | Guarda una captura y sale | H1.5 |
