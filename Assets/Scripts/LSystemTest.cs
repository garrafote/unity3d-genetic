using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using ParametricLSystem;
using Random = UnityEngine.Random;

public class LSystemTest : MonoBehaviour {

    public static readonly Vector3 RotationX = Vector3.right * 90;
    public static readonly Vector3 RotationY = Vector3.up * 90;
    public static readonly Vector3 RotationZ = Vector3.forward * 90;

    public struct TurtleState
    {
        public Vector3 position;
        public Quaternion direction;
        public Quaternion previousPivotDirection;
        public Transform pivot;
        public Transform previousPivot;
        public Transform head;
        public string name;
    }

    public class Turtle
    {
        public TurtleState state;
        public Stack<TurtleState> stack;
        public Material linkMaterial;
        public Material nodeMaterial;
    }

    public class TokenizedGenotype
    {
        public List<string> tokens;
        public int openWrapIndex;
        public int closeWrapIndex;
        internal bool isWrapped;
    }

    private Transform creature;

    private LSystem sys;
    private LSystemRule[] basicRules;
    private string[] ops = new[] { "+", "-", "*" };

    private int currentCreatureId;
    private int currentGenerationId;

    private List<Creature> evaluatedCreatures;
    private int creaturesToEvaluate;

    public Material[] materials;

    void Awake()
    {
        evaluatedCreatures = new List<Creature>();
        sys = new LSystem(PushState, PopState, PrepareState);
    }

	void Start () {
        sys.AddRule("Rev<x>", RevoluteCommand);
        sys.AddRule("Rev2<x>", Revolute2Command);
        sys.AddRule("Tst<x>", TwistCommand);
        sys.AddRule("Tst2<x>", Twist2Command);

        sys.AddRule("R<x>", RollCommand);
        sys.AddRule("P<x>", PitchCommand);
        sys.AddRule("Y<x>", YawCommand);

        sys.AddRule("F<x>", ForwardCommand);

        basicRules = sys.Rules.Values.ToArray();

	    for (int i = 0; i < 10; i++)
	    {
	        for (int j = 0; j < 10; j++)
	        {
                var genotype = CreateGenotype();
                var creature = CreateCreature(genotype, new Vector3(50*i, 10, 50*j));
                creature.evaluationCallback = OnCreatureEvaluated;
	            creaturesToEvaluate += 1;
	        }
	    }
    }

    private void OnCreatureEvaluated(Creature creature)
    {
        creaturesToEvaluate -= 1;
        evaluatedCreatures.Add(creature);

        if (creaturesToEvaluate == 0)
        {
            EvaluateGeneration();
        }
    }


