using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;

public class DataStructures : MonoBehaviour
{
    public struct Cell
    {
        public int state;
        public FixedList4096Bytes<float3> neighbours;
        public int cubeInstanceID;

        public Cell(int state, FixedList4096Bytes<float3> neighbours, int cubeInstanceID)
        {
            this.state = state;
            this.neighbours = neighbours;
            this.cubeInstanceID = cubeInstanceID;
        }
    }

    public struct Triangle
    {
        public Vector3 v1, v2, v3;

        public Triangle(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
        }
    }

    public struct HermiteData
    {
        public List<Vector3> intersections;
        public List<Vector3> normals;
        public Vector3 vertex;

        public HermiteData(List<Vector3> intersections, List<Vector3> normals, Vector3 vertex)
        {
            this.intersections = intersections;
            this.normals = normals;
            this.vertex = vertex;
        }
    }
}