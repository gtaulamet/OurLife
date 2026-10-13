# OurLife

Simulador de colonias al estilo de Dwarf Fortress. Un grupo de colonos funda una aldea medieval en un valle con niveles Z: talan, excavan, construyen y sobreviven. Más adelante, la aldea podrá avanzar por la Edad Moderna y la Contemporánea. El juego está en español por defecto y también en inglés.

**Estado:** versión `0.2.0`, el cierre de IP2 "Asentamiento". Lo que sigue es IP3 ([plan maestro](docs/plan-maestro.md)). Los registros están en [docs/ip/IP1.md](docs/ip/IP1.md) y [docs/ip/IP2.md](docs/ip/IP2.md).

## Cómo se juega (v0.2.0)

Cinco colonos llegan en primavera a un valle con 30 raciones. Comen, duermen de noche y cumplen tus órdenes: talan, excavan, recolectan, llevan lo suelto al almacén y construyen casas con muebles. Las bayas rebrotan cada pocos días, salvo en invierno: si no juntaste comida en otoño, la colonia muere de hambre. La partida se guarda desde la pausa y se autoguarda cada medianoche.

| Acción | Teclado | Ratón |
|---|---|---|
| Mover la cámara | WASD o flechas | |
| Zoom | | Rueda |
| Rotar 90° | Q / E | |
| Subir o bajar de nivel | RePág / AvPág | |
| Pausa y velocidad 1×, 2×, 4× | Espacio, 1, 2, 3 | Botones de la barra superior |
| Excavar, Escalera, Talar, Recolectar, Almacén, Desmontar, Canalizar, Cancelar | M, B, T, R, Z, V, H, X | Botones de la barra inferior |
| Construir: muros, suelos, puertas, rampas, escaleras y muebles, con el material elegido | C; G gira un mueble | Barra de construcción |
| Panel Colonia: actividad y prioridades de trabajo de cada colono | P | Botón de la barra superior |
| Marcar un área con la orden activa | | Arrastrar con el botón izquierdo |
| Seleccionar un colono o una celda | | Clic izquierdo sin orden |
| Salir de la orden o de la selección; menú de pausa | Esc | Clic derecho |

El idioma (español o inglés), la pantalla completa y el ciclo de día y noche se cambian en Opciones.

## Requisitos

| Herramienta | Versión |
|---|---|
| .NET SDK | 10.0.300 o posterior dentro de la banda 10.0.3xx (ver `global.json`) |
| Runtime .NET | 8.x, porque los proyectos usan `net8.0` |
| Godot | 4.7.2, edición .NET |

Después de instalar Godot, crea la variable de entorno de usuario `GODOT_BIN` con la ruta del ejecutable **de consola**. Ajusta la ruta a donde lo hayas descomprimido:

```powershell
[Environment]::SetEnvironmentVariable("GODOT_BIN", "C:\Herramientas\Godot\Godot_v4.7.2-stable_mono_win64_console.exe", "User")
```

## Compilar y probar

```powershell
dotnet build OurLife.sln
dotnet test OurLife.sln
& $env:GODOT_BIN --headless --path game -- --smoke
```

## Jugar o abrir el editor

```powershell
& $env:GODOT_BIN --path game            # juego
& $env:GODOT_BIN --path game --editor   # editor
```

## Exportar para Windows

Hacen falta las plantillas de exportación de Godot 4.7.2 .NET. Se instalan desde el editor (Editor > Administrar plantillas de exportación > Descargar e instalar), o a mano: el `.tpz` oficial de la versión 4.7.2 mono se descomprime en `%APPDATA%\Godot\export_templates\4.7.2.stable.mono`. Para Windows alcanzan `version.txt` y los `windows_*_x86_64*.exe`.

```powershell
./tools/package-windows.ps1
```

El script exporta a `builds/windows`, corre el smoke sobre el ejecutable exportado, agrega `LICENSES.txt` (avisos de Godot y de .NET) y `CREDITS.md`, y arma `builds/OurLife-v<versión>-windows-x86_64.zip`. La versión sale de `config/version` en `game/project.godot`. El juego exportado guarda sus preferencias en `%APPDATA%\OurLife`.

## Exportar para macOS

La versión de Mac es una app universal (Apple Silicon e Intel) y se exporta desde Windows. Hace falta además `macos.zip`, del mismo `.tpz` de las plantillas, en `%APPDATA%\Godot\export_templates\4.7.2.stable.mono`.

```powershell
./tools/package-macos.ps1
```

El script exporta `OurLife.app`, le agrega `LICENSES.txt` y `CREDITS.md` sin tocar los permisos del zip, y arma `builds/OurLife-v<versión>-macos-universal.zip`. No puede correr el smoke: eso se hace en una Mac.

**En la Mac:**

- La app va con firma ad hoc, sin notarizar, y la firma que genera Godot desde Windows no la acepta macOS en Apple Silicon: Gatekeeper la bloquea y, aun abierta desde la terminal, el sistema mata el proceso. Hay que firmarla de nuevo en la Mac, una vez, desde la Terminal en la carpeta donde se descomprimió:

  ```bash
  codesign --force --deep --sign - OurLife.app
  xattr -cr OurLife.app
  open OurLife.app
  ```

  Probado en una M1 con macOS Tahoe 26.6.2. Sin esos pasos, "Abrir de todos modos" en Privacidad y seguridad no alcanza.
- Smoke: `./OurLife.app/Contents/MacOS/OurLife --headless -- --smoke --seed 42 --ticks 5000`. Tiene que terminar con `[smoke] OK` y dar la misma huella que en Windows (la que figura en `docs/ip/bitacora.md`).
- El juego guarda sus preferencias y partidas en `~/Library/Application Support/OurLife`.

## Medir el rendimiento

```powershell
& $env:GODOT_BIN --path game -- --benchmark 20
```

Juega 20 segundos a 4× con órdenes, sin vsync, y comprueba el presupuesto: 60 FPS de media y menos de 2 ms por tick. Con `--seed 42 --ticks 5000` mide con la aldea ya construida.

## Git en el disco D:

El disco D: usa exFAT, que no guarda propietarios de archivos, y git rechaza el repositorio desde la terminal. Para usar git desde la línea de comandos, agrega una vez la excepción:

```powershell
git config --global --add safe.directory D:/Proyectos/Juegos/ourlife
```

## Estructura

| Ruta | Contenido |
|---|---|
| `src/OurLife.Core` | Simulación pura, sin Godot |
| `src/OurLife.Localization` | Textos e idiomas, sin Godot |
| `game/` | Proyecto Godot: código de presentación, datos y arte |
| `tests/` | Tests automáticos |
| `docs/` | Documentación del proyecto |
| `tools/` | Scripts, como el de exportación |

## Documentación

- [Plan maestro](docs/plan-maestro.md)
- [Visión del juego](docs/gdd/vision.md) y [progreso de épocas](docs/gdd/eras.md)
- [Arquitectura](docs/architecture/overview.md) y [decisiones (ADR)](docs/architecture/adr/)
- [Localización](docs/i18n.md) y [guía de arte](docs/art.md)
- [IP actual](docs/ip/IP1.md) y [backlog](docs/ip/backlog.md)

## Licencias

El arte, las fuentes y el código de terceros se registran en [CREDITS.md](CREDITS.md).
