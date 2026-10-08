# OurLife

Simulador de colonias al estilo de Dwarf Fortress. Un grupo de colonos funda una aldea medieval en un valle con niveles Z: talan, excavan, construyen y sobreviven. Más adelante, la aldea podrá avanzar por la Edad Moderna y la Contemporánea. El juego está en español por defecto y también en inglés.

**Estado:** IP1 en desarrollo. El avance de los hitos está en [docs/ip/IP1.md](docs/ip/IP1.md).

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

## Documentación

- [Plan maestro](docs/plan-maestro.md)
- [Visión del juego](docs/gdd/vision.md) y [progreso de épocas](docs/gdd/eras.md)
- [Arquitectura](docs/architecture/overview.md) y [decisiones (ADR)](docs/architecture/adr/)
- [Localización](docs/i18n.md) y [guía de arte](docs/art.md)
- [IP actual](docs/ip/IP1.md) y [backlog](docs/ip/backlog.md)

## Licencias

El arte, las fuentes y el código de terceros se registran en [CREDITS.md](CREDITS.md).
