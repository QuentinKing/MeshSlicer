using System.Collections;
using System.Collections.Generic;

using UnityEngine;

public struct LineData
{
    public Vector3 vert1;
    public Vector3 vert2;

    public LineData(Vector3 v1, Vector3 v2)
    {
        vert1 = v1;
        vert2 = v2;
    }
}

public struct TriangleData
{
    public Vector3 vert1;
    public Vector3 vert2;
    public Vector3 vert3;

    public Vector3 normal1;
    public Vector3 normal2;
    public Vector3 normal3;

    public TriangleData(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3)
    {
        vert1 = v1;
        vert2 = v2;
        vert3 = v3;

        normal1 = n1;
        normal2 = n2;
        normal3 = n3;
    }
}

public class SlixelCreator : MonoBehaviour
{
    public MeshFilter m_meshFilter;
    public Mesh m_baseMesh;
    private Mesh m_newMesh;

    public int numSlixels;

    public bool build = false;
    public bool reset = false;
    private bool built = false;

    private void Update()
    {
        if (m_meshFilter == null || m_baseMesh == null)
            return;

        if (reset)
        {
            build = false;
            built = false;
            m_meshFilter.mesh = m_baseMesh;
        }
        else if (build && !built)
        {
            BuildMesh();
            built = true;
        }
    }

    public bool m_debugDraw = false;
    public float m_gizmosSize = 3.0f;
    private List<LineData> m_linesToDraw = new List<LineData>();
    public void OnDrawGizmosSelected()
    {
        if (!m_debugDraw)
            return;

        Gizmos.color = Color.red;

        for (int i = 0; i < m_linesToDraw.Count; i++)
        {
            Gizmos.DrawLine(m_linesToDraw[i].vert1, m_linesToDraw[i].vert2);
        }
    }

    public Queue<TriangleData> GetTriangleData(Mesh mesh)
    {
        var queue = new Queue<TriangleData>();

        for (int i = 0; i + 2 < mesh.triangles.Length; i += 3)
        {
            Vector3 v1 = mesh.vertices[mesh.triangles[i]];
            Vector3 v2 = mesh.vertices[mesh.triangles[i + 1]];
            Vector3 v3 = mesh.vertices[mesh.triangles[i + 2]];
            Vector3 n1 = mesh.normals[mesh.triangles[i]];
            Vector3 n2 = mesh.normals[mesh.triangles[i + 1]];
            Vector3 n3 = mesh.normals[mesh.triangles[i + 2]];
            queue.Enqueue(new TriangleData(v1, v2, v3, n1, n2, n3));
        }

        return queue;
    }

