class_name WorldBase
extends Node3D
# База для всех миров: небо/свет/туман, помощники для платформ, деревьев,
# монет, врагов, порталов, воды, травы и облаков.
# Конкретные миры переопределяют build().

const WATER_SHADER := preload("res://shaders/water.gdshader")
const GRASS_SHADER := preload("res://shaders/grass.gdshader")

static var _tex_cache: Dictionary = {}

var spawn_point := Vector3(0, 1.5, 6)
var coins_node: Node3D
var enemies_node: Node3D

var _mat_cache: Dictionary = {}
var _coin_count := 0
var _enemy_count := 0
var _clouds: Array[Node3D] = []


func _ready() -> void:
	coins_node = Node3D.new()
	coins_node.name = "Coins"
	add_child(coins_node)
	enemies_node = Node3D.new()
	enemies_node.name = "Enemies"
	add_child(enemies_node)
	build()


func build() -> void:
	pass


func _process(delta: float) -> void:
	for c in _clouds:
		c.position.x += delta * 0.6
		if c.position.x > 70.0:
			c.position.x = -70.0


# ---------- Процедурные текстуры (общие на все миры) ----------


static func detail_tex() -> Texture2D:
	if _tex_cache.has("detail"):
		return _tex_cache["detail"]
	var n := FastNoiseLite.new()
	n.noise_type = FastNoiseLite.TYPE_SIMPLEX
	n.frequency = 0.06
	var img := n.get_seamless_image(128, 128)
	for y in range(128):
		for x in range(128):
			var v := img.get_pixel(x, y).r
			var g := 0.78 + 0.22 * v
			img.set_pixel(x, y, Color(g, g, g))
	var t := ImageTexture.create_from_image(img)
	_tex_cache["detail"] = t
	return t


static func bump_tex() -> Texture2D:
	if _tex_cache.has("bump"):
		return _tex_cache["bump"]
	var n := FastNoiseLite.new()
	n.noise_type = FastNoiseLite.TYPE_SIMPLEX
	n.frequency = 0.1
	var img := n.get_seamless_image(128, 128)
	img.bump_map_to_normal_map(2.0)
	var t := ImageTexture.create_from_image(img)
	_tex_cache["bump"] = t
	return t


static func water_bump_tex() -> Texture2D:
	if _tex_cache.has("water_bump"):
		return _tex_cache["water_bump"]
	var n := FastNoiseLite.new()
	n.noise_type = FastNoiseLite.TYPE_SIMPLEX
	n.frequency = 0.15
	var img := n.get_seamless_image(128, 128)
	img.bump_map_to_normal_map(1.5)
	var t := ImageTexture.create_from_image(img)
	_tex_cache["water_bump"] = t
	return t


# ---------- Окружение ----------


func setup_sky(
	top: Color,
	horizon: Color,
	ground: Color,
	sun_rot_deg: Vector3,
	sun_energy := 1.3,
	fog_density := 0.0,
	fog_color := Color(0.75, 0.82, 0.9)
) -> void:
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = top
	sky_mat.sky_horizon_color = horizon
	sky_mat.ground_bottom_color = ground
	sky_mat.ground_horizon_color = horizon
	var sky := Sky.new()
	sky.sky_material = sky_mat

	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 1.0
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.glow_enabled = true
	env.glow_intensity = 0.6
	env.glow_bloom = 0.05
	env.adjustment_enabled = true
	env.adjustment_saturation = 1.12
	env.adjustment_contrast = 1.04
	if fog_density > 0.0:
		env.fog_enabled = true
		env.fog_light_color = fog_color
		env.fog_density = fog_density
		env.fog_sky_affect = 0.3

	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = sun_rot_deg
	sun.light_energy = sun_energy
	sun.shadow_enabled = true
	sun.shadow_blur = 1.4
	sun.directional_shadow_max_distance = 70.0
	add_child(sun)


# ---------- Материалы и геометрия ----------


func mat(
	color: Color,
	rough := 0.9,
	metal := 0.0,
	emis := Color.BLACK,
	detail := 0.0,
	normal_s := 0.0
) -> StandardMaterial3D:
	var key := (
		"%s|%f|%f|%s|%f|%f" % [color.to_html(), rough, metal, emis.to_html(), detail, normal_s]
	)
	if _mat_cache.has(key):
		return _mat_cache[key]
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = rough
	m.metallic = metal
	if emis != Color.BLACK:
		m.emission_enabled = true
		m.emission = emis
		m.emission_energy_multiplier = 1.2
	if detail > 0.0 or normal_s > 0.0:
		m.uv1_triplanar = true
		m.uv1_world_triplanar = true
		var s := detail if detail > 0.0 else 0.3
		m.uv1_scale = Vector3(s, s, s)
	if detail > 0.0:
		m.albedo_texture = detail_tex()
	if normal_s > 0.0:
		m.normal_enabled = true
		m.normal_texture = bump_tex()
		m.normal_scale = normal_s
	_mat_cache[key] = m
	return m


