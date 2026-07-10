class_name MeadowWorld
extends WorldBase
# Солнечные луга — платформинг, монеты и грибы-вредины.

const GRASS := Color(0.42, 0.72, 0.28)
const PLAT := Color(0.52, 0.78, 0.33)


func build() -> void:
	spawn_point = Vector3(0, 1.5, 24)
	setup_sky(
		Color(0.33, 0.6, 0.98),
		Color(0.88, 0.92, 0.8),
		Color(0.3, 0.4, 0.25),
		Vector3(-55, 20, 0),
		1.55
	)

	# Основное поле
	add_box(Vector3(0, -0.5, 0), Vector3(90, 1, 90), GRASS, 0.95, 0.0, 0.0, 0.3, 0.35)
	add_box(Vector3(0, -3.0, 0), Vector3(80, 4, 80), Color(0.45, 0.32, 0.2), 0.95, 0.0, 0.0, 0.6, 0.6)

	# Трава и облака
	add_grass(
		Vector3(14, 0.02, 0), Vector2(26, 38), 4200, Color(0.22, 0.5, 0.12), Color(0.62, 0.9, 0.3)
	)
	add_grass(
		Vector3(-24, 0.02, 20), Vector2(14, 14), 1200, Color(0.22, 0.5, 0.12), Color(0.62, 0.9, 0.3)
	)
	add_grass(
		Vector3(24, 9.52, -42), Vector2(5, 5), 350, Color(0.22, 0.5, 0.12), Color(0.62, 0.9, 0.3)
	)
	add_cloud(Vector3(-40, 28, -25), 2.4)
	add_cloud(Vector3(20, 34, -45), 3.0)
	add_cloud(Vector3(45, 26, 20), 2.0)
	add_cloud(Vector3(-20, 30, 35), 2.2)
	add_cloud(Vector3(0, 36, 0), 2.6)

	# Горы на горизонте
	var mnt := Color(0.42, 0.55, 0.62)
	add_mountain(Vector3(-110, 0, -80), 42, 52, mnt)
	add_mountain(Vector3(0, 0, -125), 48, 62, mnt.darkened(0.05))
	add_mountain(Vector3(100, 0, -90), 36, 46, mnt.lightened(0.04))
	add_mountain(Vector3(125, 0, 30), 40, 50, mnt)
	add_mountain(Vector3(-120, 0, 55), 38, 44, mnt.darkened(0.03))

	# Пыльца и падающие листья
	add_motes(Vector3(0, 2.5, 0), Vector3(30, 2.5, 30), 70, Color(1.0, 0.98, 0.7, 0.65))
	add_leaves(Vector3(26, 16, -44), Vector3(6, 1, 6), 24, Color(0.55, 0.75, 0.3))
	add_leaves(Vector3(-14, 8, 20), Vector3(5, 1, 5), 16, Color(0.5, 0.7, 0.28))
	add_leaves(Vector3(22, 7, -6), Vector3(4, 1, 4), 14, Color(0.9, 0.6, 0.3))

	# Холмы по краям (декор)
	add_decor_ball(Vector3(-34, -2, -30), Vector3(30, 12, 26), GRASS.darkened(0.08))
	add_decor_ball(Vector3(36, -3, -24), Vector3(26, 14, 24), GRASS.darkened(0.12))
	add_decor_ball(Vector3(30, -4, 32), Vector3(28, 12, 22), GRASS.darkened(0.05))

	# Портал домой
	add_portal(Vector3(0, 1.4, 32), "hub", "В деревню", Color(1.0, 0.8, 0.4))

	# Поле с монетами и грибами
	add_coin(Vector3(4, 1.0, 16))
	add_coin(Vector3(-5, 1.0, 12))
	add_coin(Vector3(8, 1.0, 8))
	add_coin(Vector3(-9, 1.0, 4))
	add_coin(Vector3(2, 1.0, 0))
	add_coin(Vector3(-3, 1.0, -6))
	add_coin(Vector3(7, 1.0, -10))
	add_coin(Vector3(-8, 1.0, -14))
	add_enemy(Vector3(-6, 0, 8), Vector3(6, 0, 8), "mushroom", 2.2)
	add_enemy(Vector3(5, 0, -2), Vector3(-5, 0, -6), "mushroom", 2.5)

	# Пруд с камнями-ступеньками
	add_box(Vector3(-20, -0.4, -8), Vector3(16, 0.8, 20), Color(0.5, 0.42, 0.3))
	add_water(Vector3(-20, 0.12, -8), Vector2(15, 19))
	var stone := Color(0.62, 0.6, 0.55)
	add_box(Vector3(-15, 0.3, -2), Vector3(2, 0.7, 2), stone, 0.95)
	add_box(Vector3(-18, 0.3, -6), Vector3(2, 0.7, 2), stone, 0.95)
	add_box(Vector3(-21, 0.3, -10), Vector3(2, 0.7, 2), stone, 0.95)
	add_box(Vector3(-24, 0.3, -13), Vector3(2, 0.7, 2), stone, 0.95)
	add_coin(Vector3(-15, 1.4, -2))
	add_coin(Vector3(-18, 1.4, -6))
	add_coin(Vector3(-21, 1.4, -10))
	add_coin(Vector3(-24, 1.4, -13))

	# Парящие платформы наверх к плато
	var steps := [
		Vector3(6, 1.5, -18),
		Vector3(10, 3.0, -22),
		Vector3(14, 4.5, -26),
		Vector3(10, 6.0, -30),
		Vector3(14, 7.5, -34),
		Vector3(18, 8.5, -38),
	]
	for s in steps:
		add_box(s, Vector3(3.2, 0.6, 3.2), PLAT, 0.9)
		add_coin(s + Vector3(0, 1.2, 0))

	# Плато с большим деревом
	add_box(Vector3(24, 9.0, -42), Vector3(12, 1, 12), GRASS.lightened(0.05))
	add_tree(Vector3(26, 9.5, -44), Color(0.3, 0.6, 0.25), 1.8)
	add_enemy(Vector3(20, 9.7, -40), Vector3(28, 9.7, -40), "mushroom", 2.0)
	add_coin(Vector3(22, 10.6, -40))
	add_coin(Vector3(26, 10.6, -40))
	add_coin(Vector3(24, 10.6, -46))
	add_coin(Vector3(20, 10.6, -46))

	# Деревья, камни, цветы
	add_tree(Vector3(16, 0, 14))
	add_tree(Vector3(-14, 0, 20), Color(0.32, 0.55, 0.2), 1.2)
	add_tree(Vector3(22, 0, -6), Color(0.85, 0.55, 0.3), 0.9)
	add_tree(Vector3(-26, 0, 12), Color(0.25, 0.55, 0.3))
	add_rock(Vector3(12, 0.3, 2), 1.5)
	add_rock(Vector3(-2, 0.25, 22), 1.1)
	var flower_colors := [
		Color(0.95, 0.4, 0.45), Color(0.95, 0.85, 0.3), Color(0.6, 0.5, 0.95), Color(1.0, 0.65, 0.3)
	]
	for i in range(26):
		var ang := randf() * TAU
		var r := randf_range(4.0, 34.0)
		var p := Vector3(cos(ang) * r, 0, sin(ang) * r)
		if p.x > -12 or p.z > 4:
			add_flower(p, flower_colors[i % 4])