    public void BuildMesh()
    {
        m_linesToDraw.Clear();

        List<Vector3> newVertices = new List<Vector3>();
        List<int> newTriangles = new List<int>();
        List<Vector3> newNormals = new List<Vector3>();

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        foreach (Vector3 vert in m_baseMesh.vertices)
        {
            minY = Mathf.Min(minY, vert.y);
            maxY = Mathf.Max(maxY, vert.y);
        }
        float deltaY = (maxY - minY) / numSlixels;

        List<float> sliceHeights = new List<float>();

        for (int i = 1; i < numSlixels; i++)
        {
            sliceHeights.Add(minY + deltaY * i);
        }

        Queue<TriangleData> trianglesToCheck = GetTriangleData(m_baseMesh);

        while (trianglesToCheck.Count != 0)
        {
            TriangleData t = trianglesToCheck.Dequeue();

            bool slice = false;
            for (int j = 0; j < sliceHeights.Count; j++)
            {
                float h = sliceHeights[j];
                slice = !((t.vert1.y <= h && t.vert2.y <= h && t.vert2.y <= h) || (t.vert1.y >= h && t.vert2.y >= h && t.vert2.y >= h));

                if (slice)
                {
                    // Slice the triangle but don't cap it just yet
                    Vector3 outlier;
                    Vector3 base1;
                    Vector3 base2;
                    Vector3 outlierNormal;
                    Vector3 base1Normal;
                    Vector3 base2Normal;
                    if ((t.vert1.y < h && t.vert2.y < h) || (t.vert1.y >= h && t.vert2.y >= h))
                    {
                        base1 = t.vert2;
                        base1Normal = t.normal1;
                        base2 = t.vert1;
                        base2Normal = t.normal2;
                        outlier = t.vert3;
                        outlierNormal = t.normal3;
                    }
                    else if ((t.vert1.y < h && t.vert3.y < h) || (t.vert1.y >= h && t.vert3.y >= h))
                    {
                        base1 = t.vert1;
                        base1Normal = t.normal1;
                        outlier = t.vert2;
                        outlierNormal = t.normal2;
                        base2 = t.vert3;
                        base2Normal = t.normal3;
                    }
                    else
                    {
                        outlier = t.vert1;
                        outlierNormal = t.normal1;
                        base1 = t.vert3;
                        base1Normal = t.normal2;
                        base2 = t.vert2;
                        base2Normal = t.normal3;
                    }

                    float t1 = ((h - base1.y) / (outlier.y - base1.y));
                    Vector3 newVert1 = base1 + t1 * (outlier - base1);
                    Vector3 newVert1Normal = Vector3.Normalize(Vector3.Lerp(base1Normal, outlierNormal, t1));

                    float t2 = ((h - base2.y) / (outlier.y - base2.y));
                    Vector3 newVert2 = base2 + t2 * (outlier - base2);
                    Vector3 newVert2Normal = Vector3.Normalize(Vector3.Lerp(base2Normal, outlierNormal, t2));

                    Vector3 newVert3 = newVert1;
                    Vector3 newVert3Normal = newVert1Normal;
                    Vector3 newVert4 = newVert2;
                    Vector3 newVert4Normal = newVert2Normal;

                    m_linesToDraw.Add(new LineData(this.transform.TransformPoint(newVert1), this.transform.TransformPoint(newVert2)));

                    int startIndex = newVertices.Count;

                    newVertices.Add(outlier);
                    newVertices.Add(base1);     // +1
                    newVertices.Add(base2);     // +2
                    newVertices.Add(newVert1);  // +3
                    newVertices.Add(newVert2);  // +4
                    newVertices.Add(newVert3);  // +5
                    newVertices.Add(newVert4);  // +6

                    newNormals.Add(outlierNormal);
                    newNormals.Add(base1Normal);     // +1
                    newNormals.Add(base2Normal);     // +2
                    newNormals.Add(newVert1Normal);  // +3
                    newNormals.Add(newVert2Normal);  // +4
                    newNormals.Add(newVert3Normal);  // +5
                    newNormals.Add(newVert4Normal);  // +6

                    newTriangles.AddRange(new List<int> { startIndex, startIndex + 4, startIndex + 3 });
                    newTriangles.AddRange(new List<int> { startIndex + 1, startIndex + 5, startIndex + 2 });
                    newTriangles.AddRange(new List<int> { startIndex + 2, startIndex + 5, startIndex + 6 });

                    trianglesToCheck.Enqueue(new TriangleData(outlier, newVert2, newVert1, outlierNormal, newVert2Normal, newVert1Normal));
                    trianglesToCheck.Enqueue(new TriangleData(base1, newVert3, base2, base1Normal, newVert3Normal, base2Normal));
                    trianglesToCheck.Enqueue(new TriangleData(base2, newVert3, newVert4, base2Normal, newVert3Normal, newVert4Normal));
                    break;
                }
            }

            if (!slice)
            { 
                // Add original triangle
                // Might be duplicating verts here
                int startIndex = newVertices.Count;
                newVertices.Add(t.vert1);
                newVertices.Add(t.vert2);
                newVertices.Add(t.vert3);
                newNormals.Add(t.normal1);
                newNormals.Add(t.normal2);
                newNormals.Add(t.normal3);
                newTriangles.AddRange(new List<int> { startIndex, startIndex + 1, startIndex + 2 });
            }
        }

        m_newMesh = new Mesh();
        m_newMesh.vertices = newVertices.ToArray();
        m_newMesh.triangles = newTriangles.ToArray();
        m_newMesh.normals = newNormals.ToArray();

        m_meshFilter.mesh = m_newMesh;
    }
}
