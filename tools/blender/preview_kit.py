#!/usr/bin/env python3
import bpy, sys, math, os
argv = sys.argv[sys.argv.index("--")+1:]
src, out = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)

# габариты сцены
import mathutils
mn = mathutils.Vector(( 1e9,)*3); mx = mathutils.Vector((-1e9,)*3)
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    for c in o.bound_box:
        w = o.matrix_world @ mathutils.Vector(c)
        for i in range(3):
            mn[i] = min(mn[i], w[i]); mx[i] = max(mx[i], w[i])
ctr = (mn+mx)/2; size = max((mx-mn).x, (mx-mn).y, (mx-mn).z)
print("BOUNDS size=%.2f  x=%.2f y=%.2f z=%.2f" % (size,(mx-mn).x,(mx-mn).y,(mx-mn).z))
print("MATERIALS:", len(bpy.data.materials), "MESHES:", len([o for o in bpy.data.objects if o.type=='MESH']))
tris = sum(len(o.data.loop_triangles) for o in bpy.data.objects if o.type=='MESH' and (o.data.calc_loop_triangles() or True))
print("TRIS:", tris)

# Текстуры Unity вешает в рантайме по ИМЕНИ материала (KoenigProp.CatFromName).
# Повторяем то же самое, иначе превью врёт: в FBX ссылок на текстуры нет.
TEXDIR = "/home/user/AR2/unity/Assets/Resources/Textures/koenig/"
CAT = [("plaster","T_Plaster"),("unevenbrick","T_UnevenBrick"),("brick","T_Brick"),
       ("redbrick","T_RedBrick"),("rocktrim","T_RockTrim"),("roundtiles","T_RoundTiles"),
       ("woodtrim","T_WoodTrim"),("tiles","T_RoundTiles"),("wood","T_WoodTrim"),
       ("stone","T_RockTrim"),("roof","T_RoundTiles")]
FLAT = {"metal":(0.24,0.26,0.30),"glass":(0.18,0.26,0.34),"vine":(0.30,0.50,0.24)}
import os
def hook(mat):
    n = mat.name.lower()
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    if bsdf is None: return
    bsdf.inputs["Roughness"].default_value = 0.85
    for key, rgb in FLAT.items():
        if key in n:
            bsdf.inputs["Base Color"].default_value = (*rgb,1); return
    for key, tex in CAT:
        if key in n:
            path = TEXDIR + tex + "_BaseColor.png"
            if os.path.exists(path):
                img = nt.nodes.new("ShaderNodeTexImage")
                img.image = bpy.data.images.load(path)
                nt.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
            npath = TEXDIR + tex + "_Normal.png"
            if os.path.exists(npath):
                nim = nt.nodes.new("ShaderNodeTexImage")
                nim.image = bpy.data.images.load(npath)
                nim.image.colorspace_settings.name = "Non-Color"
                nmap = nt.nodes.new("ShaderNodeNormalMap")
                nt.links.new(nim.outputs["Color"], nmap.inputs["Color"])
                nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
            return
    bsdf.inputs["Base Color"].default_value = (0.72,0.68,0.62,1)

for m in bpy.data.materials: hook(m)
print("HOOKED materials:", len(bpy.data.materials))

# камера три четверти
cam_d = size*2.0
bpy.ops.object.camera_add(location=(ctr.x+cam_d*0.62, ctr.y-cam_d*0.72, ctr.z+cam_d*0.5))
cam = bpy.context.object
cam.rotation_euler = (math.radians(66), 0, math.radians(41))
cam.data.lens = 55
bpy.context.scene.camera = cam

# солнце сбоку + мягкое небо
bpy.ops.object.light_add(type='SUN', location=(ctr.x-size, ctr.y-size, ctr.z+size*2))
s = bpy.context.object; s.data.energy = 4.0; s.data.angle = math.radians(6)
s.rotation_euler = (math.radians(52), 0, math.radians(-50))
w = bpy.data.worlds.new("W"); bpy.context.scene.world = w
w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.55,0.72,0.92,1)
w.node_tree.nodes["Background"].inputs[1].default_value = 1.1

sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 24; sc.cycles.device='CPU'; sc.cycles.use_denoising=False
sc.render.resolution_x = 900; sc.render.resolution_y = 700
sc.render.filepath = out
sc.view_settings.view_transform = 'Filmic'
bpy.ops.render.render(write_still=True)
print("OK ->", out)
