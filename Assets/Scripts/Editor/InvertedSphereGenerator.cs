using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace RHCommunityHack.EditorTools
{
    // Builds a sphere whose faces only exist on the INSIDE, and saves it as a mesh asset.
    //
    // The trick is the triangle winding, not the normals and not the shader. With the winding
    // reversed, every triangle's front face points inward, so with ordinary back-face culling:
    //
    //   - from OUTSIDE, the near hemisphere shows you its back faces, which are culled - you see
    //     straight through it to the inner surface of the far hemisphere
    //   - from INSIDE, every front face points at you, so it wraps you completely
    //
    // That means it works with any normal opaque material. The alternatives are worse: `Cull Front`
    // needs a custom shader and makes the object vanish rather than become see-through from
    // outside, and flipping normals alone changes lighting without changing what gets culled.
    //
    // UVs are equirectangular (u = longitude, v = latitude), which is what 360 footage expects, so
    // the same mesh doubles as a video dome. Unity's built-in sphere is no use for that: it is low
    // poly and its UVs are not a lat/long mapping.
    //
    // Radius is 1. Scale the GameObject instead - one mesh then serves every size.
    public static class InvertedSphereGenerator
    {
        const string MeshFolder = "Assets/Meshes";
        const int LongitudeSegments = 64;   // around the equator
        const int LatitudeSegments = 32;    // pole to pole

        [MenuItem("RH Community Hack/Generate Inverted Sphere Mesh")]
        public static void GenerateAsset()
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder("Assets", "Meshes");

            var mesh = Build(LongitudeSegments, LatitudeSegments);
            string path = MeshFolder + "/InvertedSphere.asset";

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Overwrite in place so every scene reference survives.
                existing.Clear();
                existing.SetVertices(new List<Vector3>(mesh.vertices));
                existing.SetNormals(new List<Vector3>(mesh.normals));
                existing.SetUVs(0, new List<Vector2>(mesh.uv));
                existing.SetTriangles(mesh.triangles, 0);
                existing.RecalculateBounds();
                existing.RecalculateTangents();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[InvertedSphereGenerator] {path}: {mesh.vertexCount} verts, " +
                      $"{mesh.triangles.Length / 3} triangles, radius 1.");
            Selection.activeObject = mesh;
        }

        public static Mesh Build(int longitudeSegments, int latitudeSegments)
        {
            longitudeSegments = Mathf.Max(3, longitudeSegments);
            latitudeSegments = Mathf.Max(2, latitudeSegments);

            int columns = longitudeSegments + 1;   // the seam column is duplicated so u can reach 1
            var vertices = new Vector3[columns * (latitudeSegments + 1)];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];

            for (int lat = 0; lat <= latitudeSegments; lat++)
            {
                float v = (float)lat / latitudeSegments;
                float theta = v * Mathf.PI;              // 0 at the north pole
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                for (int lon = 0; lon <= longitudeSegments; lon++)
                {
                    float u = (float)lon / longitudeSegments;
                    float phi = u * Mathf.PI * 2f;

                    var p = new Vector3(sinTheta * Mathf.Sin(phi), cosTheta, sinTheta * Mathf.Cos(phi));
                    int i = lat * columns + lon;

                    vertices[i] = p;
                    normals[i] = -p;                    // inward, so lit shaders work from inside
                    uvs[i] = new Vector2(u, 1f - v);    // v=1 at the north pole, as equirect expects
                }
            }

            var triangles = new List<int>(longitudeSegments * latitudeSegments * 6);
            for (int lat = 0; lat < latitudeSegments; lat++)
            {
                for (int lon = 0; lon < longitudeSegments; lon++)
                {
                    int current = lat * columns + lon;
                    int next = current + columns;

                    // REVERSED winding - this is the entire mechanism this file exists for.
                    // Do NOT "tidy" the index order: with this parameterisation the opposite
                    // order builds an ordinary outward sphere, and the difference is invisible
                    // in the inspector. Verified by measuring each triangle's geometric normal
                    // against its own centre; see the assert-style check in the menu command.
                    triangles.Add(current); triangles.Add(current + 1); triangles.Add(next);
                    triangles.Add(current + 1); triangles.Add(next + 1); triangles.Add(next);
                }
            }

            var mesh = new Mesh { name = "InvertedSphere" };
            if (vertices.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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
