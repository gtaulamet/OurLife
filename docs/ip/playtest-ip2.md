# Playtest de IP2 (`v0.2.0`)

Checklist para la prueba humana de H2.7. Cubre la definición de hecho de IP2 (`docs/ip/IP2.md`) y los criterios de aceptación de las specs de H2.1 a H2.6. Se juega con el ejecutable exportado, no con el editor. El usuario ya probó cada hito en el editor; esta pasada mira el conjunto, sobre todo una partida larga.

## Preparación

1. Armar el paquete con `./tools/package-windows.ps1` y descomprimir `builds/OurLife-v0.2.0-windows-x86_64.zip` en una carpeta nueva.
2. Perfil limpio: borrar la carpeta `%APPDATA%\OurLife` si existe. Ahí quedan las preferencias (`settings.cfg`), las partidas (`saves`) y el registro (`logs\godot.log`).
3. Los atajos de esta guía se lanzan desde PowerShell en la carpeta del juego, por ejemplo `.\OurLife.exe -- --play --seed 42 --ticks 43200`. Un día son 1440 ticks; el día empieza a las 06:00.

| Atajo | Empieza en |
|---|---|
| `--ticks 900` | El primer día, a las 21:00: la aldea se va a dormir |
| `--ticks 43200` | El primer día del otoño, a las 06:00 |
| `--ticks 64800` | El primer día del invierno, a las 06:00 |

A 4×, un día de juego dura unos 36 segundos y una estación unos 9 minutos.

## Checklist

Marcar cada punto con ✅ (bien), ❌ (falla, anotar el hallazgo abajo) o ➖ (no se probó).

### Guardado y carga (H2.1)

- [ ] Guardar desde la pausa con un nombre; "Partida guardada" al volver. Salir al menú: "Cargar partida" está habilitado y la lista muestra la ranura con su fecha.
- [ ] Cargar la ranura: la aldea, el reloj, las órdenes, los almacenes, las construcciones, los muebles y las prioridades siguen como estaban.
- [ ] Jugar hasta pasar la medianoche: aparece el autoguardado en la lista.
- [ ] Una ranura borrada desaparece de la lista; la confirmación de Sí/No se cierra con Esc.

### Día, noche y sueño (H2.2)

- [ ] El sol cruza el cielo; a las 21 la luz baja y la aldea se acuesta; a las 6 se levanta.
- [ ] Opciones > Ciclo de día y noche: No deja la luz del mediodía; la elección persiste al reiniciar.
- [ ] El registro muestra la fecha antes de los mensajes de cada día.

### Almacenes (H2.3)

- [ ] Almacén (Z): arrastrar una zona; los colonos llevan ahí troncos y piedras sueltas, una pila por celda.
- [ ] En el inspector de una celda del almacén, quitar una categoría: lo que no acepta deja de llegar, y lo que estaba se lleva a otro almacén si lo hay.
- [ ] Un colono que acarrea muestra la carga en las manos y el inspector dice qué lleva.

### Construcción (H2.4)

- [ ] Construir (C): una casa de muros de madera arrastrando la caja (sólo el borde), con una puerta; los colonos traen los troncos, construyen y entran por la puerta.
- [ ] Muros de mampostería con piedras del almacén.
- [ ] Suelo sobre un hueco, rampa junto a un muro y escalera hacia arriba: se construyen y se pueden recorrer.
- [ ] Desmontar (V) un muro devuelve un tronco; Canalizar (H) abre un hueco con rampa abajo.
- [ ] Cancelar un plano con material entregado deja el material en el suelo.

### Muebles y camas (H2.5)

- [ ] Cama, mesa, silla y cofre se colocan dentro de la casa; G los gira antes de colocarlos.
- [ ] De noche, un colono duerme en la cama, acostado y a lo largo de ella; el inspector de la cama dice de quién es, y el del colono, "Duerme en su cama".
- [ ] El cofre recibe objetos como un almacén y sus filtros se cambian desde el inspector.

### Gestión (H2.6)

- [ ] Colonia (P): cambiar la prioridad de un colono (por ejemplo, Acarreo en 1 y Tala en No) cambia lo que hace.
- [ ] Un colono sin nada que hacer aparece como alerta amarilla y en el registro; con hambre crítica, en rojo. Un clic en la alerta lo selecciona.

### Partida larga: definición de hecho de IP2

- [ ] Sin tocar código, construir una casa con muros, puerta, camas y un almacén.
- [ ] Con `--ticks 43200`, recolectar en otoño los arbustos alrededor de la aldea y llevar la partida a 4× hasta la primavera: la colonia pasa el invierno.
- [ ] Guardar a mitad del invierno, salir, cargar y seguir: todo continúa igual.

### Textos

- [ ] En español y en inglés, ningún texto cortado, superpuesto ni como clave cruda; el cambio de idioma traduce también el registro y el panel Colonia.

### macOS (en una Mac con Apple Silicon)

- [ ] Descomprimir `OurLife-v0.2.0-macos-universal.zip`; la primera vez se abre con clic derecho > Abrir (o `xattr -cr OurLife.app`).
- [ ] Smoke en la terminal: `./OurLife.app/Contents/MacOS/OurLife --headless -- --smoke --seed 42 --ticks 5000` termina con `[smoke] OK` y la misma huella que en Windows.
- [ ] El menú y una partida se ven bien en es y en; guardar, salir, volver a abrir y cargar.

### Rendimiento

- [ ] `.\OurLife.exe -- --benchmark 20 --seed 42 --ticks 5000` termina con "within the budget" en `%APPDATA%\OurLife\logs\godot.log`.
- [ ] Jugando a 4× con la aldea construida no hay tirones visibles.

## Pasada automática

Lo que no necesita manos, verificado por Claude Code antes de la prueba humana.

| Punto | Cómo se verificó | Resultado |
|---|---|---|
| Continuidad del guardado con construcciones, almacenes, camas y prioridades | `SaveGameTests` en Core y con el contenido real, semillas 1, 7 y 42; el smoke guarda y carga a mitad | ✅ |
| Partida larga | Smoke de 30 días en las semillas 1, 42 y 7: los 5 colonos vivos en el otoño, casa construida (entera en la 42; en la 1 y la 7 se acaban los troncos del radio que tala el smoke) | ✅ |
| Invierno | Tests del contenido real: sin recolectar la colonia muere en su primer invierno; recolectando en otoño, lo pasa. El smoke de un año y medio, que sólo recolecta al empezar, muere en el invierno, como en IP1 | ✅ |
| Paquete de Windows | `./tools/package-windows.ps1`: exporta, corre el smoke sobre el ejecutable exportado y arma `OurLife-v0.2.0-windows-x86_64.zip` | ✅ |
| Paquete de macOS | `./tools/package-macos.ps1` desde Windows: app universal con firma ad hoc; el binario conserva el permiso de ejecución en el zip. No se puede ejecutar acá | ✅ (falta probarlo en una Mac) |
| Rendimiento con la aldea construida | `--benchmark 20 --seed 42 --ticks 5000` en el editor: 406 FPS de media, 360 en el 1 % más lento, 0,027 ms por tick (RTX 4090 de portátil) | ✅ |

## Hallazgos

| # | Qué pasó | Pasos para reproducirlo | Gravedad (bloquea `v0.2.0` o no) |
|---|---|---|---|

## Resultado

- Fecha y PC:
- Quién jugó:
- ¿Se puede publicar `v0.2.0`?
