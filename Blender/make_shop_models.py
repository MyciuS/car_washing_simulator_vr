# Builds the shop and the things sold in it, and exports them as FBX files to Assets/Models/Shop.
# Run it with Blender in the background (from the project folder):
#   blender --background --factory-startup --python Blender/make_shop_models.py
# It also saves Blender/Shop.blend so the models can be changed by hand later.
# Blender is Z-up; the meshes are built in meters. Every item has its origin at the bottom middle,
# except the gun attachments, whose origin is the end that clicks onto the gun (they point along -Y).
import math
import os
import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
OUT = os.path.join(PROJECT, "Assets", "Models", "Shop")
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------- materials (Unity replaces them with its own by name) ----------
COLORS = {
    "ShopWall": (0.88, 0.91, 0.93, 1), "ShopTrim": (0.13, 0.33, 0.70, 1), "ShopFloor": (0.70, 0.71, 0.70, 1),
    "ShopRoof": (0.24, 0.26, 0.29, 1), "AwningRed": (0.80, 0.12, 0.12, 1), "AwningWhite": (0.95, 0.95, 0.95, 1),
    "SignBoard": (0.10, 0.22, 0.55, 1), "Glass": (0.70, 0.85, 0.95, 0.25), "ShopLight": (1, 1, 0.95, 1),
    "ShelfMetal": (0.50, 0.53, 0.57, 1), "ShelfWood": (0.66, 0.48, 0.32, 1), "PriceStrip": (0.97, 0.97, 0.97, 1),
    "Counter": (0.15, 0.35, 0.72, 1), "CounterTop": (0.92, 0.92, 0.90, 1), "Register": (0.12, 0.12, 0.13, 1),
    "Screen": (0.20, 0.80, 0.45, 1), "Cooler": (0.93, 0.94, 0.96, 1), "CoolerInside": (0.75, 0.86, 0.95, 1),
    "DoorFrame": (0.20, 0.21, 0.23, 1), "Mat": (0.35, 0.12, 0.12, 1),
    "BottleBody": (0.92, 0.95, 0.98, 0.45), "Liquid": (0.95, 0.40, 0.70, 1), "Label": (0.95, 0.40, 0.70, 1),
    "SprayerHead": (0.95, 0.95, 0.95, 1), "Trigger": (0.15, 0.15, 0.16, 1),
    "Cup": (0.96, 0.95, 0.92, 1), "Sleeve": (0.55, 0.36, 0.22, 1), "Lid": (0.15, 0.13, 0.12, 1),
    "CanBody": (0.10, 0.75, 0.30, 1), "CanMetal": (0.80, 0.82, 0.85, 1),
    "Dough": (0.85, 0.60, 0.32, 1), "Icing": (0.95, 0.45, 0.65, 1), "Sprinkle": (0.98, 0.95, 0.40, 1),
    "Bun": (0.88, 0.65, 0.35, 1), "Sausage": (0.72, 0.28, 0.18, 1), "Mustard": (0.98, 0.82, 0.10, 1),
    "Brass": (0.80, 0.62, 0.25, 1), "Knob": (0.12, 0.12, 0.13, 1), "CannonBottle": (0.92, 0.95, 0.98, 0.5),
    "TurboBody": (0.98, 0.80, 0.10, 1), "TurboTip": (0.13, 0.13, 0.14, 1),
}
MATS = {}
for name, color in COLORS.items():
    m = bpy.data.materials.new(name)
    m.diffuse_color = color
    if m.node_tree is None and hasattr(m, "use_nodes"):
        m.use_nodes = True
    bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if m.node_tree else None
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Alpha"].default_value = color[3]
    MATS[name] = m


def finish(obj, mat, smooth_angle=None):
    obj.data.materials.append(MATS[mat])
    if smooth_angle is not None:
        for p in obj.data.polygons:
            p.use_smooth = True
        try:
            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(smooth_angle))
            obj.select_set(False)
        except Exception:
            pass
    return obj


