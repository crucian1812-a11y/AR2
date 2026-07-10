shader_type spatial;
render_mode cull_disabled;

uniform vec3 col_base: source_color = vec3(0.2, 0.45, 0.15);
uniform vec3 col_tip: source_color = vec3(0.5, 0.8, 0.3);

void vertex() {
	float ph = float(INSTANCE_ID) * 1.73;
	float top = 1.0 - UV.y;
	VERTEX.x += sin(TIME * 2.2 + ph) * 0.12 * top;
	VERTEX.z += cos(TIME * 1.7 + ph * 0.9) * 0.06 * top;
}

void fragment() {
	float top = 1.0 - UV.y;
	ALBEDO = mix(col_base, col_tip, top);
	ROUGHNESS = 0.9;
}
