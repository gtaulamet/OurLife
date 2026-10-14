# Vista previa de muebles (V3)

Spec de V3 de IP2.5. Hasta IP2, al colocar un mueble sólo se veía el cubo de selección y la pista "G gira (1 de 4)": no se sabía hacia dónde iba a mirar. Los planos de muebles se dibujaban como una caja baja, sin orientación.

## Al colocar

- Con la herramienta Construir y un mueble elegido, la celda bajo el cursor muestra, dentro del cubo de selección, el **modelo del mueble translúcido**, con el giro actual. Al pulsar **G** gira en el acto.
- Una **flecha** plana en el suelo, en el borde de la celda, marca el frente del mueble: hacia donde mira la silla, el lado por donde se abre el baúl, los pies de la cama. Sirve sobre todo para la mesa, que se ve igual en dos giros.
- Si el mueble **no se puede colocar** en esa celda (sin suelo firme, con otra cosa encima, en un almacén, con una orden o una planta), el modelo y la flecha se ven rojos.
- **Durante un arrastre**, la vista previa aparece en cada celda de la caja: el modelo donde se colocaría un plano, y rojo donde no. Las celdas válidas son exactamente las que recibe el plano al soltar. Con más de 64 celdas en la caja, se muestran sólo las primeras 64 válidas, sin las rojas.
- Fuera de la herramienta Construir, o con un construible que no es mueble (muro, suelo, puerta, rampa, escalera), no hay vista previa: sólo el cubo, como hasta ahora.
- La vista previa sigue el nivel de la vista, como el cubo.

## Planos colocados

- Un plano de mueble se dibuja con el **modelo del mueble translúcido y girado**, no con una caja. Con el material completo se ve más opaco, como los otros planos.
- Los demás planos (muro, suelo, puerta, rampa, escalera) siguen siendo cajas.

## Reglas en Core

- `Construction.CellsToPlan(simulación, construible, desde, hasta)` devuelve, sin cambiar nada, las celdas donde `PlaceBlueprintCommand` crearía un plano, en el mismo orden. El comando la usa, así que la vista previa y el comando no pueden diferir.
- No hay comandos, componentes ni eventos nuevos. Las huellas no cambian.

## Textos

No hay textos nuevos: la pista del giro (`ui.orders.rotate_hint`) ya existe.

## Criterios de aceptación

1. Captura con la vista previa de una silla y de una cama en los cuatro giros: la flecha apunta al frente del modelo en cada giro.
2. Captura de una celda donde no se puede colocar: modelo y flecha rojos.
3. Captura de un arrastre sobre una zona mixta: modelos en las celdas válidas y rojos en las otras.
4. Captura de planos de cama y de silla colocados con giros distintos: se ve su orientación.
5. Test de Core: `CellsToPlan` da las mismas celdas que crea `PlaceBlueprintCommand`, en una caja con celdas válidas y no válidas, y no cambia la huella.
6. El smoke y los tests en verde, con las mismas huellas.
