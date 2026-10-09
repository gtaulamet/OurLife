# ADR-001: Godot 4.7 .NET con C#

- **Estado:** aceptada
- **Fecha:** 2026-10-08

## Contexto

El juego necesita render 2D de tiles, una UI rica, audio, exportación a escritorio y un lenguaje con buen rendimiento para una simulación grande. El desarrollador conoce C# y trabaja con agentes de Claude Code, que deben poder compilar y probar sin abrir el editor.

## Decisión

- Motor Godot 4.7.2, edición .NET, con C#. Es la última versión estable publicada al iniciar el proyecto.
- Renderizador GL Compatibility: el juego es 2D pixel art y así funciona en más equipos.
- Todos los proyectos usan `net8.0`, porque GodotSharp 4.7.2 está compilado para .NET 8.
- Godot deriva los nombres de la solución y del proyecto del nombre de ensamblado. Con `dotnet/project/assembly_name="OurLife"` y `dotnet/project/solution_directory="../"`, los archivos son `OurLife.sln` en la raíz y `game/OurLife.csproj`. El namespace del juego es `OurLife.Game`.
- La solución define las configuraciones `ExportDebug` y `ExportRelease`, que Godot usa al exportar. Los tests no se compilan en ellas.
- Desde la terminal se usa el ejecutable de consola de Godot (`..._console.exe`), que espera al proceso y devuelve su salida y su código de salida.

## Consecuencias

- El soporte de .NET 8 termina el 10 de noviembre de 2026. Hay que migrar a `net10.0` cuando Godot lo adopte o cuando se valide que el editor funciona con él, antes de IP8. Está anotado en el backlog.
- `Godot.NET.Sdk` en `game/OurLife.csproj` debe coincidir siempre con la versión del editor instalado.
- La lógica pesada vive fuera de Godot (ADR-002), lo que limita la dependencia del motor.

## Actualización del 2026-10-09

El render pasa a 3D low-poly (ADR-009). GL Compatibility se mantiene porque alcanza para modelos low-poly con una luz direccional; se pasará a Forward+ sólo si hacen falta sombras o efectos que Compatibility no da.
