class_name BearPlayer
extends CharacterBody3D
# Медведь-игрок: движение от третьего лица, прыжок с сальто, удар, здоровье.
# Локальный игрок управляется вводом, удалённые интерполируются по RPC.

const SPEED := 6.5
const ACCEL := 12.0
const JUMP_VELOCITY := 9.5
const GRAVITY := 22.0
const FLIP_TIME := 0.55

var peer_id := 1
var display_name := ""
var hue := 0.07
var is_local := false

var hearts := 3
var anim_state := "idle"

var _invuln := 0.0
var _attack_cd := 0.0
var _attack_anim := 0.0
var _send_tick := 0
var _anim_t := 0.0
var _spawn_pos := Vector3.ZERO
var _flip := 0.0
var _lean := 0.0
var _squash_t := 0.0
var _was_airborne := false

var _target_pos := Vector3.ZERO
var _target_yaw := 0.0
var _net_anim := "idle"
var _prev_net_anim := "idle"

var _visual: Node3D
var _flip_node: Node3D
var _cam_yaw: Node3D
var _spring: SpringArm3D
var _l_arm: Node3D
var _r_arm: Node3D
var _l_leg: Node3D
var _r_leg: Node3D
var _body: Node3D
var _swipe: MeshInstance3D
var _swipe_mat: StandardMaterial3D


func _ready() -> void:
	collision_layer = 2
	collision_mask = 1
	var cs := CollisionShape3D.new()
	var cap := CapsuleShape3D.new()
	cap.radius = 0.45
	cap.height = 1.6
	cs.shape = cap
	cs.position = Vector3(0, 0.85, 0)
	add_child(cs)

	_visual = Node3D.new()
	add_child(_visual)
	_build_bear()

	var label := Label3D.new()
	label.text = display_name
	label.position = Vector3(0, 2.3, 0)
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.font_size = 56
	label.outline_size = 10
	add_child(label)

	_target_pos = global_position
	_spawn_pos = global_position

	if is_local:
		add_to_group("local_player")
		_cam_yaw = Node3D.new()
		_cam_yaw.position = Vector3(0, 1.5, 0)
		add_child(_cam_yaw)
		_spring = SpringArm3D.new()
		_spring.spring_length = 5.5
		_spring.rotation.x = deg_to_rad(-16.0)
		_spring.add_excluded_object(get_rid())
		_cam_yaw.add_child(_spring)
		var cam := Camera3D.new()
		cam.fov = 73.0
		_spring.add_child(cam)
		cam.current = true


func place_at(pos: Vector3) -> void:
	global_position = pos
	_spawn_pos = pos
	_target_pos = pos
	velocity = Vector3.ZERO


func add_cam_input(rel: Vector2) -> void:
	if not is_local or _cam_yaw == null:
		return
	_cam_yaw.rotation.y -= rel.x
	_spring.rotation.x = clampf(_spring.rotation.x - rel.y, deg_to_rad(-75.0), deg_to_rad(25.0))


func _unhandled_input(event: InputEvent) -> void:
	if not is_local:
		return
	if event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		add_cam_input(event.relative * 0.003)
	elif event.is_action_pressed("ui_cancel"):
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	elif event is InputEventMouseButton and event.pressed:
		if Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
			if not DisplayServer.is_touchscreen_available():
				Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _physics_process(delta: float) -> void:
	if is_local:
		_local_move(delta)
		_enemy_interactions()
		_tick_timers(delta)
		if global_position.y < -30.0:
			_fall_respawn()
	else:
		global_position = global_position.lerp(_target_pos, minf(delta * 10.0, 1.0))
		if global_position.distance_to(_target_pos) > 12.0:
			global_position = _target_pos
		_visual.rotation.y = lerp_angle(_visual.rotation.y, _target_yaw, minf(delta * 10.0, 1.0))
		anim_state = _net_anim
		if _net_anim != _prev_net_anim:
			if _prev_net_anim == "jump" and _net_anim != "jump":
				_squash_t = 0.22
			if _net_anim == "attack":
				_attack_anim = 0.35
			_prev_net_anim = _net_anim
	_animate(delta)


