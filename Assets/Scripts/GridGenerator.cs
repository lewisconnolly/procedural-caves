using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System.Linq;
using Unity.Jobs;
using System;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using System.Diagnostics;
using System.Threading;
using Unity.Burst;
using TMPro;
using UnityEngine.UI;
using Unity.VisualScripting;

public class GridGenerator : MonoBehaviour
{
    public int width;
    public int height;
    public int depth;
    
    public NativeParallelHashMap<Vector3, DataStructures.Cell> grid;
    Dictionary<int, GameObject> cubes;
    //bool isGenerated = false;

    public enum RulePreset {
        Brain3D,
        FourFourFive,
        Amoeba,
        Architechture,
        Builder1,
        Builder2,
        Clouds1,
        Clouds2,
        Construction,
        Coral,
        CrystalGrowth1,
        CrystalGrowth2,
        DiamondGrowth,
        ExpandingShell,
        MoreStructures,
        PulseWaves,
        Pyroclastic,
        Sample1,
        Shells,
        SinglePointReplication,
        SlowDecay1,
        SlowDecay2,
        SpikyGrowth,
        StableStructures,
        Symmetry,
        vonNeumannBuilder
    }
    public RulePreset rulePreset;
    public bool usePreset;

    [Range(0f, 100f)]
    public float randomFillPercent;
    public string seed;
    public bool useRandomSeed;
    public int numGenerations;
    public bool useMooreNeighbours;
    public bool drawGrid;
    public int numStates;
    List<int> nbrsForSurvival;
    List<int> nbrsForBirth;
    public string survivalRuleStr;
    public string birthRuleStr;    

    JobHandle handle;
    NativeParallelHashMap<Vector3, DataStructures.Cell> newGrid;
    NativeArray<Vector3> keyArray;
    NativeArray<int> nbrsForBirthNA;
    NativeArray<int> nbrsForSurvivalNA;
    NativeArray<int> numStatesNA;

    public GameObject statusIndicator;

    public TMP_InputField widthIF;
    public TMP_InputField heightIF;
    public TMP_InputField depthIF;
    public TMP_Dropdown rulePresetDD;
    public TMP_InputField numGenerationsIF;
    public Slider fillPercentSlider;    
    public TMP_InputField survivalIF;
    public TMP_InputField birthIF;
    public Toggle usePresetToggle;
    public Toggle useMooreToggle;
    public Toggle useRandomSeedToggle;
    public TMP_InputField seedIF;
    public TMP_InputField numStatesIF;
    
    void Start()
    {
    }

    private void Update()
    {
        //if (isGenerated)
        //{
        //    if (UnityEngine.Input.GetMouseButtonDown(0))
        //    {
        //        statusIndicator.SetActive(false);
        //        UpdateGridParallel();
        //        if (drawGrid) DrawGrid();
        //        statusIndicator.SetActive(true);
        //    }
        //}

        //Add listener for when the value of the Dropdown changes, to take action
        rulePresetDD.onValueChanged.AddListener(delegate {
            DropdownValueChanged(rulePresetDD);
        });
    }

    public void DropdownValueChanged(TMP_Dropdown dropdown)
    {
        rulePreset = (RulePreset)dropdown.value;

        SetUpRules();

        numStatesIF.text = numStates.ToString();
        survivalIF.text = survivalRuleStr;
        birthIF.text = birthRuleStr;
        useMooreToggle.isOn = useMooreNeighbours;
    }

    private void GetInput()
    {
        width = int.Parse(widthIF.text);
        height = int.Parse(heightIF.text);
        depth = int.Parse(depthIF.text);
        rulePreset = (RulePreset)rulePresetDD.value;
        numGenerations = int.Parse(numGenerationsIF.text);
        randomFillPercent = fillPercentSlider.value;
        survivalRuleStr = survivalIF.text;
        birthRuleStr = birthIF.text;
        usePreset = usePresetToggle.isOn;
        useMooreNeighbours = useMooreToggle.isOn;
        useRandomSeed = useRandomSeedToggle.isOn;
        seed = seedIF.text;
        numStates = int.Parse(numStatesIF.text);
    }    

