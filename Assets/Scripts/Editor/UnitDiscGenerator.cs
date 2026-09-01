using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace RHCommunityHack.EditorTools
{
    // A flat disc of radius 1 in the XZ plane, facing up. Unity has no circle primitive, and the
    // usual substitutes are both wrong for a floor: a flattened Cylinder keeps its side wall, which
    // shows as a rim, and a Quad is square unless you cut a circle out of it with a texture.
    //
    // Radius is 1 so the GameObject's scale sets the size - which is what lets ImmersiveDomeFloor
    // drive the radius from a single number every frame.
    //
    // UVs map the -1..1 disc onto 0..1, so a texture lands on it like a top-down picture rather
    // than being smeared radially.
    public static class UnitDiscGenerator
    {
        const string MeshFolder = "Assets/Meshes";
        const int Segments = 64;   // matches the sphere, so silhouettes agree where they meet

        [MenuItem("RH Community Hack/Generate Unit Disc Mesh")]
        public static void GenerateAsset()
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder("Assets", "Meshes");

            var built = Build(Segments);
            string path = MeshFolder + "/UnitDisc.asset";

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                existing.SetVertices(new List<Vector3>(built.vertices));
                existing.SetNormals(new List<Vector3>(built.normals));
                existing.SetUVs(0, new List<Vector2>(built.uv));
                existing.SetTriangles(built.triangles, 0);
                existing.RecalculateBounds();
                existing.RecalculateTangents();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(built);
                built = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(built, path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UnitDiscGenerator] {path}: {built.vertexCount} verts, " +
                      $"{built.triangles.Length / 3} triangles, radius 1, facing +Y.");
            Selection.activeObject = built;
        }

        public static Mesh Build(int segments)
        {
            segments = Mathf.Max(3, segments);

            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);

                vertices[i + 1] = new Vector3(x, 0f, z);
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);
            }

            var triangles = new List<int>(segments * 3);
            for (int i = 0; i < segments; i++)
            {
                int a = i + 1;
                int b = (i + 1) % segments + 1;
                // Wound so the front face points UP - a floor seen from below is culled, which is
                // correct here and cheaper than making it two-sided.
                triangles.Add(0); triangles.Add(b); triangles.Add(a);
            }

            var mesh = new Mesh { name = "UnitDisc" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