func add_box(
	pos: Vector3,
	size: Vector3,
	color: Color,
	rough := 0.9,
	metal := 0.0,
	yaw_deg := 0.0,
	detail := 0.0,
	normal_s := 0.0
) -> StaticBody3D:
	var body := StaticBody3D.new()
	body.position = pos
	body.rotation.y = deg_to_rad(yaw_deg)
	var cs := CollisionShape3D.new()
	var sh := BoxShape3D.new()
	sh.size = size
	cs.shape = sh
	body.add_child(cs)
	var mi := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	mesh.size = size
	mi.mesh = mesh
	mi.material_override = mat(color, rough, metal, Color.BLACK, detail, normal_s)
	body.add_child(mi)
	add_child(body)
	return body


func add_cylinder(
	pos: Vector3, radius: float, height: float, color: Color, rough := 0.9, collide := true
) -> Node3D:
	var root: Node3D
	if collide:
		var body := StaticBody3D.new()
		var cs := CollisionShape3D.new()
		var sh := CylinderShape3D.new()
		sh.radius = radius
		sh.height = height
		cs.shape = sh
		body.add_child(cs)
		root = body
	else:
		root = Node3D.new()
	root.position = pos
	var mi := MeshInstance3D.new()
	var mesh := CylinderMesh.new()
	mesh.top_radius = radius
	mesh.bottom_radius = radius
	mesh.height = height
	mi.mesh = mesh
	mi.material_override = mat(color, rough)
	root.add_child(mi)
	add_child(root)
	return root


func add_decor_ball(pos: Vector3, s: Vector3, color: Color, rough := 0.9) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = 0.5
	mesh.height = 1.0
	mi.mesh = mesh
	mi.position = pos
	mi.scale = s
	mi.material_override = mat(color, rough)
	add_child(mi)
	return mi


func add_tree(pos: Vector3, leaf_color := Color(0.25, 0.6, 0.25), scale_f := 1.0) -> void:
	var trunk := add_cylinder(
		pos + Vector3(0, 1.2 * scale_f, 0),
		0.3 * scale_f,
		2.4 * scale_f,
		Color(0.42, 0.28, 0.15)
	)
	trunk.name = "Tree_%d" % get_child_count()
	add_decor_ball(
		pos + Vector3(0, 3.0 * scale_f, 0),
		Vector3(2.6, 2.2, 2.6) * scale_f,
		leaf_color
	)
	add_decor_ball(
		pos + Vector3(0.9 * scale_f, 2.4 * scale_f, 0.4 * scale_f),
		Vector3(1.6, 1.4, 1.6) * scale_f,
		leaf_color.lightened(0.12)
	)
	add_decor_ball(
		pos + Vector3(-0.8 * scale_f, 2.5 * scale_f, -0.5 * scale_f),
		Vector3(1.5, 1.3, 1.5) * scale_f,
		leaf_color.darkened(0.08)
	)


func add_pine(pos: Vector3, snowy := true, scale_f := 1.0) -> void:
	add_cylinder(
		pos + Vector3(0, 0.8 * scale_f, 0), 0.22 * scale_f, 1.6 * scale_f, Color(0.35, 0.24, 0.14)
	)
	var green := Color(0.16, 0.38, 0.24)
	var sizes := [2.4, 1.9, 1.3]
	for i in range(3):
		var cone := MeshInstance3D.new()
		var mesh := CylinderMesh.new()
		mesh.top_radius = 0.0
		mesh.bottom_radius = sizes[i] * 0.5 * scale_f
		mesh.height = 1.5 * scale_f
		cone.mesh = mesh
		cone.position = pos + Vector3(0, (1.6 + i * 1.0) * scale_f, 0)
		cone.material_override = mat(green.lightened(i * 0.06))
		add_child(cone)
		if snowy:
			var cap := MeshInstance3D.new()
			var cap_mesh := CylinderMesh.new()
			cap_mesh.top_radius = 0.0
			cap_mesh.bottom_radius = sizes[i] * 0.3 * scale_f
			cap_mesh.height = 0.7 * scale_f
			cap.mesh = cap_mesh
			cap.position = pos + Vector3(0, (2.2 + i * 1.0) * scale_f, 0)
			cap.material_override = mat(Color(0.95, 0.96, 1.0), 0.6)
			add_child(cap)


