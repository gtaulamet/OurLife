# Backlog

Ideas y tareas fuera del alcance de la IP en curso. Se revisan al planificar cada IP.

## Técnico

- Migrar de `net8.0` a `net10.0` cuando Godot lo adopte o se valide que el editor funciona con él. El soporte de .NET 8 termina el 10 de noviembre de 2026. Hacerlo antes de IP8. Dato a favor: Godot 4.7.2 ya ejecuta el juego sobre el runtime .NET 10.0.8; falta probar compilar con `net10.0` y exportar.
- Correr el smoke test de Godot en la CI, instalando Godot en el runner.
- Biblioteca compartida de utilidades para tests: raíz del repositorio y mapas ASCII.
- Autoguardado sin tirón: hoy el guardado corre en el hilo principal entre dos cuadros (unos 30 ms con el valle de IP1). Capturar el estado en el hilo principal y escribir el archivo en segundo plano, si con mapas más grandes se nota. Hallazgo de la revisión de H2.1.
- macOS sin Terminal: firmar con una cuenta de Apple Developer (99 USD por año) y notarizar, desde una Mac, para que la app abra con doble clic. Hoy la firma ad hoc que Godot genera desde Windows no la acepta macOS en Apple Silicon, y quien juega tiene que volver a firmarla con `codesign` (README). Alternativa sin cuenta: hacer la firma ad hoc en una Mac al empaquetar. Hallazgo del playtest de H2.7.
- Separador de miles en español según CLDR, que pide al menos dos dígitos de agrupación: "1234" y "12.345". .NET escribe "1.234".

## Interfaz

- Que `EntityRenderer` cambie los modelos cuando cambie la época. Llega con la segunda época.

## Diseño

- ¿Elementos de fantasía en la ambientación medieval? Por ahora no.
- Salto temporal opcional entre épocas. Se decide en IP9.
- ¿Retroceso de época tras un colapso de la colonia?
- Siglo XX y actualidad, después de IP10.
- Fluidos dinámicos, multijugador, modo aventura y mundos persistentes entre partidas.
