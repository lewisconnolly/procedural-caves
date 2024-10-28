using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using static DataStructures;
using static GridGenerator;
using UnityEngine.UI;

public class MeshGenerator2 : MonoBehaviour
{
    GridGenerator gridGenerator;
    SpeleothemGenerator speleothemGenerator;

    NativeParallelHashMap<Vector3, DataStructures.Cell> grid;
    List<DataStructures.Triangle> triangles;

    public int isoLevel = 2;

    [Range(0f,25f)]
    public float speleothemPercent = 10f;
    public bool generateSpeleothems = true;

    Mesh mesh;
    MeshFilter meshFilter;
    //MeshCollider meshCollider;

    public GameObject statusIndicator;

    public TMP_InputField mcIsoLevelIF;

    private void GetInput()
    {
        isoLevel = int.Parse(mcIsoLevelIF.text);        
    }

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
        GetInput();
        
        gridGenerator = GetComponent<GridGenerator>();
        meshFilter = GetComponent<MeshFilter>();
        //meshCollider = GetComponent<MeshCollider>();
        speleothemGenerator = GetComponent<SpeleothemGenerator>();
        mesh = new Mesh();
        meshFilter.mesh = mesh;
        grid = gridGenerator.grid;
        triangles = new List<DataStructures.Triangle>();

        // Iterate through the grid
        foreach (var cell in grid)
        {
            // Don't form cubes with cells at edges of grid
            if (!(cell.Key.x == (float)(gridGenerator.width - 1) ||
                cell.Key.y == (float)(gridGenerator.height - 1) ||
                cell.Key.z == (float)(gridGenerator.depth - 1)))
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
                    if (GetCellValue(cubeCorners[i]) > 0)
                    {
                        cubeIndex |= 1 << i;
                    }
                }

