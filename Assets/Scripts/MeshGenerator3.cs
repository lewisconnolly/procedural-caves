using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static DataStructures;
using static UnityEngine.Rendering.DebugUI;

public class MeshGenerator3 : MonoBehaviour
{
    GridGenerator gridGenerator;
    SpeleothemGenerator speleothemGenerator;

    NativeParallelHashMap<Vector3, DataStructures.Cell> grid;
    Dictionary<Vector3, DataStructures.HermiteData> hermiteData;
    List<DataStructures.Triangle> triangles;

    List<Vector3> vertices;
    List<int> indices;
    List<Vector2> uvs;

    public int isoLevel = 1;

    [Range(0f, 25f)]
    public float speleothemPercent = 10f;
    public bool generateSpeleothems = true;

    Mesh mesh;
    MeshFilter meshFilter;

    public GameObject statusIndicator;

    public TMP_InputField dcIsoLevelIF;

    private static readonly Vector3[,] edgeTable = new Vector3[12, 2]
    {
        {Vector3.zero, Vector3.right},
        {Vector3.right, Vector3.right + Vector3.forward},
        {Vector3.right + Vector3.forward, Vector3.forward},
        {Vector3.forward, Vector3.zero},
        {Vector3.up, Vector3.up + Vector3.right},
        {Vector3.up + Vector3.right, Vector3.up + Vector3.right + Vector3.forward},
        {Vector3.up + Vector3.right + Vector3.forward, Vector3.up + Vector3.forward},
        {Vector3.up + Vector3.forward, Vector3.up},
        {Vector3.zero, Vector3.up},
        {Vector3.right, Vector3.up + Vector3.right},
        {Vector3.right + Vector3.forward, Vector3.up + Vector3.right + Vector3.forward},
        {Vector3.forward, Vector3.up + Vector3.forward}
    };

    private void GetInput()
    {
        isoLevel = int.Parse(dcIsoLevelIF.text);
    }

    public void GenerateMesh()
    {
        GetInput();
        
        gridGenerator = GetComponent<GridGenerator>();
        meshFilter = GetComponent<MeshFilter>();
        speleothemGenerator = GetComponent<SpeleothemGenerator>();
        mesh = new Mesh();
        meshFilter.mesh = mesh;
        grid = gridGenerator.grid;
        hermiteData = new Dictionary<Vector3, DataStructures.HermiteData>();
        vertices = new List<Vector3>();
        indices = new List<int>();
        uvs = new List<Vector2>();
        triangles = new List<DataStructures.Triangle>();

        // Iterate through the grid
        foreach (var point in grid)
        {
            if (!(point.Key.x == (float)(gridGenerator.width - 1) ||
                point.Key.y == (float)(gridGenerator.height - 1) ||
                point.Key.z == (float)(gridGenerator.depth - 1)))
            {
                Vector3 cellCorner = point.Key;

                CalculateHermiteData(cellCorner);

                RunQefOnCell(cellCorner);
            }
        }

        GenerateTriangles();
        CalculateUVs();
        if (generateSpeleothems) GenerateSpeleothems();
    }

    void CalculateHermiteData(Vector3 cellCorner)
    {
        DataStructures.HermiteData data = new DataStructures.HermiteData();
        data.intersections = new List<Vector3>();
        data.normals = new List<Vector3>();
        data.vertex = new Vector3();

        // Check each edge of the current cell
        for (int edge = 0; edge < 12; edge++)
        {
            Vector3 start = cellCorner + edgeTable[edge, 0];
            Vector3 end = cellCorner + edgeTable[edge, 1];

            float startValue = EvaluatePoint(start);
            float endValue = EvaluatePoint(end);

            if (startValue < 0 != endValue < 0)
            {
                // Surface intersection found
                // float t = startValue / (startValue - endValue);
                //Vector3 intersection = Vector3.Lerp(start, end, t);
                Vector3 intersection = InterpolateVertex(start, end, startValue, endValue);
                Vector3 normal = EvaluatePointNormal(intersection);
                               
                data.intersections.Add(intersection);
                data.normals.Add(normal);
            }
        }

        hermiteData[cellCorner] = data;
    }

