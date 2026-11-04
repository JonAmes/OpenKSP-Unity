using System;
using System.Collections.Generic;
using System.Text;

namespace OpenKSP.Core.Modding
{
    /// <summary>
    /// Entry-type codes of the .mu binary object/component stream (MuEnum in
    /// the reference mu.py reader). Values are byte-format facts and must not
    /// change.
    /// </summary>
    public static class MuEntry
    {
        public const int Magic = 76543;           // 0x012AFF
        public const int MaxVersion = 5;

        public const int ChildTransformStart = 0;
        public const int ChildTransformEnd = 1;
        public const int Animation = 2;
        public const int MeshCollider = 3;
        public const int SphereCollider = 4;
        public const int CapsuleCollider = 5;
        public const int BoxCollider = 6;
        public const int MeshFilter = 7;
        public const int MeshRenderer = 8;
        public const int SkinnedMeshRenderer = 9;
        public const int Materials = 10;
        public const int Material = 11;           // unused by PartTools
        public const int Textures = 12;
        public const int MeshStart = 13;
        public const int MeshVerts = 14;
        public const int MeshUv = 15;
        public const int MeshUv2 = 16;
        public const int MeshNormals = 17;
        public const int MeshTangents = 18;
        public const int MeshTriangles = 19;
        public const int MeshBoneWeights = 20;
        public const int MeshBindPoses = 21;
        public const int MeshEnd = 22;
        public const int Light = 23;
        public const int TagAndLayer = 24;
        public const int MeshCollider2 = 25;
        public const int SphereCollider2 = 26;
        public const int CapsuleCollider2 = 27;
        public const int BoxCollider2 = 28;
        public const int WheelCollider = 29;
        public const int Camera = 30;
        public const int Particles = 31;
        public const int MeshVertexColors = 32;
    }

    /// <summary>Shader type codes of version &lt; 4 materials (ST_* in mu.py).</summary>
    public static class MuShaderType
    {
        public const int Custom = 0;
        public const int Diffuse = 1;
        public const int Specular = 2;
        public const int Bumped = 3;
        public const int BumpedSpecular = 4;
        public const int Emissive = 5;
        public const int EmissiveSpecular = 6;
        public const int EmissiveBumpedSpecular = 7;
        public const int AlphaCutoff = 8;
        public const int AlphaCutoffBumped = 9;
        public const int Alpha = 10;
        public const int AlphaSpecular = 11;
        public const int AlphaUnlit = 12;
        public const int Unlit = 13;
        public const int ParticlesAlphaBlended = 14;
        public const int ParticlesAdditive = 15;

        /// <summary>Shader name per type code, verbatim from mu.py ShaderNames.</summary>
        public static readonly string[] ShaderNames =
        {
            "",
            "KSP/Diffuse",
            "KSP/Specular",
            "KSP/Bumped",
            "KSP/Bumped Specular",
            "KSP/Emissive/Diffuse",
            "KSP/Emissive/Specular",
            "KSP/Emissive/Bumped Specular",
            "KSP/Alpha/Cutoff",
            "KSP/Alpha/Cutoff Bumped",
            "KSP/Alpha/Translucent",
            "KSP/Alpha/Translucent Specular",
            "KSP/Alpha/Unlit Transparent",
            "KSP/Unlit",
            "KSP/Particles/Alpha Blended",
            "KSP/Particles/Additive",
        };
    }

    public struct MuVec2
    {
        public float X;
        public float Y;

        public MuVec2(float x, float y) { X = x; Y = y; }
    }

    public struct MuVec3
    {
        public float X;
        public float Y;
        public float Z;

        public MuVec3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    public struct MuVec4
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public MuVec4(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
    }

    /// <summary>
    /// Quaternion as emitted by the reader. The .mu stores LHS components as
    /// (x, y, z, w); the reader applies mu.py's read_quaternion transform and
    /// emits (W, X, Y, Z) = (q3, -q0, -q2, -q1). The identity therefore reads
    /// (1, -0, -0, -0). The Unity-side bridge maps these four components to
    /// UnityEngine.Quaternion(X, Y, Z, W) verbatim.
    /// </summary>
    public struct MuQuat
    {
        public float W;
        public float X;
        public float Y;
        public float Z;
    }

    public sealed class MuTransform
    {
        public string Name = "";
        public MuVec3 LocalPosition;
        public MuQuat LocalRotation;
        public MuVec3 LocalScale;
    }

    public sealed class MuTagLayer
    {
        public string Tag = "";
        public int Layer;
    }

    public struct MuBoneWeight
    {
        public int Index0;
        public int Index1;
        public int Index2;
        public int Index3;
        public float Weight0;
        public float Weight1;
        public float Weight2;
        public float Weight3;
    }

