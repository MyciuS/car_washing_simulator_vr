# Builds a big 5 litre soap canister (like the car shampoo / snow foam jugs sold in car shops) and exports it
# to Assets/Models/Shop/Soap Canister.fbx. Run it with Blender in the background (from the project folder):
#   blender --background --factory-startup --python Blender/make_soap_canister.py
# Blender is Z-up, meters. Origin at the bottom middle; the label is on the front (-Y, which is +Z in Unity).
# The parts get separate materials (Canister, CanisterCap, CanisterLabel) so Unity can colour each product.
import os
import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets", "Models", "Shop")

bpy.ops.wm.read_factory_settings(use_empty=True)

MATS = {}
for name, color in {"Canister": (1.0, 0.35, 0.05, 1), "CanisterCap": (0.05, 0.05, 0.05, 1),
                    "CanisterLabel": (0.06, 0.06, 0.07, 1)}.items():
    m = bpy.data.materials.new(name)
    m.diffuse_color = color
    MATS[name] = m


def rounded_box(name, x0, x1, y0, y1, z0, z1, mat, bevel, segments=3):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    v = [bm.verts.new((x, y, z)) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
        bm.faces.new([v[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=bevel, segments=segments,
                        profile=0.5, affect="EDGES")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(MATS[mat])
    for p in mesh.polygons:
        p.use_smooth = True
    return obj


def cylinder(name, r, z0, z1, x, y, mat, segs=24, smooth=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=segs, radius=r, depth=z1 - z0, location=(x, y, (z0 + z1) / 2))
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(MATS[mat])
    for p in obj.data.polygons:
        p.use_smooth = smooth and abs(p.normal.z) < 0.5
    obj.select_set(False)
    return obj


W, D, H = 0.095, 0.06, 0.27        # half width, half depth, body height
parts = [
    rounded_box("Body", -W, W, -D, D, 0.0, H, "Canister", 0.022, 4),
    # Handle loop on the right of the top: two posts and a bar (the gap between them is the hole for the fingers).
    rounded_box("Handle Post A", 0.0, 0.026, -0.018, 0.018, H - 0.02, H + 0.06, "Canister", 0.008),
    rounded_box("Handle Post B", 0.066, 0.092, -0.018, 0.018, H - 0.02, H + 0.06, "Canister", 0.008),
    rounded_box("Handle Bar", 0.0, 0.092, -0.018, 0.018, H + 0.035, H + 0.065, "Canister", 0.01),
    # Spout on the left of the top.
    cylinder("Neck", 0.022, H - 0.01, H + 0.018, -0.052, 0.0, "Canister"),
]
body = parts[0]
bpy.ops.object.select_all(action="DESELECT")
for p in parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
body.name = "Canister"
body.data.name = "Canister"
bpy.ops.object.select_all(action="DESELECT")

# Ribbed black cap (flat sides look like grip ridges).
cap = cylinder("Cap", 0.027, H + 0.016, H + 0.05, -0.052, 0.0, "CanisterCap", 20, smooth=False)
cap_top = cylinder("Cap Top", 0.024, H + 0.05, H + 0.054, -0.052, 0.0, "CanisterCap", 20)
bpy.ops.object.select_all(action="DESELECT")
cap.select_set(True)
cap_top.select_set(True)
bpy.context.view_layer.objects.active = cap
bpy.ops.object.join()
cap.name = "Cap"
bpy.ops.object.select_all(action="DESELECT")

# Label: a thin rounded plate on the front.
rounded_box("Label", -0.078, 0.078, -D - 0.003, -D + 0.004, 0.035, 0.225, "CanisterLabel", 0.003, 2)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Soap Canister.fbx"), use_selection=True, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         bake_space_transform=True, object_types={"MESH"}, mesh_smooth_type="FACE",
                         add_leaf_bones=False, bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Soap Canister.blend"))
print("exported Soap Canister.fbx")
