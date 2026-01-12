using System.Globalization;
using System.Text.Json;
using KuyumculukTakipProgrami.Application.Gold.Formula;
using KuyumculukTakipProgrami.Domain.Entities;

namespace KuyumculukTakipProgrami.Infrastructure.Services;

public sealed class GoldFormulaEngine : IGoldFormulaEngine
{
    private const int MaxSteps = 200;
    private const int MaxExprLength = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GoldFormulaEvaluationResult Evaluate(string definitionJson, GoldFormulaContext context, GoldFormulaMode mode)
    {
        var definition = ParseDefinition(definitionJson);
        var variables = BuildInitialVariables(context, definition.Vars);
        var evalContext = new FormulaEvaluationContext(variables, mode == GoldFormulaMode.Preview ? 3 : 2);
        var debugSteps = new List<string>();

        foreach (var step in definition.Steps)
        {
            if (string.Equals(step.Op, "set", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(step.Var))
                    throw new ArgumentException("Formül adımı var boş olamaz.");
                if (!step.Value.HasValue)
                    throw new ArgumentException($"Formül adımı '{step.Var}' için value gerekli.");
                evalContext.Variables[step.Var] = step.Value.Value;
                debugSteps.Add($"{step.Var}={step.Value.Value.ToString(CultureInfo.InvariantCulture)}");
                continue;
            }

            if (string.Equals(step.Op, "calc", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(step.Var))
                    throw new ArgumentException("Formül adımı var boş olamaz.");
                if (string.IsNullOrWhiteSpace(step.Expr))
                    throw new ArgumentException($"Formül adımı '{step.Var}' için expr gerekli.");
                if (step.Expr.Length > MaxExprLength)
                    throw new ArgumentException($"Formül ifadesi çok uzun: {step.Var}");

                var parser = new ExpressionParser(step.Expr);
                var expr = parser.ParseExpression();
                var value = expr.Evaluate(evalContext).AsNumber();
                evalContext.Variables[step.Var] = value;
                debugSteps.Add($"{step.Var}={value.ToString(CultureInfo.InvariantCulture)}");
                continue;
            }

            throw new ArgumentException($"Desteklenmeyen op: {step.Op}");
        }

        var output = definition.Output is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(definition.Output, StringComparer.OrdinalIgnoreCase);
        if (!TryGetOutput(output, evalContext.Variables, "gram", out var gram))
            throw new ArgumentException("Formül çıktısı 'gram' gerekli.");
        if (!TryGetOutput(output, evalContext.Variables, "laborGross", out var laborGross))
            throw new ArgumentException("Formül çıktısı 'laborGross' gerekli.");

        var amount = TryGetOutput(output, evalContext.Variables, "amount", out var amountValue)
            ? amountValue
            : context.Amount;
        var goldService = TryGetOutput(output, evalContext.Variables, "goldService", out var goldServiceValue)
            ? goldServiceValue
            : 0m;
        var laborNet = TryGetOutput(output, evalContext.Variables, "laborNet", out var laborNetValue)
            ? laborNetValue
            : 0m;
        var vat = TryGetOutput(output, evalContext.Variables, "vat", out var vatValue)
            ? vatValue
            : 0m;
        var unitHasPrice = TryGetOutput(output, evalContext.Variables, "unitHasPriceUsed", out var unitHasValue)
            ? unitHasValue
            : 0m;

        return new GoldFormulaEvaluationResult
        {
            Result = new GoldCalculationResult
            {
                Gram = gram,
                Amount = amount,
                GoldServiceAmount = goldService,
                LaborGross = laborGross,
                LaborNet = laborNet,
                Vat = vat,
                UnitHasPriceUsed = unitHasPrice
            },
            UsedVariables = new Dictionary<string, decimal>(evalContext.Variables, StringComparer.OrdinalIgnoreCase),
            DebugSteps = debugSteps
        };
    }

    public void ValidateDefinition(string definitionJson)
    {
        _ = ParseDefinition(definitionJson);
    }

