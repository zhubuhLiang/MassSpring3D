using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    private Transform cam;
    private float mouseHorizontal;
    private float mouseVertical;
    private Vector3 velocity;
    private float horizontal;
    private float vertical;

    public float walkSpeed = 3f;

    void Start()
    {
        cam = GameObject.Find("Main Camera").transform;
    }


    private void FixedUpdate()
    {
        CalculateVelocity();
        transform.Rotate(Vector3.up * mouseHorizontal);
        cam.Rotate(Vector3.right * - mouseVertical);
        transform.Translate(velocity, Space.World);

    }

    private void Update()
    {
        GetPlayerInputs();
    }

    private void GetPlayerInputs()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");
        mouseHorizontal = Input.GetAxis("Mouse X");
        mouseVertical = Input.GetAxis("Mouse Y");
    }

    private void CalculateVelocity()
    {
        velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * walkSpeed;
    }
}


