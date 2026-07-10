class_name Hud
extends CanvasLayer
# Игровой интерфейс: монеты, жизни, задание, диалоги, экран победы,
# а также сенсорное управление на телефоне.

var _coins_label: Label
var _hearts_label: Label
var _quest_label: Label
var _info_label: Label
var _dialog_panel: PanelContainer
var _dialog_label: Label
var _loading: ColorRect
var _victory: ColorRect


func _ready() -> void:
	add_to_group("hud")
	var root := Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)

	var touch := TouchControls.new()
	root.add_child(touch)

	_coins_label = _label(root, 30, Color(1.0, 0.9, 0.3))
	_coins_label.position = Vector2(20, 14)
	_hearts_label = _label(root, 26, Color(1.0, 0.45, 0.45))
	_hearts_label.position = Vector2(20, 56)

	_quest_label = _label(root, 20, Color.WHITE)
	_quest_label.set_anchors_preset(Control.PRESET_TOP_WIDE)
	_quest_label.offset_left = 280
	_quest_label.offset_right = -280
	_quest_label.offset_top = 12
	_quest_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_quest_label.autowrap_mode = TextServer.AUTOWRAP_WORD

	_info_label = _label(root, 18, Color(0.85, 0.9, 1.0))
	_info_label.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	_info_label.offset_left = -320
	_info_label.offset_right = -16
	_info_label.offset_top = 52
	_info_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

	var menu_btn := Button.new()
	menu_btn.text = "Меню"
	menu_btn.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	menu_btn.offset_left = -110
	menu_btn.offset_right = -14
	menu_btn.offset_top = 10
	menu_btn.offset_bottom = 46
	menu_btn.pressed.connect(_on_menu_pressed)
	root.add_child(menu_btn)

	_dialog_panel = PanelContainer.new()
	_dialog_panel.set_anchors_preset(Control.PRESET_CENTER_BOTTOM)
	_dialog_panel.offset_left = -320
	_dialog_panel.offset_right = 320
	_dialog_panel.offset_top = -150
	_dialog_panel.offset_bottom = -70
	_dialog_panel.visible = false
	root.add_child(_dialog_panel)
	_dialog_label = Label.new()
	_dialog_label.add_theme_font_size_override("font_size", 22)
	_dialog_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_dialog_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_dialog_label.autowrap_mode = TextServer.AUTOWRAP_WORD
	_dialog_panel.add_child(_dialog_label)

	_loading = ColorRect.new()
	_loading.color = Color(0.06, 0.08, 0.14, 0.9)
	_loading.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_loading.visible = false
	root.add_child(_loading)
	var load_label := _label(_loading, 34, Color.WHITE)
	load_label.text = "Подключение..."
	load_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	load_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	load_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER

	_victory = ColorRect.new()
	_victory.color = Color(0.05, 0.05, 0.12, 0.75)
	_victory.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_victory.visible = false
	root.add_child(_victory)
	var vbox := VBoxContainer.new()
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	vbox.alignment = BoxContainer.ALIGNMENT_CENTER
	_victory.add_child(vbox)
	var v1 := Label.new()
	v1.text = "ПОБЕДА!"
	v1.add_theme_font_size_override("font_size", 72)
	v1.add_theme_color_override("font_color", Color(1.0, 0.85, 0.2))
	v1.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(v1)
	var v2 := Label.new()
	v2.text = "Вы покорили Снежные вершины!\nВозвращаемся в деревню..."
	v2.add_theme_font_size_override("font_size", 28)
	v2.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(v2)

	Net.victory_happened.connect(_on_victory)
	Net.world_changed.connect(_on_world_changed)
	Net.quest_changed.connect(_on_quest_changed_snd)


func _on_quest_changed_snd(_stage: int) -> void:
	Snd.play("quest")


func _label(parent: Node, size: int, color: Color) -> Label:
	var l := Label.new()
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	l.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.7))
	l.add_theme_constant_override("outline_size", 6)
	parent.add_child(l)
	return l


func _process(_delta: float) -> void:
	_coins_label.text = "Монеты: %d" % Net.coins_total
	var p := get_tree().get_first_node_in_group("local_player")
	if p is BearPlayer:
		_hearts_label.text = "Жизни: %d" % p.hearts
	_quest_label.text = _quest_text()
	var info := "Игроки: %d" % maxi(Net.players.size(), 1)
	if Net.online and Net.is_host:
		info += "\nВаш IP: %s" % Net.get_local_ip_text()
	_info_label.text = info


func _quest_text() -> String:
	if Net.victory_reached:
		return "Победа! Вы покорили вершину!"
	match Net.quest_stage:
		2:
			return "Портал открыт! Доберитесь до звезды на Снежной вершине"
		1:
			return "Задание: соберите монеты (%d/%d)" % [Net.coins_total, Net.QUEST_COINS]
		_:
			return "Подойдите к старейшине в деревне"


func show_dialog(text: String) -> void:
	_dialog_label.text = text
	_dialog_panel.visible = true


func hide_dialog() -> void:
	_dialog_panel.visible = false


func show_loading(v: bool) -> void:
	_loading.visible = v


func _on_victory() -> void:
	Snd.play("victory")
	_victory.visible = true


func _on_world_changed(_world_id: String) -> void:
	_victory.visible = false
	hide_dialog()


func _on_menu_pressed() -> void:
	Net.leave_to_menu("")
