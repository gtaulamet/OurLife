# ADR-010: Guardado en JSON comprimido y versionado

- **Estado:** aceptada
- **Fecha:** 2026-10-11
- **Relacionada con:** ADR-003, ADR-004, ADR-005, ADR-011

## Contexto

IP2 trae partidas guardadas. La simulación ya tiene todo su estado en datos planos: los componentes y los comandos se serializan a JSON para la huella (ADR-004), el mapa son tres arreglos densos (ADR-003) y los derivados se reconstruyen (ADR-011). Falta decidir el formato del archivo, cómo se nombran los tipos, cómo se ven las ranuras sin leer la partida entera y qué pasa con un archivo de otra versión.

## Decisión

- **Contenedor:** un ZIP (`System.IO.Compression`) con dos entradas:
  - `header.json`: versión del formato, nombre de la aldea, tick, época, cantidad de colonos y los datos que agrega la presentación (identificador de la partida, si es autoguardado, versión del juego y momento del guardado). Listar las ranuras lee sólo esta entrada.
  - `state.json`: el estado completo.
- **Estado en JSON** con `System.Text.Json`, igual que la huella. Los arreglos del mapa van en Base64 (los materiales en little-endian, como en la huella) y el ZIP los comprime.
- **Los materiales del mapa se guardan con la tabla de ids** de los defs. Al cargar, cada índice se traduce al del contenido actual: agregar o reordenar materiales no rompe las partidas.
- **Tipos con nombre estable:** cada componente y cada comando tiene un nombre de guardado fijo (`colonist`, `designate`…) en un registro de Core (`SaveTypes`), en lugar del nombre de la clase. Un test falla si un componente o un comando no está registrado. Renombrar una clase no rompe las partidas.
- **Versión del formato:** un entero (`SaveGame.FormatVersion`), que sube con cada cambio incompatible del estado. Una versión distinta se rechaza con un motivo propio hasta que haya migraciones (ver Consecuencias).
- **Validación al cargar:** cada def que nombra el estado (materiales, plantas, objetos, criaturas, necesidades, época y conocimientos) debe existir en el contenido actual. Si falta, la carga falla y nombra el id.
- **Errores tipados:** la carga lanza `SaveLoadException` con un motivo (`Corrupt`, `Newer`, `Older`, `MissingContent`) y un detalle. La presentación elige el texto localizado por el motivo.
- **Core no toca archivos:** `SaveGame` escribe y lee un `Stream`. Las ranuras, las rutas y la escritura atómica son de la presentación.
- **Los sistemas no son estado:** cargar devuelve una simulación sin sistemas. Quien la carga agrega los mismos sistemas que al empezar (`ColonySystems.AddTo`). Por eso un sistema no puede guardar entre ticks nada que cambie sus decisiones, salvo cachés que se reconstruyen igual (ADR-011).

## Alternativas descartadas

- **Formato binario propio:** más chico y rápido, pero cada componente nuevo necesitaría su código de lectura y escritura. Con JSON, un componente de datos planos se guarda sin código nuevo.
- **Un archivo de metadatos aparte de la partida:** dos archivos que se pueden desincronizar. El ZIP da lo mismo en un solo archivo.
- **Nombres de clase en el archivo:** renombrar o mover una clase rompería las partidas.

## Consecuencias

- El test de continuidad ("guardar a mitad y seguir = partida continua") cubre cada componente, comando y sistema que se agregue: si un sistema guarda estado propio, el test lo detecta.
- Hasta `v0.2.0` el formato cambia en cada hito de IP2 y las partidas de desarrollo se rechazan con "versión vieja". Desde `v0.2.0`, un cambio de formato trae su migración de la versión anterior (plan maestro, riesgos).
- Los eventos pendientes no se guardan: la presentación los lee en el mismo cuadro en que ocurren.
