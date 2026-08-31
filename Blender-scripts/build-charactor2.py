import bpy
import math
from mathutils import Vector

def clear_scene():
    """シーン内の既存オブジェクトを削除"""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

def create_materials():
    """レトロ・ボクセル風マテリアルを作成"""
    mats = {}
    
    # 1. メインボディ (レトロ水色)
    m_body = bpy.data.materials.new(name="Mat_Box_Body")
    m_body.use_nodes = True
    bsdf = m_body.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.28, 0.52, 0.62, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.6
        bsdf.inputs['Metallic'].default_value = 0.1
    mats['body'] = m_body

    # 2. ダークフレーム (濃いグレー)
    m_dark = bpy.data.materials.new(name="Mat_Box_Dark")
    m_dark.use_nodes = True
    bsdf = m_dark.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.15, 0.16, 0.18, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.7
    mats['dark'] = m_dark

    # 3. 関節・アクセント (黄色/オレンジ)
    m_accent = bpy.data.materials.new(name="Mat_Box_Yellow")
    m_accent.use_nodes = True
    bsdf = m_accent.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.92, 0.70, 0.15, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.5
    mats['yellow'] = m_accent

    # 4. 発光ピクセル目 (黄色LED)
    m_eye = bpy.data.materials.new(name="Mat_Pixel_Eye")
    m_eye.use_nodes = True
    bsdf = m_eye.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (1.0, 0.9, 0.2, 1.0)
        if 'Emission Color' in bsdf.inputs:
            bsdf.inputs['Emission Color'].default_value = (1.0, 0.9, 0.2, 1.0)
            bsdf.inputs['Emission Strength'].default_value = 3.0
        elif 'Emission' in bsdf.inputs:
            bsdf.inputs['Emission'].default_value = (1.0, 0.9, 0.2, 1.0)
    mats['eye'] = m_eye

    # 5. 発光ピクセル口 (赤/オレンジLED)
    m_mouth = bpy.data.materials.new(name="Mat_Pixel_Mouth")
    m_mouth.use_nodes = True
    bsdf = m_mouth.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.95, 0.25, 0.1, 1.0)
        if 'Emission Color' in bsdf.inputs:
            bsdf.inputs['Emission Color'].default_value = (0.95, 0.25, 0.1, 1.0)
            bsdf.inputs['Emission Strength'].default_value = 2.5
        elif 'Emission' in bsdf.inputs:
            bsdf.inputs['Emission'].default_value = (0.95, 0.25, 0.1, 1.0)
    mats['mouth'] = m_mouth

    # 6. 赤ボタン
    m_red = bpy.data.materials.new(name="Mat_Box_Red")
    m_red.use_nodes = True
    bsdf = m_red.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.85, 0.15, 0.15, 1.0)
    mats['red'] = m_red

    return mats

