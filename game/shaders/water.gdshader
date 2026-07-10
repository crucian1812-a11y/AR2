shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back;

uniform vec3 shallow_color: source_color = vec3(0.3, 0.7, 0.85);
uniform vec3 deep_color: source_color = vec3(0.1, 0.3, 0.6);
uniform float wave_height = 0.08;
uniform float wave_speed = 1.6;

void vertex() {
	float w1 = sin(TIME * wave_speed + VERTEX.x * 0.9 + VERTEX.z * 0.4);
	float w2 = cos(TIME * wave_speed * 0.7 + VERTEX.z * 1.1);
	VERTEX.y += (w1 + w2) * wave_height * 0.5;
}

void fragment() {
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	ALBEDO = mix(deep_color, shallow_color, fresnel);
	ROUGHNESS = 0.08;
	METALLIC = 0.1;
	SPECULAR = 0.6;
	ALPHA = 0.82;
	EMISSION = shallow_color * fresnel * 0.15;
}
