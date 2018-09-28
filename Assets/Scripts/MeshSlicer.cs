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

    public void OnDrawGizmosSelected()
    {
        if (!m_debugDraw)
            return;

        if (m_plane != null && m_plane.IsValid())
        {
            List<Vector3> axes = m_plane.GetCoordinateLines();
            axes[0] = this.transform.rotation * axes[0];
            axes[1] = this.transform.rotation * axes[1];
            Vector3 point = Vector3.Scale(this.transform.rotation * m_plane.point, this.transform.localScale);

            Mesh planeMesh = new Mesh();
            planeMesh.vertices = new Vector3[] { point + (axes[0] + axes[1]) * m_debugPlaneSize,
                                                 point + (axes[0] - axes[1]) * m_debugPlaneSize,
                                                 point - (axes[0] - axes[1]) * m_debugPlaneSize,
                                                 point - (axes[0] + axes[1]) * m_debugPlaneSize};
            planeMesh.triangles = new int[] { 0, 3, 1,  0, 1, 3,  0, 2, 3,  0, 3, 2}; // Render back faces
            planeMesh.RecalculateNormals();

            Gizmos.color = m_debugColor;
            Gizmos.DrawMesh(planeMesh, this.transform.position);
        }
    }

    public void SliceAllChildren()
    {
        List<GameObject> originalChildren = new List<GameObject>();

        foreach (Transform child in this.transform)
        {
            originalChildren.Add(child.gameObject);
        }

        foreach (GameObject child in originalChildren)
        {
            SliceMesh(child);
        }
    }

    public void SliceCurrentMesh()
    {
        SliceMesh(this.gameObject);
    }

    private Vector3 RoundVector3(Vector3 v, int decimals)
    {
        float x = (float)Math.Round(v.x, decimals);
        float y = (float)Math.Round(v.y, decimals);
        float z = (float)Math.Round(v.z, decimals);
        return new Vector3(x, y, z);
    }



    /// <summary>
    /// Slices the given mesh along the currently set plane
    /// </summary>
    private void SliceMesh(GameObject meshObject)
    {
        float startTime = Time.realtimeSinceStartup;

        if (m_plane == null || !m_plane.IsValid())
        {
            Debug.LogError("Invalid plane on gameobject " + this.gameObject.ToString());
            return;
        }

        MeshFilter filter = meshObject.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            Debug.LogError("No mesh filter attached to gameobject " + this.gameObject.ToString());
            return;
        }

        // We will to allocate space for the original mesh data since we are constantly
        // accessing the vertices, normals, tangents, etc.
        AllocatedMesh mesh = new AllocatedMesh(filter.sharedMesh);

        // Create two temporary meshs where we will be adding new vertices and whatnot.
        // Once we finish all the calculations we will convert these objects into real unity meshes.
        TemporaryMesh meshPositive = new TemporaryMesh();
        TemporaryMesh meshNegative = new TemporaryMesh();

        bool useNormals = mesh.normals.Count > 0;
        bool useTangents = mesh.tangents.Count > 0;

        // Keep a look up table of vertices on the sliced cross section so we can link them together nicely
        Dictionary<Vector3, int> positiveBoundary = new Dictionary<Vector3, int>();
        Dictionary<Vector3, int> negativeBoundary = new Dictionary<Vector3, int>();

        int triangleLength = mesh.triangles.Count;
        for (int i = 0; i < triangleLength; i += 3)
        {
            int v1 = mesh.triangles[i];
            int v2 = mesh.triangles[i + 1];
            int v3 = mesh.triangles[i + 2];
            float d1 = Mathf.Sign(m_plane.DistanceToPoint(mesh.vertices[v1]));
            float d2 = Mathf.Sign(m_plane.DistanceToPoint(mesh.vertices[v2]));
            float d3 = Mathf.Sign(m_plane.DistanceToPoint(mesh.vertices[v3]));

            // If there are vertices on either side of the plane, we need to cut this triangle
            bool cut = d1 != d2 || d1 != d3;

            if (!cut)
            {
                // No cut, so just copy this triangle from the original mesh to it's respective new temporary mesh
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
                // base1 and base2 lie on the same side of the plane, outlier lies on the opposite side
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

                Vector3 base1_v = mesh.vertices[base1];
                Vector3 base2_v = mesh.vertices[base2];
                Vector3 outlier_v = mesh.vertices[outlier];
                Vector3 base1_n = mesh.normals[base1];
                Vector3 base2_n = mesh.normals[base2];
                Vector3 outlier_n = mesh.normals[outlier];
                Vector3 base1_t = mesh.tangents[base1];
                Vector3 base2_t = mesh.tangents[base2];
                Vector3 outlier_t = mesh.tangents[outlier];

                // Find the intersection between the plane and the two intersecting lines of the triangle,
                // then interpolate the vertices values, and create new ones at the intersection point.
                PlaneSliceLineIntersection intersection1 = m_plane.GetLineIntersection(base1_v, outlier_v - base1_v);
                Vector3 newNormal1 = Vector3.negativeInfinity;
                Vector4 newTangent1 = Vector4.negativeInfinity;
                if (useNormals) newNormal1 = Vector3.Lerp(base1_n, outlier_n, intersection1.directionIntersectionScalar);
                if (useTangents) newTangent1 = Vector4.Lerp(base1_t, outlier_t, intersection1.directionIntersectionScalar);
                int iPos1 = meshPositive.AddPoint(intersection1.intersectionPoint, newNormal1, newTangent1);
                int iNeg1 = meshNegative.AddPoint(intersection1.intersectionPoint, newNormal1, newTangent1);

                PlaneSliceLineIntersection intersection2 = m_plane.GetLineIntersection(base2_v, outlier_v - base2_v);
                Vector3 newNormal2 = Vector3.negativeInfinity;
                Vector4 newTangent2 = Vector4.negativeInfinity;
                if (useNormals) newNormal2 = Vector3.Lerp(base2_n, outlier_n, intersection2.directionIntersectionScalar);
                if (useTangents) newTangent2 = Vector4.Lerp(base2_t, outlier_t, intersection2.directionIntersectionScalar);
                int iPos2 = meshPositive.AddPoint(intersection2.intersectionPoint, newNormal2, newTangent2);
                int iNeg2 = meshNegative.AddPoint(intersection2.intersectionPoint, newNormal2, newTangent2);

                // Create new boundary vertices that lie on the cross section on the mesh and plane.
                // These will be used to cap off the hole thats made by slicing the mesh.
                int b1;
                int b2;
                int b3;
                int b4;

                // For the lookup table, this prevents some floating point errors. The decimals may have to be
                // adjusted depending on the fidelity of the mesh, but this should be enough for even really high-res
                // meshes
                Vector3 approxIntersection1 = RoundVector3(intersection1.intersectionPoint, 5);
                Vector3 approxIntersection2 = RoundVector3(intersection2.intersectionPoint, 5);

                // Add these verts to our boundary lists
                if (!positiveBoundary.ContainsKey(approxIntersection1))
                {
                    b1 = meshPositive.AddPoint(intersection1.intersectionPoint, -m_plane.normal, newTangent1);
                    positiveBoundary[approxIntersection1] = b1;
                }
                else
                {
                    b1 = positiveBoundary[approxIntersection1];
                }

                if (!negativeBoundary.ContainsKey(approxIntersection1))
                {
                    b2 = meshNegative.AddPoint(intersection1.intersectionPoint, m_plane.normal, newTangent1);
                    negativeBoundary[approxIntersection1] = b2;
                }
                else
                {
                    b2 = negativeBoundary[approxIntersection1];
                }

                if (!positiveBoundary.ContainsKey(approxIntersection2))
                {
                    b3 = meshPositive.AddPoint(intersection2.intersectionPoint, -m_plane.normal, newTangent2);
                    positiveBoundary[approxIntersection2] = b3;
                }
                else
                {
                    b3 = positiveBoundary[approxIntersection2];
                }

                if (!negativeBoundary.ContainsKey(approxIntersection2))
                {
                    b4 = meshNegative.AddPoint(intersection2.intersectionPoint, m_plane.normal, newTangent2);
                    negativeBoundary[approxIntersection2] = b4;
                }
                else
                {
                    b4 = negativeBoundary[approxIntersection2];
                }

                // The triangle we sliced is now three new triangles, add these to our new meshes
                if (Mathf.Sign(m_plane.DistanceToPoint(mesh.vertices[outlier])) > 0)
                {
                    int p1 = meshPositive.AddPoint(mesh.vertices[outlier], mesh.normals[outlier], mesh.tangents[outlier]);
                    int p2 = meshNegative.AddPoint(mesh.vertices[base1], mesh.normals[base1], mesh.tangents[base1]);
                    int p3 = meshNegative.AddPoint(mesh.vertices[base2], mesh.normals[base2], mesh.tangents[base2]);
                    meshPositive.AddTriangle(p1, iPos2, iPos1);
                    meshNegative.AddTriangle(iNeg1, iNeg2, p3);
                    meshNegative.AddTriangle(p3, p2, iNeg1);

                    meshPositive.RegisterBoundaryLine(b3, b1);
                    meshNegative.RegisterBoundaryLine(b2, b4);
                }
                else
                {
                    int p1 = meshNegative.AddPoint(mesh.vertices[outlier], mesh.normals[outlier], mesh.tangents[outlier]);
                    int p2 = meshPositive.AddPoint(mesh.vertices[base1], mesh.normals[base1], mesh.tangents[base1]);
                    int p3 = meshPositive.AddPoint(mesh.vertices[base2], mesh.normals[base2], mesh.tangents[base2]);
                    meshNegative.AddTriangle(p1, iNeg2, iNeg1);
                    meshPositive.AddTriangle(iPos1, iPos2, p3);
                    meshPositive.AddTriangle(p3, p2, iPos1);

                    meshPositive.RegisterBoundaryLine(b1, b3);
                    meshNegative.RegisterBoundaryLine(b4, b2);
                }
            }
        }

        // Cap off the cross section
        meshPositive.CapBoundaires();
        meshNegative.CapBoundaires();

        // Create and assign our new meshes
        filter.sharedMesh = meshPositive.ConvertToFinalMesh();
        GameObject NewObj = GameObject.Instantiate(meshObject);
        NewObj.GetComponent<MeshFilter>().sharedMesh = meshNegative.ConvertToFinalMesh();
        NewObj.transform.SetParent(meshObject.transform.parent, false);

        Debug.LogError("Slice took " + (Time.realtimeSinceStartup - startTime));
    }
}