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

        /// <summary>
        /// Glowing edge frame that traces a solid's silhouette. This is what turns a plain primitive into a
        /// piece of lit hardware: the bloom pass catches the bars and the shape reads as a neon wireframe
        /// wrapped around a matte core.
        /// </summary>
        public static void CreateEdgeFrame(FighterShape shape, Transform parent, Color color)
        {
            const float thickness = 0.055f;
            switch (shape)
            {
                case FighterShape.Cube:
                {
                    const float h = 0.52f;
                    foreach (int y in Signs)
                    foreach (int z in Signs)
                        CreateBar(parent, new Vector3(-h, y * h, z * h), new Vector3(h, y * h, z * h), thickness, color);
                    foreach (int x in Signs)
                    foreach (int z in Signs)
                        CreateBar(parent, new Vector3(x * h, -h, z * h), new Vector3(x * h, h, z * h), thickness, color);
                    foreach (int x in Signs)
                    foreach (int y in Signs)
                        CreateBar(parent, new Vector3(x * h, y * h, -h), new Vector3(x * h, y * h, h), thickness, color);
                    break;
                }

                case FighterShape.Tetrahedron:
                {
                    Vector3[] corners = TetrahedronCorners(0.53f);
                    for (int a = 0; a < corners.Length; a++)
                    {
                        for (int b = a + 1; b < corners.Length; b++)
                        {
                            CreateBar(parent, corners[a], corners[b], thickness, color);
                        }
                    }
                    break;
                }

                case FighterShape.Sphere:
                    CreateRing(parent, 0.52f, 16, Vector3.forward, thickness, color);
                    CreateRing(parent, 0.52f, 16, Vector3.up, thickness, color);
                    break;

                case FighterShape.Cylinder:
                    CreateRing(parent, 0.52f, 16, Vector3.up, thickness, color, 0.52f);
                    CreateRing(parent, 0.52f, 16, Vector3.up, thickness, color, -0.52f);
                    foreach (int x in Signs)
                    {
                        CreateBar(parent, new Vector3(x * 0.52f, -0.52f, 0f), new Vector3(x * 0.52f, 0.52f, 0f), thickness, color);
                    }
                    break;
            }
        }

        private static readonly int[] Signs = { -1, 1 };

        private static Vector3[] TetrahedronCorners(float radius) => new[]
        {
            new Vector3(1f, 1f, 1f).normalized * radius,
            new Vector3(1f, -1f, -1f).normalized * radius,
            new Vector3(-1f, 1f, -1f).normalized * radius,
            new Vector3(-1f, -1f, 1f).normalized * radius
        };

        private static void CreateRing(Transform parent, float radius, int segments, Vector3 axis, float thickness,
            Color color, float offset = 0f)
        {
            Quaternion orient = Quaternion.FromToRotation(Vector3.up, axis);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 p0 = orient * new Vector3(Mathf.Cos(a0) * radius, offset, Mathf.Sin(a0) * radius);
                Vector3 p1 = orient * new Vector3(Mathf.Cos(a1) * radius, offset, Mathf.Sin(a1) * radius);
                CreateBar(parent, p0, p1, thickness, color);
            }
        }

        /// <summary>A thin glowing cube stretched between two local-space points.</summary>
        public static GameObject CreateBar(Transform parent, Vector3 from, Vector3 to, float thickness, Color color)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Edge";
            bar.transform.SetParent(parent, false);
            StripCollider(bar);

            Vector3 direction = to - from;
            bar.transform.localPosition = (from + to) * 0.5f;
            bar.transform.localRotation = direction.sqrMagnitude > 0.000001f
                ? Quaternion.FromToRotation(Vector3.forward, direction.normalized)
                : Quaternion.identity;
            bar.transform.localScale = new Vector3(thickness, thickness, direction.magnitude);

            PrototypeMaterials.AssignNeon(bar.GetComponent<Renderer>(), color, 1.6f);
            return bar;
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