def box(name, x0, x1, y0, y1, z0, z1, mat, origin=(0, 0, 0)):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    ox, oy, oz = origin
    v = [bm.verts.new((x - ox, y - oy, z - oz)) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
        bm.faces.new([v[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = origin
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat)


def cone(name, r0, r1, z0, z1, mat, segs=20, x=0.0, y=0.0, axis="Z", smooth=True):
    # A cylinder/cone from z0 to z1 (along the axis), centered on (x, y) across it.
    bpy.ops.mesh.primitive_cone_add(vertices=segs, radius1=r0, radius2=r1, depth=z1 - z0, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name
    me = obj.data
    for vert in me.vertices:
        vert.co.z += (z0 + z1) / 2
    if axis == "Y":  # along -Y (forward), from y=-z0 to y=-z1
        for vert in me.vertices:
            vx, vy, vz = vert.co
            vert.co = (vx, -vz, vy)
    for vert in me.vertices:
        vert.co.x += x
        if axis == "Z":
            vert.co.y += y
        else:
            vert.co.z += y
    obj.select_set(False)
    return finish(obj, mat, 40 if smooth else None)


def torus(name, major, minor, z, mat, keep_top_only=False):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=24, minor_segments=12,
                                     location=(0, 0, z))
    obj = bpy.context.active_object
    obj.name = name
    obj.select_set(False)
    if keep_top_only:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -minor * 0.15], context="VERTS")
        bm.to_mesh(obj.data)
        bm.free()
    return finish(obj, mat, 60)


def sphere(name, sx, sy, sz, loc, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    for v in obj.data.vertices:
        v.co.x *= sx
        v.co.y *= sy
        v.co.z *= sz
    obj.select_set(False)
    return finish(obj, mat, 60)


def join(name, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name
    bpy.ops.object.select_all(action="DESELECT")
    return obj


def collection(name):
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    layer = bpy.context.view_layer.layer_collection.children[name]
    bpy.context.view_layer.active_layer_collection = layer
    return col


def export(col, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in col.objects:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, filename), use_selection=True, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, object_types={"MESH", "EMPTY"}, use_mesh_modifiers=True,
                             mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
    bpy.ops.object.select_all(action="DESELECT")
    print("exported", filename, len(col.objects), "objects")


# ---------- the shop building (7 m x 6 m, door in the front wall at -Y) ----------
shop = collection("Shop")
W, D, H, T = 3.5, 3.0, 3.2, 0.2          # half width, half depth, wall height, wall thickness
box("Floor", -W, W, -D, D, 0, 0.06, "ShopFloor")
box("Door Mat", -0.8, 0.8, -D - 0.9, -D + 0.6, 0, 0.065, "Mat")
box("Wall Back", -W, W, D - T, D, 0, H, "ShopWall")
box("Wall Left", -W, -W + T, -D, D - T, 0, H, "ShopWall")
box("Wall Right", W - T, W, -D, D - T, 0, H, "ShopWall")
# Front wall: low wall under the windows, a band above, pillars beside the door, glass windows.
box("Wall Front Low L", -W + T, -0.9, -D, -D + T, 0, 0.9, "ShopWall")
box("Wall Front Low R", 0.9, W - T, -D, -D + T, 0, 0.9, "ShopWall")
box("Wall Front Top", -W + T, W - T, -D, -D + T, 2.3, H, "ShopWall")
box("Door Post L", -1.2, -0.9, -D, -D + T, 0, 2.3, "DoorFrame")
box("Door Post R", 0.9, 1.2, -D, -D + T, 0, 2.3, "DoorFrame")
box("Window L", -W + T, -1.2, -D + 0.09, -D + 0.11, 0.9, 2.3, "Glass")
box("Window R", 1.2, W - T, -D + 0.09, -D + 0.11, 0.9, 2.3, "Glass")
box("Sill L", -W + T, -1.2, -D - 0.06, -D + T + 0.04, 0.86, 0.92, "ShopTrim")
box("Sill R", 1.2, W - T, -D - 0.06, -D + T + 0.04, 0.86, 0.92, "ShopTrim")
# Blue stripe around the outside (like the car wash).
box("Stripe Front", -W - 0.02, W + 0.02, -D - 0.02, -D, 2.45, 2.65, "ShopTrim")
box("Stripe Back", -W - 0.02, W + 0.02, D, D + 0.02, 2.45, 2.65, "ShopTrim")
box("Stripe Left", -W - 0.02, -W, -D, D, 2.45, 2.65, "ShopTrim")
box("Stripe Right", W, W + 0.02, -D, D, 2.45, 2.65, "ShopTrim")
box("Roof", -W - 0.3, W + 0.3, -D - 0.3, D + 0.3, H, H + 0.25, "ShopRoof")
for i, x in enumerate((-1.8, 0.0, 1.8)):
    box("Ceiling Light %d" % i, x - 0.6, x + 0.6, -0.5, 0.5, H - 0.04, H, "ShopLight")
box("Sign Board", -2.6, 2.6, -D - 0.32, -D - 0.12, H + 0.25, H + 1.0, "SignBoard")
box("Sign Frame", -2.65, 2.65, -D - 0.3, -D - 0.1, H + 0.2, H + 1.05, "ShopTrim")

# Striped awning over the front: slats sloping down and out, with a short hanging edge.
aw = []
for i in range(7):
    x0, x1 = -W + i, -W + i + 1
    mat = "AwningRed" if i % 2 == 0 else "AwningWhite"
    mesh = bpy.data.meshes.new("Awning %d" % i)
    bm = bmesh.new()
    top, low, out = 2.85, 2.45, -D - 0.9
    pts = [(x0, -D, top), (x1, -D, top), (x1, out, low), (x0, out, low)]
    vs = [bm.verts.new(p) for p in pts] + [bm.verts.new((p[0], p[1], p[2] - 0.03)) for p in pts]
    for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
        bm.faces.new([vs[j] for j in f])
    # hanging edge
    hv = [bm.verts.new(p) for p in ((x0, out, low), (x1, out, low), (x1, out, low - 0.25), (x0, out, low - 0.25),
                                    (x0, out + 0.02, low), (x1, out + 0.02, low), (x1, out + 0.02, low - 0.25),
                                    (x0, out + 0.02, low - 0.25))]
    for f in ((0, 1, 2, 3), (5, 4, 7, 6), (3, 2, 6, 7), (0, 3, 7, 4), (1, 5, 6, 2)):
        bm.faces.new([hv[j] for j in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    o = bpy.data.objects.new("Awning %d" % i, mesh)
    bpy.context.collection.objects.link(o)
    finish(o, mat)


def shelf_unit(name, x0, x1, y0, y1, facing, heights=(0.1, 0.55, 1.0, 1.45, 1.9)):
    # A shelf: two side panels, a back panel, boards, and a white strip on each board's front edge
    # (the price tags go there). facing = which way the open front looks: "-Y", "+X" or "-X".
    parts = []
    t = 0.035
    if facing == "-Y":
        parts.append(box(name + " Side L", x0, x0 + t, y0, y1, 0, 2.0, "ShelfMetal"))
        parts.append(box(name + " Side R", x1 - t, x1, y0, y1, 0, 2.0, "ShelfMetal"))
        parts.append(box(name + " Back", x0, x1, y1 - 0.02, y1, 0, 2.0, "ShelfMetal"))
        for k, z in enumerate(heights):
            parts.append(box(name + " Board %d" % k, x0 + t, x1 - t, y0, y1 - 0.02, z - 0.03, z, "ShelfWood"))
            parts.append(box(name + " Strip %d" % k, x0 + t, x1 - t, y0 - 0.012, y0, z - 0.05, z + 0.005, "PriceStrip"))
    else:
        sign = 1 if facing == "+X" else -1
        front, back = (x1, x0) if sign == 1 else (x0, x1)
        parts.append(box(name + " Side A", x0, x1, y0, y0 + t, 0, 2.0, "ShelfMetal"))
        parts.append(box(name + " Side B", x0, x1, y1 - t, y1, 0, 2.0, "ShelfMetal"))
        bx0, bx1 = (back, back + 0.02) if sign == 1 else (back - 0.02, back)
        parts.append(box(name + " Back", bx0, bx1, y0, y1, 0, 2.0, "ShelfMetal"))
        for k, z in enumerate(heights):
            parts.append(box(name + " Board %d" % k, min(x0, x1) + (0.02 if sign == 1 else 0), max(x0, x1) - (0 if sign == 1 else 0.02),
                             y0 + t, y1 - t, z - 0.03, z, "ShelfWood"))
            sx0, sx1 = (front, front + 0.012) if sign == 1 else (front - 0.012, front)
            parts.append(box(name + " Strip %d" % k, sx0, sx1, y0 + t, y1 - t, z - 0.05, z + 0.005, "PriceStrip"))
    return parts


# Back wall: tools on the left, gun attachments and sprays on the right. Left wall: car soaps.
shelf_unit("Shelf Tools", -3.15, -0.25, D - T - 0.5, D - T, "-Y")
shelf_unit("Shelf Attachments", 0.25, 3.15, D - T - 0.5, D - T, "-Y")
shelf_unit("Shelf Soaps", -W + T, -W + T + 0.45, -1.6, 1.6, "+X")

# Drinks cooler on the right wall (open front, cold blue inside).
cx0, cx1, cy0, cy1 = W - T - 0.6, W - T, -0.2, 1.8
box("Cooler Side A", cx0, cx1, cy0, cy0 + 0.05, 0, 2.1, "Cooler")
box("Cooler Side B", cx0, cx1, cy1 - 0.05, cy1, 0, 2.1, "Cooler")
box("Cooler Top", cx0, cx1, cy0, cy1, 1.95, 2.1, "Cooler")
box("Cooler Base", cx0, cx1, cy0, cy1, 0, 0.3, "Cooler")
box("Cooler Back", cx1 - 0.04, cx1, cy0, cy1, 0.3, 1.95, "CoolerInside")
for k, z in enumerate((0.75, 1.2, 1.65)):
    box("Cooler Shelf %d" % k, cx0, cx1 - 0.04, cy0 + 0.05, cy1 - 0.05, z - 0.025, z, "CoolerInside")
    box("Cooler Strip %d" % k, cx0 - 0.012, cx0, cy0 + 0.05, cy1 - 0.05, z - 0.045, z + 0.005, "PriceStrip")
box("Cooler Light", cx0 + 0.05, cx1 - 0.05, cy0 + 0.1, cy1 - 0.1, 1.93, 1.95, "ShopLight")

# Counter by the door with a cash register and a food display.
box("Counter Body", 1.3, 3.15, -2.3, -1.6, 0, 0.95, "Counter")
box("Counter Top", 1.25, 3.2, -2.35, -1.55, 0.95, 1.0, "CounterTop")
box("Register Base", 2.65, 3.05, -2.05, -1.75, 1.0, 1.12, "Register")
box("Register Screen", 2.7, 3.0, -1.85, -1.8, 1.12, 1.32, "Screen")
box("Register Stand", 2.82, 2.88, -1.82, -1.76, 1.12, 1.15, "Register")

export(shop, "Shop.fbx")

# ---------- spray bottle (soap, grime remover, wax) ----------
bottle = collection("Spray Bottle")
body = cone("Bottle", 0.042, 0.038, 0.0, 0.17, "BottleBody", 20)
neck = cone("Neck", 0.022, 0.018, 0.17, 0.2, "BottleBody", 16)
join("Bottle", [body, neck])
liquid = cone("Liquid", 0.0375, 0.0345, 0.0, 0.155, "Liquid", 20)
liquid.location = (0, 0, 0.006)   # origin at its bottom: scaling Z lowers the level
cone("Label", 0.0435, 0.0415, 0.045, 0.125, "Label", 20)
head = box("Head", -0.018, 0.018, -0.055, 0.03, 0.2, 0.245, "SprayerHead")
cap = cone("Cap", 0.024, 0.024, 0.192, 0.21, "SprayerHead", 16)
nozzle = cone("Nozzle", 0.008, 0.006, 0.055, 0.075, "SprayerHead", 10, x=0.0, y=0.228, axis="Y", smooth=False)
join("Head", [head, cap, nozzle])
trig = box("Trigger", -0.008, 0.008, -0.05, -0.034, 0.145, 0.205, "Trigger")
export(bottle, "Spray Bottle.fbx")

# ---------- food ----------
coffee = collection("Coffee")
join("Coffee", [cone("Cup", 0.033, 0.043, 0.0, 0.12, "Cup", 20),
                cone("Sleeve", 0.0365, 0.0415, 0.035, 0.085, "Sleeve", 20),
                cone("Lid", 0.046, 0.042, 0.118, 0.135, "Lid", 20)])
export(coffee, "Coffee.fbx")

can = collection("Energy Drink")
join("Energy Drink", [cone("Can", 0.033, 0.033, 0.006, 0.124, "CanBody", 20),
                      cone("Bottom", 0.029, 0.033, 0.0, 0.006, "CanMetal", 20),
                      cone("Top", 0.033, 0.028, 0.124, 0.134, "CanMetal", 20)])
export(can, "Energy Drink.fbx")

donut = collection("Donut")
parts = [torus("Dough", 0.045, 0.022, 0.022, "Dough"), torus("Icing", 0.045, 0.0235, 0.0235, "Icing", True)]
for i in range(10):
    a = i * 2 * math.pi / 10 + 0.3
    r = 0.045 + (0.008 if i % 2 else -0.008)
    s = sphere("Sprinkle %d" % i, 0.006, 0.002, 0.002, (math.cos(a) * r, math.sin(a) * r, 0.045), "Sprinkle")
    s.rotation_euler = (0, 0, a + (0.8 if i % 2 else -0.6))
    bpy.ops.object.select_all(action="DESELECT")
    s.select_set(True)
    bpy.context.view_layer.objects.active = s
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    parts.append(s)
join("Donut", parts)
export(donut, "Donut.fbx")

hotdog = collection("Hot Dog")
join("Hot Dog", [sphere("Bun L", 0.085, 0.022, 0.026, (0, -0.016, 0.026), "Bun"),
                 sphere("Bun R", 0.085, 0.022, 0.026, (0, 0.016, 0.026), "Bun"),
                 sphere("Sausage", 0.1, 0.016, 0.016, (0, 0, 0.044), "Sausage"),
                 sphere("Mustard", 0.075, 0.006, 0.004, (0, 0, 0.06), "Mustard")])
export(hotdog, "Hot Dog.fbx")

# ---------- gun attachments (origin = the end that clicks onto the gun, pointing along -Y) ----------
foam = collection("Foam Cannon")
join("Foam Cannon", [
    cone("Connector", 0.012, 0.012, 0.0, 0.03, "Brass", 12, axis="Y"),
    box("Block", -0.022, 0.022, -0.095, -0.03, -0.022, 0.022, "Brass"),
    cone("Knob", 0.012, 0.012, 0.022, 0.04, "Knob", 12, y=-0.062),
    cone("Flare", 0.012, 0.032, 0.095, 0.17, "Brass", 16, axis="Y"),
    cone("Cap", 0.03, 0.03, -0.045, -0.022, "Knob", 16, y=-0.062),
    cone("Bottle", 0.055, 0.055, -0.2, -0.045, "CannonBottle", 20, y=-0.062),
])
export(foam, "Foam Cannon.fbx")

turbo = collection("Turbo Nozzle")
join("Turbo Nozzle", [
    cone("Connector", 0.012, 0.012, 0.0, 0.02, "TurboTip", 12, axis="Y"),
    cone("Body", 0.017, 0.017, 0.02, 0.1, "TurboBody", 16, axis="Y"),
    cone("Ring A", 0.019, 0.019, 0.035, 0.045, "TurboTip", 16, axis="Y"),
    cone("Ring B", 0.019, 0.019, 0.075, 0.085, "TurboTip", 16, axis="Y"),
    cone("Tip", 0.017, 0.006, 0.1, 0.13, "TurboTip", 16, axis="Y"),
])
export(turbo, "Turbo Nozzle.fbx")

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Shop.blend"))
print("done")
