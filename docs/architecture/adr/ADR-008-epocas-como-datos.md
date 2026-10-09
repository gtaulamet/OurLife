# ADR-008: Épocas como datos

- **Estado:** aceptada; el modelo de progreso se confirma en IP9
- **Fecha:** 2026-10-08

## Contexto

La colonia empieza en la Edad Media y, después de la versión 1.0, debe poder avanzar a la Edad Moderna y a la Contemporánea. Si el código asume la Edad Media, agregar épocas obligaría a reescribir sistemas.

## Decisión

- Las épocas son defs (`EraDef`). En IP1 sólo existe `medieval`.
- Todo def de contenido puede declarar `era` (época mínima) y `requires` (conocimientos). `UnlockService` decide qué está disponible.
- El calendario usa años absolutos, desde 1200 por defecto, y está pensado para recorrer siglos.
- Los visuales (modelos 3D desde ADR-009) admiten variantes `@<era>`; el tema de UI y la música se eligen por época.
- Los textos que cambian con la época usan `select` sobre `era`.
- Modelo de progreso recomendado: tecnológico y continuo, al estilo de Dawn of Man. Detalle en `docs/gdd/eras.md`.

## Consecuencias

- La versión 1.0 medieval no se reescribe al sumar épocas.
- Pequeño costo desde IP1, de unas 4 horas, más el árbol de conocimientos de IP3.
- Un test verificará que un def de otra época queda bloqueado en la Edad Media (H1.3).
- El arte de épocas modernas no está cubierto por los packs medievales gratuitos.