    private static GoldFormulaDefinition ParseDefinition(string definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson))
            throw new ArgumentException("Formül tanımı boş olamaz.");

        GoldFormulaDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<GoldFormulaDefinition>(definitionJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Formül JSON hatalı: {ex.Message}");
        }

        if (definition is null)
            throw new ArgumentException("Formül tanımı okunamadı.");
        if (definition.Steps is null || definition.Steps.Count == 0)
            throw new ArgumentException("Formül adımları boş olamaz.");
        if (definition.Steps.Count > MaxSteps)
            throw new ArgumentException("Formül adım sayısı limiti aşıldı.");

        foreach (var step in definition.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Op))
                throw new ArgumentException("Formül adımı op boş olamaz.");
        }

        return definition;
    }

    private static Dictionary<string, decimal> BuildInitialVariables(GoldFormulaContext context, Dictionary<string, decimal>? vars)
    {
        var dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Amount"] = context.Amount,
            ["HasGoldPrice"] = context.HasGoldPrice,
            ["VatRate"] = context.VatRate,
            ["AltinSatisFiyati"] = context.AltinSatisFiyati ?? context.HasGoldPrice,
            ["Product.Gram"] = context.ProductGram ?? 0m,
            ["Product.AccountingType"] = (decimal)(int)context.AccountingType,
            ["Direction"] = (decimal)(int)context.Direction,
            ["OperationType"] = (decimal)(int)context.OperationType
        };

        if (vars is null) return dict;

        foreach (var kvp in vars)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
            dict[kvp.Key] = kvp.Value;
        }

        return dict;
    }

    private static bool TryGetOutput(
        IReadOnlyDictionary<string, string> output,
        IReadOnlyDictionary<string, decimal> vars,
        string key,
        out decimal value)
    {
        value = 0m;
        if (!output.TryGetValue(key, out var varName) || string.IsNullOrWhiteSpace(varName))
            return false;
        if (!vars.TryGetValue(varName, out value))
            throw new ArgumentException($"Çıktı değişkeni bulunamadı: {varName}");
        return true;
    }

    private sealed class GoldFormulaDefinition
    {
        public Dictionary<string, decimal>? Vars { get; set; }
        public List<GoldFormulaStep> Steps { get; set; } = new();
        public Dictionary<string, string>? Output { get; set; }
    }

    private sealed class GoldFormulaStep
    {
        public string Op { get; set; } = string.Empty;
        public string Var { get; set; } = string.Empty;
        public decimal? Value { get; set; }
        public string? Expr { get; set; }
    }

    private sealed class FormulaEvaluationContext
    {
        public FormulaEvaluationContext(Dictionary<string, decimal> variables, int defaultRound)
        {
            Variables = variables;
            DefaultRound = defaultRound;
        }

        public Dictionary<string, decimal> Variables { get; }
        public int DefaultRound { get; }
    }

    private readonly struct FormulaValue
    {
        private FormulaValue(decimal number, bool boolean, bool isBool)
        {
            Number = number;
            Boolean = boolean;
            IsBool = isBool;
        }

        public decimal Number { get; }
        public bool Boolean { get; }
        public bool IsBool { get; }

        public static FormulaValue FromNumber(decimal value) => new(value, value != 0m, false);
        public static FormulaValue FromBool(bool value) => new(value ? 1m : 0m, value, true);

        public decimal AsNumber() => IsBool ? (Boolean ? 1m : 0m) : Number;
        public bool AsBool() => IsBool ? Boolean : Number != 0m;
    }

    private abstract class ExprNode
    {
        public abstract FormulaValue Evaluate(FormulaEvaluationContext context);
    }

    private sealed class NumberNode : ExprNode
    {
        private readonly decimal _value;
        public NumberNode(decimal value) => _value = value;
        public override FormulaValue Evaluate(FormulaEvaluationContext context) => FormulaValue.FromNumber(_value);
    }

    private sealed class VariableNode : ExprNode
    {
        private readonly string _name;
        public VariableNode(string name) => _name = name;
        public override FormulaValue Evaluate(FormulaEvaluationContext context)
        {
            if (!context.Variables.TryGetValue(_name, out var value))
                throw new ArgumentException($"Bilinmeyen değişken: {_name}");
            return FormulaValue.FromNumber(value);
        }
    }

    private sealed class UnaryNode : ExprNode
    {
        private readonly string _op;
        private readonly ExprNode _expr;
        public UnaryNode(string op, ExprNode expr)
        {
            _op = op;
            _expr = expr;
        }

        public override FormulaValue Evaluate(FormulaEvaluationContext context)
        {
            var value = _expr.Evaluate(context).AsNumber();
            return _op == "-" ? FormulaValue.FromNumber(-value) : FormulaValue.FromNumber(value);
        }
    }

    private sealed class BinaryNode : ExprNode
    {
        private readonly string _op;
        private readonly ExprNode _left;
        private readonly ExprNode _right;

        public BinaryNode(string op, ExprNode left, ExprNode right)
        {
            _op = op;
            _left = left;
            _right = right;
        }

        public override FormulaValue Evaluate(FormulaEvaluationContext context)
        {
            if (_op is "+" or "-" or "*" or "/")
            {
                var l = _left.Evaluate(context).AsNumber();
                var r = _right.Evaluate(context).AsNumber();
                return _op switch
                {
                    "+" => FormulaValue.FromNumber(l + r),
                    "-" => FormulaValue.FromNumber(l - r),
                    "*" => FormulaValue.FromNumber(l * r),
                    "/" => r == 0m ? throw new ArgumentException("Sıfıra bölme hatası.") : FormulaValue.FromNumber(l / r),
                    _ => throw new ArgumentException($"Desteklenmeyen operatör: {_op}")
                };
            }

            if (_op is "==" or "!=" or "<" or "<=" or ">" or ">=")
            {
                var l = _left.Evaluate(context);
                var r = _right.Evaluate(context);
                if (l.IsBool || r.IsBool)
                {
                    var leftValue = l.AsBool();
                    var rightValue = r.AsBool();
                    var result = _op switch
                    {
                        "==" => leftValue == rightValue,
                        "!=" => leftValue != rightValue,
                        _ => throw new ArgumentException("Bool karsilastirmasi sadece == veya != ile yapilabilir.")
                    };
                    return FormulaValue.FromBool(result);
                }

                var leftNumber = l.AsNumber();
                var rightNumber = r.AsNumber();
                var numberResult = _op switch
                {
                    "==" => leftNumber == rightNumber,
                    "!=" => leftNumber != rightNumber,
                    "<" => leftNumber < rightNumber,
                    "<=" => leftNumber <= rightNumber,
                    ">" => leftNumber > rightNumber,
                    ">=" => leftNumber >= rightNumber,
                    _ => false
                };
                return FormulaValue.FromBool(numberResult);
            }

            throw new ArgumentException($"Desteklenmeyen operatör: {_op}");
        }
    }

    private sealed class TernaryNode : ExprNode
    {
        private readonly ExprNode _condition;
        private readonly ExprNode _whenTrue;
        private readonly ExprNode _whenFalse;
        public TernaryNode(ExprNode condition, ExprNode whenTrue, ExprNode whenFalse)
        {
            _condition = condition;
            _whenTrue = whenTrue;
            _whenFalse = whenFalse;
        }

        public override FormulaValue Evaluate(FormulaEvaluationContext context)
        {
            var cond = _condition.Evaluate(context).AsBool();
            return cond ? _whenTrue.Evaluate(context) : _whenFalse.Evaluate(context);
        }
    }

    private sealed class FunctionNode : ExprNode
    {
        private readonly string _name;
        private readonly List<ExprNode> _args;
        public FunctionNode(string name, List<ExprNode> args)
        {
            _name = name;
            _args = args;
        }

        public override FormulaValue Evaluate(FormulaEvaluationContext context)
        {
            var lower = _name.ToLowerInvariant();
            if (lower == "round")
            {
                if (_args.Count is < 1 or > 2)
                    throw new ArgumentException("round fonksiyonu 1 veya 2 parametre alir.");
                var value = _args[0].Evaluate(context).AsNumber();
                var precision = _args.Count == 2
                    ? (int)_args[1].Evaluate(context).AsNumber()
                    : context.DefaultRound;
                return FormulaValue.FromNumber(Math.Round(value, precision, MidpointRounding.AwayFromZero));
            }

            if (lower == "min" || lower == "max")
            {
                if (_args.Count != 2)
                    throw new ArgumentException($"{_name} fonksiyonu 2 parametre alir.");
                var left = _args[0].Evaluate(context).AsNumber();
                var right = _args[1].Evaluate(context).AsNumber();
                return FormulaValue.FromNumber(lower == "min" ? Math.Min(left, right) : Math.Max(left, right));
            }

            if (lower == "abs")
            {
                if (_args.Count != 1)
                    throw new ArgumentException("abs fonksiyonu 1 parametre alir.");
                var value = _args[0].Evaluate(context).AsNumber();
                return FormulaValue.FromNumber(Math.Abs(value));
            }

            throw new ArgumentException($"Desteklenmeyen fonksiyon: {_name}");
        }
    }

    private sealed class ExpressionParser
    {
        private readonly Tokenizer _tokenizer;

        public ExpressionParser(string input)
        {
            _tokenizer = new Tokenizer(input);
        }

        public ExprNode ParseExpression() => ParseTernary();

        private ExprNode ParseTernary()
        {
            var condition = ParseComparison();
            if (_tokenizer.Current.Type == TokenType.Question)
            {
                _tokenizer.Next();
                var whenTrue = ParseExpression();
                _tokenizer.Expect(TokenType.Colon);
                var whenFalse = ParseExpression();
                return new TernaryNode(condition, whenTrue, whenFalse);
            }
            return condition;
        }

        private ExprNode ParseComparison()
        {
            var left = ParseAdditive();
            while (_tokenizer.Current.Type == TokenType.Operator && _tokenizer.Current.Value is "==" or "!=" or "<" or "<=" or ">" or ">=")
            {
                var op = _tokenizer.Current.Value;
                _tokenizer.Next();
                var right = ParseAdditive();
                left = new BinaryNode(op, left, right);
            }
            return left;
        }

        private ExprNode ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (_tokenizer.Current.Type == TokenType.Operator && _tokenizer.Current.Value is "+" or "-")
            {
                var op = _tokenizer.Current.Value;
                _tokenizer.Next();
                var right = ParseMultiplicative();
                left = new BinaryNode(op, left, right);
            }
            return left;
        }

        private ExprNode ParseMultiplicative()
        {
            var left = ParseUnary();
            while (_tokenizer.Current.Type == TokenType.Operator && _tokenizer.Current.Value is "*" or "/")
            {
                var op = _tokenizer.Current.Value;
                _tokenizer.Next();
                var right = ParseUnary();
                left = new BinaryNode(op, left, right);
            }
            return left;
        }

        private ExprNode ParseUnary()
        {
            if (_tokenizer.Current.Type == TokenType.Operator && _tokenizer.Current.Value is "+" or "-")
            {
                var op = _tokenizer.Current.Value;
                _tokenizer.Next();
                var expr = ParseUnary();
                return new UnaryNode(op, expr);
            }
            return ParsePrimary();
        }

        private ExprNode ParsePrimary()
        {
            if (_tokenizer.Current.Type == TokenType.Number)
            {
                var value = _tokenizer.Current.NumberValue;
                _tokenizer.Next();
                return new NumberNode(value);
            }

            if (_tokenizer.Current.Type == TokenType.Identifier)
            {
                var name = _tokenizer.Current.Value;
                _tokenizer.Next();
                if (_tokenizer.Current.Type == TokenType.LeftParen)
                {
                    _tokenizer.Next();
                    var args = new List<ExprNode>();
                    if (_tokenizer.Current.Type != TokenType.RightParen)
                    {
                        while (true)
                        {
                            args.Add(ParseExpression());
                            if (_tokenizer.Current.Type == TokenType.Comma)
                            {
                                _tokenizer.Next();
                                continue;
                            }
                            break;
                        }
                    }
                    _tokenizer.Expect(TokenType.RightParen);
                    return new FunctionNode(name, args);
                }
                return new VariableNode(name);
            }

            if (_tokenizer.Current.Type == TokenType.LeftParen)
            {
                _tokenizer.Next();
                var expr = ParseExpression();
                _tokenizer.Expect(TokenType.RightParen);
                return expr;
            }

            throw new ArgumentException($"Beklenmeyen token: {_tokenizer.Current.Value}");
        }
    }

    private enum TokenType
    {
        End,
        Number,
        Identifier,
        Operator,
        LeftParen,
        RightParen,
        Comma,
        Question,
        Colon
    }

    private sealed class Token
    {
        public Token(TokenType type, string value, decimal numberValue = 0m)
        {
            Type = type;
            Value = value;
            NumberValue = numberValue;
        }

        public TokenType Type { get; }
        public string Value { get; }
        public decimal NumberValue { get; }
    }

    private sealed class Tokenizer
    {
        private readonly string _input;
        private int _pos;

        public Tokenizer(string input)
        {
            _input = input ?? string.Empty;
            Next();
        }

        public Token Current { get; private set; } = new(TokenType.End, string.Empty);

        public void Next()
        {
            SkipWhitespace();
            if (_pos >= _input.Length)
            {
                Current = new Token(TokenType.End, string.Empty);
                return;
            }

            var ch = _input[_pos];
            if (char.IsDigit(ch) || ch == '.')
            {
                var start = _pos;
                while (_pos < _input.Length && (char.IsDigit(_input[_pos]) || _input[_pos] == '.'))
                    _pos++;
                var text = _input.Substring(start, _pos - start);
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    throw new ArgumentException($"Sayi parse edilemedi: {text}");
                Current = new Token(TokenType.Number, text, number);
                return;
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                var start = _pos;
                while (_pos < _input.Length)
                {
                    var c = _input[_pos];
                    if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                        break;
                    _pos++;
                }
                var text = _input.Substring(start, _pos - start);
                Current = new Token(TokenType.Identifier, text);
                return;
            }

            _pos++;
            Current = ch switch
            {
                '(' => new Token(TokenType.LeftParen, "("),
                ')' => new Token(TokenType.RightParen, ")"),
                ',' => new Token(TokenType.Comma, ","),
                '?' => new Token(TokenType.Question, "?"),
                ':' => new Token(TokenType.Colon, ":"),
                '+' or '-' or '*' or '/' => new Token(TokenType.Operator, ch.ToString()),
                '=' or '!' or '<' or '>' => ReadOperator(ch),
                _ => throw new ArgumentException($"Desteklenmeyen karakter: {ch}")
            };
        }

        public void Expect(TokenType type)
        {
            if (Current.Type != type)
                throw new ArgumentException($"Beklenen token: {type}, mevcut: {Current.Value}");
            Next();
        }

        private Token ReadOperator(char first)
        {
            if (_pos < _input.Length)
            {
                var second = _input[_pos];
                if ((first == '=' && second == '=') ||
                    (first == '!' && second == '=') ||
                    (first == '<' && second == '=') ||
                    (first == '>' && second == '='))
                {
                    _pos++;
                    return new Token(TokenType.Operator, $"{first}{second}");
                }
            }

            if (first is '<' or '>')
                return new Token(TokenType.Operator, first.ToString());

            throw new ArgumentException($"Geçersiz operatör: {first}");
        }

        private void SkipWhitespace()
        {
            while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
                _pos++;
        }
    }
}
