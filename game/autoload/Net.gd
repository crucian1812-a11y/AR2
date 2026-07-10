extends Node
# Сетевой менеджер: ENet-сервер/клиент + обнаружение хостов в локальной сети
# по UDP-broadcast. Состояние игры (монеты, задание, миры) хранится на хосте
# и рассылается всем игрокам через RPC.

const GAME_PORT := 8910
const DISCOVERY_PORT := 8911
const DISCOVERY_MSG := "BEAR_DISCOVER"
const QUEST_COINS := 15
const MAX_PLAYERS := 8

signal player_list_changed
signal world_changed(world_id: String)
signal coins_changed(total: int)
signal quest_changed(stage: int)
signal coin_removed(world_id: String, coin_name: String)
signal enemy_removed(world_id: String, enemy_name: String)
signal enemy_states(data: Array)
signal victory_happened
signal join_failed(message: String)
signal server_list_updated

var player_name := "Медведь"
var players: Dictionary = {}
var my_id := 1
var is_host := true
var online := false
var current_world := "hub"
var coins_total := 0
var quest_stage := 0
var victory_reached := false
var world_state: Dictionary = {}
var status_message := ""
var found_servers: Dictionary = {}

var _disc_server: PacketPeerUDP = null
var _disc_client: PacketPeerUDP = null
var _browse_accum := 0.0


func _ready() -> void:
	player_name = "Медведь-%d" % randi_range(1, 99)
	multiplayer.peer_disconnected.connect(_on_peer_disconnected)
	multiplayer.connected_to_server.connect(_on_connected_ok)
	multiplayer.connection_failed.connect(_on_connection_failed)
	multiplayer.server_disconnected.connect(_on_server_disconnected)


func _process(delta: float) -> void:
	_poll_discovery_server()
	_poll_browse(delta)


func reset_session() -> void:
	players.clear()
	coins_total = 0
	quest_stage = 0
	victory_reached = false
	world_state.clear()
	current_world = "hub"
	my_id = 1
	is_host = true
	online = false


func start_solo() -> void:
	reset_session()
	_reset_peer()
	players[1] = _make_info(player_name, 0)
	_goto_game()


func start_host() -> bool:
	reset_session()
	_reset_peer()
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_server(GAME_PORT, MAX_PLAYERS)
	if err != OK:
		status_message = "Не удалось создать игру (порт %d занят?)" % GAME_PORT
		return false
	multiplayer.multiplayer_peer = peer
	online = true
	is_host = true
	players[1] = _make_info(player_name, 0)
	_start_discovery_server()
	_goto_game()
	return true


func start_join(ip: String) -> bool:
	reset_session()
	_reset_peer()
	if ip.strip_edges() == "":
		return false
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_client(ip.strip_edges(), GAME_PORT)
	if err != OK:
		status_message = "Неверный адрес: %s" % ip
		return false
	multiplayer.multiplayer_peer = peer
	online = true
	is_host = false
	return true


func leave_to_menu(msg: String) -> void:
	status_message = msg
	_stop_discovery_server()
	stop_browse()
	_reset_peer()
	reset_session()
	get_tree().change_scene_to_file("res://scenes/Menu.tscn")


func notify_game_scene_ready() -> void:
	if online and not is_host:
		register_player.rpc_id(1, player_name)


func get_local_ip_text() -> String:
	var ips: Array[String] = []
	for a in IP.get_local_addresses():
		if a.contains(".") and not a.begins_with("127."):
			ips.append(a)
	return ", ".join(ips)


func _goto_game() -> void:
	get_tree().change_scene_to_file("res://scenes/Main.tscn")


func _reset_peer() -> void:
	if multiplayer.multiplayer_peer != null:
		multiplayer.multiplayer_peer.close()
	multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()


func _make_info(pname: String, idx: int) -> Dictionary:
	return {"name": pname.substr(0, 16), "hue": fposmod(0.07 + idx * 0.18, 1.0)}


func _on_connected_ok() -> void:
	my_id = multiplayer.get_unique_id()
	_goto_game()


func _on_connection_failed() -> void:
	status_message = "Не удалось подключиться к хосту"
	_reset_peer()
	online = false
	is_host = true
	join_failed.emit(status_message)


func _on_server_disconnected() -> void:
	leave_to_menu("Хост отключился")


func _on_peer_disconnected(id: int) -> void:
	if is_host and players.has(id):
		players.erase(id)
		sync_players.rpc(players)


func _sender_id() -> int:
	var s := multiplayer.get_remote_sender_id()
	return 1 if s == 0 else s


func _ws(world_id: String) -> Dictionary:
	if not world_state.has(world_id):
		world_state[world_id] = {"collected": {}, "killed": {}}
	return world_state[world_id]


# ---------- RPC: регистрация и синхронизация ----------


@rpc("any_peer", "call_local", "reliable")
func register_player(pname: String) -> void:
	if not is_host:
		return
	var sender := _sender_id()
	players[sender] = _make_info(pname, players.size())
	sync_players.rpc(players)
	if sender != 1:
		sync_full.rpc_id(
			sender, current_world, coins_total, quest_stage, world_state, victory_reached
		)


@rpc("authority", "call_local", "reliable")
func sync_players(d: Dictionary) -> void:
	players = d
	player_list_changed.emit()


@rpc("authority", "call_remote", "reliable")
func sync_full(world: String, coins: int, quest: int, wstate: Dictionary, vict: bool) -> void:
	current_world = world
	coins_total = coins
	quest_stage = quest
	world_state = wstate
	victory_reached = vict
	world_changed.emit(world)
	coins_changed.emit(coins)
	quest_changed.emit(quest)


