class_name SnowWorld
extends WorldBase
# Снежные вершины — восхождение по спиральным уступам к золотой звезде.

const SNOW := Color(0.93, 0.95, 1.0)
const ICE := Color(0.7, 0.85, 1.0)


func build() -> void:
	spawn_point = Vector3(0, 1.5, 22)
	setup_sky(
		Color(0.45, 0.55, 0.75),
		Color(0.8, 0.85, 0.95),
		Color(0.5, 0.55, 0.65),
		Vector3(-25, 60, 0),
		1.0,
		0.012,
		Color(0.8, 0.86, 0.95)
	)

	# Снежная долина
	add_box(Vector3(0, -0.5, 0), Vector3(80, 1, 80), SNOW, 0.6, 0.0, 0.0, 0.4, 0.5)
	add_box(Vector3(0, -3.0, 0), Vector3(70, 4, 70), Color(0.6, 0.65, 0.75), 0.9, 0.0, 0.0, 0.6, 0.7)

	# Облака
	add_cloud(Vector3(-30, 30, -25), 2.4, Color(0.92, 0.94, 1.0))
	add_cloud(Vector3(25, 34, -40), 2.8, Color(0.9, 0.92, 0.98))
	add_cloud(Vector3(40, 28, 15), 2.0, Color(0.92, 0.94, 1.0))
	add_cloud(Vector3(-18, 36, 30), 2.3, Color(0.9, 0.92, 0.98))

	# Горные хребты вокруг долины
	var mnt := Color(0.62, 0.68, 0.8)
	add_mountain(Vector3(-75, 0, -60), 30, 44, mnt)
	add_mountain(Vector3(-15, 0, -90), 38, 56, mnt.darkened(0.05))
	add_mountain(Vector3(55, 0, -75), 28, 40, mnt)
	add_mountain(Vector3(85, 0, -5), 32, 46, mnt.lightened(0.04))
	add_mountain(Vector3(70, 0, 60), 26, 36, mnt)
	add_mountain(Vector3(-80, 0, 45), 30, 42, mnt.darkened(0.04))

	# Портал домой
	add_portal(Vector3(0, 1.4, 30), "hub", "В деревню", Color(1.0, 0.8, 0.4))

	# Снегопад
	var snow_parts := GPUParticles3D.new()
	snow_parts.amount = 400
	snow_parts.lifetime = 8.0
	snow_parts.preprocess = 8.0
	snow_parts.position = Vector3(0, 20, 0)
	snow_parts.visibility_aabb = AABB(Vector3(-40, -25, -40), Vector3(80, 50, 80))
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	pm.emission_box_extents = Vector3(40, 1, 40)
	pm.direction = Vector3(0, -1, 0)
	pm.spread = 10.0
	pm.initial_velocity_min = 1.5
	pm.initial_velocity_max = 3.0
	pm.gravity = Vector3(0.4, -1.0, 0)
	pm.scale_min = 0.03
	pm.scale_max = 0.08
	pm.color = Color(1, 1, 1, 0.9)
	snow_parts.process_material = pm
	var quad := QuadMesh.new()
	quad.size = Vector2(0.25, 0.25)
	var qm := StandardMaterial3D.new()
	qm.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	qm.vertex_color_use_as_albedo = true
	qm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	quad.material = qm
	snow_parts.draw_pass_1 = quad
	add_child(snow_parts)

	# Гора: центральный пик
	var peak_center := Vector3(0, 0, -18)
	add_cylinder(peak_center + Vector3(0, 8, 0), 5.0, 16.0, Color(0.75, 0.78, 0.88), 0.9)
	add_cylinder(peak_center + Vector3(0, 18.5, 0), 3.2, 5.0, SNOW, 0.7)
	var cone := MeshInstance3D.new()
	var cone_mesh := CylinderMesh.new()
	cone_mesh.top_radius = 0.0
	cone_mesh.bottom_radius = 3.4
	cone_mesh.height = 4.0
	cone.mesh = cone_mesh
	cone.position = peak_center + Vector3(0, 23, 0)
	cone.material_override = mat(SNOW, 0.6)
	add_child(cone)

	# Спиральные уступы вокруг пика
	var ledges := 14
	for i in range(ledges):
		var ang := 0.85 * i + PI * 0.5
		var r := 7.2
		var pos := peak_center + Vector3(cos(ang) * r, 1.2 + i * 1.55, sin(ang) * r)
		var col := ICE if i % 3 == 2 else SNOW
		var rough := 0.15 if i % 3 == 2 else 0.7
		var metal := 0.3 if i % 3 == 2 else 0.0
		add_box(pos, Vector3(3.6, 0.5, 3.6), col, rough, metal, rad_to_deg(-ang))
		if i % 2 == 0:
			add_coin(pos + Vector3(0, 1.2, 0))

	# Враги на уступах
	add_enemy(Vector3(-6, 0.5, 6), Vector3(6, 0.5, 6), "slime", 2.2)
	add_enemy(Vector3(-8, 0.5, -4), Vector3(-2, 0.5, -8), "slime", 2.6)
	var e_ang := 0.85 * 4 + PI * 0.5
	var e_pos := peak_center + Vector3(cos(e_ang) * 7.2, 1.2 + 4 * 1.55 + 0.5, sin(e_ang) * 7.2)
	add_enemy(e_pos + Vector3(-1.2, 0, 0), e_pos + Vector3(1.2, 0, 0), "slime", 1.5)
	var e_ang2 := 0.85 * 9 + PI * 0.5
	var e_pos2 := peak_center + Vector3(cos(e_ang2) * 7.2, 1.2 + 9 * 1.55 + 0.5, sin(e_ang2) * 7.2)
	add_enemy(e_pos2 + Vector3(-1.2, 0, 0), e_pos2 + Vector3(1.2, 0, 0), "slime", 1.8)

	# Вершина со звездой
	var top := peak_center + Vector3(0, 1.2 + ledges * 1.55 + 0.8, 0)
	add_box(top, Vector3(6, 0.6, 6), Color(0.85, 0.9, 1.0), 0.5)
	add_star(top + Vector3(0, 1.8, 0))

	# Монеты в долине
	add_coin(Vector3(8, 1.0, 12))
	add_coin(Vector3(-10, 1.0, 8))
	add_coin(Vector3(12, 1.0, -2))
	add_coin(Vector3(-6, 1.0, 18))
	add_coin(Vector3(14, 1.0, 20))
	add_coin(Vector3(-16, 1.0, -6))
	add_coin(Vector3(6, 1.0, -10))
	add_coin(Vector3(-12, 1.0, 16))

	# Ели и камни
	add_pine(Vector3(-18, 0, 10))
	add_pine(Vector3(16, 0, 8), true, 1.3)
	add_pine(Vector3(-14, 0, -14), true, 0.9)
	add_pine(Vector3(20, 0, -10), true, 1.1)
	add_pine(Vector3(10, 0, 24), true, 0.8)
	add_pine(Vector3(-22, 0, 22), true, 1.2)
	add_rock(Vector3(-8, 0.3, 2), 1.8, Color(0.65, 0.68, 0.75))
	add_rock(Vector3(10, 0.25, 4), 1.3, Color(0.6, 0.62, 0.7))
	add_rock(Vector3(18, 0.3, 14), 1.5, Color(0.68, 0.7, 0.78))
