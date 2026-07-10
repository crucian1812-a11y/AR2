class_name Coin
extends Area3D
# Золотая монета: крутится, светится, собирается локальным игроком.

var _base_y := 0.0
var _t := 0.0
var _requested := false


func _ready() -> void:
	collision_layer = 0
	collision_mask = 2
	var cs := CollisionShape3D.new()
	var sh := SphereShape3D.new()
	sh.radius = 0.7
	cs.shape = sh
	add_child(cs)

	var mi := MeshInstance3D.new()
	var mesh := CylinderMesh.new()
	mesh.top_radius = 0.32
	mesh.bottom_radius = 0.32
	mesh.height = 0.08
	mi.mesh = mesh
	mi.rotation.x = deg_to_rad(90.0)
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(1.0, 0.85, 0.2)
	m.metallic = 0.8
	m.roughness = 0.25
	m.emission_enabled = true
	m.emission = Color(1.0, 0.75, 0.1)
	m.emission_energy_multiplier = 1.4
	mi.material_override = m
	add_child(mi)

	_base_y = position.y
	_t = randf() * TAU
	body_entered.connect(_on_body_entered)


func _process(delta: float) -> void:
	_t += delta
	rotate_y(delta * 2.5)
	position.y = _base_y + sin(_t * 2.0) * 0.12


func _on_body_entered(body: Node3D) -> void:
	if _requested:
		return
	if body is BearPlayer and body.is_local:
		_requested = true
		Net.request_collect.rpc_id(1, Net.current_world, String(name))


func collect_effect() -> void:
	set_process(false)
	set_deferred("monitoring", false)
	var tw := create_tween()
	tw.tween_property(self, "scale", Vector3(2.0, 2.0, 2.0), 0.18)
	tw.parallel().tween_property(self, "position:y", position.y + 1.2, 0.18)
	tw.tween_callback(queue_free)
