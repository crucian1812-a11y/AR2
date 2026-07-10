extends Node3D
# Главная игровая сцена: держит текущий мир и всех игроков,
# управляет сменой миров и серверной симуляцией врагов.

const PLAYER_SCRIPT := preload("res://scenes/Player.gd")

const WORLD_SCRIPTS := {
	"hub": preload("res://worlds/Hub.gd"),
	"meadow": preload("res://worlds/Meadow.gd"),
	"snow": preload("res://worlds/Snow.gd"),
}

var world_holder: Node3D
var players_node: Node3D
var hud: Hud
var current_world_node: WorldBase = null

var _enemy_send_accum := 0.0
var _got_world := false


func _ready() -> void:
	world_holder = Node3D.new()
	world_holder.name = "WorldHolder"
	add_child(world_holder)
	players_node = Node3D.new()
	players_node.name = "Players"
	add_child(players_node)

	hud = Hud.new()
	add_child(hud)

	Net.world_changed.connect(_load_world)
	Net.player_list_changed.connect(_refresh_players)
	Net.coin_removed.connect(_on_coin_removed)
	Net.enemy_removed.connect(_on_enemy_removed)
	Net.enemy_states.connect(_on_enemy_states)

	if Net.is_host:
		_load_world(Net.current_world)
		_refresh_players()
	else:
		hud.show_loading(true)
		Net.notify_game_scene_ready()


func _physics_process(delta: float) -> void:
	if Net.is_host and current_world_node != null:
		current_world_node.simulate_enemies(delta)
		if Net.online:
			_enemy_send_accum += delta
			if _enemy_send_accum >= 0.1:
				_enemy_send_accum = 0.0
				Net.enemies_state.rpc(Net.current_world, current_world_node.gather_enemy_states())


func _load_world(world_id: String) -> void:
	if not WORLD_SCRIPTS.has(world_id):
		return
	_got_world = true
	hud.show_loading(false)
	for c in world_holder.get_children():
		world_holder.remove_child(c)
		c.queue_free()
	var w: WorldBase = WORLD_SCRIPTS[world_id].new()
	w.name = "World"
	world_holder.add_child(w)
	current_world_node = w
	_apply_world_state(world_id, w)
	_place_players_at_spawn(w)


func _apply_world_state(world_id: String, w: WorldBase) -> void:
	if not Net.world_state.has(world_id):
		return
	var ws: Dictionary = Net.world_state[world_id]
	for coin_name in ws.get("collected", {}):
		var c := w.coins_node.get_node_or_null(NodePath(str(coin_name)))
		if c != null:
			c.queue_free()
	for enemy_name in ws.get("killed", {}):
		var e := w.enemies_node.get_node_or_null(NodePath(str(enemy_name)))
		if e != null:
			if e is Enemy3:
				e.remove_from_group("enemies")
			e.queue_free()


func _place_players_at_spawn(w: WorldBase) -> void:
	var idx := 0
	for p in players_node.get_children():
		if p is BearPlayer:
			var off := Vector3((idx % 4) * 1.4 - 2.1, 0, (idx / 4) * 1.4)
			p.place_at(w.spawn_point + off)
			idx += 1


func _refresh_players() -> void:
	if not _got_world and not Net.is_host:
		# Мир ещё не пришёл — игроков расставим после его загрузки.
		pass
	for id in Net.players:
		var key := str(id)
		if players_node.has_node(key):
			continue
		var p: BearPlayer = PLAYER_SCRIPT.new()
		p.name = key
		p.peer_id = int(id)
		p.display_name = str(Net.players[id]["name"])
		p.hue = float(Net.players[id]["hue"])
		p.is_local = int(id) == Net.my_id
		p.set_multiplayer_authority(int(id))
		players_node.add_child(p)
		if current_world_node != null:
			var idx := players_node.get_child_count() - 1
			var off := Vector3((idx % 4) * 1.4 - 2.1, 0, (idx / 4) * 1.4)
			p.place_at(current_world_node.spawn_point + off)
	for c in players_node.get_children():
		if not Net.players.has(str(c.name).to_int()):
			c.queue_free()


func _on_coin_removed(world_id: String, coin_name: String) -> void:
	if world_id != Net.current_world or current_world_node == null:
		return
	var c := current_world_node.coins_node.get_node_or_null(NodePath(coin_name))
	if c is Coin:
		c.collect_effect()


func _on_enemy_removed(world_id: String, enemy_name: String) -> void:
	if world_id != Net.current_world or current_world_node == null:
		return
	var e := current_world_node.enemies_node.get_node_or_null(NodePath(enemy_name))
	if e is Enemy3:
		e.die_effect()


func _on_enemy_states(data: Array) -> void:
	if current_world_node != null:
		current_world_node.apply_enemy_states(data)
