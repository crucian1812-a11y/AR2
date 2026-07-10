class_name Enemy3
extends Node3D
# Враг (гриб или ледяной слизень), патрулирует между двумя точками.
# Движение считает хост, клиенты интерполируют по данным из сети.

var point_a := Vector3.ZERO
var point_b := Vector3.ZERO
var speed := 2.0
var kind := "mushroom"
var dying := false

var _target := Vector3.ZERO
var _net_pos := Vector3.ZERO
var _net_yaw := 0.0
var _has_net := false
var _bob_t := 0.0
var _visual: Node3D


func _ready() -> void:
	add_to_group("enemies")
	_target = point_b
	_net_pos = global_position
	_bob_t = randf() * TAU
	_visual = Node3D.new()
	add_child(_visual)
	if kind == "slime":
		_build_slime()
	else:
		_build_mushroom()


func host_step(delta: float) -> void:
	if dying:
		return
	var to := _target - global_position
	if to.length() < 0.08:
		_target = point_a if _target.is_equal_approx(point_b) else point_b
		to = _target - global_position
	global_position = global_position.move_toward(_target, speed * delta)
	if to.length() > 0.01:
		_visual.rotation.y = atan2(to.x, to.z)


func set_net_state(pos: Vector3, yaw: float) -> void:
	_net_pos = pos
	_net_yaw = yaw
	_has_net = true


func get_yaw() -> float:
	return _visual.rotation.y


func _process(delta: float) -> void:
	if dying:
		return
	if not Net.is_host and _has_net:
		global_position = global_position.lerp(_net_pos, minf(delta * 10.0, 1.0))
		if global_position.distance_to(_net_pos) > 8.0:
			global_position = _net_pos
		_visual.rotation.y = lerp_angle(_visual.rotation.y, _net_yaw, minf(delta * 10.0, 1.0))
	_bob_t += delta * 6.0
	_visual.scale.y = 1.0 + sin(_bob_t) * 0.05


func die_effect() -> void:
	dying = true
	remove_from_group("enemies")
	var tw := create_tween()
	tw.tween_property(_visual, "scale", Vector3(1.4, 0.1, 1.4), 0.25)
	tw.tween_callback(queue_free)


func _mat(col: Color, rough := 0.8, emis := Color.BLACK) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = col
	m.roughness = rough
	if emis != Color.BLACK:
		m.emission_enabled = true
		m.emission = emis
		m.emission_energy_multiplier = 0.6
	return m


func _ball(parent: Node3D, pos: Vector3, s: Vector3, m: StandardMaterial3D) -> void:
	var mi := MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = 0.5
	mesh.height = 1.0
	mi.mesh = mesh
	mi.position = pos
	mi.scale = s
	mi.material_override = m
	parent.add_child(mi)


func _build_mushroom() -> void:
	var stem := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = 0.2
	cyl.bottom_radius = 0.26
	cyl.height = 0.5
	stem.mesh = cyl
	stem.position = Vector3(0, 0.25, 0)
	stem.material_override = _mat(Color(0.93, 0.87, 0.7))
	_visual.add_child(stem)

	var cap_m := _mat(Color(0.85, 0.2, 0.15))
	_ball(_visual, Vector3(0, 0.62, 0), Vector3(0.85, 0.5, 0.85), cap_m)
	var dot_m := _mat(Color(0.95, 0.95, 0.9))
	_ball(_visual, Vector3(0.2, 0.78, 0.1), Vector3(0.12, 0.08, 0.12), dot_m)
	_ball(_visual, Vector3(-0.18, 0.76, -0.12), Vector3(0.1, 0.07, 0.1), dot_m)
	_ball(_visual, Vector3(0.0, 0.8, -0.2), Vector3(0.09, 0.06, 0.09), dot_m)
	var eye_m := _mat(Color(0.1, 0.07, 0.06))
	_ball(_visual, Vector3(-0.1, 0.42, 0.22), Vector3(0.08, 0.1, 0.05), eye_m)
	_ball(_visual, Vector3(0.1, 0.42, 0.22), Vector3(0.08, 0.1, 0.05), eye_m)


func _build_slime() -> void:
	var body_m := _mat(Color(0.45, 0.75, 1.0, 0.85), 0.15, Color(0.2, 0.4, 0.8))
	body_m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_ball(_visual, Vector3(0, 0.4, 0), Vector3(0.9, 0.75, 0.9), body_m)
	var eye_m := _mat(Color(0.08, 0.1, 0.2))
	_ball(_visual, Vector3(-0.14, 0.5, 0.35), Vector3(0.1, 0.12, 0.06), eye_m)
	_ball(_visual, Vector3(0.14, 0.5, 0.35), Vector3(0.1, 0.12, 0.06), eye_m)
