// Tufts, flowers and pebbles that dress the grass: their colours are in the vertices, as #RRGGBB
// values. They take the tint of the season from the terrain (docs/gdd/estaciones-visibles.md):
// the grass colours of the season over those of the catalog, and some snow.
shader_type spatial;
render_mode specular_disabled, cull_disabled;

uniform float brightness = 1.0;
// What the season's grass colour is against the catalog's, per channel, and the snow on top.
uniform vec3 season_ratio = vec3(1.0);
uniform vec3 snow_color = vec3(0.95, 0.96, 0.98);
uniform float snow_amount = 0.0;

vec3 to_output(vec3 color) {
	if (!OUTPUT_IS_SRGB) {
		color = mix(pow((color + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), color * (1.0 / 12.92), lessThan(color, vec3(0.04045)));
	}
	return color;
}

void fragment() {
	vec3 color = clamp(COLOR.rgb * season_ratio, 0.0, 1.0);
	color = mix(color, snow_color, snow_amount);
	ALBEDO = to_output(color * brightness);
	ROUGHNESS = 1.0;
}