    /// <summary>
    /// Mesh block (mu.py MuMesh.read). Raw little-endian values, no
    /// presentation-space conversion: vertices/normals keep file order (mu.py's
    /// y/z swap is Blender-only) and triangle windings keep file order (mu.py's
    /// reversal is Blender-only). Each submesh's triangles are flat index
    /// triplets [a,b,c, a,b,c, ...].
    /// </summary>
    public sealed class MuMesh
    {
        public int NumVertices;
        public int NumSubmeshes;
        public MuVec3[] Verts = new MuVec3[0];
        public MuVec2[]Uvs = new MuVec2[0];
        public MuVec2[] Uv2s = new MuVec2[0];
        public MuVec3[] Normals = new MuVec3[0];
        public MuVec4[] Tangents = new MuVec4[0];
        public MuBoneWeight[] BoneWeights = new MuBoneWeight[0];
        public float[][] BindPoses = new float[0][];   // each 16 floats
        public int[][] Submeshes = new int[0][];       // flat index triplets
        public MuVec4[] VertexColors = new MuVec4[0];  // bytes divided by 255
    }

    public enum MuColliderKind
    {
        Mesh,
        Sphere,
        Capsule,
        Box,
        Wheel,
    }

    public sealed class MuCollider
    {
        public MuColliderKind Kind;
        public bool IsTrigger;        // present only on *Collider2 entries
        public bool Convex;           // mesh collider
        public MuMesh? Mesh;          // mesh collider
        public float Radius;          // sphere/capsule/wheel
        public float Height;          // capsule
        public int Direction;         // capsule
        public MuVec3 Center;         // sphere/capsule/box/wheel
        public MuVec3 Size;           // box
        // Wheel collider:
        public float Mass;
        public float SuspensionDistance;
        public float Spring;
        public float Damper;
        public float SpringTargetPosition;
        public float ExtremumSlip;    // forward friction
        public float ExtremumValue;
        public float AsymptoteSlip;
        public float AsymptoteValue;
        public float ForwardStiffness;
        public float SideExtremumSlip;
        public float SideExtremumValue;
        public float SideAsymptoteSlip;
        public float SideAsymptoteValue;
        public float SidewaysStiffness;
    }

    public sealed class MuRenderer
    {
        public int CastShadows = 1;   // default 1 when version == 0
        public int ReceiveShadows = 1;
        public int[] MaterialIndices = new int[0];
    }

    public sealed class MuSkinnedMeshRenderer
    {
        public int[] MaterialIndices = new int[0];
        public MuVec3 Center;
        public MuVec3 Size;
        public int Quality;
        public int UpdateWhenOffscreen;
        public string[] Bones = new string[0];
        public MuMesh Mesh = new MuMesh();
    }

    public struct MuKey
    {
        public float Time;
        public float Value;
        public float TangentIn;
        public float TangentOut;
        public int TangentMode;   // editable, smooth, linear, stepped (0..3?)
    }

    public sealed class MuCurve
    {
        public string Path = "";
        public string Property = "";
        public int Type;          // AT_TRANSFORM 0, AT_MATERIAL 1, AT_LIGHT 2, AT_AUDIO_SOURCE 3
        public int WrapModePre;
        public int WrapModePost;
        public MuKey[] Keys = new MuKey[0];
    }

    public sealed class MuClip
    {
        public string Name = "";
        public MuVec3 LbCenter;
        public MuVec3 LbSize;
        public int WrapMode;
        public MuCurve[] Curves = new MuCurve[0];
    }

    public sealed class MuAnimation
    {
        public MuClip[] Clips = new MuClip[0];
        public string Clip = "";
        public int AutoPlay;
    }

    public sealed class MuCamera
    {
        public int ClearFlags;
        public MuVec4 BackgroundColor;
        public uint CullingMask;
        public int Orthographic;
        public float Fov;
        public float Near;
        public float Far;
        public float Depth;
    }

    public sealed class MuLight
    {
        public int Type;
        public float Intensity;
        public float Range;
        public MuVec4 Color;
        public uint CullingMask;
        public float SpotAngle;   // read only when version > 1
    }

