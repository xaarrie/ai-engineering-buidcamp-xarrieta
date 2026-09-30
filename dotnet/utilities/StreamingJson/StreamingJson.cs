using System.Text;
using System.Text.Json;

namespace StreamingJson;

// Module lesson: Feed advances an incremental UTF-8 state machine and Complete closes its lifecycle. Escape and surrogate state survives chunk boundaries; frames track nested structure and captures retain bytes for completed values. Callback order is field start, string chunks, completed value, array item, then document completion. Errors preserve the byte offset and transition once.

/// <summary>Lifecycle state of an incremental parser.</summary>
public enum StreamingJsonParserState
{
    /// <summary>Accepts bytes through <see cref="StreamingJsonParser.Feed"/>.</summary>
    Reading,
    /// <summary><see cref="StreamingJsonParser.Complete"/> validated one complete document.</summary>
    Complete,
    /// <summary>A terminal syntax, UTF-8, callback, or completion error was reported.</summary>
    Error
}

/// <summary>Contains one decoded string fragment; fragments may split across UTF-8 input chunks.</summary>
/// <param name="FieldName">Owning object field, or null for non-property strings.</param>
/// <param name="Depth">Container nesting depth when emitted.</param>
public sealed record JsonStringChunk(string? FieldName, int Depth, string Value);

/// <summary>Contains a completed object property value parsed as a cloned <see cref="JsonElement"/>.</summary>
public sealed record JsonFieldValue(string Name, JsonElement Value);

/// <summary>Contains a completed array item and its nesting depth.</summary>
public sealed record JsonArrayItem(int Depth, JsonElement Value);

/// <summary>Describes the terminal parser failure and zero-based byte offset at which it was observed.</summary>
public sealed record StreamingJsonError(string Message, long ByteOffset);

/// <summary>Receives parser callbacks in field-start, string-chunk, completed-value, array-item, and terminal order.</summary>
public interface IStreamingJsonHandler
{
    /// <summary>Called when an object property name has been decoded and its value is expected next.</summary>
    void OnFieldStart(string name);
    /// <summary>Called for decoded string text, possibly multiple times per value.</summary>
    void OnStringChunk(JsonStringChunk chunk);
    /// <summary>Called after a property value, including nested captures, is complete.</summary>
    void OnFieldValue(JsonFieldValue field);
    /// <summary>Called after an array item has been fully validated and cloned.</summary>
    void OnArrayItem(JsonArrayItem item);
    /// <summary>Called once after <see cref="StreamingJsonParser.Complete"/> succeeds.</summary>
    void OnDocumentComplete();
    /// <summary>Called once when parsing becomes terminal <see cref="StreamingJsonParserState.Error"/>.</summary>
    void OnError(StreamingJsonError error);
}

