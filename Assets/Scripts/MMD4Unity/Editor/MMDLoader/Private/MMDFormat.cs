﻿using System.Collections.Generic;
using System;
using UnityEngine;

// Reference URL:
//      http://blog.goo.ne.jp/torisu_tetosuki/e/209ad341d3ece2b1b4df24abf619d6e4
//      http://mikudan.blog120.fc2.com/blog-entry-280.html

namespace MMD
{
    namespace PMX
    {
        // PMXフォーマットクラス
        public class PMXFormat
        {
            public MetaHeader meta_header;
            public Header header;
            public VertexList vertex_list;
            public FaceVertexList face_vertex_list;
            public TextureList texture_list;
            public MaterialList material_list;
            public BoneList bone_list;
            public MorphList morph_list;
            public DisplayFrameList display_frame_list;
            public RigidbodyList rigidbody_list;
            public RigidbodyJointList rigidbody_joint_list;

            public class MetaHeader
            {
                public string path;            // フルパス
                public string name;            // 拡張子とパス抜きのファイルの名前
                public string folder;        // ファイル名抜きのパス
            }

            public class Header
            {
                public enum StringCode {
                    Utf16le,
                    Utf8,
                }
                public enum IndexSize {
                    Byte1 = 1,
                    Byte2 = 2,
                    Byte4 = 4,
                }
                public byte[] magic; // "PMX "
                public float version; // 00 00 80 3F == 1.00

                public byte dataSize;
                public StringCode encodeMethod;
                public byte additionalUV;
                public IndexSize vertexIndexSize;
                public IndexSize textureIndexSize;
                public IndexSize materialIndexSize;
                public IndexSize boneIndexSize;
                public IndexSize morphIndexSize;
                public IndexSize rigidbodyIndexSize;
                
                public string model_name;
                public string model_english_name;
                public string comment;
                public string english_comment;
            }

            public class VertexList
            {
                public Vertex[] vertex;  // 頂点データ(38bytes/頂点)
            }

            public class Vertex
            {
                public enum WeightMethod {
                    BDEF1,
                    BDEF2,
                    BDEF4,
                    SDEF,
                    QDEF,
                }
                public Vector3 pos; // x, y, z // 座標
                public Vector3 normal_vec; // nx, ny, nz // 法線ベクトル
                public Vector2 uv; // u, v // UV座標 // MMDは頂点UV
                public Vector4[] add_uv; // x,y,z,w
                public BoneWeight bone_weight;
                public float edge_magnification;
                
            }
            
            public interface BoneWeight
            {
                Vertex.WeightMethod method {get;}
                uint bone1_ref {get;}
                uint bone2_ref {get;}
                uint bone3_ref {get;}
                uint bone4_ref {get;}
                float bone1_weight {get;}
                float bone2_weight {get;}
                float bone3_weight {get;}
                float bone4_weight {get;}
                Vector3 c_value {get;}
                Vector3 r0_value {get;}
                Vector3 r1_value {get;}
            }

