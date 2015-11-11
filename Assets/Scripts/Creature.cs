using System;
using UnityEngine;
using System.Collections;
using System.Threading;
using System.Collections.Generic;

public class Creature : MonoBehaviour
{
    public float initialTime;
    public float finalTime;
    public Vector3 finalPosition;
    public Vector3 initialPosition;
    public float finalDistance;
    public float rnd;
    public string genotype;

    public float evaluationTime = 30;
    public Action<Creature> evaluationCallback;


    void Start()
    {
        StartCoroutine(AwakeCreature());
        rnd = UnityEngine.Random.value;
    }

    IEnumerator AwakeCreature()
    {
        yield return new WaitForSeconds(1);
        StartEvaluation();
        BroadcastMessage("StartMovement", SendMessageOptions.DontRequireReceiver);

        yield return new WaitForSeconds(evaluationTime);
        StopEvaluation();
        BroadcastMessage("StopMovement", SendMessageOptions.DontRequireReceiver);
    }

    private void StartEvaluation()
    {
        initialTime = Time.time;
        initialPosition = transform.position;
        initialPosition.y = 0;
    }

    private void StopEvaluation()
    {
        finalTime = Time.time;
        finalPosition = transform.position;
        finalPosition.y = 0;
        finalDistance = (finalPosition - initialPosition).magnitude;

        if (evaluationCallback != null) evaluationCallback(this);
    }
}
