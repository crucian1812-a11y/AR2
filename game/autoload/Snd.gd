extends Node
# Процедурный звук: все эффекты и фоновая музыка синтезируются кодом
# при запуске (WAV из сэмплов), внешних файлов нет.

const RATE := 22050

var muted := false

var _sounds: Dictionary = {}
var _pool: Array[AudioStreamPlayer] = []
var _pool_i := 0
var _music: AudioStreamPlayer
var _music_thread: Thread


func _ready() -> void:
	for i in range(8):
		var p := AudioStreamPlayer.new()
		add_child(p)
		_pool.append(p)
	_music = AudioStreamPlayer.new()
	_music.volume_db = -13.0
	add_child(_music)
	_generate_sfx()
	_music_thread = Thread.new()
	_music_thread.start(_music_job)


func _exit_tree() -> void:
	if _music_thread != null and _music_thread.is_started():
		_music_thread.wait_to_finish()


func play(sname: String, vol_db := 0.0, pitch := 1.0) -> void:
	if not _sounds.has(sname):
		return
	var p := _pool[_pool_i]
	_pool_i = (_pool_i + 1) % _pool.size()
	p.stream = _sounds[sname]
	p.volume_db = vol_db
	p.pitch_scale = pitch
	p.play()


func set_muted(m: bool) -> void:
	muted = m
	AudioServer.set_bus_mute(AudioServer.get_bus_index("Master"), m)


func _music_job() -> void:
	var m := _make_music()
	call_deferred("_music_ready", m)


func _music_ready(m: AudioStreamWAV) -> void:
	_sounds["music"] = m
	_music.stream = m
	_music.play()


# ---------- Синтез ----------


func _wav_from_floats(buf: PackedFloat32Array, do_loop := false) -> AudioStreamWAV:
	var n := buf.size()
	var data := PackedByteArray()
	data.resize(n * 2)
	for i in range(n):
		data.encode_s16(i * 2, int(clampf(buf[i], -1.0, 1.0) * 32000.0))
	var w := AudioStreamWAV.new()
	w.format = AudioStreamWAV.FORMAT_16_BITS
	w.mix_rate = RATE
	w.stereo = false
	w.data = data
	if do_loop:
		w.loop_mode = AudioStreamWAV.LOOP_FORWARD
		w.loop_begin = 0
		w.loop_end = n
	return w


func _synth(
	duration: float,
	f0: float,
	f1: float,
	wave := 0,
	attack := 0.01,
	decay_pow := 1.5,
	vol := 0.5,
	noise_mix := 0.0
) -> AudioStreamWAV:
	var n := int(duration * RATE)
	var buf := PackedFloat32Array()
	buf.resize(n)
	var phase := 0.0
	for i in range(n):
		var t := float(i) / float(n)
		var f := lerpf(f0, f1, t)
		phase += TAU * f / RATE
		var s: float
		match wave:
			1:
				s = signf(sin(phase)) * 0.6
			2:
				s = (2.0 * fposmod(phase / TAU, 1.0) - 1.0) * 0.7
			_:
				s = sin(phase)
		if noise_mix > 0.0:
			s = lerpf(s, randf() * 2.0 - 1.0, noise_mix)
		var env := minf(t / maxf(attack, 0.0001), 1.0) * pow(1.0 - t, decay_pow)
		buf[i] = s * env * vol
	return _wav_from_floats(buf)


func _mix_notes(total: float, notes: Array, vol := 0.4, do_loop := false) -> AudioStreamWAV:
	var n := int(total * RATE)
	var buf := PackedFloat32Array()
	buf.resize(n)
	for note in notes:
		var freq: float = note[0]
		var start: float = note[1]
		var dur: float = note[2]
		var amp: float = note[3] if note.size() > 3 else 1.0
		var i0 := int(start * RATE)
		var cnt := int(dur * RATE)
		for j in range(cnt):
			var idx := i0 + j
			if idx >= n:
				break
			var t := float(j) / float(cnt)
			var env := minf(t * 40.0, 1.0) * pow(1.0 - t, 2.5)
			buf[idx] += sin(TAU * freq * float(j) / RATE) * env * amp
	for i in range(n):
		buf[i] *= vol
	return _wav_from_floats(buf, do_loop)


func _generate_sfx() -> void:
	_sounds["click"] = _synth(0.06, 1200.0, 900.0, 0, 0.005, 1.2, 0.25)
	_sounds["jump"] = _synth(0.18, 320.0, 640.0, 0, 0.01, 1.4, 0.4)
	_sounds["land"] = _synth(0.12, 130.0, 60.0, 0, 0.005, 1.5, 0.35)
	_sounds["swing"] = _synth(0.16, 500.0, 180.0, 0, 0.02, 1.2, 0.3, 0.85)
	_sounds["stomp"] = _synth(0.22, 300.0, 80.0, 1, 0.005, 1.5, 0.4, 0.3)
	_sounds["hurt"] = _synth(0.3, 260.0, 150.0, 2, 0.005, 1.4, 0.35, 0.2)
	_sounds["portal"] = _synth(0.55, 200.0, 900.0, 0, 0.25, 1.2, 0.32, 0.25)
	_sounds["coin"] = _mix_notes(0.32, [[1046.5, 0.0, 0.14, 1.0], [1568.0, 0.07, 0.22, 0.8]], 0.4)
	_sounds["quest"] = _mix_notes(0.4, [[880.0, 0.0, 0.16, 1.0], [1174.7, 0.1, 0.28, 0.9]], 0.35)
	_sounds["victory"] = _mix_notes(
		1.1,
		[
			[523.25, 0.0, 0.2, 1.0],
			[659.26, 0.16, 0.2, 1.0],
			[783.99, 0.32, 0.2, 1.0],
			[1046.5, 0.48, 0.6, 1.2],
		],
		0.45
	)


func _make_music() -> AudioStreamWAV:
	var beat := 0.5  # 120 BPM
	var chords := [
		[261.63, 329.63, 392.0],  # C
		[220.0, 261.63, 329.63],  # Am
		[174.61, 220.0, 261.63],  # F
		[196.0, 246.94, 293.66],  # G
	]
	var notes: Array = []
	for b in range(4):
		var chord: Array = chords[b]
		var bar_t := b * beat * 4.0
		notes.append([chord[0] * 0.5, bar_t, 0.9, 0.9])
		notes.append([chord[0] * 0.5, bar_t + beat * 2.0, 0.9, 0.7])
		for k in range(8):
			var tone: float = chord[k % 3]
			if k % 4 == 3:
				tone = chord[1] * 2.0
			notes.append([tone, bar_t + k * beat * 0.5, 0.35, 0.45])
		notes.append([chord[2] * 2.0, bar_t + beat, 0.7, 0.2])
	return _mix_notes(4.0 * beat * 4.0, notes, 0.33, true)
