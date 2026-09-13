using System;
using System.Collections.Generic;
using System.Globalization;

namespace GLaDE.Core
{
    /// <summary>
    /// Tiny arithmetic expression evaluator used for parametric geometry and loads.
    /// Supports + - * / ^, parentheses, unary minus, named parameters and the
    /// functions sin, cos, tan (degrees), sqrt, abs, min, max.
    /// </summary>
    public static class Expr
    {
        public static double Eval(string source, IReadOnlyDictionary<string, double> vars)
        {
            if (string.IsNullOrWhiteSpace(source)) return 0;
            var p = new Parser(source, vars);
            double v = p.ParseExpression();
            p.ExpectEnd();
            return v;
        }

        public static bool TryEval(string source, IReadOnlyDictionary<string, double> vars, out double value)
        {
            try { value = Eval(source, vars); return true; }
            catch { value = 0; return false; }
        }

        class Parser
        {
            readonly string s;
            readonly IReadOnlyDictionary<string, double> vars;
            int i;

            public Parser(string source, IReadOnlyDictionary<string, double> variables)
            {
                s = source; vars = variables; i = 0;
            }

            void SkipWs() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            public void ExpectEnd()
            {
                SkipWs();
                if (i != s.Length) throw new FormatException($"Unexpected '{s[i]}' at {i} in \"{s}\"");
            }

            public double ParseExpression()
            {
                double v = ParseTerm();
                while (true)
                {
                    SkipWs();
                    if (i < s.Length && s[i] == '+') { i++; v += ParseTerm(); }
                    else if (i < s.Length && s[i] == '-') { i++; v -= ParseTerm(); }
                    else return v;
                }
            }

            double ParseTerm()
            {
                double v = ParsePower();
                while (true)
                {
                    SkipWs();
                    if (i < s.Length && s[i] == '*') { i++; v *= ParsePower(); }
                    else if (i < s.Length && s[i] == '/') { i++; v /= ParsePower(); }
                    else return v;
                }
            }

            double ParsePower()
            {
                double v = ParseUnary();
                SkipWs();
                if (i < s.Length && s[i] == '^') { i++; v = Math.Pow(v, ParsePower()); }
                return v;
            }

            double ParseUnary()
            {
                SkipWs();
                if (i < s.Length && s[i] == '-') { i++; return -ParseUnary(); }
                if (i < s.Length && s[i] == '+') { i++; return ParseUnary(); }
                return ParsePrimary();
            }

            double ParsePrimary()
            {
                SkipWs();
                if (i >= s.Length) throw new FormatException($"Unexpected end of expression \"{s}\"");
                char c = s[i];
                if (c == '(')
                {
                    i++;
                    double v = ParseExpression();
                    SkipWs();
                    if (i >= s.Length || s[i] != ')') throw new FormatException($"Missing ')' in \"{s}\"");
                    i++;
                    return v;
                }
                if (char.IsDigit(c) || c == '.')
                {
                    int start = i;
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                    return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                    string name = s.Substring(start, i - start);
                    SkipWs();
                    if (i < s.Length && s[i] == '(')
                    {
                        i++;
                        var args = new List<double>();
                        SkipWs();
                        if (i < s.Length && s[i] == ')') { i++; }
                        else
                        {
                            while (true)
                            {
                                args.Add(ParseExpression());
                                SkipWs();
                                if (i < s.Length && s[i] == ',') { i++; continue; }
                                if (i < s.Length && s[i] == ')') { i++; break; }
                                throw new FormatException($"Bad argument list in \"{s}\"");
                            }
                        }
                        return CallFunction(name, args);
                    }
                    if (vars != null && vars.TryGetValue(name, out double val)) return val;
                    if (name == "pi") return Math.PI;
                    throw new FormatException($"Unknown parameter '{name}' in \"{s}\"");
                }
                throw new FormatException($"Unexpected '{c}' at {i} in \"{s}\"");
            }

            static double CallFunction(string name, List<double> a)
            {
                const double d2r = Math.PI / 180.0;
                switch (name.ToLowerInvariant())
                {
                    case "sin": return Math.Sin(a[0] * d2r);
                    case "cos": return Math.Cos(a[0] * d2r);
                    case "tan": return Math.Tan(a[0] * d2r);
                    case "sqrt": return Math.Sqrt(a[0]);
                    case "abs": return Math.Abs(a[0]);
                    case "min": return Math.Min(a[0], a[1]);
                    case "max": return Math.Max(a[0], a[1]);
                    default: throw new FormatException($"Unknown function '{name}'");
                }
            }
        }
    }
}
