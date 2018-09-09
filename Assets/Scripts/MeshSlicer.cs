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
    public float m_debugPlaneSize = 2.0f;
    public Color m_debugColor = new Color(0.0f, 1.0f, 0.0f, 0.5f);

    private Stack<Mesh> resetMeshStack = new Stack<Mesh>();
    private const int NUM_JOBS = 6;

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

        if (m_plane != null && m_plane.IsValid())
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

    /// <summary>
    /// Slices the mesh along the currently set plane
    /// </summary>
    public void SliceMesh()
    {
        float startTime = Time.realtimeSinceStartup;

        if (m_plane == null || !m_plane.IsValid())
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

        AllocatedMesh mesh = new AllocatedMesh(filter.sharedMesh);
        TemporaryMesh meshPositive = new TemporaryMesh();
        TemporaryMesh meshNegative = new TemporaryMesh();
        bool useNormals = mesh.normals.Count > 0;
        bool useTangents = mesh.tangents.Count > 0;

        // Hash types to track computed cuts
        Dictionary<VertexPair, VertexPair> cutLines = new Dictionary<VertexPair, VertexPair>();
        int triangleLength = mesh.triangles.Count;
        for (int i = 0; i < triangleLength; i += 3)
        {
            int v1 = mesh.triangles[i];
            int v2 = mesh.triangles[i + 1];
            int v3 = mesh.triangles[i + 2];

            float d1 = Mathf.Sign(m_plane.DistanceToPoint(mesh.vertices[v1]));
            float d2 = Mathf.Sign(m_plane.DistanceToPoint(mesh.vertices[v2]));
            float d3 = Mathf.Sign(m_plane.DistanceToPoint(mesh.vertices[v3]));

            bool cut = d1 != d2 || d1 != d3;

            if (!cut)
            {
                if (d1 > 0)
                {
                    meshPositive.CopyTriangleFromMesh(mesh, v1, v2, v3, useNormals, useTangents);
                }
                else
                {
                    meshNegative.CopyTriangleFromMesh(mesh, v1, v2, v3, useNormals, useTangents);
                }
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

                VertexPair line1 = new VertexPair(base1, outlier);

                // Check if we need to cut the first line
                if (!cutLines.ContainsKey(line1))
                {
                    Vector3 a = mesh.vertices[line1.v1];
                    Vector3 b = mesh.vertices[line1.v2];

                    Vector3 c = mesh.normals[line1.v1];
                    Vector3 d = mesh.normals[line1.v2];

                    Vector3 e = mesh.tangents[line1.v1];
                    Vector3 f = mesh.tangents[line1.v2];

                    PlaneSliceLineIntersection intersection = m_plane.GetLineIntersection(a, b - a);
                    Vector3 newNormal = Vector3.negativeInfinity;
                    Vector4 newTangent = Vector4.negativeInfinity;
                    if (useNormals) newNormal = Vector3.Lerp(c, d, intersection.directionIntersectionScalar);
                    if (useTangents) newTangent = Vector4.Lerp(e, f, intersection.directionIntersectionScalar);

                    int iPos = meshPositive.AddPoint(intersection.intersectionPoint, newNormal, newTangent);
                    int iNeg = meshNegative.AddPoint(intersection.intersectionPoint, newNormal, newTangent);
                    cutLines.Add(line1, new VertexPair(iPos, iNeg));
                }

                VertexPair line2 = new VertexPair(base2, outlier);

                // Check if we need to cut the second line
                if (!cutLines.ContainsKey(line2))
                {
                    Vector3 a = mesh.vertices[line2.v1];
                    Vector3 b = mesh.vertices[line2.v2];

                    Vector3 c = mesh.normals[line2.v1];
                    Vector3 d = mesh.normals[line2.v2];

                    Vector3 e = mesh.tangents[line2.v1];
                    Vector3 f = mesh.tangents[line2.v2];

                    PlaneSliceLineIntersection intersection = m_plane.GetLineIntersection(a, b - a);
                    Vector3 newNormal = Vector3.negativeInfinity;
                    Vector4 newTangent = Vector4.negativeInfinity;
                    if (useNormals) newNormal = Vector3.Lerp(c, d, intersection.directionIntersectionScalar);
                    if (useTangents) newTangent = Vector4.Lerp(e, f, intersection.directionIntersectionScalar);

                    int iPos = meshPositive.AddPoint(intersection.intersectionPoint, newNormal, newTangent);
                    int iNeg = meshNegative.AddPoint(intersection.intersectionPoint, newNormal, newTangent);
                    cutLines.Add(line2, new VertexPair(iPos, iNeg));
                }

                // Add new triangles!
                if (Mathf.Sign(m_plane.DistanceToPoint(mesh.vertices[outlier])) > 0)
                {
                    int p1 = meshPositive.AddPoint(mesh.vertices[outlier], mesh.normals[outlier], mesh.tangents[outlier]);
                    int p2 = meshNegative.AddPoint(mesh.vertices[base1], mesh.normals[base1], mesh.tangents[base1]);
                    int p3 = meshNegative.AddPoint(mesh.vertices[base2], mesh.normals[base2], mesh.tangents[base2]);
                    meshPositive.AddTriangle(p1, cutLines[line2].v1, cutLines[line1].v1);
                    meshNegative.AddTriangle(cutLines[line1].v2, cutLines[line2].v2, p3);
                    meshNegative.AddTriangle(p3, p2, cutLines[line1].v2);
                }
                else
                {
                    int p1 = meshNegative.AddPoint(mesh.vertices[outlier], mesh.normals[outlier], mesh.tangents[outlier]);
                    int p2 = meshPositive.AddPoint(mesh.vertices[base1], mesh.normals[base1], mesh.tangents[base1]);
                    int p3 = meshPositive.AddPoint(mesh.vertices[base2], mesh.normals[base2], mesh.tangents[base2]);
                    meshNegative.AddTriangle(p1, cutLines[line2].v2, cutLines[line1].v2);
                    meshPositive.AddTriangle(cutLines[line1].v1, cutLines[line2].v1, p3);
                    meshPositive.AddTriangle(p3, p2, cutLines[line1].v1);
                }
            }
        }

        filter.sharedMesh = meshPositive.ConvertToFinalMesh();

        GameObject NewObj = GameObject.Instantiate(this.gameObject);
        NewObj.GetComponent<MeshFilter>().sharedMesh = meshNegative.ConvertToFinalMesh();
        NewObj.transform.SetParent(this.transform.parent, false);

        Debug.LogError("Slice took " + (Time.realtimeSinceStartup - startTime));
    }
}