    private void EvaluateGeneration()
    {
        var genotypes = new List<string>();
        //evaluatedCreatures.Sort(MyComparer<Creature>.Create(c => c.evaluationTime));
        var rankedCreatures = evaluatedCreatures.OrderByDescending(b => b.finalDistance).ToArray();
        var top40Creatures = rankedCreatures.Take(40).ToArray();

        // creatures to be removed from LSystem rules
        // either because they were mutated or because they
        // wern't fit according to the fittness function
        var discardedCreatures = rankedCreatures.Skip(20).ToList();

        // start creating the new generation of creatures
        currentGenerationId += 1;
        currentCreatureId = 0;

        // top 20 will be randomly mutated
        foreach (var creature in rankedCreatures.Take(5))
        {
            genotypes.Add(creature.genotype);
        }

        foreach (var creature in rankedCreatures.Skip(5).Take(15))
        {
            if (Random.Range(0.0f, 1.0f) > 0.4f)
            {
                var newGenotype = Mutate(creature.genotype);

                var gen = string.Format("G{0}C{1}", currentGenerationId, currentCreatureId++);

                sys.AddRule(gen + "<x,y>", Nil).Otherwise(newGenotype);

                genotypes.Add(gen);

                discardedCreatures.Add(creature);

                Debug.LogFormat("Mutate: {0} => {1}", creature.genotype, gen);
            }
            else
            {
                genotypes.Add(creature.genotype);
            }

        }

        // top 40 will crossover generating 80 new creatures
        foreach (var creature in top40Creatures)
        {
            var g1 = creature.genotype;
            var g2 = top40Creatures[Random.Range(0, 40)].genotype;

            var newGenotypes = Crossover(g1, g2);

            var g3 = string.Format("G{0}C{1}", currentGenerationId, currentCreatureId++);
            var g4 = string.Format("G{0}C{1}", currentGenerationId, currentCreatureId++);

            sys.AddRule(g3 + "<x,y>", Nil).Otherwise(newGenotypes[0]);
            sys.AddRule(g4 + "<x,y>", Nil).Otherwise(newGenotypes[1]);

            genotypes.Add(g3);
            genotypes.Add(g4);

            Debug.LogFormat("Crossover: {0} {1} => {2} {3}", g1, g2, g3, g4);
        }


        foreach (var creature in discardedCreatures)
        {
            sys.Rules.Remove(creature.genotype);
        }

        foreach (var creature in evaluatedCreatures)
        {
            Destroy(creature.gameObject);
        }
        evaluatedCreatures.Clear();

        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                var genotype = genotypes[(i*10) + j];
                var creature = CreateCreature(genotype, new Vector3(50 * i, 10, 50 * j));
                creature.evaluationCallback = OnCreatureEvaluated;
                creaturesToEvaluate += 1;
            }
        }
    }

    private string Mutate(string genotype)
    {
        var rnd = Random.Range(0, 10);
        if (rnd < 2)
        {
            Debug.Log("Remove");
            return RemoveToken(genotype);
        }
        else if (rnd < 6)
        {
            Debug.Log("Add");
            return AddToken(genotype);
        }
        else
        {
            Debug.Log("Mutate");
            return MutateToken(genotype);
        }
    }

    private string AddToken(string genotype)
    {
        var gen = Tokenize(genotype);
        gen.tokens.Insert(Random.Range(0, gen.tokens.Count), CreateToken());

        return string.Join("", gen.tokens.ToArray());
    }

    private string MutateToken(string genotype)
    {
        var tokenPattern = new Regex(@"(?<key>\w+)<(?<lhs>\w+)(?<op>[\+\-\*])(?<rhs>\w+)>", RegexOptions.ExplicitCapture);

        var gen = Tokenize(genotype);
        int tokenIndex;

        do
        {
            tokenIndex = Random.Range(0, gen.tokens.Count);

        } while (tokenIndex == gen.closeWrapIndex || tokenIndex == gen.openWrapIndex);

        var token = gen.tokens[tokenIndex];
        var match = tokenPattern.Match(token);

        var key = match.Groups["key"].Value;
        var lhs = match.Groups["lhs"].Value;
        var rhs = match.Groups["rhs"].Value;
        var op = match.Groups["op"].Value;

        var rnd = Random.Range(0, 3);
        if (rnd == 0)
        {
            string newOp;
            do
            {
                newOp = ops[Random.Range(0, ops.Length)];

            } while (newOp == op);
        }
        else if (rnd == 1)
        {
            string newLhs;

            if (lhs == "x")
            {
                lhs = "y";
            }
            else
            {
                if (lhs == "y")
                {
                    lhs = "x";
                }
                else
                {
                    lhs = Mathf.Clamp(int.Parse(lhs) + Mathf.Sign(Random.value), -6, 6).ToString();
                }
            }
        }
        else
        {
            if (rhs == "x")
            {
                rhs = "y";
            }
            else
            {
                if (rhs == "y")
                {
                    rhs = "x";
                }
                else
                {
                    rhs = Mathf.Clamp(int.Parse(rhs) + Mathf.Sign(Random.value), -6, 6).ToString();
                }
            }
        }


        gen.tokens[tokenIndex] = string.Format("{0}<{1}{2}{3}>", key, lhs, op, rhs);
        return string.Join("", gen.tokens.ToArray());
    }

    private string RemoveToken(string genotype)
    {
        // only remove if more than 1
        var gen = Tokenize(genotype);
        if (gen.tokens.Count > (gen.isWrapped ? 3 : 1))
        {
            int tokenIndex;
            do
            {
                tokenIndex = Random.Range(0, gen.tokens.Count);

            } while (tokenIndex == gen.closeWrapIndex || tokenIndex == gen.openWrapIndex);
            gen.tokens.RemoveAt(tokenIndex);
        }

        return string.Join("", gen.tokens.ToArray());
    }

    private string[] Crossover(string g1, string g2)
    {
        var r1 = sys.Rules[g1];
        var r2 = sys.Rules[g2];

        var gen1 = Tokenize(g1);
        var gen2 = Tokenize(g2);

        var genOut1 = new List<string>();
        var genOut2 = new List<string>();

        var rndSet = new List<int> {0, 0, 1};

        var idx = Random.Range(0, 3);
        var rnd1 = rndSet[idx];
        rndSet.RemoveAt(idx);

        idx = Random.Range(0, 2);
        var rnd2 = rndSet[idx];
        rndSet.RemoveAt(idx);

        var rnd3 = rndSet[0];

        var genBlock1 = rnd1 == 0 ? gen1 : gen2;
        var genBlock2 = rnd1 == 0 ? gen2 : gen1;

        // add pre block
        for (int i = 0; i < genBlock1.openWrapIndex; i++)
        {
            genOut1.Add(genBlock1.tokens[i]);
        }

        for (int i = 0; i < genBlock2.openWrapIndex; i++)
        {
            genOut2.Add(genBlock2.tokens[i]);
        }

        // add block open wrap
        if (gen1.isWrapped) genOut1.Add(gen1.tokens[gen1.openWrapIndex]);
        if (gen2.isWrapped) genOut2.Add(gen2.tokens[gen2.openWrapIndex]);

        genBlock1 = rnd2 == 0 ? gen1 : gen2;
        genBlock2 = rnd2 == 0 ? gen2 : gen1;

        // add block
        for (int i = genBlock1.openWrapIndex + 1; i < genBlock1.closeWrapIndex; i++)
        {
            genOut1.Add(genBlock1.tokens[i]);
        }

        for (int i = genBlock2.openWrapIndex + 1; i < genBlock2.closeWrapIndex; i++)
        {
            genOut2.Add(genBlock2.tokens[i]);
        }

        // add block close wrap
        if (gen1.isWrapped) genOut1.Add(gen1.tokens[gen1.closeWrapIndex]);
        if (gen2.isWrapped) genOut2.Add(gen2.tokens[gen2.closeWrapIndex]);

        genBlock1 = rnd3 == 0 ? gen1 : gen2;
        genBlock2 = rnd3 == 0 ? gen2 : gen1;

        // add post block
        for (int i = genBlock1.closeWrapIndex + 1; i < genBlock1.tokens.Count; i++)
        {
            genOut1.Add(genBlock1.tokens[i]);
        }

        for (int i = genBlock2.closeWrapIndex + 1; i < genBlock2.tokens.Count; i++)
        {
            genOut2.Add(genBlock2.tokens[i]);
        }

        return new[] {string.Join("", genOut1.ToArray()), string.Join("", genOut2.ToArray())};
    }




    private TokenizedGenotype Tokenize(string genotype)
    {
        var tokenPattern = new Regex(@"(?<token>(\[)|(\])|(\{)(\}\(.+?\))|(\w+<.*?>))", RegexOptions.ExplicitCapture);

        if (!sys.Rules.ContainsKey(genotype))
        {
            Debug.Log("nokey");
        }

        var rule = sys.Rules[genotype];
        var str = rule.Fallback;

        var matches = tokenPattern.Matches(str);
        var result = new TokenizedGenotype {
            tokens = new List<string>(),
            openWrapIndex = -1,
            closeWrapIndex = matches.Count,
            isWrapped = false,
        };

        foreach (Match match in matches.Cast<Match>().OrderBy(m => m.Index))
        {
            var val = match.Value;
            if (val.StartsWith("]") || val.StartsWith("}"))
            {
                result.closeWrapIndex = result.tokens.Count;
                result.isWrapped = true;
            }

            if (val.StartsWith("[") || val.StartsWith("{"))
            {
                result.openWrapIndex = result.tokens.Count;
                result.isWrapped = true;
            }

            result.tokens.Add(val);
        }

        return result;
    }


    private string CreateGenotype()
    {
        var ruleCount = basicRules.Length;
        
        // we can have two different wraps, or no wrap at all 
        var wrapType = Random.Range(0, 3);
        string openWrap = "", closeWrap = "";
        if (wrapType == 0)
        {
            openWrap = "[";
            closeWrap = "]";
        }
        else if (wrapType == 1)
        {
            openWrap = "{";
            closeWrap = "}(" + Random.Range(1, 4) + ")";
        }

        // openWrap will be added to the string BEFORE the atom at the 
        // index 'openWrapIndex' and closeWrap will be added to the string
        // AFTER the atom at the index 'closeWrapIndex'.
        var countAtoms = Random.Range(1, 6);
        var openWrapIndex = Random.Range(0, countAtoms);
        var closeWrapIndex = Random.Range(0, countAtoms);

        // we need to make sure that closeWrapIndex is equal or higher than
        // OpenWrapIndex
        if (closeWrapIndex < openWrapIndex)
        {
            var tmp = openWrapIndex;
            openWrapIndex = closeWrapIndex;
            closeWrapIndex = tmp;
        }


        var genotype = "";
        for (int i = 0; i < countAtoms; i++)
        {
            if (openWrapIndex == i)
            {
                genotype += openWrap;
            }

            genotype += CreateToken();

            if (closeWrapIndex == i)
            {
                genotype += closeWrap;
            }
        }

        var genotypeId = string.Format("G{0}C{1}", currentGenerationId, currentCreatureId);
        currentCreatureId += 1;

        var genotypeKey = genotypeId + "<x,y>";
        sys.AddRule(genotypeKey, Nil).Otherwise(genotype);

        //Debug.Log(genotypeKey + " "+ genotype);

        return genotypeId;
    }

    private string CreateToken()
    {
        var rule = basicRules[Random.Range(0, basicRules.Length)];
        var args = new string[rule.Parameters.Length];
        for (int j = 0; j < args.Length; j++)
        {
            var lhs = Random.Range(0, 4).ToString();
            var rhs = ((Random.Range(0, 2) % 2) == 0) ? "x" : "y";
            var op = ops[Random.Range(0, ops.Length)];

            if ((Random.Range(0, 2) % 2) == 0)
            {
                var tmp = lhs;
                lhs = rhs;
                rhs = tmp;
            }

            args[j] = lhs + op + rhs;
        }

        return string.Format("{0}<{1}>", rule.Atom, string.Join(",", args));
    }


    private static void Nil(object data, string[] args) { }


    void PushState(object data)
    {
        var turtle = (Turtle)data;

        turtle.stack.Push(turtle.state);
    }


    void PopState(object data)
    {
        var turtle = (Turtle)data;

        turtle.state = turtle.stack.Pop();
    }


    void PrepareState(object data, string atom)
    {
        var turtle = (Turtle)data;

        turtle.state.name = atom;
    }


    void ForwardCommand(object data, string[] args)
    {
        var value = float.Parse(args[0]);
        var turtle = (Turtle)data;

        Forward(turtle, value);
        AddNode(turtle);
    }
    

    void RollCommand(object data, string[] args)
    {
        var value = float.Parse(args[0]);
        var turtle = (Turtle)data;

        Roll(turtle, value);
    }


    void PitchCommand(object data, string[] args)
    {
        var value = float.Parse(args[0]);
        var turtle = (Turtle)data;

        Pitch(turtle, value);
    }


    void YawCommand(object data, string[] args)
    {
        var value = float.Parse(args[0]);
        var turtle = (Turtle)data;

        Yaw(turtle, value);
    }


    void RevoluteCommand(object data, string[] args)
    {
        var value = float.Parse(args[0]);
        var turtle = (Turtle)data;

        AddPivot(turtle);
        //Pitch(1);
        Revolute(turtle, value);
        Forward(turtle, 1);
        //Pitch(-1);
        AddNode(turtle);
    }


    void Revolute2Command(object data, string[] args)
    {
        var value = float.Parse(args[0]);
        var turtle = (Turtle)data;

        AddPivot(turtle);
        Revolute2(turtle, value);
        Forward(turtle, 1);
        AddNode(turtle);
    }


    void TwistCommand(object data, string[] args)
    {
        var value = float.Parse(args[0]);
        var turtle = (Turtle)data;

        AddPivot(turtle);
        Twist(turtle, value);
        Forward(turtle, 1);
        AddNode(turtle);
    }


    void Twist2Command(object data, string[] args)
    {
        var value = float.Parse(args[0]);
        var turtle = (Turtle)data;

        AddPivot(turtle);
        Twist2(turtle, value);
        Forward(turtle, 1);
        AddNode(turtle);
    }


    Creature CreateCreature(string genotype, Vector3 position)
    {
        var go = new GameObject(string.Format("Creature [{0}] => {1}", genotype, sys.Rules[genotype].Fallback));
        go.transform.position = position;

        Debug.Log(string.Format("Creature [{0}] => {1}", genotype, sys.Rules[genotype].Fallback));

        var cr = go.AddComponent<Creature>();
        cr.genotype = genotype;

        var rb = go.AddComponent<Rigidbody>();
        //rb.isKinematic = true;

        var turtle = new Turtle
        {
            stack = new Stack<TurtleState>(),
            state = new TurtleState {
                head = creature = go.transform,
                position = position,
                direction = Quaternion.identity,
            },
            linkMaterial = materials[Random.Range(0, materials.Length)],
            nodeMaterial = materials[Random.Range(0, materials.Length)],
        };

        AddPivot(turtle);
        AddNode(turtle);

        // add pivot sets state.pivot to the added pivot
        var joint = turtle.state.pivot.gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = rb;

        // always start with params 1 and 1
        sys.Execute(genotype + "<1,1>", turtle, 1);

        return cr;
    }


    void AddNode(Turtle turtle)
    {
        var node = GetObjectAt(turtle, turtle.state.position);
        if (node)
        {
            turtle.state.head = node;
            return;
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = string.Format("Node [{0}]", turtle.state.name);

        go.GetComponent<MeshRenderer>().material = turtle.nodeMaterial;

        turtle.state.pivot.GetComponent<Rigidbody>().mass += 1;

        go.transform.SetParent(turtle.state.pivot);
        go.transform.position = turtle.state.position;

        turtle.state.head = go.transform;
    }


    void AddPivot(Turtle turtle)
    {
        var go = new GameObject(string.Format("Pivot [{0}]", turtle.state.name));

        //go.transform.SetParent(turtle.state.pivot ?? creature);
        go.transform.SetParent(creature);
        go.transform.position = turtle.state.position;
        go.transform.localRotation = turtle.state.direction;

        var rb = go.AddComponent<Rigidbody>();

        turtle.state.previousPivot = turtle.state.pivot;
        turtle.state.previousPivotDirection = turtle.state.direction;
        turtle.state.pivot = go.transform;
    }


    void AddJoint(Turtle turtle, float min, float max, Vector3 axis, float frequency)
    {
        if (turtle.state.previousPivot == null)
        {
            return;
        }

        var jointAffector = turtle.state.pivot.gameObject.AddComponent<LSystemJoint>();
        jointAffector.Frequency = frequency * 0.3f;
        jointAffector.Axis = axis;

        var joint = turtle.state.pivot.gameObject.AddComponent<HingeJoint>();
        joint.connectedBody = turtle.state.previousPivot.GetComponent<Rigidbody>();
        joint.autoConfigureConnectedAnchor = false;
       
        joint.connectedAnchor = turtle.state.previousPivot.InverseTransformPoint(turtle.state.head.position);
        joint.useLimits = true;
        joint.limits = new JointLimits {min = min, max = max};
        joint.axis = axis;

    }

    Transform GetObjectAt(Turtle turtle, Vector3 position)
    {
        foreach (Transform child in turtle.state.pivot)
        {
            if ((position - child.position).magnitude < 0.001f)
            {
                return child;
            }
        }

        return null;
    }

    void Forward(Turtle turtle, float value)
    {
        var previousPosition = turtle.state.position;
        var len = Mathf.RoundToInt(Mathf.Max(0, value));


        for (int i = 0; i < len; i++)
        {
            var move = turtle.state.direction * Vector3.forward;
            turtle.state.position.x += Mathf.Round(move.x * 1000) * .001f;
            turtle.state.position.y += Mathf.Round(move.y * 1000) * .001f;
            turtle.state.position.z += Mathf.Round(move.z * 1000) * .001f;

            var linkPosition = (turtle.state.position + previousPosition) * .5f;

            var link = GetObjectAt(turtle, linkPosition);
            if (!link)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Link";

                var norm = (turtle.state.position - previousPosition).normalized;
                var rotation = new Vector3 {
                   z = 90 * norm.y,
                   x = 90 * norm.z,
                };

                go.GetComponent<MeshRenderer>().material = turtle.linkMaterial;

                go.transform.SetParent(turtle.state.pivot);
                go.transform.position = linkPosition;
                go.transform.localScale = Vector3.one * .5f;
                go.transform.rotation = Quaternion.Euler(RotationZ) * Quaternion.Euler(rotation);
            }


            previousPosition = turtle.state.position;
        }
    }


    void Yaw(Turtle turtle, float value)
    {
        turtle.state.direction *= Quaternion.Euler(RotationY * value);
    }


    void Pitch(Turtle turtle, float value)
    {
        turtle.state.direction *= Quaternion.Euler(RotationX * value);
    }


    void Roll(Turtle turtle, float value)
    {
        turtle.state.direction *= Quaternion.Euler(RotationZ * value);
    }


    void Revolute(Turtle turtle, float value)
    {
        AddJoint(turtle, 0, 90, Vector3.forward, value);
    }


    void Revolute2(Turtle turtle, float value)
    {
        AddJoint(turtle, -45, 45, Vector3.forward, value);
    }


    void Twist(Turtle turtle, float value)
    {
        AddJoint(turtle, 0, 90, Vector3.up, value);
    }


    void Twist2(Turtle turtle, float value)
    {
        AddJoint(turtle, -90, 90, Vector3.up, value);
    }

}