func add_rock(pos: Vector3, s: float, color := Color(0.55, 0.55, 0.58)) -> void:
	var body := StaticBody3D.new()
	body.position = pos
	var cs := CollisionShape3D.new()
	var sh := SphereShape3D.new()
	sh.radius = s * 0.45
	cs.shape = sh
	body.add_child(cs)
	var mi := MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = 0.5
	mesh.height = 1.0
	mi.mesh = mesh
	mi.scale = Vector3(s, s * 0.7, s * 0.9)
	mi.material_override = mat(color, 0.95, 0.0, Color.BLACK, 1.2, 0.8)
	body.add_child(mi)
	add_child(body)


func add_bush(pos: Vector3, color := Color(0.2, 0.45, 0.2)) -> void:
	add_decor_ball(pos + Vector3(0, 0.35, 0), Vector3(1.1, 0.8, 1.1), color)
	add_decor_ball(pos + Vector3(0.45, 0.28, 0.2), Vector3(0.7, 0.55, 0.7), color.lightened(0.1))
	add_decor_ball(pos + Vector3(-0.4, 0.3, -0.15), Vector3(0.65, 0.5, 0.65), color.darkened(0.07))


func add_flower(pos: Vector3, color: Color) -> void:
	var stem := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = 0.03
	cyl.bottom_radius = 0.03
	cyl.height = 0.4
	stem.mesh = cyl
	stem.position = pos + Vector3(0, 0.2, 0)
	stem.material_override = mat(Color(0.3, 0.55, 0.25))
	add_child(stem)
	add_decor_ball(pos + Vector3(0, 0.45, 0), Vector3(0.22, 0.18, 0.22), color)


func add_grass(center: Vector3, extent: Vector2, count: int, base: Color, tip: Color) -> void:
	var quad := QuadMesh.new()
	quad.size = Vector2(0.16, 0.42)
	quad.center_offset = Vector3(0, 0.21, 0)
	var sm := ShaderMaterial.new()
	sm.shader = GRASS_SHADER
	sm.set_shader_parameter("col_base", base)
	sm.set_shader_parameter("col_tip", tip)
	quad.material = sm

	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = quad
	mm.instance_count = count
	var rng := RandomNumberGenerator.new()
	rng.seed = 12345
	for i in range(count):
		var pos := center + Vector3(
			rng.randf_range(-extent.x, extent.x), 0.0, rng.randf_range(-extent.y, extent.y)
		)
		var b := Basis(Vector3.UP, rng.randf() * TAU).scaled(
			Vector3.ONE * rng.randf_range(0.7, 1.5)
		)
		mm.set_instance_transform(i, Transform3D(b, pos))

	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = mm
	mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mmi)


func add_cloud(pos: Vector3, s := 1.0, tint := Color(1, 1, 1)) -> void:
	var root := Node3D.new()
	root.position = pos
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(tint.r, tint.g, tint.b, 0.85)
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	var offsets := [
		Vector3(0, 0, 0),
		Vector3(2.2, 0.3, 0.4),
		Vector3(-2.0, 0.2, -0.3),
		Vector3(0.8, 0.8, -0.5),
	]
	var scales := [
		Vector3(4.0, 1.6, 2.6), Vector3(2.6, 1.2, 2.0), Vector3(2.4, 1.1, 1.8),
		Vector3(2.2, 1.3, 1.8)
	]
	for i in range(4):
		var mi := MeshInstance3D.new()
		var mesh := SphereMesh.new()
		mesh.radius = 0.5
		mesh.height = 1.0
		mi.mesh = mesh
		mi.position = offsets[i] * s
		mi.scale = scales[i] * s
		mi.material_override = m
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		root.add_child(mi)
	add_child(root)
	_clouds.append(root)


func add_mountain(pos: Vector3, radius: float, height: float, color: Color, cap := true) -> void:
	var cone := MeshInstance3D.new()
	var mesh := CylinderMesh.new()
	mesh.top_radius = radius * 0.06
	mesh.bottom_radius = radius
	mesh.height = height
	cone.mesh = mesh
	cone.position = pos + Vector3(0, height * 0.5 - 2.0, 0)
	cone.material_override = mat(color, 0.95, 0.0, Color.BLACK, 0.15, 0.5)
	cone.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(cone)
	if cap:
		var top := MeshInstance3D.new()
		var top_mesh := CylinderMesh.new()
		top_mesh.top_radius = 0.0
		top_mesh.bottom_radius = radius * 0.3
		top_mesh.height = height * 0.28
		top.mesh = top_mesh
		top.position = pos + Vector3(0, height - 2.0, 0)
		top.material_override = mat(Color(0.96, 0.97, 1.0), 0.6)
		top.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(top)


