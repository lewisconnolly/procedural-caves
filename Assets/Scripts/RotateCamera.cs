using TMPro;
using UnityEngine;

public class RotateCamera : MonoBehaviour
{
    Vector3 cameraStartPos;
    
    public GameObject model;
    Vector3 modelStartPos;

    public GridGenerator grid;

    public bool isPaused;
    public TextMeshProUGUI pauseButtonText;

    // Start is called before the first frame update
    void Start()
    {
        modelStartPos = model.transform.position;
        cameraStartPos = transform.position;
        isPaused = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isPaused)
        {
            // Get centre of model
            Vector3 target = modelStartPos + Vector3.right * ((float)grid.width / 2f) + Vector3.up * ((float)grid.height/ 2f) + Vector3.forward * ((float)grid.depth / 2f);

            transform.LookAt(target);
            transform.Translate(Vector3.right * Time.deltaTime * 5f);
        }
    }

    public void ReorientCamera()
    {
        Vector3 newPos = cameraStartPos;

        newPos.x = (float)grid.width + 10f;
        newPos.y = (float)grid.height + 10f;
        newPos.z = (float)grid.depth + 10f;

        transform.position = newPos;
    }

    public void PauseUnpauseCamera()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            pauseButtonText.text = "Unpause";
        }
        else
        {
            pauseButtonText.text = "Pause";
        }        
    }
}
