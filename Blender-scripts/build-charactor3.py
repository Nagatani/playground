import bpy
import bmesh
import math
from mathutils import Vector

def clear_scene():
    """シーン内の既存オブジェクトを削除"""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

def generate_minecraft_texture():
    """64x64のマイクラ標準スキンテクスチャをピクセル単位で自動生成"""
    img_name = "Minecraft_Skin_Texture"
    if img_name in bpy.data.images:
        bpy.data.images.remove(bpy.data.images[img_name])
    
    width = 64
    height = 64
    image = bpy.data.images.new(img_name, width=width, height=height, alpha=True)
    
    # カラーパレット
    C_SKIN = (0.94, 0.76, 0.62, 1.0)       # 肌
    C_SKIN_SHADOW = (0.84, 0.66, 0.52, 1.0)# 肌（影）
    C_HAIR = (0.32, 0.20, 0.12, 1.0)       # 髪（ダークブラウン）
    C_HAIR_LIGHT = (0.42, 0.28, 0.18, 1.0) # 髪（ハイライト）
    C_EYE_WHITE = (1.0, 1.0, 1.0, 1.0)     # 白目
    C_EYE_PUPIL = (0.15, 0.45, 0.85, 1.0)  # 青目
    C_MOUTH = (0.75, 0.35, 0.35, 1.0)      # 口
    C_SHIRT = (0.12, 0.65, 0.68, 1.0)      # エメラルドシアンの服
    C_SHIRT_DARK = (0.08, 0.50, 0.54, 1.0) # 服の影/襟
    C_PANTS = (0.18, 0.22, 0.45, 1.0)      # デニムブルー
    C_PANTS_DARK = (0.12, 0.15, 0.35, 1.0) # デニム影
    C_SHOES = (0.30, 0.30, 0.32, 1.0)      # 靴（スニーカー）
    
    # 64x64ピクセル配列初期化 (RGBA)
    pixels = [0.0] * (width * height * 4)

    def set_pixel(x, y, color):
        if 0 <= x < width and 0 <= y < height:
            idx = (y * width + x) * 4
            pixels[idx:idx+4] = color

    def fill_rect(x1, y1, w, h, color):
        for cy in range(y1, y1 + h):
            for cx in range(x1, x1 + w):
                set_pixel(cx, cy, color)

    # 1. 全体をクリア
    fill_rect(0, 0, 64, 64, (0, 0, 0, 0))

    # --- 頭部 (Head) ---
    # Top & Bottom (8x8)
    fill_rect(8, 56, 8, 8, C_HAIR)          # Top
    fill_rect(16, 56, 8, 8, C_SKIN_SHADOW)  # Bottom (顎下)
    # Right, Front, Left, Back (8x8 each, Y: 48..55)
    fill_rect(0, 48, 8, 8, C_HAIR)          # Right
    fill_rect(8, 48, 8, 8, C_SKIN)          # Front (Face)
    fill_rect(16, 48, 8, 8, C_HAIR)         # Left
    fill_rect(24, 48, 8, 8, C_HAIR)         # Back
    
    # 顔のディテール描画 (Front: x=8..15, y=48..55)
    # 前髪 (上部2段)
    fill_rect(8, 54, 8, 2, C_HAIR)
    fill_rect(8, 53, 2, 1, C_HAIR)
    fill_rect(14, 53, 2, 1, C_HAIR)
    # 目 (y=50, x=9..10, x=13..14)
    set_pixel(9, 50, C_EYE_WHITE)
    set_pixel(10, 50, C_EYE_PUPIL)
    set_pixel(13, 50, C_EYE_PUPIL)
    set_pixel(14, 50, C_EYE_WHITE)
    # 口 (y=48, x=11..12)
    set_pixel(11, 48, C_MOUTH)
    set_pixel(12, 48, C_MOUTH)

    # --- 胴体 (Torso) ---
    # Top/Bottom (8x4)
    fill_rect(20, 44, 8, 4, C_SHIRT)
    fill_rect(28, 44, 8, 4, C_PANTS)
    # Right, Front, Left, Back (Y: 32..43)
    fill_rect(16, 32, 4, 12, C_SHIRT)       # Right
    fill_rect(20, 32, 8, 12, C_SHIRT)       # Front
    fill_rect(28, 32, 4, 12, C_SHIRT)       # Left
    fill_rect(32, 32, 8, 12, C_SHIRT)       # Back
    # 服の襟と裾
    fill_rect(23, 42, 2, 2, C_SKIN)         # 襟元の肌見せ
    fill_rect(20, 32, 8, 2, C_SHIRT_DARK)   # 裾

    # --- 右腕 (Right Arm) ---
    fill_rect(44, 44, 4, 4, C_SHIRT)        # Top
    fill_rect(48, 44, 4, 4, C_SKIN_SHADOW)  # Bottom
    # Sides (Y: 32..43)
    fill_rect(40, 32, 16, 12, C_SHIRT)      # 袖
    fill_rect(40, 32, 16, 4, C_SKIN)        # 手首・手

    # --- 左腕 (Left Arm: 64x64レイアウト) ---
    fill_rect(36, 12, 4, 4, C_SHIRT)
    fill_rect(40, 12, 4, 4, C_SKIN_SHADOW)
    fill_rect(32, 0, 16, 12, C_SHIRT)
    fill_rect(32, 0, 16, 4, C_SKIN)

    # --- 右脚 (Right Leg) ---
    fill_rect(4, 28, 4, 4, C_PANTS)
    fill_rect(8, 28, 4, 4, C_SHOES)
    fill_rect(0, 16, 16, 12, C_PANTS)       # ズボン
    fill_rect(0, 16, 16, 3, C_SHOES)        # 靴

    # --- 左脚 (Left Leg) ---
    fill_rect(20, 12, 4, 4, C_PANTS)
    fill_rect(24, 12, 4, 4, C_SHOES)
    fill_rect(16, 0, 16, 12, C_PANTS)
    fill_rect(16, 0, 16, 3, C_SHOES)

    image.pixels = pixels
    image.pack()
    return image

