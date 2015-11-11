using UnityEngine;
using System.Collections;

public class LSystemJoint : MonoBehaviour {

    public float Frequency;
    public float Force = 100;
    public Vector3 Axis = Vector3.up;
    private float time = 0f;

    private bool isMoving;

	void Update ()
	{
	    if (!isMoving)
	    {
	        return;
	    }

	    var t = Sin(time * Frequency);
	    //var a = axis == Vector3.up ? Vector3.right : Vector3.up;
        GetComponent<Rigidbody>().AddTorque(transform.localRotation * Axis * Force * Mathf.Sin(time * 2 * Mathf.PI * Frequency));

	    time += Time.deltaTime;
	}

    void StartMovement()
    {
        isMoving = true;
    }

    void StopMovement()
    {
        isMoving = false;
    }

    public static float Sin(float time)
    {
        var x = Mathf.PI * 2 * time;

        return (Mathf.Sin(x) + 1) * .5f;
    }
}