    public void GenerateGrid()
    {
        GetInput();
        
        DisposeJobResources();
        
        grid = new NativeParallelHashMap<Vector3, DataStructures.Cell>(width * height * depth, Allocator.Persistent);
        cubes = new Dictionary<int, GameObject>();

        if (usePreset)
        {
            SetUpRules();
        }

        nbrsForSurvival = ParseRuleString(survivalRuleStr);
        nbrsForBirth = ParseRuleString(birthRuleStr);

        RandomFillGrid();
        SetUpdateGridJobPersistentData();
        if (drawGrid) DrawGrid();

        for (int i = 0; i < numGenerations; i++)
        {
            UpdateGridParallel();
            if (drawGrid) DrawGrid();
        }

        CreateWalls();
        SimplifyGrid();

        //isGenerated = true;
    }

    private void SetUpRules()
    {
        switch (rulePreset)
        {
            case RulePreset.Brain3D:
                survivalRuleStr = "27"; // Always die
                birthRuleStr = "4";
                numStates = 2;
                useMooreNeighbours = true;
                break;

            case RulePreset.FourFourFive:
                survivalRuleStr = "4";
                birthRuleStr = "4";
                numStates = 5;
                useMooreNeighbours = true;
                break;

            case RulePreset.Amoeba:
                survivalRuleStr = "9-26";
                birthRuleStr = "5-7,12-13,15";
                numStates = 5;
                useMooreNeighbours = true;
                break;

            case RulePreset.Architechture:
                survivalRuleStr = "4-6";
                birthRuleStr = "3";
                numStates = 2;
                useMooreNeighbours = true;
                break;

            case RulePreset.Builder1:
                survivalRuleStr = "2,6,9";
                birthRuleStr = "4,6,8-9";
                numStates = 10;
                useMooreNeighbours = true;
                break;

            case RulePreset.Builder2:
                survivalRuleStr = "5-7";
                birthRuleStr = "1";
                numStates = 2;
                useMooreNeighbours = true;
                break;

            case RulePreset.Clouds1:
                survivalRuleStr = "13-26";
                birthRuleStr = "13-14,17-19";
                numStates = 2;
                useMooreNeighbours = true;
                break;

            case RulePreset.Clouds2:
                survivalRuleStr = "12-26";
                birthRuleStr = "13-14";
                numStates = 2;
                useMooreNeighbours = true;
                break;

            case RulePreset.Construction:
                survivalRuleStr = "0-2,4,6-11,13-17,21-26";
                birthRuleStr = "9-10,16,23-24";
                numStates = 2;
                useMooreNeighbours = true;
                break;

            case RulePreset.Coral:
                survivalRuleStr = "5-8";
                birthRuleStr = "6-7,9,12";
                numStates = 4;
                useMooreNeighbours = true;
                break;

            case RulePreset.CrystalGrowth1:
                survivalRuleStr = "0-6";
                birthRuleStr = "1,3";
                numStates = 2;
                useMooreNeighbours = false;
                break;

            case RulePreset.CrystalGrowth2:
                survivalRuleStr = "1-2";
                birthRuleStr = "1,3";
                numStates = 5;
                useMooreNeighbours = false;
                break;

            case RulePreset.DiamondGrowth:
                survivalRuleStr = "5-6";
                birthRuleStr = "1-3";
                numStates = 7;
                useMooreNeighbours = false;
                break;

            case RulePreset.ExpandingShell:
                survivalRuleStr = "6,7-9,11,13,15-16,18";
                birthRuleStr = "6-10,13-14,16,18-19,22-25";
                numStates = 5;
                useMooreNeighbours = true;
                break;

            case RulePreset.MoreStructures:
                survivalRuleStr = "7-26";
                birthRuleStr = "4";
                numStates = 4;
                useMooreNeighbours = true;
                break;

            case RulePreset.PulseWaves:
                survivalRuleStr = "3";
                birthRuleStr = "1-3";
                numStates = 10;
                useMooreNeighbours = true;
                break;

            case RulePreset.Pyroclastic:
                survivalRuleStr = "4-7";
                birthRuleStr = "6-8";
                numStates = 10;
                useMooreNeighbours = true;
                break;

            case RulePreset.Sample1:
                survivalRuleStr = "10-26";
                birthRuleStr = "5,8-26";
                numStates = 4;
                useMooreNeighbours = true;
                break;

            case RulePreset.Shells:
                survivalRuleStr = "3,5,7,9,11,15,17,19,21,23-24,26";
                birthRuleStr = "3,6,8-9,11,14-17,19,24";
                numStates = 7;
                useMooreNeighbours = true;
                break;

            case RulePreset.SinglePointReplication:
                survivalRuleStr = "27"; // Always die
                birthRuleStr = "1";
                numStates = 2;
                useMooreNeighbours = true;
                break;

            case RulePreset.SlowDecay1:
                survivalRuleStr = "13-26";
                birthRuleStr = "10-26";
                numStates = 3;
                useMooreNeighbours = true;
                break;

            case RulePreset.SlowDecay2:
                survivalRuleStr = "1,4,8,11,13-26";
                birthRuleStr = "13-26";
                numStates = 5;
                useMooreNeighbours = true;
                break;

            case RulePreset.SpikyGrowth:
                survivalRuleStr = "0-3,7-9,11-13,18,21-22,24,26";
                birthRuleStr = "13,17,20-26";
                numStates = 4;
                useMooreNeighbours = true;
                break;

            case RulePreset.StableStructures:
                survivalRuleStr = "13-26";
                birthRuleStr = "14-19";
                numStates = 2;
                useMooreNeighbours = true;
                break;

            case RulePreset.Symmetry:
                survivalRuleStr = "27"; // Always die
                birthRuleStr = "2";
                numStates = 10;
                useMooreNeighbours = true;
                break;

            case RulePreset.vonNeumannBuilder:
                survivalRuleStr = "1-3";
                birthRuleStr = "1,4-5";
                numStates = 5;
                useMooreNeighbours = false;
                break;                        
        }
    }

