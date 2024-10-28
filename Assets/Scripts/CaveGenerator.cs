using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GridGenerator;
using static UnityEngine.Rendering.DebugUI;

public class CaveGenerator : MonoBehaviour
{
    GridGenerator gridGenerator;
    MeshGenerator2 meshGenerator2;
    MeshGenerator3 meshGenerator3;

    public bool generateGrid;
    public bool generateMesh;

    //public RotateCamera cameraObj;

    public enum MeshAlgorithm
    {
        MarchingCubes,
        DualContouring
    }
    public MeshAlgorithm meshAlgorithm;

    public GameObject generateButton;

    public TMP_Dropdown algoDD;
    public Slider tessellationSlider;
    private int tessFactor;
    public Slider smoothingSlider;
    private float smoothingFactor;
    public Slider displacementSlider;
    private float displacementFactor;
    public Slider spelPercentSlider;
    private float spelPercent;
    private Renderer rend;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        gridGenerator = GetComponent<GridGenerator>();
        meshGenerator2 = GetComponent<MeshGenerator2>();
        meshGenerator3 = GetComponent<MeshGenerator3>();

        Generate();
    }

    // Update is called once per frame
    void Update()
    {                
        if (Cursor.lockState == CursorLockMode.Locked || Cursor.visible == false)
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            HandleDemoKeys();
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void GetInput()
    {
        meshAlgorithm = (MeshAlgorithm)algoDD.value;
        tessFactor = (int)tessellationSlider.value;
        smoothingFactor = smoothingSlider.value;
        displacementFactor = displacementSlider.value;
        spelPercent = spelPercentSlider.value;
    }

    private void HandleDemoKeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetDemoSettings("FourFourFive", "Marching Cubes", 0, 10, 10, 10, 90f, 1, 10f, 32);
            Generate();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetDemoSettings("Builder1", "Dual Contouring", 4, 20, 10, 10, 95f, 2, 2.5f, 32);
            Generate();
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetDemoSettings("Clouds2", "Marching Cubes", 0, 10, 10, 10, 93f, 10, 10f, 32);
            Generate();
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SetDemoSettings("DiamondGrowth", "Dual Contouring", 3, 10, 10, 15, 80f, 10, 5f, 24);
            Generate();
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            SetDemoSettings("ExpandingShell", "Marching Cubes", 1, 10, 10, 10, 51.4f, 50, 15f, 24);
            Generate();
        }

        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            SetDemoSettings("MoreStructures", "Dual Contouring", 1, 20, 10, 10, 75f, 200, 2.5f, 16);
            Generate();
        }

        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            SetDemoSettings("Shells", "Marching Cubes", 0, 15, 15, 20, 20f, 5, 5f, 16);
            Generate();
        }
    }

    private void SetDemoSettings(string rule,
                                string algo,
                                int isoLevel,
                                int width,
                                int height,
                                int depth,
                                float fill,
                                int gens,
                                float spel,
                                int tess)
    {
        // Set rule preset             
        TMP_Dropdown.OptionData item = gridGenerator.rulePresetDD.options.Where(opt => opt.text == rule).ToList().First();
        int itemIndex = gridGenerator.rulePresetDD.options.IndexOf(item);
        gridGenerator.rulePresetDD.SetValueWithoutNotify(itemIndex);
        gridGenerator.DropdownValueChanged(gridGenerator.rulePresetDD);
        // Set isosurface algo and params
        if (algo == "Marching Cubes")
        {
            meshAlgorithm = MeshAlgorithm.MarchingCubes;
            // Threshold value
            meshGenerator2.isoLevel = isoLevel;
            meshGenerator2.mcIsoLevelIF.text = isoLevel.ToString();
            // Speleothem percentage
            meshGenerator2.speleothemPercent = spel;
        }

        if (algo == "Dual Contouring")
        {
            meshAlgorithm = MeshAlgorithm.DualContouring;
            // Threshold value
            meshGenerator3.isoLevel = isoLevel;
            meshGenerator3.dcIsoLevelIF.text = isoLevel.ToString();
            // Speleothem percentage
            meshGenerator3.speleothemPercent = spel;
        }

        spelPercent = spel;
        spelPercentSlider.value = spelPercent;

        item = algoDD.options.Where(opt => opt.text == algo).ToList().First();
        itemIndex = algoDD.options.IndexOf(item);
        algoDD.SetValueWithoutNotify(itemIndex);        
        // Grid dimensions
        gridGenerator.width = width;
        gridGenerator.widthIF.text = width.ToString();
        gridGenerator.height = height;
        gridGenerator.heightIF.text = height.ToString();
        gridGenerator.depth = depth;
        gridGenerator.depthIF.text = depth.ToString();
        // Fill percentage
        gridGenerator.randomFillPercent = fill;
        gridGenerator.fillPercentSlider.value = fill;
        // Num generations
        gridGenerator.numGenerations = gens;
        gridGenerator.numGenerationsIF.text = gens.ToString();
        // Tessellation factor
        tessFactor = tess;
        tessellationSlider.value = tess;
    }

    public void Generate()
    {
        StartCoroutine(GenerateAsync());        
    }

    //public void Generate()
    IEnumerator GenerateAsync()
    {
        GetInput();

        rend = GetComponent<Renderer>();        
        rend.material.SetInt("_Tess", tessFactor);
        rend.material.SetFloat("_TessellationSmoothing", smoothingFactor);
        rend.material.SetFloat("_Weight", displacementFactor);

        //gridGenerator.statusIndicator.SetActive(false);
        //meshGenerator2.statusIndicator.SetActive(false);
        //meshGenerator3.statusIndicator.SetActive(false);

        //cameraObj.ReorientCamera();

        System.Diagnostics.Stopwatch watch;
        long elapsedMs;

        if (generateGrid)
        {
            watch = System.Diagnostics.Stopwatch.StartNew();            
            gridGenerator.GenerateGrid();
            watch.Stop();
            elapsedMs = watch.ElapsedMilliseconds;
            Debug.Log($"Grid generation time:{elapsedMs}");
            //gridGenerator.statusIndicator.SetActive(true);
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
                    //meshGenerator2.statusIndicator.SetActive(true);
                    break;

                case MeshAlgorithm.DualContouring:
                    meshGenerator3.ClearMesh();
                    watch = System.Diagnostics.Stopwatch.StartNew();
                    meshGenerator3.GenerateMesh();
                    meshGenerator3.CreateMesh();
                    watch.Stop();
                    elapsedMs = watch.ElapsedMilliseconds;
                    Debug.Log($"DC time:{elapsedMs}");
                    //meshGenerator3.statusIndicator.SetActive(true);
                    break;
            }
        }

        yield return null;
    }
}
