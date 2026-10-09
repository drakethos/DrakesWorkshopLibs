// Copied from Drakes Asset Forge (Forge/Format/Json/JsonValue.cs) so mods get .glb models from Libs alone; keep in step with it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DrakeModsLibs.Forge.Gltf;

internal enum JsonKind
{
    Null,
    Bool,
    Number,
    String,
    Array,
    Object
}

/// <summary>
/// Minimal JSON DOM. Lives here so the desktop app and the in-game runtime read packs identically
/// without shipping a JSON library into BepInEx.
/// </summary>
internal sealed class JsonValue
{
    public static readonly JsonValue Null = new(JsonKind.Null);

    private readonly List<JsonValue>? _items;
    private readonly List<string>? _keys;
    private readonly Dictionary<string, JsonValue>? _props;

    private JsonValue(JsonKind kind)
    {
        Kind = kind;
        if (kind == JsonKind.Array)
            _items = new List<JsonValue>();
        if (kind == JsonKind.Object)
        {
            _keys = new List<string>();
            _props = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
        }
    }

    public JsonValue(bool value) : this(JsonKind.Bool) => BoolValue = value;
    public JsonValue(double value) : this(JsonKind.Number) => NumberValue = value;
    public JsonValue(string value) : this(JsonKind.String) => StringValue = value;

    public JsonKind Kind { get; }
    public bool BoolValue { get; }
    public double NumberValue { get; }
    public string? StringValue { get; }

    public static JsonValue NewObject() => new(JsonKind.Object);
    public static JsonValue NewArray() => new(JsonKind.Array);

    public static implicit operator JsonValue(string value) => new(value);
    public static implicit operator JsonValue(double value) => new(value);
    public static implicit operator JsonValue(int value) => new(value);
    public static implicit operator JsonValue(bool value) => new(value);

    public bool IsNull => Kind == JsonKind.Null;
    public bool IsObject => Kind == JsonKind.Object;
    public bool IsArray => Kind == JsonKind.Array;

    public JsonValue? this[string key] =>
        _props != null && _props.TryGetValue(key, out var value) ? value : null;

    public IReadOnlyList<JsonValue> Items => _items ?? (IReadOnlyList<JsonValue>)System.Array.Empty<JsonValue>();

    public IEnumerable<KeyValuePair<string, JsonValue>> Properties
    {
        get
        {
            if (_keys == null)
                yield break;
            foreach (var key in _keys)
                yield return new KeyValuePair<string, JsonValue>(key, _props![key]);
        }
    }

    public int Count => _items?.Count ?? _keys?.Count ?? 0;

    public JsonValue Set(string key, JsonValue? value)
    {
        if (_props == null)
            throw new InvalidOperationException("Set is only valid on a JSON object.");
        if (value == null)
        {
            if (_props.Remove(key))
                _keys!.Remove(key);
            return this;
        }

        if (!_props.ContainsKey(key))
            _keys!.Add(key);
        _props[key] = value;
        return this;
    }

    public JsonValue Add(JsonValue value)
    {
        if (_items == null)
            throw new InvalidOperationException("Add is only valid on a JSON array.");
        _items.Add(value);
        return this;
    }

    public string? AsString() => Kind == JsonKind.String ? StringValue : null;
    public double? AsNumber() => Kind == JsonKind.Number ? NumberValue : null;
    public bool? AsBool() => Kind == JsonKind.Bool ? BoolValue : null;

    public static JsonValue Parse(string text) => new Parser(text).ParseDocument();

    public string ToJson(bool indented = true)
    {
        var sb = new StringBuilder();
        Write(sb, indented, 0);
        return sb.ToString();
    }

    public override string ToString() => ToJson(false);

    private void Write(StringBuilder sb, bool indented, int depth)
    {
        switch (Kind)
        {
            case JsonKind.Null:
                sb.Append("null");
                break;
            case JsonKind.Bool:
                sb.Append(BoolValue ? "true" : "false");
                break;
            case JsonKind.Number:
                sb.Append(FormatNumber(NumberValue));
                break;
            case JsonKind.String:
                WriteString(sb, StringValue!);
                break;
            case JsonKind.Array:
                WriteContainer(sb, indented, depth, '[', ']', _items!.Count, (i, d) => _items[i].Write(sb, indented, d), InlineArray());
                break;
            case JsonKind.Object:
                WriteContainer(sb, indented, depth, '{', '}', _keys!.Count, (i, d) =>
                {
                    WriteString(sb, _keys[i]);
                    sb.Append(indented ? ": " : ":");
                    _props![_keys[i]].Write(sb, indented, d);
                }, false);
                break;
        }
    }

    // Short arrays of plain numbers (vectors, colors) read better on one line.
    private bool InlineArray() =>
        _items!.Count <= 4 && _items.TrueForAll(v => v.Kind == JsonKind.Number);

