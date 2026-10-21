# Agricultura, cocina y deterioro (IP4)

Parte de la spec de IP4 (`docs/gdd/oficios.md`): zonas de cultivo, siembra, crecimiento y cosecha; comidas cocinadas más nutritivas; deterioro simple de los alimentos; y la silvicultura (sembrar árboles) que `libre-albedrio.md` dejó para IP4. Resuelve el límite que `vida-y-generaciones.md` anotó: sin agricultura el valle sostiene unas 7 personas.

**Estado:** propuesta de O1, pendiente de aprobación. Números iniciales, a ajustar en O11.

## Qué hay hoy

- Comida: arbustos de bayas (10 bayas, vuelven a los 3 días, duermen en invierno), unas 100 en el valle; bayas de 400 de nutrición y provisiones de 2500. La necesidad de hambre baja 200 por hora (un adulto necesita 4800 por día, unas 12 bayas).
- Nada se pudre. La población la limitan la reserva de comida (30 días por boca) y las plantas de comida del mapa (7 por boca) para concebir (`vida-y-generaciones.md`, "Balance de población y comida").
- Un año del juego son 60 días, 15 por estación (primavera, verano, otoño e invierno). `dormantSeasons` ya existe para los arbustos.

## Zonas de cultivo

- Una **zona de cultivo** es un conjunto de celdas con un cultivo asignado. Se dibuja con la herramienta **Cultivo (J)**: se arrastra la caja y se elige el cultivo; las celdas nuevas de una caja forman una zona nueva, como el almacén (`almacenes.md`). **Cancelar (X)** también quita celdas de cultivo; una zona sin celdas desaparece.
- **Celdas válidas:** suelo natural de tierra (material `soil`), plano (no rampa), con aire arriba y sin planta, plano, orden, mueble, almacén ni otra zona. Una celda con una planta o un árbol se saltea. Lo construido no es tierra: no se cultiva sobre suelos construidos.
- **Cambiar el cultivo** de una zona vale para lo que se siembre desde ese momento; lo ya sembrado sigue y se cosecha.
- Las zonas son **órdenes permanentes del jugador**: se cumplen con la opción de libre albedrío apagada o prendida, y respetan las prioridades de Agricultura de cada colono.
- La naturaleza no pisa las zonas: ningún retoño natural aparece en una celda de cultivo, y un almacén nuevo las saltea.

## Cultivos

Def nuevo `Crop`. Los datos de IP4:

| Cultivo | Producto | Rinde | Crece | Estaciones de crecimiento | Siembra | Cosecha | Requiere |
|---|---|---|---|---|---|---|---|
| `wheat` trigo | 8 trigos | 8 | 10 días | primavera, verano, otoño | 40 ticks | 60 | `cereals` |
| `peas` guisantes | 6 guisantes | 6 | 8 días | primavera, verano, otoño | 40 | 50 | `agriculture` |
| `cabbage` repollo | 5 repollos | 5 | 8 días | primavera, verano, otoño | 40 | 50 | `agriculture` |
| `oak_grove` bosquecillo de robles | un retoño de roble | | (el del retoño) | | 60 | | `silviculture` |
| `pine_grove` bosquecillo de pinos | un retoño de pino | | (el del retoño) | | 60 | | `silviculture` |

```json
{ "type": "Crop", "id": "wheat", "era": "medieval", "requires": ["cereals"],
  "item": "wheat", "count": 8, "growDays": 10, "growSeasons": ["spring", "summer", "autumn"],
  "sowTicks": 40, "reapTicks": 60, "visual": "crop.wheat" }
```

```json
{ "type": "Crop", "id": "oak_grove", "era": "medieval", "requires": ["silviculture"],
  "tree": "oak", "sowTicks": 60, "spacing": 2 }
```