@rpc("authority", "call_local", "reliable")
func set_world(world_id: String) -> void:
	current_world = world_id
	world_changed.emit(world_id)


# ---------- RPC: игровые запросы (клиент -> хост) ----------


@rpc("any_peer", "call_local", "reliable")
func request_portal(target: String) -> void:
	if not is_host:
		return
	if target == current_world:
		return
	if target == "snow" and quest_stage < 2:
		return
	set_world.rpc(target)


@rpc("any_peer", "call_local", "reliable")
func request_quest() -> void:
	if not is_host:
		return
	if quest_stage == 0:
		quest_stage = 1
		quest_update.rpc(1)
		_check_quest()


@rpc("any_peer", "call_local", "reliable")
func request_collect(world_id: String, coin_name: String) -> void:
	if not is_host:
		return
	if world_id != current_world:
		return
	var ws := _ws(world_id)
	if ws["collected"].has(coin_name):
		return
	ws["collected"][coin_name] = true
	coins_total += 1
	coin_collected.rpc(world_id, coin_name, coins_total)
	_check_quest()


@rpc("any_peer", "call_local", "reliable")
func request_kill_enemy(world_id: String, enemy_name: String) -> void:
	if not is_host:
		return
	if world_id != current_world:
		return
	var ws := _ws(world_id)
	if ws["killed"].has(enemy_name):
		return
	ws["killed"][enemy_name] = true
	enemy_killed.rpc(world_id, enemy_name)


@rpc("any_peer", "call_local", "reliable")
func request_victory() -> void:
	if not is_host:
		return
	if victory_reached or current_world != "snow":
		return
	victory_now.rpc()
	get_tree().create_timer(7.0).timeout.connect(_return_to_hub)


func _return_to_hub() -> void:
	if is_host:
		set_world.rpc("hub")


func _check_quest() -> void:
	if quest_stage == 1 and coins_total >= QUEST_COINS:
		quest_stage = 2
		quest_update.rpc(2)


# ---------- RPC: оповещения (хост -> все) ----------


@rpc("authority", "call_local", "reliable")
func quest_update(stage: int) -> void:
	quest_stage = stage
	quest_changed.emit(stage)


@rpc("authority", "call_local", "reliable")
func coin_collected(world_id: String, coin_name: String, total: int) -> void:
	coins_total = total
	_ws(world_id)["collected"][coin_name] = true
	coins_changed.emit(total)
	coin_removed.emit(world_id, coin_name)


@rpc("authority", "call_local", "reliable")
func enemy_killed(world_id: String, enemy_name: String) -> void:
	_ws(world_id)["killed"][enemy_name] = true
	enemy_removed.emit(world_id, enemy_name)


@rpc("authority", "call_local", "reliable")
func victory_now() -> void:
	victory_reached = true
	victory_happened.emit()


@rpc("authority", "call_remote", "unreliable")
func enemies_state(world_id: String, data: Array) -> void:
	if is_host:
		return
	if world_id != current_world:
		return
	enemy_states.emit(data)


# ---------- Обнаружение хостов в локальной сети ----------


func _start_discovery_server() -> void:
	_disc_server = PacketPeerUDP.new()
	if _disc_server.bind(DISCOVERY_PORT, "0.0.0.0") != OK:
		_disc_server = null


func _stop_discovery_server() -> void:
	if _disc_server != null:
		_disc_server.close()
		_disc_server = null


func _poll_discovery_server() -> void:
	if _disc_server == null:
		return
	while _disc_server.get_available_packet_count() > 0:
		var pkt := _disc_server.get_packet()
		var ip := _disc_server.get_packet_ip()
		var port := _disc_server.get_packet_port()
		if pkt.get_string_from_utf8() == DISCOVERY_MSG:
			var reply := JSON.stringify({"name": player_name, "players": players.size()})
			_disc_server.set_dest_address(ip, port)
			_disc_server.put_packet(reply.to_utf8_buffer())


func start_browse() -> void:
	stop_browse()
	found_servers.clear()
	_disc_client = PacketPeerUDP.new()
	var bound := false
	for p in range(DISCOVERY_PORT + 1, DISCOVERY_PORT + 20):
		if _disc_client.bind(p, "0.0.0.0") == OK:
			bound = true
			break
	if not bound:
		_disc_client = null
		return
	_disc_client.set_broadcast_enabled(true)
	_browse_accum = 1.0


func stop_browse() -> void:
	if _disc_client != null:
		_disc_client.close()
		_disc_client = null


func _send_discover() -> void:
	if _disc_client == null:
		return
	_disc_client.set_dest_address("255.255.255.255", DISCOVERY_PORT)
	_disc_client.put_packet(DISCOVERY_MSG.to_utf8_buffer())


func _poll_browse(delta: float) -> void:
	if _disc_client == null:
		return
	_browse_accum += delta
	if _browse_accum >= 1.0:
		_browse_accum = 0.0
		_send_discover()
	var changed := false
	while _disc_client.get_available_packet_count() > 0:
		var pkt := _disc_client.get_packet()
		var ip := _disc_client.get_packet_ip()
		var parsed = JSON.parse_string(pkt.get_string_from_utf8())
		if parsed is Dictionary:
			found_servers[ip] = {
				"name": str(parsed.get("name", "Игра")),
				"players": int(parsed.get("players", 1)),
				"seen": Time.get_ticks_msec(),
			}
			changed = true
	var now := Time.get_ticks_msec()
	for ip in found_servers.keys():
		if now - int(found_servers[ip]["seen"]) > 5000:
			found_servers.erase(ip)
			changed = true
	if changed:
		server_list_updated.emit()
