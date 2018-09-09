using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Unity.Jobs;

public class MeshSlicer : MonoBehaviour
{
    [Header("Plane Slice")]
    public PlaneSlice m_plane;

    [Header("Debug Settings")]
    public bool m_debugDraw = true;
    public float m_debugPlaneSize = 15.0f;
    public Color m_debugColor = new Color(0.0f, 1.0f, 0.0f, 0.5f);

    private Stack<Mesh> resetMeshStack = new Stack<Mesh>();
    private const int NUM_JOBS = 6;
    private const float SCALE = 0.25f;

    /// <summary>
    /// Removes all slices and resets mesh to original shape
    /// </summary>
    public void ResetMesh()
    {
        MeshFilter filter = this.transform.GetComponent<MeshFilter>();
        if (resetMeshStack.Count > 0 && filter != null)
        {
            filter.sharedMesh = resetMeshStack.Pop();
        }
    }

    public void OnDrawGizmosSelected()
    {
        if (!m_debugDraw)
            return;

        if (m_plane.IsValid())
        {
            List<Vector3> axes = m_plane.GetCoordinateLines();

            Mesh planeMesh = new Mesh();
            planeMesh.vertices = new Vector3[] { m_plane.point + (axes[0] + axes[1]) * m_debugPlaneSize,
                                                 m_plane.point + (axes[0] - axes[1]) * m_debugPlaneSize,
                                                 m_plane.point - (axes[0] - axes[1]) * m_debugPlaneSize,
                                                 m_plane.point - (axes[0] + axes[1]) * m_debugPlaneSize};
            planeMesh.triangles = new int[] { 0, 3, 1,  0, 1, 3,  0, 2, 3,  0, 3, 2}; // Render back faces
            planeMesh.RecalculateNormals();

            Gizmos.color = m_debugColor;
            Gizmos.DrawMesh(planeMesh, this.transform.position);
        }
    }

    private Mesh CopyMesh(Mesh mesh)
    {
        Mesh newMesh = new Mesh();
        newMesh.vertices = mesh.vertices;
        newMesh.triangles = mesh.triangles;
        newMesh.uv = mesh.uv;
        newMesh.normals = mesh.normals;
        newMesh.colors = mesh.colors;
        newMesh.tangents = mesh.tangents;
        return newMesh;
    }

    private int[] CutLine(Dictionary<VertexPair, VertexPair> cutLines, 
                         VertexPair line, 
                         List<Vector3> vertices, 
                         List<Vector3> normals,
                         List<Vector2> uvs)
    {
        if (!cutLines.ContainsKey(line))
        {
            Vector3 v1 = vertices[line.v1];
            Vector3 v2 = vertices[line.v2];
            Vector3 n1 = normals[line.v1];
            Vector3 n2 = normals[line.v2];
            PlaneSliceLineIntersection intersection = m_plane.GetLineIntersection(v1, v2 - v1);
            vertices.Add(intersection.intersectionPoint + m_plane.normal * Mathf.Sign(m_plane.DistanceToPoint(v1)) * SCALE);
            vertices.Add(intersection.intersectionPoint + m_plane.normal * Mathf.Sign(m_plane.DistanceToPoint(v2)) * SCALE);
            normals.Add(Vector3.Lerp(n1, n2, intersection.directionIntersectionScalar));
            normals.Add(Vector3.Lerp(n1, n2, intersection.directionIntersectionScalar));

            if (uvs.Count > 0)
            {
                Vector2 uv1 = uvs[line.v1];
                Vector2 uv2 = uvs[line.v2];
                uvs.Add(Vector2.Lerp(uv1, uv2, intersection.directionIntersectionScalar));
                uvs.Add(Vector2.Lerp(uv1, uv2, intersection.directionIntersectionScalar));
            }

            cutLines.Add(line, new VertexPair(vertices.Count - 2, vertices.Count - 1));
            return new int[] { vertices.Count - 2, vertices.Count - 1 };
        }
        else
        {
            // Already cut this line
            return new int[] { cutLines[line].v1, cutLines[line].v2 };
        }
    }