- **Crecimiento:** una celda sembrada tiene una entidad `Crop` (no bloquea el paso). A medianoche suma un día si la estación es de crecimiento (`growSeasons`); fuera de ellas se detiene, sin morir (las heladas llegan con el clima de IP6). Con `growDays` días acumulados, está **madura**. Tiene tres etapas visibles (brote, crecimiento, madura).
- **Sembrar** (trabajo `sow`, tipo `farming`): un colono con prioridad en Agricultura toma una celda vacía de una zona, alcanzable y que nadie reserva, y trabaja `sowTicks` desde la celda o desde una vecina. Sembrar es gratis: no hay semillas (decisión 8). No siembra si **no queda tiempo para madurar**: los días que faltan hasta que termine la última estación de crecimiento seguida deben ser al menos `growDays`. Así nadie siembra trigo el último día del otoño.
- **Cosechar** (trabajo `reap`, tipo `farming`): un cultivo maduro con una zona. Deja el producto en la celda, sin mover el cultivo, y la celda queda vacía y se vuelve a sembrar.
- **Rendimiento:** `rinde × (70 + 3 × nivel de Agricultura) / 100`, con el resto fraccionario sorteado en el flujo `farm.yield` (nivel 0: 70 %; nivel 10: 100 %; nivel 20: 130 %). La azada de hierro acelera el trabajo, no el rendimiento.
- **Silvicultura:** el cultivo `tree` siembra un retoño (`Sapling`, de L4b) en las celdas de la zona con `x` e `y` pares (`spacing` 2) para dejar lugar a los árboles; la celda no se vuelve a sembrar mientras tenga retoño o árbol. El retoño crece a árbol con las reglas de `libre-albedrio.md` (10 días). Los árboles plantados cuentan para el tope natural de cobertura, pero plantar no lo respeta: es una decisión del jugador. El libre albedrío puede talarlos como a cualquier árbol de la aldea.
- **Cuánto da una celda:** una celda de trigo da 8 trigos por ciclo, que molidos y horneados son 12 panes (9600 de nutrición, dos días de un adulto) en 10 días. Con tres estaciones de crecimiento (45 días) caben 4 ciclos por año: unos 7 a 8 celdas por colono para toda su comida (el arbusto de bayas rinde más por celda pero está limitado a las que da el valle y duerme en invierno). Una aldea de 12 colonos pide un campo de unas 10 × 10.

## Comidas

La nutrición del pan, el potaje y el encurtido está en `oficios.md`. Las recetas son de la cocina (ver "Recetas" ahí).

- **Cocinado = más nutritivo:** el potaje convierte 850 de nutrición cruda (2 repollos y 1 guisante) en 1600; el trigo, que crudo no se come, se vuelve pan: 2 trigos molidos y horneados dan 3 panes (2400 de nutrición).
- **Pensamiento por plato:** comer un objeto con `meal` deja "Comí un plato preparado" (+3, 8 horas, no se suma). Es un dato: `MoodConfig.mealThought` (el código no nombra pensamientos). No hay castigo por comer crudo.
- **Qué comer:** el colono con hambre baja (bajo el umbral bajo, no crítica) elige, entre las pilas de comida alcanzables a `eatChoiceRadius` (15) celdas o menos de la más cercana, primero un plato preparado (`meal`), después la de menor frescura, después la más cercana. Con hambre crítica come de la más cercana. Así la comida que se echa a perder se come primero, y las provisiones y las bayas quedan de reserva. Los filtros del almacén (por categoría) no distinguen comida cruda y cocinada: filtrar por objeto queda en el backlog.
- **Comida y concepción:** las plantas de comida del mapa (`foodPlantsPerColonist`, 7) cuentan también las celdas de cultivo con un cultivo de comida (`cropTilePlantEquivalent`, 1 por celda, ya sembrada o no), y la reserva de 30 días (`foodReserveDays`) suma la nutrición de las pilas y el `reserveValue` del trigo y la harina (800 cada uno). Con un campo, la población ya no está topada por los arbustos.
- Un plato no se come sentado salvo que haya comedor (`habitaciones.md`).

## Deterioro

Def nuevo `SpoilageConfig` y el campo `spoilDays` de los objetos (`oficios.md`).

