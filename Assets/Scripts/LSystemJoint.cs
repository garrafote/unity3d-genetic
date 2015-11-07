using UnityEngine;
using System.Collections;

public class LSystemJoint : MonoBehaviour {

    public Quaternion From = Quaternion.identity;
    public Quaternion To = Quaternion.identity;
    public float Frequency;

    private float time = 0f;

	void Update ()
	{
	    var t = Sin(time * Frequency);

	    transform.rotation = Quaternion.Lerp(From, To, t);

	    time += Time.deltaTime;
	}

    public static float Sin(float time)
    {
        var x = Mathf.PI * 2 * time;

        return (Mathf.Sin(x) + 1) * .5f;
    }
}
