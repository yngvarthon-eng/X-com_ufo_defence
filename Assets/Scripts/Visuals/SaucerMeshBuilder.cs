using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the original X-Con saucer mesh from lathe profiles (no third-party model).
/// Submesh 0 = hull, 1 = dome glass, 2 = lights (rim lights + underside glow).
/// About 3.1 units across; the UFO manager rescales spawned visuals anyway.
/// </summary>
public static class SaucerMeshBuilder
{
    public const int HullSubmesh = 0;
    public const int DomeSubmesh = 1;
    public const int LightsSubmesh = 2;

    private const int Segments = 64;
    private const int RimLightCount = 12;

    // Profiles run along the surface; each list is one smooth strip, and strips meet at sharp creases.
    // (radius, height)
    private static readonly Vector2[] Underside =
    {
        new(0.36f, -0.30f), new(0.60f, -0.285f), new(0.90f, -0.24f), new(1.20f, -0.16f), new(1.42f, -0.075f), new(1.52f, -0.03f),
    };

    private static readonly Vector2[] RimBand =
    {
        new(1.52f, -0.03f), new(1.56f, 0.0f), new(1.52f, 0.035f),
    };

    private static readonly Vector2[] UpperHull =
    {
        new(1.52f, 0.035f), new(1.30f, 0.10f), new(1.00f, 0.17f), new(0.75f, 0.215f), new(0.60f, 0.235f),
    };

    private static readonly Vector2[] DomeCollar =
    {
        new(0.60f, 0.235f), new(0.60f, 0.27f), new(0.56f, 0.28f),
    };

    private static readonly Vector2[] Dome =
    {
        new(0.56f, 0.28f), new(0.545f, 0.38f), new(0.50f, 0.48f), new(0.42f, 0.57f), new(0.30f, 0.64f), new(0.16f, 0.68f), new(0f, 0.695f),
    };

    private static readonly Vector2[] UndersideGlow =
    {
        new(0f, -0.31f), new(0.36f, -0.30f),
    };

    public static Mesh Build()
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var hull = new List<int>();
        var dome = new List<int>();
        var lights = new List<int>();

        AddLathe(Underside, vertices, normals, uvs, hull);
        AddLathe(RimBand, vertices, normals, uvs, hull);
        AddLathe(UpperHull, vertices, normals, uvs, hull);
        AddLathe(DomeCollar, vertices, normals, uvs, hull);
        AddLathe(Dome, vertices, normals, uvs, dome);
        AddLathe(UndersideGlow, vertices, normals, uvs, lights);

        for (var i = 0; i < RimLightCount; i++)
        {
            var angle = (i + 0.5f) / RimLightCount * Mathf.PI * 2f;
            var center = new Vector3(Mathf.Cos(angle) * 1.555f, 0f, Mathf.Sin(angle) * 1.555f);
            AddSphere(center, 0.065f, vertices, normals, uvs, lights);
        }

        var mesh = new Mesh { name = "XConSaucer" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(hull, HullSubmesh);
        mesh.SetTriangles(dome, DomeSubmesh);
        mesh.SetTriangles(lights, LightsSubmesh);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    /// <summary>
    /// Revolve a (radius, height) profile around the Y axis. Normals come from the profile tangent,
    /// so each strip is smooth and strips meet at a crease. Winding is clockwise seen from outside.
    /// </summary>
    private static void AddLathe(Vector2[] profile, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
    {
        var start = vertices.Count;
        var count = profile.Length;

        for (var i = 0; i < count; i++)
        {
            var prev = profile[Mathf.Max(0, i - 1)];
            var next = profile[Mathf.Min(count - 1, i + 1)];
            var tangent = (next - prev).normalized;
            var normal2D = new Vector2(tangent.y, -tangent.x); // Outward, right of the profile direction.

            for (var s = 0; s <= Segments; s++)
            {
                var u = (float)s / Segments;
                var angle = u * Mathf.PI * 2f;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                vertices.Add(new Vector3(profile[i].x * cos, profile[i].y, profile[i].x * sin));
                normals.Add(new Vector3(normal2D.x * cos, normal2D.y, normal2D.x * sin).normalized);
                uvs.Add(new Vector2(u, (float)i / (count - 1)));
            }
        }

        var stride = Segments + 1;
        for (var i = 0; i < count - 1; i++)
        {
            for (var s = 0; s < Segments; s++)
            {
                var v00 = start + i * stride + s;
                var v01 = v00 + 1;
                var v10 = v00 + stride;
                var v11 = v10 + 1;
                triangles.Add(v00); triangles.Add(v10); triangles.Add(v11);
                triangles.Add(v00); triangles.Add(v11); triangles.Add(v01);
            }
        }
    }

    private static void AddSphere(Vector3 center, float radius, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
    {
        const int rings = 6;
        const int slices = 10;
        var start = vertices.Count;

        for (var r = 0; r <= rings; r++)
        {
            // From the bottom pole up, so the strip winds the same way as the lathe strips.
            var phi = Mathf.PI * (1f - (float)r / rings);
            for (var s = 0; s <= slices; s++)
            {
                var theta = (float)s / slices * Mathf.PI * 2f;
                var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                vertices.Add(center + n * radius);
                normals.Add(n);
                uvs.Add(new Vector2((float)s / slices, (float)r / rings));
            }
        }

        var stride = slices + 1;
        for (var r = 0; r < rings; r++)
        {
            for (var s = 0; s < slices; s++)
            {
                var v00 = start + r * stride + s;
                var v01 = v00 + 1;
                var v10 = v00 + stride;
                var v11 = v10 + 1;
                triangles.Add(v00); triangles.Add(v10); triangles.Add(v11);
                triangles.Add(v00); triangles.Add(v11); triangles.Add(v01);
            }
        }
    }
}
