extends Control
# Главное меню: имя игрока, одиночная игра, создание LAN-игры,
# поиск и подключение к играм в локальной сети.

var _name_edit: LineEdit
var _status: Label
var _main_box: VBoxContainer
var _join_box: VBoxContainer
var _servers_box: VBoxContainer
var _ip_edit: LineEdit


func _ready() -> void:
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	Net.join_failed.connect(_on_join_failed)
	Net.server_list_updated.connect(_refresh_servers)

	var bg := ColorRect.new()
	bg.color = Color(0.09, 0.12, 0.2)
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(bg)

	var layout := VBoxContainer.new()
	layout.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	layout.alignment = BoxContainer.ALIGNMENT_CENTER
	layout.add_theme_constant_override("separation", 10)
	add_child(layout)

	var title := Label.new()
	title.text = "МЕДВЕЖЬИ ПРИКЛЮЧЕНИЯ"
	title.add_theme_font_size_override("font_size", 54)
	title.add_theme_color_override("font_color", Color(1.0, 0.82, 0.35))
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	layout.add_child(title)

	var subtitle := Label.new()
	subtitle.text = "3D-платформер · мультиплеер по Wi-Fi"
	subtitle.add_theme_font_size_override("font_size", 22)
	subtitle.add_theme_color_override("font_color", Color(0.7, 0.78, 0.9))
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	layout.add_child(subtitle)

	layout.add_child(_spacer(24))

	_main_box = VBoxContainer.new()
	_main_box.alignment = BoxContainer.ALIGNMENT_CENTER
	_main_box.add_theme_constant_override("separation", 12)
	layout.add_child(_main_box)

	var name_label := Label.new()
	name_label.text = "Ваше имя:"
	name_label.add_theme_font_size_override("font_size", 20)
	name_label.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_main_box.add_child(name_label)

	_name_edit = LineEdit.new()
	_name_edit.text = Net.player_name
	_name_edit.max_length = 16
	_name_edit.custom_minimum_size = Vector2(380, 52)
	_name_edit.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_name_edit.alignment = HORIZONTAL_ALIGNMENT_CENTER
	_main_box.add_child(_name_edit)

	_main_box.add_child(_menu_button("Играть одному", _on_solo))
	_main_box.add_child(_menu_button("Создать игру (по Wi-Fi)", _on_host))
	_main_box.add_child(_menu_button("Присоединиться к игре", _on_join_open))

	_join_box = VBoxContainer.new()
	_join_box.alignment = BoxContainer.ALIGNMENT_CENTER
	_join_box.add_theme_constant_override("separation", 12)
	_join_box.visible = false
	layout.add_child(_join_box)

	var search_label := Label.new()
	search_label.text = "Поиск игр в вашей сети..."
	search_label.add_theme_font_size_override("font_size", 22)
	search_label.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_join_box.add_child(search_label)

	_servers_box = VBoxContainer.new()
	_servers_box.alignment = BoxContainer.ALIGNMENT_CENTER
	_servers_box.add_theme_constant_override("separation", 8)
	_join_box.add_child(_servers_box)

	var ip_row := HBoxContainer.new()
	ip_row.alignment = BoxContainer.ALIGNMENT_CENTER
	ip_row.add_theme_constant_override("separation", 8)
	ip_row.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_join_box.add_child(ip_row)
	_ip_edit = LineEdit.new()
	_ip_edit.placeholder_text = "IP хоста, напр. 192.168.1.42"
	_ip_edit.custom_minimum_size = Vector2(300, 48)
	ip_row.add_child(_ip_edit)
	var connect_btn := Button.new()
	connect_btn.text = "Подключиться"
	connect_btn.custom_minimum_size = Vector2(160, 48)
	connect_btn.pressed.connect(_on_manual_join)
	ip_row.add_child(connect_btn)

	_join_box.add_child(_menu_button("Назад", _on_join_back))

	_status = Label.new()
	_status.text = Net.status_message
	Net.status_message = ""
	_status.add_theme_font_size_override("font_size", 20)
	_status.add_theme_color_override("font_color", Color(1.0, 0.6, 0.5))
	_status.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	layout.add_child(_status)

	var version := Label.new()
	version.text = "v1.0"
	version.add_theme_font_size_override("font_size", 14)
	version.add_theme_color_override("font_color", Color(0.5, 0.55, 0.65))
	version.set_anchors_preset(Control.PRESET_BOTTOM_RIGHT)
	version.offset_left = -80
	version.offset_top = -34
	add_child(version)


func _spacer(h: float) -> Control:
	var s := Control.new()
	s.custom_minimum_size = Vector2(0, h)
	return s


func _menu_button(text: String, handler: Callable) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(380, 56)
	b.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	b.add_theme_font_size_override("font_size", 24)
	b.pressed.connect(handler)
	return b


func _save_name() -> void:
	var n := _name_edit.text.strip_edges()
	if n != "":
		Net.player_name = n


func _on_solo() -> void:
	_save_name()
	Net.start_solo()


func _on_host() -> void:
	_save_name()
	if not Net.start_host():
		_status.text = Net.status_message


func _on_join_open() -> void:
	_save_name()
	_main_box.visible = false
	_join_box.visible = true
	_status.text = ""
	Net.start_browse()
	_refresh_servers()


func _on_join_back() -> void:
	Net.stop_browse()
	_join_box.visible = false
	_main_box.visible = true


func _on_manual_join() -> void:
	_join_to(_ip_edit.text)


func _join_to(ip: String) -> void:
	Net.stop_browse()
	if Net.start_join(ip):
		_status.text = "Подключение к %s..." % ip.strip_edges()
	else:
		_status.text = Net.status_message


func _on_join_failed(message: String) -> void:
	_status.text = message


func _refresh_servers() -> void:
	for c in _servers_box.get_children():
		c.queue_free()
	if Net.found_servers.is_empty():
		var l := Label.new()
		l.text = "(пока ничего не найдено — хост должен создать игру)"
		l.add_theme_font_size_override("font_size", 16)
		l.add_theme_color_override("font_color", Color(0.6, 0.65, 0.75))
		_servers_box.add_child(l)
		return
	for ip in Net.found_servers:
		var info: Dictionary = Net.found_servers[ip]
		var b := Button.new()
		b.text = "%s — %s (игроков: %d)" % [str(info["name"]), ip, int(info["players"])]
		b.custom_minimum_size = Vector2(460, 48)
		b.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		var ip_copy := str(ip)
		b.pressed.connect(func() -> void: _join_to(ip_copy))
		_servers_box.add_child(b)