    /// <summary>
    /// Slices the mesh along the currently set plane
    /// </summary>
    public void SliceMesh()
    {
        float startTime = Time.realtimeSinceStartup;

        if (!m_plane.IsValid())
        {
            Debug.LogError("Invalid plane on gameobject " + this.gameObject.ToString());
            return;
        }

        MeshFilter filter = this.transform.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            Debug.LogError("No mesh filter attached to gameobject " + this.gameObject.ToString());
            return;
        }

        resetMeshStack.Push(filter.sharedMesh);
        Mesh meshReference = CopyMesh(filter.sharedMesh);
        List<int> newTriangles = new List<int>();
        List<int> oldTriangles = new List<int>();
        List<Vector3> newVertices = new List<Vector3>();
        List<Vector3> newNormals = new List<Vector3>();
        List<Vector4> newTangents = new List<Vector4>();
        List<Vector2> newUvs = new List<Vector2>();
        meshReference.GetTriangles(oldTriangles, 0);
        meshReference.GetVertices(newVertices);
        meshReference.GetNormals(newNormals);
        meshReference.GetTangents(newTangents);
        meshReference.GetUVs(0, newUvs);

        // Hash types to track computed vertices / cuts
        HashSet<int> modifiedVertices = new HashSet<int>();
        Dictionary<VertexPair, VertexPair> cutLines = new Dictionary<VertexPair, VertexPair>();

        for (int i = 0; i < oldTriangles.Count; i += 3)
        {
            int v1 = oldTriangles[i];
            int v2 = oldTriangles[i + 1];
            int v3 = oldTriangles[i + 2];

            float d1 = Mathf.Sign(m_plane.DistanceToPoint(newVertices[v1]));
            float d2 = Mathf.Sign(m_plane.DistanceToPoint(newVertices[v2]));
            float d3 = Mathf.Sign(m_plane.DistanceToPoint(newVertices[v3]));

            bool cut = d1 != d2 || d1 != d3;

            // Push triangles away
            if (!modifiedVertices.Contains(v1))
            {
                modifiedVertices.Add(v1);
                newVertices[v1] += m_plane.normal * d1 * SCALE;
            }
            if (!modifiedVertices.Contains(v2))
            {
                modifiedVertices.Add(v2);
                newVertices[v2] += m_plane.normal * d2 * SCALE;
            }
            if (!modifiedVertices.Contains(v3))
            {
                modifiedVertices.Add(v3);
                newVertices[v3] += m_plane.normal * d3 * SCALE;
            }

            if (!cut)
            {
                newTriangles.Add(oldTriangles[i]);
                newTriangles.Add(oldTriangles[i + 1]);
                newTriangles.Add(oldTriangles[i + 2]);
            }
            else
            {
                // Slice into three new triangles
                int outlier;
                int base1;
                int base2;
                if (d1 == d3)
                {
                    outlier = v2;
                    base1 = v1;
                    base2 = v3;
                }
                else if (d1 == d2)
                {
                    outlier = v3;
                    base1 = v2;
                    base2 = v1;
                }
                else
                {
                    outlier = v1;
                    base1 = v3;
                    base2 = v2;
                }

                int[] cutLine = CutLine(cutLines, new VertexPair(base1, outlier), newVertices, newNormals, newUvs);
                int nv1 = cutLine[0];
                int nv2 = cutLine[1];

                cutLine = CutLine(cutLines, new VertexPair(base2, outlier), newVertices, newNormals, newUvs);
                int nv3 = cutLine[0];
                int nv4 = cutLine[1];

                // New triangles
                newTriangles.AddRange(new List<int> { outlier, nv4, nv2 });
                newTriangles.AddRange(new List<int> { nv1, nv3, base2 });
                newTriangles.AddRange(new List<int> { base2, base1, nv1 });
            }
        }

        meshReference.SetVertices(newVertices);
        meshReference.SetUVs(0, newUvs);
        meshReference.SetTriangles(newTriangles, 0);
        meshReference.SetNormals(newNormals);
        meshReference.RecalculateTangents();
        filter.sharedMesh = meshReference;

        Debug.LogError("Old triangles # " + oldTriangles.Count);
        Debug.LogError("New triangles # " + newTriangles.Count);
        Debug.LogError("Slice took " + (Time.realtimeSinceStartup - startTime));
    }
}