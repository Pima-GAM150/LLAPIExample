using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour {

    public CharacterController controller;
    public float verticalVelocity;

	// Use this for initialization
	void Start () {
        controller = gameObject.GetComponent<CharacterController>();
	}
	
	// Update is called once per frame
	void Update () {
        Vector3 inputs = Vector3.zero;

        inputs.x = Input.GetAxis("Horizontal");

        if(controller.isGrounded)
        {
            verticalVelocity = -1f;

            if(Input.GetKey(KeyCode.Space))
            {
                verticalVelocity = 10f;
            }
        }
        else
        {
            verticalVelocity -= 10f  * Time.deltaTime;
        }

        inputs.y = verticalVelocity;

        controller.Move(inputs * Time.deltaTime);

	}
}
