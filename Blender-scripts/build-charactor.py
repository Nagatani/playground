import bpy
import bmesh
import math
from mathutils import Vector, Matrix

def clear_scene():
    """既存のオブジェクトを削除してクリーンな状態にします"""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

def create_materials():
    """レトロロボット用の各種マテリアルを作成"""
    mats = {}
    
    # 1. メインボディ (レトロ・スチールブルー)
    m_body = bpy.data.materials.new(name="Mat_Retro_Body")
    m_body.use_nodes = True
    bsdf = m_body.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.22, 0.45, 0.55, 1.0)
        bsdf.inputs['Metallic'].default_value = 0.7
        bsdf.inputs['Roughness'].default_value = 0.35
    mats['body'] = m_body

    # 2. サブボディ / フレーム (ダークスチール)
    m_dark = bpy.data.materials.new(name="Mat_Retro_Dark")
    m_dark.use_nodes = True
    bsdf = m_dark.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.12, 0.13, 0.15, 1.0)
        bsdf.inputs['Metallic'].default_value = 0.85
        bsdf.inputs['Roughness'].default_value = 0.4
    mats['dark'] = m_dark

    # 3. 関節 (真鍮ゴールド)
    m_brass = bpy.data.materials.new(name="Mat_Retro_Brass")
    m_brass.use_nodes = True
    bsdf = m_brass.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.85, 0.65, 0.20, 1.0)
        bsdf.inputs['Metallic'].default_value = 0.9
        bsdf.inputs['Roughness'].default_value = 0.25
    mats['brass'] = m_brass

    # 4. モニター画面 (暗い液晶スクリーン)
    m_screen = bpy.data.materials.new(name="Mat_Retro_Screen")
    m_screen.use_nodes = True
    bsdf = m_screen.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.04, 0.06, 0.08, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.1
    mats['screen'] = m_screen

    # 5. 発光目 (シアンLED)
    m_eye = bpy.data.materials.new(name="Mat_Eye_Emission")
    m_eye.use_nodes = True
    bsdf = m_eye.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.1, 0.9, 1.0, 1.0)
        if 'Emission' in bsdf.inputs:
            bsdf.inputs['Emission'].default_value = (0.1, 0.9, 1.0, 1.0)
        elif 'Emission Color' in bsdf.inputs:
            bsdf.inputs['Emission Color'].default_value = (0.1, 0.9, 1.0, 1.0)
        if 'Emission Strength' in bsdf.inputs:
            bsdf.inputs['Emission Strength'].default_value = 4.0
    mats['eye'] = m_eye

    # 6. 発光口 (グリーンLED)
    m_mouth = bpy.data.materials.new(name="Mat_Mouth_Emission")
    m_mouth.use_nodes = True
    bsdf = m_mouth.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.2, 1.0, 0.4, 1.0)
        if 'Emission Color' in bsdf.inputs:
            bsdf.inputs['Emission Color'].default_value = (0.2, 1.0, 0.4, 1.0)
        elif 'Emission' in bsdf.inputs:
            bsdf.inputs['Emission'].default_value = (0.2, 1.0, 0.4, 1.0)
        if 'Emission Strength' in bsdf.inputs:
            bsdf.inputs['Emission Strength'].default_value = 3.0
    mats['mouth'] = m_mouth

    # 7. ボタン赤
    m_red = bpy.data.materials.new(name="Mat_Button_Red")
    m_red.use_nodes = True
    bsdf = m_red.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.9, 0.15, 0.15, 1.0)
        if 'Emission Color' in bsdf.inputs:
            bsdf.inputs['Emission Color'].default_value = (0.9, 0.15, 0.15, 1.0)
            bsdf.inputs['Emission Strength'].default_value = 2.0
    mats['red'] = m_red

    # 8. ボタン黄
    m_yel = bpy.data.materials.new(name="Mat_Button_Yellow")
    m_yel.use_nodes = True
    bsdf = m_yel.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.95, 0.8, 0.1, 1.0)
        if 'Emission Color' in bsdf.inputs:
            bsdf.inputs['Emission Color'].default_value = (0.95, 0.8, 0.1, 1.0)
            bsdf.inputs['Emission Strength'].default_value = 2.0
    mats['yellow'] = m_yel

    return mats