    /// <summary>Legacy particle system block (mu.py MuParticles.read, verbatim order).</summary>
    public sealed class MuParticles
    {
        public int Emit;
        public int Shape;
        public MuVec3 Shape3D;
        public MuVec2 Shape2D;
        public float Shape1D;
        public MuVec4 Color;
        public int UseWorldSpace;           // mu.py spells this "useUorldSpace"
        public MuVec2 Size;                 // min, max
        public MuVec2 Energy;               // min, max
        public int EmissionMin;             // mu.py: emission = int(2)
        public int EmissionMax;
        public MuVec3 WorldVelocity;
        public MuVec3 LocalVelocity;
        public MuVec3 RandVelocity;
        public float EmitterVelocityScale;
        public float AngularVelocity;
        public float RandAngularVelocity;
        public int RandRotation;
        public int DoesAnimateColor;
        public MuVec4[] ColorAnimation = new MuVec4[5]; // 5 entries
        public MuVec3 WorldRotationAxis;
        public MuVec3 LocalRotationAxis;
        public float SizeGrow;
        public MuVec3 RandForce;
        public MuVec3 Force;
        public float Damping;
        public int CastShadows;
        public int ReceiveShadows;          // mu.py spells this "recieveShadows"
        public float LengthScale;
        public float VelocityScale;
        public float MaxParticleSize;
        public int ParticleRenderMode;
        public int UvAnimXTile;             // mu.py: uvAnimation = int(3)
        public int UvAnimYTile;
        public int UvAnimCycles;
        public int Count;
    }

    public struct MuMatTex
    {
        public int Index;
        public MuVec2 Scale;
        public MuVec2 Offset;
    }

    /// <summary>
    /// Material. Version >= 4 files carry a generic property list (mu.py
    /// read_material4); older files carry a shader-type-driven layout
    /// (read_material3) whose values land in the same dictionaries under the
    /// canonical Unity property names. (The step-03 research note says
    /// "v2 → material3, v5 → material4"; mu.py's actual branch is
    /// version >= 4 — identical for the two versions real files use.)
    /// </summary>
    public sealed class MuMaterial
    {
        public string Name = "";
        public int ShaderType = -1;         // material3 files only; -1 for material4
        public string ShaderName = "";
        public Dictionary<string, MuVec4> ColorProperties = new Dictionary<string, MuVec4>();
        public Dictionary<string, MuVec4> VectorProperties = new Dictionary<string, MuVec4>();
        public Dictionary<string, float> FloatProperties2 = new Dictionary<string, float>();
        public Dictionary<string, float> FloatProperties3 = new Dictionary<string, float>();
        public Dictionary<string, MuMatTex> TextureProperties = new Dictionary<string, MuMatTex>();
    }

    public sealed class MuTexture
    {
        public string Name = "";
        public int Type;    // TT_TEXTURE 0, TT_NORMAL_MAP 1
    }

    public sealed class MuObject
    {
        public MuTransform Transform = new MuTransform();
        public List<MuObject> Children = new List<MuObject>();
        public MuTagLayer? TagLayer;
        public MuCollider? Collider;
        public MuMesh? SharedMesh;              // ET_MESH_FILTER
        public MuRenderer? Renderer;
        public MuSkinnedMeshRenderer? SkinnedMeshRenderer;
        public MuAnimation? Animation;
        public MuCamera? Camera;
        public MuLight? Light;
        public MuParticles? Particles;
    }

    /// <summary>A parsed .mu file: header, object tree, file-level materials/textures.</summary>
    public sealed class MuFile
    {
        public int Version;
        public string Name = "";
        public MuObject Root = new MuObject();
        public List<MuMaterial> Materials = new List<MuMaterial>();
        public List<MuTexture> Textures = new List<MuTexture>();
    }

    /// <summary>
    /// Reader for the KSP1 .mu (PartTools "Model Binary") format — a pure-C#
    /// mirror of the reference mu.py reader's byte consumption: little-endian
    /// primitives, 7-bit varint string lengths, recursive transform tree,
    /// component entries keyed by <see cref="MuEntry"/>. Blows up with a
    /// deterministic FormatException on bad magic, unsupported version,
    /// truncated data, or an unknown code inside a mesh block. No UnityEngine,
    /// no LINQ; the tree is allocated once per load.
    /// </summary>
    public static class Mu
    {
        public static MuFile Read(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            var r = new MuBin(bytes);

            uint magic = r.ReadUInt();
            if ((int)magic != MuEntry.Magic)
                throw new FormatException(
                    "Mu: bad magic " + (int)magic + " (expected " + MuEntry.Magic + ") at offset 0");
            int version = r.ReadInt();
            if (version < 0 || version > MuEntry.MaxVersion)
                throw new FormatException(
                    "Mu: unsupported version " + version +
                    " (expected 0.." + MuEntry.MaxVersion + ") at offset 4");

            var file = new MuFile { Version = version, Name = r.ReadString() };
            file.Root = ReadObject(r, file, version);
            return file;
        }