            public class BDEF1 : BoneWeight
            {
                public Vertex.WeightMethod method {get{return Vertex.WeightMethod.BDEF1;}}
                public uint bone1_ref {get; set;}
                public uint bone2_ref {get{return 0;}}
                public uint bone3_ref {get{return 0;}}
                public uint bone4_ref {get{return 0;}}
                public float bone1_weight {get{return 1.0f;}}
                public float bone2_weight {get{return 0.0f;}}
                public float bone3_weight {get{return 0.0f;}}
                public float bone4_weight {get{return 0.0f;}}
                public Vector3 c_value {get{return Vector3.zero;}}
                public Vector3 r0_value {get{return Vector3.zero;}}
                public Vector3 r1_value {get{return Vector3.zero;}}
            }
            public class BDEF2 : BoneWeight
            {
                public Vertex.WeightMethod method {get{return Vertex.WeightMethod.BDEF2;}}
                public uint bone1_ref {get; set;}
                public uint bone2_ref {get; set;}
                public float bone1_weight {get; set;}
                public uint bone3_ref {get{return 0;}}
                public uint bone4_ref {get{return 0;}}
                public float bone2_weight {get{return 1.0f - bone1_weight;}}
                public float bone3_weight {get{return 0.0f;}}
                public float bone4_weight {get{return 0.0f;}}
                public Vector3 c_value {get{return Vector3.zero;}}
                public Vector3 r0_value {get{return Vector3.zero;}}
                public Vector3 r1_value {get{return Vector3.zero;}}
            }
            public class BDEF4 : BoneWeight
            {
                public Vertex.WeightMethod method {get{return Vertex.WeightMethod.BDEF4;}}
                public uint bone1_ref {get; set;}
                public uint bone2_ref {get; set;}
                public uint bone3_ref {get; set;}
                public uint bone4_ref {get; set;}
                public float bone1_weight {get; set;}
                public float bone2_weight {get; set;}
                public float bone3_weight {get; set;}
                public float bone4_weight {get; set;}
                public Vector3 c_value {get{return Vector3.zero;}}
                public Vector3 r0_value {get{return Vector3.zero;}}
                public Vector3 r1_value {get{return Vector3.zero;}}
            }
            public class SDEF : BoneWeight
            {
                public Vertex.WeightMethod method {get{return Vertex.WeightMethod.SDEF;}}
                public uint bone1_ref {get; set;}
                public uint bone2_ref {get; set;}
                public float bone1_weight {get; set;}
                public Vector3 c_value {get; set;}
                public Vector3 r0_value {get; set;}
                public Vector3 r1_value {get; set;}
                public uint bone3_ref {get{return 0;}}
                public uint bone4_ref {get{return 0;}}
                public float bone2_weight {get{return 1.0f - bone1_weight;}}
                public float bone3_weight {get{return 0.0f;}}
                public float bone4_weight {get{return 0.0f;}}
            }
            public class QDEF : BoneWeight
            {
                public Vertex.WeightMethod method {get{return Vertex.WeightMethod.QDEF;}}
                public uint bone1_ref {get; set;}
                public uint bone2_ref {get; set;}
                public uint bone3_ref {get; set;}
                public uint bone4_ref {get; set;}
                public float bone1_weight {get; set;}
                public float bone2_weight {get; set;}
                public float bone3_weight {get; set;}
                public float bone4_weight {get; set;}
                public Vector3 c_value {get{return Vector3.zero;}}
                public Vector3 r0_value {get{return Vector3.zero;}}
                public Vector3 r1_value {get{return Vector3.zero;}}
            }

            // 面頂点リスト
            public class FaceVertexList
            {
                public uint[] face_vert_index; // 頂点番号(3個/面)
            }

            public class TextureList
            {
                public string[] texture_file;    // 100byte * 10個固定 
            }
            
            public class MaterialList
            {
                public Material[] material; // 材質データ(70bytes/material)
            }

            public class Material
            {
                [Flags]
                public enum Flag {
                    Reversible            = 1<< 0, //両面描画
                    CastShadow            = 1<< 1, //地面影
                    CastSelfShadow        = 1<< 2, //セルフシャドウマップへの描画
                    ReceiveSelfShadow    = 1<< 3, //セルフシャドウの描画
                    Edge                = 1<< 4, //エッジ描画
                }
                public enum SphereMode {
                    Null,        //無し
                    MulSphere,    //乗算スフィア
                    AddSphere,    //加算スフィア
                    SubTexture,    //サブテクスチャ
                }
                public string name;
                public string english_name;

                public Color diffuse_color; // dr, dg, db, da // 減衰色
                public Color specular_color; // sr, sg, sb // 光沢色
                public float specularity;
                public Color ambient_color; // mr, mg, mb // 環境色(ambient)
                public Flag flag;
                public Color edge_color; // r, g, b, a
                public float edge_size;
                public uint usually_texture_index;
                public uint sphere_texture_index;
                public SphereMode sphere_mode;
                public byte common_toon;
                public uint toon_texture_index;
                public string memo;
                public uint face_vert_count; // 面頂点数 // インデックスに変換する場合は、材質0から順に加算
            }

            public class BoneList
            {
                public Bone[] bone; // ボーンデータ(39bytes/bone)
            }

