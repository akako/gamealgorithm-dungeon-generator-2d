using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    Rigidbody2D rigidbodyCache;

    void Start()
    {
        rigidbodyCache = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        rigidbodyCache.AddForce(new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")) * 10f);
    }
}
