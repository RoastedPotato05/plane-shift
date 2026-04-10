using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CastleGate : MonoBehaviour
{
    private bool isDragging = false;
    private float originalY;
    private float dragSpeed = 5f;
    private float returnSpeed = 1f;

    void Start()
    {
        originalY = transform.position.y;
    }

    void OnMouseDown()
    {
        isDragging = true;
    }

    void OnMouseUp()
    {
        isDragging = false;
    }

    void Update()
    {
        if (isDragging)
        {
            float mouseY = Input.GetAxis("Mouse Y");
            Vector3 pos = transform.position;

            pos.y += mouseY * dragSpeed;
            transform.position = pos;
        }
        else
        {
            // Smoothly return to original Y position
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, originalY, Time.deltaTime * returnSpeed);
            transform.position = pos;
        }
    }
}