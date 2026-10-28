// Terrain of the map, drawn by MapRenderer with a painted look (ADR-012).
// Per instance: COLOR is the colour of the material (#RRGGBB values), INSTANCE_CUSTOM.x its
// pattern (TerrainPattern) and INSTANCE_CUSTOM.y the grass flags. Patterns are made here from the
// position in the world, so neighbouring cells join without seams and no textures are needed.
shader_type spatial;
render_mode specular_disabled;

// Darkening of the levels below the current one (LevelShading).
uniform float brightness = 1.0;
// Off on the level the view cuts: the grass above it is hidden, so its fringe would sit on rock.
uniform bool fringe = true;

// The greens of the catalog. Colours are #RRGGBB values, like COLOR: they are converted at the
// end, as StandardMaterial3D does with VertexColorIsSrgb.
uniform vec3 grass_lush = vec3(0.302, 0.459, 0.180);
uniform vec3 grass_dry = vec3(0.447, 0.502, 0.220);
uniform vec3 grass_deep = vec3(0.208, 0.357, 0.149);

// Snow on the rock of the heights (docs/gdd/estaciones-visibles.md): the faces that look up of
// granite and sedimentary rock, from snow_line up in the world's height, whiter the higher.
uniform vec3 snow_color = vec3(0.95, 0.96, 0.98);
uniform float snow_line = 1000.0;
uniform float snow_amount = 0.0;

const int PATTERN_PLAIN = 0;
const int PATTERN_SOIL = 1;
const int PATTERN_SPECKLED = 2;
const int PATTERN_LAYERED = 3;
const int PATTERN_PLANKS = 5;
const int PATTERN_ORE = 7;
const int PATTERN_BRICKS = 6;

const int GRASS_TOP = 1;
const int GRASS_ALL = 2;
const int GRASS_FRINGE = 4;

varying vec3 world_pos;
varying vec3 world_normal;
varying flat vec2 data;
// The cell of the instance, from its origin at the centre of the bottom of the cell; a piece
// that sticks out of it, like the cap of the cut, still takes the variation of its own cell.
varying flat vec3 cell_id;
// Height inside the piece, from its base.
varying float local_y;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	data = INSTANCE_CUSTOM.xy;
	cell_id = floor(MODEL_MATRIX[3].xyz);
	local_y = VERTEX.y;
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float hash13(vec3 p3) {
	p3 = fract(p3 * 0.1031);
	p3 += dot(p3, p3.zyx + 31.32);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), u.x),
		mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
	float value = 0.0;
	float amplitude = 0.5;
	for (int i = 0; i < 3; i++) {
		value += amplitude * vnoise(p);
		p = p * 2.03 + vec2(1.7, 9.2);
		amplitude *= 0.5;
	}
	return value / 0.875;
}

// Distance from p to the nearest edge between cells of a Voronoi pattern.
float voronoi_edge(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	float first = 8.0;
	float second = 8.0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 cell = vec2(float(x), float(y));
			vec2 point = cell + vec2(hash12(i + cell), hash12(i + cell + 17.0)) - f;
			float d = dot(point, point);
			if (d < first) {
				second = first;
				first = d;
			} else if (d < second) {
				second = d;
			}
		}
	}
	return sqrt(second) - sqrt(first);
}