        // mu.py MuObject.read: transform, then entry loop; unknown entry codes
        // are ignored (no payload), the loop ends on CT_END or end of data.
        private static MuObject ReadObject(MuBin r, MuFile file, int version)
        {
            var obj = new MuObject();
            obj.Transform = ReadTransform(r);
            while (true)
            {
                int entry;
                if (!r.TryReadInt(out entry))
                    break;                      // EOF ends the object (mu.py)
                if (entry == MuEntry.ChildTransformEnd)
                    break;
                else if (entry == MuEntry.ChildTransformStart)
                    obj.Children.Add(ReadObject(r, file, version));
                else if (entry == MuEntry.TagAndLayer)
                    obj.TagLayer = ReadTagLayer(r);
                else if (entry == MuEntry.MeshCollider || entry == MuEntry.MeshCollider2)
                    obj.Collider = ReadMeshCollider(r, entry == MuEntry.MeshCollider2);
                else if (entry == MuEntry.SphereCollider || entry == MuEntry.SphereCollider2)
                    obj.Collider = ReadSphereCollider(r, entry == MuEntry.SphereCollider2);
                else if (entry == MuEntry.CapsuleCollider || entry == MuEntry.CapsuleCollider2)
                    obj.Collider = ReadCapsuleCollider(r, entry == MuEntry.CapsuleCollider2);
                else if (entry == MuEntry.BoxCollider || entry == MuEntry.BoxCollider2)
                    obj.Collider = ReadBoxCollider(r, entry == MuEntry.BoxCollider2);
                else if (entry == MuEntry.WheelCollider)
                    obj.Collider = ReadWheelCollider(r);
                else if (entry == MuEntry.MeshFilter)
                    obj.SharedMesh = ReadMesh(r);
                else if (entry == MuEntry.MeshRenderer)
                    obj.Renderer = ReadRenderer(r, version);
                else if (entry == MuEntry.SkinnedMeshRenderer)
                    obj.SkinnedMeshRenderer = ReadSkinnedMeshRenderer(r, version);
                else if (entry == MuEntry.Animation)
                    obj.Animation = ReadAnimation(r);
                else if (entry == MuEntry.Camera)
                    obj.Camera = ReadCamera(r);
                else if (entry == MuEntry.Particles)
                    obj.Particles = ReadParticles(r);
                else if (entry == MuEntry.Light)
                    obj.Light = ReadLight(r, version);
                else if (entry == MuEntry.Materials)
                {
                    int count = r.ReadInt();
                    for (int i = 0; i < count; i++)
                        file.Materials.Add(ReadMaterial(r, version));
                }
                else if (entry == MuEntry.Textures)
                {
                    int count = r.ReadInt();
                    for (int i = 0; i < count; i++)
                        file.Textures.Add(ReadTexture(r));
                }
                // mu.py: unknown entry codes fall through with `pass`.
            }
            return obj;
        }

        private static MuTransform ReadTransform(MuBin r)
        {
            var t = new MuTransform();
            t.Name = r.ReadString();
            t.LocalPosition = r.ReadVec3();
            t.LocalRotation = r.ReadQuat();     // (q3, -q0, -q2, -q1), mu.py
            t.LocalScale = r.ReadVec3();
            return t;
        }

        private static MuTagLayer ReadTagLayer(MuBin r)
        {
            var tl = new MuTagLayer();
            tl.Tag = r.ReadString();
            tl.Layer = r.ReadInt();
            return tl;
        }

