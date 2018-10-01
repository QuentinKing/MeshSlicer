using System.Collections;
using System.Collections.Generic;

using UnityEngine;

public class BoundaryVertex
{
    private TemporaryMesh parentMesh;

    public BoundaryVertex next;
    public BoundaryVertex previous;

    public int meshIndex;

    public BoundaryVertex(TemporaryMesh parentMesh, int meshIndex)
    {
        this.parentMesh = parentMesh;
        this.meshIndex = meshIndex;
    }

    public void AssignNext(BoundaryVertex next)
    {
        this.next = next;
        next.previous = this;
    }

    public void AssignPrevious(BoundaryVertex previous)
    {
        this.previous = previous;
        previous.next = this;
    }

    public Vector3 GetLineToNext()
    {
        return parentMesh.GetVertex(next.meshIndex) - parentMesh.GetVertex(this.meshIndex);
    }

    public Vector3 GetLineFromPrevious()
    {
        return parentMesh.GetVertex(this.meshIndex) - parentMesh.GetVertex(previous.meshIndex);
    }

    public bool IsConvex()
    {
        if (previous == null || next == null)
            return false;

        return Vector3.SignedAngle(GetLineFromPrevious(), GetLineToNext(), -parentMesh.GetNormal(this.meshIndex)) > 0.0f;
    }
}

public class Boundary
{
    private TemporaryMesh parentMesh;

    public List<BoundaryVertex> vertices = new List<BoundaryVertex>();
    public List<BoundaryVertex> earTips = new List<BoundaryVertex>();

    public Boundary(TemporaryMesh parentMesh, BoundaryVertex rootVertex)
    {
        this.parentMesh = parentMesh;

        BoundaryVertex first = rootVertex;
        vertices.Add(first);

        BoundaryVertex current = rootVertex.next;
        while (current != null && current != first)
        {
            vertices.Add(current);
            current = current.next;
        }

        CheckAllEarTips();
    }

    public void RemoveEar(BoundaryVertex ear)
    {
        ear.previous.next = ear.next;
        ear.next.previous = ear.previous;

        vertices.Remove(ear);
        earTips.Remove(ear);

        CheckVertexForEarTip(ear.previous);
        CheckVertexForEarTip(ear.next);
    }

    private void CheckVertexForEarTip(BoundaryVertex vertex)
    {
        if (!vertex.IsConvex() || earTips.Contains(vertex))
            return;

        BoundaryVertex prev = vertex.previous;
        BoundaryVertex next = vertex.next;

        foreach (BoundaryVertex other in vertices)
        {
            if (other == vertex || other == prev || other == next)
                continue;

            Vector3 PA = parentMesh.GetVertex(vertex.meshIndex) - parentMesh.GetVertex(other.meshIndex);
            Vector3 PB = parentMesh.GetVertex(prev.meshIndex) - parentMesh.GetVertex(other.meshIndex);
            Vector3 PC = parentMesh.GetVertex(next.meshIndex) - parentMesh.GetVertex(other.meshIndex);

            float AngAPB = Mathf.Abs(Vector3.SignedAngle(PA, PB, -parentMesh.GetNormal(vertex.meshIndex)));
            float AngBPC = Mathf.Abs(Vector3.SignedAngle(PB, PC, -parentMesh.GetNormal(vertex.meshIndex)));
            float AngAPC = Mathf.Abs(Vector3.SignedAngle(PA, PC, -parentMesh.GetNormal(vertex.meshIndex)));

            // Is this point within the triangle? If so, not an ear tip
            if (Mathf.Min(new float[] { AngAPB + AngBPC, AngAPB + AngAPC, AngBPC + AngAPC }) >= 180.0f)
                return;
        }

        earTips.Add(vertex);
    }

    private void CheckAllEarTips()
    {
        earTips.Clear();
        for (int i = 0; i < vertices.Count; i++)
        {
            CheckVertexForEarTip(vertices[i]);
        }
    }
}

public class BoundaryVertexTable
{
    private TemporaryMesh parentMesh;

    // Look up table for easy / fast access
    public Dictionary<int, BoundaryVertex> vertexLut = new Dictionary<int, BoundaryVertex>();

    public BoundaryVertexTable(TemporaryMesh parentMesh)
    {
        this.parentMesh = parentMesh;
    }

    public void AddPoint(int v)
    {
        if (!vertexLut.ContainsKey(v))
        {
            vertexLut[v] = new BoundaryVertex(parentMesh, v);
        }
    }

    public void AddLine(int v1, int v2)
    {
        AddPoint(v1);
        AddPoint(v2);
        vertexLut[v1].AssignNext(vertexLut[v2]);
    }

    public List<Boundary> GetBoundaries()
    {
        List<Boundary> boundaries = new List<Boundary>();
        HashSet<BoundaryVertex> checkedNodes = new HashSet<BoundaryVertex>();

        foreach (KeyValuePair<int, BoundaryVertex> vertex in vertexLut)
        {
            if (checkedNodes.Contains(vertex.Value))
                continue;

            BoundaryVertex first = vertex.Value;
            BoundaryVertex current = vertex.Value.next;
            checkedNodes.Add(first);

            while (current != null && current != first)
            {
                checkedNodes.Add(current);
                current = current.next;
            }

            if (current != null)
            {
                boundaries.Add(new Boundary(parentMesh, first));
            }
        }

        if (boundaries.Count > 1)
        {
            FindAndConnectHoles(boundaries);
        }

        return boundaries;
    }

    public Boundary FindAndConnectHoles(List<Boundary> boundaries)
    {
        return null;
    }
}