    private List<int> ParseRuleString(string ruleStr)
    {
        List<int> numbers = new List<int>();

        // Deal with commas
        string[] commaSplitStrs = ruleStr.Split(",");

        for (int i = 0; i < commaSplitStrs.Length; i++)
        {
            // Deal with hyphens
            if (commaSplitStrs[i].Contains("-"))
            {
                string[] hyphenSplitStrs = commaSplitStrs[i].Split("-");
                int firstNum = int.Parse(hyphenSplitStrs[0]);
                int lastNum = int.Parse(hyphenSplitStrs[^1]);

                for (int j = firstNum; j <= lastNum; j++)
                {
                    numbers.Add(j);
                }
            }
            else
            {
                numbers.Add(int.Parse(commaSplitStrs[i]));
            }
        }

        return numbers;
    }

    void RandomFillGrid()
    {
        if (useRandomSeed)
        {
            seed = Time.time.ToString();
        }

        UnityEngine.Random.InitState(seed.GetHashCode());

        seedIF.text = seed;

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                for (int k = 0; k < depth; k++)
                {
                    Vector3 pos = new Vector3((float)i, (float)j, (float)k); // Define cell position to use as key
                    FixedList4096Bytes<float3> neighbours;

                    // Get list of neighbours for current cell
                    if (useMooreNeighbours)
                    {
                        neighbours = AddMooreNeighbours(pos);
                    }
                    else
                    {
                        neighbours = AddVonNeumannNeighbours(pos);
                    }

                    // Fill randomFillPercent% of the grid randomly with alive cells               
                    int state = (UnityEngine.Random.Range(0f, 100f) < randomFillPercent) ? numStates - 1 : 0;
                    //int state = (UnityEngine.Random.Range(0f, 100f) < randomFillPercent) ? UnityEngine.Random.Range(1, numStates) : 0;

                    int cubeId = int.Parse($"{i}{j}{k}");

                    // Create cube at cell position
                    if (drawGrid)
                    {
                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.transform.position = pos;
                        cube.GetComponent<MeshRenderer>().material.color = new Color(pos.x / 10f, pos.y / 10f, pos.z / 10f);
                        cube.GetComponent<MeshRenderer>().enabled = false;
                        cubes.Add(cube.GetInstanceID(), cube);

                        cubeId = cube.GetInstanceID();
                    }                    

                    grid.Add(pos, new DataStructures.Cell(state, neighbours, cubeId)); // Add cell to 3D grid using its position as a key
                }
            }
        }
    }

    [BurstCompile]
    struct UpdateGridJob : IJobParallelFor
    {
        [ReadOnly]        
        public NativeParallelHashMap<Vector3, DataStructures.Cell> currentGrid;
        [WriteOnly]
        public NativeParallelHashMap<Vector3, DataStructures.Cell>.ParallelWriter newGrid;
        [ReadOnly]
        public NativeArray<Vector3> keyArray;
        [ReadOnly]
        public NativeArray<int> nbrsForBirth;
        [ReadOnly]
        public NativeArray<int> nbrsForSurvival;
        [ReadOnly]
        public NativeArray<int> numStates;

        public void Execute(int index)
        {
            // Get key for grid hashmap
            Vector3 pos = keyArray[index];
            // Get value at key
            DataStructures.Cell cell = currentGrid[pos];
            // Create template cell
            DataStructures.Cell templateCell = new DataStructures.Cell();

            templateCell.state = cell.state;
            templateCell.neighbours = cell.neighbours;
            templateCell.cubeInstanceID = cell.cubeInstanceID;            

            if (cell.state == 0) // If cell is dead
            {
                int alive = CountAliveNeighbours(cell.neighbours);

                // If number of alive neighbours satisfies rule for birth
                if (nbrsForBirth.Contains(alive))
                {
                    templateCell.state = numStates[0] - 1;

                    // Birth cell (create new cell from current cell with max state and add to t+1 grid)
                    newGrid.TryAdd(pos, templateCell);
                }
                else
                {
                    // Cell stays the same
                    newGrid.TryAdd(pos, cell);
                }
            }
            else if (cell.state != numStates[0] - 1) // If cell is dying
            {
                templateCell.state = cell.state - 1;

                // Continue decreasing state until dead
                newGrid.TryAdd(pos, templateCell);

            }
            else // Cell is at max state
            {
                int alive = CountAliveNeighbours(cell.neighbours);

                // If number of alive neighbours does not satisfy rule for survivial
                if (!nbrsForSurvival.Contains(alive))
                {
                    templateCell.state = cell.state - 1;

                    // Start dying (create new cell from current cell with state - 1)
                    newGrid.TryAdd(pos, templateCell);
                }
                else
                {
                    // Cell stays the same
                    newGrid.TryAdd(pos, cell);
                }
            }
        }

        int CountAliveNeighbours(FixedList4096Bytes<float3> neighbours)
        {
            // Count number of alive neighbours
            int alive = 0;

            foreach (float3 nbr in neighbours)
            {
                if (currentGrid.TryGetValue((Vector3)nbr, out DataStructures.Cell neighborCell) && neighborCell.state != 0) alive++;
            }

            return alive;
        }
    }

    void SetUpdateGridJobPersistentData()
    {
        // Create UpdateGridJob data that never changes
        newGrid = new NativeParallelHashMap<Vector3, DataStructures.Cell>(grid.Count(), Allocator.Persistent);
        keyArray = grid.GetKeyArray(Allocator.Persistent);
        nbrsForBirthNA = new NativeArray<int>(nbrsForBirth.ToArray(), Allocator.Persistent);
        nbrsForSurvivalNA = new NativeArray<int>(nbrsForSurvival.ToArray(), Allocator.Persistent);
        numStatesNA = new NativeArray<int>(1, Allocator.Persistent);
        numStatesNA[0] = numStates;
    }

    void UpdateGridParallel() // Create grid for next timestep
    {
        // Create job
        UpdateGridJob job = new UpdateGridJob();

        // Set data sources for job        
        newGrid.Clear();
        job.currentGrid = grid;
        job.newGrid = newGrid.AsParallelWriter();
        job.keyArray = keyArray;
        job.nbrsForBirth = nbrsForBirthNA;
        job.nbrsForSurvival = nbrsForSurvivalNA;
        job.numStates = numStatesNA;

        // Schedule the job with one Execute per index in the grid and 1 item per processing batch
        handle = job.Schedule(grid.Count(), 2);

        // Wait for the job to complete
        handle.Complete();       

        // Replace current grid with t+1 grid
        NativeKeyValueArrays<Vector3, DataStructures.Cell> keyValuePairs = newGrid.GetKeyValueArrays(Allocator.Temp);
        
        grid.Clear();

        for (int i = 0; i < keyValuePairs.Length; i++)
        {
            grid.Add(keyValuePairs.Keys[i], keyValuePairs.Values[i]);
        }

        keyValuePairs.Dispose();
    }

    void OnApplicationQuit()
    {
        DisposeJobResources();
    }

    bool WaitForCondition(Func<bool> condition, TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed > timeout)
            {
                return false; // Timeout occurred
            }

            Thread.Sleep(100); // Wait for 100ms before checking again
        }

        return true; // Condition met within timeout period
    }

    void DisposeJobResources()
    {
        bool jobCompleted = WaitForCondition(() => handle.IsCompleted == true, TimeSpan.FromSeconds(10));

        if (jobCompleted)
        {
            try
            {
                newGrid.Dispose(handle);
                grid.Dispose(handle);
                nbrsForBirthNA.Dispose(handle);
                nbrsForSurvivalNA.Dispose(handle);
                numStatesNA.Dispose(handle);
                keyArray.Dispose(handle);
            }
            catch (ObjectDisposedException ex)
            {
                UnityEngine.Debug.LogError($"Object already disposed: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Catch-all for unexpected exceptions
                UnityEngine.Debug.LogError($"Unexpected error: {ex.Message}");
            }
        }
    }

    FixedList4096Bytes<float3> AddMooreNeighbours(Vector3 cellPos)
    {
        FixedList4096Bytes<float3> neighbours = new FixedList4096Bytes<float3>();
        float[] d = { -1.0f, 0.0f, 1.0f }; // Check neighbours on level below, same, and above (includes diagonals)

        foreach (float dx in d)
        {
            foreach (float dy in d)
            {
                foreach (float dz in d)
                {
                    // New neighbour
                    float nx = cellPos.x + dx;
                    float ny = cellPos.y + dy;
                    float nz = cellPos.z + dz;

                    if ((new Vector3(dx, dy, dz) != Vector3.zero) && // Not self
                        (nx >= 0.0f && nx < (float)width) && // Neighbour not outside cube
                        (ny >= 0.0f && ny < (float)height) && // Neighbour not outside cube
                        (nz >= 0.0f && nz < (float)depth) // Neighbour not outside cube
                    )
                    {
                        neighbours.Add(new float3(nx, ny, nz)); // Add valid neighbour
                    }
                }
            }
        }

        return neighbours;
    }

    FixedList4096Bytes<float3> AddVonNeumannNeighbours(Vector3 cellPos)
    {
        FixedList4096Bytes<float3> neighbours = new FixedList4096Bytes<float3>();
        float[] dirs = { -1.0f, 1.0f };
        string[] axes = { "x", "y", "z" };

        foreach (float dir in dirs)
        {
            foreach (string axis in axes)
            {
                float newCoord;

                switch (axis)
                {
                    case "x":
                        newCoord = cellPos.x + dir;                        
                        if (newCoord >= 0.0f && newCoord < (float)width) // Check new coordinate in bounds
                        {
                            neighbours.Add(new float3(newCoord, cellPos.y, cellPos.z));
                        }
                        break;
                    case "y":
                        newCoord = cellPos.y + dir;
                        if (newCoord >= 0.0f && newCoord < (float)height) // Check new coordinate in bounds
                        {
                            neighbours.Add(new float3(cellPos.x, newCoord, cellPos.z));
                        }
                        break;
                    case "z":
                        newCoord = cellPos.z + dir;
                        if (newCoord >= 0.0f && newCoord < (float)depth) // Check new coordinate in bounds
                        {
                            neighbours.Add(new float3(cellPos.x, cellPos.y, newCoord));
                        }
                        break;
                    default:                        
                        break;
                }
            }
        }        

        return neighbours;
    }

    private void CreateWalls()
    {
        Dictionary<Vector3, DataStructures.Cell> tempGrid = new Dictionary<Vector3, DataStructures.Cell>(width * height * depth);        

        // Create walls
        DataStructures.Cell templateCell = new DataStructures.Cell();
        foreach (var cell in grid)
        {            
            Vector3 pos = cell.Key;
            templateCell.state = cell.Value.state;
            templateCell.neighbours = cell.Value.neighbours;
            templateCell.cubeInstanceID = cell.Value.cubeInstanceID;

            // Leave grid open on one side only (far x)
            if (pos.z == 0 || pos.z == (float)(depth - 1) || pos.y == 0 || pos.y == (float)(height - 1) || pos.x == 0)
            {
                templateCell.state = 0;                
            }

            tempGrid.Add(pos, templateCell);
        }

        // Replace current grid with temp grid        
        grid.Clear();

        foreach (KeyValuePair<Vector3, DataStructures.Cell> entry in tempGrid)
        {
            grid.Add(entry.Key, entry.Value);
        }
    }

    private void SimplifyGrid()
    {
        Dictionary<Vector3, DataStructures.Cell> tempGrid = new Dictionary<Vector3, DataStructures.Cell>(width * height * depth);
        Vector3 pos;
        int wallCount;
        DataStructures.Cell templateCell = new DataStructures.Cell();

        // Join cells to walls
        foreach (var cell in grid)
        {
            pos = cell.Key;
            templateCell.state = cell.Value.state;
            templateCell.neighbours = cell.Value.neighbours;
            templateCell.cubeInstanceID = cell.Value.cubeInstanceID;

            int threshold = 3;
            if (useMooreNeighbours) threshold = 13;

            wallCount = CountNeighbourWalls(cell.Value.neighbours);

            // Make walls where more than half of neighbours are walls
            if (wallCount > threshold)
            {
                templateCell.state = 0;
            }

            tempGrid.Add(pos, templateCell);
        }

        // Eliminate isolated cells
        List<Vector3> keys = new List<Vector3>(tempGrid.Keys);
        int isolatedCellsCount = 0;
        int iterations = 0;        
        while (iterations < 500)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                pos = keys[i];
                DataStructures.Cell cell = tempGrid[pos];                

                wallCount = CountNeighbourWalls(cell.neighbours, tempGrid);

                // Check for cells with no neighbouring walls and exclude from isosurface
                if (wallCount == 0)
                {
                    isolatedCellsCount++;

                    templateCell.neighbours = cell.neighbours;
                    templateCell.cubeInstanceID = cell.cubeInstanceID;
                    templateCell.state = numStates - 1;
                    
                    tempGrid[pos] = templateCell;
                }
            }

            if (isolatedCellsCount == 0) break;

            isolatedCellsCount = 0;
            iterations++;
        }

        // Replace current grid with temp grid 
        grid.Clear();

        foreach (KeyValuePair<Vector3, DataStructures.Cell> entry in tempGrid)
        {
            grid.Add(entry.Key, entry.Value);
        }        
    }

    private int CountNeighbourWalls(FixedList4096Bytes<float3> neighbours)
    {
        int wallCount = 0;

        for (int i = 0; i < neighbours.Length; i++)
        {
            Vector3 key = neighbours[i];
            if (grid[key].state == 0) wallCount++;
        }

        return wallCount;
    }

    private int CountNeighbourWalls(FixedList4096Bytes<float3> neighbours, Dictionary<Vector3, DataStructures.Cell> grid)
    {
        int wallCount = 0;

        for (int i = 0; i < neighbours.Length; i++)
        {
            Vector3 key = neighbours[i];
            if (grid[key].state == 0) wallCount++;
        }

        return wallCount;
    }

    private void DrawGrid()
    {
        // Draw grid
        if (grid.Count() > 0)
        {
            foreach (var cell in grid)
            {                
                // Show cube if cell is not dead (state 0)
                if (cell.Value.state != 0)
                {
                    cubes[cell.Value.cubeInstanceID].GetComponent<MeshRenderer>().enabled = true;
                }
                else
                {
                    cubes[cell.Value.cubeInstanceID].GetComponent<MeshRenderer>().enabled = false;
                }
            }
        }
    }

    public void SetWidth(string s)
    {
        width = int.Parse(s);
    }

    public void SetHeight(string s)
    {
        height = int.Parse(s);
    }

    public void SetDepth(string s)
    {
        depth = int.Parse(s);
    }
}