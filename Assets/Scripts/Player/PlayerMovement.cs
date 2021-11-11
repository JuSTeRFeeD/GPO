using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{ 
    private Rigidbody2D rb;

    float horizontal;
    float vertical;

    public float runSpeed = 6.0f;

    void Start ()
    {
        rb = GetComponent<Rigidbody2D>(); 
    }

    void Update ()
    {
        horizontal = Input.GetAxisRaw("Horizontal");
        vertical = Input.GetAxisRaw("Vertical"); 
    }

    private void FixedUpdate()
    {  
        rb.velocity = new Vector2(horizontal * runSpeed, vertical * runSpeed);
    }
}
