using TMPro;
using UnityEngine;
using static GridGenerator;

public class CaveGenerator : MonoBehaviour
{
    GridGenerator gridGenerator;
    MeshGenerator2 meshGenerator2;
    MeshGenerator3 meshGenerator3;

    public bool generateGrid;
    public bool generateMesh;

    public RotateCamera cameraObj;

    public enum MeshAlgorithm
    {
        MarchingCubes,
        DualContouring
    }
    public MeshAlgorithm meshAlgorithm;

    public GameObject generateButton;

    public TMP_Dropdown algoDD;

    // Start is called before the first frame update
    void Start()
    {
        
        gridGenerator = GetComponent<GridGenerator>();
        meshGenerator2 = GetComponent<MeshGenerator2>();
        meshGenerator3 = GetComponent<MeshGenerator3>();
        
        Generate();
    }

    // Update is called once per frame
    void Update()
    {        
    }

    private void GetInput()
    {
        meshAlgorithm = (MeshAlgorithm)algoDD.value;        
    }

    public void Generate()
    {
        GetInput();
        
        gridGenerator.statusIndicator.SetActive(false);  
        meshGenerator2.statusIndicator.SetActive(false);        
        meshGenerator3.statusIndicator.SetActive(false);

        cameraObj.ReorientCamera();

        System.Diagnostics.Stopwatch watch;
        long elapsedMs;

        if (generateGrid)
        {
            watch = System.Diagnostics.Stopwatch.StartNew();            
            gridGenerator.GenerateGrid();
            watch.Stop();
            elapsedMs = watch.ElapsedMilliseconds;
            Debug.Log($"Grid generation time:{elapsedMs}");
            gridGenerator.statusIndicator.SetActive(true);
        }

        if (generateMesh)
        {
            switch (meshAlgorithm)
            {
                case MeshAlgorithm.MarchingCubes:
                    meshGenerator2.ClearMesh();
                    watch = System.Diagnostics.Stopwatch.StartNew();
                    meshGenerator2.GenerateMesh();
                    meshGenerator2.CreateMesh();
                    watch.Stop();
                    elapsedMs = watch.ElapsedMilliseconds;
                    Debug.Log($"MC time:{elapsedMs}");
                    meshGenerator2.statusIndicator.SetActive(true);
                    break;

                case MeshAlgorithm.DualContouring:
                    meshGenerator3.ClearMesh();
                    watch = System.Diagnostics.Stopwatch.StartNew();
                    meshGenerator3.GenerateMesh();
                    meshGenerator3.CreateMesh();
                    watch.Stop();
                    elapsedMs = watch.ElapsedMilliseconds;
                    Debug.Log($"DC time:{elapsedMs}");
                    meshGenerator3.statusIndicator.SetActive(true);
                    break;
            }
        }
    }
}