    private Vector3 InterpolateVertex(Vector3 v1, Vector3 v2, float v1Value, float v2Value)
    {
        if (Vector3LessThan(v2, v1))
        {
            Vector3 tempVec;
            float tempVal;
            tempVec = v1;
            v1 = v2;
            v2 = tempVec;
            tempVal = v1Value;
            v1Value = v2Value;
            v2Value = tempVal;
        }

        Vector3 lerpVector;
        if (Mathf.Abs(v1Value - v2Value) > float.Epsilon)
        {
            lerpVector = v1 + (v2 - v1) / (v2Value - v1Value) * (0.5f - v1Value);
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

    void RunQefOnCell(Vector3 cellCorner)
    {
        if(hermiteData[cellCorner].intersections.Count > 0)
        {
            // Solve QEF
            Vector3 vertex = GetVertexFromQef(hermiteData[cellCorner].intersections, hermiteData[cellCorner].normals);

            // Add vertex to mesh
            DataStructures.HermiteData hd = hermiteData[cellCorner];
            hd.vertex = vertex;
            hermiteData[cellCorner] = hd;
        }
    }

    Vector3 GetVertexFromQef(List<Vector3> points, List<Vector3> normals)
    {        
        Vector4 pointAccum = Vector4.zero;
        Matrix4x4 ATA = Matrix4x4.zero;
        Vector3 ATb = Vector3.zero;

        // Calculate the average of all intersection points
        Vector3 average = Vector3.zero;
        var e = points.GetEnumerator();
        while (e.MoveNext())
        {
            average += e.Current;
        }
        average /= points.Count;

        // Add extra normals that add extra error the further we go
        // from the cell, this encourages the final result to be
        // inside the cell
        // These normals are shorter than the input normals
        // as that makes the bias weaker,  we want them to only
        // really be important when the input is ambiguous
        // - https://www.boristhebrave.com/2018/04/15/dual-contouring-tutorial/
        normals.Add(new Vector3(0.01f, 0, 0));
        points.Add(average);
        normals.Add(new Vector3(0, 0.01f, 0));
        points.Add(average);
        normals.Add(new Vector3(0, 0, 0.01f));
        points.Add(average);

        for (int i = 0; i < points.Count; ++i)
        {
            QuadraticErrorFunction.QefAdd(normals[i], points[i], ref ATA, ref ATb, ref pointAccum);
        }

        //Vector3 com = new Vector3(pointAccum.x, pointAccum.y, pointAccum.z) / pointAccum.w;
        //float error = QuadraticErrorFunction.QefSolve(ATA, ATb, pointAccum, out x);
        QuadraticErrorFunction.QefSolve(ATA, ATb, pointAccum, out Vector3 x);

        return x;
    }

    void GenerateTriangles()
    {
        int index = 0;

        // For each cell edge, emit a face between the center of the adjacent cells if it is a sign changing edge
        foreach (var point in grid)
        {
            if (!(point.Key.x == (float)(gridGenerator.width - 1) ||
                point.Key.y == (float)(gridGenerator.height - 1) ||
                point.Key.z == (float)(gridGenerator.depth - 1)))
            {
                Vector3 cellCorner = point.Key;

                if (cellCorner.x > 0f && cellCorner.y > 0f)
                {
                    bool edgeStartTest = EvaluatePoint(cellCorner) < 0;
                    bool edgeEndTest = EvaluatePoint(cellCorner + Vector3.forward) < 0;

                    if (edgeStartTest != edgeEndTest)
                    {
                        Vector3 v1 = hermiteData[new Vector3(cellCorner.x - 1.0f, cellCorner.y - 1.0f, cellCorner.z)].vertex;
                        Vector3 v2 = hermiteData[new Vector3(cellCorner.x, cellCorner.y - 1.0f, cellCorner.z)].vertex;
                        Vector3 v3 = hermiteData[new Vector3(cellCorner.x, cellCorner.y, cellCorner.z)].vertex;
                        Vector3 v4 = hermiteData[new Vector3(cellCorner.x - 1.0f, cellCorner.y, cellCorner.z)].vertex;

                        vertices.Add(v1);
                        vertices.Add(v2);
                        vertices.Add(v3);
                        vertices.Add(v4);

                        if (!edgeEndTest)
                        {
                            indices.Add(index);
                            indices.Add(index + 1);
                            indices.Add(index + 2);
                            indices.Add(index);
                            indices.Add(index + 2);
                            indices.Add(index + 3);

                            triangles.Add(new DataStructures.Triangle(v1, v2, v3));
                            triangles.Add(new DataStructures.Triangle(v1, v3, v4));
                        }
                        else // Flip drawing order (back-facing triangles)
                        {
                            indices.Add(index + 3);
                            indices.Add(index + 2);
                            indices.Add(index);
                            indices.Add(index + 2);
                            indices.Add(index + 1);
                            indices.Add(index);

                            triangles.Add(new DataStructures.Triangle(v4, v3, v1));
                            triangles.Add(new DataStructures.Triangle(v3, v2, v1));
                        }

                        index += 4;
                    }
                }

                if (cellCorner.x > 0f && cellCorner.z > 0f)
                {
                    bool edgeStartTest = EvaluatePoint(cellCorner) < 0;
                    bool edgeEndTest = EvaluatePoint(cellCorner + Vector3.up) < 0;

                    if (edgeStartTest != edgeEndTest)
                    {
                        Vector3 v1 = hermiteData[new Vector3(cellCorner.x - 1.0f, cellCorner.y, cellCorner.z - 1.0f)].vertex;
                        Vector3 v2 = hermiteData[new Vector3(cellCorner.x, cellCorner.y, cellCorner.z - 1.0f)].vertex;
                        Vector3 v3 = hermiteData[new Vector3(cellCorner.x, cellCorner.y, cellCorner.z)].vertex;
                        Vector3 v4 = hermiteData[new Vector3(cellCorner.x - 1.0f, cellCorner.y, cellCorner.z)].vertex;

                        vertices.Add(v1);
                        vertices.Add(v2);
                        vertices.Add(v3);
                        vertices.Add(v4);

                        if (!edgeStartTest)
                        {
                            indices.Add(index);
                            indices.Add(index + 1);
                            indices.Add(index + 2);
                            indices.Add(index);
                            indices.Add(index + 2);
                            indices.Add(index + 3);

                            triangles.Add(new DataStructures.Triangle(v1, v2, v3));
                            triangles.Add(new DataStructures.Triangle(v1, v3, v4));
                        }
                        else // Flip drawing order (back-facing triangles)
                        {
                            indices.Add(index + 3);
                            indices.Add(index + 2);
                            indices.Add(index);
                            indices.Add(index + 2);
                            indices.Add(index + 1);
                            indices.Add(index);

                            triangles.Add(new DataStructures.Triangle(v4, v3, v1));
                            triangles.Add(new DataStructures.Triangle(v3, v2, v1));
                        }

                        index += 4;
                    }
                }

                if (cellCorner.y > 0f && cellCorner.z > 0f)
                {
                    bool edgeStartTest = EvaluatePoint(cellCorner) < 0;
                    bool edgeEndTest = EvaluatePoint(cellCorner + Vector3.right) < 0;

                    if (edgeStartTest != edgeEndTest)
                    {
                        Vector3 v1 = hermiteData[new Vector3(cellCorner.x, cellCorner.y - 1.0f, cellCorner.z - 1.0f)].vertex;
                        Vector3 v2 = hermiteData[new Vector3(cellCorner.x, cellCorner.y, cellCorner.z - 1.0f)].vertex;
                        Vector3 v3 = hermiteData[new Vector3(cellCorner.x, cellCorner.y, cellCorner.z)].vertex;
                        Vector3 v4 = hermiteData[new Vector3(cellCorner.x, cellCorner.y - 1.0f, cellCorner.z)].vertex;

                        vertices.Add(v1);
                        vertices.Add(v2);
                        vertices.Add(v3);
                        vertices.Add(v4);

                        if (!edgeEndTest)
                        {
                            indices.Add(index);
                            indices.Add(index + 1);
                            indices.Add(index + 2);
                            indices.Add(index);
                            indices.Add(index + 2);
                            indices.Add(index + 3);

                            triangles.Add(new DataStructures.Triangle(v1, v2, v3));
                            triangles.Add(new DataStructures.Triangle(v1, v3, v4));
                        }
                        else // Flip drawing order (back-facing triangles)
                        {
                            indices.Add(index + 3);
                            indices.Add(index + 2);
                            indices.Add(index);
                            indices.Add(index + 2);
                            indices.Add(index + 1);
                            indices.Add(index);

                            triangles.Add(new DataStructures.Triangle(v4, v3, v1));
                            triangles.Add(new DataStructures.Triangle(v3, v2, v1));
                        }

                        index += 4;
                    }
                }
            }
        }
    }

    private int EvaluatePoint(Vector3 pos)
    {
        if (grid.TryGetValue(pos, out DataStructures.Cell value))
        {
            return value.state - isoLevel;
        }

        return 0;        
    }   

    private float EvaluatePerturbedPoint(Vector3 point)
    {        
        float xDown;
        float yDown;
        float zDown;
        float xUp;
        float yUp;
        float zUp;

        // Get left/back/below start point of edge perturbed point lies on
        xDown = Mathf.Floor(point.x);
        yDown = Mathf.Floor(point.y);
        zDown = Mathf.Floor(point.z);

        // Get right/front/above start point of edge perturbed point lies on
        xUp = Mathf.Ceil(point.x);
        yUp = Mathf.Ceil(point.y);
        zUp = Mathf.Ceil(point.z);

        Vector3 vDown = new Vector3(xDown, yDown, zDown);
        Vector3 vUp = new Vector3(xUp, yUp, zUp);
        
        // Move edge points inside bounds of grid if out
        vDown.x = Mathf.Max(vDown.x, 0);
        vDown.y = Mathf.Max(vDown.y, 0);
        vDown.z = Mathf.Max(vDown.z, 0);
        vUp.x = Mathf.Max(vUp.x, 0);
        vUp.y = Mathf.Max(vUp.y, 0);
        vUp.z = Mathf.Max(vUp.z, 0);
               
        vDown.x = Mathf.Min(vDown.x, (float)gridGenerator.width - 1.0f);
        vDown.y = Mathf.Min(vDown.y, (float)gridGenerator.height - 1.0f);
        vDown.z = Mathf.Min(vDown.z, (float)gridGenerator.depth - 1.0f);
        vUp.x = Mathf.Min(vUp.x, (float)gridGenerator.width - 1.0f);
        vUp.y = Mathf.Min(vUp.y, (float)gridGenerator.height - 1.0f);
        vUp.z = Mathf.Min(vUp.z, (float)gridGenerator.depth - 1.0f);

        // Get edge state values
        int valDown = EvaluatePoint(vDown);
        int valUp = EvaluatePoint(vUp);

        // Interpolate between edge point values
        if (valUp < valDown)
        {            
            int tempVal;            
            tempVal = valDown;
            valDown = valUp;
            valUp = tempVal;
        }

        float valLerp;
        if (Mathf.Abs(valDown - valUp) > float.Epsilon)
        {
            valLerp = valDown + (valUp - valDown) / (valUp - valDown) * (0.5f - valDown);
        }
        else
        {
            valLerp = valDown;
        }

        return valLerp;
    }

    private Vector3 EvaluatePointNormal(Vector3 pos)
    {
        float dx = EvaluatePerturbedPoint(pos + Vector3.right) -
                   EvaluatePerturbedPoint(pos - Vector3.right);
        float dy = EvaluatePerturbedPoint(pos + Vector3.up) -
                   EvaluatePerturbedPoint(pos - Vector3.up);
        float dz = EvaluatePerturbedPoint(pos + Vector3.forward) -
                   EvaluatePerturbedPoint(pos - Vector3.forward);

        return new Vector3(dx, dy, dz).normalized;
    }

    private void GenerateSpeleothems()
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
        foreach (Vector3 v in vertices)
        {
            if (v.y > maxY) maxY = v.y;
            
            if (v.y < minY) minY = v.y;
            
            if (v.x > maxX) maxX = v.x;
            
            if (v.x < minX) minX = v.x;
            
            if (v.z > maxZ) maxZ = v.z;
            
            if (v.z < minZ) minZ = v.z;            
        }

        // Allow  a small distance from the highest and lowest points for spawn locations
        maxY = Mathf.Floor(maxY * 10f) / 10f * 0.95f;
        minY = Mathf.Ceil(minY * 10f) / 10f * 1.05f;

        // Generate speleothems on ceiling and floor of mesh
        foreach (Vector3 v in vertices)
        {
            Vector3 pos = Vector3.zero;

            if (v.y <= minY || v.y >= maxY) pos = v;

            // Ensure within bounds of mesh and not duplicating speleothems at same positions
            if (pos != Vector3.zero && pos.x >= minX && pos.x <= maxX && pos.z >= minZ && pos.z <= maxZ && !usedPositions.Contains(pos))
            {
                if (UnityEngine.Random.Range(0f, 100f) < speleothemPercent)
                {
                    randomSizeModifier = UnityEngine.Random.Range(0.1f, 1.5f);
                    randomSizeModifier1 = UnityEngine.Random.Range(1f, 1.5f);

                    float oppositeY;
                    bool isMite;
                    if (pos.y >= maxY) { oppositeY = minY; isMite = false; } else { oppositeY = maxY; isMite = true;  }

                    speleothemTris.AddRange(speleothemGenerator.GenerateSpeleothem((float)gridGenerator.width * 0.05f * randomSizeModifier, (float)gridGenerator.height * 0.025f * randomSizeModifier, pos, isMite));
                    speleothemTris.AddRange(speleothemGenerator.GenerateSpeleothem((float)gridGenerator.width * 0.05f * randomSizeModifier * randomSizeModifier1, (float)gridGenerator.height * 0.025f * randomSizeModifier * randomSizeModifier1, new Vector3(pos.x, oppositeY, pos.z), !isMite));

                    usedPositions.Add(pos);
                    usedPositions.Add(new Vector3(pos.x, oppositeY, pos.z));
                }
            }
        }

        // Add speleothems to vertex and index lists for drawing
        if (speleothemTris.Count > 0)
        {
            triangles.AddRange(speleothemTris);

            int index = vertices.Count;
            
            for (int i = 0; i < speleothemTris.Count; i++)
            {
                vertices.Add(speleothemTris[i].v1);
                vertices.Add(speleothemTris[i].v2);
                vertices.Add(speleothemTris[i].v3);

                indices.Add(index);
                indices.Add(index + 1);
                indices.Add(index + 2);

                index += 3;
            }

            Mesh speleothemMesh = speleothemGenerator.CreateMesh(speleothemTris);

            uvs.AddRange(speleothemMesh.uv);
        }
    }

    public void CreateMesh()
    {
        if (vertices.Count == 0 || indices.Count == 0) return;

        mesh.vertices = vertices.ToArray();
        mesh.triangles = indices.ToArray();
        mesh.uv = uvs.ToArray();
        CalculateNormals();

        Debug.Log($"DC tri count: {triangles.Count}");
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
        int[] triangles = indices.ToArray();
        Vector2[] meshUVs = new Vector2[vertices.Count];
        int quadCount = vertices.Count / 4;

        for (int i = 0; i < quadCount; i++)
        {
            int baseIndex = i * 6;

            HashSet<int> uniqueIndices = new HashSet<int>
            {
                triangles[baseIndex],
                triangles[baseIndex + 1],
                triangles[baseIndex + 2],
                triangles[baseIndex + 3],
                triangles[baseIndex + 4],
                triangles[baseIndex + 5]
            };

            Vector3[] quadVertices = uniqueIndices.Select(index => vertices[index]).ToArray();

            // Calculate face normal using the first three vertices
            Vector3 normal = Vector3.Cross(
                quadVertices[1] - quadVertices[0],
                quadVertices[2] - quadVertices[0]
            ).normalized;

            foreach (int index in uniqueIndices)
            {
                CalculateUVForCubeFace(vertices[index], normal, ref meshUVs[index]);
            }
        }

        uvs.AddRange(meshUVs);
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