func _local_move(delta: float) -> void:
	var v2 := Input.get_vector("move_left", "move_right", "move_forward", "move_back")
	var dir := Vector3.ZERO
	if _cam_yaw != null:
		dir = _cam_yaw.global_transform.basis * Vector3(v2.x, 0.0, v2.y)
	dir.y = 0.0
	if dir.length() > 0.01:
		dir = dir.normalized()

	velocity.x = lerpf(velocity.x, dir.x * SPEED, minf(ACCEL * delta, 1.0))
	velocity.z = lerpf(velocity.z, dir.z * SPEED, minf(ACCEL * delta, 1.0))

	if is_on_floor():
		if _was_airborne:
			_was_airborne = false
			_squash_t = 0.22
			Snd.play("land")
		if Input.is_action_just_pressed("jump"):
			velocity.y = JUMP_VELOCITY
			_flip = 0.001
			_was_airborne = true
			Snd.play("jump")
	else:
		_was_airborne = true
		velocity.y -= GRAVITY * delta

	move_and_slide()

	if dir.length() > 0.1 and _attack_anim <= 0.0:
		var yaw := atan2(dir.x, dir.z)
		_visual.rotation.y = lerp_angle(_visual.rotation.y, yaw, minf(10.0 * delta, 1.0))

	if Input.is_action_just_pressed("attack") and _attack_cd <= 0.0:
		_do_attack()

	if _attack_anim > 0.0:
		anim_state = "attack"
	elif not is_on_floor():
		anim_state = "jump"
	elif Vector2(velocity.x, velocity.z).length() > 0.8:
		anim_state = "run"
	else:
		anim_state = "idle"

	_send_tick += 1
	if _send_tick % 3 == 0 and Net.online:
		net_state.rpc(global_position, _visual.rotation.y, anim_state, _flip)


@rpc("authority", "call_remote", "unreliable")
func net_state(pos: Vector3, yaw: float, a: String, flip: float) -> void:
	_target_pos = pos
	_target_yaw = yaw
	_net_anim = a
	if not is_local:
		_flip = flip


func _tick_timers(delta: float) -> void:
	if _attack_cd > 0.0:
		_attack_cd -= delta
	if _invuln > 0.0:
		_invuln -= delta
		_visual.visible = fmod(_invuln, 0.2) > 0.1
	else:
		_visual.visible = true


func _do_attack() -> void:
	_attack_cd = 0.55
	_attack_anim = 0.35
	Snd.play("swing")
	# Рывок вперёд + спин-атака бьёт вокруг
	var fwd := Vector3(sin(_visual.rotation.y), 0.0, cos(_visual.rotation.y))
	velocity.x += fwd.x * 3.5
	velocity.z += fwd.z * 3.5
	for e in get_tree().get_nodes_in_group("enemies"):
		if not is_instance_valid(e):
			continue
		var to: Vector3 = e.global_position - global_position
		if to.length() < 2.5 and absf(to.y) < 1.6:
			Net.request_kill_enemy.rpc_id(1, Net.current_world, String(e.name))


func _enemy_interactions() -> void:
	for e in get_tree().get_nodes_in_group("enemies"):
		if not is_instance_valid(e) or not e.is_inside_tree():
			continue
		var dv: Vector3 = e.global_position - global_position
		if Vector2(dv.x, dv.z).length() < 0.95 and absf(dv.y) < 1.3:
			if velocity.y < -2.0 and global_position.y > e.global_position.y + 0.4:
				velocity.y = 8.0
				Net.request_kill_enemy.rpc_id(1, Net.current_world, String(e.name))
			elif _invuln <= 0.0:
				_take_damage(e.global_position)


func _take_damage(from: Vector3) -> void:
	hearts -= 1
	_invuln = 1.5
	Snd.play("hurt")
	var push := global_position - from
	push.y = 0.0
	if push.length() < 0.01:
		push = Vector3.BACK
	velocity = push.normalized() * 8.0
	velocity.y = 6.0
	if hearts <= 0:
		hearts = 3
		_invuln = 2.0
		place_at(_spawn_pos)


func _fall_respawn() -> void:
	hearts -= 1
	if hearts <= 0:
		hearts = 3
	_invuln = 2.0
	Snd.play("hurt")
	place_at(_spawn_pos)


# ---------- Анимация ----------