    private static void WriteContainer(StringBuilder sb, bool indented, int depth, char open, char close, int count, Action<int, int> writeItem, bool inline)
    {
        sb.Append(open);
        if (count == 0)
        {
            sb.Append(close);
            return;
        }

        var pretty = indented && !inline;
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
                sb.Append(inline && indented ? ", " : ",");
            if (pretty)
                sb.Append('\n').Append(' ', (depth + 1) * 2);
            writeItem(i, depth + 1);
        }

        if (pretty)
            sb.Append('\n').Append(' ', depth * 2);
        sb.Append(close);
    }

    private static string FormatNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "0";
        if (Math.Abs(value) < 1e15 && Math.Abs(value % 1) < double.Epsilon)
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static void WriteString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
    }

    private sealed class Parser
    {
        private readonly string _text;
        private int _pos;

        public Parser(string text) => _text = text ?? string.Empty;

        public JsonValue ParseDocument()
        {
            SkipWhitespace();
            var value = ParseValue();
            SkipWhitespace();
            if (_pos < _text.Length)
                throw Error("Unexpected text after the JSON value");
            return value;
        }

        private JsonValue ParseValue()
        {
            SkipWhitespace();
            if (_pos >= _text.Length)
                throw Error("Unexpected end of JSON");

            var c = _text[_pos];
            switch (c)
            {
                case '{': return ParseObject();
                case '[': return ParseArray();
                case '"': return new JsonValue(ParseString());
                case 't': Expect("true"); return new JsonValue(true);
                case 'f': Expect("false"); return new JsonValue(false);
                case 'n': Expect("null"); return Null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                        return new JsonValue(ParseNumber());
                    throw Error($"Unexpected character '{c}'");
            }
        }

        private JsonValue ParseObject()
        {
            var obj = NewObject();
            _pos++;
            SkipWhitespace();
            if (Peek() == '}')
            {
                _pos++;
                return obj;
            }

            while (true)
            {
                SkipWhitespace();
                if (Peek() != '"')
                    throw Error("Expected a property name in quotes");
                var key = ParseString();
                SkipWhitespace();
                if (Peek() != ':')
                    throw Error($"Expected ':' after \"{key}\"");
                _pos++;
                obj.Set(key, ParseValue());
                SkipWhitespace();
                var next = Peek();
                _pos++;
                if (next == ',')
                    continue;
                if (next == '}')
                    return obj;
                _pos--;
                throw Error("Expected ',' or '}' in object");
            }
        }

        private JsonValue ParseArray()
        {
            var arr = NewArray();
            _pos++;
            SkipWhitespace();
            if (Peek() == ']')
            {
                _pos++;
                return arr;
            }

            while (true)
            {
                arr.Add(ParseValue());
                SkipWhitespace();
                var next = Peek();
                _pos++;
                if (next == ',')
                    continue;
                if (next == ']')
                    return arr;
                _pos--;
                throw Error("Expected ',' or ']' in array");
            }
        }

        private string ParseString()
        {
            _pos++;
            var sb = new StringBuilder();
            while (_pos < _text.Length)
            {
                var c = _text[_pos++];
                if (c == '"')
                    return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (_pos >= _text.Length)
                    break;
                var esc = _text[_pos++];
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (_pos + 4 > _text.Length)
                            throw Error("Bad \\u escape");
                        sb.Append((char)int.Parse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        _pos += 4;
                        break;
                    default:
                        throw Error($"Bad escape '\\{esc}'");
                }
            }

            throw Error("Unterminated string");
        }

        private double ParseNumber()
        {
            var start = _pos;
            while (_pos < _text.Length && "+-0123456789.eE".IndexOf(_text[_pos]) >= 0)
                _pos++;
            var raw = _text.Substring(start, _pos - start);
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw Error($"Bad number '{raw}'");
            return value;
        }

        private void Expect(string word)
        {
            if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0)
                throw Error($"Expected '{word}'");
            _pos += word.Length;
        }

        private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

        private void SkipWhitespace()
        {
            while (_pos < _text.Length)
            {
                var c = _text[_pos];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    _pos++;
                    continue;
                }

                // Allow // line comments so hand-edited recipes can carry notes.
                if (c == '/' && _pos + 1 < _text.Length && _text[_pos + 1] == '/')
                {
                    while (_pos < _text.Length && _text[_pos] != '\n')
                        _pos++;
                    continue;
                }

                break;
            }
        }

        private FormatException Error(string message)
        {
            if (_pos >= _text.Length)
                message = "Unexpected end of JSON (missing a closing bracket or brace?)";

            var line = 1;
            var col = 1;
            for (var i = 0; i < _pos && i < _text.Length; i++)
            {
                if (_text[i] == '\n')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
            }

            return new FormatException($"{message} (line {line}, column {col})");
        }
    }
}