/// <summary>Parses one JSON document incrementally from arbitrary UTF-8 byte chunks.</summary>
/// <remarks>Feed is inert after Complete or Error. Escape, surrogate, UTF-8, nesting, and value-capture state survives chunk boundaries.</remarks>
public sealed class StreamingJsonParser
{
    private readonly IStreamingJsonHandler _handler;
    private readonly Stack<ContainerFrame> _containers = new();
    private readonly Stack<ValueCapture> _values = new();
    private readonly StringBuilder _propertyName = new();
    private bool _inString;
    private bool _stringIsPropertyName;
    private EscapeMode _escapeMode;
    private int _unicodeValue;
    private int _unicodeDigits;
    private char? _pendingHighSurrogate;
    private int _utf8Expected;
    private int _utf8CodePoint;
    private int _utf8Minimum;
    private bool _rootComplete;
    private long _offset;

/// <summary>Creates a parser with the callback sink used for every lifecycle event.</summary>
    public StreamingJsonParser(IStreamingJsonHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>Gets the current state: Reading, Complete, or terminal Error.</summary>
    public StreamingJsonParserState State { get; private set; } = StreamingJsonParserState.Reading;

    /// <summary>Gets the terminal error, or null while reading and after successful completion.</summary>
    public StreamingJsonError? Error { get; private set; }

/// <summary>Consumes a UTF-8 byte chunk; incomplete strings, escapes, surrogate pairs, and values remain pending for the next chunk.</summary>
    /// <remarks>Each processed byte advances the error offset. Calls after a terminal state are ignored.</remarks>
    public void Feed(ReadOnlySpan<byte> chunk)
    {
        if (State != StreamingJsonParserState.Reading)
        {
            return;
        }

        try
        {
            foreach (var value in chunk)
            {
                ProcessByte(value);
                _offset++;
            }
        }
        catch (StreamingJsonParseException exception)
        {
            Fail(exception.Message);
        }
        catch (Exception exception)
        {
            Fail($"Handler or parser failure: {exception.Message}");
        }
    }

/// <summary>Closes input, completes a pending primitive when valid, and requires exactly one complete JSON root.</summary>
    /// <remarks>Success emits OnDocumentComplete; incomplete structure or decoding emits one terminal OnError with the current byte offset.</remarks>
    public void Complete()
    {
        if (State != StreamingJsonParserState.Reading)
        {
            return;
        }

        try
        {
            if (_inString || _escapeMode != EscapeMode.None || _utf8Expected != 0 || _pendingHighSurrogate is not null)
            {
                throw new StreamingJsonParseException("The JSON string ended before it was complete.");
            }

            if (_values.TryPeek(out var value) && value.Kind == ValueKind.Primitive)
            {
                CompleteValue();
            }

            if (_containers.Count != 0 || _values.Count != 0 || !_rootComplete)
            {
                throw new StreamingJsonParseException("The JSON document ended before its structure was complete.");
            }

            State = StreamingJsonParserState.Complete;
            _handler.OnDocumentComplete();
        }
        catch (StreamingJsonParseException exception)
        {
            Fail(exception.Message);
        }
        catch (Exception exception)
        {
            Fail($"Handler or parser failure: {exception.Message}");
        }
    }

    private void ProcessByte(byte value)
    {
        if (_inString)
        {
            AppendToOpenValues(value, includeTop: true);
            ProcessStringByte(value);
            return;
        }

        if (_values.TryPeek(out var activeValue) && activeValue.Kind == ValueKind.Primitive)
        {
            if (IsWhitespace(value))
            {
                CompleteValue();
                AppendToOpenValues(value, includeTop: true);
                return;
            }

            if (value is (byte)',' or (byte)']' or (byte)'}')
            {
                CompleteValue();
                ProcessByte(value);
                return;
            }

            AppendToOpenValues(value, includeTop: true);
            return;
        }

        if (IsWhitespace(value))
        {
            AppendToOpenValues(value, includeTop: true);
            return;
        }

        if (_rootComplete)
        {
            throw new StreamingJsonParseException("A JSON document may contain only one root value.");
        }

        switch (value)
        {
            case (byte)'"':
                StartString();
                break;
            case (byte)'{':
                StartContainer(ContainerKind.Object);
                break;
            case (byte)'[':
                StartContainer(ContainerKind.Array);
                break;
            case (byte)'}':
                CloseContainer(ContainerKind.Object);
                break;
            case (byte)']':
                CloseContainer(ContainerKind.Array);
                break;
            case (byte)':':
                ConsumeColon();
                break;
            case (byte)',':
                ConsumeComma();
                break;
            default:
                StartPrimitive(value);
                break;
        }
    }

    private void StartString()
    {
        if (IsExpectingPropertyName())
        {
            AppendToOpenValues((byte)'"', includeTop: true);
            _inString = true;
            _stringIsPropertyName = true;
            _propertyName.Clear();
            ResetStringDecoder();
            return;
        }

        EnsureExpectingValue();
        BeginValue(ValueKind.String);
        AppendToOpenValues((byte)'"', includeTop: true);
        _inString = true;
        _stringIsPropertyName = false;
        ResetStringDecoder();
    }

    private void StartContainer(ContainerKind kind)
    {
        EnsureExpectingValue();
        BeginValue(ValueKind.Composite);
        AppendToOpenValues(kind == ContainerKind.Object ? (byte)'{' : (byte)'[', includeTop: true);
        _containers.Push(new ContainerFrame(kind));
    }

    private void StartPrimitive(byte value)
    {
        EnsureExpectingValue();
        BeginValue(ValueKind.Primitive);
        AppendToOpenValues(value, includeTop: true);
    }

    private void CloseContainer(ContainerKind kind)
    {
        if (_containers.Count == 0 || _containers.Peek().Kind != kind)
        {
            throw new StreamingJsonParseException("The JSON closing delimiter does not match an open container.");
        }

        var frame = _containers.Peek();
        var allowed = kind == ContainerKind.Object
            ? frame.State is ContainerState.ObjectExpectPropertyOrEnd or ContainerState.ExpectCommaOrEnd
            : frame.State is ContainerState.ArrayExpectValueOrEnd or ContainerState.ExpectCommaOrEnd;

        if (!allowed)
        {
            throw new StreamingJsonParseException("The JSON container closed while a property or value was incomplete.");
        }

        AppendToOpenValues(kind == ContainerKind.Object ? (byte)'}' : (byte)']', includeTop: true);
        _containers.Pop();
        CompleteValue();
    }

    private void ConsumeColon()
    {
        if (_containers.Count == 0 || _containers.Peek().State != ContainerState.ObjectExpectColon)
        {
            throw new StreamingJsonParseException("A colon must follow an object property name.");
        }

        AppendToOpenValues((byte)':', includeTop: true);
        _containers.Peek().State = ContainerState.ObjectExpectValue;
    }

    private void ConsumeComma()
    {
        if (_containers.Count == 0 || _containers.Peek().State != ContainerState.ExpectCommaOrEnd)
        {
            throw new StreamingJsonParseException("A comma must follow a completed object property or array item.");
        }

        AppendToOpenValues((byte)',', includeTop: true);
        _containers.Peek().State = _containers.Peek().Kind == ContainerKind.Object
            ? ContainerState.ObjectExpectPropertyOrEnd
            : ContainerState.ArrayExpectValueOrEnd;
    }

    private void ProcessStringByte(byte value)
    {
        if (_escapeMode == EscapeMode.Unicode)
        {
            var hex = HexValue(value);
            if (hex < 0)
            {
                throw new StreamingJsonParseException("A Unicode escape must contain four hexadecimal digits.");
            }

            _unicodeValue = (_unicodeValue << 4) | hex;
            _unicodeDigits++;
            if (_unicodeDigits == 4)
            {
                _escapeMode = EscapeMode.None;
                AcceptUnicodeEscape((char)_unicodeValue);
            }

            return;
        }

        if (_escapeMode == EscapeMode.Simple)
        {
            _escapeMode = EscapeMode.None;
            switch (value)
            {
                case (byte)'"': EmitDecodedCharacter('"'); break;
                case (byte)'\\': EmitDecodedCharacter('\\'); break;
                case (byte)'/': EmitDecodedCharacter('/'); break;
                case (byte)'b': EmitDecodedCharacter('\b'); break;
                case (byte)'f': EmitDecodedCharacter('\f'); break;
                case (byte)'n': EmitDecodedCharacter('\n'); break;
                case (byte)'r': EmitDecodedCharacter('\r'); break;
                case (byte)'t': EmitDecodedCharacter('\t'); break;
                case (byte)'u':
                    _escapeMode = EscapeMode.Unicode;
                    _unicodeDigits = 0;
                    _unicodeValue = 0;
                    break;
                default: throw new StreamingJsonParseException("The JSON string contains an invalid escape sequence.");
            }

            return;
        }

        if (value == (byte)'"')
        {
            if (_pendingHighSurrogate is not null)
            {
                throw new StreamingJsonParseException("The JSON string contains an unpaired high surrogate.");
            }

            _inString = false;
            if (_stringIsPropertyName)
            {
                var frame = _containers.Peek();
                frame.PropertyName = _propertyName.ToString();
                frame.State = ContainerState.ObjectExpectColon;
                _handler.OnFieldStart(frame.PropertyName);
            }
            else
            {
                CompleteValue();
            }

            return;
        }

        if (value == (byte)'\\')
        {
            _escapeMode = EscapeMode.Simple;
            return;
        }

        if (value < 0x20)
        {
            throw new StreamingJsonParseException("JSON strings cannot contain unescaped control characters.");
        }

        if (_pendingHighSurrogate is not null)
        {
            throw new StreamingJsonParseException("The JSON string contains an unpaired high surrogate.");
        }

        AcceptUtf8Byte(value);
    }

    private void AcceptUnicodeEscape(char value)
    {
        if (char.IsHighSurrogate(value))
        {
            if (_pendingHighSurrogate is not null)
            {
                throw new StreamingJsonParseException("The JSON string contains consecutive high surrogates.");
            }

            _pendingHighSurrogate = value;
            return;
        }

        if (char.IsLowSurrogate(value))
        {
            if (_pendingHighSurrogate is null)
            {
                throw new StreamingJsonParseException("The JSON string contains an unpaired low surrogate.");
            }

            EmitDecodedText(new string([_pendingHighSurrogate.Value, value]));
            _pendingHighSurrogate = null;
            return;
        }

        if (_pendingHighSurrogate is not null)
        {
            throw new StreamingJsonParseException("The JSON string contains an unpaired high surrogate.");
        }

        EmitDecodedCharacter(value);
    }

    private void AcceptUtf8Byte(byte value)
    {
        if (_utf8Expected == 0)
        {
            if (value < 0x80)
            {
                EmitDecodedCharacter((char)value);
                return;
            }

            if (value is >= 0xC2 and <= 0xDF)
            {
                _utf8Expected = 1;
                _utf8CodePoint = value & 0x1F;
                _utf8Minimum = 0x80;
                return;
            }

            if (value is >= 0xE0 and <= 0xEF)
            {
                _utf8Expected = 2;
                _utf8CodePoint = value & 0x0F;
                _utf8Minimum = 0x800;
                return;
            }

            if (value is >= 0xF0 and <= 0xF4)
            {
                _utf8Expected = 3;
                _utf8CodePoint = value & 0x07;
                _utf8Minimum = 0x10000;
                return;
            }

            throw new StreamingJsonParseException("The JSON string contains invalid UTF-8.");
        }

        if (value is < 0x80 or > 0xBF)
        {
            throw new StreamingJsonParseException("The JSON string contains invalid UTF-8 continuation bytes.");
        }

        _utf8CodePoint = (_utf8CodePoint << 6) | (value & 0x3F);
        _utf8Expected--;
        if (_utf8Expected != 0)
        {
            return;
        }

        if (_utf8CodePoint < _utf8Minimum || _utf8CodePoint is > 0x10FFFF or >= 0xD800 and <= 0xDFFF)
        {
            throw new StreamingJsonParseException("The JSON string contains an invalid UTF-8 code point.");
        }

        EmitDecodedText(char.ConvertFromUtf32(_utf8CodePoint));
    }

    private void EmitDecodedCharacter(char value) => EmitDecodedText(value.ToString());

    private void EmitDecodedText(string value)
    {
        if (_stringIsPropertyName)
        {
            _propertyName.Append(value);
            return;
        }

        _handler.OnStringChunk(new JsonStringChunk(_values.Peek().FieldName, _containers.Count, value));
    }

    private void CompleteValue()
    {
        if (_values.Count == 0)
        {
            throw new StreamingJsonParseException("The parser completed a value that was never started.");
        }

        var capture = _values.Pop();
        JsonElement value = default;
        try
        {
            if (capture.CapturesBytes)
            {
                var bytes = capture.Bytes.ToArray();
                var reader = new Utf8JsonReader(bytes, isFinalBlock: true, state: default);
                if (!reader.Read())
                {
                    throw new JsonException("The completed value is empty.");
                }

                while (reader.Read())
                {
                }

                if (reader.BytesConsumed != bytes.Length)
                {
                    throw new JsonException("The completed value has trailing content.");
                }

                using var document = JsonDocument.Parse(bytes);
                value = document.RootElement.Clone();
            }
        }
        catch (JsonException exception)
        {
            throw new StreamingJsonParseException($"Invalid JSON value: {exception.Message}");
        }

        if (capture.FieldName is not null)
        {
            _handler.OnFieldValue(new JsonFieldValue(capture.FieldName, value));
        }

        if (capture.IsArrayItem)
        {
            _handler.OnArrayItem(new JsonArrayItem(capture.Depth, value));
        }

        if (capture.ParentKind is null)
        {
            _rootComplete = true;
            return;
        }

        var parent = _containers.Peek();
        parent.State = ContainerState.ExpectCommaOrEnd;
    }

    private void BeginValue(ValueKind kind)
    {
        ContainerKind? parentKind = null;
        string? fieldName = null;
        var isArrayItem = false;
        var depth = _containers.Count;

        if (_containers.TryPeek(out var parent))
        {
            parentKind = parent.Kind;
            fieldName = parent.Kind == ContainerKind.Object ? parent.PropertyName : null;
            isArrayItem = parent.Kind == ContainerKind.Array;
        }

        var capturesBytes = parentKind is not null || kind != ValueKind.Composite;
        _values.Push(new ValueCapture(kind, parentKind, fieldName, isArrayItem, depth, capturesBytes));
    }

    private void EnsureExpectingValue()
    {
        if (_containers.Count == 0)
        {
            if (_rootComplete || _values.Count != 0)
            {
                throw new StreamingJsonParseException("A JSON document may contain only one root value.");
            }

            return;
        }

        var state = _containers.Peek().State;
        if (state is not ContainerState.ObjectExpectValue and not ContainerState.ArrayExpectValueOrEnd)
        {
            throw new StreamingJsonParseException("The JSON value appeared where a delimiter or property name was expected.");
        }
    }

    private bool IsExpectingPropertyName() =>
        _containers.TryPeek(out var frame) && frame.State == ContainerState.ObjectExpectPropertyOrEnd;

    private void AppendToOpenValues(byte value, bool includeTop)
    {
        var values = _values.ToArray();
        for (var index = values.Length - 1; index >= 0; index--)
        {
            if (!includeTop && index == 0)
            {
                continue;
            }

            if (values[index].CapturesBytes)
            {
                values[index].Bytes.Add(value);
            }
        }
    }

    private void ResetStringDecoder()
    {
        _escapeMode = EscapeMode.None;
        _unicodeDigits = 0;
        _unicodeValue = 0;
        _pendingHighSurrogate = null;
        _utf8Expected = 0;
        _utf8CodePoint = 0;
        _utf8Minimum = 0;
    }

    private void Fail(string message)
    {
        if (State == StreamingJsonParserState.Error)
        {
            return;
        }

        Error = new StreamingJsonError(message, _offset);
        State = StreamingJsonParserState.Error;
        _handler.OnError(Error);
    }

    private static bool IsWhitespace(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    private static int HexValue(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - (byte)'0',
        >= (byte)'a' and <= (byte)'f' => value - (byte)'a' + 10,
        >= (byte)'A' and <= (byte)'F' => value - (byte)'A' + 10,
        _ => -1
    };

    private sealed class ContainerFrame(ContainerKind kind)
    {
        public ContainerKind Kind { get; } = kind;
        public ContainerState State { get; set; } = kind == ContainerKind.Object
            ? ContainerState.ObjectExpectPropertyOrEnd
            : ContainerState.ArrayExpectValueOrEnd;
        public string? PropertyName { get; set; }
    }

    private sealed class ValueCapture(ValueKind kind, ContainerKind? parentKind, string? fieldName, bool isArrayItem, int depth, bool capturesBytes)
    {
        public ValueKind Kind { get; } = kind;
        public ContainerKind? ParentKind { get; } = parentKind;
        public string? FieldName { get; } = fieldName;
        public bool IsArrayItem { get; } = isArrayItem;
        public int Depth { get; } = depth;
        public bool CapturesBytes { get; } = capturesBytes;
        public List<byte> Bytes { get; } = [];
    }

    private enum ContainerKind { Object, Array }
    private enum ValueKind { String, Primitive, Composite }
    private enum EscapeMode { None, Simple, Unicode }
    private enum ContainerState { ObjectExpectPropertyOrEnd, ObjectExpectColon, ObjectExpectValue, ArrayExpectValueOrEnd, ExpectCommaOrEnd }
    private sealed class StreamingJsonParseException(string message) : Exception(message);
}
