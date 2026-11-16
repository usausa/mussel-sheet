namespace MusselSheet.Internal;

using System.Buffers;

// Text of at most the given capacity, built on the stack when it fits and in a rented array when it does not
internal ref struct TextBuffer
{
    public const int StackLength = 256;

    private char[]? rented;

    private Span<char> chars;

    private int length;

    public TextBuffer(Span<char> stack, int capacity)
    {
        if (capacity <= stack.Length)
        {
            chars = stack;
        }
        else
        {
            rented = ArrayPool<char>.Shared.Rent(capacity);
            chars = rented;
        }
    }

    public void Append(char c) => chars[length++] = c;

    public void Append(scoped ReadOnlySpan<char> text)
    {
        text.CopyTo(chars[length..]);
        length += text.Length;
    }

    public string ToStringAndClear()
    {
        var text = new string(chars[..length]);
        if (rented is not null)
        {
            ArrayPool<char>.Shared.Return(rented);
            rented = null;
        }

        return text;
    }
}
