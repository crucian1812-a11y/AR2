class_name StarGoal
extends Area3D
# Золотая звезда на вершине — цель финального задания.

var _t := 0.0
var _core: MeshInstance3D


func _ready() -> void:
	collision_layer = 0
	collision_mask = 2
	var cs := CollisionShape3D.new()
	var sh := SphereShape3D.new()
	sh.radius = 1.3
	cs.shape = sh
	add_child(cs)

	_core = MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = 0.6
	mesh.height = 1.2
	_core.mesh = mesh
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(1.0, 0.9, 0.3)
	m.emission_enabled = true
	m.emission = Color(1.0, 0.8, 0.2)
	m.emission_energy_multiplier = 3.0
	_core.material_override = m
	add_child(_core)

	for i in range(5):
		var spike := MeshInstance3D.new()
		var prism := PrismMesh.new()
		prism.size = Vector3(0.35, 0.8, 0.15)
		spike.mesh = prism
		var ang := TAU * i / 5.0
		spike.position = Vector3(cos(ang) * 0.75, sin(ang) * 0.75, 0)
		spike.rotation.z = ang - PI / 2.0
		spike.material_override = m
		_core.add_child(spike)

	var parts := GPUParticles3D.new()
	parts.amount = 30
	parts.lifetime = 1.8
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	pm.emission_sphere_radius = 1.0
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 60.0
	pm.initial_velocity_min = 0.4
	pm.initial_velocity_max = 1.2
	pm.gravity = Vector3.ZERO
	pm.scale_min = 0.05
	pm.scale_max = 0.12
	pm.color = Color(1.0, 0.9, 0.4)
	parts.process_material = pm
	var quad := QuadMesh.new()
	quad.size = Vector2(0.4, 0.4)
	var qm := StandardMaterial3D.new()
	qm.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	qm.vertex_color_use_as_albedo = true
	qm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	qm.emission_enabled = true
	qm.emission = Color(1.0, 0.85, 0.3)
	qm.emission_energy_multiplier = 2.0
	quad.material = qm
	parts.draw_pass_1 = quad
	add_child(parts)

	var label := Label3D.new()
	label.text = "Вершина!"
	label.position = Vector3(0, 2.0, 0)
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.font_size = 80
	label.outline_size = 14
	add_child(label)

	body_entered.connect(_on_body_entered)


func _process(delta: float) -> void:
	_t += delta
	_core.rotation.y += delta * 1.5
	_core.position.y = sin(_t * 2.0) * 0.2 + 0.2


func _on_body_entered(body: Node3D) -> void:
	if body is BearPlayer and body.is_local:
		Net.request_victory.rpc_id(1)
