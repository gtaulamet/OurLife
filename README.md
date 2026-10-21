# OurLife

Simulador de colonias al estilo de Dwarf Fortress. Un grupo de colonos funda una aldea medieval en un valle con niveles Z: talan, excavan, construyen y sobreviven. Más adelante, la aldea podrá avanzar por la Edad Moderna y la Contemporánea. El juego está en español por defecto y también en inglés.

**Estado:** versión `0.3.0`, el cierre de IP3 "Vida y generaciones": los colonos tienen edad, personalidad, habilidades, relaciones, ánimo y deseos, forman parejas, tienen hijos que crecen y mueren de viejos. Lo que sigue es IP3.5 "Libre albedrío" ([plan](docs/ip/IP3.5.md) y [plan maestro](docs/plan-maestro.md)). Los registros están en [docs/ip/IP1.md](docs/ip/IP1.md), [docs/ip/IP2.md](docs/ip/IP2.md), [docs/ip/IP2.5.md](docs/ip/IP2.5.md) y [docs/ip/IP3.md](docs/ip/IP3.md).

## Cómo se juega (v0.3.0)

Siete colonos llegan en primavera a un valle con 42 raciones; seis de ellos son tres parejas. Comen, duermen de noche y cumplen tus órdenes: talan, excavan, recolectan, llevan lo suelto al almacén y construyen casas con muebles. Las bayas rebrotan cada pocos días, salvo en invierno: si no juntaste comida en otoño, la colonia muere de hambre. La orden de recolectar un arbusto se cumple al recolectarlo: hay que volver a darla cuando rebrota.

Cada colono tiene edad, personalidad, habilidades que mejoran con el trabajo, opiniones de los demás, ánimo y deseos. Charlan, bromean, se enseñan y discuten; se hacen amigos, rivales o pareja. Las parejas tienen hijos si la aldea tiene comida guardada y el valle da para uno más; los bebés necesitan que alguien les lleve comida (Crianza), los niños juegan y los ancianos mueren de viejos. En Nueva partida eliges el ritmo de la vida: Normal, Rápido o Muy rápido. La partida se guarda desde la pausa y se autoguarda cada medianoche.

| Acción | Teclado | Ratón |
|---|---|---|
| Mover la cámara | WASD o flechas | |
| Zoom | + / - (mantener) | Rueda; en el trackpad, pellizcar o deslizar dos dedos |
| Rotar 90° | Q / E | |
| Subir o bajar de nivel | RePág / AvPág | |
| Pausa y velocidad 1×, 2×, 4× | Espacio, 1, 2, 3 | Botones de la barra superior |
| Excavar, Escalera, Talar, Recolectar, Almacén, Desmontar, Canalizar, Cancelar | M, B, T, R, Z, V, H, X | Botones de la barra inferior |
| Construir: muros, suelos, puertas, rampas, escaleras y muebles, con el material elegido. Un mueble se ve antes de colocarlo, con una flecha hacia su frente | C; G gira un mueble | Barra de construcción |
| Panel Colonia: edad, ánimo, actividad y prioridades de trabajo de cada colono | P | Botón de la barra superior |
| Ficha del colono seleccionado: personalidad, habilidades, familia, pensamientos, deseos y relaciones; la opción de hijos de una pareja | F | Botón del inspector |
| Marcar un área con la orden activa | | Arrastrar con el botón izquierdo |
| Seleccionar un colono o una celda | | Clic izquierdo sin orden |
| Salir de la orden o de la selección; menú de pausa | Esc | Clic derecho |

El idioma (español o inglés), la pantalla completa, el tamaño de la interfaz (automático según la resolución, o de 100 % a 300 %), la decoración del pasto (nada, poca, normal o mucha) y el ciclo de día y noche se cambian en Opciones.

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

El script, que necesita PowerShell 7.4 o posterior, exporta `OurLife.app`, le agrega `LICENSES.txt` y `CREDITS.md`, vuelve a marcar las entradas del zip como hechas en Unix para que la Mac respete los permisos de ejecución, y arma `builds/OurLife-v<versión>-macos-universal.zip`. No puede correr el smoke: eso se hace en una Mac.

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

## Exportar para Linux ARM64 (Raspberry Pi 4)

La versión para Linux en ARM64 se exporta desde Windows. Hacen falta además `linux_release.arm64` y `linux_debug.arm64`, del mismo `.tpz` de las plantillas, en `%APPDATA%\Godot\export_templates\4.7.2.stable.mono`.

```powershell
./tools/package-linux-arm64.ps1
```

El script, que necesita PowerShell 7.4 o posterior, exporta a `builds/linux-arm64`, agrega `LICENSES.txt` y `CREDITS.md`, arma `builds/OurLife-v<versión>-linux-arm64.zip` y marca el ejecutable como ejecutable dentro del zip. Las texturas van en ETC2/ASTC, que es lo que lee la GPU de la Pi (no S3TC, como en Windows). No puede correr el smoke: eso se hace en la Pi.

**En la Raspberry Pi 4** (Raspberry Pi OS de 64 bits):

- La GPU de la Pi no tiene OpenGL 3.3 de escritorio; el juego cae solo a OpenGL ES 3 (`rendering/gl_compatibility/fallback_to_gles`). Hace falta el escritorio con el driver gráfico de Mesa (V3D), el que trae el sistema.
- Smoke: `./OurLife.arm64 --headless -- --smoke --seed 42 --ticks 5000`. Tiene que terminar con `[smoke] OK` y dar la misma huella que en Windows (la que figura en `docs/ip/bitacora.md`).
- Rendimiento: `./OurLife.arm64 -- --benchmark 20`. El presupuesto del juego (60 FPS) es el de una PC; en la Pi se esperan muchos menos. Bajar la "Decoración del pasto" en Opciones ayuda.
- El juego guarda sus preferencias y partidas en `~/.local/share/OurLife`.

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