def build_retro_robot():
    clear_scene()
    mats = create_materials()
    
    parts = []

    def create_cube_part(name, size, location, mat, bone_group):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
        obj = bpy.context.active_object
        obj.name = name
        obj.scale = size
        bpy.ops.object.transform_apply(scale=True)
        if mat:
            obj.data.materials.append(mat)
        parts.append((obj, bone_group))
        return obj

    def create_cylinder_part(name, radius, depth, location, rotation, mat, bone_group):
        bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=depth, location=location, rotation=rotation)
        obj = bpy.context.active_object
        obj.name = name
        if mat:
            obj.data.materials.append(mat)
        parts.append((obj, bone_group))
        return obj

    def create_sphere_part(name, radius, location, mat, bone_group):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=location)
        obj = bpy.context.active_object
        obj.name = name
        if mat:
            obj.data.materials.append(mat)
        parts.append((obj, bone_group))
        return obj

    # --- 1. 頭部 (Head: Z = 1.35 ~ 1.65) ---
    # 頭メイン（角丸風箱）
    head_box = create_cube_part("Head_Main", (0.36, 0.32, 0.28), (0, 0, 1.50), mats['body'], "Head")
    # モニター画面（少し窪んだ前面）
    create_cube_part("Head_Screen", (0.28, 0.02, 0.20), (0, -0.155, 1.50), mats['screen'], "Head")
    
    # 目（左・右）
    create_cylinder_part("Head_Eye_L", 0.035, 0.015, (0.07, -0.165, 1.53), (math.radians(90), 0, 0), mats['eye'], "Head")
    create_cylinder_part("Head_Eye_R", 0.035, 0.015, (-0.07, -0.165, 1.53), (math.radians(90), 0, 0), mats['eye'], "Head")
    
    # 口（スピーカースリット / LED）
    create_cube_part("Head_Mouth", (0.12, 0.015, 0.025), (0, -0.165, 1.43), mats['mouth'], "Head")
    
    # アンテナ（支柱＋トップ赤球体）
    create_cylinder_part("Antenna_Pole", 0.01, 0.12, (0, 0, 1.70), (0, 0, 0), mats['brass'], "Head")
    create_sphere_part("Antenna_Ball", 0.035, (0, 0, 1.77), mats['red'], "Head")
    
    # 耳ボルト/ダイヤル（左・右）
    create_cylinder_part("Ear_L", 0.04, 0.04, (0.19, 0, 1.50), (0, math.radians(90), 0), mats['brass'], "Head")
    create_cylinder_part("Ear_R", 0.04, 0.04, (-0.19, 0, 1.50), (0, math.radians(90), 0), mats['brass'], "Head")

    # --- 2. 首 (Neck: Z = 1.30 ~ 1.36) ---
    create_cylinder_part("Neck_Joint", 0.07, 0.06, (0, 0, 1.33), (0, 0, 0), mats['dark'], "Neck")

    # --- 3. 胸・胴体 (Chest: Z = 0.98 ~ 1.30) ---
    create_cube_part("Chest_Body", (0.42, 0.30, 0.32), (0, 0, 1.14), mats['body'], "Chest")
    # 胸のメーターパネル
    create_cube_part("Chest_Meter", (0.16, 0.02, 0.10), (0, -0.155, 1.20), mats['dark'], "Chest")
    # ボタン
    create_cylinder_part("Button_1", 0.02, 0.02, (-0.08, -0.155, 1.06), (math.radians(90), 0, 0), mats['red'], "Chest")
    create_cylinder_part("Button_2", 0.02, 0.02, (0.0, -0.155, 1.06), (math.radians(90), 0, 0), mats['yellow'], "Chest")
    create_cylinder_part("Button_3", 0.02, 0.02, (0.08, -0.155, 1.06), (math.radians(90), 0, 0), mats['mouth'], "Chest")

    # --- 4. 腹部・背骨 (Spine: Z = 0.85 ~ 0.98) ---
    create_cylinder_part("Spine_Joint", 0.12, 0.13, (0, 0, 0.915), (0, 0, 0), mats['dark'], "Spine")

    # --- 5. 腰 (Hips: Z = 0.72 ~ 0.85) ---
    create_cube_part("Hips_Body", (0.36, 0.26, 0.13), (0, 0, 0.785), mats['body'], "Hips")

    # --- 6. 腕 (左右) ---
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        # 肩関節
        create_sphere_part(f"Shoulder_Joint_{side}", 0.07, (sign * 0.28, 0, 1.22), mats['brass'], f"{b_side}UpperArm")
        # 上腕
        create_cylinder_part(f"UpperArm_{side}", 0.05, 0.18, (sign * 0.40, 0, 1.22), (0, math.radians(90), 0), mats['body'], f"{b_side}UpperArm")
        # 肘関節
        create_sphere_part(f"Elbow_Joint_{side}", 0.055, (sign * 0.52, 0, 1.22), mats['brass'], f"{b_side}LowerArm")
        # 前腕
        create_cylinder_part(f"LowerArm_{side}", 0.055, 0.18, (sign * 0.64, 0, 1.22), (0, math.radians(90), 0), mats['body'], f"{b_side}LowerArm")
        # 手首関節
        create_sphere_part(f"Wrist_Joint_{side}", 0.045, (sign * 0.76, 0, 1.22), mats['brass'], f"{b_side}Hand")
        # マジックハンド（カニ爪型）
        create_cube_part(f"Claw_Base_{side}", (0.04, 0.06, 0.06), (sign * 0.80, 0, 1.22), mats['dark'], f"{b_side}Hand")
        create_cube_part(f"Claw_Top_{side}", (0.06, 0.02, 0.02), (sign * 0.84, 0, 1.25), mats['brass'], f"{b_side}Hand")
        create_cube_part(f"Claw_Btm_{side}", (0.06, 0.02, 0.02), (sign * 0.84, 0, 1.19), mats['brass'], f"{b_side}Hand")

    # --- 7. 脚 (左右) ---
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        # 股関節
        create_sphere_part(f"Hip_Joint_{side}", 0.065, (sign * 0.12, 0, 0.70), mats['brass'], f"{b_side}UpperLeg")
        # 太もも
        create_cylinder_part(f"UpperLeg_{side}", 0.055, 0.24, (sign * 0.12, 0, 0.55), (0, 0, 0), mats['body'], f"{b_side}UpperLeg")
        # 膝関節
        create_sphere_part(f"Knee_Joint_{side}", 0.06, (sign * 0.12, 0, 0.40), mats['brass'], f"{b_side}LowerLeg")
        # すね
        create_cylinder_part(f"LowerLeg_{side}", 0.06, 0.24, (sign * 0.12, 0, 0.25), (0, 0, 0), mats['body'], f"{b_side}LowerLeg")
        # 足首関節
        create_sphere_part(f"Ankle_Joint_{side}", 0.05, (sign * 0.12, 0, 0.10), mats['brass'], f"{b_side}Foot")
        # 足ブロック
        create_cube_part(f"Foot_{side}", (0.13, 0.22, 0.08), (sign * 0.12, -0.04, 0.04), mats['dark'], f"{b_side}Foot")

    # --- 8. 全パーツを1つのメッシュに結合し、頂点グループ（ウェイト）を設定 ---
    bpy.ops.object.select_all(action='DESELECT')
    for obj, bg in parts:
        vg = obj.vertex_groups.new(name=bg)
        all_indices = [v.index for v in obj.data.vertices]
        vg.add(all_indices, 1.0, 'REPLACE')
        obj.select_set(True)
    
    bpy.context.view_layer.objects.active = parts[0][0]
    bpy.ops.object.join()
    robot_mesh = bpy.context.active_object
    robot_mesh.name = "Retro_Robot_Mesh"

    # スムーズシェード適用
    bpy.ops.object.shade_smooth()

    # --- 9. 表情シェイプキーの作成 ---
    robot_mesh.shape_key_add(name="Basis")
    
    # 頂点インデックスの分類（目と口を検出）
    eye_l_indices = []
    eye_r_indices = []
    mouth_indices = []

    for v in robot_mesh.data.vertices:
        co = v.co
        # 目L (X > 0.02, Y < -0.15, Z > 1.48)
        if co.x > 0.02 and co.y < -0.15 and co.z > 1.48:
            eye_l_indices.append(v.index)
        # 目R (X < -0.02, Y < -0.15, Z > 1.48)
        elif co.x < -0.02 and co.y < -0.15 and co.z > 1.48:
            eye_r_indices.append(v.index)
        # 口 (Y < -0.15, 1.40 < Z < 1.46)
        elif co.y < -0.15 and 1.40 < co.z < 1.46:
            mouth_indices.append(v.index)

    def add_sk(name, mutator):
        sk = robot_mesh.shape_key_add(name=name)
        for idx in range(len(robot_mesh.data.vertices)):
            orig_co = robot_mesh.data.vertices[idx].co
            new_co = mutator(idx, Vector(orig_co))
            sk.data[idx].co = new_co

    # リップシンク: A (口が縦に大きく開く)
    def mut_a(i, co):
        if i in mouth_indices:
            dz = (co.z - 1.43) * 2.5
            return Vector((co.x, co.y, 1.43 + dz))
        return co
    add_sk("A", mut_a)
    add_sk("Fcl_MTH_A", mut_a)

    # リップシンク: I (口が横に広がる)
    def mut_i(i, co):
        if i in mouth_indices:
            dx = co.x * 1.5
            dz = (co.z - 1.43) * 0.4
            return Vector((dx, co.y, 1.43 + dz))
        return co
    add_sk("I", mut_i)
    add_sk("Fcl_MTH_I", mut_i)

    # リップシンク: U (口が小さくすぼまる)
    def mut_u(i, co):
        if i in mouth_indices:
            dx = co.x * 0.4
            dz = (co.z - 1.43) * 1.6
            return Vector((dx, co.y, 1.43 + dz))
        return co
    add_sk("U", mut_u)
    add_sk("Fcl_MTH_U", mut_u)

    # リップシンク: E (口が少し横長で平ら)
    def mut_e(i, co):
        if i in mouth_indices:
            dx = co.x * 1.2
            dz = (co.z - 1.43) * 0.7
            return Vector((dx, co.y, 1.43 + dz))
        return co
    add_sk("E", mut_e)
    add_sk("Fcl_MTH_E", mut_e)

    # リップシンク: O (口が縦長円)
    def mut_o(i, co):
        if i in mouth_indices:
            dx = co.x * 0.6
            dz = (co.z - 1.43) * 2.2
            return Vector((dx, co.y, 1.43 + dz))
        return co
    add_sk("O", mut_o)
    add_sk("Fcl_MTH_O", mut_o)

    # まばたき: Blink (両目を薄く潰す)
    def mut_blink(i, co):
        if i in eye_l_indices or i in eye_r_indices:
            dz = (co.z - 1.53) * 0.05
            return Vector((co.x, co.y, 1.53 + dz))
        return co
    add_sk("Blink", mut_blink)
    add_sk("Fcl_EYE_Blink", mut_blink)

    # まばたき: 左目
    def mut_blink_l(i, co):
        if i in eye_l_indices:
            dz = (co.z - 1.53) * 0.05
            return Vector((co.x, co.y, 1.53 + dz))
        return co
    add_sk("Blink_L", mut_blink_l)
    add_sk("Fcl_EYE_Blink_L", mut_blink_l)

    # まばたき: 右目
    def mut_blink_r(i, co):
        if i in eye_r_indices:
            dz = (co.z - 1.53) * 0.05
            return Vector((co.x, co.y, 1.53 + dz))
        return co
    add_sk("Blink_R", mut_blink_r)
    add_sk("Fcl_EYE_Blink_R", mut_blink_r)

    # 表情: Joy (目がにっこり ^ ^ 上部に弧を描く)
    def mut_joy(i, co):
        if i in eye_l_indices or i in eye_r_indices:
            # 弧を描く
            dx = abs(co.x - 0.07) if i in eye_l_indices else abs(co.x + 0.07)
            curve = (0.035 - dx) * 0.6
            dz = (co.z - 1.53) * 0.1 + curve
            return Vector((co.x, co.y, 1.53 + dz))
        return co
    add_sk("Joy", mut_joy)
    add_sk("Fcl_ALL_Joy", mut_joy)

    # 表情: Angry (目がキリッと斜めに吊り上がる)
    def mut_angry(i, co):
        if i in eye_l_indices:
            slant = (co.x - 0.07) * 0.6
            return Vector((co.x, co.y, co.z + slant))
        elif i in eye_r_indices:
            slant = (-co.x - 0.07) * 0.6
            return Vector((co.x, co.y, co.z + slant))
        return co
    add_sk("Angry", mut_angry)
    add_sk("Fcl_ALL_Angry", mut_angry)

    # 表情: Sorrow (目がハの字に下がる)
    def mut_sorrow(i, co):
        if i in eye_l_indices:
            slant = -(co.x - 0.07) * 0.6
            return Vector((co.x, co.y, co.z + slant))
        elif i in eye_r_indices:
            slant = -(-co.x - 0.07) * 0.6
            return Vector((co.x, co.y, co.z + slant))
        return co
    add_sk("Sorrow", mut_sorrow)
    add_sk("Fcl_ALL_Sorrow", mut_sorrow)

    # 表情: Surprised (目が大きく見開く)
    def mut_surprised(i, co):
        if i in eye_l_indices:
            dx = (co.x - 0.07) * 1.5 + 0.07
            dz = (co.z - 1.53) * 1.5 + 1.53
            return Vector((dx, co.y, dz))
        elif i in eye_r_indices:
            dx = (co.x + 0.07) * 1.5 - 0.07
            dz = (co.z - 1.53) * 1.5 + 1.53
            return Vector((dx, co.y, dz))
        return co
    add_sk("Surprised", mut_surprised)
    add_sk("Fcl_ALL_Surprised", mut_surprised)


    # --- 10. ボーン (Armature / Humanoid Rig) の作成 ---
    bpy.ops.object.armature_add(location=(0, 0, 0))
    arm_obj = bpy.context.active_object
    arm_obj.name = "Retro_Robot_Armature"
    arm_data = arm_obj.data
    arm_data.name = "Retro_Robot_Rig"

    bpy.ops.object.mode_set(mode='EDIT')
    edit_bones = arm_data.edit_bones

    # デフォルトボーン削除
    for b in edit_bones:
        edit_bones.remove(b)

    # ボーン作成ヘルパー
    def add_bone(name, head, tail, parent=None):
        b = edit_bones.new(name)
        b.head = head
        b.tail = tail
        if parent:
            b.parent = edit_bones[parent]
            b.use_connect = False
        return b

    # 背骨系
    add_bone("Hips", (0, 0, 0.785), (0, 0, 0.915))
    add_bone("Spine", (0, 0, 0.915), (0, 0, 1.14), "Hips")
    add_bone("Chest", (0, 0, 1.14), (0, 0, 1.33), "Spine")
    add_bone("Neck", (0, 0, 1.33), (0, 0, 1.45), "Chest")
    add_bone("Head", (0, 0, 1.45), (0, 0, 1.77), "Neck")

    # 左右の腕
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        add_bone(f"{b_side}UpperArm", (sign * 0.28, 0, 1.22), (sign * 0.52, 0, 1.22), "Chest")
        add_bone(f"{b_side}LowerArm", (sign * 0.52, 0, 1.22), (sign * 0.76, 0, 1.22), f"{b_side}UpperArm")
        add_bone(f"{b_side}Hand", (sign * 0.76, 0, 1.22), (sign * 0.90, 0, 1.22), f"{b_side}LowerArm")

    # 左右の脚
    for side, sign, b_side in [('L', 1, 'Left'), ('R', -1, 'Right')]:
        add_bone(f"{b_side}UpperLeg", (sign * 0.12, 0, 0.70), (sign * 0.12, 0, 0.40), "Hips")
        add_bone(f"{b_side}LowerLeg", (sign * 0.12, 0, 0.40), (sign * 0.12, 0, 0.10), f"{b_side}UpperLeg")
        add_bone(f"{b_side}Foot", (sign * 0.12, 0, 0.10), (sign * 0.12, -0.15, 0.0), f"{b_side}LowerLeg")

    bpy.ops.object.mode_set(mode='OBJECT')

    # メッシュにアーマチュアモディファイアをアタッチ
    mod = robot_mesh.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = arm_obj

    # 親子関係設定
    robot_mesh.parent = arm_obj

    print("=== レトロロボットの自動生成が完了しました！ ===")

build_retro_robot()