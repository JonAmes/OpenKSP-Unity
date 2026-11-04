using System.Collections.Generic;
using OpenKSP.Core.Modding;
using UnityEngine;

// =============================================================================
// MuMeshLoader — Unity bridge over the Core .mu reader (step-03 Task 8).
// Mu.Read (Core, byte-faithful) → UnityEngine.Mesh per MeshFilter in the
// object tree. Vectors are copied VERBATIM (mu.py's y/z swaps are Blender
// conversions, not part of the byte format — pinned by T16). Quaternion
// bridge (plan open question q4): Core emits MuQuat as (W,X,Y,Z) already
// transformed by mu.py's read_quaternion; the Unity ctor takes (x,y,z,w),
// so the bridge is new Quaternion(q.X, q.Y, q.Z, q.W) verbatim.
// One-time load allocations are fine; nothing here runs in an Update path.
// =============================================================================

public static class MuMeshLoader
{
    /// <summary>Parse a .mu blob and build one Mesh per MeshFilter object.</summary>
    public static Mesh[] LoadMeshes(byte[] muBytes)
    {
        MuFile file = Mu.Read(muBytes);
        var meshes = new List<Mesh>();
        Collect(file.Root, meshes);
        return meshes.ToArray();
    }

    /// <summary>Full transform tree (local TRS per object, pre-order) for callers
    /// that rebuild the hierarchy; parallel to LoadMeshes' traversal order is
    /// NOT guaranteed — use the MuFile tree directly for pairing.</summary>
    public static MuFile LoadFile(byte[] muBytes)
    {
        return Mu.Read(muBytes);
    }

    /// <summary>Core MuQuat (W,X,Y,Z layout) → UnityEngine.Quaternion (x,y,z,w).</summary>
    public static Quaternion ToUnityQuat(MuQuat q)
    {
        return new Quaternion(q.X, q.Y, q.Z, q.W);
    }

    private static void Collect(MuObject obj, List<Mesh> outMeshes)
    {
        if (obj.SharedMesh != null)
            outMeshes.Add(BuildMesh(obj.SharedMesh));
        for (int i = 0; i < obj.Children.Count; i++)
            Collect(obj.Children[i], outMeshes);
    }

    private static Mesh BuildMesh(MuMesh src)
    {
        var mesh = new Mesh();
        if (src.NumVertices > 65000)
        {
            // Past this vertex count Unity needs uint16 index buffers off;
            // keep the scaffold simple and use index-format-aware ctor below.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        var verts = new Vector3[src.Verts.Length];
        for (int i = 0; i < verts.Length; i++)
            verts[i] = new Vector3(src.Verts[i].X, src.Verts[i].Y, src.Verts[i].Z);
        mesh.vertices = verts;

        if (src.Uvs.Length == verts.Length)
        {
            var uvs = new Vector2[src.Uvs.Length];
            for (int i = 0; i < uvs.Length; i++)
                uvs[i] = new Vector2(src.Uvs[i].X, src.Uvs[i].Y);
            mesh.uv = uvs;
        }

        if (src.Normals.Length == verts.Length)
        {
            var nrm = new Vector3[src.Normals.Length];
            for (int i = 0; i < nrm.Length; i++)
                nrm[i] = new Vector3(src.Normals[i].X, src.Normals[i].Y, src.Normals[i].Z);
            mesh.normals = nrm;
        }
        else
        {
            mesh.RecalculateNormals();
        }

        // Submeshes: flat index triplets straight into Unity submeshes;
        // windings kept raw (mu.py's winding reversal is Blender-only, T16).
        mesh.subMeshCount = System.Math.Max(1, src.Submeshes.Length);
        if (src.Submeshes.Length == 0)
        {
            mesh.triangles = new int[0];
        }
        else
        {
            for (int s = 0; s < src.Submeshes.Length; s++)
                mesh.SetTriangles(src.Submeshes[s], s);
        }

        mesh.RecalculateBounds();
        return mesh;
    }
}
