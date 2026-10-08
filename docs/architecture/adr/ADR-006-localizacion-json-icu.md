# ADR-006: Localización con catálogos JSON e ICU

- **Estado:** aceptada
- **Fecha:** 2026-10-08

## Contexto

El juego debe estar en español por defecto y en inglés desde la IP1, con espacio para más idiomas. Gran parte del texto se genera desde la simulación y en español exige concordancia de género y número.

## Decisión

- Biblioteca `OurLife.Localization`, sin Godot, con catálogos JSON planos por idioma y dominio en `game/content/locales/`.
- Mensajes en formato ICU MessageFormat para plural, género y época, con la librería MessageFormat.NET. Se valida en H1.3.
- Idioma por defecto y de respaldo: `es`. No se usa el idioma del sistema operativo.
- Puente con Godot: cada catálogo se registra en `TranslationServer` para que los textos estáticos se traduzcan solos; los dinámicos se regeneran al recibir `NotificationTranslationChanged`.
- La simulación emite `LocalizedText(clave, args)` y el registro de eventos guarda `LocalizedText`, así que el historial también cambia de idioma.

## Consecuencias

- Cambio de idioma en vivo y tests de localización sin Godot.
- Un tercer idioma se agrega sin tocar código.
- La convención de claves y los tests de paridad son obligatorios.
- ICU tiene una pequeña curva de aprendizaje para quien escribe los textos.
