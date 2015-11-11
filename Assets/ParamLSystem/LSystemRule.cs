using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NCalc;

namespace ParametricLSystem
{
    public class LSystemRule
    {
        public string Atom;
        public string[] Parameters;
        public string Fallback;
        public readonly IDictionary<string, string> Conditionals = new Dictionary<string, string>();
        public Action<object, string[]> Delegate;

        public LSystemRule Conditional(string condition, string value)
        {
            Conditionals.Add(condition, value);

            return this;
        }

        public LSystemRule Otherwise(string fallback)
        {
            Fallback = fallback;

            return this;
        }

        public string Expand(string[] args)
        {
            var rule = Fallback;

            foreach (var pair in Conditionals)
            {
                var condition = ApplyArgs(pair.Key, args);
                var expression = new Expression(condition);
                if (!(bool) expression.Evaluate())
                    continue;

                rule = pair.Value;
                break;
            }
            
            var result = EvaluateArgs(rule, args);
            result = ExpandLoops(result, args);

            return result;
        }

        private string ApplyArgs(string str, string[] args)
        {
            for (int i = 0; i < Parameters.Length; i++)
            {
                str = str.Replace(Parameters[i], args[i]);
            }

            return str;
        }

        private string EvaluateArgs(string str, string[] args)
        {
            var start = str.IndexOf('<');
            var start1 = start + 1;
            var end = 0;

            var result = "";

            while (start > 0)
            {
                result += str.Substring(end, start1 - end);

                end = str.IndexOf('>', start);

                var splitStr = str.Substring(start1, end - start1);

                var split = splitStr.Split(',');
                for (int i = 0; i < split.Length; i++)
                {
                    var s = ApplyArgs(split[i], args);
                    var expression = new Expression(s);
                    split[i] = expression.Evaluate().ToString();
                }

                result += string.Join(",", split);

                start = str.IndexOf('<', end);
                start1 = start + 1;
            }

            result += str.Substring(end, str.Length - end);

            return result;
        }

        private string ExpandLoops(string str, string[] args)
        {
            var result = "";
            var matches = LSystem.TokenPattern.Matches(str);
            foreach (Match match in matches)
            {
                var capture = match.Groups["token"].Value;

                if (capture.StartsWith("{"))
                {
                    var content = match.Groups["content"].Value;


                    var s = ApplyArgs(match.Groups["iterations"].Value, args);
                    var expression = new Expression(s);
                    var iterations = (int)expression.Evaluate();

                    for (int i = 0; i < iterations; i++)
                    {
                        result += content;
                    }
                }
                else
                {
                    result += capture;
                }
            }

            return result;
        }
    }
}