        // mu.py MuMesh.read: START, numVerts, numSubmeshes, entry loop to END.
        private static MuMesh ReadMesh(MuBin r)
        {
            int start = r.ReadInt();
            if (start != MuEntry.MeshStart)
                throw new FormatException(
                    "Mu: expected mesh START (" + MuEntry.MeshStart + "), got " +
                    start + " at offset " + r.Position);

            var m = new MuMesh();
            m.NumVertices = r.ReadInt();
            m.NumSubmeshes = r.ReadInt();
            while (true)
            {
                int type = r.ReadInt();
                if (type == MuEntry.MeshEnd)
                    break;
                else if (type == MuEntry.MeshVerts)
                {
                    m.Verts = new MuVec3[m.NumVertices];
                    for (int i = 0; i < m.NumVertices; i++)
                        m.Verts[i] = r.ReadVec3();
                }
                else if (type == MuEntry.MeshUv)
                {
                    m.Uvs = new MuVec2[m.NumVertices];
                    for (int i = 0; i < m.NumVertices; i++)
                        m.Uvs[i] = r.ReadVec2();
                }
                else if (type == MuEntry.MeshUv2)
                {
                    m.Uv2s = new MuVec2[m.NumVertices];
                    for (int i = 0; i < m.NumVertices; i++)
                        m.Uv2s[i] = r.ReadVec2();
                }
                else if (type == MuEntry.MeshNormals)
                {
                    m.Normals = new MuVec3[m.NumVertices];
                    for (int i = 0; i < m.NumVertices; i++)
                        m.Normals[i] = r.ReadVec3();
                }
                else if (type == MuEntry.MeshTangents)
                {
                    m.Tangents = new MuVec4[m.NumVertices];
                    for (int i = 0; i < m.NumVertices; i++)
                        m.Tangents[i] = r.ReadVec4();  // raw; mu.py's w-negation is Blender-only
                }
                else if (type == MuEntry.MeshBoneWeights)
                {
                    m.BoneWeights = new MuBoneWeight[m.NumVertices];
                    for (int i = 0; i < m.NumVertices; i++)
                    {
                        var bw = new MuBoneWeight();
                        // mu.py MuBoneWeight: 4 interleaved (int index, float weight)
                        bw.Index0 = r.ReadInt(); bw.Weight0 = r.ReadFloat();
                        bw.Index1 = r.ReadInt(); bw.Weight1 = r.ReadFloat();
                        bw.Index2 = r.ReadInt(); bw.Weight2 = r.ReadFloat();
                        bw.Index3 = r.ReadInt(); bw.Weight3 = r.ReadFloat();
                        m.BoneWeights[i] = bw;
                    }
                }
                else if (type == MuEntry.MeshBindPoses)
                {
                    int numPoses = r.ReadInt();
                    m.BindPoses = new float[numPoses][];
                    for (int i = 0; i < numPoses; i++)
                        m.BindPoses[i] = r.ReadFloats(16);
                }
                else if (type == MuEntry.MeshTriangles)
                {
                    // mu.py reads an int, then int/3 index triplets; winding is
                    // kept as stored (mu.py's reversal is Blender-only).
                    int numTris = r.ReadInt();
                    int triplets = numTris / 3;
                    var tris = new int[triplets * 3];
                    for (int i = 0; i < triplets * 3; i++)
                        tris[i] = r.ReadInt();
                    var old = m.Submeshes;
                    m.Submeshes = new int[old.Length + 1][];
                    for (int i = 0; i < old.Length; i++)
                        m.Submeshes[i] = old[i];
                    m.Submeshes[old.Length] = tris;
                }
                else if (type == MuEntry.MeshVertexColors)
                {
                    m.VertexColors = new MuVec4[m.NumVertices];
                    for (int i = 0; i < m.NumVertices; i++)
                    {
                        // mu.py: 4 bytes per vertex, each divided by 255
                        m.VertexColors[i] = new MuVec4(
                            r.ReadByte() / 255f, r.ReadByte() / 255f,
                            r.ReadByte() / 255f, r.ReadByte() / 255f);
                    }
                }
                else
                {
                    throw new FormatException(
                        "Mu: unknown mesh entry " + type + " at offset " + r.Position);
                }
            }
            return m;
        }

        private static MuCollider ReadMeshCollider(MuBin r, bool hasTrigger)
        {
            var c = new MuCollider { Kind = MuColliderKind.Mesh };
            if (hasTrigger) c.IsTrigger = r.ReadByte() != 0;
            c.Convex = r.ReadByte() != 0;
            c.Mesh = ReadMesh(r);
            return c;
        }

        private static MuCollider ReadSphereCollider(MuBin r, bool hasTrigger)
        {
            var c = new MuCollider { Kind = MuColliderKind.Sphere };
            if (hasTrigger) c.IsTrigger = r.ReadByte() != 0;
            c.Radius = r.ReadFloat();
            c.Center = r.ReadVec3();
            return c;
        }

        private static MuCollider ReadCapsuleCollider(MuBin r, bool hasTrigger)
        {
            var c = new MuCollider { Kind = MuColliderKind.Capsule };
            if (hasTrigger) c.IsTrigger = r.ReadByte() != 0;
            c.Radius = r.ReadFloat();
            c.Height = r.ReadFloat();
            c.Direction = r.ReadInt();
            c.Center = r.ReadVec3();
            return c;
        }

        private static MuCollider ReadBoxCollider(MuBin r, bool hasTrigger)
        {
            var c = new MuCollider { Kind = MuColliderKind.Box };
            if (hasTrigger) c.IsTrigger = r.ReadByte() != 0;
            c.Size = r.ReadVec3();
            c.Center = r.ReadVec3();
            return c;
        }

        private static MuCollider ReadWheelCollider(MuBin r)
        {
            var c = new MuCollider { Kind = MuColliderKind.Wheel };
            c.Mass = r.ReadFloat();
            c.Radius = r.ReadFloat();
            c.SuspensionDistance = r.ReadFloat();
            c.Center = r.ReadVec3();
            c.Spring = r.ReadFloat();
            c.Damper = r.ReadFloat();
            c.SpringTargetPosition = r.ReadFloat();
            c.ExtremumSlip = r.ReadFloat();
            c.ExtremumValue = r.ReadFloat();
            c.AsymptoteSlip = r.ReadFloat();
            c.AsymptoteValue = r.ReadFloat();
            c.ForwardStiffness = r.ReadFloat();
            c.SideExtremumSlip = r.ReadFloat();
            c.SideExtremumValue = r.ReadFloat();
            c.SideAsymptoteSlip = r.ReadFloat();
            c.SideAsymptoteValue = r.ReadFloat();
            c.SidewaysStiffness = r.ReadFloat();
            return c;
        }

