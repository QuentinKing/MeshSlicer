using System.Collections;
using System.Collections.Generic;

using UnityEngine;

public class BoundaryNode
{
    public BoundaryNode start;
    public BoundaryNode previous;
    public BoundaryNode next;
    public BoundaryNode end;

    private int v1;
    private int v2;

    public BoundaryNode(int v1, int v2)
    {
        this.v1 = v1;
        this.v2 = v2;
        this.start = this;
        this.previous = null;
        this.next = null;
        this.end = this;
    }

    public void AssignNext(BoundaryNode next)
    {
        this.next = next;
        next.start = this.start;
        this.end = next.end;
    }

    public void AssignPrevious(BoundaryNode previous)
    {
        this.previous = previous;
        previous.end = this.end;
        this.start = previous.start;
    }

    public bool IsCircular()
    {
        return this.start == this.end && this.next != null && this.previous != null;
    }
}

public class BoundaryList
{
    Dictionary<int, BoundaryNode> v1Lut = new Dictionary<int, BoundaryNode>();
    Dictionary<int, BoundaryNode> v2Lut = new Dictionary<int, BoundaryNode>();

    public List<BoundaryNode> circularLists = new List<BoundaryNode>(); 

    public void AddLine(int v1, int v2)
    {
        BoundaryNode newNode = new BoundaryNode(v1, v2);

        bool hasPrevious = v2Lut.ContainsKey(v1);
        bool hasNext = v1Lut.ContainsKey(v2);

        v1Lut[v1] = newNode;
        v2Lut[v2] = newNode;

        if (hasPrevious)
            v2Lut[v1].AssignNext(newNode);

        if (hasNext)
            v1Lut[v2].AssignPrevious(newNode);

        if (newNode.IsCircular() && !circularLists.Contains(newNode.start))
            circularLists.Add(newNode.start);
    }
}
