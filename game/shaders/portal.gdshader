shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec3 col: source_color = vec3(0.4, 1.0, 0.6);

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float a = atan(p.y, p.x);
	float swirl = sin(a * 3.0 + TIME * 3.0 - r * 9.0) * 0.5 + 0.5;
	float edge = smoothstep(1.0, 0.8, r);
	ALBEDO = col * 0.3;
	EMISSION = col * (0.5 + 1.3 * swirl) * edge;
	ALPHA = edge * (0.35 + 0.5 * swirl);
}