        private static MuRenderer ReadRenderer(MuBin r, int version)
        {
            var m = new MuRenderer();
            if (version > 0)
            {
                m.CastShadows = r.ReadByte();
                m.ReceiveShadows = r.ReadByte();
            }
            int count = r.ReadInt();
            m.MaterialIndices = new int[count];
            for (int i = 0; i < count; i++)
                m.MaterialIndices[i] = r.ReadInt();
            return m;
        }

        private static MuSkinnedMeshRenderer ReadSkinnedMeshRenderer(MuBin r, int version)
        {
            var s = new MuSkinnedMeshRenderer();
            int numMat = r.ReadInt();
            s.MaterialIndices = new int[numMat];
            for (int i = 0; i < numMat; i++)
                s.MaterialIndices[i] = r.ReadInt();
            s.Center = r.ReadVec3();
            s.Size = r.ReadVec3();
            s.Quality = r.ReadInt();
            s.UpdateWhenOffscreen = r.ReadByte();
            int numBones = r.ReadInt();
            s.Bones = new string[numBones];
            for (int i = 0; i < numBones; i++)
                s.Bones[i] = r.ReadString();
            s.Mesh = ReadMesh(r);
            return s;
        }

        private static MuAnimation ReadAnimation(MuBin r)
        {
            var a = new MuAnimation();
            int numClips = r.ReadInt();
            a.Clips = new MuClip[numClips];
            for (int i = 0; i < numClips; i++)
                a.Clips[i] = ReadClip(r);
            a.Clip = r.ReadString();
            a.AutoPlay = r.ReadByte();
            return a;
        }

        private static MuClip ReadClip(MuBin r)
        {
            var c = new MuClip();
            c.Name = r.ReadString();
            c.LbCenter = r.ReadVec3();
            c.LbSize = r.ReadVec3();
            c.WrapMode = r.ReadInt();
            int numCurves = r.ReadInt();
            c.Curves = new MuCurve[numCurves];
            for (int i = 0; i < numCurves; i++)
                c.Curves[i] = ReadCurve(r);
            return c;
        }

        private static MuCurve ReadCurve(MuBin r)
        {
            var c = new MuCurve();
            c.Path = r.ReadString();
            c.Property = r.ReadString();
            c.Type = r.ReadInt();
            c.WrapModePre = r.ReadInt();
            c.WrapModePost = r.ReadInt();
            int numKeys;
            if (c.Type == 8)
            {
                // mu.py quirk: a broken PartTools export omitted the type, so
                // the key count was swallowed into wrapModePost. Guess the type
                // and re-align.
                c.Type = c.Path.StartsWith("material", StringComparison.Ordinal) ? 1 : 0;
                numKeys = c.WrapModePost;
                c.WrapModePost = c.WrapModePre;
                c.WrapModePre = c.Type;
            }
            else
            {
                numKeys = r.ReadInt();
            }
            c.Keys = new MuKey[numKeys];
            for (int i = 0; i < numKeys; i++)
            {
                var k = new MuKey();
                k.Time = r.ReadFloat();
                k.Value = r.ReadFloat();
                k.TangentIn = r.ReadFloat();
                k.TangentOut = r.ReadFloat();
                k.TangentMode = r.ReadInt();
                c.Keys[i] = k;
            }
            return c;
        }

        private static MuCamera ReadCamera(MuBin r)
        {
            var c = new MuCamera();
            c.ClearFlags = r.ReadInt();
            c.BackgroundColor = r.ReadVec4();
            c.CullingMask = r.ReadUInt();
            c.Orthographic = r.ReadByte();
            c.Fov = r.ReadFloat();
            c.Near = r.ReadFloat();
            c.Far = r.ReadFloat();
            c.Depth = r.ReadFloat();
            return c;
        }

        private static MuLight ReadLight(MuBin r, int version)
        {
            var l = new MuLight();
            l.Type = r.ReadInt();
            l.Intensity = r.ReadFloat();
            l.Range = r.ReadFloat();
            l.Color = r.ReadVec4();
            l.CullingMask = r.ReadUInt();
            if (version > 1)
                l.SpotAngle = r.ReadFloat();
            return l;
        }

