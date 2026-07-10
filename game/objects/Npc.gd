class_name Npc
extends Area3D
# Старейшина-медведь: выдаёт задание и комментирует прогресс.


func _ready() -> void:
	collision_layer = 0
	collision_mask = 2
	var cs := CollisionShape3D.new()
	var sh := SphereShape3D.new()
	sh.radius = 3.0
	cs.shape = sh
	cs.position = Vector3(0, 1, 0)
	add_child(cs)

	_build_elder()

	var label := Label3D.new()
	label.text = "Старейшина"
	label.position = Vector3(0, 2.9, 0)
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.font_size = 56
	label.outline_size = 10
	label.modulate = Color(1.0, 0.95, 0.6)
	add_child(label)

	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)


func dialog_text() -> String:
	if Net.victory_reached:
		return "Вы — настоящие герои! Вершина покорена!"
	match Net.quest_stage:
		2:
			return "Портал открыт! Доберитесь до золотой звезды на Снежной вершине!"
		1:
			return (
				"Соберите %d монет (%d/%d) — и я открою портал в Снежные вершины!"
				% [Net.QUEST_COINS, Net.coins_total, Net.QUEST_COINS]
			)
		_:
			return "Приветствую, медвежата! У меня есть для вас задание..."


func _on_body_entered(body: Node3D) -> void:
	if body is BearPlayer and body.is_local:
		Net.request_quest.rpc_id(1)
		var hud := get_tree().get_first_node_in_group("hud")
		if hud != null:
			hud.show_dialog(dialog_text())


func _on_body_exited(body: Node3D) -> void:
	if body is BearPlayer and body.is_local:
		var hud := get_tree().get_first_node_in_group("hud")
		if hud != null:
			hud.hide_dialog()


func _mat(col: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = col
	m.roughness = 0.85
	return m


func _ball(pos: Vector3, s: Vector3, m: StandardMaterial3D) -> void:
	var mi := MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = 0.5
	mesh.height = 1.0
	mi.mesh = mesh
	mi.position = pos
	mi.scale = s
	mi.material_override = m
	add_child(mi)


func _build_elder() -> void:
	var fur := _mat(Color(0.55, 0.55, 0.58))
	var fur_light := _mat(Color(0.75, 0.75, 0.78))
	var dark := _mat(Color(0.12, 0.1, 0.1))

	_ball(Vector3(0, 0.95, 0), Vector3(1.35, 1.6, 1.2), fur)
	_ball(Vector3(0, 0.9, 0.4), Vector3(0.85, 1.1, 0.55), fur_light)
	_ball(Vector3(0, 1.95, 0), Vector3(0.95, 0.9, 0.9), fur)
	_ball(Vector3(0, 1.85, 0.38), Vector3(0.45, 0.36, 0.34), fur_light)
	_ball(Vector3(0, 1.9, 0.55), Vector3(0.14, 0.12, 0.1), dark)
	_ball(Vector3(-0.18, 2.05, 0.38), Vector3(0.11, 0.12, 0.08), dark)
	_ball(Vector3(0.18, 2.05, 0.38), Vector3(0.11, 0.12, 0.08), dark)
	_ball(Vector3(-0.32, 2.35, 0), Vector3(0.28, 0.28, 0.16), fur)
	_ball(Vector3(0.32, 2.35, 0), Vector3(0.28, 0.28, 0.16), fur)
	_ball(Vector3(0, 1.62, 0.42), Vector3(0.5, 0.5, 0.3), fur_light)

	var staff := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = 0.05
	cyl.bottom_radius = 0.07
	cyl.height = 2.4
	staff.mesh = cyl
	staff.position = Vector3(0.85, 1.2, 0.2)
	staff.material_override = _mat(Color(0.4, 0.26, 0.13))
	add_child(staff)

	var orb := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 0.14
	sphere.height = 0.28
	orb.mesh = sphere
	orb.position = Vector3(0.85, 2.45, 0.2)
	var om := StandardMaterial3D.new()
	om.albedo_color = Color(0.5, 0.9, 1.0)
	om.emission_enabled = true
	om.emission = Color(0.4, 0.8, 1.0)
	om.emission_energy_multiplier = 2.0
	orb.material_override = om
	add_child(orb)
