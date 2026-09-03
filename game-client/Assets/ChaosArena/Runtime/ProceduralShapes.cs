using UnityEngine;

namespace ChaosArena
{
    public enum FighterShape { Cube, Sphere, Tetrahedron, Cylinder }

    /// <summary>
    /// Builds the fighter body forms. Unity ships no tetrahedron primitive, so that one is generated here with
    /// duplicated vertices per face to keep the facets flat and the silhouette hard-edged.
    /// </summary>
    public static class ProceduralShapes
    {
        public static GameObject CreateBody(FighterShape shape, string name)
        {
            if (shape == FighterShape.Tetrahedron) return CreateTetrahedron(name);

            PrimitiveType primitive = shape switch
            {
                FighterShape.Sphere => PrimitiveType.Sphere,
                FighterShape.Cylinder => PrimitiveType.Cylinder,
                _ => PrimitiveType.Cube
            };

            GameObject body = GameObject.CreatePrimitive(primitive);
            body.name = name;
            StripCollider(body);
            return body;
        }

        private static GameObject CreateTetrahedron(string name)
        {
            GameObject body = new(name, typeof(MeshFilter), typeof(MeshRenderer));

            // Four alternating corners of a cube form a regular tetrahedron.
            Vector3[] corners =
            {
                new Vector3(1f, 1f, 1f).normalized,
                new Vector3(1f, -1f, -1f).normalized,
                new Vector3(-1f, 1f, -1f).normalized,
                new Vector3(-1f, -1f, 1f).normalized
            };

            int[][] faces =
            {
                new[] { 0, 2, 1 },
                new[] { 0, 1, 3 },
                new[] { 0, 3, 2 },
                new[] { 1, 2, 3 }
            };

            Vector3[] vertices = new Vector3[faces.Length * 3];
            Vector3[] normals = new Vector3[faces.Length * 3];
            int[] triangles = new int[faces.Length * 3];

            for (int face = 0; face < faces.Length; face++)
            {
                Vector3 a = corners[faces[face][0]];
                Vector3 b = corners[faces[face][1]];
                Vector3 c = corners[faces[face][2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

                for (int corner = 0; corner < 3; corner++)
                {
                    int index = face * 3 + corner;
                    vertices[index] = corner == 0 ? a : corner == 1 ? b : c;
                    normals[index] = normal;
                    triangles[index] = index;
                }
            }

            Mesh mesh = new()
            {
                name = "Tetrahedron",
                vertices = vertices,
                normals = normals,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            body.GetComponent<MeshFilter>().sharedMesh = mesh;
            return body;
        }

        private static void StripCollider(GameObject target)
        {
            Collider bodyCollider = target.GetComponent<Collider>();
            if (bodyCollider == null) return;
            bodyCollider.enabled = false;
            Object.Destroy(bodyCollider);
        }
    }
}