        private static MuParticles ReadParticles(MuBin r)
        {
            var p = new MuParticles();
            p.Emit = r.ReadByte();
            p.Shape = r.ReadInt();
            p.Shape3D = r.ReadVec3();
            p.Shape2D = r.ReadVec2();
            p.Shape1D = r.ReadFloat();
            p.Color = r.ReadVec4();
            p.UseWorldSpace = r.ReadByte();
            p.Size = r.ReadVec2();
            p.Energy = r.ReadVec2();
            p.EmissionMin = r.ReadInt();
            p.EmissionMax = r.ReadInt();
            p.WorldVelocity = r.ReadVec3();
            p.LocalVelocity = r.ReadVec3();
            p.RandVelocity = r.ReadVec3();
            p.EmitterVelocityScale = r.ReadFloat();
            p.AngularVelocity = r.ReadFloat();
            p.RandAngularVelocity = r.ReadFloat();
            p.RandRotation = r.ReadByte();
            p.DoesAnimateColor = r.ReadByte();
            for (int i = 0; i < 5; i++)
                p.ColorAnimation[i] = r.ReadVec4();
            p.WorldRotationAxis = r.ReadVec3();
            p.LocalRotationAxis = r.ReadVec3();
            p.SizeGrow = r.ReadFloat();
            p.RandForce = r.ReadVec3();
            p.Force = r.ReadVec3();
            p.Damping = r.ReadFloat();
            p.CastShadows = r.ReadByte();
            p.ReceiveShadows = r.ReadByte();
            p.LengthScale = r.ReadFloat();
            p.VelocityScale = r.ReadFloat();
            p.MaxParticleSize = r.ReadFloat();
            p.ParticleRenderMode = r.ReadInt();
            p.UvAnimXTile = r.ReadInt();
            p.UvAnimYTile = r.ReadInt();
            p.UvAnimCycles = r.ReadInt();
            p.Count = r.ReadInt();
            return p;
        }

        // mu.py MuMaterial.read branches on version >= 4: read_material4
        // (generic property list) vs read_material3 (shader-type-driven).
        private static MuMaterial ReadMaterial(MuBin r, int version)
        {
            return version >= 4 ? ReadMaterial4(r) : ReadMaterial3(r);
        }

        private static MuMaterial ReadMaterial4(MuBin r)
        {
            var m = new MuMaterial();
            m.Name = r.ReadString();
            m.ShaderName = r.ReadString();
            int numProps = r.ReadInt();
            for (int i = 0; i < numProps; i++)
            {
                string prop = r.ReadString();
                int propType = r.ReadInt();
                if (propType == 0)
                    m.ColorProperties[prop] = r.ReadVec4();
                else if (propType == 1)
                    m.VectorProperties[prop] = r.ReadVec4();
                else if (propType == 2)
                    m.FloatProperties2[prop] = r.ReadFloat();
                else if (propType == 3)
                    m.FloatProperties3[prop] = r.ReadFloat();
                else if (propType == 4)
                    m.TextureProperties[prop] = ReadMatTex(r);
                // mu.py: other prop types print and continue.
            }
            return m;
        }

