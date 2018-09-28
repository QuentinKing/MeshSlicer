using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TemporaryMesh
{
    protected Dictionary<int, int> copiedVerts = new Dictionary<int, int>();
    protected List<int> newTriangles = new List<int>();
    protected List<Vector3> newVertices = new List<Vector3>();
    protected List<Vector3> newNormals = new List<Vector3>();
    protected List<Vector4> newTangents = new List<Vector4>();

    public BoundaryVertexTable boundary;

    public TemporaryMesh()
    {
        boundary = new BoundaryVertexTable(this);
    }

    private int CopyVertex(AllocatedMesh reference, int vertex, bool copyNormals, bool copyTangents)
    {
        if (!copiedVerts.ContainsKey(vertex))
        {
            newVertices.Add(reference.vertices[vertex]);
            if (copyNormals) newNormals.Add(reference.normals[vertex]);
            if (copyTangents) newTangents.Add(reference.tangents[vertex]);
            copiedVerts[vertex] = newVertices.Count - 1;
        }

        return copiedVerts[vertex];
    }

    public void CopyTriangleFromMesh(AllocatedMesh reference, int v1, int v2, int v3, bool copyNormals, bool copyTangents)
    {
        int newVert1 = CopyVertex(reference, v1, copyNormals, copyTangents);
        int newVert2 = CopyVertex(reference, v2, copyNormals, copyTangents);
        int newVert3 = CopyVertex(reference, v3, copyNormals, copyTangents);

        newTriangles.AddRange(new List<int>()
        {
            newVert1,
            newVert2,
            newVert3
        });
    }

    public void RegisterBoundaryLine(int v1, int v2)
    {
        if (v1 == v2)
        {
            boundary.AddPoint(v1);
        }
        else
        {
            boundary.AddLine(v1, v2);
        }
    }

    public void CapBoundaires()
    {
        List<Boundary> boundaires = boundary.GetBoundaries();

        // TODO: check for holes and support non genus 0 polygons

        foreach (Boundary b in boundaires)
        {
            CapBoundary(b);
        }
    }

    private void CapBoundary(Boundary boundary)
    {
        int i = 0;
        while (boundary.vertices.Count >= 3 && i < 1000000)
        {
            BoundaryVertex tipToRemove = boundary.earTips[0];
            AddTriangle(tipToRemove.next.meshIndex, tipToRemove.meshIndex, tipToRemove.previous.meshIndex);
            boundary.RemoveEar(tipToRemove);
            i++;
        }
    }

    public int AddPoint(Vector3 vertex, Vector3 normal, Vector4 tangent)
    {
        newVertices.Add(vertex);
        if (normal != Vector3.negativeInfinity) newNormals.Add(normal);
        if (tangent != Vector4.negativeInfinity) newTangents.Add(normal);
        return newVertices.Count - 1;
    }

    public void AddTriangle(int v1, int v2, int v3)
    {
        newTriangles.AddRange(new List<int> { v1, v2, v3 });
    }

    public Vector3 GetVertex(int index)
    {
        return newVertices[index];
    }

    public Vector3 GetNormal(int index)
    {
        return newNormals[index];
    }

    public Mesh ConvertToFinalMesh()
    {
        Mesh mesh = new Mesh();
        mesh.SetVertices(newVertices);
        mesh.SetTriangles(newTriangles, 0);
        mesh.SetNormals(newNormals);
        mesh.SetTangents(newTangents);
        return mesh;
    }
}
