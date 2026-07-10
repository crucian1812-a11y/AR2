class_name WorldBase
extends Node3D
# База для всех миров: небо/свет/туман, помощники для платформ, деревьев,
# монет, врагов, порталов, воды. Конкретные миры переопределяют build().

const WATER_SHADER := preload("res://shaders/water.gdshader")

var spawn_point := Vector3(0, 1.5, 6)
var coins_node: Node3D
var enemies_node: Node3D

var _mat_cache: Dictionary = {}
var _coin_count := 0
var _enemy_count := 0


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
	sun.directional_shadow_max_distance = 70.0
	add_child(sun)


# ---------- Материалы и геометрия ----------


func mat(color: Color, rough := 0.9, metal := 0.0, emis := Color.BLACK) -> StandardMaterial3D:
	var key := "%s|%f|%f|%s" % [color.to_html(), rough, metal, emis.to_html()]
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
	_mat_cache[key] = m
	return m


func add_box(
	pos: Vector3, size: Vector3, color: Color, rough := 0.9, metal := 0.0, yaw_deg := 0.0
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
	mi.material_override = mat(color, rough, metal)
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
	mi.material_override = mat(color, 0.95)
	body.add_child(mi)
	add_child(body)


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