- **Frescura:** cada pila tiene `Freshness` de 0 a 10 000. A medianoche baja `10 000 × velocidad / (spoilDays × 100)`, en enteros, con `velocidad` en tanto por ciento. Un objeto con `spoilDays` 0 no se mide.
- **Velocidad:** 100 % en general; `coveredPercent` (50) si la celda tiene algo sólido arriba (un techo, un suelo construido o roca): una bodega cavada o una casa con techo conserva el doble. Las pilas en las manos de un colono bajan al 100 %. Es el único efecto del lugar en IP4 (la temperatura llega con IP6).
- **Se pudre:** con la frescura en 0 la pila entera desaparece. El Core emite un aviso por objeto y por día si se perdieron `rotNoticeMinUnits` (5) unidades o más ("Se pudrieron 20 bayas"), para no llenar el registro.
- **Al juntar pilas** la frescura es el promedio ponderado por cantidad, redondeado hacia abajo. Un sobrante pequeño que se pudre no arrastra a una pila grande hasta que se junten.
- **Valores de partida:** bayas 60 días (cubiertas, 120); repollo 30; guisantes 90; trigo 200; harina 90; pan 20; potaje 6; encurtido 150; provisiones no se pudren. Los platos duran poco y se hornean a pedido con una orden "Mantener N": el trigo y la harina son la reserva del invierno.
- **Motivo:** hoy 3000 bayas guardadas valen para siempre, y eso quita el motivo de cocinar y de cavar una bodega. El plazo de 60 días cubre lo recolectado en otoño hasta el final del invierno (30 días) con el doble de margen; a la intemperie, las bayas de verano se pierden. Es la decisión 2 del usuario y el riesgo principal de balance de IP4 (ver `IP4.md`).

```json
{ "type": "SpoilageConfig", "id": "default", "coveredPercent": 50, "rotNoticeMinUnits": 5 }
```

## Contrato

| Tipo | Nombre | Uso |
|---|---|---|
| Comando | `PlaceFarmZoneCommand(desde, hasta, cultivo)` | Crea una zona con las celdas válidas de la caja |
| Comando | `SetFarmZoneCropCommand(zona, cultivo)` | Cambia el cultivo de la zona |
| Comando | `DesignateCommand` con `Cancel` | Además, quita celdas de cultivo |
| Estado | `FarmZone` (celdas y cultivo), `Crop` (cultivo, días de crecimiento), `ItemStack.Freshness` | Guardado y huella |
| Trabajos | `Sow`, `Reap` | Tipo `farming` |
| Evento | `FarmZonesChangedEvent` | Avisa a la vista |
| Evento | `EntitySpawnedEvent`, `EntityChangedEvent`, `EntityRemovedEvent` | Un cultivo aparece, crece o se cosecha |
| Evento | `NotificationEvent` | Aviso de comida podrida |
| Consulta | `FarmRules.Zones`, `FarmRules.State(celda)` | Inspector |
| Flujos | `farm.yield` | Rendimiento fraccionario |

- `farming` suma las cuatro tareas de las zonas a los tipos de trabajo de `oficios.md`: sembrar y cosechar entran en el mismo tipo con la misma prioridad, y la cosecha va antes de sembrar.
- Libre albedrío: ver `oficios.md`. Las zonas no necesitan la opción. `gather_food` sigue recolectando sólo arbustos.

## Textos nuevos (propuesta)

