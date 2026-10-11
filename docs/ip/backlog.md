# Backlog

Ideas y tareas fuera del alcance de la IP en curso. Se revisan al planificar cada IP.

## Técnico

- Migrar de `net8.0` a `net10.0` cuando Godot lo adopte o se valide que el editor funciona con él. El soporte de .NET 8 termina el 10 de noviembre de 2026. Hacerlo antes de IP8. Dato a favor: Godot 4.7.2 ya ejecuta el juego sobre el runtime .NET 10.0.8; falta probar compilar con `net10.0` y exportar.
- Correr el smoke test de Godot en la CI, instalando Godot en el runner.
- Biblioteca compartida de utilidades para tests: raíz del repositorio y mapas ASCII.
- Pantalla de error al arrancar: hoy, si el contenido no carga, el juego se cierra y el error sólo queda en el registro de Godot.
- Separador de miles en español según CLDR, que pide al menos dos dígitos de agrupación: "1234" y "12.345". .NET escribe "1.234".

## Interfaz

- Registro de eventos con el día, para IP2: hoy cada mensaje muestra sólo la hora, y en una partida larga no se sabe de qué día es cada uno. Propuesta: una línea con la fecha cuando cambia el día, o el día junto a la hora. Hallazgo de la pasada automática del playtest de IP1.

## Diseño

- ¿Elementos de fantasía en la ambientación medieval? Por ahora no.
- Salto temporal opcional entre épocas. Se decide en IP9.
- ¿Retroceso de época tras un colapso de la colonia?
- Siglo XX y actualidad, después de IP10.
- Fluidos dinámicos, multijugador, modo aventura y mundos persistentes entre partidas.