                // Generate triangles based on the configuration
                triangles.AddRange(ProcessCube(cubeCorners, cubeIndex));
            }
        }

        if (generateSpeleothems) triangles.AddRange(GenerateSpeleothems());

        //meshCollider.sharedMesh = mesh;
    }

    private Vector3 GetCentreVertex(Vector3[] corners)
    {
        float u = 0;

        Vector3 centreVertex = new Vector3();

        foreach (int[] edge in MarchingCubesLookupTables2.EdgeIndexTable)
        {
            ++u;
            Vector3 v = InterpolateVertex(corners, edge);
            centreVertex.x += v.x;
            centreVertex.y += v.y;
            centreVertex.z += v.z;
        }
        
        centreVertex.x /= u;
        centreVertex.y /= u;
        centreVertex.z /= u;

        return centreVertex;
    }

    private int GetCellValue(Vector3 pos)
    {
        return grid.TryGetValue(pos, out DataStructures.Cell value) ? value.state - isoLevel : 0;
    }

    private List<DataStructures.Triangle> ProcessCube(Vector3[] corners, int cubeIndex)
    {
        List<DataStructures.Triangle> triangles = new List<DataStructures.Triangle>();

        Vector3 centreVertex = GetCentreVertex(corners);
        int mainCase = MarchingCubesLookupTables2.cases[cubeIndex][0];
        int config = MarchingCubesLookupTables2.cases[cubeIndex][1];
        int subConfig = 0;

        switch (mainCase)
        {
            case 0:
                break;

            case 1:
                triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling1[config], 1, centreVertex);
                break;

            case 2:
                triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling2[config], 2, centreVertex);
                break;

            case 3:
                if (TestFace(corners, MarchingCubesLookupTables2.test3[config]))
                    triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling3_2[config], 4, centreVertex); // 3.2
                else
                    triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling3_1[config], 2, centreVertex); // 3.1
                break;

            case 4:
                if (TestInterior(corners, mainCase, config, subConfig, MarchingCubesLookupTables2.test4[config]))
                    triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling4_1[config], 2, centreVertex); // 4.1.1
                else
                    triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling4_2[config], 6, centreVertex); // 4.1.2
                break;

            case 5:
                triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling5[config], 3, centreVertex);
                break;

            case 6:
                if (TestFace(corners, MarchingCubesLookupTables2.test6[config][0]))
                    triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling6_2[config], 5, centreVertex); // 6.2
                else
                {
                    if (TestInterior(corners, mainCase, config, subConfig, MarchingCubesLookupTables2.test6[config][1]))
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling6_1_1[config], 3, centreVertex); // 6.1.1
                    else
                    {
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling6_1_2[config], 9, centreVertex); // 6.1.2
                    }
                }
                break;

            case 7:
                if (TestFace(corners, MarchingCubesLookupTables2.test7[config][0])) subConfig += 1;
                if (TestFace(corners, MarchingCubesLookupTables2.test7[config][1])) subConfig += 2;
                if (TestFace(corners, MarchingCubesLookupTables2.test7[config][2])) subConfig += 4;
                switch (subConfig)
                {
                    case 0:
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling7_1[config], 3, centreVertex); break;
                    case 1:
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling7_2[config][0], 5, centreVertex); break;
                    case 2:
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling7_2[config][1], 5, centreVertex); break;
                    case 3:
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling7_3[config][0], 9, centreVertex); break;
                    case 4:
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling7_2[config][2], 5, centreVertex); break;
                    case 5:
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling7_3[config][1], 9, centreVertex); break;
                    case 6:
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling7_3[config][2], 9, centreVertex); break;
                    case 7:
                        if (TestInterior(corners, mainCase, config, subConfig, MarchingCubesLookupTables2.test7[config][3]))
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling7_4_2[config], 9, centreVertex);
                        else
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling7_4_1[config], 5, centreVertex);
                        break;
                };
                break;

            case 8:
                triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling8[config], 2, centreVertex);
                break;

            case 9:
                triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling9[config], 4, centreVertex);
                break;

            case 10:
                if (TestFace(corners, MarchingCubesLookupTables2.test10[config][0]))
                {
                    if (TestFace(corners, MarchingCubesLookupTables2.test10[config][1]))
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling10_1_1_[config], 4, centreVertex); // 10.1.1
                    else
                    {
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling10_2[config], 8, centreVertex); // 10.2
                    }
                }
                else
                {
                    if (TestFace(corners, MarchingCubesLookupTables2.test10[config][1]))
                    {
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling10_2_[config], 8, centreVertex); // 10.2
                    }
                    else
                    {
                        if (TestInterior(corners, mainCase, config, subConfig, MarchingCubesLookupTables2.test10[config][2]))
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling10_1_1[config], 4, centreVertex); // 10.1.1
                        else
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling10_1_2[config], 8, centreVertex); // 10.1.2
                    }
                }
                break;

            case 11:
                triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling11[config], 4, centreVertex);
                break;

            case 12:
                if (TestFace(corners, MarchingCubesLookupTables2.test12[config][0]))
                {
                    if (TestFace(corners, MarchingCubesLookupTables2.test12[config][1]))
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling12_1_1_[config], 4, centreVertex); // 12.1.1
                    else
                    {
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling12_2[config], 8, centreVertex); // 12.2
                    }
                }
                else
                {
                    if (TestFace(corners, MarchingCubesLookupTables2.test12[config][1]))
                    {
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling12_2_[config], 8, centreVertex); // 12.2
                    }
                    else
                    {
                        if (TestInterior(corners, mainCase, config, subConfig, MarchingCubesLookupTables2.test12[config][2]))
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling12_1_1[config], 4, centreVertex); // 12.1.1
                        else
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling12_1_2[config], 8, centreVertex); // 12.1.2
                    }
                }
                break;

            case 13:
                if (TestFace(corners, MarchingCubesLookupTables2.test13[config][0])) subConfig += 1;
                if (TestFace(corners, MarchingCubesLookupTables2.test13[config][1])) subConfig += 2;
                if (TestFace(corners, MarchingCubesLookupTables2.test13[config][2])) subConfig += 4;
                if (TestFace(corners, MarchingCubesLookupTables2.test13[config][3])) subConfig += 8;
                if (TestFace(corners, MarchingCubesLookupTables2.test13[config][4])) subConfig += 16;
                if (TestFace(corners, MarchingCubesLookupTables2.test13[config][5])) subConfig += 32;
                switch (MarchingCubesLookupTables2.subconfig13[subConfig])
                {
                    case 0:/* 13.1 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_1[config], 4, centreVertex); break;

                    case 1:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2[config][0], 6, centreVertex); break;
                    case 2:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2[config][1], 6, centreVertex); break;
                    case 3:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2[config][2], 6, centreVertex); break;
                    case 4:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2[config][3], 6, centreVertex); break;
                    case 5:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2[config][4], 6, centreVertex); break;
                    case 6:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2[config][5], 6, centreVertex); break;

                    case 7:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][0], 10, centreVertex); break;
                    case 8:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][1], 10, centreVertex); break;
                    case 9:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][2], 10, centreVertex); break;
                    case 10:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][3], 10, centreVertex); break;
                    case 11:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][4], 10, centreVertex); break;
                    case 12:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][5], 10, centreVertex); break;
                    case 13:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][6], 10, centreVertex); break;
                    case 14:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][7], 10, centreVertex); break;
                    case 15:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][8], 10, centreVertex); break;
                    case 16:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][9], 10, centreVertex); break;
                    case 17:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][10], 10, centreVertex); break;
                    case 18:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3[config][11], 10, centreVertex); break;

                    case 19:/* 13.4 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_4[config][0], 12, centreVertex); break;
                    case 20:/* 13.4 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_4[config][1], 12, centreVertex); break;
                    case 21:/* 13.4 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_4[config][2], 12, centreVertex); break;
                    case 22:/* 13.4 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_4[config][3], 12, centreVertex); break;

                    case 23:/* 13.5 */
                        subConfig = 0;
                        if (TestInterior(corners, mainCase, config, subConfig, MarchingCubesLookupTables2.test13[config][6]))
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_5_1[config][0], 6, centreVertex);
                        else
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_5_2[config][0], 10, centreVertex);
                        break;
                    case 24:/* 13.5 */
                        subConfig = 1;
                        if (TestInterior(corners, mainCase, config, subConfig, MarchingCubesLookupTables2.test13[config][6]))
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_5_1[config][1], 6, centreVertex);
                        else
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_5_2[config][1], 10, centreVertex);
                        break;
                    case 25:/* 13.5 */
                        subConfig = 2;
                        if (TestInterior(corners, mainCase, config, subConfig, MarchingCubesLookupTables2.test13[config][6]))
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_5_1[config][2], 6, centreVertex);
                        else
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_5_2[config][2], 10, centreVertex);
                        break;
                    case 26:/* 13.5 */
                        subConfig = 3;
                        if (TestInterior(corners, mainCase, config, subConfig, MarchingCubesLookupTables2.test13[config][6]))
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_5_1[config][3], 6, centreVertex);
                        else
                            triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_5_2[config][3], 10, centreVertex);
                        break;

                    case 27:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][0], 10, centreVertex); break;
                    case 28:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][1], 10, centreVertex); break;
                    case 29:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][2], 10, centreVertex); break;
                    case 30:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][3], 10, centreVertex); break;
                    case 31:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][4], 10, centreVertex); break;
                    case 32:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][5], 10, centreVertex); break;
                    case 33:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][6], 10, centreVertex); break;
                    case 34:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][7], 10, centreVertex); break;
                    case 35:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][8], 10, centreVertex); break;
                    case 36:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][9], 10, centreVertex); break;
                    case 37:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][10], 10, centreVertex); break;
                    case 38:/* 13.3 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_3_[config][11], 10, centreVertex); break;

                    case 39:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2_[config][0], 6, centreVertex); break;
                    case 40:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2_[config][1], 6, centreVertex); break;
                    case 41:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2_[config][2], 6, centreVertex); break;
                    case 42:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2_[config][3], 6, centreVertex); break;
                    case 43:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2_[config][4], 6, centreVertex); break;
                    case 44:/* 13.2 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_2_[config][5], 6, centreVertex); break;

                    case 45:/* 13.1 */
                        triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling13_1_[config], 4, centreVertex); break;

                    default:
                        Debug.Log("Marching Cubes: Impossible case 13?"); break;
                }
                break;

            case 14:
                triangles = GenerateTrianglesForCube(corners, MarchingCubesLookupTables2.tiling14[config], 4, centreVertex);
                break;
        }

        return triangles;
    }

    private List<DataStructures.Triangle> GenerateTrianglesForCube(Vector3[] corners, int[] edgeIndices, int numTriangles, Vector3 centreVertex)
    {
        List<DataStructures.Triangle> triangles = new List<DataStructures.Triangle>();

        for (int i = 0; i < 3 * numTriangles; i+=3)
        {                                    
            Vector3 v1 = (edgeIndices[i] != 12) ? InterpolateVertex(corners, MarchingCubesLookupTables2.EdgeIndexTable[edgeIndices[i]]) : centreVertex;
            Vector3 v2 = (edgeIndices[i + 1] != 12) ? InterpolateVertex(corners, MarchingCubesLookupTables2.EdgeIndexTable[edgeIndices[i + 1]]) : centreVertex;
            Vector3 v3 = (edgeIndices[i + 2] != 12) ? InterpolateVertex(corners, MarchingCubesLookupTables2.EdgeIndexTable[edgeIndices[i + 2]]) : centreVertex;

            //triangles.Add(new DataStructures.Triangle(v1, v2, v3));
            triangles.Add(new DataStructures.Triangle(v3, v2, v1));

            //Debug.Log($"v3:({v3.x},{v3.y},{v3.z})\nv2:({v2.x},{v2.y},{v2.z})\nv1:({v1.x},{v1.y},{v1.z})");
        }           

        return triangles;
    }

    // Test a face
    // if face>0 return true if the face contains a part of the surface
    private bool TestFace(Vector3[] corners, int face)
    {
        int A, B, C, D;

        switch (face)
        {
            case -1: case 1: A = GetCellValue(corners[0]); B = GetCellValue(corners[4]); C = GetCellValue(corners[5]); D = GetCellValue(corners[1]); break;
            case -2: case 2: A = GetCellValue(corners[1]); B = GetCellValue(corners[5]); C = GetCellValue(corners[6]); D = GetCellValue(corners[2]); break;
            case -3: case 3: A = GetCellValue(corners[2]); B = GetCellValue(corners[6]); C = GetCellValue(corners[7]); D = GetCellValue(corners[3]); break;
            case -4: case 4: A = GetCellValue(corners[3]); B = GetCellValue(corners[7]); C = GetCellValue(corners[4]); D = GetCellValue(corners[0]); break;
            case -5: case 5: A = GetCellValue(corners[0]); B = GetCellValue(corners[3]); C = GetCellValue(corners[2]); D = GetCellValue(corners[1]); break;
            case -6: case 6: A = GetCellValue(corners[4]); B = GetCellValue(corners[7]); C = GetCellValue(corners[6]); D = GetCellValue(corners[5]); break;
            default: Debug.Log($"Invalid face code {face}"); A = B = C = D = 0; break;
        };

        if (Mathf.Abs(A * C - B * D) < float.Epsilon)
        {
            return face >= 0;
        }
        
        return face * A * (A * C - B * D) >= 0;  // face and A invert signs
    }

    // Test the interior of a cube
    // if s == 7, return true  if the interior is empty
    // if s ==-7, return false if the interior is empty
    private bool TestInterior(Vector3[] corners, int mainCase, int config, int subConfig, int s)
    {
        int t, At = 0, Bt = 0, Ct = 0, Dt = 0, a, b;
        int test = 0;
        int edge = 1;
        int[] cornerValues = new int[] {
            GetCellValue(corners[0]),
            GetCellValue(corners[1]),
            GetCellValue(corners[2]),
            GetCellValue(corners[3]),
            GetCellValue(corners[4]),
            GetCellValue(corners[5]),
            GetCellValue(corners[6]),
            GetCellValue(corners[7])
        };

        switch (mainCase)
        {
            case 4:
            case 10:
                a = (cornerValues[4] - cornerValues[0]) * (cornerValues[6] - cornerValues[2]) - (cornerValues[7] - cornerValues[3]) * (cornerValues[5] - cornerValues[1]);
                b = cornerValues[2] * (cornerValues[4] - cornerValues[0]) + cornerValues[0] * (cornerValues[6] - cornerValues[2])
                         - cornerValues[1] * (cornerValues[7] - cornerValues[3]) - cornerValues[3] * (cornerValues[5] - cornerValues[1]);                
                if (a == 0) a = 1;
                t = -b / (2 * a);
                if (t < 0 || t > 1) return s > 0;

                At = cornerValues[0] + (cornerValues[4] - cornerValues[0]) * t;
                Bt = cornerValues[3] + (cornerValues[7] - cornerValues[3]) * t;
                Ct = cornerValues[2] + (cornerValues[6] - cornerValues[2]) * t;
                Dt = cornerValues[1] + (cornerValues[5] - cornerValues[1]) * t;
                break;

            case 6:
            case 7:
            case 12:
            case 13:
                switch (mainCase)
                {
                    case 6: edge = MarchingCubesLookupTables2.test6[config][2]; break;
                    case 7: edge = MarchingCubesLookupTables2.test7[config][4]; break;
                    case 12: edge = MarchingCubesLookupTables2.test12[config][3]; break;
                    case 13: edge = MarchingCubesLookupTables2.tiling13_5_1[config][subConfig][0]; break;
                }
                switch (edge)
                {
                    case 0:

                        a = cornerValues[0] - cornerValues[1];
                        if (a == 0) a = 1;
                        t = cornerValues[0] / a;
                        At = 0;
                        Bt = cornerValues[3] + (cornerValues[2] - cornerValues[3]) * t;
                        Ct = cornerValues[7] + (cornerValues[6] - cornerValues[7]) * t;
                        Dt = cornerValues[4] + (cornerValues[5] - cornerValues[4]) * t;
                        break;
                    case 1:
                        a = cornerValues[1] - cornerValues[2];
                        if (a == 0) a = 1;
                        t = cornerValues[1] / a;
                        At = 0;
                        Bt = cornerValues[0] + (cornerValues[3] - cornerValues[0]) * t;
                        Ct = cornerValues[4] + (cornerValues[7] - cornerValues[4]) * t;
                        Dt = cornerValues[5] + (cornerValues[6] - cornerValues[5]) * t;
                        break;
                    case 2:
                        a = cornerValues[2] - cornerValues[3];
                        if (a == 0) a = 1;
                        t = cornerValues[2] / a;
                        At = 0;
                        Bt = cornerValues[1] + (cornerValues[0] - cornerValues[1]) * t;
                        Ct = cornerValues[5] + (cornerValues[4] - cornerValues[5]) * t;
                        Dt = cornerValues[6] + (cornerValues[7] - cornerValues[6]) * t;
                        break;
                    case 3:
                        a = cornerValues[3] - cornerValues[0];
                        if (a == 0) a = 1;
                        t = cornerValues[3] / a;
                        At = 0;
                        Bt = cornerValues[2] + (cornerValues[1] - cornerValues[2]) * t;
                        Ct = cornerValues[6] + (cornerValues[5] - cornerValues[6]) * t;
                        Dt = cornerValues[7] + (cornerValues[4] - cornerValues[7]) * t;
                        break;
                    case 4:
                        a = cornerValues[4] - cornerValues[5];
                        if (a == 0) a = 1;
                        t = cornerValues[4] / a;
                        At = 0;
                        Bt = cornerValues[7] + (cornerValues[6] - cornerValues[7]) * t;
                        Ct = cornerValues[3] + (cornerValues[2] - cornerValues[3]) * t;
                        Dt = cornerValues[0] + (cornerValues[1] - cornerValues[0]) * t;
                        break;
                    case 5:
                        a = cornerValues[5] - cornerValues[6];
                        if (a == 0) a = 1;
                        t = cornerValues[5] / a;
                        At = 0;
                        Bt = cornerValues[4] + (cornerValues[7] - cornerValues[4]) * t;
                        Ct = cornerValues[0] + (cornerValues[3] - cornerValues[0]) * t;
                        Dt = cornerValues[1] + (cornerValues[2] - cornerValues[1]) * t;
                        break;
                    case 6:
                        a = cornerValues[6] - cornerValues[7];
                        if (a == 0) a = 1;
                        t = cornerValues[6] / a;
                        At = 0;
                        Bt = cornerValues[5] + (cornerValues[4] - cornerValues[5]) * t;
                        Ct = cornerValues[1] + (cornerValues[0] - cornerValues[1]) * t;
                        Dt = cornerValues[2] + (cornerValues[3] - cornerValues[2]) * t;
                        break;
                    case 7:
                        a = cornerValues[7] - cornerValues[4];
                        if (a == 0) a = 1;
                        t = cornerValues[7] / a;
                        At = 0;
                        Bt = cornerValues[6] + (cornerValues[5] - cornerValues[6]) * t;
                        Ct = cornerValues[2] + (cornerValues[1] - cornerValues[2]) * t;
                        Dt = cornerValues[3] + (cornerValues[0] - cornerValues[3]) * t;
                        break;
                    case 8:
                        a = cornerValues[0] - cornerValues[4];
                        if (a == 0) a = 1;
                        t = cornerValues[0] / a;
                        At = 0;
                        Bt = cornerValues[3] + (cornerValues[7] - cornerValues[3]) * t;
                        Ct = cornerValues[2] + (cornerValues[6] - cornerValues[2]) * t;
                        Dt = cornerValues[1] + (cornerValues[5] - cornerValues[1]) * t;
                        break;
                    case 9:
                        a = cornerValues[1] - cornerValues[5];
                        if (a == 0) a = 1;
                        t = cornerValues[1] / a;
                        At = 0;
                        Bt = cornerValues[0] + (cornerValues[4] - cornerValues[0]) * t;
                        Ct = cornerValues[3] + (cornerValues[7] - cornerValues[3]) * t;
                        Dt = cornerValues[2] + (cornerValues[6] - cornerValues[2]) * t;
                        break;
                    case 10:
                        a = cornerValues[2] - cornerValues[6];
                        if (a == 0) a = 1;
                        t = cornerValues[2] / a;
                        At = 0;
                        Bt = cornerValues[1] + (cornerValues[5] - cornerValues[1]) * t;
                        Ct = cornerValues[0] + (cornerValues[4] - cornerValues[0]) * t;
                        Dt = cornerValues[3] + (cornerValues[7] - cornerValues[3]) * t;
                        break;
                    case 11:
                        a = cornerValues[3] - cornerValues[7];
                        if (a == 0) a = 1;
                        t = cornerValues[3] / a;
                        At = 0;
                        Bt = cornerValues[2] + (cornerValues[6] - cornerValues[2]) * t;
                        Ct = cornerValues[1] + (cornerValues[5] - cornerValues[1]) * t;
                        Dt = cornerValues[0] + (cornerValues[4] - cornerValues[0]) * t;
                        break;
                    default: Debug.Log($"Invalid edge {edge}"); break;
                }
                break;

            default: Debug.Log($"Invalid ambiguous case {mainCase}"); break;
        }

        if (At >= 0) test++;
        if (Bt >= 0) test += 2;
        if (Ct >= 0) test += 4;
        if (Dt >= 0) test += 8;
        switch (test)
        {
            case 0: return s > 0;
            case 1: return s > 0;
            case 2: return s > 0;
            case 3: return s > 0;
            case 4: return s > 0;
            case 5: if (At * Ct - Bt * Dt < float.Epsilon) return s > 0; break;
            case 6: return s > 0;
            case 7: return s < 0;
            case 8: return s > 0;
            case 9: return s > 0;
            case 10: if (At * Ct - Bt * Dt >= float.Epsilon) return s > 0; break;
            case 11: return s < 0;
            case 12: return s > 0;
            case 13: return s < 0;
            case 14: return s < 0;
            case 15: return s < 0;
        }

        return s < 0;
    }

    private Vector3 InterpolateVertex(Vector3[] corners, int[] edgeIndices)
    {
        Vector3 v1 = corners[edgeIndices[0]];
        Vector3 v2 = corners[edgeIndices[1]];

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
        if (Mathf.Abs(value1 - value2) > float.Epsilon)
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

    private List<Triangle> GenerateSpeleothems()
    {
        List<Triangle> speleothemTris = new List<Triangle>();      
        float maxY = ((float)gridGenerator.height - 1.0f) / 2;
        float minY = maxY;
        float maxX = ((float)gridGenerator.width - 1.0f) / 2;
        float minX = maxX;
        float maxZ = ((float)gridGenerator.depth - 1.0f) / 2;
        float minZ = maxZ;
        List<Vector3> usedPositions = new List<Vector3>();
        float randomSizeModifier;
        float randomSizeModifier1;

        // Find bounds of mesh
        foreach (Triangle tri in triangles)
        {
            if (tri.v1.y > maxY) maxY = tri.v1.y;
            if (tri.v2.y > maxY) maxY = tri.v2.y;
            if (tri.v3.y > maxY) maxY = tri.v3.y;

            if (tri.v1.y < minY) minY = tri.v1.y;
            if (tri.v2.y < minY) minY = tri.v2.y;
            if (tri.v3.y < minY) minY = tri.v3.y;

            if (tri.v1.x > maxX) maxX = tri.v1.x;
            if (tri.v2.x > maxX) maxX = tri.v2.x;
            if (tri.v3.x > maxX) maxX = tri.v3.x;

            if (tri.v1.x < minX) minX = tri.v1.x;
            if (tri.v2.x < minX) minX = tri.v2.x;
            if (tri.v3.x < minX) minX = tri.v3.x;

            if (tri.v1.z > maxZ) maxZ = tri.v1.z;
            if (tri.v2.z > maxZ) maxZ = tri.v2.z;
            if (tri.v3.z > maxZ) maxZ = tri.v3.z;

            if (tri.v1.z < minZ) minZ = tri.v1.z;
            if (tri.v2.z < minZ) minZ = tri.v2.z;
            if (tri.v3.z < minZ) minZ = tri.v3.z;
        }

        // Allow  a small distance from the highest and lowest points for spawn locations
        maxY = Mathf.Floor(maxY * 10f) / 10f * 0.95f;
        minY = Mathf.Ceil(minY * 10f) / 10f * 1.05f;

        foreach (Triangle tri in triangles)
        {
            Vector3 pos = Vector3.zero;

            if (tri.v1.y <= minY || tri.v1.y >= maxY) pos = tri.v1;
            if (tri.v2.y <= minY || tri.v2.y >= maxY) pos = tri.v2;
            if (tri.v3.y <= minY || tri.v3.y >= maxY) pos = tri.v3;

            if (pos != Vector3.zero && pos.x >= minX && pos.x <= maxX && pos.z >= minZ && pos.z <= maxZ && !usedPositions.Contains(pos))
            {
                if (UnityEngine.Random.Range(0f, 100f) < speleothemPercent)
                {                    
                    randomSizeModifier = UnityEngine.Random.Range(0.1f, 1.5f);
                    randomSizeModifier1 = UnityEngine.Random.Range(1f, 1.5f);

                    float oppositeY;
                    bool isMite;
                    if (pos.y >= maxY) { oppositeY = minY; isMite = false; } else { oppositeY = maxY; isMite = true; }

                    speleothemTris.AddRange(speleothemGenerator.GenerateSpeleothem((float)gridGenerator.width * 0.05f * randomSizeModifier, (float)gridGenerator.height * 0.025f * randomSizeModifier, pos, isMite));
                    speleothemTris.AddRange(speleothemGenerator.GenerateSpeleothem((float)gridGenerator.width * 0.05f * randomSizeModifier * randomSizeModifier1, (float)gridGenerator.height * 0.025f * randomSizeModifier * randomSizeModifier1, new Vector3(pos.x, oppositeY, pos.z), !isMite));

                    usedPositions.Add(pos);
                    usedPositions.Add(new Vector3(pos.x, oppositeY, pos.z));                  
                }
            }
        }

        return speleothemTris;
    }

    public void CreateMesh()
    {               
        // Add triangles
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
            CalculateUVs();
            CalculateNormals();
        }

        Debug.Log($"MC tri count: {triangles.Count}");
    }

    private void CalculateNormals()
    {
        List<Vector3> faceNormals = CalculateFaceNormals();
        Vector3[] normals = new Vector3[mesh.vertices.Length];

        for (int i = 0; i < mesh.vertices.Length; i++)
        {
            List<Vector3> neighbouringFaceNormals = new List<Vector3>();
            Vector3 currentVertex = mesh.vertices[i];

            for (int j = 0; j < triangles.Count; j++)
            {
                if (triangles[j].v1 == currentVertex || triangles[j].v2 == currentVertex || triangles[j].v3 == currentVertex)
                {
                    neighbouringFaceNormals.Add(faceNormals[j]);
                }
            }

            Vector3 sum = Vector3.zero;
            foreach (Vector3 faceNormal in neighbouringFaceNormals)
            {
                sum += faceNormal;                
            }

            normals[i] = (sum / neighbouringFaceNormals.Count).normalized;
        }
        
        mesh.normals = normals;
    }

    private List<Vector3> CalculateFaceNormals()
    {
        List<Vector3> faceNormals = new List<Vector3>();
        for (int i = 0; i < triangles.Count; i++)
        {          
            faceNormals.Add(Vector3.Cross((triangles[i].v2 - triangles[i].v1), (triangles[i].v3 - triangles[i].v1)).normalized);
        }
        return faceNormals;
    }

    void CalculateUVs()
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Vector2[] uvs = new Vector2[vertices.Length];

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int index1 = triangles[i];
            int index2 = triangles[i + 1];
            int index3 = triangles[i + 2];

            Vector3 v1 = vertices[index1];
            Vector3 v2 = vertices[index2];
            Vector3 v3 = vertices[index3];

            // Calculate face normal
            Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;

            // Determine which face this triangle belongs to
            CalculateUVForCubeFace(v1, normal, ref uvs[index1]);
            CalculateUVForCubeFace(v2, normal, ref uvs[index2]);
            CalculateUVForCubeFace(v3, normal, ref uvs[index3]);
        }

        mesh.uv = uvs;
    }

    void CalculateUVForCubeFace(Vector3 vertex, Vector3 normal, ref Vector2 uv)
    {
        float textureScale = 1.0f / gridGenerator.width;

        if (Mathf.Abs(normal.x) > 0.5f)
        {
            // Side faces (X-axis aligned)
            uv.x = vertex.z * textureScale;
            uv.y = vertex.y * textureScale;
        }
        else if (Mathf.Abs(normal.y) > 0.5f)
        {
            // Top and bottom faces (Y-axis aligned)
            uv.x = vertex.x * textureScale;
            uv.y = vertex.z * textureScale;
        }
        else
        {
            // Front and back faces (Z-axis aligned)
            uv.x = vertex.x * textureScale;
            uv.y = vertex.y * textureScale;
        }
    }

    public void ClearMesh()
    {
        if (mesh != null) mesh.Clear();
    }

}
