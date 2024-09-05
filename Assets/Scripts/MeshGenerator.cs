using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class MeshGenerator : MonoBehaviour
{
    GridGenerator gridGenerator;
    MarchingCubesLookupTables marchingCubesLookupTables;

    NativeParallelHashMap<Vector3, DataStructures.Cell> grid;
    List<DataStructures.Triangle> triangles;
    
    public float isoLevel = 2f;

    Mesh mesh;
    MeshFilter meshFilter;

    public GameObject statusIndicator;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        //if (UnityEngine.Input.GetMouseButtonDown(1))
        //{
        //    GenerateMesh();
        //    DrawMesh();
        //}
    }

    public void GenerateMesh()
    {
        gridGenerator = GetComponent<GridGenerator>();
        marchingCubesLookupTables = GetComponent<MarchingCubesLookupTables>();
        meshFilter = GetComponent<MeshFilter>();
        mesh = new Mesh();
        meshFilter.mesh = mesh;
        grid = gridGenerator.grid;
        triangles = new List<DataStructures.Triangle>();
        
        // Iterate through the grid
        foreach (var cell in grid)
        {
            Vector3 pos = cell.Key;

            // Form a cube from 8 neighboring cells
            Vector3[] cubeCorners = new Vector3[8]
            {
                pos,
                pos + Vector3.right,
                pos + Vector3.right + Vector3.forward,
                pos + Vector3.forward,
                pos + Vector3.up,
                pos + Vector3.up + Vector3.right,
                pos + Vector3.up + Vector3.right + Vector3.forward,
                pos + Vector3.up + Vector3.forward
            };

            // Get the cube configuration
            int cubeIndex = 0;
            for (int i = 0; i < 8; i++)
            {
                if (GetCellValue(cubeCorners[i]) >= isoLevel)
                {
                    cubeIndex |= 1 << i;
                }
            }

            // Generate triangles based on the configuration
            triangles.AddRange(GenerateTrianglesForCube(cubeCorners, cubeIndex));
        }        
    }

    private float GetCellValue(Vector3 pos)
    {
        return grid.TryGetValue(pos, out DataStructures.Cell value) ? value.state : 0;
    }

    private List<DataStructures.Triangle> GenerateTrianglesForCube(Vector3[] corners, int cubeIndex)
    {
        List<DataStructures.Triangle> triangles = new List<DataStructures.Triangle>();

        // Early exit if the cube is entirely inside or outside the isosurface
        if (cubeIndex == 0 || cubeIndex == 255) return triangles;
        
        // Use a lookup table to determine which edges of the cube are intersected
        int[] edgeIndices = marchingCubesLookupTables.TriangleTable[cubeIndex];

        for (int i = 0; i < 15; i += 3) // Each group of 3 indices forms a triangle
        {
            if (edgeIndices[i] == -1) break; // -1 indicates end of triangles for this configuration

            Vector3 v1 = InterpolateVertex(corners, marchingCubesLookupTables.EdgeIndexTable[edgeIndices[i]]);
            Vector3 v2 = InterpolateVertex(corners, marchingCubesLookupTables.EdgeIndexTable[edgeIndices[i + 1]]);
            Vector3 v3 = InterpolateVertex(corners, marchingCubesLookupTables.EdgeIndexTable[edgeIndices[i + 2]]);

            triangles.Add(new DataStructures.Triangle(v1, v2, v3));
        }

        return triangles;
    }

    private Vector3 InterpolateVertex(Vector3[] corners, int[] edgeIndices)
    {
        Vector3 v1 = corners[edgeIndices[0]];
        Vector3 v2 = corners[edgeIndices[1]];
        //float value1 = GetCellValue(v1);
        //float value2 = GetCellValue(v2);

        //float t = (isoLevel - value1) / (value2 - value1);

        //return Vector3.Lerp(v1, v2, t);

        if (Vector3LessThan(v2, v1))
        {
            Vector3 temp;
            temp = v1;
            v1 = v2;
            v2 = temp;
        }

        float value1 = GetCellValue(v1);
        float value2 = GetCellValue(v2);        

        Vector3 lerpVector;
        if (Mathf.Abs(value1 - value2) > 0.00001)
        {
            lerpVector = v1 + (v2 - v1) / (value2 - value1) * (0.5f - value1);
        }
        else
        {
            lerpVector = v1;
        }

        return lerpVector;
    }
    bool Vector3LessThan(Vector3 left, Vector3 right)
    {
        if (left.x < right.x)
            return true;
        else if (left.x > right.x)
            return false;

        if (left.y < right.y)
            return true;
        else if (left.y > right.y)
            return false;

        if (left.z < right.z)
            return true;
        else if (left.z > right.z)
            return false;

        return false;
    }

    public void DrawMesh()
    {
        // Draw triangles
        if (triangles.Count > 0)
        {
            Vector3[] vertices = new Vector3[triangles.Count * 3];
            int[] indices = new int[triangles.Count * 3];

            int index = 0;
            for (int i = 0; i < triangles.Count; i++)
            {
                vertices[index] = triangles[i].v1;
                vertices[index + 1] = triangles[i].v2;
                vertices[index + 2] = triangles[i].v3;

                indices[index] = index;
                indices[index + 1] = index + 1;
                indices[index + 2] = index + 2;

                index += 3;
            }
            
            mesh.vertices = vertices;
            mesh.triangles = indices;
        }
    }

    public void ClearMesh()
    {
        if (mesh != null) mesh.Clear();
    }

}
