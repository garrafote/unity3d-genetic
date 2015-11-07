using System.Collections.Generic;
using UnityEngine;
using ParametricLSystem;

public class LSystemTest : MonoBehaviour {

    public static readonly Vector3 RotationX = Vector3.right * 90;
    public static readonly Vector3 RotationY = Vector3.up * 90;
    public static readonly Vector3 RotationZ = Vector3.forward * 90;

    public struct TurtleState
    {
        public Vector3 position;
        public Quaternion direction;
        public Transform pivot;
        public Transform head;
    }

    public Stack<TurtleState> stack;
    public TurtleState state;

    public Transform creature;

    void Awake()
    {
        stack = new Stack<TurtleState>();
        state = new TurtleState {
            position = Vector3.zero,
            direction = Quaternion.identity,
        };
    }

	void Start () {
        var sys = new LSystem(PushState, PopState);

        sys.AddRule("Rev<x>", RevoluteCommand);
        sys.AddRule("Twist<x>", TwistCommand);
        sys.AddRule("F<x>", ForwardCommand);
        sys.AddRule("L<x>", LeftCommand);
        sys.AddRule("R<x>", RightCommand);
	    sys.AddRule("Crazy<x>", Nil).Otherwise("L<x>[Crazy<x>{F<x>}(x)Twist<x>]R<x>[Crazy<x>{F<x>}(x)Rev<x>]");

        CreateCreature();
        //sys.Execute("[L<>F<>][R<>F<>]");
        //sys.Execute("[L<>F<>][R<>F<>]Rev<>F<>");
        //sys.Execute("Crazy<1>", 2);
        sys.Execute("[L<1>F<1>][R<1>F<1>]Rev<1>F<1>{F<1>F<1>[L<1>F<1>Twist<1>F<1>L<1>F<1>R<1>F<1>][R<1>F<1>Twist<1>F<1>R<1>F<1>L<1>F<1>]}(2)", 1);
    }

    void PushState()
    {
        stack.Push(state);
    }

    void PopState()
    {
        state = stack.Pop();
    }

    private void Nil(string[] args)
    {

    }

    void ForwardCommand(string[] args)
    {
        Forward(1);
        AddNode();
    }

    void LeftCommand(string[] args)
    {
        Left(1);
    }

    void RightCommand(string[] args)
    {
        Right(1);
    }

    void RevoluteCommand(string[] args)
    {
        Revolute(1);
        Forward(1);
        AddNode();
    }


    void TwistCommand(string[] args)
    {
        Twist(1);
        Forward(1);
        AddNode();
    }

    void CreateCreature()
    {
        var go = new GameObject("Creature");

        go.AddComponent<Rigidbody>();

        state.head = creature = go.transform;
        AddPivot();
    }

    void AddNode()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Node";

        go.transform.SetParent(state.pivot);
        go.transform.position = state.position;

        state.head = go.transform;
    }

    void AddPivot()
    {
        var go = new GameObject("Pivot");

        go.transform.SetParent(state.pivot ?? creature);
        go.transform.position = state.position;
        
        state.pivot = go.transform;
    }

    void AddJoint(Quaternion from, Quaternion to, float frequency)
    {
        var joint = state.pivot.gameObject.AddComponent<LSystemJoint>();
        joint.From = from;
        joint.To = to;
        joint.Frequency = frequency;
    }

    void Forward(float distance)
    {
        var move = state.direction * Vector3.forward * distance;
        state.position.x += Mathf.Round(move.x * 1000) * .001f;
        state.position.y += Mathf.Round(move.y * 1000) * .001f;
        state.position.z += Mathf.Round(move.z * 1000) * .001f;
    }

    void Left(float value)
    {
        state.direction *= Quaternion.Euler(-RotationY * value);
    }

    void Right(float value)
    {
        state.direction *= Quaternion.Euler(RotationY * value);
    }

    void Revolute(float value)
    {
        AddPivot();
        AddJoint(Quaternion.identity, Quaternion.Euler(RotationX), 0);
    }

    void Twist(float value)
    {
        AddPivot();
        AddJoint(Quaternion.Euler(-RotationZ), Quaternion.Euler(RotationZ), 0);
    }

}