def build_minecraft_style_robot():
    clear_scene()
    mats = create_materials()
    parts = []

    def create_block(name, size, location, mat, bone_group):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
        obj = bpy.context.active_object
        obj.name = name
        obj.scale = size
        bpy.ops.object.transform_apply(scale=True)
        if mat:
            obj.data.materials.append(mat)
        parts.append((obj, bone_group))
        return obj

    # ==========================================
    # 1. 頭部 (Head)
    # ==========================================
    # 頭メインキューブ (0.4 x 0.4 x 0.4)
    create_block("Head_Main", (0.42, 0.40, 0.38), (0, 0, 1.48), mats['body'], "Head")
    
    # 耳ブロック（左右）
    create_block("Ear_L", (0.06, 0.12, 0.12), (0.24, 0, 1.48), mats['dark'], "Head")
    create_block("Ear_R", (0.06, 0.12, 0.12), (-0.24, 0, 1.48), mats['dark'], "Head")

    # アンテナ（直方体の棒＋トップの四角ブロック）
    create_block("Antenna_Pole", (0.04, 0.04, 0.16), (0, 0, 1.74), mats['dark'], "Head")
    create_block("Antenna_Top", (0.08, 0.08, 0.08), (0, 0, 1.84), mats['red'], "Head")

    # ピクセル目（左・右: 正面に貼り付いた薄い直方体）
    create_block("Eye_L", (0.08, 0.02, 0.08), (0.10, -0.21, 1.52), mats['eye'], "Head")
    create_block("Eye_R", (0.08, 0.02, 0.08), (-0.10, -0.21, 1.52), mats['eye'], "Head")

    # ピクセル口（薄い直方体バー）
    create_block("Mouth", (0.18, 0.02, 0.05), (0, -0.21, 1.38), mats['mouth'], "Head")

    # ==========================================
    # 2. 首 (Neck)
    # ==========================================
    create_block("Neck_Block", (0.16, 0.16, 0.08), (0, 0, 1.25), mats['dark'], "Neck")

    # ==========================================
    # 3. 胸・胴体 (Chest & Spine)
    # ==========================================
    # 胸ブロック
    create_block("Chest_Block", (0.44, 0.28, 0.32), (0, 0, 1.05), mats['body'], "Chest")
    
    # 胸のピクセルボタン
    create_block("Btn_1", (0.05, 0.02, 0.05), (-0.10, -0.15, 1.08), mats['red'], "Chest")
    create_block("Btn_2", (0.05, 0.02, 0.05), (0.0, -0.15, 1.08), mats['yellow'], "Chest")
    create_block("Btn_3", (0.05, 0.02, 0.05), (0.10, -0.15, 1.08), mats['dark'], "Chest")

    # お腹 (Spine)
    create_block("Spine_Block", (0.36, 0.24, 0.12), (0, 0, 0.83), mats['dark'], "Spine")

    # ==========================================
    # 4. 腰 (Hips)
    # ==========================================
    create_block("Hips_Block", (0.40, 0.26, 0.14), (0, 0, 0.70), mats['body'], "Hips")

    # ==========================================
    # 5. 腕 (左右: Tポーズの四角いアーム)
    # ==========================================
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        # 肩関節ブロック
        create_block(f"Shoulder_{side}", (0.08, 0.14, 0.14), (sign * 0.26, 0, 1.12), mats['yellow'], f"{b_side}UpperArm")
        # 上腕
        create_block(f"UpperArm_{side}", (0.22, 0.13, 0.13), (sign * 0.41, 0, 1.12), mats['body'], f"{b_side}UpperArm")
        # 前腕
        create_block(f"LowerArm_{side}", (0.22, 0.14, 0.14), (sign * 0.63, 0, 1.12), mats['body'], f"{b_side}LowerArm")
        # 手（角張ったブロックハンド）
        create_block(f"Hand_{side}", (0.10, 0.15, 0.15), (sign * 0.79, 0, 1.12), mats['dark'], f"{b_side}Hand")

    # ==========================================
    # 6. 脚 (左右: 直立した四角いレッグ)
    # ==========================================
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        # 股関節ブロック
        create_block(f"HipJoint_{side}", (0.12, 0.12, 0.06), (sign * 0.11, 0, 0.60), mats['yellow'], f"{b_side}UpperLeg")
        # 太もも
        create_block(f"UpperLeg_{side}", (0.14, 0.15, 0.22), (sign * 0.11, 0, 0.46), mats['body'], f"{b_side}UpperLeg")
        # すね
        create_block(f"LowerLeg_{side}", (0.15, 0.16, 0.22), (sign * 0.11, 0, 0.24), mats['body'], f"{b_side}LowerLeg")
        # 足（少し前に出た四角い靴）
        create_block(f"Foot_{side}", (0.16, 0.22, 0.12), (sign * 0.11, -0.03, 0.07), mats['dark'], f"{b_side}Foot")

    # ==========================================
    # 7. メッシュ結合 ＆ フラットシェード
    # ==========================================
    bpy.ops.object.select_all(action='DESELECT')
    for obj, bg in parts:
        vg = obj.vertex_groups.new(name=bg)
        all_indices = [v.index for v in obj.data.vertices]
        vg.add(all_indices, 1.0, 'REPLACE')
        obj.select_set(True)
    
    bpy.context.view_layer.objects.active = parts[0][0]
    bpy.ops.object.join()
    robot_mesh = bpy.context.active_object
    robot_mesh.name = "Retro_Voxel_Robot"

    # カクカクしたポリゴン感を出すためフラットシェードを適用
    bpy.ops.object.shade_flat()

    # ==========================================
    # 8. 表情シェイプキー（ピクセル変形）
    # ==========================================
    robot_mesh.shape_key_add(name="Basis")
    
    eye_l_indices = []
    eye_r_indices = []
    mouth_indices = []

    for v in robot_mesh.data.vertices:
        co = v.co
        # 左目 (X > 0.04, Y < -0.20, Z > 1.45)
        if co.x > 0.04 and co.y < -0.20 and co.z > 1.45:
            eye_l_indices.append(v.index)
        # 右目 (X < -0.04, Y < -0.20, Z > 1.45)
        elif co.x < -0.04 and co.y < -0.20 and co.z > 1.45:
            eye_r_indices.append(v.index)
        # 口 (Y < -0.20, 1.34 < Z < 1.42)
        elif co.y < -0.20 and 1.34 < co.z < 1.42:
            mouth_indices.append(v.index)

    def add_sk(name, mutator):
        sk = robot_mesh.shape_key_add(name=name)
        for idx in range(len(robot_mesh.data.vertices)):
            orig_co = robot_mesh.data.vertices[idx].co
            new_co = mutator(idx, Vector(orig_co))
            sk.data[idx].co = new_co

    # リップシンク: A (口が縦に四角くパカッと開く)
    def mut_a(i, co):
        if i in mouth_indices:
            dz = (co.z - 1.38) * 3.0
            return Vector((co.x, co.y, 1.38 + dz))
        return co
    add_sk("A", mut_a)
    add_sk("Fcl_MTH_A", mut_a)

    # リップシンク: I (口が横長スリットに広がる)
    def mut_i(i, co):
        if i in mouth_indices:
            dx = co.x * 1.4
            dz = (co.z - 1.38) * 0.4
            return Vector((dx, co.y, 1.38 + dz))
        return co
    add_sk("I", mut_i)
    add_sk("Fcl_MTH_I", mut_i)

    # リップシンク: U (口が小さな四角に縮む)
    def mut_u(i, co):
        if i in mouth_indices:
            dx = co.x * 0.35
            dz = (co.z - 1.38) * 1.8
            return Vector((dx, co.y, 1.38 + dz))
        return co
    add_sk("U", mut_u)
    add_sk("Fcl_MTH_U", mut_u)

    # リップシンク: E / O
    add_sk("E", mut_i)
    add_sk("Fcl_MTH_E", mut_i)
    add_sk("O", mut_a)
    add_sk("Fcl_MTH_O", mut_a)

    # まばたき: Blink (目が細い横ラインに潰れる)
    def mut_blink(i, co):
        if i in eye_l_indices or i in eye_r_indices:
            dz = (co.z - 1.52) * 0.08
            return Vector((co.x, co.y, 1.52 + dz))
        return co
    add_sk("Blink", mut_blink)
    add_sk("Fcl_EYE_Blink", mut_blink)

    def mut_blink_l(i, co):
        if i in eye_l_indices:
            dz = (co.z - 1.52) * 0.08
            return Vector((co.x, co.y, 1.52 + dz))
        return co
    add_sk("Blink_L", mut_blink_l)
    add_sk("Fcl_EYE_Blink_L", mut_blink_l)

    def mut_blink_r(i, co):
        if i in eye_r_indices:
            dz = (co.z - 1.52) * 0.08
            return Vector((co.x, co.y, 1.52 + dz))
        return co
    add_sk("Blink_R", mut_blink_r)
    add_sk("Fcl_EYE_Blink_R", mut_blink_r)

    # 表情: Joy (にっこり四角い逆U字 / ピクセル風)
    def mut_joy(i, co):
        if i in eye_l_indices or i in eye_r_indices:
            dx = abs(co.x - 0.10) if i in eye_l_indices else abs(co.x + 0.10)
            offset = 0.04 if dx > 0.02 else 0.0
            return Vector((co.x, co.y, 1.54 - offset))
        return co
    add_sk("Joy", mut_joy)
    add_sk("Fcl_ALL_Joy", mut_joy)

    # 表情: Angry (斜めにキリッと傾く)
    def mut_angry(i, co):
        if i in eye_l_indices:
            slant = (co.x - 0.10) * 0.7
            return Vector((co.x, co.y, co.z + slant))
        elif i in eye_r_indices:
            slant = (-co.x - 0.10) * 0.7
            return Vector((co.x, co.y, co.z + slant))
        return co
    add_sk("Angry", mut_angry)
    add_sk("Fcl_ALL_Angry", mut_angry)

    # 表情: Sorrow (ハの字に困り顔)
    def mut_sorrow(i, co):
        if i in eye_l_indices:
            slant = -(co.x - 0.10) * 0.7
            return Vector((co.x, co.y, co.z + slant))
        elif i in eye_r_indices:
            slant = -(-co.x - 0.10) * 0.7
            return Vector((co.x, co.y, co.z + slant))
        return co
    add_sk("Sorrow", mut_sorrow)
    add_sk("Fcl_ALL_Sorrow", mut_sorrow)

    # ==========================================
    # 9. Humanoid ボーン (Armature)
    # ==========================================
    bpy.ops.object.armature_add(location=(0, 0, 0))
    arm_obj = bpy.context.active_object
    arm_obj.name = "Retro_Voxel_Armature"
    arm_data = arm_obj.data
    arm_data.name = "Retro_Voxel_Rig"

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
    add_bone("Hips", (0, 0, 0.70), (0, 0, 0.83))
    add_bone("Spine", (0, 0, 0.83), (0, 0, 1.05), "Hips")
    add_bone("Chest", (0, 0, 1.05), (0, 0, 1.25), "Spine")
    add_bone("Neck", (0, 0, 1.25), (0, 0, 1.35), "Chest")
    add_bone("Head", (0, 0, 1.35), (0, 0, 1.84), "Neck")

    # 腕
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        add_bone(f"{b_side}UpperArm", (sign * 0.26, 0, 1.12), (sign * 0.52, 0, 1.12), "Chest")
        add_bone(f"{b_side}LowerArm", (sign * 0.52, 0, 1.12), (sign * 0.74, 0, 1.12), f"{b_side}UpperArm")
        add_bone(f"{b_side}Hand", (sign * 0.74, 0, 1.12), (sign * 0.86, 0, 1.12), f"{b_side}LowerArm")

    # 脚
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        add_bone(f"{b_side}UpperLeg", (sign * 0.11, 0, 0.60), (sign * 0.11, 0, 0.35), "Hips")
        add_bone(f"{b_side}LowerLeg", (sign * 0.11, 0, 0.35), (sign * 0.11, 0, 0.12), f"{b_side}UpperLeg")
        add_bone(f"{b_side}Foot", (sign * 0.11, 0, 0.12), (sign * 0.11, -0.15, 0.0), f"{b_side}LowerLeg")

    bpy.ops.object.mode_set(mode='OBJECT')

    # アーマチュアバインド
    mod = robot_mesh.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = arm_obj
    robot_mesh.parent = arm_obj

    print("=== シンプル・マイクラ風レトロロボットの生成完了！ ===")

build_minecraft_style_robot()