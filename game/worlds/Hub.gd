class_name HubWorld
extends WorldBase
# Медвежья деревня — стартовый мир с NPC и порталами в другие миры.

const GRASS := Color(0.36, 0.62, 0.3)
const PATH_C := Color(0.78, 0.68, 0.5)


func build() -> void:
	spawn_point = Vector3(0, 1.5, 10)
	setup_sky(
		Color(0.3, 0.55, 0.9),
		Color(0.75, 0.85, 0.95),
		Color(0.25, 0.32, 0.25),
		Vector3(-48, 35, 0),
		1.35
	)

	# Остров
	add_box(Vector3(0, -0.5, 0), Vector3(60, 1, 60), GRASS, 0.95)
	add_box(Vector3(0, -2.5, 0), Vector3(52, 3, 52), Color(0.45, 0.32, 0.2))
	add_box(Vector3(0, -5.0, 0), Vector3(40, 2, 40), Color(0.38, 0.27, 0.17))

	# Дорожки
	add_box(Vector3(0, 0.02, 5), Vector3(3, 0.1, 20), PATH_C, 0.95)
	add_box(Vector3(-8, 0.02, 0), Vector3(14, 0.1, 3), PATH_C, 0.95)
	add_box(Vector3(8, 0.02, 0), Vector3(14, 0.1, 3), PATH_C, 0.95)

	# Фонтан в центре
	add_cylinder(Vector3(0, 0.4, 0), 2.6, 0.8, Color(0.7, 0.7, 0.75), 0.6)
	add_water(Vector3(0, 0.85, 0), Vector2(4.4, 4.4))
	add_cylinder(Vector3(0, 1.5, 0), 0.35, 1.6, Color(0.65, 0.65, 0.7), 0.6)
	var fount := GPUParticles3D.new()
	fount.amount = 40
	fount.lifetime = 1.2
	fount.position = Vector3(0, 2.4, 0)
	var pm := ParticleProcessMaterial.new()
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 20.0
	pm.initial_velocity_min = 3.0
	pm.initial_velocity_max = 4.5
	pm.gravity = Vector3(0, -9.0, 0)
	pm.scale_min = 0.04
	pm.scale_max = 0.1
	pm.color = Color(0.6, 0.85, 1.0, 0.8)
	fount.process_material = pm
	var quad := QuadMesh.new()
	quad.size = Vector2(0.3, 0.3)
	var qm := StandardMaterial3D.new()
	qm.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	qm.vertex_color_use_as_albedo = true
	qm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	quad.material = qm
	fount.draw_pass_1 = quad
	add_child(fount)

	# Домики
	_house(Vector3(-10, 0, -12), Color(0.85, 0.6, 0.4), 20.0)
	_house(Vector3(10, 0, -12), Color(0.6, 0.7, 0.9), -20.0)
	_house(Vector3(-16, 0, 8), Color(0.9, 0.8, 0.5), 70.0)

	# Озеро
	add_box(Vector3(18, -0.45, 16), Vector3(14, 0.9, 12), Color(0.5, 0.42, 0.3))
	add_water(Vector3(18, 0.08, 16), Vector2(13, 11))

	# Деревья и камни
	add_tree(Vector3(-22, 0, -18))
	add_tree(Vector3(22, 0, -20), Color(0.32, 0.55, 0.2), 1.2)
	add_tree(Vector3(-24, 0, 14), Color(0.2, 0.5, 0.28))
	add_tree(Vector3(24, 0, 2), Color(0.3, 0.62, 0.25), 0.9)
	add_tree(Vector3(-6, 0, -22), Color(0.85, 0.5, 0.3))
	add_tree(Vector3(4, 0, 20), Color(0.28, 0.58, 0.25), 1.1)
	add_rock(Vector3(-14, 0.3, -4), 1.6)
	add_rock(Vector3(13, 0.25, 7), 1.2)
	add_rock(Vector3(-4, 0.2, 16), 1.0)

	# Цветы
	var flower_colors := [
		Color(0.95, 0.4, 0.45), Color(0.95, 0.85, 0.3), Color(0.6, 0.5, 0.95), Color(1.0, 0.65, 0.3)
	]
	for i in range(18):
		var ang := randf() * TAU
		var r := randf_range(5.0, 24.0)
		add_flower(Vector3(cos(ang) * r, 0, sin(ang) * r), flower_colors[i % 4])

	# Монеты
	add_coin(Vector3(0, 1.0, 5))
	add_coin(Vector3(6, 1.0, 4))
	add_coin(Vector3(-6, 1.0, 4))
	add_coin(Vector3(18, 1.2, 10))
	add_coin(Vector3(-18, 1.0, -2))
	add_coin(Vector3(0, 1.0, -18))

	# NPC и порталы
	add_npc(Vector3(3.5, 0, -3.5))
	add_portal(Vector3(-14, 1.4, 0), "meadow", "Солнечные луга", Color(0.4, 1.0, 0.5))
	add_portal(Vector3(14, 1.4, 0), "snow", "Снежные вершины", Color(0.5, 0.8, 1.0))


func _house(pos: Vector3, wall: Color, yaw := 0.0) -> void:
	add_box(pos + Vector3(0, 1.5, 0), Vector3(5, 3, 4.4), wall, 0.9, 0.0, yaw)
	var roof := MeshInstance3D.new()
	var prism := PrismMesh.new()
	prism.size = Vector3(5.6, 2.0, 5.0)
	roof.mesh = prism
	roof.position = pos + Vector3(0, 4.0, 0)
	roof.rotation.y = deg_to_rad(yaw)
	roof.material_override = mat(Color(0.6, 0.25, 0.2))
	add_child(roof)
	var door_off := Vector3(0, 0.9, 2.25).rotated(Vector3.UP, deg_to_rad(yaw))
	add_box(pos + door_off, Vector3(1.1, 1.8, 0.15), Color(0.35, 0.22, 0.12))
