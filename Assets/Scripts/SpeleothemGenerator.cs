using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DataStructures;

public class SpeleothemGenerator : MonoBehaviour
{
    //public float width = 5f;
    //public float height = 2.5f;
    public AnimationCurve miteWidthCurve;
    public AnimationCurve titeWidthCurve;

    Mesh mesh;

    void Start()
    {                
        //List<Triangle> speleothemTris = GenerateSpeleothem(width, height, Vector3.zero, true);
        //CreateMesh(speleothemTris);
    }

    private void Update()
    {
        //if (UnityEngine.Input.GetMouseButton(0))
        //{
        //    List<Triangle> speleothemTris = GenerateSpeleothem(width, height, Vector3.zero, true);
        //    CreateMesh(speleothemTris);
        //}

        //if (UnityEngine.Input.GetMouseButton(1))
        //{
        //    List<Triangle> speleothemTris = GenerateSpeleothem(width, height, Vector3.zero, false);
        //    CreateMesh(speleothemTris);
        //}
    }

    public List<Triangle> GenerateSpeleothem(float maxWidth, float height, Vector3 center, bool stalagmite)
    {
        List<Triangle> triangles = new List<Triangle>();
        float numSegments = Random.Range(5f, 10f);
        float maxHeight = height * numSegments;
        Vector3 centerShift = Vector3.zero;
        float ratio;
        float currentWidth;
        AnimationCurve curve;
        if (stalagmite) { curve = miteWidthCurve; } else { curve = titeWidthCurve; }

        for (int i = 0; i < (int)numSegments; i++)
        {
            if (i > 0) {
                ratio = Mathf.Clamp((float)i / (numSegments - 1), 0f, 1f);                
                currentWidth = maxWidth - (curve.Evaluate(ratio) * maxWidth);
                //if (currentWidth < 0) currentWidth = maxWidth / numSegments;
            }
            else
            {
                currentWidth = maxWidth;
            }

            triangles.AddRange(Generate3DHexagon(currentWidth, maxHeight / numSegments, center + centerShift));

            if (stalagmite)
            {
                centerShift += new Vector3(0, maxHeight / numSegments, 0);
            }
            else
            {
                centerShift += new Vector3(0, -(maxHeight / numSegments), 0);
            }
        }

        return triangles;
    }

    private List<Triangle> Generate3DHexagon(float width, float height, Vector3 center)
    {
        List<Triangle> triangles = new List<Triangle>();
        float radius = width / 2f;
        float halfHeight = height / 2f;

        // Generate top and bottom vertices
        Vector3[] topVertices = new Vector3[6];
        Vector3[] bottomVertices = new Vector3[6];

        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI / 3f;
            float x = radius * Mathf.Cos(angle);
            float z = radius * Mathf.Sin(angle);

            topVertices[i] = center + new Vector3(x, 0, z);
            bottomVertices[i] = center + new Vector3(x, -height, z);
        }

        // Generate top face
        for (int i = 1; i < 5; i++)
        {
            //triangles.Add(new Triangle(topVertices[0], topVertices[i], topVertices[i + 1]));
            triangles.Add(new Triangle(topVertices[i+1], topVertices[i], topVertices[0]));
        }

        // Generate bottom face
        for (int i = 1; i < 5; i++)
        {
            //triangles.Add(new Triangle(bottomVertices[0], bottomVertices[i + 1], bottomVertices[i]));
            triangles.Add(new Triangle(bottomVertices[i], bottomVertices[i + 1], bottomVertices[0]));
        }

        // Generate side faces
        for (int i = 0; i < 6; i++)
        {
            int nextI = (i + 1) % 6;
            //triangles.Add(new Triangle(topVertices[i], bottomVertices[i], topVertices[nextI]));
            triangles.Add(new Triangle(topVertices[nextI], bottomVertices[i], topVertices[i]));
            //triangles.Add(new Triangle(bottomVertices[i], bottomVertices[nextI], topVertices[nextI]));
            triangles.Add(new Triangle(topVertices[nextI], bottomVertices[nextI], bottomVertices[i]));
        }

        return triangles;
    }

    public Mesh CreateMesh(List<Triangle> triangles)
    {
        mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        
        for (int i = 0; i < triangles.Count; i++)
        {
            vertices.Add(triangles[i].v1);
            vertices.Add(triangles[i].v2);
            vertices.Add(triangles[i].v3);

            indices.Add(i * 3);
            indices.Add(i * 3 + 1);
            indices.Add(i * 3 + 2);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices, 0);
        CalculateUVs();

        return mesh;
    }

    private void CalculateUVs()
    {
        Vector3[] vertices = mesh.vertices;
        int triangleCount = vertices.Length / 3;
        Vector2[] uvs = new Vector2[vertices.Length];

        for (int i = 0; i < triangleCount; i++)
        {
            int baseIndex = i * 3;

            // Bottom-left
            uvs[baseIndex] = new Vector2(0, 0);
            // Bottom-right
            uvs[baseIndex + 1] = new Vector2(1, 0);
            // Top
            uvs[baseIndex + 2] = new Vector2(0.5f, 1);
        }

        mesh.uv = uvs;
    }
}
