class_name Portal
extends Area3D
# Портал между мирами: светящееся кольцо с частицами и надписью.

var target := ""
var label_text := ""
var portal_color := Color(0.4, 1.0, 0.6)
var locked := false

const PORTAL_SHADER := preload("res://shaders/portal.gdshader")

var _ring_mat: StandardMaterial3D
var _label: Label3D
var _disc: MeshInstance3D


func _ready() -> void:
	collision_layer = 0
	collision_mask = 2
	var cs := CollisionShape3D.new()
	var sh := CylinderShape3D.new()
	sh.radius = 1.1
	sh.height = 3.0
	cs.shape = sh
	add_child(cs)

	var ring := MeshInstance3D.new()
	var torus := TorusMesh.new()
	torus.inner_radius = 1.05
	torus.outer_radius = 1.4
	ring.mesh = torus
	ring.rotation.x = deg_to_rad(90.0)
	ring.position.y = 0.2
	_ring_mat = StandardMaterial3D.new()
	_ring_mat.roughness = 0.3
	_ring_mat.metallic = 0.4
	ring.material_override = _ring_mat
	add_child(ring)

	_disc = MeshInstance3D.new()
	var quad := QuadMesh.new()
	quad.size = Vector2(2.2, 2.2)
	_disc.mesh = quad
	_disc.position.y = 0.2
	var dm := ShaderMaterial.new()
	dm.shader = PORTAL_SHADER
	dm.set_shader_parameter("col", portal_color)
	_disc.material_override = dm
	_disc.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(_disc)

	var parts := GPUParticles3D.new()
	parts.amount = 24
	parts.lifetime = 1.6
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	pm.emission_sphere_radius = 1.1
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 15.0
	pm.initial_velocity_min = 0.6
	pm.initial_velocity_max = 1.4
	pm.gravity = Vector3(0, 0.4, 0)
	pm.scale_min = 0.06
	pm.scale_max = 0.14
	pm.color = portal_color
	parts.process_material = pm
	var quad := QuadMesh.new()
	quad.size = Vector2(0.5, 0.5)
	var qm := StandardMaterial3D.new()
	qm.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	qm.vertex_color_use_as_albedo = true
	qm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	qm.emission_enabled = true
	qm.emission = portal_color
	qm.emission_energy_multiplier = 1.5
	quad.material = qm
	parts.draw_pass_1 = quad
	add_child(parts)

	_label = Label3D.new()
	_label.position = Vector3(0, 2.6, 0)
	_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_label.font_size = 72
	_label.outline_size = 14
	add_child(_label)

	Net.quest_changed.connect(_on_quest_changed)
	body_entered.connect(_on_body_entered)
	_refresh()


func _on_quest_changed(_stage: int) -> void:
	if target == "snow":
		_refresh()


func _refresh() -> void:
	if target == "snow":
		locked = Net.quest_stage < 2
	if locked:
		_ring_mat.albedo_color = Color(0.4, 0.4, 0.45)
		_ring_mat.emission_enabled = false
		_disc.visible = false
		_label.text = "%s\n(соберите %d монет)" % [label_text, Net.QUEST_COINS]
		_label.modulate = Color(0.8, 0.8, 0.8)
	else:
		_disc.visible = true
		_ring_mat.albedo_color = portal_color
		_ring_mat.emission_enabled = true
		_ring_mat.emission = portal_color
		_ring_mat.emission_energy_multiplier = 1.6
		_label.text = label_text
		_label.modulate = Color.WHITE


func _on_body_entered(body: Node3D) -> void:
	if locked:
		return
	if body is BearPlayer and body.is_local:
		Net.request_portal.rpc_id(1, target)
