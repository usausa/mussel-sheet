namespace MusselSheet.Internal;

using System.Buffers;

// Text built on the stack when it fits, and in a rented array when it does not
internal ref struct TextBuffer
{
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

    public void Append(scoped ReadOnlySpan<char> text)
    {
        if (length + text.Length > chars.Length)
        {
            Grow(text.Length);
        }

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

    private void Grow(int more)
    {
        var next = ArrayPool<char>.Shared.Rent(Math.Max(chars.Length * 2, length + more));
        chars[..length].CopyTo(next);
        if (rented is not null)
        {
            ArrayPool<char>.Shared.Return(rented);
        }

        rented = next;
        chars = next;
    }
}