            public class Bone
            {
                [Flags]
                public enum Flag {
                    Connection                = 1<< 0, //接続先(PMD子ボーン指定)表示方法(ON:ボーンで指定、OFF:座標オフセットで指定)
                    Rotatable                = 1<< 1, //回転可能
                    Movable                    = 1<< 2, //移動可能
                    DisplayFlag                = 1<< 3, //表示
                    CanOperate                = 1<< 4, //操作可
                    IkFlag                    = 1<< 5, //IK
                    AddLocal                = 1<< 7, //ローカル付与 | 付与対象(ON:親のローカル変形量、OFF:ユーザー変形値／IKリンク／多重付与)
                    AddRotation                = 1<< 8, //回転付与
                    AddMove                    = 1<< 9, //移動付与
                    FixedAxis                = 1<<10, //軸固定
                    LocalAxis                = 1<<11, //ローカル軸
                    PhysicsTransform        = 1<<12, //物理後変形
                    ExternalParentTransform    = 1<<13, //外部親変形
                }
                public string bone_name; // ボーン名
                public string bone_english_name;
                public Vector3 bone_position;
                public uint parent_bone_index; // 親ボーン番号(ない場合はuint.MaxValue)
                public int transform_level;
                public Flag bone_flag;
                public Vector3 position_offset;
                public uint connection_index;
                public uint additional_parent_index;
                public float additional_rate;
                public Vector3 axis_vector;
                public Vector3 x_axis_vector;
                public Vector3 z_axis_vector;
                public uint key_value;
                public IK_Data ik_data;
            }

            public class IK_Data
            {
                public uint ik_bone_index; // IKボーン番号
                public uint iterations; // 再帰演算回数 // IK値1
                public float limit_angle;
                public IK_Link[] ik_link;
            }
            
            public class IK_Link
            {
                public uint target_bone_index;
                public byte angle_limit;
                public Vector3 lower_limit;
                public Vector3 upper_limit;
            }
            
            public class MorphList
            {
                public MorphData[] morph_data; // 表情データ((25+16*skin_vert_count)/skin)
            }

            public class MorphData
            {
                public enum Panel {
                    Base,
                    EyeBrow,
                    Eye,
                    Lip,
                    Other,
                }
                public enum MorphType {
                    Group,
                    Vertex,
                    Bone,
                    Uv,
                    Adduv1,
                    Adduv2,
                    Adduv3,
                    Adduv4,
                    Material,

                    Flip,
                    Impulse,
                }
                public string morph_name; //　表情名
                public string morph_english_name; //　表情英名
                public Panel handle_panel;
                public MorphType morph_type;
                public MorphOffset[] morph_offset;
            }
            
            public interface MorphOffset {};

            public class VertexMorphOffset : MorphOffset
            {
                public uint vertex_index;
                public Vector3 position_offset;
            }
            public class UVMorphOffset : MorphOffset
            {
                public uint vertex_index;
                public Vector4 uv_offset;
            }
            public class BoneMorphOffset : MorphOffset
            {
                public uint bone_index;
                public Vector3 move_value;
                public Quaternion rotate_value;
            }
            public class MaterialMorphOffset : MorphOffset
            {
                public enum OffsetMethod {
                    Mul,
                    Add,
                }
                public uint material_index;
                public OffsetMethod offset_method;
                public Color diffuse;
                public Color specular;
                public float specularity;
                public Color ambient;
                public Color edge_color;
                public float edge_size;
                public Color texture_coefficient;
                public Color sphere_texture_coefficient;
                public Color toon_texture_coefficient;
            }
            public class GroupMorphOffset : MorphOffset
            {
                public uint morph_index;
                public float morph_rate;
            }

            public class ImpulseMorphOffset : MorphOffset
            {
                public uint rigidbody_index;
                public byte local_flag;
                public Vector3 move_velocity;
                public Vector3 rotation_torque;
            }

            public class DisplayFrameList
            {
                public DisplayFrame[] display_frame;
            }
            
            public class DisplayFrame
            {
                public string display_name;
                public string display_english_name;
                public byte special_frame_flag;
                public DisplayElement[] display_element;
            }
            
            public class DisplayElement
            {
                public byte element_target;
                public uint element_target_index;
            }
            
            public class RigidbodyList
            {
                public PMX.PMXFormat.Rigidbody[] rigidbody;
            }
            
            /// <summary>
            /// 剛体
            /// </summary>
            public class Rigidbody
            {
                public enum ShapeType {
                    Sphere,        //球
                    Box,        //箱
                    Capsule,    //カプセル
                }
                public enum OperationType {
                    Static,                    //ボーン追従
                    Dynamic,                //物理演算
                    DynamicAndPosAdjust,    //物理演算(Bone位置合せ)
                }
                public string name; // 諸データ：名称 ,20byte
                public string english_name; // 諸データ：名称 ,20byte
                public uint rel_bone_index;// 諸データ：関連ボーン番号 
                public byte group_index; // 諸データ：グループ 
                public ushort ignore_collision_group;
                public ShapeType shape_type;  // 形状：タイプ(0:球、1:箱、2:カプセル)
                public Vector3 shape_size;
                public Vector3 collider_position;     // 位置：位置(x, y, z) 
                public Vector3 collider_rotation;     // 位置：回転(rad(x), rad(y), rad(z)) 
                public float weight; // 諸データ：質量 // 00 00 80 3F // 1.0
                public float position_dim; // 諸データ：移動減 // 00 00 00 00
                public float rotation_dim; // 諸データ：回転減 // 00 00 00 00
                public float recoil; // 諸データ：反発力 // 00 00 00 00
                public float friction; // 諸データ：摩擦力 // 00 00 00 00
                public OperationType operation_type; // 諸データ：タイプ(0:Bone追従、1:物理演算、2:物理演算(Bone位置合せ)) // 00 // Bone追従
            }
            
