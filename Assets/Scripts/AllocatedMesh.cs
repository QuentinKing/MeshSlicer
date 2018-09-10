using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AllocatedMesh
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<int> triangles = new List<int>();
    public List<Vector3> normals = new List<Vector3>();
    public List<Vector4> tangents = new List<Vector4>();

    public AllocatedMesh(Mesh baseMesh)
    {
        baseMesh.GetVertices(vertices);
        baseMesh.GetTriangles(triangles, 0);
        baseMesh.GetNormals(normals);
        baseMesh.GetTangents(tangents);
    }
}