func add_motes(
	center: Vector3, extents: Vector3, count: int, color: Color, size := 0.09
) -> void:
	var parts := GPUParticles3D.new()
	parts.amount = count
	parts.lifetime = 7.0
	parts.preprocess = 7.0
	parts.position = center
	parts.visibility_aabb = AABB(-extents - Vector3(2, 2, 2), extents * 2.0 + Vector3(4, 4, 4))
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	pm.emission_box_extents = extents
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 180.0
	pm.initial_velocity_min = 0.15
	pm.initial_velocity_max = 0.5
	pm.gravity = Vector3(0.1, 0.05, 0.08)
	pm.scale_min = size * 0.6
	pm.scale_max = size
	pm.color = color
	parts.process_material = pm
	var quad := QuadMesh.new()
	quad.size = Vector2(0.35, 0.35)
	var qm := StandardMaterial3D.new()
	qm.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	qm.vertex_color_use_as_albedo = true
	qm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	qm.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	qm.emission_enabled = true
	qm.emission = Color(color.r, color.g, color.b)
	qm.emission_energy_multiplier = 1.2
	quad.material = qm
	parts.draw_pass_1 = quad
	add_child(parts)


func add_leaves(center: Vector3, extents: Vector3, count: int, color: Color) -> void:
	var parts := GPUParticles3D.new()
	parts.amount = count
	parts.lifetime = 6.0
	parts.preprocess = 6.0
	parts.position = center
	parts.visibility_aabb = AABB(
		-extents - Vector3(2, 10, 2), extents * 2.0 + Vector3(4, 14, 4)
	)
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	pm.emission_box_extents = extents
	pm.direction = Vector3(0, -1, 0)
	pm.spread = 30.0
	pm.initial_velocity_min = 0.4
	pm.initial_velocity_max = 0.9
	pm.gravity = Vector3(0.5, -0.9, 0.2)
	pm.angle_min = 0.0
	pm.angle_max = 360.0
	pm.angular_velocity_min = -120.0
	pm.angular_velocity_max = 120.0
	pm.scale_min = 0.5
	pm.scale_max = 0.9
	pm.color = color
	parts.process_material = pm
	var quad := QuadMesh.new()
	quad.size = Vector2(0.22, 0.16)
	var qm := StandardMaterial3D.new()
	qm.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	qm.vertex_color_use_as_albedo = true
	qm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	quad.material = qm
	parts.draw_pass_1 = quad
	add_child(parts)


func add_water(pos: Vector3, size: Vector2) -> void:
	var mi := MeshInstance3D.new()
	var mesh := PlaneMesh.new()
	mesh.size = size
	mesh.subdivide_width = 16
	mesh.subdivide_depth = 16
	mi.mesh = mesh
	mi.position = pos
	var sm := ShaderMaterial.new()
	sm.shader = WATER_SHADER
	sm.set_shader_parameter("normal_tex", water_bump_tex())
	sm.set_shader_parameter("uv_scale", maxf(size.x, size.y) * 0.4)
	mi.material_override = sm
	add_child(mi)


# ---------- Игровые объекты ----------


func add_coin(pos: Vector3) -> void:
	var c := Coin.new()
	c.name = "Coin_%d" % _coin_count
	_coin_count += 1
	c.position = pos
	coins_node.add_child(c)


func add_enemy(a: Vector3, b: Vector3, kind := "mushroom", speed := 2.0) -> void:
	var e := Enemy3.new()
	e.name = "Enemy_%d" % _enemy_count
	_enemy_count += 1
	e.point_a = a
	e.point_b = b
	e.kind = kind
	e.speed = speed
	e.position = a
	enemies_node.add_child(e)


func add_portal(pos: Vector3, target: String, text: String, color: Color) -> void:
	var p := Portal.new()
	p.target = target
	p.label_text = text
	p.portal_color = color
	p.position = pos
	add_child(p)


func add_npc(pos: Vector3) -> void:
	var n := Npc.new()
	n.position = pos
	add_child(n)


func add_star(pos: Vector3) -> void:
	var s := StarGoal.new()
	s.position = pos
	add_child(s)


# ---------- Сетевая синхронизация врагов ----------


func simulate_enemies(delta: float) -> void:
	for e in enemies_node.get_children():
		if e is Enemy3:
			e.host_step(delta)


func gather_enemy_states() -> Array:
	var out: Array = []
	for e in enemies_node.get_children():
		if e is Enemy3 and not e.dying:
			out.append([String(e.name), e.global_position, e.get_yaw()])
	return out


func apply_enemy_states(data: Array) -> void:
	for row in data:
		if row is Array and row.size() >= 3:
			var e := enemies_node.get_node_or_null(NodePath(String(row[0])))
			if e is Enemy3:
				e.set_net_state(row[1], row[2])
