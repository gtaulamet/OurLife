# Playtest de IP3.5 (`v0.3.5`)

Checklist para la prueba humana de L5. Cubre la definición de hecho de IP3.5 (`docs/ip/IP3.5.md`) y los criterios de aceptación de `docs/gdd/libre-albedrio.md`. Se juega con el ejecutable exportado, no con el editor. Lo nuevo: los colonos hacen cosas por su cuenta, buscan compañía, el bosque vuelve y el valle cambia de color con la estación. Lo más importante es dejar una aldea sin órdenes un rato largo y decir si se entiende lo que hace y si el paisaje se nota.

## Preparación

1. Descomprimir `builds/OurLife-v0.3.5-windows-x86_64.zip` en una carpeta nueva.
2. Atajos, desde PowerShell en la carpeta del juego:

| Atajo | Empieza en |
|---|---|
| `--play --seed 1` | La llegada, con el ritmo Rápido |
| `--play --seed 1 --season autumn` | Lo mismo, con el paisaje forzado en otoño (también `spring`, `summer`, `winter`) |

3. Metas por defecto: 3000 de comida y 40 de madera. Para concebir, la aldea necesita comida guardada para 30 días por boca (unas 360 bayas por colono); con una meta más baja nadie nace, y con una muy alta (6000) los colonos pueden vaciar los arbustos y la aldea extinguirse en una semilla.

## Checklist

Marcar cada punto con ✅ (bien), ❌ (falla, anotar el hallazgo abajo) o ➖ (no se probó).

### Opción y metas

- [✅] Opciones tiene "Libre albedrío", prendida en una instalación nueva; el cambio se conserva al reiniciar.
- [✅] El panel Colonia (P) muestra "Comida: N de [3000]" y "Madera: N de [40]"; el campo acepta dígitos, Enter, Tab o un clic afuera aplican y Esc cancela; escribir no mueve la cámara ni cambia la velocidad.
- [✅] Todo se ve igual de bien en inglés.

### Trabajo por iniciativa

- [✅] Sin dar órdenes, los colonos libres recolectan bayas y talan por su cuenta; el inspector dice "Recolectando por su cuenta" / "Talando un árbol por su cuenta".
- [✅] Con la meta de comida en 0, nadie recolecta por su cuenta. Con una prioridad en "nunca" (panel Colonia), ese colono no hace esa tarea.
- [✅] Una orden del jugador sobre un arbusto o un árbol la toma un colono antes que cualquier iniciativa.
- [✅] Nadie va a talar a más de 30 celdas de la llegada.
- [✅] Con la opción apagada, nadie trabaja por su cuenta.

### Vida social con intención

- [✅] Quien no tiene nada que hacer camina hacia la pareja, un familiar o un amigo y se queda cerca un rato; el inspector dice "Pasando un rato con alguien".
- [✅] Al deambular, un colono se aleja de su rival cercano.
- [✅] En un año sin órdenes se forman varias amistades, y el registro las avisa.

### Bosque y estaciones

- [✅] Después de talar un sector, junto a los árboles que quedan aparecen retoños (más chicos), que crecen en unos días; el bosque no pasa de su densidad original.
- [✅] En otoño los robles y los pinos se ven amarillos y rojizos con variación entre árboles; en invierno hay nieve en las copas y el pasto se blanquea de a poco; las cumbres de roca se ven nevadas; en primavera y verano todo vuelve al verde. La transición dura unos 3 días.
- [✅] En invierno los pinos y los robles se siguen distinguiendo entre sí.

### Partida larga

- [✅] Una aldea sin órdenes pasa varios años sin morir de hambre y nace al menos un hijo.
- [✅] Guardar y cargar conserva la opción, las metas, los retoños y la estación.

### Rendimiento y paquetes

- [✅] `.\OurLife.exe -- --benchmark 20 --seed 42 --ticks 5000` termina con "within the budget" en `%APPDATA%\OurLife\logs\godot.log`.

## Pasada automática (L5, 21 de octubre de 2026)

| Qué | Resultado | |
|---|---|---|
| `dotnet build` y `dotnet test` | Sin advertencias; 816 tests en verde (con los lentos) | ✅ |
| Smoke `--seed 1 --ticks 5000` | Código 0, huella `3E6090980A92B35A` | ✅ |
| Benchmark de 30 s | 304 FPS, 0,16 ms por tick (límite 2 ms) | ✅ |
| Diez años, libre albedrío y sin órdenes, semillas 1, 7 y 42 | Sin muertes por hambre; 0,04 ms por tick en la simulación; con la meta de 3000, un nacimiento por semilla | ✅ |

## Hallazgos

(Ninguno todavía.)