func _animate(delta: float) -> void:
	# Сальто в прыжке
	if is_local and _flip > 0.0:
		_flip += delta * TAU / FLIP_TIME
		if _flip >= TAU:
			_flip = 0.0

	# Наклон корпуса в беге
	var lean_target := 0.16 if anim_state == "run" else 0.0
	_lean = lerpf(_lean, lean_target, minf(delta * 8.0, 1.0))
	_flip_node.rotation.x = _flip + _lean

	# Сквош при приземлении
	var squash_target := Vector3.ONE
	if _squash_t > 0.0:
		_squash_t -= delta
		squash_target = Vector3(1.15, 0.78, 1.15)
	_flip_node.scale = _flip_node.scale.lerp(squash_target, minf(delta * 14.0, 1.0))

	# Спин-атака: полный оборот + расходящаяся ударная волна
	if _attack_anim > 0.0:
		_attack_anim -= delta
		var pr := 1.0 - maxf(_attack_anim, 0.0) / 0.35
		_visual.rotation.y += delta * TAU / 0.35
		_swipe.visible = true
		_swipe.scale = Vector3(0.5 + 1.9 * pr, 1.0, 0.5 + 1.9 * pr)
		_swipe_mat.albedo_color = Color(1.0, 1.0, 0.9, 0.55 * (1.0 - pr))
	else:
		_swipe.visible = false

	var run_speed := 11.0 if anim_state == "run" else 2.0
	_anim_t += delta * run_speed
	var la := 0.0
	var ra := 0.0
	var laz := 0.0
	var raz := 0.0
	var ll := 0.0
	var rl := 0.0
	var bob := 0.0
	match anim_state:
		"run":
			ll = sin(_anim_t) * 0.95
			rl = -sin(_anim_t) * 0.95
			la = -sin(_anim_t) * 0.75
			ra = sin(_anim_t) * 0.75
			bob = absf(sin(_anim_t)) * 0.07
		"jump":
			ll = -0.6
			rl = 0.7
			la = -2.5
			ra = -2.5
		"attack":
			laz = 1.4
			raz = -1.4
		_:
			la = sin(_anim_t) * 0.08
			ra = -sin(_anim_t) * 0.08
	var k := minf(delta * 14.0, 1.0)
	_l_arm.rotation.x = lerpf(_l_arm.rotation.x, la, k)
	_r_arm.rotation.x = lerpf(_r_arm.rotation.x, ra, k)
	_l_arm.rotation.z = lerpf(_l_arm.rotation.z, laz, minf(delta * 20.0, 1.0))
	_r_arm.rotation.z = lerpf(_r_arm.rotation.z, raz, minf(delta * 20.0, 1.0))
	_l_leg.rotation.x = lerpf(_l_leg.rotation.x, ll, k)
	_r_leg.rotation.x = lerpf(_r_leg.rotation.x, rl, k)
	_body.position.y = lerpf(_body.position.y, -1.0 + bob, k)


# ---------- Внешний вид ----------


func _fur(col: Color, rough := 0.8) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = col
	m.roughness = rough
	m.rim_enabled = true
	m.rim = 0.3
	m.rim_tint = 0.5
	return m


func _ball(parent: Node3D, pos: Vector3, s: Vector3, m: StandardMaterial3D) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = 0.5
	mesh.height = 1.0
	mi.mesh = mesh
	mi.position = pos
	mi.scale = s
	mi.material_override = m
	parent.add_child(mi)
	return mi


func _limb(
	parent: Node3D, pivot_pos: Vector3, length: float, radius: float, m: StandardMaterial3D,
	paw_m: StandardMaterial3D
) -> Node3D:
	var pivot := Node3D.new()
	pivot.position = pivot_pos
	parent.add_child(pivot)
	var mi := MeshInstance3D.new()
	var mesh := CapsuleMesh.new()
	mesh.radius = radius
	mesh.height = length
	mi.mesh = mesh
	mi.position = Vector3(0, -length * 0.5 + radius * 0.5, 0)
	mi.material_override = m
	pivot.add_child(mi)
	_ball(pivot, Vector3(0, -length * 0.72, 0.07), Vector3(0.22, 0.13, 0.28), paw_m)
	return pivot