        private static MuMaterial ReadMaterial3(MuBin r)
        {
            var m = new MuMaterial();
            m.Name = r.ReadString();
            int type = r.ReadInt();
            m.ShaderType = type;
            if (type < 1 || type >= MuShaderType.ShaderNames.Length)
                throw new FormatException("Mu: unknown material shader type " + type);
            m.ShaderName = MuShaderType.ShaderNames[type];

            if (type == MuShaderType.Specular)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.ColorProperties["_SpecColor"] = r.ReadVec4();
                m.FloatProperties3["_Shininess"] = r.ReadFloat();
            }
            else if (type == MuShaderType.Bumped)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.TextureProperties["_BumpMap"] = ReadMatTex(r);
            }
            else if (type == MuShaderType.BumpedSpecular)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.TextureProperties["_BumpMap"] = ReadMatTex(r);
                m.ColorProperties["_SpecColor"] = r.ReadVec4();
                m.FloatProperties3["_Shininess"] = r.ReadFloat();
            }
            else if (type == MuShaderType.Emissive)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.TextureProperties["_Emissive"] = ReadMatTex(r);
                m.ColorProperties["_EmissiveColor"] = r.ReadVec4();
            }
            else if (type == MuShaderType.EmissiveSpecular)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.ColorProperties["_SpecColor"] = r.ReadVec4();
                m.FloatProperties3["_Shininess"] = r.ReadFloat();
                m.TextureProperties["_Emissive"] = ReadMatTex(r);
                m.ColorProperties["_EmissiveColor"] = r.ReadVec4();
            }
            else if (type == MuShaderType.EmissiveBumpedSpecular)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.TextureProperties["_BumpMap"] = ReadMatTex(r);
                m.ColorProperties["_SpecColor"] = r.ReadVec4();
                m.FloatProperties3["_Shininess"] = r.ReadFloat();
                m.TextureProperties["_Emissive"] = ReadMatTex(r);
                m.ColorProperties["_EmissiveColor"] = r.ReadVec4();
            }
            else if (type == MuShaderType.AlphaCutoff)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.FloatProperties3["_Cutoff"] = r.ReadFloat();
            }
            else if (type == MuShaderType.AlphaCutoffBumped)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.TextureProperties["_BumpMap"] = ReadMatTex(r);
                m.FloatProperties3["_Cutoff"] = r.ReadFloat();
            }
            else if (type == MuShaderType.Alpha)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
            }
            else if (type == MuShaderType.AlphaSpecular)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.FloatProperties3["_Gloss"] = r.ReadFloat();      // mu.py: "FIXME bogus"
                m.ColorProperties["_SpecColor"] = r.ReadVec4();
                m.FloatProperties3["_Shininess"] = r.ReadFloat();
            }
            else if (type == MuShaderType.AlphaUnlit)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.ColorProperties["_Color"] = r.ReadVec4();
            }
            else if (type == MuShaderType.Unlit)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.ColorProperties["_Color"] = r.ReadVec4();
            }
            else if (type == MuShaderType.ParticlesAlphaBlended)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.ColorProperties["_Color"] = r.ReadVec4();
                m.FloatProperties3["_InvFade"] = r.ReadFloat();
            }
            else if (type == MuShaderType.ParticlesAdditive)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
                m.ColorProperties["_Color"] = r.ReadVec4();
                m.FloatProperties3["_InvFade"] = r.ReadFloat();
            }
            else if (type == MuShaderType.Diffuse)
            {
                m.TextureProperties["_MainTex"] = ReadMatTex(r);
            }
            else
            {
                // ST_CUSTOM (0) has no defined layout in mu.py — ValueError there.
                throw new FormatException("Mu: unknown material type " + type);
            }
            return m;
        }

        private static MuMatTex ReadMatTex(MuBin r)
        {
            var t = new MuMatTex();
            t.Index = r.ReadInt();
            t.Scale = r.ReadVec2();
            t.Offset = r.ReadVec2();
            return t;
        }

        private static MuTexture ReadTexture(MuBin r)
        {
            var t = new MuTexture();
            t.Name = r.ReadString();
            t.Type = r.ReadInt();
            return t;
        }

        /// <summary>
        /// Little-endian cursor over the raw bytes. Truncated reads throw a
        /// deterministic FormatException naming the offset. Strings are
        /// 7-bit-varint length + bytes mapped to chars (mu.py chr()).
        /// </summary>
        private sealed class MuBin
        {
            private readonly byte[] _bytes;
            public int Position;

            public MuBin(byte[] bytes)
            {
                _bytes = bytes;
                Position = 0;
            }

            private void Need(int n)
            {
                if (Position + n > _bytes.Length)
                    throw new FormatException(
                        "Mu: unexpected end of data at offset " + Position);
            }

            public int ReadByte()
            {
                Need(1);
                return _bytes[Position++];
            }

            public bool TryReadInt(out int value)
            {
                if (Position + 4 > _bytes.Length)
                {
                    value = 0;
                    return false;
                }
                value = ReadInt();
                return true;
            }

            public int ReadInt()
            {
                Need(4);
                int v = _bytes[Position] | (_bytes[Position + 1] << 8) |
                        (_bytes[Position + 2] << 16) | (_bytes[Position + 3] << 24);
                Position += 4;
                return v;
            }

            public uint ReadUInt()
            {
                return (uint)ReadInt();
            }

            public float ReadFloat()
            {
                Need(4);
                float v = BitConverter.ToSingle(_bytes, Position);
                Position += 4;
                return v;
            }

            public float[] ReadFloats(int count)
            {
                var v = new float[count];
                for (int i = 0; i < count; i++)
                    v[i] = ReadFloat();
                return v;
            }

            public MuVec2 ReadVec2()
            {
                return new MuVec2(ReadFloat(), ReadFloat());
            }

            public MuVec3 ReadVec3()
            {
                return new MuVec3(ReadFloat(), ReadFloat(), ReadFloat());
            }

            public MuVec4 ReadVec4()
            {
                return new MuVec4(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
            }

            /// <summary>
            /// mu.py read_quaternion: stored (x, y, z, w) -> (q3, -q0, -q2, -q1).
            /// </summary>
            public MuQuat ReadQuat()
            {
                float q0 = ReadFloat();
                float q1 = ReadFloat();
                float q2 = ReadFloat();
                float q3 = ReadFloat();
                return new MuQuat { W = q3, X = -q0, Y = -q2, Z = -q1 };
            }

            public string ReadString()
            {
                int size = Read7BitInt();
                Need(size);
                var sb = new StringBuilder(size);
                for (int i = 0; i < size; i++)
                    sb.Append((char)_bytes[Position + i]);
                Position += size;
                return sb.ToString();
            }

            private int Read7BitInt()
            {
                int val = 0;
                int mult = 1;
                while (true)
                {
                    int b = ReadByte();
                    val += (b & 127) * mult;
                    if (b < 128)
                        return val;
                    mult *= 128;
                }
            }
        }
    }
}