            public class RigidbodyJointList
            {
                public MMD.PMX.PMXFormat.Joint[] joint;
            }
            
            public class Joint
            {
                public enum OperationType {
                    Spring6DOF,    //スプリング6DOF
                }
                public string name;    // 20byte
                public string english_name;
                public OperationType operation_type;
                public uint rigidbody_a; // 諸データ：剛体A 
                public uint rigidbody_b; // 諸データ：剛体B 
                public Vector3 position; // 諸データ：位置(x, y, z) // 諸データ：位置合せでも設定可 
                public Vector3 rotation; // 諸データ：回転(rad(x), rad(y), rad(z)) 
                public Vector3 constrain_pos_lower; // 制限：移動1(x, y, z) 
                public Vector3 constrain_pos_upper; // 制限：移動2(x, y, z) 
                public Vector3 constrain_rot_lower; // 制限：回転1(rad(x), rad(y), rad(z)) 
                public Vector3 constrain_rot_upper; // 制限：回転2(rad(x), rad(y), rad(z)) 
                public Vector3 spring_position; // ばね：移動(x, y, z) 
                public Vector3 spring_rotation; // ばね：回転(rad(x), rad(y), rad(z)) 
            }
        }
    }

    namespace VMD
    {
        // VMDのフォーマットクラス
        public class VMDFormat
        {
            public string name;
            public string path;
            public string folder;
            
            public Header header;
            public MotionList motion_list;
            public SkinList skin_list;
            public LightList light_list;
            public CameraList camera_list;
            public SelfShadowList self_shadow_list;
            
            public class Header
            {
                public string vmd_header; // 30byte, "Vocaloid Motion Data 0002"
                public string vmd_model_name; // 20byte
            }
            
            public class MotionList
            {
                public uint motion_count;
                public Dictionary<string, List<Motion>> motion;
            }
            
            public class Motion
            {
                public string bone_name;    // 15byte
                public uint frame_no;
                public Vector3 location;
                public Quaternion rotation;
                public byte[] interpolation;    // [4][4][4], 64byte
                
                // なんか不便になりそうな気がして
                public byte GetInterpolation(int i, int j, int k)
                {
                    return this.interpolation[i*16+j*4+k];
                }
                
                public void SetInterpolation(byte val, int i, int j, int k)
                {
                    this.interpolation[i*16+j*4+k] = val;
                }
            }
            
            /// <summary>
            /// 表情リスト
            /// </summary>
            public class SkinList
            {
                public uint skin_count;
                public Dictionary<string, List<SkinData>> skin;
            }
            
            public class SkinData
            {
                public string skin_name;    // 15byte
                public uint frame_no;
                public float weight;
            }
            
            public class CameraList
            {
                public uint camera_count;
                public CameraData[] camera;
            }
            
            public class CameraData
            {
                public uint frame_no;
                public float length;
                public Vector3 location;
                public Vector3 rotation;    // オイラー角, X軸は符号が反転している
                public byte[] interpolation;    // [6][4], 24byte(未検証)
                public uint viewing_angle;
                public byte perspective;    // 0:on 1:off
                
                public byte GetInterpolation(int i, int j)
                {
                    return this.interpolation[i*6+j];
                }
                
                public void SetInterpolation(byte val, int i, int j)
                {
                    this.interpolation[i*6+j] = val;
                }
            }
            
            public class LightList
            {
                public uint light_count;
                public LightData[] light;
            }
            
            public class LightData
            {
                public uint frame_no;
                public Color rgb;    // αなし, 256
                public Vector3 location;
            }
            
            public class SelfShadowList
            {
                public uint self_shadow_count;
                public SelfShadowData[] self_shadow;
            }
            
            public class SelfShadowData
            {
                public uint frame_no;
                public byte mode; //00-02
                public float distance;    // 0.1 - (dist * 0.00001)
            }
        }
    }
}