def create_minecraft_material(image):
    """ピクセルがクッキリ表示されるマテリアルを作成"""
    mat = bpy.data.materials.new(name="Mat_Minecraft_Skin")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs['Roughness'].default_value = 0.8
    bsdf.inputs['Specular IOR Level' if 'Specular IOR Level' in bsdf.inputs else 'Specular'].default_value = 0.1
    
    tex_node = nodes.new('ShaderNodeTexImage')
    tex_node.image = image
    tex_node.interpolation = 'Closest'  # ピクセルアートをくっきり表示！
    
    links.new(tex_node.outputs['Color'], bsdf.inputs['Base Color'])
    return mat

def build_minecraft_avatar():
    clear_scene()
    skin_img = generate_minecraft_texture()
    skin_mat = create_minecraft_material(skin_img)
    parts = []

    vertex_count = 0
    eye_l_indices = []
    eye_r_indices = []
    mouth_indices = []

    def create_voxel_box(name, size, location, uv_box, bone_group):
        """直方体を作成し、マイクラ標準のUV座標を自動マッピング"""
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
        obj = bpy.context.active_object
        obj.name = name
        obj.scale = size
        bpy.ops.object.transform_apply(scale=True)
        obj.data.materials.append(skin_mat)

        # UVマッピング適用 (標準64x64スキン対応)
        # uv_box: dict(top, bottom, front, back, left, right) 各(u_min, v_min, w, h)
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        uv_layer = bm.loops.layers.uv.verify()

        # キューブの各面に対する法線とUV領域の対応
        for face in bm.faces:
            n = face.normal
            if n.z > 0.5:    rect = uv_box['top']
            elif n.z < -0.5: rect = uv_box['bottom']
            elif n.y < -0.5: rect = uv_box['front']
            elif n.y > 0.5:  rect = uv_box['back']
            elif n.x > 0.5:  rect = uv_box['left']
            elif n.x < -0.5: rect = uv_box['right']
            else: rect = (0, 0, 1, 1)

            u0, v0, uw, vh = rect[0]/64.0, rect[1]/64.0, rect[2]/64.0, rect[3]/64.0
            for loop in face.loops:
                v = loop.vert.co - Vector(location)
                # 簡易UV投影
                if abs(n.z) > 0.5:
                    u = u0 + (v.x / size[0] + 0.5) * uw
                    v_coord = v0 + (v.y / size[1] + 0.5) * vh
                elif abs(n.y) > 0.5:
                    u = u0 + (v.x / size[0] + 0.5) * uw
                    v_coord = v0 + (v.z / size[2] + 0.5) * vh
                else:
                    u = u0 + (v.y / size[1] + 0.5) * uw
                    v_coord = v0 + (v.z / size[2] + 0.5) * vh
                loop[uv_layer].uv = (u, v_coord)

        bm.to_mesh(obj.data)
        bm.free()
        parts.append((obj, bone_group))
        return obj

    def create_feature_pixel(name, size, location, color_mat, part_type):
        """表情用のピクセルメッシュを作成"""
        nonlocal vertex_count
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
        obj = bpy.context.active_object
        obj.name = name
        obj.scale = size
        bpy.ops.object.transform_apply(scale=True)
        if color_mat:
            obj.data.materials.append(color_mat)

        num_v = len(obj.data.vertices)
        v_indices = list(range(vertex_count, vertex_count + num_v))
        vertex_count += num_v

        if part_type == "eye_l": eye_l_indices.extend(v_indices)
        elif part_type == "eye_r": eye_r_indices.extend(v_indices)
        elif part_type == "mouth": mouth_indices.extend(v_indices)

        parts.append((obj, "Head"))
        return obj

    # ==========================================
    # 1. 頭部 (Head: 0.4 x 0.4 x 0.4 = 8x8x8 px)
    # ==========================================
    head_uv = {
        'top': (8, 56, 8, 8), 'bottom': (16, 56, 8, 8),
        'front': (8, 48, 8, 8), 'back': (24, 48, 8, 8),
        'left': (16, 48, 8, 8), 'right': (0, 48, 8, 8)
    }
    create_voxel_box("Head", (0.40, 0.40, 0.40), (0, 0, 1.45), head_uv, "Head")
    vertex_count += 8  # キューブ頂点数

    # 表情用ピクセル（目・口）
    # 発光/表情用マテリアル
    m_eye = bpy.data.materials.new(name="Mat_Eye_Pixel")
    m_eye.use_nodes = True
    m_eye.node_tree.nodes.get("Principled BSDF").inputs['Base Color'].default_value = (0.15, 0.45, 0.85, 1.0)
    
    m_mouth = bpy.data.materials.new(name="Mat_Mouth_Pixel")
    m_mouth.use_nodes = True
    m_mouth.node_tree.nodes.get("Principled BSDF").inputs['Base Color'].default_value = (0.75, 0.35, 0.35, 1.0)

    # 左目・右目・口 (前面 Y=-0.20 からわずかに前に配置 Y=-0.203)
    create_feature_pixel("Eye_L", (0.09, 0.005, 0.05), (0.10, -0.203, 1.45), m_eye, "eye_l")
    create_feature_pixel("Eye_R", (0.09, 0.005, 0.05), (-0.10, -0.203, 1.45), m_eye, "eye_r")
    create_feature_pixel("Mouth", (0.10, 0.005, 0.04), (0, -0.203, 1.35), m_mouth, "mouth")

    # ==========================================
    # 2. 胴体 (Torso: 0.4 x 0.2 x 0.6 = 8x12x4 px)
    # 胸 (Chest) と 腰 (Hips) に分割
    # ==========================================
    chest_uv = {
        'top': (20, 44, 8, 4), 'bottom': (28, 44, 8, 4),
        'front': (20, 38, 8, 6), 'back': (32, 38, 8, 6),
        'left': (28, 38, 4, 6), 'right': (16, 38, 4, 6)
    }
    create_voxel_box("Chest", (0.40, 0.20, 0.35), (0, 0, 1.075), chest_uv, "Chest")
    vertex_count += 8

    hips_uv = {
        'top': (20, 38, 8, 4), 'bottom': (28, 44, 8, 4),
        'front': (20, 32, 8, 6), 'back': (32, 32, 8, 6),
        'left': (28, 32, 4, 6), 'right': (16, 32, 4, 6)
    }
    create_voxel_box("Hips", (0.40, 0.20, 0.25), (0, 0, 0.775), hips_uv, "Hips")
    vertex_count += 8

    # ==========================================
    # 3. 腕 (Arms: 0.2 x 0.2 x 0.6 = 4x12x4 px)
    # Tポーズで上腕・前腕に分割
    # ==========================================
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        u_base = 32 if side == 'L' else 40
        arm_top_uv = {
            'top': (u_base + 4, 44, 4, 4), 'bottom': (u_base + 8, 44, 4, 4),
            'front': (u_base + 4, 38, 4, 6), 'back': (u_base + 12, 38, 4, 6),
            'left': (u_base + 8, 38, 4, 6), 'right': (u_base, 38, 4, 6)
        }
        create_voxel_box(f"UpperArm_{side}", (0.28, 0.18, 0.18), (sign * 0.34, 0, 1.15), arm_top_uv, f"{b_side}UpperArm")
        vertex_count += 8

        arm_btm_uv = {
            'top': (u_base + 4, 38, 4, 4), 'bottom': (u_base + 8, 44, 4, 4),
            'front': (u_base + 4, 32, 4, 6), 'back': (u_base + 12, 32, 4, 6),
            'left': (u_base + 8, 32, 4, 6), 'right': (u_base, 32, 4, 6)
        }
        create_voxel_box(f"LowerArm_{side}", (0.28, 0.18, 0.18), (sign * 0.62, 0, 1.15), arm_btm_uv, f"{b_side}LowerArm")
        vertex_count += 8

    # ==========================================
    # 4. 脚 (Legs: 0.2 x 0.2 x 0.6 = 4x12x4 px)
    # 太もも・すねに分割
    # ==========================================
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        u_base = 16 if side == 'L' else 0
        leg_top_uv = {
            'top': (u_base + 4, 28, 4, 4), 'bottom': (u_base + 8, 28, 4, 4),
            'front': (u_base + 4, 22, 4, 6), 'back': (u_base + 12, 22, 4, 6),
            'left': (u_base + 8, 22, 4, 6), 'right': (u_base, 22, 4, 6)
        }
        create_voxel_box(f"UpperLeg_{side}", (0.19, 0.19, 0.32), (sign * 0.10, 0, 0.49), leg_top_uv, f"{b_side}UpperLeg")
        vertex_count += 8

        leg_btm_uv = {
            'top': (u_base + 4, 22, 4, 4), 'bottom': (u_base + 8, 28, 4, 4),
            'front': (u_base + 4, 16, 4, 6), 'back': (u_base + 12, 16, 4, 6),
            'left': (u_base + 8, 16, 4, 6), 'right': (u_base, 16, 4, 6)
        }
        create_voxel_box(f"LowerLeg_{side}", (0.19, 0.19, 0.32), (sign * 0.10, 0, 0.17), leg_btm_uv, f"{b_side}LowerLeg")
        vertex_count += 8

    # ==========================================
    # 5. メッシュ結合 ＆ フラットシェード
    # ==========================================
    bpy.ops.object.select_all(action='DESELECT')
    for obj, bg in parts:
        vg = obj.vertex_groups.new(name=bg)
        all_indices = [v.index for v in obj.data.vertices]
        vg.add(all_indices, 1.0, 'REPLACE')
        obj.select_set(True)
    
    bpy.context.view_layer.objects.active = parts[0][0]
    bpy.ops.object.join()
    avatar_mesh = bpy.context.active_object
    avatar_mesh.name = "Minecraft_Avatar_Mesh"
    bpy.ops.object.shade_flat()

    # ==========================================
    # 6. 表情シェイプキー
    # ==========================================
    avatar_mesh.shape_key_add(name="Basis")

    def add_sk(name, mutator):
        sk = avatar_mesh.shape_key_add(name=name)
        for idx in range(len(avatar_mesh.data.vertices)):
            orig_co = avatar_mesh.data.vertices[idx].co
            new_co = mutator(idx, Vector(orig_co))
            sk.data[idx].co = new_co

    # リップシンク A (口が四角く縦に開く)
    def mut_a(i, co):
        if i in mouth_indices:
            dz = (co.z - 1.35) * 3.0
            return Vector((co.x, co.y, 1.35 + dz))
        return co
    add_sk("A", mut_a)
    add_sk("Fcl_MTH_A", mut_a)

    # リップシンク I
    def mut_i(i, co):
        if i in mouth_indices:
            dx = co.x * 1.5
            dz = (co.z - 1.35) * 0.4
            return Vector((dx, co.y, 1.35 + dz))
        return co
    add_sk("I", mut_i)
    add_sk("Fcl_MTH_I", mut_i)

    # リップシンク U
    def mut_u(i, co):
        if i in mouth_indices:
            dx = co.x * 0.4
            dz = (co.z - 1.35) * 1.6
            return Vector((dx, co.y, 1.35 + dz))
        return co
    add_sk("U", mut_u)
    add_sk("Fcl_MTH_U", mut_u)

    add_sk("E", mut_i)
    add_sk("Fcl_MTH_E", mut_i)
    add_sk("O", mut_a)
    add_sk("Fcl_MTH_O", mut_a)

    # まばたき Blink
    def mut_blink(i, co):
        if i in eye_l_indices or i in eye_r_indices:
            dz = (co.z - 1.45) * 0.08
            return Vector((co.x, co.y, 1.45 + dz))
        return co
    add_sk("Blink", mut_blink)
    add_sk("Fcl_EYE_Blink", mut_blink)

    def mut_blink_l(i, co):
        if i in eye_l_indices:
            dz = (co.z - 1.45) * 0.08
            return Vector((co.x, co.y, 1.45 + dz))
        return co
    add_sk("Blink_L", mut_blink_l)
    add_sk("Fcl_EYE_Blink_L", mut_blink_l)

    def mut_blink_r(i, co):
        if i in eye_r_indices:
            dz = (co.z - 1.45) * 0.08
            return Vector((co.x, co.y, 1.45 + dz))
        return co
    add_sk("Blink_R", mut_blink_r)
    add_sk("Fcl_EYE_Blink_R", mut_blink_r)

    # 表情 Joy
    def mut_joy(i, co):
        if i in eye_l_indices or i in eye_r_indices:
            dx = abs(co.x - 0.10) if i in eye_l_indices else abs(co.x + 0.10)
            offset = 0.03 if dx > 0.02 else 0.0
            return Vector((co.x, co.y, 1.46 - offset))
        return co
    add_sk("Joy", mut_joy)
    add_sk("Fcl_ALL_Joy", mut_joy)

    # ==========================================
    # 7. Humanoid アーマチュア (Rig)
    # ==========================================
    bpy.ops.object.armature_add(location=(0, 0, 0))
    arm_obj = bpy.context.active_object
    arm_obj.name = "Minecraft_Avatar_Armature"
    arm_data = arm_obj.data
    arm_data.name = "Minecraft_Avatar_Rig"

    bpy.ops.object.mode_set(mode='EDIT')
    edit_bones = arm_data.edit_bones

    for b in edit_bones:
        edit_bones.remove(b)

    def add_bone(name, head, tail, parent=None):
        b = edit_bones.new(name)
        b.head = head
        b.tail = tail
        if parent:
            b.parent = edit_bones[parent]
            b.use_connect = False
        return b

    # 体幹
    add_bone("Hips", (0, 0, 0.65), (0, 0, 0.90))
    add_bone("Spine", (0, 0, 0.90), (0, 0, 1.05), "Hips")
    add_bone("Chest", (0, 0, 1.05), (0, 0, 1.25), "Spine")
    add_bone("Neck", (0, 0, 1.25), (0, 0, 1.30), "Chest")
    add_bone("Head", (0, 0, 1.30), (0, 0, 1.65), "Neck")

    # 腕
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        add_bone(f"{b_side}UpperArm", (sign * 0.20, 0, 1.15), (sign * 0.48, 0, 1.15), "Chest")
        add_bone(f"{b_side}LowerArm", (sign * 0.48, 0, 1.15), (sign * 0.76, 0, 1.15), f"{b_side}UpperArm")
        add_bone(f"{b_side}Hand", (sign * 0.76, 0, 1.15), (sign * 0.85, 0, 1.15), f"{b_side}LowerArm")

    # 脚
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        add_bone(f"{b_side}UpperLeg", (sign * 0.10, 0, 0.65), (sign * 0.10, 0, 0.33), "Hips")
        add_bone(f"{b_side}LowerLeg", (sign * 0.10, 0, 0.33), (sign * 0.10, 0, 0.05), f"{b_side}UpperLeg")
        add_bone(f"{b_side}Foot", (sign * 0.10, 0, 0.05), (sign * 0.10, -0.10, 0.0), f"{b_side}LowerLeg")

    bpy.ops.object.mode_set(mode='OBJECT')

    mod = avatar_mesh.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = arm_obj
    avatar_mesh.parent = arm_obj

    print("=== マイクラ風ボクセルアバター＆テクスチャ生成完了！ ===")

build_minecraft_avatar()