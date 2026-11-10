using System.Text;

namespace FocusLock.Core.Detection.Steam;

/// <summary>Tolerant parser for Valve's KeyValues text format. Broken input yields fewer entries, never an exception.</summary>
public static class KeyValuesParser
{
    public static KeyValuesNode Parse(string text)
    {
        var root = new KeyValuesNode(string.Empty);
        ParseInto(root, new Tokenizer(text));
        return root;
    }

    private static void ParseInto(KeyValuesNode parent, Tokenizer tokenizer)
    {
        while (true)
        {
            var key = tokenizer.Next();
            if (key.Kind is TokenKind.End or TokenKind.Close)
            {
                return;
            }

            if (key.Kind == TokenKind.Open)
            {
                continue; // stray brace: skip it
            }

            var value = tokenizer.Next();
            switch (value.Kind)
            {
                case TokenKind.Open:
                    var block = new KeyValuesNode(key.Text);
                    ParseInto(block, tokenizer);
                    parent.Children.Add(block);
                    break;
                case TokenKind.Text:
                    parent.Children.Add(new KeyValuesNode(key.Text) { Value = value.Text });
                    break;
                default:
                    parent.Children.Add(new KeyValuesNode(key.Text) { Value = string.Empty });
                    return;
            }
        }
    }

    private enum TokenKind
    {
        Text,
        Open,
        Close,
        End,
    }

    private readonly record struct Token(TokenKind Kind, string Text);

    private sealed class Tokenizer
    {
        private readonly string _text;
        private int _position;

        public Tokenizer(string text)
        {
            _text = text;
        }

        public Token Next()
        {
            SkipWhitespaceAndComments();
            if (_position >= _text.Length)
            {
                return new Token(TokenKind.End, string.Empty);
            }

            var current = _text[_position];
            if (current == '{')
            {
                _position++;
                return new Token(TokenKind.Open, "{");
            }

            if (current == '}')
            {
                _position++;
                return new Token(TokenKind.Close, "}");
            }

            return current == '"' ? ReadQuoted() : ReadBare();
        }

        private Token ReadQuoted()
        {
            _position++; // opening quote
            var builder = new StringBuilder();
            while (_position < _text.Length)
            {
                var current = _text[_position++];
                if (current == '"')
                {
                    break;
                }

                if (current == '\\' && _position < _text.Length)
                {
                    var escaped = _text[_position++];
                    builder.Append(escaped switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        _ => escaped,
                    });
                    continue;
                }

                builder.Append(current);
            }

            return new Token(TokenKind.Text, builder.ToString());
        }

        private Token ReadBare()
        {
            var start = _position;
            while (_position < _text.Length)
            {
                var current = _text[_position];
                if (char.IsWhiteSpace(current) || current is '{' or '}' or '"')
                {
                    break;
                }

                _position++;
            }

            return new Token(TokenKind.Text, _text[start.._position]);
        }

        private void SkipWhitespaceAndComments()
        {
            while (_position < _text.Length)
            {
                if (char.IsWhiteSpace(_text[_position]))
                {
                    _position++;
                }
                else if (_text[_position] == '/' && _position + 1 < _text.Length && _text[_position + 1] == '/')
                {
                    while (_position < _text.Length && _text[_position] != '\n')
                    {
                        _position++;
                    }
                }
                else
                {
                    return;
                }
            }
        }
    }
}
