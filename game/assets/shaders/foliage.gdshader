// Leaves of trees and bushes, which change with the season (docs/gdd/estaciones-visibles.md).
// Each tree takes a tone between tint_a and tint_b from its position, so a grove is not one
// colour, and snow whitens the faces that look up. Colours are sRGB values, as in the catalog.
shader_type spatial;
render_mode specular_disabled;

// The colour of the leaves in the model, darkened for the depth below the current level.
uniform vec4 base_color : source_color = vec4(0.3, 0.48, 0.17, 1.0);
uniform float brightness = 1.0;

uniform vec4 tint_a : source_color = vec4(0.3, 0.48, 0.17, 1.0);
uniform vec4 tint_b : source_color = vec4(0.3, 0.48, 0.17, 1.0);
uniform float tint_amount = 0.0;
uniform vec4 snow_color : source_color = vec4(0.95, 0.96, 0.98, 1.0);
uniform float snow_amount = 0.0;

varying flat float tree_tone;
varying vec3 world_normal;

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

void vertex() {
	tree_tone = hash12(floor(MODEL_MATRIX[3].xz * 2.0));
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 tone = mix(tint_a.rgb, tint_b.rgb, tree_tone);
	vec3 color = mix(base_color.rgb, tone, tint_amount);
	// Snow rests on the top of the crown and thins out down its sides.
	float top = smoothstep(0.1, 0.65, normalize(world_normal).y);
	color = mix(color, snow_color.rgb, clamp(snow_amount * (0.25 + 0.75 * top), 0.0, 1.0));
	ALBEDO = color * brightness;
	ROUGHNESS = 1.0;
}
