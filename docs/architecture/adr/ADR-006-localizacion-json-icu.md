# ADR-006: Localización con catálogos JSON e ICU

- **Estado:** aceptada
- **Fecha:** 2026-10-08

## Contexto

El juego debe estar en español por defecto y en inglés desde la IP1, con espacio para más idiomas. Gran parte del texto se genera desde la simulación y en español exige concordancia de género y número.

## Decisión

- Biblioteca `OurLife.Localization`, sin Godot, con catálogos JSON planos por idioma y dominio en `game/content/locales/`.
- Mensajes en formato ICU MessageFormat para plural, género y época. La librería se decidió en H1.3; ver la revisión al final.
- Idioma por defecto y de respaldo: `es`. No se usa el idioma del sistema operativo.
- Puente con Godot: cada catálogo se registra en `TranslationServer` para que los textos estáticos se traduzcan solos; los dinámicos se regeneran cuando cambia el idioma.
- La simulación emite `LocalizedText(clave, args)` y el registro de eventos guarda `LocalizedText`, así que el historial también cambia de idioma.

## Consecuencias

- Cambio de idioma en vivo y tests de localización sin Godot.
- Un tercer idioma se agrega sin tocar código.
- La convención de claves y los tests de paridad son obligatorios.
- ICU tiene una pequeña curva de aprendizaje para quien escribe los textos.

## Revisión en H1.3 (2026-10-08)

- **ICU propio en lugar de MessageFormat.NET.** `OurLife.Localization` implementa el subconjunto de ICU MessageFormat que usa el juego: argumentos simples, `number` con estilo `integer` opcional, `plural` con categorías CLDR y casos `=N`, `select`, `#` y comillas con apóstrofe. Motivo: los tests necesitan analizar cada mensaje para comparar sus argumentos entre idiomas, y así la biblioteca queda sin dependencias externas. Las reglas de plural de cada idioma están en `PluralRules`.
- **Textos dinámicos.** Las pantallas se suscriben a `Localizer.LocaleChanged` y vuelven a formatear sus textos. Los controles cuyo texto es una clave los traduce Godot solo.
- **Números.** Los enteros se escriben sin separador de miles, así los años se leen "1203". Para separadores se usa `{n, number}`, que respeta el formato de cada idioma.
- **Archivos.** Los JSON de contenido son JSON estricto, sin comentarios ni comas finales, porque el editor de Godot también los lee.