vec3 to_output(vec3 color) {
	if (!OUTPUT_IS_SRGB) {
		color = mix(pow((color + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), color * (1.0 / 12.92), lessThan(color, vec3(0.04045)));
	}
	return color;
}

// Patches of dry and deep grass, and blades as dark and light specks, a little stretched.
vec3 grass(vec2 uv, vec3 cell) {
	float patch = fbm(uv * 0.22);
	vec3 color = mix(grass_lush, grass_dry, smoothstep(0.55, 0.8, patch) * 0.8);
	color = mix(color, grass_deep, smoothstep(0.45, 0.22, patch) * 0.7);
	color *= 0.96 + 0.08 * hash13(cell);
	color *= mix(1.0, 0.86, step(0.72, vnoise(uv * vec2(26.0, 20.0))));
	color = mix(color, color * 1.1 + vec3(0.01, 0.02, 0.0), step(0.8, vnoise(uv * vec2(19.0, 23.0) + 7.3)));
	return color;
}

// The colour of the material, varied a little per cell and in soft patches, with its pattern.
vec3 material(int pattern, vec3 base, vec2 uv, vec3 pos, vec3 cell, bool top) {
	vec3 color = base * (0.93 + 0.14 * hash13(cell)) * (0.94 + 0.12 * fbm(uv * 0.4));
	if (pattern == PATTERN_PLAIN) {
		return color;
	}

	if (pattern == PATTERN_SOIL) {
		color *= 0.9 + 0.2 * vnoise(uv * 18.0);
		float pebble = step(0.8, vnoise(uv * 7.0 + 3.1));
		color = mix(color, base * 1.35 + vec3(0.06), pebble * 0.45);
	} else if (pattern == PATTERN_SPECKLED) {
		float speck = hash12(floor(uv * 24.0));
		color *= speck > 0.86 ? 0.68 : (speck < 0.1 ? 1.22 : 1.0);
		color *= mix(0.72, 1.0, smoothstep(0.0, 0.05, voronoi_edge(uv * 2.6)));
	} else if (pattern == PATTERN_ORE) {
		// A reddish rock with dark, roundish blobs of ore, a few of them bright at the edge.
		vec2 grid = uv * 7.0;
		vec2 id = floor(grid);
		vec2 jitter = vec2(hash12(id + 3.1), hash12(id + 8.7)) * 0.5 + 0.25;
		float blob = length(fract(grid) - jitter);
		float size = 0.16 + 0.14 * hash12(id + 1.9);
		color *= mix(0.45, 1.0, smoothstep(size - 0.03, size + 0.03, blob));
		color += vec3(0.05, 0.02, 0.0) * (1.0 - smoothstep(size, size + 0.1, blob)) * step(0.7, hash12(id));
	} else if (pattern == PATTERN_LAYERED) {
		if (top) {
			color *= 0.9 + 0.2 * fbm(uv * 3.0);
		} else {
			float band = vnoise(vec2(pos.y * 7.0 + fbm(uv * vec2(1.2, 0.0)) * 1.6, 0.5));
			color *= 0.86 + 0.26 * band;
			color *= mix(0.8, 1.0, smoothstep(0.0, 0.04, voronoi_edge(uv * vec2(1.4, 3.5))));
		}
	} else if (pattern == PATTERN_PLANKS) {
		// Planks of a quarter of a cell, along X on top and lying on the sides.
		float across = top ? uv.y : pos.y;
		float along = uv.x;
		float row = floor(across * 4.0);
		float seam = smoothstep(0.0, 0.09, fract(across * 4.0)) * smoothstep(1.0, 0.91, fract(across * 4.0));
		color *= mix(0.62, 1.0, seam);
		color *= 0.86 + 0.18 * hash12(vec2(row, floor(along * 0.7 + hash12(vec2(row, 3.0)))));
		color *= 0.9 + 0.14 * vnoise(vec2(along * 1.6, across * 40.0));
	} else if (pattern == PATTERN_BRICKS) {
		vec2 brick = top ? uv * vec2(2.0, 4.0) : vec2(uv.x * 2.0, pos.y * 4.0);
		brick.x += mod(floor(brick.y), 2.0) * 0.5;
		vec2 inside = fract(brick);
		float mortar = min(min(inside.x, 1.0 - inside.x) * 0.5, min(inside.y, 1.0 - inside.y));
		color *= 0.88 + 0.2 * hash12(floor(brick));
		color = mix(base * 1.25 + vec3(0.05), color, smoothstep(0.02, 0.05, mortar));
	}
	return color;
}

void fragment() {
	int pattern = int(data.x + 0.5);
	int flags = int(data.y + 0.5);
	vec3 base = COLOR.rgb;
	vec3 normal = normalize(world_normal);
	vec3 cell = cell_id;
	vec3 pos = world_pos;
	vec3 n = abs(normal);
	// Every face that looks up, the slope of a ramp included, is painted from above.
	bool top = normal.y > 0.5;
	vec2 uv = top ? pos.xz : (n.x >= n.z ? vec2(pos.z, pos.y) : vec2(pos.x, pos.y));

	bool is_grass = (flags & GRASS_ALL) != 0 || ((flags & GRASS_TOP) != 0 && top);
	if (fringe && !is_grass && (flags & GRASS_FRINGE) != 0 && !top && normal.y > -0.5) {
		// A ragged strip of grass hangs over the top of the side of a block.
		float edge = 0.84 - 0.12 * vnoise(vec2(uv.x * 9.0, cell.y));
		is_grass = fract(pos.y) > edge;
	}

	vec3 color = is_grass ? grass(uv, cell) : material(pattern, base, uv, pos, cell, top);
	if (!is_grass && top && snow_amount > 0.0 && (pattern == PATTERN_SPECKLED || pattern == PATTERN_LAYERED)) {
		float height = smoothstep(snow_line - 0.5, snow_line + 3.0, pos.y);
		float patchy = 0.75 + 0.25 * fbm(uv * 0.5);
		color = mix(color, snow_color * (0.94 + 0.06 * hash13(cell)), clamp(snow_amount * height * patchy * 1.3, 0.0, 1.0));
	}

	// Edges of the cells read as a faint bevel on top, and the bottom of the sides darkens a
	// little, as if in the shade of the block in front.
	if (top) {
		vec2 f = fract(uv);
		float edge = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));
		color *= mix(is_grass ? 0.93 : 0.86, 1.0, smoothstep(0.0, 0.07, edge));
	} else if (normal.y > -0.5) {
		color *= mix(0.8, 1.0, smoothstep(0.0, 0.55, local_y));
	}

	ALBEDO = to_output(color * brightness);
	ROUGHNESS = 1.0;
}
