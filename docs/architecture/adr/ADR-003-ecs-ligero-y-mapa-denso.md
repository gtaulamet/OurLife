# ADR-003: ECS ligero propio y mapa en arrays densos

- **Estado:** aceptada
- **Fecha:** 2026-10-08

## Contexto

La colonia tendrá de cientos a miles de entidades: colonos, animales, plantas y objetos. El mapa tendrá entre 300 mil y 2,4 millones de tiles. Todo debe guardarse y cargarse sin perder el determinismo.

## Decisión

- ECS mínimo propio:
  - `EntityId` con índice y generación, para referencias seguras.
  - Componentes como clases POCO serializables.
  - `ComponentStore<T>` tipo sparse set, iterado por `EntityId` ascendente.
  - Sistemas con intervalo de ejecución y desfase por entidad.
- Mapa en arrays densos por propiedad (material, forma, banderas), indexados `x + y·W + z·W·H`, con chunks de 16×16 por nivel.
- Muros y suelos son tiles; árboles, plantas, objetos y criaturas son entidades.

## Consecuencias

- Control total del orden de iteración y del guardado, sin APIs de terceros que los agentes puedan confundir.
- Hay que escribir y mantener el ECS, aunque es pequeño.
- Si la escala crece mucho más de lo previsto, se evaluará una librería ECS con un ADR nuevo.
