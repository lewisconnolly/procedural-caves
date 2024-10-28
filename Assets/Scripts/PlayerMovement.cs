using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement instance;    

    private void Awake()
    {
        instance = this;        
    }

    public float speed = 5f;
    public CharacterController controller;
    private Vector3 velocity;

    // Update is called once per frame
    void Update()
    {        
        if (!Cursor.visible)
        {

            // Get input for moving left, right, forward and backward
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");

            // Modify x and z positions of character controller
            Vector3 move = transform.right * x + Camera.main.transform.forward * z;
            // Multiply move vector by player speed variable and delta time for movement (to be framerate independent)
            controller.Move(move * speed * Time.deltaTime);
        }
    }
}
