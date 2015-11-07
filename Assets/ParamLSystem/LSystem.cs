using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NCalc;

namespace ParametricLSystem
{
    public class LSystem
    {
        // $key<$params>
        public static readonly Regex RulePattern = new Regex(@"(?<key>\w+)<(?<params>.*?)?>", RegexOptions.ExplicitCapture);

        // [ or ] or {tokens}($iterations) or token<params>
        public static readonly Regex TokenPattern = new Regex(@"(?<token>(\[)|(\])|(\{(?<content>.*?)\}(?<iterations>\(.+?\)))|(\w+<.*?>))", RegexOptions.ExplicitCapture);

        public readonly IDictionary<string, LSystemRule> Rules = new Dictionary<string, LSystemRule>();
        public Action PushDelegate;
        public Action PullDelegate;

        public LSystem(Action push, Action pull)
        {
            PushDelegate = push;
            PullDelegate = pull;
        }

        public LSystemRule AddRule(string key, Action<string[]> delegatAction)
        {
            var match = RulePattern.Match(key);
            if (!match.Success)
            {
                return null;
            }

            var rule = new LSystemRule {
                Atom = match.Groups["key"].Value,
                Parameters = match.Groups["params"].Value.Split(','),
                Delegate = delegatAction,
                Fallback = key,
            };

            Rules.Add(rule.Atom, rule);

            return rule;
        }

        // Expands the axiom iteration times parsing all arguments
        // then executes delegates for the resulting expression
        public void Execute(string axiom, int iterations)
        {
            // iterate over the axiom before executing
            for (int i = 0; i < iterations; i++)
            {
                axiom = ExpandAxiom(axiom);
            }

            Execute(axiom);
        }

        // Executes the delegates for a given axiom
        // This version of Execute doesn't parse axiom arguments
        public void Execute(string axiom)
        {
            // break the axiom into tokens and then execute each token
            var matches = TokenPattern.Matches(axiom);

            foreach (Match match in matches)
            {
                var capture = match.Groups["token"].Value;

                if (capture.StartsWith("["))
                {
                    // push
                    PushDelegate();
                }
                else if (capture.StartsWith("]"))
                {
                    // pop
                    PullDelegate();
                }
                else if (capture.StartsWith("{"))
                {
                    var content = match.Groups["content"].Value;

                    var s = match.Groups["iterations"].Value;
                    var expression = new Expression(s);
                    var repetitions = (int)expression.Evaluate();

                    // do nothing in case of 0 repetitions
                    if (repetitions == 0)
                    {
                        continue;
                    }

                    var expandedContent = ExpandAxiom(content);
                    for (int j = 0; j < repetitions; j++)
                    {
                        Execute(expandedContent);
                    }
                }
                else
                {
                    ExecuteToken(capture);
                }
            }
        }
        private void ExecuteToken(string value)
        {
            var match = RulePattern.Match(value);
            var key = match.Groups["key"].Value;
            var args = match.Groups["params"].Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            var rule = Rules[key];
            rule.Delegate(args);
        }

        private string ExpandAxiom(string axiom)
        {
            // [ ] {b<2>}(3) a<2>
            var tokenPattern = new Regex(@"(?<token>(\[)|(\])|(\{(?<content>.*?)\}(?<iterations>\(.+?\)))|(\w+<.*?>))",
                RegexOptions.ExplicitCapture);
            var result = "";

            var matches = tokenPattern.Matches(axiom);
            foreach (Match match in matches)
            {
                var capture = match.Groups["token"].Value;

                if (capture.StartsWith("[") || capture.StartsWith("]"))
                {
                    result += capture;
                }
                else if (capture.StartsWith("{"))
                {
                    var content = match.Groups["content"].Value;

                    var s = match.Groups["iterations"].Value;
                    var expression = new Expression(s);
                    var repetitions = (int) expression.Evaluate();

                    // do nothing in case of 0 repetitions
                    if (repetitions == 0)
                    {
                        continue;
                    }

                    var expandedContent = ExpandAxiom(content);
                    for (int j = 0; j < repetitions; j++)
                    {
                        result += expandedContent;
                    }
                }
                else
                {
                    result += ExpandToken(capture);
                }
            }

            return result;
        }

        private string ExpandToken(string value)
        {
            var match = RulePattern.Match(value);
            var key = match.Groups["key"].Value;
            var args = match.Groups["params"].Value.Split(new []{','}, StringSplitOptions.RemoveEmptyEntries);

            var rule = Rules[key];

            return rule.Expand(args);
        }
    }
}