func _build_bear() -> void:
	var fur := _fur(Color.from_hsv(hue, 0.5, 0.55))
	var fur_light := _fur(Color.from_hsv(hue, 0.32, 0.8))
	var dark := _fur(Color(0.1, 0.08, 0.07), 0.6)
	var white := _fur(Color(0.95, 0.95, 0.95), 0.5)

	_flip_node = Node3D.new()
	_flip_node.position = Vector3(0, 1.0, 0)
	_visual.add_child(_flip_node)
	_body = Node3D.new()
	_body.position = Vector3(0, -1.0, 0)
	_flip_node.add_child(_body)

	# Туловище
	_ball(_body, Vector3(0, 0.78, 0), Vector3(1.0, 1.15, 0.9), fur)
	_ball(_body, Vector3(0, 0.76, 0.27), Vector3(0.6, 0.8, 0.45), fur_light)
	_ball(_body, Vector3(0, 0.68, -0.42), Vector3(0.26, 0.26, 0.26), fur_light)

	# Голова
	_ball(_body, Vector3(0, 1.5, 0), Vector3(0.85, 0.8, 0.82), fur)
	_ball(_body, Vector3(0, 1.4, 0.34), Vector3(0.4, 0.3, 0.32), fur_light)
	_ball(_body, Vector3(0, 1.47, 0.49), Vector3(0.13, 0.1, 0.1), dark)
	_ball(_body, Vector3(-0.15, 1.58, 0.33), Vector3(0.11, 0.13, 0.06), white)
	_ball(_body, Vector3(0.15, 1.58, 0.33), Vector3(0.11, 0.13, 0.06), white)
	_ball(_body, Vector3(-0.15, 1.58, 0.37), Vector3(0.05, 0.07, 0.03), dark)
	_ball(_body, Vector3(0.15, 1.58, 0.37), Vector3(0.05, 0.07, 0.03), dark)
	_ball(_body, Vector3(-0.28, 1.85, -0.02), Vector3(0.26, 0.26, 0.15), fur)
	_ball(_body, Vector3(0.28, 1.85, -0.02), Vector3(0.26, 0.26, 0.15), fur)
	_ball(_body, Vector3(-0.28, 1.86, 0.03), Vector3(0.13, 0.13, 0.08), fur_light)
	_ball(_body, Vector3(0.28, 1.86, 0.03), Vector3(0.13, 0.13, 0.08), fur_light)

	# Шарф — яркий цвет игрока
	var scarf := MeshInstance3D.new()
	var torus := TorusMesh.new()
	torus.inner_radius = 0.18
	torus.outer_radius = 0.42
	scarf.mesh = torus
	scarf.position = Vector3(0, 1.16, 0)
	scarf.scale = Vector3(1.0, 0.7, 1.0)
	var sm := StandardMaterial3D.new()
	sm.albedo_color = Color.from_hsv(hue, 0.85, 0.95)
	sm.roughness = 0.7
	sm.emission_enabled = true
	sm.emission = Color.from_hsv(hue, 0.85, 0.6)
	sm.emission_energy_multiplier = 0.4
	scarf.material_override = sm
	_body.add_child(scarf)

	# Конечности
	_l_arm = _limb(_body, Vector3(-0.52, 1.06, 0), 0.52, 0.13, fur, fur_light)
	_r_arm = _limb(_body, Vector3(0.52, 1.06, 0), 0.52, 0.13, fur, fur_light)
	_l_leg = _limb(_body, Vector3(-0.23, 0.5, 0), 0.5, 0.15, fur, fur_light)
	_r_leg = _limb(_body, Vector3(0.23, 0.5, 0), 0.5, 0.15, fur, fur_light)

	# Кольцо ударной волны спин-атаки
	_swipe = MeshInstance3D.new()
	var swipe_torus := TorusMesh.new()
	swipe_torus.inner_radius = 0.85
	swipe_torus.outer_radius = 1.05
	_swipe.mesh = swipe_torus
	_swipe.position = Vector3(0, 0.9, 0)
	_swipe.visible = false
	_swipe_mat = StandardMaterial3D.new()
	_swipe_mat.albedo_color = Color(1, 1, 0.9, 0.5)
	_swipe_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_swipe_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_swipe_mat.emission_enabled = true
	_swipe_mat.emission = Color(1.0, 0.95, 0.6)
	_swipe_mat.emission_energy_multiplier = 1.5
	_swipe.material_override = _swipe_mat
	_swipe.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_visual.add_child(_swipe)