| Clave | es | en |
|---|---|---|
| `ui.orders.farm` | Cultivo (J) | Farm zone (J) |
| `ui.orders.name.farm` | Cultivo | Farm zone |
| `ui.farm.choose_crop` | Elegir cultivo | Choose crop |
| `def.crop.{wheat,peas,cabbage,oak_grove,pine_grove}.name` | trigo, guisantes, repollo, bosquecillo de robles, bosquecillo de pinos | wheat, peas, cabbage, oak grove, pine grove |
| `ui.inspector.crop` | Cultivo de {crop}: {percent} | {crop} crop: {percent} |
| `ui.inspector.crop_ripe` | Cultivo de {crop}: listo para cosechar | {crop} crop: ready to harvest |
| `ui.inspector.farm_zone` | Zona de cultivo de {crop} | {crop} farm zone |
| `ui.inspector.freshness` | Frescura: {percent} | Freshness: {percent} |
| `job.sow.report` | Sembrando {crop} | Sowing {crop} |
| `job.reap.report` | Cosechando {crop} | Harvesting {crop} |
| `event.food.spoiled` | Se pudrieron {count, plural, one {# unidad} other {# unidades}} de {item}. | {count, plural, one {# unit} other {# units}} of {item} spoiled. |
| `def.thought.ate_meal.name` | Comí un plato preparado | I ate a prepared meal |

## Modelos 3D necesarios

| Visual | Qué es | Origen probable |
|---|---|---|
| `crop.wheat`, `crop.peas`, `crop.cabbage` | Cada uno con tres etapas (brote, crecimiento, madura), chicos y bajos | Revisar si el Nature Kit trae `crops_*`; si no, Blender |
| Marca de zona de cultivo | Tinte de suelo propio, distinto del almacén y de las órdenes | Shader del terreno (como las zonas) |
| `item.wheat`, `item.flour`, `item.cabbage`, `item.peas`, `item.bread`, `item.pottage`, `item.pickled_cabbage` | En las manos y en el suelo | Ver `oficios.md` |
| Retoño plantado | El de L4b (`plant.*.sapling`) | Ya existe |

## Criterios de aceptación

1. **Zona.** Una caja de 5 × 5 de tierra plana con un arbusto de bayas y un roble ⇒ la zona toma las 23 celdas libres; si además una celda es de un almacén, toma 22.
2. **Sembrar y cosechar.** Una zona de trigo en primavera con un colono con prioridad de Agricultura ⇒ se siembra, madura a los 10 días y se cosechan `8 × (70 + 3 × nivel) / 100` trigos por celda; la celda se vuelve a sembrar.
3. **Sin tiempo para madurar.** En el día 40 del año, con el otoño terminando el día 44, nadie siembra trigo (quedan 5 días de crecimiento, y hacen falta 10); sí siembra repollo en el día 35 (quedan 10 y hacen falta 8).
4. **Invierno.** Un cultivo sembrado en otoño tardío no crece en invierno ni muere, y vuelve a crecer en primavera.
5. **Prioridades y opción.** Con Agricultura en "nunca", un colono no siembra; la zona funciona con la opción de libre albedrío apagada.
6. **Cocina.** Escenario: trigo, una cocina y órdenes de harina y pan ⇒ salen panes con la nutrición de la tabla; un colono con hambre baja come primero el plato preparado más fresco y, si no hay, la pila más cercana.
7. **Deterioro.** Una pila de bayas sin techo llega a 0 de frescura a los 60 días y desaparece con aviso; bajo un techo o en una celda con roca arriba, a los 120. Con `spoilDays` 0 nada cambia.
8. **Juntar pilas.** Una pila de 40 con frescura 5000 y otra de 40 con 10 000 dan 80 con 7500.
9. **Concepción.** Con un campo de trigo sembrado, la reserva (trigo, harina y pan) y las celdas de cultivo cuentan para `foodReserveDays` y `foodPlantsPerColonist`: una aldea sin arbustos suficientes pero con un campo puede concebir.
10. **Silvicultura.** Una zona de robles en una caja de 6 × 6 siembra retoños en las celdas pares, no vuelve a sembrar donde hay uno, y los retoños crecen en 10 días.
11. **La naturaleza no pisa.** Ningún retoño natural aparece en una celda de cultivo.
12. **Balance.** `BalanceTests` de diez años en Rápido y semillas 1, 7 y 42, con un jugador que dibuja un campo de 8 × 8 de trigo en el segundo año, una cocina y una orden "Mantener 20 panes": nadie muere de hambre, nacen al menos tantos hijos como sin campo y la población llega a más de 8.
13. **Guardado.** El test de continuidad incluye una zona con cultivos a medio crecer y pilas con frescura; una partida de `v0.3.5` carga con las bayas frescas.
14. **En la partida.** Dibujar un campo, ver sembrar y cosechar, hornear pan y ver una nota de comida podrida, en es y en, con capturas.
15. **Presupuesto.** Menos de 0,1 ms por tick de media con 300 celdas de cultivo y 200 pilas con frescura.

## Fuera de alcance

Van a `docs/ip/backlog.md`: más cultivos (cebada, avena, centeno, lino), rotación de tres campos con barbecho y fertilidad (el sistema medieval real, ver fuentes), semillas como objeto, riego (IP6), heladas y clima (IP6), malezas y plagas, cría de animales (IP5), cerveza, queso y carne ahumada, barriles y bodega con temperatura (IP6), filtros del almacén por objeto, calidad de la comida, variedad de la dieta como pensamiento, y que el libre albedrío siembre o coseche por metas.

## Fuentes

- Rotación de tres campos: cereal de otoño (trigo, cebada o centeno), cereal o legumbre de primavera (avena, cebada, guisantes) y un tercio en barbecho; las legumbres fijan nitrógeno. [Britannica, Three-field system](https://www.britannica.com/technology/three-field-system). Las fuentes consultadas no dan rendimientos por hectárea, así que los rendimientos, tiempos y nutrición del juego son de jugabilidad.
- Plausibilidad: trigo y guisantes son cultivos de la Europa medieval; el repollo y su conservación en encurtido o fermentado también.
