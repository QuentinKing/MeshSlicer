using System.Collections;
using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// Struct for defining the solution to a plane / line intersection
/// </summary>
[System.Serializable]
public struct PlaneSliceLineIntersection
{
    public bool uniqueSolution;
    public PlaneSlice plane;
    public Vector3 linePoint;
    public Vector3 lineDirection;
    public Vector3 intersectionPoint;
    public float directionIntersectionScalar;

    public PlaneSliceLineIntersection(
        bool uniqueSolution,
        PlaneSlice plane,
        Vector3 linePoint,
        Vector3 lineDirection,
        Vector3 intersectionPoint,
        float scalar)
    {
        this.uniqueSolution = uniqueSolution;
        this.plane = plane;
        this.linePoint = linePoint;
        this.lineDirection = lineDirection;
        this.intersectionPoint = intersectionPoint;
        this.directionIntersectionScalar = scalar;
    }
}

/// <summary>
/// This struct contains the necessary components to define
/// a slicing plane in 3D space
/// </summary>
[System.Serializable]
public class PlaneSlice
{
    public Vector3 normal = Vector3.up;
    public Vector3 point = Vector3.zero;

    /// <summary>
    /// Calculates the signed distance from given target to the plane
    /// </summary>
    /// <param name="target">Target point</param>
    /// <returns>Distance from given target to the plane</returns>
    public float DistanceToPoint(Vector3 target)
    {
        return Vector3.Dot(this.normal, target - point);
    }

    /// <summary>
    /// Determines whether the current plane equation is valid
    /// </summary>
    /// <returns>True, if the plane equation is valid</returns>
    public bool IsValid()
    {
        bool pointValid = this.point != Vector3.negativeInfinity;
        bool normalValid = this.normal != Vector3.zero;
        return pointValid && normalValid;
    }

    /// <summary>
    /// Computes two orthogonal lines that lie on the plane
    /// </summary>
    /// <returns>A two element list containing the orthogonal axes</returns>
    public List<Vector3> GetCoordinateLines()
    {
        List<Vector3> lines = new List<Vector3>();
        Vector3 n = Vector3.Normalize(this.normal);

        bool isNormalUp = Vector3.Distance(n, Vector3.up) < 0.01f;
        Vector3 tempCrossVector = isNormalUp ? Vector3.left : Vector3.up;
        lines.Add(Vector3.Normalize(Vector3.Cross(n, tempCrossVector)));
        lines.Add(Vector3.Normalize(Vector3.Cross(n, lines[0])));
        return lines;
    }

    /// <summary>
    /// Calculate the intersection between a line and this plane
    /// </summary>
    /// <param name="point">Point on line</param>
    /// <param name="direction">Direction of line</param>
    /// <returns>The intersection struct, if there is not a unique solution, it contains an intersection
    /// point of Vector3.negativeInfinity</returns>
    public PlaneSliceLineIntersection GetLineIntersection(Vector3 point, Vector3 direction)
    {
        float u = Vector3.Dot(this.normal, direction);
        if (u == 0.0f)
        {
            return new PlaneSliceLineIntersection(false, this, point, direction, Vector3.negativeInfinity, float.MinValue);
        }
        else
        {
            float t = -Vector3.Dot(this.normal, point - this.point) / u;
            return new PlaneSliceLineIntersection(true, this, point, direction, point + t * direction, t);
        }
    }

    public PlaneSlice TransformIntoObjectSpace(Transform obj)
    {
        PlaneSlice newPlane = new PlaneSlice();
        newPlane.point = obj.InverseTransformPoint(this.point);
        newPlane.normal = Vector3.Normalize(obj.InverseTransformDirection(this.normal));
        return newPlane;
    }
}