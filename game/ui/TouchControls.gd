class_name TouchControls
extends Control
# Сенсорное управление: виртуальный джойстик слева, кнопки прыжка и удара
# справа, свободная зона справа вращает камеру. Виден только на телефоне.

const JOY_RADIUS := 110.0
const KNOB_RADIUS := 48.0

var _joy_touch := -1
var _cam_touch := -1
var _jump_touch := -1
var _attack_touch := -1
var _knob_offset := Vector2.ZERO


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = DisplayServer.is_touchscreen_available() or OS.has_feature("mobile")


func _process(_delta: float) -> void:
	if visible:
		queue_redraw()


func _joy_center() -> Vector2:
	return Vector2(200, size.y - 200)


func _jump_center() -> Vector2:
	return Vector2(size.x - 160, size.y - 170)


func _attack_center() -> Vector2:
	return Vector2(size.x - 340, size.y - 120)


func _input(event: InputEvent) -> void:
	if not visible:
		return
	if event is InputEventScreenTouch:
		if event.pressed:
			_assign_touch(event.index, event.position)
		else:
			_release_touch(event.index)
	elif event is InputEventScreenDrag:
		if event.index == _joy_touch:
			_update_joy(event.position)
		elif event.index == _cam_touch:
			var p := get_tree().get_first_node_in_group("local_player")
			if p is BearPlayer:
				p.add_cam_input(event.relative * 0.004)


func _assign_touch(index: int, pos: Vector2) -> void:
	if pos.distance_to(_jump_center()) < 100.0 and _jump_touch < 0:
		_jump_touch = index
		Input.action_press("jump")
	elif pos.distance_to(_attack_center()) < 80.0 and _attack_touch < 0:
		_attack_touch = index
		Input.action_press("attack")
	elif pos.x < size.x * 0.45 and pos.y > size.y * 0.3 and _joy_touch < 0:
		_joy_touch = index
		_update_joy(pos)
	elif _cam_touch < 0:
		_cam_touch = index


func _release_touch(index: int) -> void:
	if index == _joy_touch:
		_joy_touch = -1
		_knob_offset = Vector2.ZERO
		Input.action_release("move_left")
		Input.action_release("move_right")
		Input.action_release("move_forward")
		Input.action_release("move_back")
	elif index == _cam_touch:
		_cam_touch = -1
	elif index == _jump_touch:
		_jump_touch = -1
		Input.action_release("jump")
	elif index == _attack_touch:
		_attack_touch = -1
		Input.action_release("attack")


func _update_joy(pos: Vector2) -> void:
	var v := pos - _joy_center()
	if v.length() > JOY_RADIUS:
		v = v.normalized() * JOY_RADIUS
	_knob_offset = v
	var s := v / JOY_RADIUS
	Input.action_press("move_right", clampf(s.x, 0.0, 1.0))
	Input.action_press("move_left", clampf(-s.x, 0.0, 1.0))
	Input.action_press("move_back", clampf(s.y, 0.0, 1.0))
	Input.action_press("move_forward", clampf(-s.y, 0.0, 1.0))


func _draw() -> void:
	if not visible:
		return
	var jc := _joy_center()
	draw_circle(jc, JOY_RADIUS, Color(1, 1, 1, 0.12))
	draw_circle(jc, JOY_RADIUS, Color(1, 1, 1, 0.25), false, 3.0)
	draw_circle(jc + _knob_offset, KNOB_RADIUS, Color(1, 1, 1, 0.35))

	var font := get_theme_default_font()
	var jmp := _jump_center()
	draw_circle(jmp, 85.0, Color(0.4, 0.9, 0.5, 0.22))
	draw_circle(jmp, 85.0, Color(0.4, 0.9, 0.5, 0.5), false, 3.0)
	draw_string(
		font,
		jmp + Vector2(-70, 8),
		"Прыжок",
		HORIZONTAL_ALIGNMENT_CENTER,
		140,
		24,
		Color(1, 1, 1, 0.85)
	)

	var atk := _attack_center()
	draw_circle(atk, 62.0, Color(1.0, 0.55, 0.35, 0.22))
	draw_circle(atk, 62.0, Color(1.0, 0.55, 0.35, 0.5), false, 3.0)
	draw_string(
		font,
		atk + Vector2(-50, 8),
		"Удар",
		HORIZONTAL_ALIGNMENT_CENTER,
		100,
		24,
		Color(1, 1, 1, 0.85)
	)
